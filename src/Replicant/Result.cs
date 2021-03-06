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
        return new StreamWithCleanup(stream, this);
    }

    public Task<byte[]> AsBytesAsync(CancellationToken token)
    {
        if (Response == null)
        {
            return FileEx.ReadAllBytesAsync(ContentPath!, token);
        }

        return Response.Content.ReadAsByteArrayAsync(token);
    }

    public Task<string> AsStringAsync(CancellationToken token)
    {
        if (Response == null)
        {
            return FileEx.ReadAllTextAsync(ContentPath!, token);
        }

        return Response.Content.ReadAsStringAsync(token);
    }

    public async Task ToStreamAsync(Stream stream, CancellationToken token)
    {
        if (Response == null)
        {
            using var openRead = FileEx.OpenRead(ContentPath!);
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

        using var stream = FileEx.OpenWrite(path);
        await Response.Content.CopyToAsync(stream, token);
    }

    public Stream AsStream(CancellationToken token = default)
    {
        if (Response == null)
        {
            return FileEx.OpenRead(ContentPath!);
        }

        var stream = Response.Content.ReadAsStream(token);
        return new StreamWithCleanup(stream, this);
    }

    public byte[] AsBytes(CancellationToken token = default)
    {
        if (Response == null)
        {
            return File.ReadAllBytes(ContentPath!);
        }

        using var stream = Response.Content.ReadAsStream(token);
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    public string AsString(CancellationToken token = default)
    {
        if (Response == null)
        {
            return File.ReadAllText(ContentPath!);
        }

        using var stream = Response.Content.ReadAsStream(token);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void ToStream(Stream stream, CancellationToken token = default)
    {
        if (Response != null)
        {
            Response.Content.CopyTo(stream, token);
            return;
        }

        using var openRead = FileEx.OpenRead(ContentPath!);
        openRead.CopyTo(stream);
    }

    public void ToFile(string path, CancellationToken token = default)
    {
        if (Response == null)
        {
            File.Copy(ContentPath!, path, true);
            return;
        }

        using var stream = FileEx.OpenWrite(path);
        Response.Content.CopyTo(stream, token);
    }

    public HttpResponseMessage AsResponseMessage()
    {
        if (Response != null)
        {
            return Response;
        }

        HttpResponseMessage response = new()
        {
            Content = new StreamContent(FileEx.OpenRead(ContentPath!))
        };
        MetaData.ApplyToResponse(MetaPath!, response);
        return response;
    }

    public void Dispose()
    {
        Response?.Dispose();
    }
}