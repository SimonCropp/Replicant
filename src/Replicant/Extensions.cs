﻿// ReSharper disable RedundantUsingDirective
using System.Reflection;
// ReSharper restore RedundantUsingDirective
using System.Net;
using System.Net.Http.Headers;

static class Extensions
{
#if NET7_0_OR_GREATER

    public static void CopyTo(this HttpContent content, Stream target, Cancellation cancellation) =>
        content.CopyTo(target, null, cancellation);

    public static HttpResponseMessage SendEx(
        this HttpClient client,
        HttpRequestMessage request,
        Cancellation cancellation)
    {
        try
        {
            return client.Send(request);
        }
        catch (HttpRequestException exception)
        {
            throw BuildHttpException(request, exception);
        }
    }

#else

    public static HttpResponseMessage SendEx(
        this HttpClient client,
        HttpRequestMessage request,
        Cancellation cancellation)
    {
        try
        {
            // Called outside of async state machine to propagate certain exception even without awaiting the returned task.
            // See decompile send on net 5 version of http client

            //SetOperationStarted();
            var setOperationStarted = typeof(HttpClient).GetMethod("SetOperationStarted", BindingFlags.Instance | BindingFlags.NonPublic)!;
            setOperationStarted.Invoke(client, null);

            //PrepareRequestMessage(request);
            var prepareRequestMessage = typeof(HttpClient).GetMethod("PrepareRequestMessage", BindingFlags.Instance | BindingFlags.NonPublic)!;
            prepareRequestMessage.Invoke(client, new object?[] {request});

            return client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellation)
                .GetAwaiter().GetResult();
        }
        catch (HttpRequestException exception)
        {
            throw BuildHttpException(request, exception);
        }
    }

    public static Task<Stream> ReadAsStreamAsync(this HttpContent content, Cancellation cancellation) =>
        content.ReadAsStreamAsync();

    public static Task<byte[]> ReadAsByteArrayAsync(this HttpContent content, Cancellation cancellation) =>
        content.ReadAsByteArrayAsync();

    public static Task<string> ReadAsStringAsync(this HttpContent content, Cancellation cancellation) =>
        content.ReadAsStringAsync();

    public static Task CopyToAsync(this Stream source, Stream target, Cancellation cancellation) =>
        source.CopyToAsync(target);

    public static Task CopyToAsync(this HttpContent content, Stream target, Cancellation cancellation) =>
        content.CopyToAsync(target);

    public static void CopyTo(this HttpContent content, Stream target, Cancellation cancellation) =>
        content.CopyToAsync(target).GetAwaiter().GetResult();

    public static Stream ReadAsStream(this HttpContent content, Cancellation cancellation) =>
        content.ReadAsStreamAsync().GetAwaiter().GetResult();

#endif

    public static DateTimeOffset GetLastModified(this HttpResponseMessage response, DateTimeOffset now)
    {
        var contentHeaders = response.Content.Headers;
        if (contentHeaders.LastModified == null)
        {
            return now;
        }

        return contentHeaders.LastModified.Value;
    }

    public static bool IsNoCache(this HttpResponseMessage response)
    {
        var cacheControl = response.Headers.CacheControl;
        return cacheControl is {NoCache: true};
    }

    public static bool IsNoStore(this HttpResponseMessage response)
    {
        var cacheControl = response.Headers.CacheControl;
        return cacheControl is {NoStore: true};
    }

    public static bool IsNotModified(this HttpResponseMessage response) =>
        response.StatusCode == HttpStatusCode.NotModified;

    public static void EnsureSuccess(this HttpResponseMessage response)
    {
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException exception)
        {
            throw BuildHttpException(response, exception);
        }
    }

    static HttpRequestException BuildHttpException(HttpResponseMessage response, HttpRequestException exception)
    {
        var uri = response.RequestMessage?.RequestUri?.OriginalString;
        var message = $"{exception.Message} Uri: {uri}";
#if NET7_0_OR_GREATER
        return new(message, exception.InnerException, exception.StatusCode);
#else
        return new(message, exception.InnerException);
#endif
    }

    static HttpRequestException BuildHttpException(HttpRequestMessage request, HttpRequestException exception)
    {
        var uri = request.RequestUri?.OriginalString;
        var message = $"{exception.Message} Uri: {uri}";
#if NET7_0_OR_GREATER
        return new(message, exception.InnerException, exception.StatusCode);
#else
        return new(message, exception.InnerException);
#endif
    }

    public static async Task<HttpResponseMessage> SendAsyncEx(
        this HttpClient client,
        HttpRequestMessage request,
        Cancellation cancellation)
    {
        try
        {
            return await client.SendAsync(request, cancellation);
        }
        catch (HttpRequestException exception)
        {
            throw BuildHttpException(request, exception);
        }
    }

    public static IEnumerable<KeyValuePair<string, IEnumerable<string>>> TrailingHeaders(
        this HttpResponseMessage response)
    {
#if NET7_0_OR_GREATER
        return response.TrailingHeaders;
#else
        return new List<KeyValuePair<string, IEnumerable<string>>>();
#endif
    }

    public static Stream AsStream(this string value)
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(value);
        writer.Flush();
        stream.Position = 0;
        return stream;
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
        if (cacheControl?.MaxAge != null)
        {
            return now.Add(cacheControl.MaxAge.Value);
        }

        return null;
    }

    public static void AddIfNoneMatch(this HttpRequestMessage request, Etag etag)
    {
        if (!etag.IsEmpty)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", etag.ForWeb);
        }
    }

    public static void AddRange(this HttpHeaders to, List<KeyValuePair<string, List<string>>> from)
    {
        foreach (var keyValue in from)
        {
            to.TryAddWithoutValidation(keyValue.Key, keyValue.Value);
        }
    }
}