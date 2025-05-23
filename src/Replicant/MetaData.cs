﻿class MetaData(
    string? uri,
    List<KeyValuePair<string, List<string>>> responseHeaders,
    List<KeyValuePair<string, List<string>>> contentHeaders,
    List<KeyValuePair<string, List<string>>>? trailingHeaders = null)
{
    public string? Uri { get; } = uri;
    public List<KeyValuePair<string, List<string>>> ResponseHeaders { get; } = responseHeaders;
    public List<KeyValuePair<string, List<string>>> ContentHeaders { get; } = contentHeaders;
    public List<KeyValuePair<string, List<string>>>? TrailingHeaders { get; } = trailingHeaders;

    public static MetaData FromEnumerables(
        string uri,
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> responseHeaders,
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> contentHeaders,
        IEnumerable<KeyValuePair<string, IEnumerable<string>>>? trailingHeaders = null) =>
        new(uri, ToList(responseHeaders)!, ToList(contentHeaders)!, ToList(trailingHeaders));

    static List<KeyValuePair<string, List<string>>>? ToList(IEnumerable<KeyValuePair<string, IEnumerable<string>>>? input)
    {
        if (input == null)
        {
            return null;
        }
        List<KeyValuePair<string, List<string>>> result = [];
        foreach (var item in input)
        {
            result.Add(new(item.Key, item.Value.ToList()));
        }

        return result;
    }

    public static MetaData ReadMeta(string path) =>
        JsonSerializer.Deserialize<MetaData>(File.ReadAllText(path))!;

    public static void ApplyToResponse(string path, HttpResponseMessage response)
    {
        var meta = ReadMeta(path);

        response.Headers.AddRange(meta.ResponseHeaders);
        response.Content.Headers.AddRange(meta.ContentHeaders);
#if NET7_0_OR_GREATER
        if (meta.TrailingHeaders != null)
        {
            response.TrailingHeaders.AddRange(meta.TrailingHeaders);
        }
#endif
    }
}