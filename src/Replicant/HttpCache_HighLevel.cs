using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Replicant
{
    public partial class HttpCache
    {
        public async Task<string> String(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            using var result = await Download(uri, staleIfError, messageCallback, token);
            return await result.AsText(token);
        }

        public async Task<byte[]> Bytes(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            using var result = await Download(uri, staleIfError, messageCallback, token);
            return await result.AsBytes(token);
        }

        public async Task<Stream> Stream(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            var result = await Download(uri, staleIfError, messageCallback, token);
            return await result.AsStream(token);
        }

        public async Task<HttpResponseMessage> Response(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            var result = await Download(uri, staleIfError, messageCallback, token);
            return result.AsResponseMessage();
        }

        public async Task ToStream(
            string uri,
            Stream stream,
            bool staleIfError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            using var result = await Download(uri, staleIfError, messageCallback, token);
            await result.ToStream(stream, token);
        }

        public async Task ToFile(
            string uri,
            string path,
            bool staleIfError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            using var result = await Download(uri, staleIfError, messageCallback, token);
            await result.ToFile(path, token);
        }
    }
}