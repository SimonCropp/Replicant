# <img src="/src/icon.png" height="30px"> Replicant

[![Build status](https://ci.appveyor.com/api/projects/status/2t806jcx34s3r796/branch/main?svg=true)](https://ci.appveyor.com/project/SimonCropp/Replicant)
[![NuGet Status](https://img.shields.io/nuget/v/Replicant.svg)](https://www.nuget.org/packages/Replicant/)

A wrapper for HttpClient that caches to disk. Cached files, over the max specified, are deleted based on the last access times.

**See [Milestones](../../milestones?state=closed) for release notes.**

Headers/Responses respected in caching decisions:

 * [Expires](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Expires)
 * [Cache-Control max-age](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Cache-Control#expiration)
 * [Cache-Control no-store](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Cache-Control#cacheability)
 * [Cache-Control no-cache](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Cache-Control#cacheability)
 * [Last-Modified](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Last-Modified)
 * [ETag](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/ETag)


## NuGet package

https://nuget.org/packages/Replicant/


## Usage


### Default instance

There is a default static instance:

<!-- snippet: DefaultInstance -->
<a id='snippet-DefaultInstance'></a>
```cs
var content = await HttpCache.Default.DownloadAsync("https://httpbin.org/status/200");
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L94-L98' title='Snippet source file'>snippet source</a> | <a href='#snippet-DefaultInstance' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

This caches to `{Temp}/Replicant`.


### Construction

An instance of HttpCache should be long running.

<!-- snippet: Construction -->
<a id='snippet-Construction'></a>
```cs
var httpCache = new HttpCache(
    cacheDirectory,
    // omit for default new HttpClient()
    new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    },
    // omit for the default of 1000
    maxEntries: 10000);

// Dispose when finished
await httpCache.DisposeAsync();
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L36-L51' title='Snippet source file'>snippet source</a> | <a href='#snippet-Construction' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Dependency injection

Add HttpCache as a singleton when using dependency injection.

<!-- snippet: DependencyInjection -->
<a id='snippet-DependencyInjection'></a>
```cs
var services = new ServiceCollection();
services.AddSingleton(_ => new HttpCache(cachePath));

using var provider = services.BuildServiceProvider();
var httpCache = provider.GetRequiredService<HttpCache>();
ClassicAssert.NotNull(httpCache);
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L57-L66' title='Snippet source file'>snippet source</a> | <a href='#snippet-DependencyInjection' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Using HttpClient with [HttpClientFactory](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests).

<!-- snippet: DependencyInjectionWithHttpFactory -->
<a id='snippet-DependencyInjectionWithHttpFactory'></a>
```cs
ServiceCollection services = new();
services.AddHttpClient();
services.AddSingleton(
    _ =>
    {
        var clientFactory = _.GetRequiredService<IHttpClientFactory>();
        return new HttpCache(cachePath, () => clientFactory.CreateClient());
    });

using var provider = services.BuildServiceProvider();
var httpCache = provider.GetRequiredService<HttpCache>();
ClassicAssert.NotNull(httpCache);
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L72-L87' title='Snippet source file'>snippet source</a> | <a href='#snippet-DependencyInjectionWithHttpFactory' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Get a string

<!-- snippet: string -->
<a id='snippet-string'></a>
```cs
var content = await httpCache.StringAsync("https://httpbin.org/json");
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L260-L264' title='Snippet source file'>snippet source</a> | <a href='#snippet-string' title='Start of snippet'>anchor</a></sup>
<a id='snippet-string-1'></a>
```cs
var lines = new List<string>();
await foreach (var line in httpCache.LinesAsync("https://httpbin.org/json"))
{
    lines.Add(line);
}
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L272-L280' title='Snippet source file'>snippet source</a> | <a href='#snippet-string-1' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Get bytes

<!-- snippet: bytes -->
<a id='snippet-bytes'></a>
```cs
var bytes = await httpCache.BytesAsync("https://httpbin.org/json");
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L288-L292' title='Snippet source file'>snippet source</a> | <a href='#snippet-bytes' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Get a stream

<!-- snippet: stream -->
<a id='snippet-stream'></a>
```cs
using var stream = await httpCache.StreamAsync("https://httpbin.org/json");
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L300-L304' title='Snippet source file'>snippet source</a> | <a href='#snippet-stream' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Download to a file

<!-- snippet: ToFile -->
<a id='snippet-ToFile'></a>
```cs
await httpCache.ToFileAsync("https://httpbin.org/json", targetFile);
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L315-L319' title='Snippet source file'>snippet source</a> | <a href='#snippet-ToFile' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Download to a stream

<!-- snippet: ToStream -->
<a id='snippet-ToStream'></a>
```cs
await httpCache.ToStreamAsync("https://httpbin.org/json", targetStream);
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L334-L338' title='Snippet source file'>snippet source</a> | <a href='#snippet-ToStream' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Manually add an item to the cache

<!-- snippet: AddItem -->
<a id='snippet-AddItem'></a>
```cs
using var response = new HttpResponseMessage(HttpStatusCode.OK)
{
    Content = new StringContent("the content")
};
await httpCache.AddItemAsync(uri, response);
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L391-L399' title='Snippet source file'>snippet source</a> | <a href='#snippet-AddItem' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Use stale item on error

If an error occurs when re-validating a potentially stale item, then the cached item can be used as a fallback.

<!-- snippet: staleIfError -->
<a id='snippet-staleIfError'></a>
```cs
var content = httpCache.StringAsync(uri, staleIfError: true);
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L436-L440' title='Snippet source file'>snippet source</a> | <a href='#snippet-staleIfError' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Customizing HttpRequestMessage

The HttpRequestMessage used can be customized using a callback.

<!-- snippet: ModifyRequest -->
<a id='snippet-ModifyRequest'></a>
```cs
var content = await httpCache.StringAsync(
    uri,
    modifyRequest: message =>
    {
        message.Headers.Add("Key1", "Value1");
        message.Headers.Add("Key2", "Value2");
    });
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L348-L358' title='Snippet source file'>snippet source</a> | <a href='#snippet-ModifyRequest' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Full HttpResponseMessage

An instance of the HttpResponseMessage can be created from a cached item:

<!-- snippet: FullHttpResponseMessage -->
<a id='snippet-FullHttpResponseMessage'></a>
```cs
using var response = await httpCache.ResponseAsync("https://httpbin.org/status/200");
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L181-L185' title='Snippet source file'>snippet source</a> | <a href='#snippet-FullHttpResponseMessage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


## Influences / Alternatives

 * [Tavis.HttpCache](https://github.com/tavis-software/Tavis.HttpCache)
 * [CacheCow](https://github.com/aliostad/CacheCow)
 * [Monkey Cache](https://github.com/jamesmontemagno/monkey-cache)


## Icon

[Cyborg](https://thenounproject.com/term/cyborg/689871/) designed by [Symbolon](https://thenounproject.com/symbolon/) from [The Noun Project](https://thenounproject.com).
