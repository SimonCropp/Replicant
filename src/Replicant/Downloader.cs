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

            timer = new(_ => PurgeOld(), null, ignoreTimeSpan, purgeInterval);
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
                using HttpRequestMessage request = new(HttpMethod.Get, uri);
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
                    return new(contentPath, CacheStatus.Hit, metaFile);
                }

                using HttpRequestMessage request = new(HttpMethod.Get, uri);
                messageCallback?.Invoke(request);
                request.Headers.IfModifiedSince = fileTimestamp.LastModified;
                if (fileTimestamp.ETag != null)
                {
                    request.Headers.TryAddWithoutValidation("If-None-Match", $"\"{fileTimestamp.ETag}\"");
                }

                using var response = await client.SendAsync(request, token);

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    return new(contentPath, CacheStatus.Hit, metaFile);
                }

                if (!response.IsSuccessStatusCode)
                {
                    if (useStaleOnError)
                    {
                        return new(contentPath, CacheStatus.Hit, metaFile);
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
            var etagValue = GetEtagValue(response);

            var lastModified = response.GetLastModified(now);

            var contentFile = Path.Combine(directory, $"{hash}_{lastModified:yyyy-MM-ddTHHmmss}_{etagValue}.bin");
            var metaFile = Path.ChangeExtension(contentFile, ".json");
            await using var httpStream = await response.Content.ReadAsStreamAsync(default);
            //TODO: should write these to temp files then copy them. then we can  pass token
            await using (var contentFileStream = FileEx.OpenWrite(contentFile))
            {
                // ReSharper disable once MethodSupportsCancellation
                await httpStream.CopyToAsync(contentFileStream);
            }

            await MetaDataReader.WriteMetaData(response, metaFile);

            if (expiry == null)
            {
                File.SetLastWriteTimeUtc(contentFile, FileEx.MinFileDate);
            }
            else
            {
                File.SetLastWriteTimeUtc(contentFile, expiry.Value.UtcDateTime);
            }

            return new(contentFile, status, metaFile);
        }

        static string? GetEtagValue(HttpResponseMessage response)
        {
            if (!response.TryGetETag(out var weak, out var etag))
            {
                return null;
            }

            if (weak.Value)
            {
                return $"W{etag}";
            }

            return $"S{etag}";

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