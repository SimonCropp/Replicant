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
