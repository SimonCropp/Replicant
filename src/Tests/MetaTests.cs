[NotInParallel]
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
    public async Task ReadMetaV1_1_StatusCodeAbsent()
    {
        var meta = MetaData.ReadMeta("v1.1Meta.json");
        await Assert.That(meta.StatusCode).IsNull();
        await Verify(meta);
    }

    [Test]
    public async Task ReadMetaV2()
    {
        var meta = MetaData.ReadMeta("v2Meta.json");
        await Assert.That(meta.StatusCode).IsEqualTo(404);
        await Verify(meta);
    }
}