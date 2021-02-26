using System.Net.Http.Headers;

namespace Replicant
{
    public readonly struct Result
    {
        public string Path { get; }
        public CacheStatus Status { get; }
        public HttpResponseHeaders ResponseHeaders { get; }
        public HttpContentHeaders ContentHeaders { get; }

        public Result(string path, CacheStatus status, HttpResponseHeaders responseHeaders, HttpContentHeaders contentHeaders)
        {
            Path = path;
            Status = status;
            ResponseHeaders = responseHeaders;
            ContentHeaders = contentHeaders;
        }
    }
}