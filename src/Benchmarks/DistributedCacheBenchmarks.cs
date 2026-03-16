using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Caching.Distributed;
using Replicant;

[MemoryDiagnoser]
public class DistributedCacheBenchmarks
{
    string cacheDir = null!;
    ReplicantDistributedCache cache = null!;

    byte[] smallPayload = new byte[256];
    byte[] largePayload = new byte[64 * 1024];

    static readonly DistributedCacheEntryOptions options = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
    };

    static readonly DistributedCacheEntryOptions slidingOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(30)
    };

    [GlobalSetup]
    public void Setup()
    {
        cacheDir = Path.Combine(Path.GetTempPath(), "ReplicantBench_Distributed", Guid.NewGuid().ToString());

        Random.Shared.NextBytes(smallPayload);
        Random.Shared.NextBytes(largePayload);

        cache = new ReplicantDistributedCache(cacheDir);

        // Prime entries for hit benchmarks
        cache.Set("hit-small", smallPayload, options);
        cache.Set("hit-large", largePayload, options);
        cache.Set("hit-sliding", smallPayload, slidingOptions);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        cache.Dispose();

        if (Directory.Exists(cacheDir))
        {
            Directory.Delete(cacheDir, true);
        }
    }

    [Benchmark]
    public void Set_Small() =>
        cache.Set("bench-small", smallPayload, options);

    [Benchmark]
    public void Set_Large() =>
        cache.Set("bench-large", largePayload, options);

    [Benchmark]
    public Task SetAsync_Small() =>
        cache.SetAsync("bench-small-async", smallPayload, options);

    [Benchmark]
    public byte[]? Get_Hit() =>
        cache.Get("hit-small");

    [Benchmark]
    public byte[]? Get_Large_Hit() =>
        cache.Get("hit-large");

    [Benchmark]
    public Task<byte[]?> GetAsync_Hit() =>
        cache.GetAsync("hit-small");

    [Benchmark]
    public byte[]? Get_Miss() =>
        cache.Get("nonexistent");

    [Benchmark]
    public void Refresh_Sliding() =>
        cache.Refresh("hit-sliding");
}
