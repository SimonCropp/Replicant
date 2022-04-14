namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual Task ToFileAsync(
        string uri,
        string path,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default) =>
        ToFileAsync(new Uri(uri), path, staleIfError, modifyRequest, token);

    /// <inheritdoc/>
    public virtual async Task ToFileAsync(
        Uri uri,
        string path,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default)
    {
        using var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
        await result.ToFileAsync(path, token);
    }

    /// <inheritdoc/>
    public virtual void ToFile(
        string uri,
        string path,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default) =>
        ToFile(new Uri(uri), path, staleIfError, modifyRequest, token);

    /// <inheritdoc/>
    public virtual void ToFile(
        Uri uri,
        string path,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default)
    {
        using var result = Download(uri, staleIfError, modifyRequest, token);
        result.ToFile(path, token);
    }
}