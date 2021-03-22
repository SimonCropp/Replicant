using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Replicant
{
    public partial class HttpCache
    {
        /// <summary>
        /// Download a resource an return the result as a string.
        /// </summary>
        public Task<string> StringAsync(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            return StringAsync(new Uri(uri), staleIfError, modifyRequest, token);
        }

        /// <summary>
        /// Download a resource an return the result as a string.
        /// </summary>
        public async Task<string> StringAsync(
            Uri uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            using var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
            return await result.AsStringAsync(token);
        }

        /// <summary>
        /// Download a resource an return the result as a string.
        /// </summary>
        public string String(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            return String(new Uri(uri), staleIfError, modifyRequest, token);
        }

        /// <summary>
        /// Download a resource an return the result as a string.
        /// </summary>
        public string String(
            Uri uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            using var result = Download(uri, staleIfError, modifyRequest, token);
            return result.AsString();
        }
    }
}