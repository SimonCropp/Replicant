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
        writer.WriteStartObject();
        writer.WritePropertyName("FromDisk");
        writer.WriteValue(result.FromDisk);
        writer.WritePropertyName("Stored");
        writer.WriteValue(result.Stored);
        writer.WritePropertyName("Revalidated");
        writer.WriteValue(result.Revalidated);
        writer.WritePropertyName("Response");
        using var message = result.AsResponseMessage();
        serializer.Serialize(writer, message);
        writer.WriteEndObject();
    }
}