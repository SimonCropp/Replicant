using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

public class DistributedCacheTests
{
    static string root = Path.Combine(Path.GetTempPath(), "ReplicantDistributedCacheTests");

    static string CachePath([CallerMemberName] string name = "") =>
        Path.Combine(root, name);

    [After(Class)]
    public static void Cleanup()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    static void DistributedCacheUsage(string cacheDirectory)
    {
        #region DistributedCacheUsage

        var services = new ServiceCollection();
        services.AddReplicantDistributedCache(cacheDirectory);
        services.AddHybridCache();

        #endregion
    }

    [Test]
    public async Task SetAndGet()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new());

        var result = cache.Get("key1");
        await Assert.That(result).IsEquivalentTo("hello"u8.ToArray(), CollectionOrdering.Matching);
    }

    [Test]
    public async Task SetAndGetAsync()
    {
        var path = CachePath();
        await using var cache = new ReplicantDistributedCache(path);

        await cache.SetAsync("key1", "hello"u8.ToArray(), new());

        var result = await cache.GetAsync("key1");
        await Assert.That(result).IsEquivalentTo("hello"u8.ToArray(), CollectionOrdering.Matching);
    }

    [Test]
    public async Task Get_MissingKey_ReturnsNull()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        var result = cache.Get("missing");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task AbsoluteExpiration()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(1)
        });

        Thread.Sleep(50);

        var result = cache.Get("key1");
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task SlidingExpiration()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new()
        {
            SlidingExpiration = TimeSpan.FromMilliseconds(1)
        });

        Thread.Sleep(50);

        var result = cache.Get("key1");
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task SlidingExpiration_RefreshKeepsAlive()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new()
        {
            SlidingExpiration = TimeSpan.FromSeconds(30)
        });

        cache.Refresh("key1");

        var result = cache.Get("key1");
        await Assert.That(result).IsEquivalentTo("hello"u8.ToArray(), CollectionOrdering.Matching);
    }

    [Test]
    public async Task Remove_DeletesEntry()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new());
        cache.Remove("key1");

        var result = cache.Get("key1");
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Purge_DeletesAll()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new());
        cache.Set("key2", "world"u8.ToArray(), new());

        cache.Purge();

        await Assert.That(cache.Get("key1")).IsNull();
        await Assert.That(cache.Get("key2")).IsNull();
    }

    [Test]
    public async Task NoExpiration_LivesForever()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new());

        var result = cache.Get("key1");
        await Assert.That(result).IsEquivalentTo("hello"u8.ToArray(), CollectionOrdering.Matching);
    }

    [Test]
    public async Task OverwriteExistingKey()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new());
        cache.Set("key1", "world"u8.ToArray(), new());

        var result = cache.Get("key1");
        await Assert.That(result).IsEquivalentTo("world"u8.ToArray(), CollectionOrdering.Matching);
    }

    [Test]
    public async Task AbsoluteExpiration_DateTimeOffset()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new()
        {
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddMilliseconds(1)
        });

        Thread.Sleep(50);

        var result = cache.Get("key1");
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task SlidingExpiration_CappedByAbsolute()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(1),
            SlidingExpiration = TimeSpan.FromHours(1)
        });

        Thread.Sleep(50);

        // Sliding is long but absolute has passed — should be expired
        var result = cache.Get("key1");
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task AbsoluteExpiration_CleansUpFiles()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(1)
        });

        Thread.Sleep(50);

        cache.Get("key1");

        var datFiles = Directory.GetFiles(path, "*.dat");
        var metaFiles = Directory.GetFiles(path, "*.meta");
        await Assert.That(datFiles.Length).IsEqualTo(0);
        await Assert.That(metaFiles.Length).IsEqualTo(0);
    }

    [Test]
    public async Task RemoveAsync_DeletesEntry()
    {
        var path = CachePath();
        await using var cache = new ReplicantDistributedCache(path);

        await cache.SetAsync("key1", "hello"u8.ToArray(), new());
        await cache.RemoveAsync("key1");

        var result = await cache.GetAsync("key1");
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task RefreshAsync_KeepsAlive()
    {
        var path = CachePath();
        await using var cache = new ReplicantDistributedCache(path);

        await cache.SetAsync("key1", "hello"u8.ToArray(), new()
        {
            SlidingExpiration = TimeSpan.FromSeconds(30)
        });

        await cache.RefreshAsync("key1");

        var result = await cache.GetAsync("key1");
        await Assert.That(result).IsEquivalentTo("hello"u8.ToArray(), CollectionOrdering.Matching);
    }

    [Test]
    public async Task PurgeOld_EnforcesMaxEntries()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path, maxEntries: 100);

        for (var i = 0; i < 150; i++)
        {
            cache.Set($"key{i}", "hello"u8.ToArray(), new());
        }

        cache.PurgeOld();

        var datFiles = Directory.GetFiles(path, "*.dat");
        await Assert.That(datFiles.Length <= 100).IsTrue();
    }

    [Test]
    public async Task Constructor_NullDirectory_Throws() =>
        await Assert.That(() => new ReplicantDistributedCache(null!)).ThrowsExactly<ArgumentNullException>();

    [Test]
    public async Task Constructor_EmptyDirectory_Throws() =>
        await Assert.That(() => new ReplicantDistributedCache("")).ThrowsExactly<ArgumentNullException>();

    [Test]
    public async Task Constructor_MaxEntriesTooLow_Throws() =>
        await Assert.That(() => new ReplicantDistributedCache(CachePath(), maxEntries: 50)).ThrowsExactly<ArgumentOutOfRangeException>();

    [Test]
    public async Task DuplicateDirectory_Throws()
    {
        var path = CachePath();
        using var cache1 = new ReplicantDistributedCache(path);

        await Assert.That(() => new ReplicantDistributedCache(path)).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public void DuplicateDirectory_AfterDispose_Allowed()
    {
        var path = CachePath();
        var cache1 = new ReplicantDistributedCache(path);
        cache1.Dispose();

        using var cache2 = new ReplicantDistributedCache(path);
    }

    [Test]
    public async Task DependencyInjection()
    {
        var path = CachePath();
        var services = new ServiceCollection();
        services.AddReplicantDistributedCache(path);

        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IDistributedCache>();

        await Assert.That(cache).IsTypeOf<ReplicantDistributedCache>();
    }

    [Test]
    public async Task HybridCacheIntegration()
    {
        var path = CachePath();
        var services = new ServiceCollection();
        services.AddReplicantDistributedCache(path);
        services.AddHybridCache();

        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<HybridCache>();

        var value = await cache.GetOrCreateAsync("key1", async _ => "hello");
        await Assert.That(value).IsEqualTo("hello");

        // Second call served from cache
        var value2 = await cache.GetOrCreateAsync("key1", async _ => "world");
        await Assert.That(value2).IsEqualTo("hello");
    }
}
