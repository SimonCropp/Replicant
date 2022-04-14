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

        timer.Dispose();
    }

    /// <inheritdoc/>
    public virtual ValueTask DisposeAsync()
    {
        if (clientIsOwned)
        {
            client!.Dispose();
        }
#if NET5_0
            return timer.DisposeAsync();
#else
        timer.Dispose();
        return default;
#endif
    }
}