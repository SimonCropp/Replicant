namespace Replicant;

/// <summary>
/// A <see cref="DelegatingHandler"/> that caches HTTP GET and HEAD responses to disk.
/// </summary>
public class ReplicantHandler : DelegatingHandler
{
    CacheSession session;
    bool ownsSession = true;

    /// <summary>
    /// Instantiate a new instance of <see cref="ReplicantHandler"/>.
    /// </summary>
    /// <param name="directory">The directory to store the cache files.</param>
    /// <param name="maxEntries">The maximum entries to store in the cache.</param>
    /// <param name="staleIfError">If true, return stale cached content when the server returns an error.</param>
    /// <param name="cache404">If true, cache 404 Not Found responses.</param>
    /// <param name="maxRetries">The maximum number of retries for transient HTTP failures. Default is 0 (no retries).</param>
    /// <param name="minFreshness">Minimum time a cached entry is considered fresh, overriding server cache headers.</param>
    public ReplicantHandler(string directory, int maxEntries = 1000, bool staleIfError = false, bool cache404 = false, int maxRetries = 0, TimeSpan? minFreshness = null) =>
        session = new(new CacheStore(directory, maxEntries), staleIfError, cache404, maxRetries, minFreshness);

    /// <summary>
    /// Instantiate a new instance of <see cref="ReplicantHandler"/>.
    /// </summary>
    /// <param name="directory">The directory to store the cache files.</param>
    /// <param name="innerHandler">The inner <see cref="HttpMessageHandler"/>.</param>
    /// <param name="maxEntries">The maximum entries to store in the cache.</param>
    /// <param name="staleIfError">If true, return stale cached content when the server returns an error.</param>
    /// <param name="cache404">If true, cache 404 Not Found responses.</param>
    /// <param name="maxRetries">The maximum number of retries for transient HTTP failures. Default is 0 (no retries).</param>
    /// <param name="minFreshness">Minimum time a cached entry is considered fresh, overriding server cache headers.</param>
    public ReplicantHandler(string directory, HttpMessageHandler innerHandler, int maxEntries = 1000, bool staleIfError = false, bool cache404 = false, int maxRetries = 0, TimeSpan? minFreshness = null)
        : base(innerHandler) =>
        session = new(new CacheStore(directory, maxEntries), staleIfError, cache404, maxRetries, minFreshness);

    /// <summary>
    /// Instantiate a new instance of <see cref="ReplicantHandler"/> using a shared <see cref="ReplicantCache"/>.
    /// The cache is not disposed when this handler is disposed.
    /// </summary>
    /// <param name="cache">The shared cache.</param>
    /// <param name="staleIfError">If true, return stale cached content when the server returns an error.</param>
    /// <param name="cache404">If true, cache 404 Not Found responses.</param>
    /// <param name="maxRetries">The maximum number of retries for transient HTTP failures. Default is 0 (no retries).</param>
    /// <param name="minFreshness">Minimum time a cached entry is considered fresh, overriding server cache headers.</param>
    public ReplicantHandler(ReplicantCache cache, bool staleIfError = false, bool cache404 = false, int maxRetries = 0, TimeSpan? minFreshness = null)
    {
        session = new(cache.Store, staleIfError, cache404, maxRetries, minFreshness);
        ownsSession = false;
    }

    /// <summary>
    /// Instantiate a new instance of <see cref="ReplicantHandler"/> using a shared <see cref="ReplicantCache"/>.
    /// The cache is not disposed when this handler is disposed.
    /// </summary>
    /// <param name="cache">The shared cache.</param>
    /// <param name="innerHandler">The inner <see cref="HttpMessageHandler"/>.</param>
    /// <param name="staleIfError">If true, return stale cached content when the server returns an error.</param>
    /// <param name="cache404">If true, cache 404 Not Found responses.</param>
    /// <param name="maxRetries">The maximum number of retries for transient HTTP failures. Default is 0 (no retries).</param>
    /// <param name="minFreshness">Minimum time a cached entry is considered fresh, overriding server cache headers.</param>
    public ReplicantHandler(ReplicantCache cache, HttpMessageHandler innerHandler, bool staleIfError = false, bool cache404 = false, int maxRetries = 0, TimeSpan? minFreshness = null)
        : base(innerHandler)
    {
        session = new(cache.Store, staleIfError, cache404, maxRetries, minFreshness);
        ownsSession = false;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, Cancel cancel)
    {
        if (request.Method != HttpMethod.Get &&
            request.Method != HttpMethod.Head)
        {
            return base.SendAsync(request, cancel);
        }

        return SendAsyncCore(request, cancel);
    }

    async Task<HttpResponseMessage> SendAsyncCore(
        HttpRequestMessage request, Cancel cancel)
    {
        var uri = request.RequestUri!;
        var (_, _, resultFile, response) = await session.ProcessAsync(
            uri,
            possibleTimestamp =>
            {
                if (possibleTimestamp is { } timestamp)
                {
                    timestamp.ApplyHeadersToRequest(request);
                }

                return base.SendAsync(request, cancel);
            },
            cancel);

        return response ?? CacheStore.BuildResponseFromCache(resultFile!.Value);
    }

#if NET8_0_OR_GREATER

    protected override HttpResponseMessage Send(
        HttpRequestMessage request, Cancel cancel)
    {
        if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
        {
            return base.Send(request, cancel);
        }

        return SendCore(request, cancel);
    }

    HttpResponseMessage SendCore(
        HttpRequestMessage request, Cancel cancel)
    {
        var uri = request.RequestUri!;
        var (_, _, resultFile, response) = session.Process(
            uri,
            timestamp =>
            {
                if (timestamp is { } t)
                {
                    t.ApplyHeadersToRequest(request);
                }

                return base.Send(request, cancel);
            },
            cancel);

        return response ?? CacheStore.BuildResponseFromCache(resultFile!.Value);
    }

#endif

    /// <summary>
    /// Purge all cached items.
    /// </summary>
    public void Purge() => session.Purge();

    /// <summary>
    /// Purge cached items that exceed max entries, ordered by last access time.
    /// </summary>
    public void PurgeOld() => session.PurgeOld();

    protected override void Dispose(bool disposing)
    {
        if (disposing && ownsSession)
        {
            session.Dispose();
        }

        base.Dispose(disposing);
    }
}
