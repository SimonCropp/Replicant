using System.Collections.Generic;
using Newtonsoft.Json;
using VerifyTests;

class ResultConverter :
    WriteOnlyJsonConverter<Result>
{
    public override void WriteJson(
        JsonWriter writer,
        Result result,
        JsonSerializer serializer,
        IReadOnlyDictionary<string, object> context)
    {
        writer.WritePropertyName("FromDisk");
        serializer.Serialize(writer, result.FromDisk);
        writer.WritePropertyName("Stored");
        serializer.Serialize(writer, result.Stored);
        writer.WritePropertyName("Revalidated");
        serializer.Serialize(writer, result.Revalidated);
        writer.WritePropertyName("Response");
        using var message = result.AsResponseMessage();
        serializer.Serialize(writer, message);
    }
}