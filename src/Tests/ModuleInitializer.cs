using System.Runtime.CompilerServices;
using VerifyTests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifierSettings.ModifySerialization(
            settings =>
            {
                settings.AddExtraSettings(x => x.Converters.Add(new ResultConverter()));
#if NET5_0
                settings.IgnoreMember<System.Net.Http.HttpRequestException>(x => x.StatusCode);
#endif
                settings.IgnoreMember<Result>(x => x.ContentPath);
                settings.IgnoreMembers(
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
            });
    }
}

//Only required if using a legacy version of .net
#if(!NET5_0)
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class ModuleInitializerAttribute :
        Attribute
    {
    }
}
#endif