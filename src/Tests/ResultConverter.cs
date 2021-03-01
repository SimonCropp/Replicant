using System.Collections.Generic;
using Newtonsoft.Json;
using Replicant;
using VerifyTests;

public class ResultConverter :
    WriteOnlyJsonConverter<Result>
{
    public override void WriteJson(
        JsonWriter writer,
        Result result,
        JsonSerializer serializer,
        IReadOnlyDictionary<string, object> context)
    {
        writer.WritePropertyName("Status");
        serializer.Serialize(writer, result.Status.ToString());
        writer.WritePropertyName("Response");
        using var message = result.AsResponseMessage();
        serializer.Serialize(writer, message);
    }
}