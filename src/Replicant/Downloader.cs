using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Replicant
{
    public partial class Download :
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


        public async Task<Result> DownloadFile(string uri, bool useStaleOnError = false, Action<HttpRequestMessage>? messageCallback = null, CancellationToken token = default)
        {
            var hash = Hash.Compute(uri);
            var contentFile = new DirectoryInfo(directory)
                .GetFiles($"{hash}_*.bin")
                .OrderBy(x => x.LastWriteTime)
                .FirstOrDefault();

            var now = DateTimeOffset.UtcNow;

            if (contentFile == null)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                messageCallback?.Invoke(request);
                using var response = await client.SendAsync(request, token);
                response.EnsureSuccessStatusCode();
                return await AddItem(response, now, hash, CacheStatus.Miss);
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
                messageCallback?.Invoke(request);
                request.Headers.IfModifiedSince = fileTimestamp.LastModified;
                if (fileTimestamp.ETag != null)
                {
                    request.Headers.TryAddWithoutValidation("If-None-Match", $"\"{fileTimestamp.ETag}\"");
                }

                using var response = await client.SendAsync(request, token);

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    var meta = await ReadMeta(metaFile);
                    return BuildResult(meta, contentPath, CacheStatus.Hit);
                }

                if (!response.IsSuccessStatusCode)
                {
                    if (useStaleOnError)
                    {
                        var meta = await ReadMeta(metaFile);
                        return BuildResult(meta, contentPath, CacheStatus.Hit);
                    }

                    response.EnsureSuccessStatusCode();
                }

                File.Delete(metaFile);
                File.Delete(contentPath);
                return await AddItem(response, now, hash, CacheStatus.Miss);
            }
        }

        public Task AddItem(string uri, HttpResponseMessage response)
        {
            Guard.AgainstNull(response.Content, nameof(response.Content));
            var now = DateTime.UtcNow;
            var hash = Hash.Compute(uri);

            return AddItem(response, now, hash, CacheStatus.Miss);
        }

        async Task<Result> AddItem(HttpResponseMessage response, DateTimeOffset now, string hash, CacheStatus status)
        {
            var expiry = response.GetExpiry(now);
            string? etagValue = null;
            if (response.TryGetETag(out var weak, out var etag))
            {
                if (weak.Value)
                {
                    etagValue = $"W{etag}";
                }
                else
                {
                    etagValue = $"S{etag}";
                }
            }

            var lastModified = response.GetLastModified(now);

            var contentFile = Path.Combine(directory, $"{hash}_{lastModified:yyyy-MM-ddTHHmmss}_{etagValue}.bin");
            await WriteContent(contentFile, response, expiry);
            return new(contentFile, status, response.Headers, response.Content.Headers);
        }

        static async Task WriteContent(string newContentFile, HttpResponseMessage response, DateTimeOffset? webExpiry, CancellationToken token = default)
        {
            var metaFile = Path.ChangeExtension(newContentFile, ".json");
            await using var httpStream = await response.Content.ReadAsStreamAsync(token);
            //TODO: should write these to temp files then copy them. then we can  pass token
            await using (var contentFileStream = FileEx.OpenWrite(newContentFile))
            {
                // ReSharper disable once MethodSupportsCancellation
                await httpStream.CopyToAsync(contentFileStream);
            }

            await using (var metaFileStream = FileEx.OpenWrite(metaFile))
            {
                var meta = new MetaData(response.Headers, response.Content.Headers);
                // ReSharper disable once MethodSupportsCancellation
                await JsonSerializer.SerializeAsync(metaFileStream, meta);
            }

            if (webExpiry == null)
            {
                File.SetLastWriteTimeUtc(newContentFile, FileEx.MinFileDate);
            }
            else
            {
                File.SetLastWriteTimeUtc(newContentFile, webExpiry.Value.UtcDateTime);
            }
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