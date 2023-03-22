﻿using System.Net;
using System.Text.Json;

namespace Replicant;

public partial class HttpCache :
    IHttpCache
{
    string directory;
    HttpClient? client;
    Func<HttpClient>? clientFunc;
    static HttpCache? defaultCache;

    public static HttpCache Default
    {
        get
        {
            if (defaultCache == null)
            {
                var directory = Path.Combine(Path.GetTempPath(), "Replicant");
                Interlocked.CompareExchange(ref defaultCache, new(directory,new HttpClient()), null);
            }

            return defaultCache;
        }
    }

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

    /// <summary>
    /// Instantiate a new instance of <see cref="HttpCache"/>.
    /// </summary>
    /// <param name="directory">The directory to store the cache files.</param>
    /// <param name="clientFunc">A factory to retrieve a <see cref="HttpClient"/> each time a resource is downloaded.</param>
    /// <param name="maxEntries">The maximum entries to store in the cache.</param>
    public HttpCache(string directory, Func<HttpClient> clientFunc, int maxEntries = 1000) :
        this(directory, maxEntries) =>
        this.clientFunc = clientFunc;

    /// <summary>
    /// Instantiate a new instance of <see cref="HttpCache"/>.
    /// </summary>
    /// <param name="directory">The directory to store the cache files.</param>
    /// <param name="client">The <see cref="HttpCache"/> to use do web calls. If not supplied an instance will be instantiate.</param>
    /// <param name="maxEntries">The maximum entries to store in the cache.</param>
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
        Cancellation cancellation = default) =>
        DownloadAsync(new Uri(uri), staleIfError, modifyRequest, cancellation);

    internal Task<Result> DownloadAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default)
    {
        var contentFile = FindContentFileForUri(uri);

        if (contentFile == null)
        {
            return HandleFileMissingAsync(uri, modifyRequest, cancellation);
        }

        return HandleFileExistsAsync(uri, staleIfError, modifyRequest, cancellation, contentFile.Value);
    }

    internal Result Download(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default) =>
        Download(new Uri(uri), staleIfError, modifyRequest, cancellation);

    internal Result Download(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default)
    {
        var contentFile = FindContentFileForUri(uri);

        if (contentFile == null)
        {
            return HandleFileMissing(uri, modifyRequest, cancellation);
        }

        return HandleFileExists(uri, staleIfError, modifyRequest, contentFile.Value, cancellation);
    }

    FilePair? FindContentFileForUri(Uri uri)
    {
        var hash = Hash.Compute(uri.AbsoluteUri);
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
        Uri uri,
        bool staleIfError,
        Action<HttpRequestMessage>? modifyRequest,
        Cancellation cancellation,
        FilePair file)
    {
        var now = DateTimeOffset.UtcNow;

        var timestamp = Timestamp.FromPath(file.Content);

        // if the current file hasn't expired, return the current file
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
            response = await httpClient.SendAsyncEx(request, cancellation);
        }
        catch (Exception exception)
        {
            if (ShouldReturnStaleIfError(staleIfError, exception, cancellation))
            {
                return new(file, true, false);
            }

            throw;
        }

        var status = response.GetCacheStatus(staleIfError);
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
                    return await AddItemAsync(response, uri, cancellation);
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
        Uri uri,
        bool staleIfError,
        Action<HttpRequestMessage>? modifyRequest,
        FilePair contentFile,
        Cancellation cancellation)
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
            response = httpClient.SendEx(request, cancellation);
        }
        catch (Exception exception)
        {
            if (ShouldReturnStaleIfError(staleIfError, exception, cancellation))
            {
                return new(contentFile, true, false);
            }

            throw;
        }

        var status = response.GetCacheStatus(staleIfError);
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
                    return AddItem(response, uri, cancellation);
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

    static bool ShouldReturnStaleIfError(bool staleIfError, Exception exception, Cancellation cancellation) =>
        (
            exception is HttpRequestException ||
            exception is TaskCanceledException &&
            !cancellation.IsCancellationRequested
        )
        && staleIfError;

    async Task<Result> HandleFileMissingAsync(
        Uri uri,
        Action<HttpRequestMessage>? modifyRequest,
        Cancellation cancellation)
    {
        var httpClient = GetClient();
        using var request = BuildRequest(uri, modifyRequest);
        var response = await httpClient.SendAsyncEx(request, cancellation);
        response.EnsureSuccess();
        if (response.IsNoStore())
        {
            return new(response);
        }

        using (response)
        {
            return await AddItemAsync(response, uri, cancellation);
        }
    }

    Result HandleFileMissing(
        Uri uri,
        Action<HttpRequestMessage>? modifyRequest,
        Cancellation cancellation)
    {
        var httpClient = GetClient();
        using var request = BuildRequest(uri, modifyRequest);
        var response = httpClient.SendEx(request, cancellation);
        response.EnsureSuccess();
        if (response.IsNoStore())
        {
            return new(response);
        }

        using (response)
        {
            return AddItem(response, uri, cancellation);
        }
    }

    static HttpRequestMessage BuildRequest(Uri uri, Action<HttpRequestMessage>? modifyRequest)
    {
        HttpRequestMessage request = new(HttpMethod.Get, uri);
        modifyRequest?.Invoke(request);
        return request;
    }

    HttpClient GetClient() =>
        client ?? clientFunc!();

    /// <summary>
    /// Manually add an item to the cache.
    /// </summary>
    public virtual Task AddItemAsync(
        Uri uri,
        Stream stream,
        DateTimeOffset? expiry = null,
        DateTimeOffset? modified = null,
        string? etag = null,
        Headers? responseHeaders = null,
        Headers? contentHeaders = null,
        Headers? trailingHeaders = null,
        Cancellation cancellation = default)
    {
        var hash = Hash.Compute(uri.AbsoluteUri);
        var now = DateTimeOffset.Now;

        var timestamp = new Timestamp(
            expiry,
            modified.GetValueOrDefault(now),
            Etag.FromHeader(etag),
            hash);

        responseHeaders ??= new();
        contentHeaders ??= new();
        trailingHeaders ??= new();

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

        var meta = MetaData.FromEnumerables(uri.AbsoluteUri, responseHeaders, contentHeaders, trailingHeaders);
        return InnerAddItemAsync(cancellation, _ => Task.FromResult(stream), meta, timestamp);
    }

    Task<Result> AddItemAsync(HttpResponseMessage response, Uri uri, Cancellation cancellation)
    {
        var timestamp = Timestamp.FromResponse(uri, response);
        Task<Stream> ContentFunc(Cancellation cancellation) => response.Content.ReadAsStreamAsync(cancellation);

        var meta = MetaData.FromEnumerables(uri.AbsoluteUri, response.Headers, response.Content.Headers, response.TrailingHeaders());
        return InnerAddItemAsync(cancellation, ContentFunc, meta, timestamp);
    }

    static JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = true
    };

    async Task<Result> InnerAddItemAsync(
        Cancellation cancellation,
        Func<Cancellation, Task<Stream>> httpContentFunc,
        MetaData meta,
        Timestamp timestamp)
    {
        var tempFile = FilePair.GetTemp();
        try
        {
#if NET7_0_OR_GREATER
            await using var httpStream = await httpContentFunc(cancellation);
            await using (var contentFileStream = FileEx.OpenWrite(tempFile.Content))
            await using (var metaFileStream = FileEx.OpenWrite(tempFile.Meta))
            {
#else
            using var httpStream = await httpContentFunc(cancellation);
            using (var contentFileStream = FileEx.OpenWrite(tempFile.Content))
            using (var metaFileStream = FileEx.OpenWrite(tempFile.Meta))
            {
#endif
                await JsonSerializer.SerializeAsync(metaFileStream, meta, serializerOptions, cancellation);
                await httpStream.CopyToAsync(contentFileStream, cancellation);
            }

            return BuildResult(timestamp, tempFile);
        }
        finally
        {
            tempFile.Delete();
        }
    }

    Result AddItem(HttpResponseMessage response, Uri uri, Cancellation cancellation)
    {
        var timestamp = Timestamp.FromResponse(uri, response);

#if NET7_0_OR_GREATER
        var meta = MetaData.FromEnumerables(uri.AbsoluteUri, response.Headers, response.Content.Headers, response.TrailingHeaders);
#else
        var meta = MetaData.FromEnumerables(uri.AbsoluteUri, response.Headers, response.Content.Headers);
#endif
        var tempFile = FilePair.GetTemp();
        try
        {
            using var httpStream = response.Content.ReadAsStream(cancellation);
            using (var contentFileStream = FileEx.OpenWrite(tempFile.Content))
            using (var metaFileStream = FileEx.OpenWrite(tempFile.Meta))
            using (var writer = new Utf8JsonWriter(metaFileStream, new() {Indented = true}))
            {
                JsonSerializer.Serialize(writer, meta, serializerOptions);
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
            when (exception is IOException or UnauthorizedAccessException)
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
                return new(new(newContent, newMeta), true, true);
            }

            return new(new(contentFile, newMeta), true, true);
        }

        return new(new(contentFile, metaFile), true, true);
    }
}