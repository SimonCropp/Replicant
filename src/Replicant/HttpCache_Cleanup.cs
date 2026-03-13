namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual void Purge() => store.Purge();

    /// <inheritdoc/>
    public virtual void PurgeOld() => store.PurgeOld();
}
