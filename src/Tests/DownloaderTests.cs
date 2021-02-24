using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

[UsesVerify]
public class DownloaderTests
{
    [Fact]
    public async Task Valid()
    {
        var content = await Downloader.DownloadContent("https://raw.githubusercontent.com/SimonCropp/MarkdownSnippets/master/.gitattributes");
        await Verifier.Verify(new {content.success, content.content});
    }

    [Fact]
    public async Task Missing()
    {
        var content = await Downloader.DownloadContent("https://raw.githubusercontent.com/SimonCropp/MarkdownSnippets/master/missing.txt");
        Assert.False(content.success);
        Assert.Null(content.content);
    }
}