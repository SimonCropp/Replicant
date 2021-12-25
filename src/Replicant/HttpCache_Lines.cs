namespace Replicant;

public partial class HttpCache
{
    public IAsyncEnumerable<string> LinesAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default)
    {
        return LinesAsync(new Uri(uri), staleIfError, modifyRequest, token);
    }

    public async IAsyncEnumerable<string> LinesAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        using var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
        using var stream = result.AsStream(token);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            yield return line;
        }
    }

    public IEnumerable<string> Lines(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default)
    {
        return Lines(new Uri(uri), staleIfError, modifyRequest, token);
    }

    public IEnumerable<string> Lines(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default)
    {
        using var result = Download(uri, staleIfError, modifyRequest, token);
        using var stream = result.AsStream(token);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            yield return line;
        }
    }
}