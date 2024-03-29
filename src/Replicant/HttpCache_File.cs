﻿namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual Task ToFileAsync(
        string uri,
        string path,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default) =>
        ToFileAsync(new Uri(uri), path, staleIfError, modifyRequest, cancel);

    /// <inheritdoc/>
    public virtual async Task ToFileAsync(
        Uri uri,
        string path,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default)
    {
        using var result = await DownloadAsync(uri, staleIfError, modifyRequest, cancel);
        await result.ToFileAsync(path, cancel);
    }

    /// <inheritdoc/>
    public virtual void ToFile(
        string uri,
        string path,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default) =>
        ToFile(new Uri(uri), path, staleIfError, modifyRequest, cancel);

    /// <inheritdoc/>
    public virtual void ToFile(
        Uri uri,
        string path,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancel cancel = default)
    {
        using var result = Download(uri, staleIfError, modifyRequest, cancel);
        result.ToFile(path, cancel);
    }
}