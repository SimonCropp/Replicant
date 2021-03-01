using System.Diagnostics;
using System.Net.Http;
using Replicant;

[DebuggerDisplay("ContentPath={ContentPath}, MetaPath={MetaPath}, Status={Status}")]
readonly struct Result
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

    public HttpResponseMessage AsResponseMessage()
    {
        var meta = MetaDataReader.ReadMeta(MetaPath);
        HttpResponseMessage message = new()
        {
            Content = new StreamContent(FileEx.OpenRead(ContentPath))
        };
        message.Headers.AddRange(meta.ResponseHeaders);
        message.Content.Headers.AddRange(meta.ContentHeaders);
        message.TrailingHeaders.AddRange(meta.TrailingHeaders);
        return message;
    }
}