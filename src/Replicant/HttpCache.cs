namespace Replicant;

public partial class HttpCache :
    IHttpCache
{
    string directory;
    HttpMessageHandler? handler;
    Func<HttpMessageHandler>? handlerFunc;
    Func<HttpClient>? clientFunc;
    HttpMessageInvoker? invoker;

    static readonly Lazy<HttpCache> defaultInstance = new(() =>
    {
        var directory = Path.Combine(FileEx.TempPath, "Replicant");
        return new(directory, handler: null);
    });

    public static HttpCache Default => defaultInstance.Value;

    public static Action<string> LogError = _ =>
    {
    };

    bool handlerIsOwned;

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
    /// <param name="handlerFunc">A factory to retrieve a <see cref="HttpMessageHandler"/> each time a resource is downloaded.</param>
    /// <param name="maxEntries">The maximum entries to store in the cache.</param>
    public HttpCache(string directory, Func<HttpMessageHandler> handlerFunc, int maxEntries = 1000) :
        this(directory, maxEntries) =>
        this.handlerFunc = handlerFunc;

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
    /// <param name="handler">The <see cref="HttpMessageHandler"/> to use for web calls. If not supplied an instance will be instantiated.</param>
    /// <param name="maxEntries">The maximum entries to store in the cache.</param>
    public HttpCache(string directory, HttpMessageHandler? handler = null, int maxEntries = 1000) :
        this(directory, maxEntries)
    {
        if (handler == null)
        {
            handlerIsOwned = true;
            this.handler = new HttpClientHandler();
        }
        else
        {
            this.handler = handler;
        }

        invoker = new(this.handler, disposeHandler: false);
    }

    /// <summary>
    /// Instantiate a new instance of <see cref="HttpCache"/>.
    /// </summary>
    /// <param name="directory">The directory to store the cache files.</param>
    /// <param name="client">The <see cref="HttpClient"/> to use for web calls.</param>
    /// <param name="maxEntries">The maximum entries to store in the cache.</param>
    /// <remarks>HttpClient is itself an HttpMessageInvoker, so it is used directly.</remarks>
    public HttpCache(string directory, HttpClient client, int maxEntries = 1000) :
        this(directory, maxEntries) =>
        invoker = client;

    internal Task<Result> DownloadAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default) =>
        DownloadAsync(new Uri(uri), staleIfError, modifyRequest, cancel);

    internal Task<Result> DownloadAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default)
    {
        var contentFile = FindContentFileForUri(uri);

        if (contentFile == null)
        {
            return HandleFileMissingAsync(uri, modifyRequest, cancel);
        }

        return HandleFileExistsAsync(uri, staleIfError, modifyRequest, cancel, contentFile.Value);
    }

    internal Result Download(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default) =>
        Download(new Uri(uri), staleIfError, modifyRequest, cancel);

    internal Result Download(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default)
    {
        var contentFile = FindContentFileForUri(uri);

        if (contentFile == null)
        {
            return HandleFileMissing(uri, modifyRequest, cancel);
        }

        return HandleFileExists(uri, staleIfError, modifyRequest, contentFile.Value, cancel);
    }

    FilePair? FindContentFileForUri(Uri uri)
    {
        var hash = Hash.Compute(uri.AbsoluteUri);
        var file = Directory
            .EnumerateFiles(directory, $"{hash}_*.bin")
            .MinBy(File.GetLastWriteTime);
        if (file == null)
        {
            return null;
        }

        return FilePair.FromContentFile(file);
    }

    async Task<Result> HandleFileExistsAsync(
        Uri uri,
        bool staleIfError,
        Action<HttpRequestMessage>? modifyRequest,
        Cancel cancel,
        FilePair file)
    {
        var now = DateTimeOffset.UtcNow;

        var timestamp = Timestamp.FromPath(file.Content);

        // if the current file hasn't expired, return the current file
        var expiry = timestamp.Expiry;
        if (expiry == null || expiry > now)
        {
            return new(file, false, false);
        }

        var expired = expiry < now;
        using var request = BuildRequest(uri, modifyRequest);

        if (!expired)
        {
            timestamp.ApplyHeadersToRequest(request);
        }

        HttpResponseMessage? response;

        var invoker = GetInvoker();
        try
        {
            response = await invoker.SendAsyncEx(request, cancel);
        }
        catch (Exception exception)
        {
            if (ShouldReturnStaleIfError(staleIfError, exception, cancel))
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
                    return await AddItemAsync(response, uri, cancel);
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
        Cancel cancel)
    {
        var now = DateTimeOffset.UtcNow;

        var timestamp = Timestamp.FromPath(contentFile.Content);
        var expiry = timestamp.Expiry;
        if (expiry == null || expiry > now)
        {
            return new(contentFile, false, false);
        }

        using var request = BuildRequest(uri, modifyRequest);
        timestamp.ApplyHeadersToRequest(request);

        HttpResponseMessage? response;

        var invoker = GetInvoker();
        try
        {
            response = invoker.SendEx(request, cancel);
        }
        catch (Exception exception)
        {
            if (ShouldReturnStaleIfError(staleIfError, exception, cancel))
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
                    return AddItem(response, uri, cancel);
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

    static bool ShouldReturnStaleIfError(bool staleIfError, Exception exception, Cancel cancel) =>
        (
            exception is HttpRequestException ||
            exception is TaskCanceledException &&
            !cancel.IsCancellationRequested
        )
        && staleIfError;

    async Task<Result> HandleFileMissingAsync(
        Uri uri,
        Action<HttpRequestMessage>? modifyRequest,
        Cancel cancel)
    {
        var invoker = GetInvoker();
        using var request = BuildRequest(uri, modifyRequest);
        var response = await invoker.SendAsyncEx(request, cancel);
        response.EnsureSuccess();
        if (response.IsNoStore())
        {
            return new(response);
        }

        using (response)
        {
            return await AddItemAsync(response, uri, cancel);
        }
    }

    Result HandleFileMissing(
        Uri uri,
        Action<HttpRequestMessage>? modifyRequest,
        Cancel cancel)
    {
        var invoker = GetInvoker();
        using var request = BuildRequest(uri, modifyRequest);
        var response = invoker.SendEx(request, cancel);
        response.EnsureSuccess();
        if (response.IsNoStore())
        {
            return new(response);
        }

        using (response)
        {
            return AddItem(response, uri, cancel);
        }
    }

    static HttpRequestMessage BuildRequest(Uri uri, Action<HttpRequestMessage>? modifyRequest)
    {
        HttpRequestMessage request = new(HttpMethod.Get, uri);
        modifyRequest?.Invoke(request);
        return request;
    }

    HttpMessageInvoker GetInvoker()
    {
        if (invoker != null)
        {
            return invoker;
        }

        if (clientFunc != null)
        {
            return clientFunc();
        }

        var newHandler = handlerFunc!();
        return new(newHandler, disposeHandler: false);
    }

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
        Cancel cancel = default)
    {
        var hash = Hash.Compute(uri.AbsoluteUri);
        var now = DateTimeOffset.Now;

        var timestamp = new Timestamp(
            expiry,
            modified.GetValueOrDefault(now),
            Etag.FromHeader(etag),
            hash);

        responseHeaders ??= [];
        contentHeaders ??= [];
        trailingHeaders ??= [];

        if (expiry != null)
        {
            contentHeaders.Add(
                nameof(HttpResponseHeader.Expires),
                expiry.Value.ToUniversalTime().ToString("r"));
        }

        if (modified != null)
        {
            responseHeaders.Add(
                nameof(HttpResponseHeader.LastModified),
                modified.Value.ToUniversalTime().ToString("r"));
        }

        var meta = MetaData.FromEnumerables(uri.AbsoluteUri, responseHeaders, contentHeaders, trailingHeaders);
        return InnerAddItemAsync(cancel, _ => Task.FromResult(stream), meta, timestamp);
    }

    Task<Result> AddItemAsync(HttpResponseMessage response, Uri uri, Cancel cancel)
    {
        var timestamp = Timestamp.FromResponse(uri, response);
        Task<Stream> ContentFunc(Cancel cancel) => response.Content.ReadAsStreamAsync(cancel);

        var meta = MetaData.FromEnumerables(uri.AbsoluteUri, response.Headers, response.Content.Headers, response.TrailingHeaders());
        return InnerAddItemAsync(cancel, ContentFunc, meta, timestamp);
    }

    static JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = true
    };

    async Task<Result> InnerAddItemAsync(
        Cancel cancel,
        Func<Cancel, Task<Stream>> httpContentFunc,
        MetaData meta,
        Timestamp timestamp)
    {
        var tempFile = FilePair.GetTemp();
        try
        {
#if NET7_0_OR_GREATER
            await using var httpStream = await httpContentFunc(cancel);
            await using (var contentFileStream = FileEx.OpenWrite(tempFile.Content))
            await using (var metaFileStream = FileEx.OpenWrite(tempFile.Meta))
            {
#else
            using var httpStream = await httpContentFunc(cancel);
            using (var contentFileStream = FileEx.OpenWrite(tempFile.Content))
            using (var metaFileStream = FileEx.OpenWrite(tempFile.Meta))
            {
#endif
                await JsonSerializer.SerializeAsync(metaFileStream, meta, serializerOptions, cancel);
                await httpStream.CopyToAsync(contentFileStream, cancel);
            }

            return BuildResult(timestamp, tempFile);
        }
        finally
        {
            tempFile.Delete();
        }
    }

    Result AddItem(HttpResponseMessage response, Uri uri, Cancel cancel)
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
            using var httpStream = response.Content.ReadAsStream(cancel);
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
            File.Move(tempFile.Content, contentFile, true);
            File.Move(tempFile.Meta, metaFile, true);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            //Failed to move files, so use temp files instead
            var newName = Path.GetFileNameWithoutExtension(contentFile);
            newName += Guid.NewGuid();

            var newMeta = $"{newName}.json";
            File.Move(tempFile.Meta, newMeta, true);
            if (File.Exists(tempFile.Content))
            {
                var newContent = $"{newName}.bin";
                File.Move(tempFile.Content, newContent, true);
                return new(new(newContent, newMeta), true, true);
            }

            return new(new(contentFile, newMeta), true, true);
        }

        return new(new(contentFile, metaFile), true, true);
    }
}
