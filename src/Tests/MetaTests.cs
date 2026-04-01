[TestFixture]
public class MetaTests
{
    [Test]
    public Task ReadMetaV1()
    {
        var meta = MetaData.ReadMeta("v1Meta.json");
        return Verify(meta);
    }

    [Test]
    public Task ReadMetaV1_1()
    {
        var meta = MetaData.ReadMeta("v1.1Meta.json");
        return Verify(meta);
    }

    [Test]
    public Task ReadMetaV1_1_StatusCodeAbsent()
    {
        var meta = MetaData.ReadMeta("v1.1Meta.json");
        Assert.That(meta.StatusCode, Is.Null);
        return Verify(meta);
    }

    [Test]
    public Task ReadMetaV2()
    {
        var meta = MetaData.ReadMeta("v2Meta.json");
        Assert.That(meta.StatusCode, Is.EqualTo(404));
        return Verify(meta);
    }
}