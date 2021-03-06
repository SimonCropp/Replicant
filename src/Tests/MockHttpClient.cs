using System.Collections;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public class MockHttpClient :
    HttpClient
{
    IEnumerator responses;

    public MockHttpClient(params HttpResponseMessage[] responses)
    {
        this.responses = responses.GetEnumerator();
    }

    public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        responses.MoveNext();
        return Task.FromResult((HttpResponseMessage) responses.Current!);
    }
}