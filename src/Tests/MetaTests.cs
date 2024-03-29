public class MetaTests
{
    [Fact]
    public Task ReadMetaV1()
    {
        var meta = MetaData.ReadMeta("v1Meta.json");
        return Verify(meta);
    }

    [Fact]
    public Task ReadMetaV1_1()
    {
        var meta = MetaData.ReadMeta("v1.1Meta.json");
        return Verify(meta);
    }
}