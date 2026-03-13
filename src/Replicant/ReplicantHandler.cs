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
    public ReplicantHandler(string directory, int maxEntries = 1000, bool staleIfError = false) =>
        session = new(new CacheStore(directory, maxEntries), staleIfError);

    /// <summary>
    /// Instantiate a new instance of <see cref="ReplicantHandler"/>.
    /// </summary>
    /// <param name="directory">The directory to store the cache files.</param>
    /// <param name="innerHandler">The inner <see cref="HttpMessageHandler"/>.</param>
    /// <param name="maxEntries">The maximum entries to store in the cache.</param>
    /// <param name="staleIfError">If true, return stale cached content when the server returns an error.</param>
    public ReplicantHandler(string directory, HttpMessageHandler innerHandler, int maxEntries = 1000, bool staleIfError = false)
        : base(innerHandler) =>
        session = new(new CacheStore(directory, maxEntries), staleIfError);

    /// <summary>
    /// Instantiate a new instance of <see cref="ReplicantHandler"/> using a shared <see cref="ReplicantCache"/>.
    /// The cache is not disposed when this handler is disposed.
    /// </summary>
    /// <param name="cache">The shared cache.</param>
    /// <param name="staleIfError">If true, return stale cached content when the server returns an error.</param>
    public ReplicantHandler(ReplicantCache cache, bool staleIfError = false)
    {
        session = new(cache.Store, staleIfError);
        ownsSession = false;
    }

    /// <summary>
    /// Instantiate a new instance of <see cref="ReplicantHandler"/> using a shared <see cref="ReplicantCache"/>.
    /// The cache is not disposed when this handler is disposed.
    /// </summary>
    /// <param name="cache">The shared cache.</param>
    /// <param name="innerHandler">The inner <see cref="HttpMessageHandler"/>.</param>
    /// <param name="staleIfError">If true, return stale cached content when the server returns an error.</param>
    public ReplicantHandler(ReplicantCache cache, HttpMessageHandler innerHandler, bool staleIfError = false)
        : base(innerHandler)
    {
        session = new(cache.Store, staleIfError);
        ownsSession = false;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, Cancel cancel)
    {
        if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
        {
            return await base.SendAsync(request, cancel);
        }

        var uri = request.RequestUri!;
        var (_, _, resultFile, response) = await session.ProcessAsync(
            uri,
            timestamp =>
            {
                if (timestamp is { } t)
                {
                    t.ApplyHeadersToRequest(request);
                }

                return base.SendAsync(request, cancel);
            },
            cancel);

        return response ?? CacheStore.BuildResponseFromCache(resultFile!.Value);
    }

#if NET7_0_OR_GREATER

    protected override HttpResponseMessage Send(
        HttpRequestMessage request, Cancel cancel)
    {
        if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
        {
            return base.Send(request, cancel);
        }

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
