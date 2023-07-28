namespace Replicant;

public partial class HttpCache
{
    public IAsyncEnumerable<string> LinesAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default) =>
        LinesAsync(new Uri(uri), staleIfError, modifyRequest, cancel);

    public async IAsyncEnumerable<string> LinesAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        [EnumeratorCancellation] Cancel cancel = default)
    {
        using var result = await DownloadAsync(uri, staleIfError, modifyRequest, cancel);
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
        Cancel cancel = default) =>
        Lines(new Uri(uri), staleIfError, modifyRequest, cancel);

    public IEnumerable<string> Lines(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default)
    {
        using var result = Download(uri, staleIfError, modifyRequest, cancel);
        using var stream = result.AsStream(cancel);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}