using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

static class Extensions
{
    public static bool TryGetETag(
        this HttpResponseMessage response,
        [NotNullWhen(true)]out bool? weak,
        [NotNullWhen(true)] out string? value)
    {
        if (response.Headers.TryGetValues("ETag", out var values))
        {
            var tag = values.First();
            if (tag.StartsWith("W/"))
            {
                weak = true;
                value = tag[2..].Trim('"');
            }
            else
            {
                weak = false;
                value = tag.Trim('"');
            }

            return true;
        }

        weak = null;
        value = null;
        return false;
    }

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

    public static void AddRange(this HttpHeaders to, IEnumerable<KeyValuePair<string, IEnumerable<string>>> from)
    {
        foreach (var (key, value) in from)
        {
            to.TryAddWithoutValidation(key, value);
        }
    }
}