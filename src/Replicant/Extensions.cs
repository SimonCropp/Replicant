using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;

static class Extensions
{
    public static string? GetETag(this HttpResponseMessage response)
    {
        return response.Headers.ETag?.Tag;
    }

    public static DateTime GetLastModified(this HttpResponseMessage response, DateTime now)
    {
        var contentHeaders = response.Content.Headers;
        if (contentHeaders.LastModified == null)
        {
            return now;
        }

        return contentHeaders.LastModified.Value.UtcDateTime;
    }

    public static DateTime? GetExpiry(this HttpResponseMessage response, DateTime now)
    {
        var responseHeaders = response.Headers;
        var contentHeaders = response.Content.Headers;

        if (contentHeaders.Expires != null)
        {
            return contentHeaders.Expires.Value.UtcDateTime;
        }

        var cacheControl = responseHeaders.CacheControl;
        if (cacheControl != null && cacheControl.MaxAge != null)
        {
            return now.Add(cacheControl.MaxAge.Value);
        }

        return null;
    }

    public static void AddRange(this HttpHeaders to, IEnumerable<KeyValuePair<string, IEnumerable<string>>> from)
    {
        foreach (var (key, value) in from)
        {
            to.TryAddWithoutValidation(key, value);
        }
    }
}