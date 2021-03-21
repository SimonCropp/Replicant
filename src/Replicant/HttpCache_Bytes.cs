using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Replicant
{
    public partial class HttpCache
    {
        /// <summary>
        /// Download a resource an return the result as byte array.
        /// </summary>
        public async Task<byte[]> BytesAsync(
            Uri uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            using var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
            return await result.AsBytesAsync(token);
        }

        /// <summary>
        /// Download a resource an return the result as byte array.
        /// </summary>
        public Task<byte[]> BytesAsync(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            return BytesAsync(BuildUri(uri), staleIfError, modifyRequest, token);
        }

        /// <summary>
        /// Download a resource an return the result as a byte array.
        /// </summary>
        public byte[] Bytes(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            return Bytes(BuildUri(uri), staleIfError, modifyRequest, token);
        }

        /// <summary>
        /// Download a resource an return the result as a byte array.
        /// </summary>
        public byte[] Bytes(
            Uri uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            using var result = Download(uri, staleIfError, modifyRequest, token);
            return result.AsBytes();
        }
    }
}