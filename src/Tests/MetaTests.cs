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
}