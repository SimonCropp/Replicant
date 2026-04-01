public class MockHttpClient :
    HttpClient
{
    IEnumerator responses;

    public MockHttpClient(params HttpResponseMessage[] responses) =>
        this.responses = responses.GetEnumerator();

    public List<HttpRequestMessage> Requests { get; } = [];

    public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, Cancel cancel)
    {
        Requests.Add(request);
        responses.MoveNext();
        return Task.FromResult((HttpResponseMessage) responses.Current!);
    }
}