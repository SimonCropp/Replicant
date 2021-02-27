namespace Replicant
{
    public enum CacheStatus
    {
        Hit,
        Stored,
        Revalidate,
        UseStaleDueToError
    }
}