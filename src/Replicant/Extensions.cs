using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;

static class Extensions
{
    public static DateTimeOffset GetLastModified(this HttpResponseMessage response, DateTimeOffset now)
    {
        var contentHeaders = response.Content.Headers;
        if (contentHeaders.LastModified == null)
        {
            return now;
        }

        return contentHeaders.LastModified.Value;
    }

    public static DateTimeOffset? GetExpiry(this HttpResponseMessage response, DateTimeOffset now)
    {
        var responseHeaders = response.Headers;
        var contentHeaders = response.Content.Headers;

        if (contentHeaders.Expires != null)
        {
            return contentHeaders.Expires.Value;
        }

        var cacheControl = responseHeaders.CacheControl;
        if (cacheControl != null && cacheControl.MaxAge != null)
        {
            return now.Add(cacheControl.MaxAge.Value);
        }

        return null;
    }

    public static void AddIfNoneMatch(this  HttpRequestMessage request, Etag etag)
    {
        if (!etag.IsEmpty)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", etag.ForWeb);
        }
    }

    public static void AddRange(this HttpHeaders to, IEnumerable<KeyValuePair<string, IEnumerable<string>>> from)
    {
        foreach (var (key, value) in @from)
        {
            to.TryAddWithoutValidation(key, value);
        }
    }
}