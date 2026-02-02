namespace Replicant;

/// <summary>
/// A <see cref="DelegatingHandler"/> that provides transparent HTTP response caching.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CachingHandler"/> implements RFC 7234 HTTP caching semantics by intercepting
/// GET and HEAD requests and storing responses to disk. Subsequent requests for the same
/// resource return cached responses when valid, reducing network traffic and latency.
/// </para>
/// <para>
/// Only GET and HEAD requests are cached. Other HTTP methods (POST, PUT, DELETE, PATCH)
/// bypass the cache and forward directly to the inner handler.
/// </para>
/// <para>
/// Cache status is reported via the <see cref="CacheStatusHeaderName"/> response header:
/// </para>
/// <list type="bullet">
/// <item><description><c>hit</c> - Response returned from cache without revalidation</description></item>
/// <item><description><c>miss</c> - Response fetched from origin and stored in cache</description></item>
/// <item><description><c>revalidate</c> - Cached response revalidated with origin (304 Not Modified)</description></item>
/// <item><description><c>no-store</c> - Response not cached due to Cache-Control directives</description></item>
/// <item><description><c>stale</c> - Stale response returned due to network error (stale-if-error)</description></item>
/// </list>
/// </remarks>
/// <example>
/// Basic usage:
/// <code>
/// var handler = new CachingHandler();
/// var httpClient = new HttpClient(handler);
/// var response = await httpClient.GetAsync("https://httpbin.org/json");
/// </code>
/// </example>
/// <example>
/// With HttpClientFactory:
/// <code>
/// services.AddHttpClient("cached")
///     .ConfigurePrimaryHttpMessageHandler(() => new CachingHandler());
/// </code>
/// </example>
/// <example>
/// With stale-if-error support:
/// <code>
/// var handler = new CachingHandler { StaleIfError = true };
/// var httpClient = new HttpClient(handler);
/// </code>
/// </example>
public class CachingHandler : DelegatingHandler
{
    /// <summary>
    /// The name of the response header that indicates cache status.
    /// </summary>
    public const string CacheStatusHeaderName = "X-Replicant-Cache-Status";

    /// <summary>
    /// The name of the request header that can override the <see cref="StaleIfError"/> property.
    /// </summary>
    public const string StaleIfErrorHeaderName = "X-Replicant-Stale-If-Error";

    readonly HttpCache httpCache;

    /// <summary>
    /// Gets or sets whether to return stale cached responses when the origin server is unavailable.
    /// </summary>
    /// <remarks>
    /// When enabled, if a request fails due to a network error and a stale cached response exists,
    /// the stale response is returned instead of propagating the error. This can be overridden
    /// per-request using the <see cref="StaleIfErrorHeaderName"/> header.
    /// </remarks>
    public bool StaleIfError { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="CachingHandler"/> using the default cache directory.
    /// </summary>
    /// <param name="maxEntries">
    /// The maximum number of cache entries to retain. Older entries are purged when this limit is exceeded.
    /// Default is 1000.
    /// </param>
    /// <remarks>
    /// The default cache directory is located at <c>Path.Combine(Path.GetTempPath(), "Replicant")</c>.
    /// </remarks>
    public CachingHandler(int maxEntries = 1000)
        : this(Path.Combine(Path.GetTempPath(), "Replicant"), maxEntries)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CachingHandler"/> with a custom cache directory.
    /// </summary>
    /// <param name="cacheDirectory">
    /// The directory path where cached responses will be stored.
    /// </param>
    /// <param name="maxEntries">
    /// The maximum number of cache entries to retain. Older entries are purged when this limit is exceeded.
    /// Default is 1000.
    /// </param>
    public CachingHandler(string cacheDirectory, int maxEntries = 1000)
        : this(cacheDirectory, new HttpClientHandler(), maxEntries)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CachingHandler"/> with a custom cache directory
    /// and inner handler.
    /// </summary>
    /// <param name="cacheDirectory">
    /// The directory path where cached responses will be stored.
    /// </param>
    /// <param name="innerHandler">
    /// The inner <see cref="HttpMessageHandler"/> to delegate requests to when cache misses occur.
    /// </param>
    /// <param name="maxEntries">
    /// The maximum number of cache entries to retain. Older entries are purged when this limit is exceeded.
    /// Default is 1000.
    /// </param>
    public CachingHandler(string cacheDirectory, HttpMessageHandler innerHandler, int maxEntries = 1000)
    {
        InnerHandler = innerHandler;

        // Create a single HttpClient that wraps the inner handler
        // We don't dispose the inner handler since DelegatingHandler will handle that
        var wrappedClient = new HttpClient(InnerHandler, disposeHandler: false);

        httpCache = new HttpCache(
            cacheDirectory,
            wrappedClient,
            maxEntries);
    }

    /// <summary>
    /// Removes all cached responses from disk.
    /// </summary>
    public void Purge() =>
        httpCache.Purge();

    /// <summary>
    /// Removes cached responses that exceed the maximum entry limit.
    /// </summary>
    /// <remarks>
    /// Entries are removed in least-recently-used order until the cache size
    /// is within the <c>maxEntries</c> limit specified in the constructor.
    /// </remarks>
    public void PurgeOld() =>
        httpCache.PurgeOld();

    /// <summary>
    /// Sends an HTTP request and returns the response, utilizing the cache for GET and HEAD requests.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <param name="cancellationToken">
    /// A cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the HTTP response message.
    /// </returns>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Only cache GET and HEAD requests
        if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // Check for cache bypass directives
        if (ShouldBypassCache(request))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // Determine stale-if-error setting (header overrides property)
        var staleIfError = StaleIfError;
        if (request.Headers.TryGetValues(StaleIfErrorHeaderName, out var headerValues))
        {
            var headerValue = headerValues.FirstOrDefault();
            if (bool.TryParse(headerValue, out var parsedValue))
            {
                staleIfError = parsedValue;
            }
        }

        // Use HttpCache to handle caching logic
        var result = await httpCache.DownloadAsync(
            request.RequestUri!.ToString(),
            staleIfError,
            modifyRequest: internalRequest =>
            {
                // Copy HTTP method
                internalRequest.Method = request.Method;

                // Copy all headers from original request
                foreach (var header in request.Headers)
                {
                    internalRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                // Copy content headers if present
                if (request.Content is not null && internalRequest.Content is not null)
                {
                    foreach (var header in request.Content.Headers)
                    {
                        internalRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
            },
            cancellationToken);

        // Convert Result to HttpResponseMessage
        var response = await result.AsResponseMessageAsync();

        // Add cache status header
        var status = GetCacheStatus(result);
        response.Headers.TryAddWithoutValidation(CacheStatusHeaderName, status);

        // Preserve request context
        response.RequestMessage = request;

        return response;
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="CachingHandler"/> and optionally
    /// releases the managed resources.
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/> to release both managed and unmanaged resources;
    /// <see langword="false"/> to release only unmanaged resources.
    /// </param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            httpCache.Dispose();
        }

        base.Dispose(disposing);
    }

    static bool ShouldBypassCache(HttpRequestMessage request)
    {
        if (request.Headers.CacheControl == null)
        {
            return false;
        }

        return request.Headers.CacheControl.NoCache ||
               request.Headers.CacheControl.NoStore;
    }

    static string GetCacheStatus(Result result)
    {
        // Hit: returned from disk without revalidation
        if (result.FromDisk && !result.Revalidated && !result.Stored)
        {
            return "hit";
        }

        // Miss: fetched from origin and stored
        if (!result.FromDisk && result.Stored)
        {
            return "miss";
        }

        // Revalidate: cached response revalidated with 304
        if (result.FromDisk && result.Revalidated)
        {
            return "revalidate";
        }

        // Stale: returned stale response due to error (stale-if-error)
        if (result.FromDisk && result.Stored)
        {
            return "stale";
        }

        // No-store: not cached due to cache control directives
        return "no-store";
    }
}
