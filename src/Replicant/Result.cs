readonly struct Result :
    IDisposable
{
    public FilePair? File { get; }
    public HttpResponseMessage? Response { get; }
    public bool Revalidated { get; }
    public bool Stored { get; }
    public bool FromDisk { get; }

    public Result(FilePair file, bool revalidated, bool stored)
    {
        File = file;
        Revalidated = revalidated;
        Stored = stored;
        FromDisk = true;
        Response = null;
    }

    public Result(HttpResponseMessage response)
    {
        File = null;
        Response = response;
        Revalidated = true;
        Stored = false;
        FromDisk = false;
    }

    public async Task<Stream> AsStreamAsync(Cancellation cancellation)
    {
        if (Response == null)
        {
            return FileEx.OpenRead(File!.Value.Content);
        }

        var stream = await Response.Content.ReadAsStreamAsync(cancellation);
        return new StreamWithCleanup(stream, this);
    }

    public Task<byte[]> AsBytesAsync(Cancellation cancellation)
    {
        if (Response == null)
        {
            return FileEx.ReadAllBytesAsync(File!.Value.Content, cancellation);
        }

        return Response.Content.ReadAsByteArrayAsync(cancellation);
    }

    public Task<string> AsStringAsync(Cancellation cancellation)
    {
        if (Response == null)
        {
            return FileEx.ReadAllTextAsync(File!.Value.Content, cancellation);
        }

        return Response.Content.ReadAsStringAsync(cancellation);
    }

    public async Task ToStreamAsync(Stream stream, Cancellation cancellation)
    {
        if (Response == null)
        {
            using var openRead = FileEx.OpenRead(File!.Value.Content);
            await openRead.CopyToAsync(stream, cancellation);
            return;
        }

        await Response.Content.CopyToAsync(stream, cancellation);
    }

    public async Task ToFileAsync(string path, Cancellation cancellation)
    {
        if (Response == null)
        {
            System.IO.File.Copy(File!.Value.Content, path, true);
            return;
        }

        using var stream = FileEx.OpenWrite(path);
        await Response.Content.CopyToAsync(stream, cancellation);
    }

    public Stream AsStream(Cancellation cancellation = default)
    {
        if (Response == null)
        {
            return FileEx.OpenRead(File!.Value.Content);
        }

        var stream = Response.Content.ReadAsStream(cancellation);
        return new StreamWithCleanup(stream, this);
    }

    public byte[] AsBytes(Cancellation cancellation = default)
    {
        if (Response == null)
        {
            return System.IO.File.ReadAllBytes(File!.Value.Content);
        }

        using var stream = Response.Content.ReadAsStream(cancellation);
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    public string AsString(Cancellation cancellation = default)
    {
        if (Response == null)
        {
            return System.IO.File.ReadAllText(File!.Value.Content);
        }

        using var stream = Response.Content.ReadAsStream(cancellation);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void ToStream(Stream stream, Cancellation cancellation = default)
    {
        if (Response != null)
        {
            Response.Content.CopyTo(stream, cancellation);
            return;
        }

        using var openRead = FileEx.OpenRead(File!.Value.Content);
        openRead.CopyTo(stream);
    }

    public void ToFile(string path, Cancellation cancellation = default)
    {
        if (Response == null)
        {
            System.IO.File.Copy(File!.Value.Content, path, true);
            return;
        }

        using var stream = FileEx.OpenWrite(path);
        Response.Content.CopyTo(stream, cancellation);
    }

    public HttpResponseMessage AsResponseMessage()
    {
        if (Response != null)
        {
            return Response;
        }

        HttpResponseMessage response = new()
        {
            Content = new StreamContent(FileEx.OpenRead(File!.Value.Content))
        };
        MetaData.ApplyToResponse(File!.Value.Meta, response);
        return response;
    }

    public void Dispose() =>
        Response?.Dispose();
}