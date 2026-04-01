static class DeriveCacheStatus
{
    public static CacheStatus GetCacheStatus(this HttpResponseMessage response, bool staleIfError, bool cache404)
    {
        if (response.IsNoStore())
        {
            return CacheStatus.NoStore;
        }

        if (response.IsNoCache())
        {
            return CacheStatus.Revalidate;
        }

        if (response.IsNotModified())
        {
            return CacheStatus.Hit;
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