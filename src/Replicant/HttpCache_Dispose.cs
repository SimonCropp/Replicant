namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual void Dispose()
    {
        if (clientIsOwned)
        {
            client!.Dispose();
        }

        store.Dispose();
    }

    /// <inheritdoc/>
    public virtual ValueTask DisposeAsync()
    {
        if (clientIsOwned)
        {
            client!.Dispose();
        }

        return store.DisposeAsync();
    }
}
