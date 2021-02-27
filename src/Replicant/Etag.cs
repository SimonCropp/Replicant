using System.Linq;
using System.Net.Http;

readonly struct Etag
{
    readonly bool weak;
    public bool IsEmpty { get; }
    readonly string forWeb;

    public bool Weak
    {
        get
        {
            ThrowIfEmpty();
            return weak;
        }
    }

    public string ForWeb
    {
        get
        {
            ThrowIfEmpty();
            return forWeb;
        }
    }

    public string ForFile { get; }
    public static Etag Empty { get; } = new(string.Empty, string.Empty,true,true);

    Etag(string forWeb, string forFile, bool weak, bool isEmpty)
    {
        this.forWeb = forWeb;
        this.weak = weak;
        IsEmpty = isEmpty;
        ForFile = forFile;
    }

    public static Etag FromFile(string value)
    {
        if (value == string.Empty)
        {
            return Empty;
        }

        var tag = value.Substring(1);
        if (value.StartsWith("W"))
        {
            return new Etag($"W/\"{tag}\"",value, true, false);
        }

        return new Etag($"\"{tag}\"", tag, false, false);
    }

    public static Etag FromResponse(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("ETag", out var values))
        {
            return Empty;
        }

        var tag = values.First();

        if (tag.StartsWith("W/"))
        {
            return new Etag(tag, $"W{tag[2..].Trim('"')}", true, false);
        }

        return new Etag(tag, $"S{tag.Trim('"')}", false, false);
    }

    void ThrowIfEmpty()
    {
        if (IsEmpty)
        {
            throw new("Cant use on empty Etag.");
        }
    }
}