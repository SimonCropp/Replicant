namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual Task<Stream> StreamAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default) =>
        StreamAsync(new Uri(uri), staleIfError, modifyRequest, cancel);

    /// <inheritdoc/>
    public virtual async Task<Stream> StreamAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default)
    {
        var result = await DownloadAsync(uri, staleIfError, modifyRequest, cancel);
        return await result.AsStreamAsync(cancel);
    }

    /// <inheritdoc/>
    public virtual Stream Stream(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default) =>
        Stream(new Uri(uri), staleIfError, modifyRequest, cancel);

    /// <inheritdoc/>
    public virtual Stream Stream(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default)
    {
        var result = Download(uri, staleIfError, modifyRequest, cancel);
        return result.AsStream(cancel);
    }

    /// <inheritdoc/>
    public virtual Task ToStreamAsync(
        string uri,
        Stream stream,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default) =>
        ToStreamAsync(new Uri(uri), stream, staleIfError, modifyRequest, cancel);

    /// <inheritdoc/>
    public virtual async Task ToStreamAsync(
        Uri uri,
        Stream stream,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default)
    {
        using var result = await DownloadAsync(uri, staleIfError, modifyRequest, cancel);
        await result.ToStreamAsync(stream, cancel);
    }

    /// <inheritdoc/>
    public virtual void ToStream(
        string uri,
        Stream stream,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default) =>
        ToStream(new Uri(uri), stream, staleIfError, modifyRequest, cancel);

    /// <inheritdoc/>
    public virtual void ToStream(
        Uri uri,
        Stream stream,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default)
    {
        using var result = Download(uri, staleIfError, modifyRequest, cancel);
        result.ToStream(stream, cancel);
    }
}