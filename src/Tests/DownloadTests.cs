using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Replicant;
using VerifyXunit;
using Xunit;

[UsesVerify]
public class DownloadTests
{
    Download download;
    static string CachePath = Path.Combine(Path.GetTempPath(), "DownloadTests");

    public DownloadTests()
    {
        download = new(CachePath);
        download.Purge();
    }

    [Fact]
    public async Task EmptyContent()
    {
        var content = await download.DownloadFile("https://httpbin.org/status/200");
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task Etag()
    {
        var uri = "https://httpbin.org/etag/{etag}";
        await download.DownloadFile(uri);
        var content = await download.DownloadFile(uri);
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task WeakEtag()
    {
        var uri = "https://www.wikipedia.org/";
        Result content;
        content = await download.DownloadFile(uri);
        HttpResponseMessage newMessage = new(HttpStatusCode.OK);
        newMessage.Headers.ETag = (await content.GetResponseHeaders()).ETag;
        await download.AddItem(uri, newMessage);
        content = await download.DownloadFile(uri);
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task CacheControlMaxAge()
    {
        var uri = "https://httpbin.org/cache/20";
        await download.DownloadFile(uri);
        var content = await download.DownloadFile(uri);
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task WithContent()
    {
        var content = await download.DownloadFile("https://httpbin.org/json");
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task String()
    {
        var content = await download.String("https://httpbin.org/json");
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task Bytes()
    {
        var content = await download.Bytes("https://httpbin.org/json");
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task Stream()
    {
        var content = await download.Stream("https://httpbin.org/json");
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task ToFile()
    {
        var tempFileName = $"{Path.GetTempFileName()}.txt";
        try
        {
            await download.ToFile("https://httpbin.org/json", tempFileName);
            await Verifier.VerifyFile(tempFileName);
        }
        finally
        {
            File.Delete(tempFileName);
        }
    }

    [Fact]
    public async Task ToStream()
    {
        MemoryStream stream = new();
        await download.ToStream("https://httpbin.org/json", stream);
        await Verifier.Verify(stream);
    }

    [Fact]
    public Task NotFound()
    {
        return Verifier.ThrowsTask(() => download.String("https://httpbin.org/status/404"));
    }

    [Fact]
    public async Task Timeout()
    {
        HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromMilliseconds(1)
        };
        download = new(CachePath, httpClient);
        download.Purge();
        var exception = await Assert.ThrowsAsync<TaskCanceledException>(() => download.String("https://httpbin.org/status/200"));
        await Verifier.Verify(exception.Message);
    }

    [Fact]
    public async Task TimeoutUseStale()
    {
        HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromMilliseconds(1)
        };
        download = new(CachePath, httpClient);
        download.Purge();
        var uri = "https://httpbin.org/status/200";
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent("foo")
        };
        await download.AddItem(uri, response);
        await Verifier.Verify(download.DownloadFile(uri, true));
    }

    [Fact]
    public Task ServerError()
    {
        return Verifier.ThrowsTask(() => download.String("https://httpbin.org/status/500"));
    }

    [Fact]
    public async Task ServerErrorDontUseStale()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent("foo")
        };
        var uri = "https://httpbin.org/status/500";
        await download.AddItem(uri, response);
        await Verifier.ThrowsTask(() => download.String(uri));
    }

    [Fact]
    public async Task ServerErrorUseStale()
    {
        using HttpResponseMessage httpResponseMessage = new(HttpStatusCode.OK)
        {
            Content = new StringContent("foo")
        };
        var uri = "https://httpbin.org/status/500";
        await download.AddItem(uri, httpResponseMessage);
        await Verifier.Verify(download.DownloadFile(uri, true));
    }
}