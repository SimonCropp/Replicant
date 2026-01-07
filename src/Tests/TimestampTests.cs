[TestFixture]
public class TimestampTests
{
    [Test]
    public Task FromPath()
    {
        var timestamps = new List<Timestamp>();
        foreach (var file in new[]
        {
            "/Dir/92cb8761e3e5246f873da524f438f95468eb3d36_2021-03-22T095023_S{etag}.bin",
            "/Dir/92cb8761e3e5246f873da524f438f95468eb3d36_2021-03-22T095023_Setag.bin",
            "/Dir/92cb8761e3e5246f873da524f438f95468eb3d36_2021-03-22T095023_W{etag}.bin",
            "/Dir/92cb8761e3e5246f873da524f438f95468eb3d36_2021-03-22T095023_Wetag.bin",
            "/Dir/92cb8761e3e5246f873da524f438f95468eb3d36_2021-03-22T095023_.bin",
        })
        {
            timestamps.Add(Timestamp.FromPath(file));
        }

        return Verify(timestamps);
    }

    [Test]
    public void FromPath_EtagParsedCorrectly()
    {
        // Verify etag is parsed correctly without off-by-one error
        // Format: {hash}_{date}_{etag}.bin
        // - hash: 40 chars (indexOf = 40 for first underscore)
        // - date: 17 chars (yyyy-MM-ddTHHmmss)
        // - etag: starts at indexOf + 19 (after hash + underscore + date + underscore)
        var path = "/Dir/92cb8761e3e5246f873da524f438f95468eb3d36_2021-03-22T095023_SmyEtagValue.bin";

        var timestamp = Timestamp.FromPath(path);

        // The etag ForFile should be exactly "SmyEtagValue" (S prefix + value)
        // If there's an off-by-one error, it would be "myEtagValue" (missing S)
        AreEqual("SmyEtagValue", timestamp.Etag.ForFile);
    }

    [Test]
    public void FromPath_MalformedFilename_NoUnderscore_Throws()
    {
        var path = "/Dir/malformedfilewithoutunderscore.bin";

        var ex = Assert.Throws<ArgumentException>(() => Timestamp.FromPath(path));
        StringAssert.Contains("Invalid cache filename format", ex!.Message);
    }

    [Test]
    public void FromPath_MalformedFilename_TooShort_Throws()
    {
        var path = "/Dir/hash_short.bin";

        var ex = Assert.Throws<ArgumentException>(() => Timestamp.FromPath(path));
        StringAssert.Contains("Invalid cache filename format", ex!.Message);
    }

    [Test]
    public void FromPath_MalformedFilename_InvalidDate_Throws()
    {
        // Valid structure but invalid date format
        var path = "/Dir/hash_notavaliddate1234_Setag.bin";

        var ex = Assert.Throws<ArgumentException>(() => Timestamp.FromPath(path));
        StringAssert.Contains("Invalid date format", ex!.Message);
    }
}