namespace Replicant;

/// <summary>
/// A shared disk cache that can be used by multiple <see cref="ReplicantHandler"/> instances.
/// Register as a singleton to share a single cache (and purge timer) across handlers.
/// </summary>
public class ReplicantCache :
    IDisposable,
    IAsyncDisposable
{
    internal CacheStore Store { get; }

    /// <summary>
    /// Instantiate a new instance of <see cref="ReplicantCache"/>.
    /// </summary>
    /// <param name="directory">The directory to store the cache files.</param>
    /// <param name="maxEntries">The maximum entries to store in the cache.</param>
    public ReplicantCache(string directory, int maxEntries = 1000) =>
        Store = new(directory, maxEntries);

    /// <summary>
    /// Purge all cached items.
    /// </summary>
    public void Purge() => Store.Purge();

    /// <summary>
    /// Purge cached items that exceed max entries, ordered by last access time.
    /// </summary>
    public void PurgeOld() => Store.PurgeOld();

    /// <inheritdoc/>
    public void Dispose() => Store.Dispose();

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => Store.DisposeAsync();
}
