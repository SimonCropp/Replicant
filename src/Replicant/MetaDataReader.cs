using System.IO;
using System.Text.Json;

static class MetaDataReader
{
    public static MetaData ReadMeta(string path)
    {
        return JsonSerializer.Deserialize<MetaData>(File.ReadAllText(path))!;
    }
}