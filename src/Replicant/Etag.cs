readonly struct Etag
{
    public bool IsEmpty { get; }
    readonly string forWeb;

    public string ForWeb
    {
        get
        {
            ThrowIfEmpty();
            return forWeb;
        }
    }

    public string ForFile { get; }

    internal static Etag Empty { get; } = new(string.Empty, string.Empty, true);

    internal Etag(string forWeb, string forFile, bool isEmpty)
    {
        this.forWeb = forWeb;
        IsEmpty = isEmpty;
        ForFile = forFile;
    }

    public static Etag FromFilePart(string value)
    {
        if (value == string.Empty)
        {
            return Empty;
        }

        var tag = value.Substring(1);
        if (value.StartsWith("W"))
        {
            return new($"W/\"{tag}\"", value, false);
        }

        return new($"\"{tag}\"", value, false);
    }

    public static Etag FromResponse(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("ETag", out var values))
        {
            return Empty;
        }

        var tag = values.First();

        return FromHeader(tag);
    }

    public static Etag FromHeader(string? tag)
    {
        if (tag == null)
        {
            return Empty;
        }

        if (tag.StartsWith("W/"))
        {
            return new(tag, $"W{tag.Substring(2).Trim('"')}", false);
        }

        return new(tag, $"S{tag.Trim('"')}", false);
    }

    void ThrowIfEmpty()
    {
        if (IsEmpty)
        {
            throw new("Cant use on empty Etag.");
        }
    }
}