using System.Text.Json;
using System.Threading.Tasks;

static class MetaDataReader
{
    public static async Task<MetaData> ReadMeta(string path)
    {
        await using var stream = FileEx.OpenRead(path);
        return (await JsonSerializer.DeserializeAsync<MetaData>(stream))!;
    }
}