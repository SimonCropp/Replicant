using System.IO;
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
    public async Task CacheControlMaxAge()
    {
        await download.DownloadFile("https://httpbin.org/cache/100");
        var content = await download.DownloadFile("https://httpbin.org/cache/100");
        await Verifier.Verify(File.ReadAllTextAsync(content.path))
            .ScrubLinesContaining("X-Amzn-Trace-Id");
        Assert.Equal(CacheStatus.Hit, content.status);
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
        var stream = new MemoryStream();
        await download.ToStream("https://httpbin.org/json", stream);
        await Verifier.Verify(stream);
    }

    [Fact]
    public Task NotFound()
    {
        return Verifier.ThrowsTask(() => download.String("https://httpbin.org/status/404"));
    }
}