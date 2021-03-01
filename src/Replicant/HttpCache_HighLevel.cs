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
            bool useStaleOnError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            var result = await Download(uri, useStaleOnError, messageCallback, token);
            return await result.AsText(token);
        }

        public async Task<byte[]> Bytes(
            string uri,
            bool useStaleOnError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            var result = await Download(uri, useStaleOnError, messageCallback, token);
            return await result.AsBytes(token);
        }

        public async Task<Stream> Stream(
            string uri,
            bool useStaleOnError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            var result = await Download(uri, useStaleOnError, messageCallback, token);
            return result.AsStream();
        }

        public async Task<HttpResponseMessage> Response(
            string uri,
            bool useStaleOnError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            var result = await Download(uri, useStaleOnError, messageCallback, token);
            return result.AsResponseMessage();
        }

        public async Task ToStream(
            string uri,
            Stream stream,
            bool useStaleOnError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            var result = await Download(uri, useStaleOnError, messageCallback, token);
            await using var source = result.AsStream();
            await source.CopyToAsync(stream, token);
        }

        public async Task ToFile(
            string uri,
            string path,
            bool useStaleOnError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            var result = await Download(uri, useStaleOnError, messageCallback, token);
            await result.ToFile(path, token);
        }
    }
}