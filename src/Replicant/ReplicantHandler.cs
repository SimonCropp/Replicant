namespace Replicant;

/// <summary>
/// A <see cref="DelegatingHandler"/> that caches HTTP GET and HEAD responses to disk.
/// </summary>
public class ReplicantHandler : DelegatingHandler
{
    CacheStore store;
    bool staleIfError;

    /// <summary>
    /// Instantiate a new instance of <see cref="ReplicantHandler"/>.
    /// </summary>
    /// <param name="directory">The directory to store the cache files.</param>
    /// <param name="maxEntries">The maximum entries to store in the cache.</param>
    /// <param name="staleIfError">If true, return stale cached content when the server returns an error.</param>
    public ReplicantHandler(string directory, int maxEntries = 1000, bool staleIfError = false)
    {
        store = new(directory, maxEntries);
        this.staleIfError = staleIfError;
    }

    /// <summary>
    /// Instantiate a new instance of <see cref="ReplicantHandler"/>.
    /// </summary>
    /// <param name="directory">The directory to store the cache files.</param>
    /// <param name="innerHandler">The inner <see cref="HttpMessageHandler"/>.</param>
    /// <param name="maxEntries">The maximum entries to store in the cache.</param>
    /// <param name="staleIfError">If true, return stale cached content when the server returns an error.</param>
    public ReplicantHandler(string directory, HttpMessageHandler innerHandler, int maxEntries = 1000, bool staleIfError = false)
        : base(innerHandler)
    {
        store = new(directory, maxEntries);
        this.staleIfError = staleIfError;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, Cancel cancel)
    {
        if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
        {
            return base.SendAsync(request, cancel);
        }

        var uri = request.RequestUri!;
        var file = store.FindContentFileForUri(uri);

        if (file == null)
        {
            return HandleFileMissingAsync(request, uri, cancel);
        }

        return HandleFileExistsAsync(request, uri, cancel, file.Value);
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
        var file = store.FindContentFileForUri(uri);

        if (file == null)
        {
            return HandleFileMissing(request, uri, cancel);
        }

        return HandleFileExists(request, uri, cancel, file.Value);
    }

#endif

    async Task<HttpResponseMessage> HandleFileMissingAsync(
        HttpRequestMessage request, Uri uri, Cancel cancel)
    {
        var response = await base.SendAsync(request, cancel);
        response.EnsureSuccess();

        if (response.IsNoStore())
        {
            return response;
        }

        using (response)
        {
            var filePair = await store.StoreResponseAsync(response, uri, cancel);
            return CacheStore.BuildResponseFromCache(filePair);
        }
    }

    async Task<HttpResponseMessage> HandleFileExistsAsync(
        HttpRequestMessage request, Uri uri, Cancel cancel, FilePair file)
    {
        var now = DateTimeOffset.UtcNow;
        var timestamp = Timestamp.FromPath(file.Content);
        var expiry = timestamp.Expiry;

        if (expiry == null || expiry > now)
        {
            return CacheStore.BuildResponseFromCache(file);
        }

        timestamp.ApplyHeadersToRequest(request);

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancel);
        }
        catch (Exception exception)
        {
            if (CacheStore.ShouldReturnStaleIfError(staleIfError, exception, cancel))
            {
                return CacheStore.BuildResponseFromCache(file);
            }

            throw;
        }

        var (_, resultFile) = await store.HandleCacheStatusAsync(response, staleIfError, file, uri, cancel);
        if (resultFile == null)
        {
            return response;
        }

        return CacheStore.BuildResponseFromCache(resultFile.Value);
    }

#if NET7_0_OR_GREATER

    HttpResponseMessage HandleFileMissing(
        HttpRequestMessage request, Uri uri, Cancel cancel)
    {
        var response = base.Send(request, cancel);
        response.EnsureSuccess();

        if (response.IsNoStore())
        {
            return response;
        }

        using (response)
        {
            var filePair = store.StoreResponse(response, uri, cancel);
            return CacheStore.BuildResponseFromCache(filePair);
        }
    }

    HttpResponseMessage HandleFileExists(
        HttpRequestMessage request, Uri uri, Cancel cancel, FilePair file)
    {
        var now = DateTimeOffset.UtcNow;
        var timestamp = Timestamp.FromPath(file.Content);
        var expiry = timestamp.Expiry;

        if (expiry == null || expiry > now)
        {
            return CacheStore.BuildResponseFromCache(file);
        }

        timestamp.ApplyHeadersToRequest(request);

        HttpResponseMessage response;
        try
        {
            response = base.Send(request, cancel);
        }
        catch (Exception exception)
        {
            if (CacheStore.ShouldReturnStaleIfError(staleIfError, exception, cancel))
            {
                return CacheStore.BuildResponseFromCache(file);
            }

            throw;
        }

        var (_, resultFile) = store.HandleCacheStatus(response, staleIfError, file, uri, cancel);
        if (resultFile == null)
        {
            return response;
        }

        return CacheStore.BuildResponseFromCache(resultFile.Value);
    }

#endif

    /// <summary>
    /// Purge all cached items.
    /// </summary>
    public void Purge() => store.Purge();

    /// <summary>
    /// Purge cached items that exceed max entries, ordered by last access time.
    /// </summary>
    public void PurgeOld() => store.PurgeOld();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            store.Dispose();
        }

        base.Dispose(disposing);
    }
}
