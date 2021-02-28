using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Replicant
{
    [DebuggerDisplay("ContentPath={ContentPath}, MetaPath={MetaPath}, Status={Status}")]
    public readonly struct Result
    {
        public string ContentPath { get; }
        public string MetaPath { get; }
        public CacheStatus Status { get; }

        public Result(string contentPath, CacheStatus status, string metaPath)
        {
            ContentPath = contentPath;
            Status = status;
            MetaPath = metaPath;
        }

        public async Task<HttpResponseMessage> AsResponseMessage()
        {
            var meta = await MetaDataReader.ReadMeta(MetaPath);
            HttpResponseMessage message = new()
            {
                Content = new StreamContent(FileEx.OpenRead(ContentPath))
            };
            message.Headers.AddRange(meta.ResponseHeaders);
            message.Content.Headers.AddRange(meta.ContentHeaders);
            return message;
        }

        public async Task<HttpResponseHeaders> GetResponseHeaders()
        {
            var meta = await MetaDataReader.ReadMeta(MetaPath);
            using HttpResponseMessage message = new();
            message.Headers.AddRange(meta.ResponseHeaders);
            return message.Headers;
        }

        public async Task<HttpContentHeaders> GetContentHeaders()
        {
            var meta = await MetaDataReader.ReadMeta(MetaPath);
            using HttpResponseMessage message = new();
            message.Content.Headers.AddRange(meta.ContentHeaders);
            return message.Content.Headers;
        }

        public async Task<HttpResponseHeaders> GetTrailingHeaders()
        {
            var meta = await MetaDataReader.ReadMeta(MetaPath);
            using HttpResponseMessage message = new();
            message.TrailingHeaders.AddRange(meta.TrailingHeaders);
            return message.TrailingHeaders;
        }
    }
}