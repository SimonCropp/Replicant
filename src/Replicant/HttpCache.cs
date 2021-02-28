using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Replicant
{
    public partial class HttpCache :
        IAsyncDisposable,
        IDisposable
    {
        string directory;
        int maxEntries;
        HttpClient? client;
        Func<HttpClient>? clientFunc;

        Timer timer;
        static TimeSpan purgeInterval = TimeSpan.FromMinutes(10);
        static TimeSpan ignoreTimeSpan = TimeSpan.FromMilliseconds(-1);
        public static Action<string> LogError = _ => { };
        bool clientIsOwned;

        HttpCache(string directory, int maxEntries = 1000)
        {
            Guard.AgainstNullOrEmpty(directory, nameof(directory));
            if (maxEntries < 100)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEntries), "maxEntries must be greater than 100");
            }

            this.directory = directory;
            this.maxEntries = maxEntries;

            Directory.CreateDirectory(directory);

            timer = new(_ => PauseAndPurgeOld(), null, ignoreTimeSpan, purgeInterval);
        }

        public HttpCache(string directory, Func<HttpClient> clientFunc, int maxEntries = 1000) :
            this(directory, maxEntries)
        {
            this.clientFunc = clientFunc;
        }

        public HttpCache(string directory, HttpClient? client = null, int maxEntries = 1000) :
            this(directory, maxEntries)
        {
            if (client == null)
            {
                clientIsOwned = true;
                this.client = new();
            }
            else
            {
                this.client = client;
            }
        }

        void PauseAndPurgeOld()
        {
            timer.Change(ignoreTimeSpan, ignoreTimeSpan);
            try
            {
                PurgeOld();
            }
            finally
            {
                timer.Change(purgeInterval, ignoreTimeSpan);
            }
        }

        public void PurgeOld()
        {
            foreach (var file in new DirectoryInfo(directory)
                .GetFiles("*_*_*.bin")
                .OrderByDescending(x => x.LastAccessTime)
                .Skip(maxEntries))
            {
                PurgeItem(file.FullName);
            }
        }

        internal static void PurgeItem(string contentPath)
        {
            var tempContent = FileEx.GetTempFileName();
            var tempMeta = FileEx.GetTempFileName();
            var metaPath = Path.ChangeExtension(contentPath, "json");
            try
            {
                File.Move(contentPath, tempContent);
                File.Move(metaPath, tempMeta);
            }
            catch (Exception)
            {
                try
                {
                    if (File.Exists(tempContent))
                    {
                        File.Move(tempContent, contentPath, true);
                    }

                    if (File.Exists(tempMeta))
                    {
                        File.Move(tempMeta, metaPath, true);
                    }

                    LogError($"Could not purge item due to locked file. Cached item remains. Path: {contentPath}");
                }
                catch (Exception e)
                {
                    throw new Exception($"Could not purge item due to locked file. Cached item is in a corrupted state. Path: {contentPath}", e);
                }
            }
            finally
            {
                File.Delete(tempContent);
                File.Delete(tempMeta);
            }
        }

        public void Purge()
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                File.Delete(file);
            }
        }

        public Task<Result> Download(
            string uri,
            bool useStaleOnError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            var hash = Hash.Compute(uri);
            var contentFile = new DirectoryInfo(directory)
                .GetFiles($"{hash}_*.bin")
                .OrderBy(x => x.LastWriteTime)
                .FirstOrDefault();

            if (contentFile == null)
            {
                return HandleFileMissing(uri, messageCallback, token, hash);
            }

            return HandleFileExists(uri, useStaleOnError, messageCallback, token, contentFile, hash);
        }

        async Task<Result> HandleFileExists(
            string uri,
            bool useStaleOnError,
            Action<HttpRequestMessage>? messageCallback,
            CancellationToken token,
            FileInfo contentFile,
            string hash)
        {
            var now = DateTimeOffset.UtcNow;

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
            request.AddIfNoneMatch(fileTimestamp.ETag);

            HttpResponseMessage? response = null;

            try
            {
                try
                {
                    response = await GetClient().SendAsync(request, token);
                }
                catch (TaskCanceledException)
                {
                    if (token.IsCancellationRequested)
                    {
                        throw;
                    }

                    if (useStaleOnError)
                    {
                        return new(contentPath, CacheStatus.UseStaleDueToError, metaFile);
                    }

                    throw;
                }

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    return new(contentPath, CacheStatus.Revalidate, metaFile);
                }

                if (!response.IsSuccessStatusCode)
                {
                    if (useStaleOnError)
                    {
                        return new(contentPath, CacheStatus.Hit, metaFile);
                    }

                    response.EnsureSuccessStatusCode();
                }

                return await AddItem(response, now, hash, CacheStatus.Stored);
            }
            finally
            {
                response?.Dispose();
            }
        }

        async Task<Result> HandleFileMissing(
            string uri,
            Action<HttpRequestMessage>? messageCallback,
            CancellationToken token,
            string hash)
        {
            var now = DateTimeOffset.UtcNow;

            using HttpRequestMessage request = new(HttpMethod.Get, uri);
            messageCallback?.Invoke(request);
            using var response = await GetClient().SendAsync(request, token);
            response.EnsureSuccessStatusCode();
            return await AddItem(response, now, hash, CacheStatus.Stored);
        }

        HttpClient GetClient()
        {
            if (client == null)
            {
                return clientFunc!();
            }

            return client;
        }

        public Task AddItem(string uri, HttpResponseMessage response)
        {
            Guard.AgainstNull(response.Content, nameof(response.Content));
            var now = DateTime.UtcNow;
            var hash = Hash.Compute(uri);

            return AddItem(response, now, hash, CacheStatus.Stored);
        }

        async Task<Result> AddItem(HttpResponseMessage response, DateTimeOffset now, string hash, CacheStatus status)
        {
            var expiry = response.GetExpiry(now);
            var etag = Etag.FromResponse(response);

            var lastModified = response.GetLastModified(now);
            var tempContentFile = FileEx.GetTempFileName();
            var tempMetaFile = FileEx.GetTempFileName();
            try
            {
                await using var httpStream = await response.Content.ReadAsStreamAsync(default);
                //TODO: should write these to temp files then copy them. then we can  pass token
                await using (var contentFileStream = FileEx.OpenWrite(tempContentFile))
                {
                    // ReSharper disable once MethodSupportsCancellation
                    await httpStream.CopyToAsync(contentFileStream);
                }

                await MetaDataReader.WriteMetaData(response, tempMetaFile);

                if (expiry == null)
                {
                    File.SetLastWriteTimeUtc(tempContentFile, FileEx.MinFileDate);
                }
                else
                {
                    File.SetLastWriteTimeUtc(tempContentFile, expiry.Value.UtcDateTime);
                }

                var contentFile = Path.Combine(directory, $"{hash}_{lastModified:yyyy-MM-ddTHHmmss}_{etag.ForFile}.bin");
                var metaFile = Path.ChangeExtension(contentFile, ".json");
                // if another thread has downloaded in parallel, the use those files
                if (!File.Exists(contentFile))
                {
                    File.Move(tempContentFile, contentFile, true);
                    File.Move(tempMetaFile, metaFile, true);
                }

                return new(contentFile, status, metaFile);
            }
            finally
            {
                File.Delete(tempContentFile);
                File.Delete(tempMetaFile);
            }
        }

        public void Dispose()
        {
            if (clientIsOwned)
            {
                client!.Dispose();
            }

            timer.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            if (clientIsOwned)
            {
                client!.Dispose();
            }

            return timer.DisposeAsync();
        }
    }
}