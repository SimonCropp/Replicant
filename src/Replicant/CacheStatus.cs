namespace Replicant
{
    public enum CacheStatus
    {
        Hit,
        Stored,
        NoCache,
        Revalidate,
        UseStaleDueToError
    }
}