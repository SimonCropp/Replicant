class CacheStore
{
    string directory;
    int maxEntries;
    Timer timer;
    Action purgeOldAction;

    static TimeSpan purgeInterval = TimeSpan.FromMinutes(10);
    static TimeSpan ignoreTimeSpan = TimeSpan.FromMilliseconds(-1);

    static JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = true
    };

    public CacheStore(string directory, int maxEntries, Action? onTimerPurge = null)
    {
        Guard.AgainstNullOrEmpty(directory, nameof(directory));
        if (maxEntries < 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "maxEntries must be greater than 100");
        }

        this.directory = directory;
        this.maxEntries = maxEntries;

        Directory.CreateDirectory(directory);

        purgeOldAction = onTimerPurge ?? PurgeOld;
        timer = new(_ => PauseAndPurgeOld(), null, ignoreTimeSpan, purgeInterval);
    }

    public FilePair? FindContentFileForUri(Uri uri)
    {
        var hash = Hash.Compute(uri.AbsoluteUri);
        var file = Directory
            .EnumerateFiles(directory, $"{hash}_*.bin")
            .MinBy(File.GetLastWriteTime);
        if (file == null)
        {
            return null;
        }

        return FilePair.FromContentFile(file);
    }

    public async Task<FilePair> AddItemAsync(
        Cancel cancel,
        Func<Cancel, Task<Stream>> contentFunc,
        MetaData meta,
        Timestamp timestamp)
    {
        var tempFile = FilePair.GetTemp();
        try
        {
#if NET7_0_OR_GREATER
            await using var httpStream = await contentFunc(cancel);
            await using (var contentFileStream = FileEx.OpenWrite(tempFile.Content))
            await using (var metaFileStream = FileEx.OpenWrite(tempFile.Meta))
            {
#else
            using var httpStream = await contentFunc(cancel);
            using (var contentFileStream = FileEx.OpenWrite(tempFile.Content))
            using (var metaFileStream = FileEx.OpenWrite(tempFile.Meta))
            {
#endif
                await JsonSerializer.SerializeAsync(metaFileStream, meta, serializerOptions, cancel);
                await httpStream.CopyToAsync(contentFileStream, cancel);
            }

            return MoveToFinalLocation(timestamp, tempFile);
        }
        finally
        {
            tempFile.Delete();
        }
    }

    public FilePair AddItem(
        Cancel cancel,
        Stream content,
        MetaData meta,
        Timestamp timestamp)
    {
        var tempFile = FilePair.GetTemp();
        try
        {
            using (var contentFileStream = FileEx.OpenWrite(tempFile.Content))
            using (var metaFileStream = FileEx.OpenWrite(tempFile.Meta))
            using (var writer = new Utf8JsonWriter(metaFileStream, new() { Indented = true }))
            {
                JsonSerializer.Serialize(writer, meta, serializerOptions);
                content.CopyTo(contentFileStream);
            }

            return MoveToFinalLocation(timestamp, tempFile);
        }
        finally
        {
            tempFile.Delete();
        }
    }

    public async Task<FilePair> StoreResponseAsync(
        HttpResponseMessage response, Uri uri, Cancel cancel)
    {
        var timestamp = Timestamp.FromResponse(uri, response);
        var meta = MetaData.FromEnumerables(
            uri.AbsoluteUri, response.Headers, response.Content.Headers, response.TrailingHeaders());
        return await AddItemAsync(cancel, c => response.Content.ReadAsStreamAsync(c), meta, timestamp);
    }

    public FilePair StoreResponse(
        HttpResponseMessage response, Uri uri, Cancel cancel)
    {
        var timestamp = Timestamp.FromResponse(uri, response);
        var meta = MetaData.FromEnumerables(
            uri.AbsoluteUri, response.Headers, response.Content.Headers, response.TrailingHeaders());
        using var stream = response.Content.ReadAsStream(cancel);
        return AddItem(cancel, stream, meta, timestamp);
    }

    FilePair MoveToFinalLocation(Timestamp timestamp, FilePair tempFile)
    {
        tempFile.SetExpiry(timestamp.Expiry);

        var contentFile = Path.Combine(directory, timestamp.ContentFileName);
        var metaFile = Path.Combine(directory, timestamp.MetaFileName);

        try
        {
            File.Move(tempFile.Content, contentFile, true);
            File.Move(tempFile.Meta, metaFile, true);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            //Failed to move files, so use temp files instead
            var newName = Path.GetFileNameWithoutExtension(contentFile);
            newName += Guid.NewGuid();

            var newMeta = $"{newName}.json";
            File.Move(tempFile.Meta, newMeta, true);
            if (File.Exists(tempFile.Content))
            {
                var newContent = $"{newName}.bin";
                File.Move(tempFile.Content, newContent, true);
                return new(newContent, newMeta);
            }

            return new(contentFile, newMeta);
        }

        return new(contentFile, metaFile);
    }

    public async Task<(bool stored, FilePair? file)> HandleCacheStatusAsync(
        HttpResponseMessage response, bool staleIfError, FilePair existingFile, Uri uri, Cancel cancel)
    {
        var status = response.GetCacheStatus(staleIfError);
        switch (status)
        {
            case CacheStatus.Hit:
            case CacheStatus.UseStaleDueToError:
            {
                response.Dispose();
                return (false, existingFile);
            }
            case CacheStatus.Stored:
            case CacheStatus.Revalidate:
            {
                using (response)
                {
                    return (true, await StoreResponseAsync(response, uri, cancel));
                }
            }
            case CacheStatus.NoStore:
            {
                return (false, null);
            }
            default:
            {
                response.Dispose();
                throw new ArgumentOutOfRangeException();
            }
        }
    }

    public (bool stored, FilePair? file) HandleCacheStatus(
        HttpResponseMessage response, bool staleIfError, FilePair existingFile, Uri uri, Cancel cancel)
    {
        var status = response.GetCacheStatus(staleIfError);
        switch (status)
        {
            case CacheStatus.Hit:
            case CacheStatus.UseStaleDueToError:
            {
                response.Dispose();
                return (false, existingFile);
            }
            case CacheStatus.Stored:
            case CacheStatus.Revalidate:
            {
                using (response)
                {
                    return (true, StoreResponse(response, uri, cancel));
                }
            }
            case CacheStatus.NoStore:
            {
                return (false, null);
            }
            default:
            {
                response.Dispose();
                throw new ArgumentOutOfRangeException();
            }
        }
    }

    public async Task<(bool revalidated, bool stored, FilePair? file, HttpResponseMessage? response)> RevalidateAsync(
        FilePair existingFile,
        bool staleIfError,
        Uri uri,
        Func<Timestamp, Task<HttpResponseMessage>> sendAsync,
        Cancel cancel)
    {
        var now = DateTimeOffset.UtcNow;
        var timestamp = Timestamp.FromPath(existingFile.Content);
        var expiry = timestamp.Expiry;

        if (expiry == null || expiry > now)
        {
            return (false, false, existingFile, null);
        }

        HttpResponseMessage response;
        try
        {
            response = await sendAsync(timestamp);
        }
        catch (Exception exception)
        {
            if (ShouldReturnStaleIfError(staleIfError, exception, cancel))
            {
                return (true, false, existingFile, null);
            }

            throw;
        }

        var (stored, resultFile) = await HandleCacheStatusAsync(response, staleIfError, existingFile, uri, cancel);
        return resultFile == null
            ? (true, stored, null, response)
            : (true, stored, resultFile, null);
    }

    public (bool revalidated, bool stored, FilePair? file, HttpResponseMessage? response) Revalidate(
        FilePair existingFile,
        bool staleIfError,
        Uri uri,
        Func<Timestamp, HttpResponseMessage> send,
        Cancel cancel)
    {
        var now = DateTimeOffset.UtcNow;
        var timestamp = Timestamp.FromPath(existingFile.Content);
        var expiry = timestamp.Expiry;

        if (expiry == null || expiry > now)
        {
            return (false, false, existingFile, null);
        }

        HttpResponseMessage response;
        try
        {
            response = send(timestamp);
        }
        catch (Exception exception)
        {
            if (ShouldReturnStaleIfError(staleIfError, exception, cancel))
            {
                return (true, false, existingFile, null);
            }

            throw;
        }

        var (stored, resultFile) = HandleCacheStatus(response, staleIfError, existingFile, uri, cancel);
        return resultFile == null
            ? (true, stored, null, response)
            : (true, stored, resultFile, null);
    }

    public async Task<(FilePair? file, HttpResponseMessage? response)> StoreNewResponseAsync(
        HttpResponseMessage response, Uri uri, Cancel cancel)
    {
        response.EnsureSuccess();
        if (response.IsNoStore())
        {
            return (null, response);
        }

        using (response)
        {
            return (await StoreResponseAsync(response, uri, cancel), null);
        }
    }

    public (FilePair? file, HttpResponseMessage? response) StoreNewResponse(
        HttpResponseMessage response, Uri uri, Cancel cancel)
    {
        response.EnsureSuccess();
        if (response.IsNoStore())
        {
            return (null, response);
        }

        using (response)
        {
            return (StoreResponse(response, uri, cancel), null);
        }
    }

    public static HttpResponseMessage BuildResponseFromCache(FilePair file)
    {
        var response = new HttpResponseMessage
        {
            Content = new StreamContent(FileEx.OpenRead(file.Content))
        };
        MetaData.ApplyToResponse(file.Meta, response);
        return response;
    }

    public static bool ShouldReturnStaleIfError(bool staleIfError, Exception exception, Cancel cancel) =>
        (
            exception is HttpRequestException ||
            exception is TaskCanceledException &&
            !cancel.IsCancellationRequested
        )
        && staleIfError;

    void PauseAndPurgeOld()
    {
        timer.Change(ignoreTimeSpan, ignoreTimeSpan);
        try
        {
            purgeOldAction();
        }
        finally
        {
            timer.Change(purgeInterval, ignoreTimeSpan);
        }
    }

    public void Purge()
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.bin"))
        {
            var pair = FilePair.FromContentFile(file);
            pair.PurgeItem();
        }
    }

    public void PurgeOld()
    {
        foreach (var file in Directory
            .EnumerateFiles(directory, "*_*_*.bin")
            .OrderByDescending(File.GetLastAccessTime)
            .Skip(maxEntries))
        {
            var pair = FilePair.FromContentFile(file);
            pair.PurgeItem();
        }
    }

    public void Dispose() => timer.Dispose();

    public ValueTask DisposeAsync()
    {
#if NET7_0_OR_GREATER
        return timer.DisposeAsync();
#else
        timer.Dispose();
        return default;
#endif
    }
}
