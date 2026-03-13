class CacheSession(CacheStore store, bool staleIfError)
{
    public async Task<(bool revalidated, bool stored, FilePair? file, HttpResponseMessage? response)> ProcessAsync(
        Uri uri,
        Func<Timestamp?, Task<HttpResponseMessage>> sendAsync,
        Cancel cancel)
    {
        var existingFile = store.FindContentFileForUri(uri);

        if (existingFile == null)
        {
            var response = await sendAsync(null);
            return await StoreNewResponseAsync(response, uri, cancel);
        }

        return await RevalidateAsync(existingFile.Value, uri, sendAsync, cancel);
    }

    public (bool revalidated, bool stored, FilePair? file, HttpResponseMessage? response) Process(
        Uri uri,
        Func<Timestamp?, HttpResponseMessage> send,
        Cancel cancel)
    {
        var existingFile = store.FindContentFileForUri(uri);

        if (existingFile == null)
        {
            var response = send(null);
            return StoreNewResponse(response, uri, cancel);
        }

        return Revalidate(existingFile.Value, uri, send, cancel);
    }

    async Task<(bool revalidated, bool stored, FilePair? file, HttpResponseMessage? response)> RevalidateAsync(
        FilePair existingFile,
        Uri uri,
        Func<Timestamp?, Task<HttpResponseMessage>> sendAsync,
        Cancel cancel)
    {
        var now = DateTimeOffset.UtcNow;
        var timestamp = Timestamp.FromPath(existingFile.Content);
        var expiry = timestamp.Expiry;

        if (expiry == null ||
            expiry > now)
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
            if (CacheStore.ShouldReturnStaleIfError(staleIfError, exception, cancel))
            {
                return (true, false, existingFile, null);
            }

            throw;
        }

        var (stored, resultFile) = await HandleCacheStatusAsync(response, existingFile, uri, cancel);
        return resultFile == null
            ? (true, stored, null, response)
            : (true, stored, resultFile, null);
    }

    (bool revalidated, bool stored, FilePair? file, HttpResponseMessage? response) Revalidate(
        FilePair existingFile,
        Uri uri,
        Func<Timestamp?, HttpResponseMessage> send,
        Cancel cancel)
    {
        var now = DateTimeOffset.UtcNow;
        var timestamp = Timestamp.FromPath(existingFile.Content);
        var expiry = timestamp.Expiry;

        if (expiry == null ||
            expiry > now)
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
            if (CacheStore.ShouldReturnStaleIfError(staleIfError, exception, cancel))
            {
                return (true, false, existingFile, null);
            }

            throw;
        }

        var (stored, resultFile) = HandleCacheStatus(response, existingFile, uri, cancel);
        return resultFile == null
            ? (true, stored, null, response)
            : (true, stored, resultFile, null);
    }

    async Task<(bool stored, FilePair? file)> HandleCacheStatusAsync(
        HttpResponseMessage response, FilePair existingFile, Uri uri, Cancel cancel)
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
                    return (true, await store.StoreResponseAsync(response, uri, cancel));
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

    (bool stored, FilePair? file) HandleCacheStatus(
        HttpResponseMessage response, FilePair existingFile, Uri uri, Cancel cancel)
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
                    return (true, store.StoreResponse(response, uri, cancel));
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

    async Task<(bool revalidated, bool stored, FilePair? file, HttpResponseMessage? response)> StoreNewResponseAsync(
        HttpResponseMessage response, Uri uri, Cancel cancel)
    {
        response.EnsureSuccess();
        if (response.IsNoStore())
        {
            return (true, false, null, response);
        }

        using (response)
        {
            return (true, true, await store.StoreResponseAsync(response, uri, cancel), null);
        }
    }

    (bool revalidated, bool stored, FilePair? file, HttpResponseMessage? response) StoreNewResponse(
        HttpResponseMessage response, Uri uri, Cancel cancel)
    {
        response.EnsureSuccess();
        if (response.IsNoStore())
        {
            return (true, false, null, response);
        }

        using (response)
        {
            return (true, true, store.StoreResponse(response, uri, cancel), null);
        }
    }

    public void Purge() => store.Purge();

    public void PurgeOld() => store.PurgeOld();

    public void Dispose() => store.Dispose();

    public ValueTask DisposeAsync() => store.DisposeAsync();
}
