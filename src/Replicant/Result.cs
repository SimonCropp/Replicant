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

    public async Task<Stream> AsStreamAsync(Cancel cancel)
    {
        if (Response == null)
        {
            return FileEx.OpenRead(File!.Value.Content);
        }

        var stream = await Response.Content.ReadAsStreamAsync(cancel);
        return new StreamWithCleanup(stream, this);
    }

    public Task<byte[]> AsBytesAsync(Cancel cancel)
    {
        if (Response == null)
        {
            return FilePolyfill.ReadAllBytesAsync(File!.Value.Content, cancel);
        }

        return Response.Content.ReadAsByteArrayAsync(cancel);
    }

    public Task<string> AsStringAsync(Cancel cancel)
    {
        if (Response == null)
        {
            return FilePolyfill.ReadAllTextAsync(File!.Value.Content, cancel);
        }

        return Response.Content.ReadAsStringAsync(cancel);
    }

    public async Task ToStreamAsync(Stream stream, Cancel cancel)
    {
        if (Response == null)
        {
            using var openRead = FileEx.OpenRead(File!.Value.Content);
            await openRead.CopyToAsync(stream, cancel);
            return;
        }

        await Response.Content.CopyToAsync(stream, cancel);
    }

    public async Task ToFileAsync(string path, Cancel cancel)
    {
        if (Response == null)
        {
            System.IO.File.Copy(File!.Value.Content, path, true);
            return;
        }

        using var stream = FileEx.OpenWrite(path);
        await Response.Content.CopyToAsync(stream, cancel);
    }

    public Stream AsStream(Cancel cancel = default)
    {
        if (Response == null)
        {
            return FileEx.OpenRead(File!.Value.Content);
        }

        var stream = Response.Content.ReadAsStream(cancel);
        return new StreamWithCleanup(stream, this);
    }

    public byte[] AsBytes(Cancel cancel = default)
    {
        if (Response == null)
        {
            return System.IO.File.ReadAllBytes(File!.Value.Content);
        }

        using var stream = Response.Content.ReadAsStream(cancel);
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    public string AsString(Cancel cancel = default)
    {
        if (Response == null)
        {
            return System.IO.File.ReadAllText(File!.Value.Content);
        }

        using var stream = Response.Content.ReadAsStream(cancel);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void ToStream(Stream stream, Cancel cancel = default)
    {
        if (Response != null)
        {
            Response.Content.CopyTo(stream, cancel);
            return;
        }

        using var openRead = FileEx.OpenRead(File!.Value.Content);
        openRead.CopyTo(stream);
    }

    public void ToFile(string path, Cancel cancel = default)
    {
        if (Response == null)
        {
            System.IO.File.Copy(File!.Value.Content, path, true);
            return;
        }

        using var stream = FileEx.OpenWrite(path);
        Response.Content.CopyTo(stream, cancel);
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