public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifyHttp.Enable();
        VerifierSettings.AddExtraSettings(x =>
        {
            x.Converters.Add(new ResultConverter());
            x.Converters.Add(new TimestampConverter());
        });
#if NET5_0 || NET6_0_OR_GREATER
        VerifierSettings.IgnoreMember<HttpRequestException>(x => x.StatusCode);
#endif
        VerifierSettings.IgnoreMember<Result>(x => x.File);
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