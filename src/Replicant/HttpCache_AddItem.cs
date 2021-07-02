using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Replicant
{
    public partial class HttpCache
    {
        /// <inheritdoc/>
        public virtual void AddItem(string uri, HttpResponseMessage response, CancellationToken token = default)
        {
            Guard.AgainstNull(response.Content, nameof(response.Content));
            AddItem(response, new(uri), token);
        }

        /// <inheritdoc/>
        public virtual void AddItem(Uri uri, HttpResponseMessage response, CancellationToken token = default)
        {
            Guard.AgainstNull(response.Content, nameof(response.Content));
            AddItem(response, uri, token);
        }

        /// <inheritdoc/>
        public virtual Task AddItemAsync(string uri, HttpResponseMessage response, CancellationToken token = default)
        {
            Guard.AgainstNull(response.Content, nameof(response.Content));
            return AddItemAsync(response, new(uri), token);
        }

        /// <inheritdoc/>
        public virtual Task AddItemAsync(Uri uri, HttpResponseMessage response, CancellationToken token = default)
        {
            Guard.AgainstNull(response.Content, nameof(response.Content));
            return AddItemAsync(response, uri, token);
        }

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
            CancellationToken token = default)
        {
            return AddItemAsync(new Uri(uri), stream, expiry, modified, etag, responseHeaders, contentHeaders, trailingHeaders, token);
        }

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
            CancellationToken token = default)
        {
            using var stream = content.AsStream();
            await AddItemAsync(uri, stream, expiry, modified, etag, responseHeaders, contentHeaders, trailingHeaders, token);
        }
    }
}