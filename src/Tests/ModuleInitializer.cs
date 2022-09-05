public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifyHttp.Enable();
        VerifierSettings.AddExtraSettings(_ =>
        {
            _.Converters.Add(new ResultConverter());
            _.Converters.Add(new MetaDataConverter());
            _.Converters.Add(new TimestampConverter());
        });
#if NET5_0 || NET6_0_OR_GREATER
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

//Only required if using a legacy version of .net
#if(!NET5_0 && !NET6_0_OR_GREATER)
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class ModuleInitializerAttribute :
        Attribute
    {
    }
}
#endif