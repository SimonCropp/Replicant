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
    public partial class HttpCache
    {
        string directory;
        HttpClient? client;
        Func<HttpClient>? clientFunc;

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

            return HandleFileExistsAsync(uri, staleIfError, modifyRequest, token, contentFile.Value);
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

            return HandleFileExists(uri, staleIfError, modifyRequest, contentFile.Value, token);
        }

        FilePair? FindContentFileForUri(string uri)
        {
            var hash = Hash.Compute(uri);
            var directoryInfo = new DirectoryInfo(directory);
            var fileInfo = directoryInfo
                .GetFiles($"{hash}_*.bin")
                .OrderBy(x => x.LastWriteTime)
                .FirstOrDefault();
            if (fileInfo == null)
            {
                return null;
            }

            return FilePair.FromContentFile(fileInfo);
        }

        async Task<Result> HandleFileExistsAsync(
            string uri,
            bool staleIfError,
            Action<HttpRequestMessage>? modifyRequest,
            CancellationToken token,
            FilePair file)
        {
            var now = DateTimeOffset.UtcNow;

            var timestamp = Timestamp.FromPath(file.Content);
            if (timestamp.Expiry > now)
            {
                return new(file, false, false);
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
                    return new(file, true, false);
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
                    return new(file, true, false);
                }
                case CacheStatus.Stored:
                case CacheStatus.Revalidate:
                {
                    using (response)
                    {
                        return await AddItemAsync(response, uri, token);
                    }
                }
                case CacheStatus.NoStore:
                {
                    return new(response);
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
            FilePair contentFile,
            CancellationToken token)
        {
            var now = DateTimeOffset.UtcNow;

            var timestamp = Timestamp.FromPath(contentFile.Content);
            if (timestamp.Expiry > now)
            {
                return new(contentFile, false, false);
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
                    return new(contentFile, true, false);
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
                    return new(contentFile, true, false);
                }
                case CacheStatus.Stored:
                case CacheStatus.Revalidate:
                {
                    using (response)
                    {
                        return AddItem(response, uri, token);
                    }
                }
                case CacheStatus.NoStore:
                {
                    return new(response);
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
            if (response.IsNoStore())
            {
                return new(response);
            }

            using (response)
            {
                return await AddItemAsync(response, uri, token);
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
            if (response.IsNoStore())
            {
                return new(response);
            }

            using (response)
            {
                return AddItem(response, uri, token);
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

            var meta = MetaData.FromEnumerables(responseHeaders, contentHeaders, trailingHeaders);
            return InnerAddItemAsync(token, _ => Task.FromResult(stream), meta, timestamp);
        }

        Task<Result> AddItemAsync(HttpResponseMessage response, string uri, CancellationToken token)
        {
            var timestamp = Timestamp.FromResponse(uri, response);
            Task<Stream> ContentFunc(CancellationToken cancellationToken) => response.Content.ReadAsStreamAsync(cancellationToken);

            var meta = MetaData.FromEnumerables(response.Headers, response.Content.Headers, response.TrailingHeaders());
            return InnerAddItemAsync(token, ContentFunc, meta, timestamp);
        }

        async Task<Result> InnerAddItemAsync(
            CancellationToken token,
            Func<CancellationToken, Task<Stream>> httpContentFunc,
            MetaData meta,
            Timestamp timestamp)
        {
            var tempFile = FilePair.GetTemp();
            try
            {
#if NET5_0
                await using var httpStream = await httpContentFunc(token);
                await using (var contentFileStream = FileEx.OpenWrite(tempFile.Content))
                await using (var metaFileStream = FileEx.OpenWrite(tempFile.Meta))
                {
#else
                using var httpStream = await httpContentFunc(token);
                using (var contentFileStream = FileEx.OpenWrite(tempFile.Content))
                using (var metaFileStream = FileEx.OpenWrite(tempFile.Meta))
                {
#endif
                    await JsonSerializer.SerializeAsync(metaFileStream, meta, cancellationToken: token);
                    await httpStream.CopyToAsync(contentFileStream, token);
                }

                return BuildResult(timestamp, tempFile);
            }
            finally
            {
                tempFile.Delete();
            }
        }

        Result AddItem(HttpResponseMessage response, string uri, CancellationToken token)
        {
            var timestamp = Timestamp.FromResponse(uri, response);

#if NET5_0
            var meta = MetaData.FromEnumerables(response.Headers, response.Content.Headers, response.TrailingHeaders);
#else
            var meta = MetaData.FromEnumerables(response.Headers, response.Content.Headers);
#endif
            var tempFile = FilePair.GetTemp();
            try
            {
                using var httpStream = response.Content.ReadAsStream(token);
                using (var contentFileStream = FileEx.OpenWrite(tempFile.Content))
                using (var metaFileStream = FileEx.OpenWrite(tempFile.Meta))
                using (var writer = new Utf8JsonWriter(metaFileStream))
                {
                    JsonSerializer.Serialize(writer, meta);
                    httpStream.CopyTo(contentFileStream);
                }

                return BuildResult(timestamp, tempFile);
            }
            finally
            {
                tempFile.Delete();
            }
        }

        Result BuildResult(Timestamp timestamp, FilePair tempFile)
        {
            tempFile.SetExpiry(timestamp.Expiry);

            var contentFile = Path.Combine(directory, timestamp.ContentFileName);
            var metaFile = Path.Combine(directory, timestamp.MetaFileName);

            try
            {
                FileEx.Move(tempFile.Content, contentFile);
                FileEx.Move(tempFile.Meta, metaFile);
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException)
            {
                //Failed to move files, so use temp files instead
                var newName = Path.GetFileNameWithoutExtension(contentFile);
                newName += Guid.NewGuid();

                var newMeta = $"{newName}.json";
                FileEx.Move(tempFile.Meta, newMeta);
                if (File.Exists(tempFile.Content))
                {
                    var newContent = $"{newName}.bin";
                    FileEx.Move(tempFile.Content, newContent);
                    return new(new FilePair(newContent, newMeta), true, true);
                }
                else
                {
                    return new(new FilePair(contentFile, newMeta), true, true);
                }
            }

            return new(new FilePair(contentFile, metaFile), true, true);
        }
    }
}