using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

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

    public static async Task<HttpResponseMessage> SendAsyncEx(
        this HttpClient client,
        HttpRequestMessage request,
        CancellationToken token)
    {
        try
        {
            return await client.SendAsync(request, token);
        }
        catch (HttpRequestException exception)
        {
            throw new HttpRequestException($"{exception.Message}. Uri: {request}", exception.InnerException, exception.StatusCode);
        }
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