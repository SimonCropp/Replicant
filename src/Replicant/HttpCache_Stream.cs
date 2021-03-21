using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Replicant
{
    public partial class HttpCache
    {
        /// <summary>
        /// Download a resource an return the result as <see cref="System.IO.Stream"/>.
        /// </summary>
        public Task<Stream> StreamAsync(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            return StreamAsync(BuildUri(uri), staleIfError, modifyRequest, token);
        }

        /// <summary>
        /// Download a resource an return the result as <see cref="System.IO.Stream"/>.
        /// </summary>
        public async Task<Stream> StreamAsync(
            Uri uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
            return await result.AsStreamAsync(token);
        }

        /// <summary>
        /// Download a resource an return the result as <see cref="System.IO.Stream"/>.
        /// </summary>
        public Stream Stream(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            return Stream(BuildUri(uri), staleIfError, modifyRequest, token);
        }

        /// <summary>
        /// Download a resource an return the result as <see cref="System.IO.Stream"/>.
        /// </summary>
        public Stream Stream(
            Uri uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            var result = Download(uri, staleIfError, modifyRequest, token);
            return result.AsStream(token);
        }

        /// <summary>
        /// Download a resource and store the result in <paramref name="stream"/>.
        /// </summary>
        public Task ToStreamAsync(
            string uri,
            Stream stream,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            return ToStreamAsync(BuildUri(uri), stream, staleIfError, modifyRequest, token);
        }

        /// <summary>
        /// Download a resource and store the result in <paramref name="stream"/>.
        /// </summary>
        public async Task ToStreamAsync(
            Uri uri,
            Stream stream,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            using var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
            await result.ToStreamAsync(stream, token);
        }

        /// <summary>
        /// Download a resource and store the result in <paramref name="stream"/>.
        /// </summary>
        public void ToStream(
            string uri,
            Stream stream,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            ToStream(BuildUri(uri), stream, staleIfError, modifyRequest, token);
        }

        /// <summary>
        /// Download a resource and store the result in <paramref name="stream"/>.
        /// </summary>
        public void ToStream(
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
}