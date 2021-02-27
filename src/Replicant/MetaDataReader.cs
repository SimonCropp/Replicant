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
    public  static async Task WriteMetaData(HttpResponseMessage response, string metaFile)
    {
        //TODO: should write these to temp files then copy them. then we can  pass token
        await using var metaFileStream = FileEx.OpenWrite(metaFile);
        var meta = new MetaData(response.Headers, response.Content.Headers);
        // ReSharper disable once MethodSupportsCancellation
        await JsonSerializer.SerializeAsync(metaFileStream, meta);
    }
}