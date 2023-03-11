namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual Task<string> StringAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default) =>
        StringAsync(new Uri(uri), staleIfError, modifyRequest, token);

    /// <inheritdoc/>
    public virtual async Task<string> StringAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default)
    {
        using var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
        return await result.AsStringAsync(token);
    }

    /// <inheritdoc/>
    public virtual string String(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default) =>
        String(new Uri(uri), staleIfError, modifyRequest, token);

    /// <inheritdoc/>
    public virtual string String(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default)
    {
        using var result = Download(uri, staleIfError, modifyRequest, token);
        return result.AsString();
    }
}