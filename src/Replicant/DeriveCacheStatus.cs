using System.Net.Http;

static class DeriveCacheStatus
{
    public static CacheStatus GetCacheStatus(this HttpResponseMessage response, bool staleIfError)
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
            return CacheStatus.Revalidate;
        }

        if (!response.IsSuccessStatusCode)
        {
            if (staleIfError)
            {
                return CacheStatus.UseStaleDueToError;
            }

            response.EnsureSuccess();
        }

        return CacheStatus.Stored;
    }
}