using System.Collections.Generic;

class MetaData
{
    public IEnumerable<KeyValuePair<string, IEnumerable<string>>> ResponseHeaders { get; }
    public IEnumerable<KeyValuePair<string, IEnumerable<string>>> ContentHeaders { get; }
#if NET5_0_OR_GREATER
    public IEnumerable<KeyValuePair<string, IEnumerable<string>>> TrailingHeaders { get; }
#endif

    public MetaData(
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> responseHeaders,
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> contentHeaders
#if NET5_0_OR_GREATER
        ,IEnumerable<KeyValuePair<string, IEnumerable<string>>> trailingHeaders
#endif
        )
    {
        ResponseHeaders = responseHeaders;
        ContentHeaders = contentHeaders;
#if NET5_0_OR_GREATER
        TrailingHeaders = trailingHeaders;
#endif
    }
}