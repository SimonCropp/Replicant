static class DeriveCacheStatus
{
    public static CacheStatus GetCacheStatus(this HttpResponseMessage response, bool staleIfError, bool cache404, bool throwOnError = true)
    {
        if (response.IsNoStore())
        {
            return CacheStatus.NoStore;
        }

        // Checked before no-cache: a 304 carrying no-cache confirms the cached content, it does not replace it
        if (response.IsNotModified())
        {
            return CacheStatus.Hit;
        }

        // Checked before no-cache: an error response carrying no-cache must not be stored as content
        if (!response.IsSuccessStatusCode)
        {
            if (cache404 && response.StatusCode == HttpStatusCode.NotFound)
            {
                return CacheStatus.Stored;
            }

            if (staleIfError)
            {
                return CacheStatus.UseStaleDueToError;
            }

            if (!throwOnError)
            {
                return CacheStatus.Error;
            }

            response.EnsureSuccess();
        }

        if (response.IsNoCache())
        {
            return CacheStatus.Revalidate;
        }

        return CacheStatus.Stored;
    }
}