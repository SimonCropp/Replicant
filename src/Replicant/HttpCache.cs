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
            var contentFile = FindContentFileForUri(uri);

            if (contentFile == null)
            {
                return HandleFileMissing(uri, messageCallback, token);
            }

            return HandleFileExists(uri, useStaleOnError, messageCallback, token, contentFile);
        }

        FileInfo? FindContentFileForUri(string uri)
        {
            var hash = Hash.Compute(uri);
            return new DirectoryInfo(directory)
                .GetFiles($"{hash}_*.bin")
                .OrderBy(x => x.LastWriteTime)
                .FirstOrDefault();
        }

        async Task<Result> HandleFileExists(
            string uri,
            bool useStaleOnError,
            Action<HttpRequestMessage>? messageCallback,
            CancellationToken token,
            FileInfo contentFile)
        {
            var now = DateTimeOffset.UtcNow;

            var contentPath = contentFile.FullName;
            var timestamp = Timestamp.FromPath(contentPath);
            var metaFile = Path.ChangeExtension(contentPath, ".json");
            if (timestamp.Expiry > now)
            {
                return new(contentPath, CacheStatus.Hit, metaFile);
            }

            using var request = BuildRequest(uri, messageCallback);
            timestamp.ApplyHeadersToRequest(request);

            HttpResponseMessage? response = null;

            var httpClient = GetClient();
            try
            {
                try
                {
                    response = await httpClient.SendAsync(request, token);
                }
                catch (TaskCanceledException)
                    when (!token.IsCancellationRequested && useStaleOnError)
                {
                    return new(contentPath, CacheStatus.UseStaleDueToError, metaFile);
                }

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    return new(contentPath, CacheStatus.Revalidate, metaFile);
                }

                if (!response.IsSuccessStatusCode)
                {
                    if (useStaleOnError)
                    {
                        return new(contentPath, CacheStatus.UseStaleDueToError, metaFile);
                    }

                    response.EnsureSuccessStatusCode();
                }

                return await AddItem(response, uri, CacheStatus.Stored, token);
            }
            finally
            {
                response?.Dispose();
            }
        }

        async Task<Result> HandleFileMissing(
            string uri,
            Action<HttpRequestMessage>? messageCallback,
            CancellationToken token)
        {
            var httpClient = GetClient();
            using var request = BuildRequest(uri, messageCallback);
            var response = await httpClient.SendAsync(request, token);
            response.EnsureSuccessStatusCode();
            return await AddItem(response,uri, CacheStatus.Stored, token);
        }

        static HttpRequestMessage BuildRequest(string uri, Action<HttpRequestMessage>? messageCallback)
        {
            HttpRequestMessage request = new(HttpMethod.Get, uri);
            messageCallback?.Invoke(request);
            return request;
        }

        HttpClient GetClient()
        {
            if (client == null)
            {
                return clientFunc!();
            }

            return client;
        }

        public Task AddItem(string uri, HttpResponseMessage response, CancellationToken token = default)
        {
            Guard.AgainstNull(response.Content, nameof(response.Content));
            return AddItem(response, uri, CacheStatus.Stored, token);
        }

        async Task<Result> AddItem(HttpResponseMessage response, string uri, CacheStatus status, CancellationToken token)
        {
            var timestamp = Timestamp.FromResponse(uri, response);

            var tempContentFile = FileEx.GetTempFileName();
            var tempMetaFile = FileEx.GetTempFileName();
            try
            {
                await using var httpStream = await response.Content.ReadAsStreamAsync(default);
                await using (var contentFileStream = FileEx.OpenWrite(tempContentFile))
                await using (var metaFileStream = FileEx.OpenWrite(tempMetaFile))
                {
                    MetaData meta = new(response.Headers, response.Content.Headers, response.TrailingHeaders);
                    await JsonSerializer.SerializeAsync(metaFileStream, meta, cancellationToken: token);
                    await httpStream.CopyToAsync(contentFileStream, token);
                }

                return BuildResult(status, timestamp, tempContentFile, tempMetaFile);
            }
            finally
            {
                File.Delete(tempContentFile);
                File.Delete(tempMetaFile);
            }
        }

        Result BuildResult(CacheStatus status, Timestamp timestamp, string tempContentFile, string tempMetaFile)
        {
            if (timestamp.Expiry == null)
            {
                File.SetLastWriteTimeUtc(tempContentFile, FileEx.MinFileDate);
            }
            else
            {
                File.SetLastWriteTimeUtc(tempContentFile, timestamp.Expiry.Value.UtcDateTime);
            }

            var contentFile = Path.Combine(directory, timestamp.ContentFileName);
            var metaFile = Path.Combine(directory, timestamp.MetaFileName);

            // if another thread has downloaded in parallel, the use those files
            if (!File.Exists(contentFile))
            {
                File.Move(tempContentFile, contentFile, true);
                File.Move(tempMetaFile, metaFile, true);
            }

            return new(contentFile, status, metaFile);
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