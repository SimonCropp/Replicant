using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Replicant
{
    public partial class HttpCache
    {
        /// <summary>
        /// Download a resource and store the result in <paramref name="path"/>.
        /// </summary>
        public Task ToFileAsync(
            string uri,
            string path,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            return ToFileAsync(BuildUri(uri), path, staleIfError, modifyRequest, token);
        }

        /// <summary>
        /// Download a resource and store the result in <paramref name="path"/>.
        /// </summary>
        public async Task ToFileAsync(
            Uri uri,
            string path,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            using var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
            await result.ToFileAsync(path, token);
        }

        /// <summary>
        /// Download a resource and store the result in <paramref name="path"/>.
        /// </summary>
        public void ToFile(
            string uri,
            string path,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            ToFile(BuildUri(uri), path, staleIfError, modifyRequest, token);
        }

        /// <summary>
        /// Download a resource and store the result in <paramref name="path"/>.
        /// </summary>
        public void ToFile(
            Uri uri,
            string path,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            using var result = Download(uri, staleIfError, modifyRequest, token);
            result.ToFile(path, token);
        }
    }
}