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

    int maxRetries;

    HttpCache(string directory, int maxEntries = 1000, int maxRetries = 0)
    {
        if (maxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "maxRetries must be greater than or equal to 0");
        }

        this.maxRetries = maxRetries;
        store = new(directory, maxEntries, PurgeOld);
    }

    /// <summary>
    /// Instantiate a new instance of <see cref="HttpCache"/>.
    /// </summary>
    /// <param name="directory">The directory to store the cache files.</param>
    /// <param name="clientFunc">A factory to retrieve a <see cref="HttpClient"/> each time a resource is downloaded.</param>
    /// <param name="maxEntries">The maximum entries to store in the cache.</param>
    /// <param name="maxRetries">The maximum number of retries for transient HTTP failures. Default is 0 (no retries).</param>
    public HttpCache(string directory, Func<HttpClient> clientFunc, int maxEntries = 1000, int maxRetries = 0) :
        this(directory, maxEntries, maxRetries) =>
        this.clientFunc = clientFunc;

    /// <summary>
    /// Instantiate a new instance of <see cref="HttpCache"/>.
    /// </summary>
    /// <param name="directory">The directory to store the cache files.</param>
    /// <param name="client">The <see cref="HttpCache"/> to use do web calls. If not supplied an instance will be instantiate.</param>
    /// <param name="maxEntries">The maximum entries to store in the cache.</param>
    /// <param name="maxRetries">The maximum number of retries for transient HTTP failures. Default is 0 (no retries).</param>
    public HttpCache(string directory, HttpClient? client = null, int maxEntries = 1000, int maxRetries = 0) :
        this(directory, maxEntries, maxRetries)
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

    internal async Task<Result> DownloadAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default)
    {
        var session = new CacheSession(store, staleIfError, maxRetries);
        var (revalidated, stored, resultFile, response) = await session.ProcessAsync(
            uri,
            async timestamp =>
            {
                using var request = BuildRequest(uri, modifyRequest);
                if (timestamp is { } t && !(t.Expiry < DateTimeOffset.UtcNow))
                {
                    t.ApplyHeadersToRequest(request);
                }

                return await GetClient().SendAsyncEx(request, cancel);
            },
            cancel);

        return response != null
            ? new(response)
            : new(resultFile!.Value, revalidated, stored);
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
        var session = new CacheSession(store, staleIfError, maxRetries);
        var (revalidated, stored, resultFile, response) = session.Process(
            uri,
            timestamp =>
            {
                using var request = BuildRequest(uri, modifyRequest);
                if (timestamp is { } t)
                {
                    t.ApplyHeadersToRequest(request);
                }

                return GetClient().SendEx(request, cancel);
            },
            cancel);

        return response != null
            ? new(response)
            : new(resultFile!.Value, revalidated, stored);
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
