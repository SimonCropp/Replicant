[TestFixture]
public class EtagTests
{
    [TestCase("tag")]
    [TestCase("\"tag\"")]
    [TestCase("W/\"tag\"")]
    public Task RoundTrip(string etag)
    {
        var fromHeader = Etag.FromHeader(etag);
        var fromFilePath = Etag.FromFilePart(fromHeader.ForFile);
        var parameter = etag.Replace('"', '_').Replace('/', '_');
        return Verify(new
            {
                fromHeader,
                fromFilePath
            })
            .UseParameters(parameter);
    }
}