namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual Task<string> StringAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        bool cache404 = false,
        Cancel cancel = default) =>
        StringAsync(new Uri(uri), staleIfError, modifyRequest, cache404, cancel);

    /// <inheritdoc/>
    public virtual async Task<string> StringAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        bool cache404 = false,
        Cancel cancel = default)
    {
        using var result = await DownloadAsync(uri, staleIfError, modifyRequest, cache404, cancel);
        return await result.AsStringAsync(cancel);
    }

    /// <inheritdoc/>
    public virtual string String(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        bool cache404 = false,
        Cancel cancel = default) =>
        String(new Uri(uri), staleIfError, modifyRequest, cache404, cancel);

    /// <inheritdoc/>
    public virtual string String(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        bool cache404 = false,
        Cancel cancel = default)
    {
        using var result = Download(uri, staleIfError, modifyRequest, cache404, cancel);
        return result.AsString();
    }
}