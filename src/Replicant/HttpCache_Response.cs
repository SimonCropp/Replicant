using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Replicant
{
    public partial class HttpCache
    {
        /// <summary>
        /// Download a resource an return the result as <see cref="HttpResponseMessage"/>.
        /// </summary>
        public Task<HttpResponseMessage> ResponseAsync(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            return ResponseAsync(BuildUri(uri), staleIfError, modifyRequest, token);
        }

        /// <summary>
        /// Download a resource an return the result as <see cref="HttpResponseMessage"/>.
        /// </summary>
        public async Task<HttpResponseMessage> ResponseAsync(
            Uri uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
            return result.AsResponseMessage();
        }

        /// <summary>
        /// Download a resource an return the result as <see cref="HttpResponseMessage"/>.
        /// </summary>
        public Task<HttpResponseMessage> Response(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            return Response(BuildUri(uri), staleIfError, modifyRequest, token);
        }

        /// <summary>
        /// Download a resource an return the result as <see cref="HttpResponseMessage"/>.
        /// </summary>
        public async Task<HttpResponseMessage> Response(
            Uri uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
            return result.AsResponseMessage();
        }
    }
}