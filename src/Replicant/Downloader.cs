using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Replicant
{
    public class Download :
        IAsyncDisposable,
        IDisposable
    {
        string directory;
        int maxEntries;
        HttpClient client;
        Timer timer;
        static TimeSpan purgeInterval = TimeSpan.FromMinutes(10);
        static TimeSpan ignoreTimeSpan = TimeSpan.FromMilliseconds(-1);

        public Download(string directory, HttpClient? client = null, int maxEntries = 1000)
        {
            Guard.AgainstNullOrEmpty(directory, nameof(directory));
            if (maxEntries < 100)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEntries), "maxEntries must be greater than 100");
            }

            this.directory = directory;
            this.maxEntries = maxEntries;

            if (client == null)
            {
                this.client = new()
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };
            }
            else
            {
                this.client = client;
            }

            Directory.CreateDirectory(directory);

            timer = new Timer(_ => PurgeOld(), null, ignoreTimeSpan, purgeInterval);
        }

        void PurgeOld()
        {
            timer.Change(ignoreTimeSpan, ignoreTimeSpan);
            try
            {
                foreach (var file in new DirectoryInfo(directory)
                    .GetFiles("*.bin")
                    .OrderByDescending(x => x.LastAccessTime)
                    .Skip(maxEntries))
                {
                    file.Delete();
                    File.Delete(Path.ChangeExtension(file.FullName, "json"));
                }
            }
            finally
            {
                timer.Change(purgeInterval, ignoreTimeSpan);
            }
        }

        public async Task<Result> DownloadFile(string uri, bool useStaleOnError = false)
        {
            var hash = Hash.Compute(uri);
            var contentFile = new DirectoryInfo(directory)
                .GetFiles($"{hash}_*.bin")
                .OrderBy(x => x.LastWriteTime)
                .FirstOrDefault();

            var now = DateTime.UtcNow;

            if (contentFile == null)
            {
                using var response = await client.GetAsync(uri);
                response.EnsureSuccessStatusCode();
                var webExpiry = response.GetExpiry(now);
                var webEtag = response.GetETag();
                var webLastModified = response.GetLastModified(now);
                var newContentFile = Path.Combine(directory, $"{hash}_{webLastModified:yyyy-MM-ddTHHmmss}_{webEtag?.Trim('"')}.bin");
                await WriteContent(newContentFile, response, webExpiry);

                return new(newContentFile, CacheStatus.Miss, response.Headers, response.Content.Headers);
            }
            else
            {
                var contentPath = contentFile.FullName;
                var metaFile = Path.ChangeExtension(contentPath, ".json");
                var fileTimestamp = Timestamp.Get(contentPath);
                if (fileTimestamp.Expiry > now)
                {
                    var meta = await ReadMeta(metaFile);
                    return BuildResult(meta, contentPath, CacheStatus.Hit);
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.IfModifiedSince = fileTimestamp.LastModified;
                if (fileTimestamp.ETag != null)
                {
                    request.Headers.TryAddWithoutValidation("If-None-Match", $"\"{fileTimestamp.ETag}\"");
                }

                using var response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode &&
                    response.StatusCode != HttpStatusCode.NotModified)
                {
                    response.EnsureSuccessStatusCode();
                }


                var webExpiry = response.GetExpiry(now);
                var webEtag = response.GetETag()?.Trim('"');
                var webLastModified = response.GetLastModified(now);

                if (fileTimestamp.LastModified == webLastModified)
                {
                    var meta = await ReadMeta(metaFile);
                    return BuildResult(meta, contentPath, CacheStatus.Hit);
                }
                if (webEtag != null &&
                    fileTimestamp.ETag != null &&
                    fileTimestamp.ETag == webEtag)
                {
                    var meta = await ReadMeta(metaFile);
                    return BuildResult(meta, contentPath, CacheStatus.Hit);
                }

                File.Delete(metaFile);
                File.Delete(contentPath);
                var newContentFile = Path.Combine(directory, $"{hash}_{webLastModified:yyyy-MM-ddTHHmmss}_{webEtag?.Trim('"')}.bin");
                await WriteContent(newContentFile, response, webExpiry);

                return new(newContentFile, CacheStatus.Miss, response.Headers, response.Content.Headers);
            }
        }

        static async Task WriteContent(string newContentFile, HttpResponseMessage response, DateTime? webExpiry)
        {
            var newMetaFile = Path.ChangeExtension(newContentFile, ".json");
            await using var httpStream = await response.Content.ReadAsStreamAsync();
            await using (var contentFileStream = FileEx.OpenWrite(newContentFile))
            {
                await httpStream.CopyToAsync(contentFileStream);
            }

            await using (var metaFileStream = FileEx.OpenWrite(newMetaFile))
            {
                var meta = new MetaData(response.Headers, response.Content.Headers);
                await JsonSerializer.SerializeAsync(metaFileStream, meta);
            }

            File.SetLastWriteTimeUtc(newContentFile, webExpiry.GetValueOrDefault(FileEx.MinFileDate));
        }

        static Result BuildResult(MetaData metaData, string contentFile, CacheStatus cacheStatus)
        {
            var message = new HttpResponseMessage();
            message.Headers.AddRange(metaData.ResponseHeaders);
            message.Content.Headers.AddRange(metaData.ContentHeaders);
            return new(contentFile, cacheStatus, message.Headers, message.Content.Headers);
        }

        static async Task<MetaData> ReadMeta(string path)
        {
            await using var stream = FileEx.OpenRead(path);
            return (await JsonSerializer.DeserializeAsync<MetaData>(stream))!;
        }

        public async Task<string> String(string uri, bool useStaleOnError = false)
        {
            var result = await DownloadFile(uri,useStaleOnError);
            return await File.ReadAllTextAsync(result.Path);
        }

        public async Task<byte[]> Bytes(string uri, bool useStaleOnError = false)
        {
            var result = await DownloadFile(uri,useStaleOnError);
            return await File.ReadAllBytesAsync(result.Path);
        }

        public async Task<Stream> Stream(string uri, bool useStaleOnError = false)
        {
            var result = await DownloadFile(uri,useStaleOnError);
            return File.OpenRead(result.Path);
        }

        public async Task ToStream(string uri, Stream stream, bool useStaleOnError = false)
        {
            var result = await DownloadFile(uri,useStaleOnError);
            await using var fileStream = FileEx.OpenRead(result.Path);
            await fileStream.CopyToAsync(stream);
        }

        public async Task ToFile(string uri, string path, bool useStaleOnError = false)
        {
            var result = await DownloadFile(uri,useStaleOnError);
            File.Copy(result.Path, path, true);
        }

        public void Purge()
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                File.Delete(file);
            }
        }

        public void Dispose()
        {
            client.Dispose();
            timer.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            client.Dispose();
            return timer.DisposeAsync();
        }
    }

}