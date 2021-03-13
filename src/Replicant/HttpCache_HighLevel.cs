using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Replicant
{
    public partial class HttpCache
    {

        public void AddItem(string uri, HttpResponseMessage response, CancellationToken token = default)
        {
            Guard.AgainstNull(response.Content, nameof(response.Content));
            AddItem(response, uri, token);
        }

        public Task AddItemAsync(string uri, HttpResponseMessage response, CancellationToken token = default)
        {
            Guard.AgainstNull(response.Content, nameof(response.Content));
            return AddItemAsync(response, uri, token);
        }

        public async Task AddItemAsync(
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

        public async Task<string> StringAsync(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            using var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
            return await result.AsStringAsync(token);
        }

        public async Task<byte[]> BytesAsync(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            using var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
            return await result.AsBytesAsync(token);
        }

        public async Task<Stream> StreamAsync(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
            return await result.AsStreamAsync(token);
        }

        public async Task<HttpResponseMessage> ResponseAsync(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
            return result.AsResponseMessage();
        }

        public async Task ToStreamAsync(
            string uri,
            Stream stream,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            using var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
            await result.ToStreamAsync(stream, token);
        }

        public async Task ToFileAsync(
            string uri,
            string path,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            using var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
            await result.ToFileAsync(path, token);
        }

        public string String(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            using var result = Download(uri, staleIfError, modifyRequest, token);
            return result.AsString();
        }

        public async IAsyncEnumerable<string> LinesAsync(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            using var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
            using var stream = result.AsStream(token);
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                yield return line;
            }
        }

        public IEnumerable<string> Lines(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            using var result = Download(uri, staleIfError, modifyRequest, token);
            using var stream = result.AsStream(token);
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                yield return line;
            }
        }

        public byte[] Bytes(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            using var result = Download(uri, staleIfError, modifyRequest, token);
            return result.AsBytes();
        }

        public Stream Stream(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            var result = Download(uri, staleIfError, modifyRequest, token);
            return result.AsStream();
        }

        public async Task<HttpResponseMessage> Response(
            string uri,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
            return result.AsResponseMessage();
        }

        public void ToStream(
            string uri,
            Stream stream,
            bool staleIfError = false,
            Action<HttpRequestMessage>? modifyRequest = null,
            CancellationToken token = default)
        {
            using var result = Download(uri, staleIfError, modifyRequest, token);
            result.ToStream(stream, token);
        }

        public void ToFile(
            string uri,
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