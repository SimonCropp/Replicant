public class IntegrationTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task ReadFromJsonAsync()
    {
        HttpCache.Default.Purge();
        var time1 = Stopwatch.StartNew();
        var result1 = await GetResult();
        outputHelper.WriteLine($"First: {time1.ElapsedMilliseconds}ms");
        Assert.NotNull(result1);
        var time2 = Stopwatch.StartNew();
        var result2 = await GetResult();
        outputHelper.WriteLine($"Second: {time2.ElapsedMilliseconds}ms");
        Assert.NotNull(result2);
        await Verify(new
        {
            result1,
            result2
        });
    }

    static async Task<Root?> GetResult()
    {
        using var response = await HttpCache.Default.ResponseAsync("https://httpbin.org/etag/theTag");
        return await response.Content.ReadFromJsonAsync<Root>();
    }

    public class Args;

    public class Root
    {
        public Args? Args { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
        public string? Origin { get; set; }
        public string? Url { get; set; }
    }
}