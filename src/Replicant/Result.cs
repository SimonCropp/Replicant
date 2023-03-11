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

        var stream = await Response.Content.ReadAsStreamAsync(token);
        return new StreamWithCleanup(stream, this);
    }

    public Task<byte[]> AsBytesAsync(Cancellation cancellation)
    {
        if (Response == null)
        {
            return FileEx.ReadAllBytesAsync(File!.Value.Content, token);
        }

        return Response.Content.ReadAsByteArrayAsync(token);
    }

    public Task<string> AsStringAsync(Cancellation cancellation)
    {
        if (Response == null)
        {
            return FileEx.ReadAllTextAsync(File!.Value.Content, token);
        }

        return Response.Content.ReadAsStringAsync(token);
    }

    public async Task ToStreamAsync(Stream stream, Cancellation cancellation)
    {
        if (Response == null)
        {
            using var openRead = FileEx.OpenRead(File!.Value.Content);
            await openRead.CopyToAsync(stream, token);
            return;
        }

        await Response.Content.CopyToAsync(stream, token);
    }

    public async Task ToFileAsync(string path, Cancellation cancellation)
    {
        if (Response == null)
        {
            System.IO.File.Copy(File!.Value.Content, path, true);
            return;
        }

        using var stream = FileEx.OpenWrite(path);
        await Response.Content.CopyToAsync(stream, token);
    }

    public Stream AsStream(Cancellation cancellation = default)
    {
        if (Response == null)
        {
            return FileEx.OpenRead(File!.Value.Content);
        }

        var stream = Response.Content.ReadAsStream(token);
        return new StreamWithCleanup(stream, this);
    }

    public byte[] AsBytes(Cancellation cancellation = default)
    {
        if (Response == null)
        {
            return System.IO.File.ReadAllBytes(File!.Value.Content);
        }

        using var stream = Response.Content.ReadAsStream(token);
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

        using var stream = Response.Content.ReadAsStream(token);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void ToStream(Stream stream, Cancellation cancellation = default)
    {
        if (Response != null)
        {
            Response.Content.CopyTo(stream, token);
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
            Content = new StreamContent(FileEx.OpenRead(File!.Value.Content))
        };
        MetaData.ApplyToResponse(File!.Value.Meta, response);
        return response;
    }

    public void Dispose() =>
        Response?.Dispose();
}