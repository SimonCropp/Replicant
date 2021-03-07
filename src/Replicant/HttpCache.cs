using System;
using System.Collections.Generic;
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
                    TryMoveTempFilesBack(contentPath, tempContent, tempMeta, metaPath);

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

        static void TryMoveTempFilesBack(string contentPath, string tempContent, string tempMeta, string metaPath)
        {
            if (File.Exists(tempContent))
            {
                FileEx.Move(tempContent, contentPath);
            }

            if (File.Exists(tempMeta))
            {
                FileEx.Move(tempMeta, metaPath);
            }
        }

        public void Purge()
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                File.Delete(file);
            }
        }

        internal Task<Result> DownloadAsync(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            var contentFile = FindContentFileForUri(uri);

            if (contentFile == null)
            {
                return HandleFileMissingAsync(uri, modifyRequest, token);
            }

            return HandleFileExistsAsync(uri, staleIfError, modifyRequest, token, contentFile);
        }

        internal Result Download(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            var contentFile = FindContentFileForUri(uri);

            if (contentFile == null)
            {
                return HandleFileMissing(uri, modifyRequest, token);
            }

            return HandleFileExists(uri, staleIfError, modifyRequest, contentFile, token);
        }

        FileInfo? FindContentFileForUri(string uri)
        {
            var hash = Hash.Compute(uri);
            return new DirectoryInfo(directory)
                .GetFiles($"{hash}_*.bin")
                .OrderBy(x => x.LastWriteTime)
                .FirstOrDefault();
        }

        async Task<Result> HandleFileExistsAsync(
            string uri,
            bool staleIfError,
            Action<HttpRequestMessage>? modifyRequest,
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

            using var request = BuildRequest(uri, modifyRequest);
            timestamp.ApplyHeadersToRequest(request);

            HttpResponseMessage? response;

            var httpClient = GetClient();
            try
            {
                response = await httpClient.SendAsyncEx(request, token);
            }
            catch (Exception exception)
            {
                if (ShouldReturnStaleIfError(staleIfError, exception, token))
                {
                    return new(contentPath, CacheStatus.UseStaleDueToError, metaFile);
                }

                throw;
            }

            var status = response.CacheStatus(staleIfError);
            switch (status)
            {
                case CacheStatus.Hit:
                case CacheStatus.UseStaleDueToError:
                {
                    response.Dispose();
                    return new(contentPath, status, metaFile);
                }
                case CacheStatus.Stored:
                case CacheStatus.Revalidate:
                {
                    using (response)
                    {
                        return await AddItemAsync(response, uri, status, token);
                    }
                }
                case CacheStatus.NoCache:
                {
                    return new(response, CacheStatus.NoCache);
                }
                default:
                {
                    response.Dispose();
                    throw new ArgumentOutOfRangeException();
                }
            }
        }

        Result HandleFileExists(
            string uri,
            bool staleIfError,
            Action<HttpRequestMessage>? modifyRequest,
            FileInfo contentFile,
            CancellationToken token)
        {
            var now = DateTimeOffset.UtcNow;

            var contentPath = contentFile.FullName;
            var timestamp = Timestamp.FromPath(contentPath);
            var metaFile = Path.ChangeExtension(contentPath, ".json");
            if (timestamp.Expiry > now)
            {
                return new(contentPath, CacheStatus.Hit, metaFile);
            }

            using var request = BuildRequest(uri, modifyRequest);
            timestamp.ApplyHeadersToRequest(request);

            HttpResponseMessage? response;

            var httpClient = GetClient();
            try
            {
                response = httpClient.SendEx(request, token);
            }
            catch (Exception exception)
            {
                if (ShouldReturnStaleIfError(staleIfError, exception, token))
                {
                    return new(contentPath, CacheStatus.UseStaleDueToError, metaFile);
                }

                throw;
            }

            var status = DeriveCacheStatus.CacheStatus(response, staleIfError);
            switch (status)
            {
                case CacheStatus.Hit:
                case CacheStatus.UseStaleDueToError:
                {
                    response.Dispose();
                    return new(contentPath, status, metaFile);
                }
                case CacheStatus.Stored:
                case CacheStatus.Revalidate:
                {
                    using (response)
                    {
                        return AddItem(response, uri, status, token);
                    }
                }
                case CacheStatus.NoCache:
                {
                    return new(response, CacheStatus.NoCache);
                }
                default:
                {
                    response.Dispose();
                    throw new ArgumentOutOfRangeException();
                }
            }
        }

        static bool ShouldReturnStaleIfError(bool staleIfError, Exception exception, CancellationToken token)
        {
            return (
                       exception is HttpRequestException ||
                       exception is TaskCanceledException &&
                       !token.IsCancellationRequested
                   )
                   && staleIfError;
        }

        async Task<Result> HandleFileMissingAsync(
            string uri,
            Action<HttpRequestMessage>? modifyRequest,
            CancellationToken token)
        {
            var httpClient = GetClient();
            using var request = BuildRequest(uri, modifyRequest);
            var response = await httpClient.SendAsyncEx(request, token);
            response.EnsureSuccess();
            if (response.IsNoCache())
            {
                return new(response, CacheStatus.NoCache);
            }

            using (response)
            {
                return await AddItemAsync(response, uri, CacheStatus.Stored, token);
            }
        }

        Result HandleFileMissing(
            string uri,
            Action<HttpRequestMessage>? modifyRequest,
            CancellationToken token)
        {
            var httpClient = GetClient();
            using var request = BuildRequest(uri, modifyRequest);
            var response = httpClient.SendEx(request, token);
            response.EnsureSuccess();
            if (response.IsNoCache())
            {
                return new(response, CacheStatus.NoCache);
            }

            using (response)
            {
                return AddItem(response, uri, CacheStatus.Stored, token);
            }
        }

        static HttpRequestMessage BuildRequest(string uri, Action<HttpRequestMessage>? modifyRequest)
        {
            HttpRequestMessage request = new(HttpMethod.Get, uri);
            modifyRequest?.Invoke(request);
            return request;
        }

        HttpClient GetClient()
        {
            return client ?? clientFunc!();
        }

        public Task AddItemAsync(string uri, HttpResponseMessage response, CancellationToken token = default)
        {
            Guard.AgainstNull(response.Content, nameof(response.Content));
            return AddItemAsync(response, uri, CacheStatus.Stored, token);
        }

        public async Task AddItemAsync(
            string uri,
            string content,
            DateTimeOffset? expiry = null,
            DateTimeOffset? modified = null,
            string? etag = null,
            Headers? responseHeaders = null,
            Headers? contentHeaders = null,
            Headers? trailingHeaders = null,
            CancellationToken token = default)
        {
            using var contentStream = content.AsStream();
            await AddItemAsync(
                uri,
                contentStream,
                expiry,
                modified,
                etag,
                responseHeaders,
                contentHeaders,
                trailingHeaders,
                token);
        }

        public Task AddItemAsync(
            string uri,
            Stream stream,
            DateTimeOffset? expiry = null,
            DateTimeOffset? modified = null,
            string? etag = null,
            Headers? responseHeaders = null,
            Headers? contentHeaders = null,
            Headers? trailingHeaders = null,
            CancellationToken token = default)
        {
            var hash = Hash.Compute(uri);
            var now = DateTimeOffset.Now;

            var timestamp = new Timestamp(
                expiry,
                modified.GetValueOrDefault(now),
                Etag.FromHeader(etag),
                hash);

            responseHeaders ??= new Headers();
            contentHeaders ??= new Headers();
            trailingHeaders ??= new Headers();

            if (expiry != null)
            {
                contentHeaders.Add(
                    HttpResponseHeader.Expires.ToString(),
                    expiry.Value.ToUniversalTime().ToString("r"));
            }

            if (modified != null)
            {
                responseHeaders.Add(
                    HttpResponseHeader.LastModified.ToString(),
                    modified.Value.ToUniversalTime().ToString("r"));
            }

            return AddItemAsync(CacheStatus.Stored, token, _ => Task.FromResult(stream), responseHeaders, contentHeaders, trailingHeaders, timestamp);
        }

        public void AddItem(string uri, HttpResponseMessage response, CancellationToken token = default)
        {
            Guard.AgainstNull(response.Content, nameof(response.Content));
            AddItem(response, uri, CacheStatus.Stored, token);
        }

        Task<Result> AddItemAsync(HttpResponseMessage response, string uri, CacheStatus status, CancellationToken token)
        {
            var timestamp = Timestamp.FromResponse(uri, response);
            Task<Stream> ContentFunc(CancellationToken cancellationToken) => response.Content.ReadAsStreamAsync(cancellationToken);

            return AddItemAsync(status, token, ContentFunc, response.Headers, response.Content.Headers, response.TrailingHeaders(), timestamp);
        }

        async Task<Result> AddItemAsync(
            CacheStatus status,
            CancellationToken token,
            Func<CancellationToken, Task<Stream>> httpContentFunc,
            IEnumerable<KeyValuePair<string, IEnumerable<string>>> httpResponseHeaders,
            IEnumerable<KeyValuePair<string, IEnumerable<string>>> contentHeaders,
            IEnumerable<KeyValuePair<string, IEnumerable<string>>> trailingHeaders,
            Timestamp timestamp)
        {
            var tempContentFile = FileEx.GetTempFileName();
            var tempMetaFile = FileEx.GetTempFileName();
            try
            {
#if NET5_0_OR_GREATER
                await using var httpStream = await httpContentFunc(token);
                await using (var contentFileStream = FileEx.OpenWrite(tempContentFile))
                await using (var metaFileStream = FileEx.OpenWrite(tempMetaFile))
                {
#else
                using var httpStream = await httpContentFunc(token);
                using (var contentFileStream = FileEx.OpenWrite(tempContentFile))
                using (var metaFileStream = FileEx.OpenWrite(tempMetaFile))
                {
#endif
                    MetaData meta = new(httpResponseHeaders, contentHeaders, trailingHeaders);
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

        Result AddItem(HttpResponseMessage response, string uri, CacheStatus status, CancellationToken token)
        {
            var timestamp = Timestamp.FromResponse(uri, response);

            var tempContentFile = FileEx.GetTempFileName();
            var tempMetaFile = FileEx.GetTempFileName();
            try
            {
                using var httpStream = response.Content.ReadAsStream(token);
                using (var contentFileStream = FileEx.OpenWrite(tempContentFile))
                using (var metaFileStream = FileEx.OpenWrite(tempMetaFile))
                using (var writer = new Utf8JsonWriter(metaFileStream))
                {
#if NET5_0_OR_GREATER
                    MetaData meta = new(response.Headers, response.Content.Headers, response.TrailingHeaders);
#else
                    MetaData meta = new(response.Headers, response.Content.Headers);
#endif
                    JsonSerializer.Serialize(writer, meta);
                    httpStream.CopyTo(contentFileStream);
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

            // if another thread has downloaded in parallel, then use those files
            if (!File.Exists(contentFile))
            {
                try
                {
                    FileEx.Move(tempContentFile, contentFile);
                    FileEx.Move(tempMetaFile, metaFile);
                }
                catch (Exception exception)
                    when (exception is IOException ||
                          exception is UnauthorizedAccessException)
                {
                    if (!File.Exists(contentFile))
                    {
                        FileEx.Move(tempContentFile, contentFile);
                        FileEx.Move(tempMetaFile, metaFile);
                    }
                }
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
#if NET5_0_OR_GREATER
            return timer.DisposeAsync();
#else
            timer.Dispose();
            return default;
#endif
        }
    }
}