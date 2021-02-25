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
    }
    [Fact]
    public async Task EmptyContent()
    {
        var content = await download.String("https://httpbin.org/status/200");
        await Verifier.Verify(new {content.success, content.content});
    }

    [Fact]
    public async Task WithContent()
    {
        var content = await download.String("https://httpbin.org/json");
        await Verifier.Verify(new {content.success, content.content});
    }

    [Fact]
    public async Task NotFound()
    {
        var content = await download.String("https://httpbin.org/status/404");
        Assert.False(content.success);
        Assert.Null(content.content);
    }
}