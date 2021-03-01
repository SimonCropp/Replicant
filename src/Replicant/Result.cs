using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using Replicant;

[DebuggerDisplay("ContentPath={ContentPath}, MetaPath={MetaPath}, Status={Status}")]
internal readonly struct Result
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

    public HttpResponseHeaders GetResponseHeaders()
    {
        var meta = MetaDataReader.ReadMeta(MetaPath);
        using HttpResponseMessage message = new();
        message.Headers.AddRange(meta.ResponseHeaders);
        return message.Headers;
    }

    public HttpContentHeaders GetContentHeaders()
    {
        var meta = MetaDataReader.ReadMeta(MetaPath);
        using HttpResponseMessage message = new();
        message.Content.Headers.AddRange(meta.ContentHeaders);
        return message.Content.Headers;
    }

    public HttpResponseHeaders GetTrailingHeaders()
    {
        var meta = MetaDataReader.ReadMeta(MetaPath);
        using HttpResponseMessage message = new();
        message.TrailingHeaders.AddRange(meta.TrailingHeaders);
        return message.TrailingHeaders;
    }
}