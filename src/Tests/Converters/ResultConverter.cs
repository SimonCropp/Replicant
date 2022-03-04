class ResultConverter :
    WriteOnlyJsonConverter<Result>
{
    public override void Write(VerifyJsonWriter writer, Result result)
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
        writer.Serializer.Serialize(writer, message);
        writer.WriteEndObject();
    }
}