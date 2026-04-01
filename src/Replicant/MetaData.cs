class MetaData(
    string? uri,
    List<KeyValuePair<string, List<string>>> responseHeaders,
    List<KeyValuePair<string, List<string>>> contentHeaders,
    List<KeyValuePair<string, List<string>>>? trailingHeaders = null,
    int? statusCode = null)
{
    public string? Uri { get; } = uri;
    public List<KeyValuePair<string, List<string>>> ResponseHeaders { get; } = responseHeaders;
    public List<KeyValuePair<string, List<string>>> ContentHeaders { get; } = contentHeaders;
    public List<KeyValuePair<string, List<string>>>? TrailingHeaders { get; } = trailingHeaders;
    public int? StatusCode { get; } = statusCode;

    public static MetaData FromEnumerables(
        string uri,
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> responseHeaders,
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> contentHeaders,
        IEnumerable<KeyValuePair<string, IEnumerable<string>>>? trailingHeaders = null,
        HttpStatusCode? statusCode = null) =>
        new(uri, ToList(responseHeaders)!, ToList(contentHeaders)!, ToList(trailingHeaders),
            statusCode.HasValue ? (int) statusCode.Value : null);

    static List<KeyValuePair<string, List<string>>>? ToList(IEnumerable<KeyValuePair<string, IEnumerable<string>>>? input) =>
        input?
            .Select(_ => new KeyValuePair<string, List<string>>(_.Key, _.Value.ToList()))
            .ToList();

    public static MetaData ReadMeta(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<MetaData>(stream)!;
    }

    public static async ValueTask<MetaData> ReadMetaAsync(string path)
    {
        using var stream = File.OpenRead(path);
        var data = await JsonSerializer.DeserializeAsync<MetaData>(stream);
        return data!;
    }

    public static void ApplyToResponse(string path, HttpResponseMessage response)
    {
        var meta = ReadMeta(path);

        ApplyHeaders(response, meta);
    }

    public static async Task ApplyToResponseAsync(string path, HttpResponseMessage response)
    {
        var meta = await ReadMetaAsync(path);

        ApplyHeaders(response, meta);
    }

    static void ApplyHeaders(HttpResponseMessage response, MetaData meta)
    {
        if (meta.StatusCode.HasValue)
        {
            response.StatusCode = (HttpStatusCode) meta.StatusCode.Value;
        }

        response.Headers.AddRange(meta.ResponseHeaders);
        response.Content.Headers.AddRange(meta.ContentHeaders);
#if NET8_0_OR_GREATER
        if (meta.TrailingHeaders != null)
        {
            response.TrailingHeaders.AddRange(meta.TrailingHeaders);
        }
#endif
    }
}
