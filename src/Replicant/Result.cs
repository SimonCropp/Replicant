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

    public async Task<Stream> AsStreamAsync(CancellationToken token)
    {
        if (Response == null)
        {
            return FileEx.OpenRead(ContentPath!);
        }

        var stream = await Response.Content.ReadAsStreamAsync(token);
        return new StreamWithCleanup(stream);
    }

    public Task<byte[]> AsBytesAsync(CancellationToken token)
    {
        if (Response == null)
        {
            return File.ReadAllBytesAsync(ContentPath!, token);
        }

        return Response.Content.ReadAsByteArrayAsync(token);
    }

    public Task<string> AsStringAsync(CancellationToken token)
    {
        if (Response == null)
        {
            return File.ReadAllTextAsync(ContentPath!, token);
        }

        return Response.Content.ReadAsStringAsync(token);
    }

    public async Task ToStreamAsync(Stream stream, CancellationToken token)
    {
        if (Response == null)
        {
            await using var openRead = FileEx.OpenRead(ContentPath!);
            await openRead.CopyToAsync(stream, token);
            return;
        }

        await Response.Content.CopyToAsync(stream, token);
    }

    public async Task ToFileAsync(string path, CancellationToken token)
    {
        if (Response == null)
        {
            File.Copy(ContentPath!, path, true);
            return;
        }

        await using var stream = FileEx.OpenWrite(path);
        await Response.Content.CopyToAsync(stream, token);
    }

    public Stream AsStream()
    {
        if (Response == null)
        {
            return FileEx.OpenRead(ContentPath!);
        }

        var stream = Response.Content.ReadAsStream();
        return new StreamWithCleanup(stream);
    }

    public byte[] AsBytes()
    {
        if (Response == null)
        {
            return File.ReadAllBytes(ContentPath!);
        }

        using var stream = Response.Content.ReadAsStream();
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    public string AsString()
    {
        if (Response == null)
        {
            return File.ReadAllText(ContentPath!);
        }

        using var stream = Response.Content.ReadAsStream();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void ToStream(Stream stream)
    {
        if (Response == null)
        {
            using var openRead = FileEx.OpenRead(ContentPath!);
            openRead.CopyTo(stream);
            return;
        }

        Response.Content.CopyTo(stream, null, default);
    }

    public void ToFile(string path)
    {
        if (Response == null)
        {
            File.Copy(ContentPath!, path, true);
            return;
        }

        using var stream = FileEx.OpenWrite(path);
        Response.Content.CopyTo(stream, null, default);
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