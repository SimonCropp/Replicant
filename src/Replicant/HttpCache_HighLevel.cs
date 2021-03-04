using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Replicant
{
    public partial class HttpCache
    {
        public async Task<string> StringAsync(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            using var result = await DownloadAsync(uri, staleIfError, messageCallback, token);
            return await result.AsStringAsync(token);
        }

        public async Task<byte[]> BytesAsync(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            using var result = await DownloadAsync(uri, staleIfError, messageCallback, token);
            return await result.AsBytesAsync(token);
        }

        public async Task<Stream> StreamAsync(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            var result = await DownloadAsync(uri, staleIfError, messageCallback, token);
            return await result.AsStreamAsync(token);
        }

        public async Task<HttpResponseMessage> ResponseAsync(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            var result = await DownloadAsync(uri, staleIfError, messageCallback, token);
            return result.AsResponseMessage();
        }

        public async Task ToStreamAsync(
            string uri,
            Stream stream,
            bool staleIfError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            using var result = await DownloadAsync(uri, staleIfError, messageCallback, token);
            await result.ToStreamAsync(stream, token);
        }

        public async Task ToFileAsync(
            string uri,
            string path,
            bool staleIfError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            using var result = await DownloadAsync(uri, staleIfError, messageCallback, token);
            await result.ToFileAsync(path, token);
        }

        public string String(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            using var result = Download(uri, staleIfError, messageCallback, token);
            return result.AsString();
        }

        public byte[] Bytes(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            using var result = Download(uri, staleIfError, messageCallback, token);
            return result.AsBytes();
        }

        public Stream Stream(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            var result = Download(uri, staleIfError, messageCallback, token);
            return result.AsStream();
        }

        public async Task<HttpResponseMessage> Response(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            var result = await DownloadAsync(uri, staleIfError, messageCallback, token);
            return result.AsResponseMessage();
        }

        public void ToStream(
            string uri,
            Stream stream,
            bool staleIfError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            using var result = Download(uri, staleIfError, messageCallback, token);
            result.ToStream(stream, token);
        }

        public void ToFile(
            string uri,
            string path,
            bool staleIfError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            using var result = Download(uri, staleIfError, messageCallback, token);
            result.ToFile(path, token);
        }
    }
}