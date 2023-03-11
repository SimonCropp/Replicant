namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual void AddItem(string uri, HttpResponseMessage response, Cancellation cancellation = default) =>
        AddItem(response, new(uri), cancellation);

    /// <inheritdoc/>
    public virtual void AddItem(Uri uri, HttpResponseMessage response, Cancellation cancellation = default) =>
        AddItem(response, uri, cancellation);

    /// <inheritdoc/>
    public virtual Task AddItemAsync(string uri, HttpResponseMessage response, Cancellation cancellation = default) =>
        AddItemAsync(response, new(uri), cancellation);

    /// <inheritdoc/>
    public virtual Task AddItemAsync(Uri uri, HttpResponseMessage response, Cancellation cancellation = default) =>
        AddItemAsync(response, uri, cancellation);

    /// <inheritdoc/>
    public virtual Task AddItemAsync(
        string uri,
        Stream stream,
        DateTimeOffset? expiry = null,
        DateTimeOffset? modified = null,
        string? etag = null,
        Headers? responseHeaders = null,
        Headers? contentHeaders = null,
        Headers? trailingHeaders = null,
        Cancellation cancellation = default) =>
        AddItemAsync(new Uri(uri), stream, expiry, modified, etag, responseHeaders, contentHeaders, trailingHeaders, cancellation);

    /// <inheritdoc/>
    public virtual async Task AddItemAsync(
        string uri,
        string content,
        DateTimeOffset? expiry = null,
        DateTimeOffset? modified = null,
        string? etag = null,
        Headers? responseHeaders = null,
        Headers? contentHeaders = null,
        Headers? trailingHeaders = null,
        Cancellation cancellation = default)
    {
        using var stream = content.AsStream();
        await AddItemAsync(uri, stream, expiry, modified, etag, responseHeaders, contentHeaders, trailingHeaders, cancellation);
    }
}