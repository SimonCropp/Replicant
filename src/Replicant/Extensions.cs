using System.Collections.Generic;
using System.Net.Http.Headers;

static class Extensions
{

    public static void AddRange(this HttpHeaders to, IEnumerable<KeyValuePair<string, IEnumerable<string>>> from)
    {
        foreach (var header in from)
        {
            to.Add(header.Key, header.Value);
        }
    }
}