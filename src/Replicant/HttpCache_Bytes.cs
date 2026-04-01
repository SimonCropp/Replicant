namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual async Task<byte[]> BytesAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        bool cache404 = false,
        Cancel cancel = default)
    {
        using var result = await DownloadAsync(uri, staleIfError, modifyRequest, cache404, cancel);
        return await result.AsBytesAsync(cancel);
    }

    /// <inheritdoc/>
    public virtual Task<byte[]> BytesAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        bool cache404 = false,
        Cancel cancel = default) =>
        BytesAsync(new Uri(uri), staleIfError, modifyRequest, cache404, cancel);

    /// <inheritdoc/>
    public virtual byte[] Bytes(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        bool cache404 = false,
        Cancel cancel = default) =>
        Bytes(new Uri(uri), staleIfError, modifyRequest, cache404, cancel);

    /// <inheritdoc/>
    public virtual byte[] Bytes(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        bool cache404 = false,
        Cancel cancel = default)
    {
        using var result = Download(uri, staleIfError, modifyRequest, cache404, cancel);
        return result.AsBytes();
    }
}