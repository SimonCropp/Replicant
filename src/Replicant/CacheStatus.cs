enum CacheStatus
{
    Hit,
    Stored,
    NoStore,
    Revalidate,
    UseStaleDueToError,
    // Non-success response that is returned to the caller rather than thrown
    Error
}