using System.Collections.Generic;
using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

[UsesVerify]
public class TimestampTests
{
    [Fact]
    public Task FromPath()
    {
        var timestamps = new List<Timestamp>();
        foreach (var file in new[]
        {
            "C:\\Dir\\92cb8761e3e5246f873da524f438f95468eb3d36_2021-03-22T095023_S{etag}.bin",
            "C:\\Dir\\92cb8761e3e5246f873da524f438f95468eb3d36_2021-03-22T095023_Setag.bin",
            "C:\\Dir\\92cb8761e3e5246f873da524f438f95468eb3d36_2021-03-22T095023_W{etag}.bin",
            "C:\\Dir\\92cb8761e3e5246f873da524f438f95468eb3d36_2021-03-22T095023_Wetag.bin",
            "C:\\Dir\\92cb8761e3e5246f873da524f438f95468eb3d36_2021-03-22T095023_.bin",
        })
        {
            timestamps.Add(Timestamp.FromPath(file));
        }

        return Verifier.Verify(timestamps);
    }
}