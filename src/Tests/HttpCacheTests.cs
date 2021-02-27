using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Replicant;
using VerifyXunit;
using Xunit;

[UsesVerify]
public class HttpCacheTests
{
    HttpCache httpCache;
    static string CachePath = Path.Combine(Path.GetTempPath(), "DownloadTests");

    public HttpCacheTests()
    {
        httpCache = new(CachePath);
        httpCache.Purge();
    }

    [Fact]
    public async Task EmptyContent()
    {
        var content = await httpCache.Download("https://httpbin.org/status/200");
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task Etag()
    {
        var uri = "https://httpbin.org/etag/{etag}";
        await httpCache.Download(uri);
        var content = await httpCache.Download(uri);
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task WeakEtag()
    {
        var uri = "https://www.wikipedia.org/";
        Result content;
        content = await httpCache.Download(uri);
        HttpResponseMessage newMessage = new(HttpStatusCode.OK);
        newMessage.Headers.ETag = (await content.GetResponseHeaders()).ETag;
        await httpCache.AddItem(uri, newMessage);
        content = await httpCache.Download(uri);
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task CacheControlMaxAge()
    {
        var uri = "https://httpbin.org/cache/20";
        await httpCache.Download(uri);
        var content = await httpCache.Download(uri);
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task WithContent()
    {
        var content = await httpCache.Download("https://httpbin.org/json");
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task String()
    {
        #region string
        var content = await httpCache.String("https://httpbin.org/json");
        #endregion
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task Bytes()
    {
        #region bytes
        var bytes = await httpCache.Bytes("https://httpbin.org/json");
        #endregion
        await Verifier.Verify(bytes);
    }

    [Fact]
    public async Task Stream()
    {
        #region stream
        await using var stream = await httpCache.Stream("https://httpbin.org/json");
        #endregion
        await Verifier.Verify(stream);
    }

    [Fact]
    public async Task ToFile()
    {
        var targetFile = $"{Path.GetTempFileName()}.txt";
        try
        {
            #region ToFile
            await httpCache.ToFile("https://httpbin.org/json", targetFile);
            #endregion
            await Verifier.VerifyFile(targetFile);
        }
        finally
        {
            File.Delete(targetFile);
        }
    }

    [Fact]
    public async Task ToStream()
    {
        MemoryStream targetStream = new();
        #region ToStream
        await httpCache.ToStream("https://httpbin.org/json", targetStream);
        #endregion
        await Verifier.Verify(targetStream);
    }

    [Fact]
    public async Task Callback()
    {
        var uri = "https://httpbin.org/json";

        #region Callback

        var content = await httpCache.String(
            uri,
            messageCallback: message =>
            {
                message.Headers.Add("Key1", "Value1");
                message.Headers.Add("Key2", "Value2");
            });

        #endregion

        await Verifier.Verify(content);
    }

    [Fact]
    public Task NotFound()
    {
        return Verifier.ThrowsTask(() => httpCache.String("https://httpbin.org/status/404"));
    }

    [Fact]
    public async Task Timeout()
    {
        HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromMilliseconds(1)
        };
        httpCache = new(CachePath, httpClient);
        httpCache.Purge();
        var uri = "https://httpbin.org/status/200";
        var exception = await Assert.ThrowsAsync<TaskCanceledException>(() => httpCache.String(uri));
        await Verifier.Verify(exception.Message);
    }

    [Fact]
    public async Task TimeoutUseStale()
    {
        HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromMilliseconds(1)
        };
        httpCache = new(CachePath, httpClient);
        httpCache.Purge();
        #region AddItem
        var uri = "https://httpbin.org/status/200";
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent("the content")
        };
        await httpCache.AddItem(uri, response);
        #endregion
        await Verifier.Verify(httpCache.Download(uri, true));
    }

    [Fact]
    public Task ServerError()
    {
        return Verifier.ThrowsTask(() => httpCache.String("https://httpbin.org/status/500"));
    }

    [Fact]
    public async Task ServerErrorDontUseStale()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent("foo")
        };
        var uri = "https://httpbin.org/status/500";
        await httpCache.AddItem(uri, response);
        await Verifier.ThrowsTask(() => httpCache.String(uri));
    }

    [Fact]
    public async Task ServerErrorUseStale()
    {
        using HttpResponseMessage httpResponseMessage = new(HttpStatusCode.OK)
        {
            Content = new StringContent("content")
        };
        var uri = "https://httpbin.org/status/500";
        await httpCache.AddItem(uri, httpResponseMessage);
        #region useStaleOnError
        var content = httpCache.String(uri, useStaleOnError: true);
        #endregion
        await Verifier.Verify(content);
    }
}