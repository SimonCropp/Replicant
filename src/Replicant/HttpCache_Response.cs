namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual Task<HttpResponseMessage> ResponseAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default) =>
        ResponseAsync(new Uri(uri), staleIfError, modifyRequest, cancel);

    /// <inheritdoc/>
    public virtual async Task<HttpResponseMessage> ResponseAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default)
    {
        var result = await DownloadAsync(uri, staleIfError, modifyRequest, cancel);
        return await result.AsResponseMessageAsync();
    }

    /// <inheritdoc/>
    public virtual Task<HttpResponseMessage> Response(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default) =>
        Response(new Uri(uri), staleIfError, modifyRequest, cancel);

    /// <inheritdoc/>
    public virtual async Task<HttpResponseMessage> Response(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default)
    {
        var result = await DownloadAsync(uri, staleIfError, modifyRequest, cancel);
        return await result.AsResponseMessageAsync();
    }
}