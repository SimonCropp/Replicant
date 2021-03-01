using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

[DebuggerDisplay("ContentPath={ContentPath}, MetaPath={MetaPath}, Status={Status}")]
readonly struct Result :
    IDisposable
{
    public string? ContentPath { get; }
    public string? MetaPath { get; }
    public HttpResponseMessage? Response { get; }
    public CacheStatus Status { get; }

    public Result(string contentPath, CacheStatus status, string metaPath)
    {
        ContentPath = contentPath;
        Status = status;
        Response = null;
        MetaPath = metaPath;
    }

    public Result(HttpResponseMessage response, CacheStatus status)
    {
        ContentPath = null;
        Response = response;
        Status = status;
        MetaPath = null;
    }

    public async Task<Stream> AsStream(CancellationToken token)
    {
        if (Response == null)
        {
            return FileEx.OpenRead(ContentPath!);
        }

        var stream = await Response.Content.ReadAsStreamAsync(token);
        return new StreamWithCleanup(stream);
    }

    public Task<byte[]> AsBytes(CancellationToken token)
    {
        if (Response == null)
        {
            return File.ReadAllBytesAsync(ContentPath!, token);
        }

        return Response.Content.ReadAsByteArrayAsync(token);
    }

    public Task<string> AsText(CancellationToken token)
    {
        if (Response == null)
        {
            return File.ReadAllTextAsync(ContentPath!, token);
        }

        return Response.Content.ReadAsStringAsync(token);
    }

    public async Task ToStream(Stream stream, CancellationToken token)
    {
        if (Response == null)
        {
            await using var openRead = FileEx.OpenRead(ContentPath!);
            await openRead.CopyToAsync(stream, token);
            return;
        }

        await Response.Content.CopyToAsync(stream, token);
    }

    public async Task ToFile(string path, CancellationToken token)
    {
        if (Response == null)
        {
            File.Copy(ContentPath!, path, true);
            return;
        }

        await using var stream = FileEx.OpenWrite(path);
        await Response.Content.CopyToAsync(stream, token);
    }

    public HttpResponseMessage AsResponseMessage()
    {
        if (Response != null)
        {
            return Response;
        }
        var meta = MetaDataReader.ReadMeta(MetaPath!);
        HttpResponseMessage message = new()
        {
            Content = new StreamContent(FileEx.OpenRead(ContentPath!))
        };
        message.Headers.AddRange(meta.ResponseHeaders);
        message.Content.Headers.AddRange(meta.ContentHeaders);
        message.TrailingHeaders.AddRange(meta.TrailingHeaders);
        return message;
    }

    public void Dispose()
    {
        Response?.Dispose();
    }
}