using System.Threading.Tasks;
using Replicant;
using VerifyXunit;
using Xunit;

[UsesVerify]
public class DownloadTests
{
    [Fact]
    public async Task EmptyContent()
    {
        var content = await Download.DownloadContent("https://httpbin.org/status/200");
        await Verifier.Verify(new {content.success, content.content});
    }

    [Fact]
    public async Task NotFound()
    {
        var content = await Download.DownloadContent("https://httpbin.org/status/404");
        Assert.False(content.success);
        Assert.Null(content.content);
    }
}