namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual void Dispose()
    {
        invoker?.Dispose();

        if (handlerIsOwned)
        {
            handler!.Dispose();
        }

        timer.Dispose();
    }

    /// <inheritdoc/>
    public virtual ValueTask DisposeAsync()
    {
        invoker?.Dispose();

        if (handlerIsOwned)
        {
            handler!.Dispose();
        }
#if NET7_0_OR_GREATER
        return timer.DisposeAsync();
#else
        timer.Dispose();
        return default;
#endif
    }
}