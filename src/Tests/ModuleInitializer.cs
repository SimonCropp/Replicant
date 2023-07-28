[assembly: CollectionBehavior(DisableTestParallelization = true)]
public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifyHttp.Initialize();
        VerifierSettings.AddExtraSettings(_ =>
        {
            _.Converters.Add(new ResultConverter());
            _.Converters.Add(new MetaDataConverter());
            _.Converters.Add(new TimestampConverter());
        });
#if NET7_0_OR_GREATER
        VerifierSettings.IgnoreMember<HttpRequestException>(_ => _.StatusCode);
#endif
        VerifierSettings.IgnoreMember<Result>(_ => _.File);
        VerifierSettings.IgnoreMembers(
            "StackTrace",
            "Content-Length",
            "TrailingHeaders",
            "X-Amzn-Trace-Id",
            "Set-Cookie",
            "Report-To",
            "Connection",
            "Server-Timing",
            "Content-Type",
            "NEL",
            "Accept-Ranges",
            "Age",
            "Server",
            "X-Client-IP",
            "Strict-Transport-Security",
            "X-Cache-Status",
            "X-Cache",
            "origin");
    }
}