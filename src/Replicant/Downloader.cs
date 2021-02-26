using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                    .GetFiles()
                    .OrderByDescending(x => x.LastAccessTime)
                    .Skip(maxEntries))
                {
                    file.Delete();
                }
            }
            finally
            {
                timer.Change(purgeInterval, ignoreTimeSpan);
            }
        }

        public async Task<Result> DownloadFile(string uri)
        {
            var hash = Hash.Compute(uri);
            var contentFile = Path.Combine(directory, $"{hash}.bin");
            var metaFile = Path.Combine(directory, $"{hash}.json");

            if (File.Exists(contentFile))
            {
                var fileTimestamp = Timestamp.Get(contentFile);
                if (fileTimestamp.Expiry > DateTime.UtcNow)
                {
                    var meta = await ReadMeta(metaFile);
                    return BuildResult(meta, contentFile, CacheStatus.Hit);
                }
            }

            using var response = await client.GetAsync(uri);
            var webTimestamp = Timestamp.Get(response);
            if (File.Exists(contentFile))
            {
                var fileTimestamp = Timestamp.Get(contentFile);
                if (fileTimestamp.LastModified == webTimestamp.LastModified)
                {
                    var meta = await ReadMeta(metaFile);
                    return BuildResult(meta, contentFile, CacheStatus.Hit);
                }

                File.Delete(metaFile);
                File.Delete(contentFile);
            }

            await using var httpStream = await response.Content.ReadAsStreamAsync();
            await using (var contentFileStream = FileEx.OpenWrite(contentFile))
            await using (var metaFileStream = FileEx.OpenWrite(metaFile))
            {
                await httpStream.CopyToAsync(contentFileStream);
                var meta = new MetaData(response.Headers, response.Content.Headers);
                await JsonSerializer.SerializeAsync(metaFileStream, meta);
            }

            webTimestamp = Timestamp.Get(response);

            Timestamp.Set(contentFile, webTimestamp);
            return new(contentFile, CacheStatus.Miss, response.Headers, response.Content.Headers);
        }

        static Result BuildResult(MetaData metaData, string contentFile, CacheStatus cacheStatus)
        {
            var message = new HttpResponseMessage();
            message.Headers.AddRange(metaData.ResponseHeaders);
            message.Content.Headers.AddRange(metaData.ContentHeaders);
            return new(contentFile, cacheStatus, message.Headers, message.Content.Headers);
        }

        static async Task<MetaData> ReadMeta(string metadataFile)
        {
            await using var metadataStream = FileEx.OpenRead(metadataFile);
            return (await JsonSerializer.DeserializeAsync<MetaData>(metadataStream))!;
        }

        public async Task<string> String(string uri)
        {
            var result = await DownloadFile(uri);
            return await File.ReadAllTextAsync(result.Path);
        }

        public async Task<byte[]> Bytes(string uri)
        {
            var result = await DownloadFile(uri);
            return await File.ReadAllBytesAsync(result.Path);
        }

        public async Task<Stream> Stream(string uri)
        {
            var result = await DownloadFile(uri);
            return File.OpenRead(result.Path);
        }

        public async Task ToStream(string uri, Stream stream)
        {
            var result = await DownloadFile(uri);
            await using var fileStream = FileEx.OpenRead(result.Path);
            await fileStream.CopyToAsync(stream);
        }

        public async Task ToFile(string uri, string path)
        {
            var result = await DownloadFile(uri);
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