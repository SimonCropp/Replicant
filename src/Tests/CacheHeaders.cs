static class CacheHeaders
{
    // Responses without expiry information are revalidated on every use,
    // so tests that expect a cache hit need an explicit freshness lifetime
    public static CacheControlHeaderValue OneDay =>
        new()
        {
            MaxAge = TimeSpan.FromDays(1)
        };
}
