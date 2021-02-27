using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Replicant
{
    public partial class Download
    {
        public async Task<string> String(
            string uri,
            bool useStaleOnError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            var result = await DownloadFile(uri, useStaleOnError, messageCallback, token);
            return await File.ReadAllTextAsync(result.Path, token);
        }

        public async Task<byte[]> Bytes(
            string uri,
            bool useStaleOnError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            var result = await DownloadFile(uri, useStaleOnError, messageCallback, token);
            return await File.ReadAllBytesAsync(result.Path, token);
        }

        public async Task<Stream> Stream(
            string uri,
            bool useStaleOnError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            var result = await DownloadFile(uri, useStaleOnError, messageCallback, token);
            return File.OpenRead(result.Path);
        }

        public async Task ToStream(
            string uri,
            Stream stream,
            bool useStaleOnError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            var result = await DownloadFile(uri, useStaleOnError, messageCallback, token);
            await using var fileStream = FileEx.OpenRead(result.Path);
            await fileStream.CopyToAsync(stream, token);
        }

        public async Task ToFile(
            string uri,
            string path,
            bool useStaleOnError = false,
            Action<HttpRequestMessage>? messageCallback = null,
            CancellationToken token = default)
        {
            var result = await DownloadFile(uri, useStaleOnError, messageCallback, token);
            File.Copy(result.Path, path, true);
        }
    }
}