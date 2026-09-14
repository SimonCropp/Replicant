static class DeriveCacheStatus
{
    public static CacheStatus GetCacheStatus(this HttpResponseMessage response, bool staleIfError, bool cache404)
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

        if (response.IsNoCache())
        {
            return CacheStatus.Revalidate;
        }

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

            response.EnsureSuccess();
        }

        return CacheStatus.Stored;
    }
}