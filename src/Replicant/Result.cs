using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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

    public Stream AsStream()
    {
        return FileEx.OpenRead(ContentPath);
    }

    public Task<byte[]> AsBytes(CancellationToken token)
    {
        return File.ReadAllBytesAsync(ContentPath, token);
    }

    public Task<string> AsText(CancellationToken token)
    {
        return File.ReadAllTextAsync(ContentPath, token);
    }

#pragma warning disable 1998
    public async Task ToFile(string path, CancellationToken token)
#pragma warning restore 1998
    {
        File.Copy(ContentPath, path, true);
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