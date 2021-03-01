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
                settings.IgnoreMember<Result>(x => x.ContentPath);
                settings.IgnoreMembers(
                    "StackTrace",
                    "Content-Length",
                    "X-Amzn-Trace-Id",
                    "Set-Cookie",
                    "Report-To",
                    "Connection",
                    "Server-Timing",
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