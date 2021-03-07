using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;

class MetaData
{
    public IEnumerable<KeyValuePair<string, IEnumerable<string>>> ResponseHeaders { get; }
    public IEnumerable<KeyValuePair<string, IEnumerable<string>>> ContentHeaders { get; }
    public IEnumerable<KeyValuePair<string, IEnumerable<string>>>? TrailingHeaders { get; }

    public MetaData(
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> responseHeaders,
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> contentHeaders,
        IEnumerable<KeyValuePair<string, IEnumerable<string>>>? trailingHeaders = null
        )
    {
        ResponseHeaders = responseHeaders;
        ContentHeaders = contentHeaders;
        TrailingHeaders = trailingHeaders;
    }
    public static MetaData ReadMeta(string path)
    {
        return JsonSerializer.Deserialize<MetaData>(File.ReadAllText(path))!;
    }
    public static void ApplyToResponse(string path, HttpResponseMessage response)
    {
        var meta = ReadMeta(path);

        response.Headers.AddRange(meta.ResponseHeaders);
        response.Content.Headers.AddRange(meta.ContentHeaders);
#if NET5_0 || NETSTANDARD2_1
        if (meta.TrailingHeaders != null)
        {
            response.TrailingHeaders.AddRange(meta.TrailingHeaders);
        }
#endif
    }
}