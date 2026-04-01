namespace Replicant;

public partial class HttpCache
{
    public IAsyncEnumerable<string> LinesAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        bool cache404 = false,
        Cancel cancel = default) =>
        LinesAsync(new Uri(uri), staleIfError, modifyRequest, cache404, cancel);

    public async IAsyncEnumerable<string> LinesAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        bool cache404 = false,
        [EnumeratorCancellation] Cancel cancel = default)
    {
        using var result = await DownloadAsync(uri, staleIfError, modifyRequest, cache404, cancel);
        using var stream = result.AsStream(cancel);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancel) is { } line)
        {
            yield return line;
        }
    }

    public IEnumerable<string> Lines(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        bool cache404 = false,
        Cancel cancel = default) =>
        Lines(new Uri(uri), staleIfError, modifyRequest, cache404, cancel);

    public IEnumerable<string> Lines(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        bool cache404 = false,
        Cancel cancel = default)
    {
        using var result = Download(uri, staleIfError, modifyRequest, cache404, cancel);
        using var stream = result.AsStream(cancel);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}