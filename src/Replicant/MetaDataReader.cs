using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

static class MetaDataReader
{
    public static async Task<MetaData> ReadMeta(string path)
    {
        await using var stream = FileEx.OpenRead(path);
        return (await JsonSerializer.DeserializeAsync<MetaData>(stream))!;
    }

    public static async Task WriteMetaData(HttpResponseMessage response, string metaFile)
    {
        //TODO: should write these to temp files then copy them. then we can  pass token
        await using var metaFileStream = FileEx.OpenWrite(metaFile);
        MetaData meta = new(response.Headers, response.Content.Headers, response.TrailingHeaders);
        // ReSharper disable once MethodSupportsCancellation
        await JsonSerializer.SerializeAsync(metaFileStream, meta);
    }
}