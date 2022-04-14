namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual Task<Stream> StreamAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default) =>
        StreamAsync(new Uri(uri), staleIfError, modifyRequest, token);

    /// <inheritdoc/>
    public virtual async Task<Stream> StreamAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default)
    {
        var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
        return await result.AsStreamAsync(token);
    }

    /// <inheritdoc/>
    public virtual Stream Stream(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default) =>
        Stream(new Uri(uri), staleIfError, modifyRequest, token);

    /// <inheritdoc/>
    public virtual Stream Stream(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default)
    {
        var result = Download(uri, staleIfError, modifyRequest, token);
        return result.AsStream(token);
    }

    /// <inheritdoc/>
    public virtual Task ToStreamAsync(
        string uri,
        Stream stream,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default) =>
        ToStreamAsync(new Uri(uri), stream, staleIfError, modifyRequest, token);

    /// <inheritdoc/>
    public virtual async Task ToStreamAsync(
        Uri uri,
        Stream stream,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default)
    {
        using var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
        await result.ToStreamAsync(stream, token);
    }

    /// <inheritdoc/>
    public virtual void ToStream(
        string uri,
        Stream stream,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default) =>
        ToStream(new Uri(uri), stream, staleIfError, modifyRequest, token);

    /// <inheritdoc/>
    public virtual void ToStream(
        Uri uri,
        Stream stream,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default)
    {
        using var result = Download(uri, staleIfError, modifyRequest, token);
        result.ToStream(stream, token);
    }
}