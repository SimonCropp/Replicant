namespace Replicant;

public partial class HttpCache :
    IHttpCache
{
    CacheStore store;
    HttpClient? client;
    Func<HttpClient>? clientFunc;

    static readonly Lazy<HttpCache> defaultInstance = new(() =>
    {
        var directory = Path.Combine(FileEx.TempPath, "Replicant");
        return new(directory, new HttpClient());
    });

    public static HttpCache Default => defaultInstance.Value;

    public static Action<string> LogError = _ =>
    {
    };

    bool clientIsOwned;

    HttpCache(string directory, int maxEntries = 1000) =>
        store = new(directory, maxEntries, PurgeOld);

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
        Cancel cancel = default) =>
        DownloadAsync(new Uri(uri), staleIfError, modifyRequest, cancel);

    internal Task<Result> DownloadAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default)
    {
        var contentFile = store.FindContentFileForUri(uri);

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
        var contentFile = store.FindContentFileForUri(uri);

        if (contentFile == null)
        {
            return HandleFileMissing(uri, modifyRequest, cancel);
        }

        return HandleFileExists(uri, staleIfError, modifyRequest, contentFile.Value, cancel);
    }

    async Task<Result> HandleFileExistsAsync(
        Uri uri,
        bool staleIfError,
        Action<HttpRequestMessage>? modifyRequest,
        Cancel cancel,
        FilePair file)
    {
        var (revalidated, stored, resultFile, response) = await store.RevalidateAsync(
            file, staleIfError, uri,
            async timestamp =>
            {
                using var request = BuildRequest(uri, modifyRequest);
                if (!(timestamp.Expiry < DateTimeOffset.UtcNow))
                {
                    timestamp.ApplyHeadersToRequest(request);
                }

                return await GetClient().SendAsyncEx(request, cancel);
            },
            cancel);

        return response != null
            ? new(response)
            : new(resultFile!.Value, revalidated, stored);
    }

    Result HandleFileExists(
        Uri uri,
        bool staleIfError,
        Action<HttpRequestMessage>? modifyRequest,
        FilePair contentFile,
        Cancel cancel)
    {
        var (revalidated, stored, resultFile, response) = store.Revalidate(
            contentFile, staleIfError, uri,
            timestamp =>
            {
                using var request = BuildRequest(uri, modifyRequest);
                timestamp.ApplyHeadersToRequest(request);
                return GetClient().SendEx(request, cancel);
            },
            cancel);

        return response != null
            ? new(response)
            : new(resultFile!.Value, revalidated, stored);
    }

    async Task<Result> HandleFileMissingAsync(
        Uri uri,
        Action<HttpRequestMessage>? modifyRequest,
        Cancel cancel)
    {
        using var request = BuildRequest(uri, modifyRequest);
        var response = await GetClient().SendAsyncEx(request, cancel);
        var (file, passthrough) = await store.StoreNewResponseAsync(response, uri, cancel);
        return file != null ? new(file.Value, true, true) : new(passthrough!);
    }

    Result HandleFileMissing(
        Uri uri,
        Action<HttpRequestMessage>? modifyRequest,
        Cancel cancel)
    {
        using var request = BuildRequest(uri, modifyRequest);
        var response = GetClient().SendEx(request, cancel);
        var (file, passthrough) = store.StoreNewResponse(response, uri, cancel);
        return file != null ? new(file.Value, true, true) : new(passthrough!);
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
        return store.AddItemAsync(cancel, _ => Task.FromResult(stream), meta, timestamp);
    }

    async Task<Result> AddItemAsync(HttpResponseMessage response, Uri uri, Cancel cancel) =>
        new(await store.StoreResponseAsync(response, uri, cancel), true, true);

    Result AddItem(HttpResponseMessage response, Uri uri, Cancel cancel) =>
        new(store.StoreResponse(response, uri, cancel), true, true);
}
