using System.Diagnostics;
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

    public DownloadTests()
    {
        download = new Download(Path.Combine(Path.GetTempPath(), "DownloadTests"));
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
        var newMessage = new HttpResponseMessage(HttpStatusCode.OK);
        newMessage.Headers.ETag = content.ResponseHeaders.ETag;
        await download.AddItem(uri, newMessage);
        content = await download.DownloadFile(uri);
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task CacheControlMaxAge()
    {
        var uri = "https://httpbin.org/cache/20";
        await download.DownloadFile(uri);
        var stopwatch = Stopwatch.StartNew();
        var content = await download.DownloadFile(uri);
        stopwatch.Stop();
        var stopwatchElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
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
    public Task ServerError()
    {
        return Verifier.ThrowsTask(() => download.String("https://httpbin.org/status/500"));
    }

    [Fact]
    public async Task ServerErrorDontUseStale()
    {
        using HttpResponseMessage httpResponseMessage = new(HttpStatusCode.OK)
        {
            Content = new StringContent("foo")
        };
        var uri = "https://httpbin.org/status/500";
        await download.AddItem(uri, httpResponseMessage);
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