using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
[Parallelizable(ParallelScope.Children)]
public class DistributedCacheTests
{
    static string root = Path.Combine(Path.GetTempPath(), "ReplicantDistributedCacheTests");

    static string CachePath([CallerMemberName] string name = "") =>
        Path.Combine(root, name);

    [OneTimeTearDown]
    public void Cleanup()
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
    public void SetAndGet()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new());

        var result = cache.Get("key1");
        AreEqual("hello"u8.ToArray(), result);
    }

    [Test]
    public async Task SetAndGetAsync()
    {
        var path = CachePath();
        await using var cache = new ReplicantDistributedCache(path);

        await cache.SetAsync("key1", "hello"u8.ToArray(), new());

        var result = await cache.GetAsync("key1");
        AreEqual("hello"u8.ToArray(), result);
    }

    [Test]
    public void Get_MissingKey_ReturnsNull()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        var result = cache.Get("missing");

        IsNull(result);
    }

    [Test]
    public void AbsoluteExpiration()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(1)
        });

        Thread.Sleep(50);

        var result = cache.Get("key1");
        IsNull(result);
    }

    [Test]
    public void SlidingExpiration()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new()
        {
            SlidingExpiration = TimeSpan.FromMilliseconds(1)
        });

        Thread.Sleep(50);

        var result = cache.Get("key1");
        IsNull(result);
    }

    [Test]
    public void SlidingExpiration_RefreshKeepsAlive()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new()
        {
            SlidingExpiration = TimeSpan.FromSeconds(30)
        });

        cache.Refresh("key1");

        var result = cache.Get("key1");
        AreEqual("hello"u8.ToArray(), result);
    }

    [Test]
    public void Remove_DeletesEntry()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new());
        cache.Remove("key1");

        var result = cache.Get("key1");
        IsNull(result);
    }

    [Test]
    public void Purge_DeletesAll()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new());
        cache.Set("key2", "world"u8.ToArray(), new());

        cache.Purge();

        IsNull(cache.Get("key1"));
        IsNull(cache.Get("key2"));
    }

    [Test]
    public void NoExpiration_LivesForever()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new());

        var result = cache.Get("key1");
        AreEqual("hello"u8.ToArray(), result);
    }

    [Test]
    public void OverwriteExistingKey()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new());
        cache.Set("key1", "world"u8.ToArray(), new());

        var result = cache.Get("key1");
        AreEqual("world"u8.ToArray(), result);
    }

    [Test]
    public void AbsoluteExpiration_DateTimeOffset()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path);

        cache.Set("key1", "hello"u8.ToArray(), new()
        {
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddMilliseconds(1)
        });

        Thread.Sleep(50);

        var result = cache.Get("key1");
        IsNull(result);
    }

    [Test]
    public void SlidingExpiration_CappedByAbsolute()
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
        IsNull(result);
    }

    [Test]
    public void AbsoluteExpiration_CleansUpFiles()
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
        AreEqual(0, datFiles.Length);
        AreEqual(0, metaFiles.Length);
    }

    [Test]
    public async Task RemoveAsync_DeletesEntry()
    {
        var path = CachePath();
        await using var cache = new ReplicantDistributedCache(path);

        await cache.SetAsync("key1", "hello"u8.ToArray(), new());
        await cache.RemoveAsync("key1");

        var result = await cache.GetAsync("key1");
        IsNull(result);
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
        AreEqual("hello"u8.ToArray(), result);
    }

    [Test]
    public void PurgeOld_EnforcesMaxEntries()
    {
        var path = CachePath();
        using var cache = new ReplicantDistributedCache(path, maxEntries: 100);

        for (var i = 0; i < 150; i++)
        {
            cache.Set($"key{i}", "hello"u8.ToArray(), new());
        }

        cache.PurgeOld();

        var datFiles = Directory.GetFiles(path, "*.dat");
        IsTrue(datFiles.Length <= 100);
    }

    [Test]
    public void Constructor_NullDirectory_Throws() =>
        Assert.Throws<ArgumentNullException>(
            () => new ReplicantDistributedCache(null!));

    [Test]
    public void Constructor_EmptyDirectory_Throws() =>
        Assert.Throws<ArgumentNullException>(
            () => new ReplicantDistributedCache(""));

    [Test]
    public void Constructor_MaxEntriesTooLow_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReplicantDistributedCache(CachePath(), maxEntries: 50));

    [Test]
    public void DuplicateDirectory_Throws()
    {
        var path = CachePath();
        using var cache1 = new ReplicantDistributedCache(path);

        Assert.Throws<InvalidOperationException>(
            () => new ReplicantDistributedCache(path));
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
    public void DependencyInjection()
    {
        var path = CachePath();
        var services = new ServiceCollection();
        services.AddReplicantDistributedCache(path);

        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IDistributedCache>();

        IsInstanceOf<ReplicantDistributedCache>(cache);
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
        AreEqual("hello", value);

        // Second call served from cache
        var value2 = await cache.GetOrCreateAsync("key1", async _ => "world");
        AreEqual("hello", value2);
    }
}
