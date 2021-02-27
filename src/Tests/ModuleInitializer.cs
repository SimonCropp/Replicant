using System.Runtime.CompilerServices;
using Replicant;
using VerifyTests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifierSettings.ModifySerialization(
            settings =>
            {
                settings.IgnoreMember<Result>(x => x.Path);
                settings.IgnoreMembers(
                    "Content-Length",
                    "X-Amzn-Trace-Id",
                    "Server",
                    "origin");
            });
    }
}