namespace Replicant;

/// <summary>
/// A disk-based <see cref="IDistributedCache"/> implementation.
/// Can be used as an L2 cache backend for HybridCache.
/// </summary>
public class ReplicantDistributedCache :
    IDistributedCache,
    IDisposable,
    IAsyncDisposable
{
    static ConcurrentDictionary<string, byte> activeDirectories = new(StringComparer.OrdinalIgnoreCase);

    string directory;
    int maxEntries;
    Timer timer;

    static TimeSpan purgeInterval = TimeSpan.FromMinutes(10);
    static TimeSpan ignoreTimeSpan = TimeSpan.FromMilliseconds(-1);

    /// <summary>
    /// Instantiate a new instance of <see cref="ReplicantDistributedCache"/>.
    /// </summary>
    /// <param name="directory">The directory to store cache files.</param>
    /// <param name="maxEntries">The maximum entries to store in the cache.</param>
    public ReplicantDistributedCache(string directory, int maxEntries = 1000)
    {
        Guard.AgainstNullOrEmpty(directory, nameof(directory));
        if (maxEntries < 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "maxEntries must be greater than 100");
        }

        this.directory = Path.GetFullPath(directory);

        if (!activeDirectories.TryAdd(this.directory, 0))
        {
            throw new InvalidOperationException(
                $"A distributed cache already exists for directory '{this.directory}'.");
        }

        this.maxEntries = maxEntries;
        Directory.CreateDirectory(this.directory);
        timer = new(_ => PauseAndPurge(), null, ignoreTimeSpan, purgeInterval);
    }

    /// <inheritdoc/>
    public byte[]? Get(string key)
    {
        var (dataPath, metaPath) = GetPaths(key);

        if (!File.Exists(dataPath))
        {
            return null;
        }

        var meta = ReadMeta(metaPath);
        if (IsExpired(meta))
        {
            TryDelete(dataPath);
            TryDelete(metaPath);
            return null;
        }

        RefreshSliding(metaPath, meta);

        try
        {
            return File.ReadAllBytes(dataPath);
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetAsync(string key, Cancel cancel = default)
    {
        var (dataPath, metaPath) = GetPaths(key);

        if (!File.Exists(dataPath))
        {
            return null;
        }

        var meta = await ReadMetaAsync(metaPath, cancel);
        if (IsExpired(meta))
        {
            TryDelete(dataPath);
            TryDelete(metaPath);
            return null;
        }

        await RefreshSlidingAsync(metaPath, meta, cancel);

        try
        {
            return await File.ReadAllBytesAsync(dataPath, cancel);
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        var (dataPath, metaPath) = GetPaths(key);
        var meta = BuildMeta(options);

        var tempData = FileEx.GetTempFileName();
        var tempMeta = FileEx.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempData, value);
            WriteMeta(tempMeta, meta);
            File.Move(tempData, dataPath, true);
            File.Move(tempMeta, metaPath, true);
        }
        finally
        {
            TryDelete(tempData);
            TryDelete(tempMeta);
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
        Cancel cancel = default)
    {
        var (dataPath, metaPath) = GetPaths(key);
        var meta = BuildMeta(options);

        var tempData = FileEx.GetTempFileName();
        var tempMeta = FileEx.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempData, value, cancel);
            await WriteMetaAsync(tempMeta, meta, cancel);
            File.Move(tempData, dataPath, true);
            File.Move(tempMeta, metaPath, true);
        }
        finally
        {
            TryDelete(tempData);
            TryDelete(tempMeta);
        }
    }

    /// <inheritdoc/>
    public void Refresh(string key)
    {
        var (_, metaPath) = GetPaths(key);
        var meta = ReadMeta(metaPath);
        RefreshSliding(metaPath, meta);
    }

    /// <inheritdoc/>
    public async Task RefreshAsync(string key, Cancel cancel = default)
    {
        var (_, metaPath) = GetPaths(key);
        var meta = await ReadMetaAsync(metaPath, cancel);
        await RefreshSlidingAsync(metaPath, meta, cancel);
    }

    /// <inheritdoc/>
    public void Remove(string key)
    {
        var (dataPath, metaPath) = GetPaths(key);
        TryDelete(dataPath);
        TryDelete(metaPath);
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string key, Cancel cancel = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Purge all cached items.
    /// </summary>
    public void Purge()
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.dat"))
        {
            TryDelete(file);
            TryDelete(Path.ChangeExtension(file, ".meta"));
        }
    }

    /// <summary>
    /// Purge cached items that exceed max entries, ordered by last access time.
    /// </summary>
    public void PurgeOld()
    {
        foreach (var file in Directory
            .EnumerateFiles(directory, "*.dat")
            .OrderByDescending(File.GetLastAccessTime)
            .Skip(maxEntries))
        {
            TryDelete(file);
            TryDelete(Path.ChangeExtension(file, ".meta"));
        }
    }

    (string dataPath, string metaPath) GetPaths(string key)
    {
        var hash = Hash.Compute(key);
        return (
            Path.Combine(directory, $"{hash}.dat"),
            Path.Combine(directory, $"{hash}.meta"));
    }

    static CacheEntryMeta BuildMeta(DistributedCacheEntryOptions options)
    {
        var now = DateTimeOffset.UtcNow;
        long absoluteExpiration = 0;

        if (options.AbsoluteExpiration.HasValue)
        {
            absoluteExpiration = options.AbsoluteExpiration.Value.UtcTicks;
        }
        else if (options.AbsoluteExpirationRelativeToNow.HasValue)
        {
            absoluteExpiration = now.Add(options.AbsoluteExpirationRelativeToNow.Value).UtcTicks;
        }

        var slidingExpiration = options.SlidingExpiration?.Ticks ?? 0;
        var slidingDeadline = slidingExpiration > 0 ? now.UtcTicks + slidingExpiration : 0;

        if (absoluteExpiration > 0 && slidingDeadline > absoluteExpiration)
        {
            slidingDeadline = absoluteExpiration;
        }

        return new()
        {
            AbsoluteExpiration = absoluteExpiration,
            SlidingExpiration = slidingExpiration,
            SlidingDeadline = slidingDeadline
        };
    }

    static bool IsExpired(CacheEntryMeta? meta)
    {
        if (meta == null)
        {
            return true;
        }

        var now = DateTimeOffset.UtcNow.UtcTicks;

        if (meta.AbsoluteExpiration > 0 && now > meta.AbsoluteExpiration)
        {
            return true;
        }

        return meta.SlidingExpiration > 0 && now > meta.SlidingDeadline;
    }

    static void RefreshSliding(string metaPath, CacheEntryMeta? meta)
    {
        if (meta is not { SlidingExpiration: > 0 })
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.UtcTicks;
        meta.SlidingDeadline = now + meta.SlidingExpiration;

        if (meta.AbsoluteExpiration > 0 && meta.SlidingDeadline > meta.AbsoluteExpiration)
        {
            meta.SlidingDeadline = meta.AbsoluteExpiration;
        }

        WriteMeta(metaPath, meta);
    }

    static Task RefreshSlidingAsync(string metaPath, CacheEntryMeta? meta, Cancel cancel)
    {
        if (meta is not { SlidingExpiration: > 0 })
        {
            return Task.CompletedTask;
        }

        var now = DateTimeOffset.UtcNow.UtcTicks;
        meta.SlidingDeadline = now + meta.SlidingExpiration;

        if (meta.AbsoluteExpiration > 0 && meta.SlidingDeadline > meta.AbsoluteExpiration)
        {
            meta.SlidingDeadline = meta.AbsoluteExpiration;
        }

        return WriteMetaAsync(metaPath, meta, cancel);
    }

    static CacheEntryMeta? ReadMeta(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllBytes(path);
            return JsonSerializer.Deserialize<CacheEntryMeta>(json);
        }
        catch (Exception exception)
            when (exception is IOException or JsonException)
        {
            return null;
        }
    }

    static async Task<CacheEntryMeta?> ReadMetaAsync(string path, Cancel cancel)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllBytesAsync(path, cancel);
            return JsonSerializer.Deserialize<CacheEntryMeta>(json);
        }
        catch (Exception exception)
            when (exception is IOException or JsonException)
        {
            return null;
        }
    }

    static void WriteMeta(string path, CacheEntryMeta meta)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(meta);
        File.WriteAllBytes(path, json);
    }

    static Task WriteMetaAsync(string path, CacheEntryMeta meta, Cancel cancel)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(meta);
        return File.WriteAllBytesAsync(path, json, cancel);
    }

    static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // ignore — file may be locked or already deleted
        }
    }

    void PurgeExpired()
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.meta"))
        {
            var meta = ReadMeta(file);
            if (IsExpired(meta))
            {
                TryDelete(Path.ChangeExtension(file, ".dat"));
                TryDelete(file);
            }
        }
    }

    void PauseAndPurge()
    {
        timer.Change(ignoreTimeSpan, ignoreTimeSpan);
        try
        {
            PurgeExpired();
            PurgeOld();
        }
        finally
        {
            timer.Change(purgeInterval, ignoreTimeSpan);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        timer.Dispose();
        activeDirectories.TryRemove(directory, out _);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        activeDirectories.TryRemove(directory, out _);
#if NET7_0_OR_GREATER
        return timer.DisposeAsync();
#else
        timer.Dispose();
        return default;
#endif
    }

    class CacheEntryMeta
    {
        public long AbsoluteExpiration { get; set; }
        public long SlidingExpiration { get; set; }
        public long SlidingDeadline { get; set; }
    }
}
