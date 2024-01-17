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
<a id='snippet-defaultinstance'></a>
```cs
var content = await HttpCache.Default.DownloadAsync("https://httpbin.org/status/200");
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L81-L85' title='Snippet source file'>snippet source</a> | <a href='#snippet-defaultinstance' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

This caches to `{Temp}/Replicant`.


### Construction

An instance of HttpCache should be long running.

<!-- snippet: Construction -->
<a id='snippet-construction'></a>
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
<sup><a href='/src/Tests/HttpCacheTests.cs#L24-L39' title='Snippet source file'>snippet source</a> | <a href='#snippet-construction' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Dependency injection

Add HttpClient as a singleton when using dependency injection.

<!-- snippet: DependencyInjection -->
<a id='snippet-dependencyinjection'></a>
```cs
var services = new ServiceCollection();
services.AddSingleton(_ => new HttpCache(CachePath));

using var provider = services.BuildServiceProvider();
var httpCache = provider.GetRequiredService<HttpCache>();
Assert.NotNull(httpCache);
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L45-L54' title='Snippet source file'>snippet source</a> | <a href='#snippet-dependencyinjection' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Using HttpClient with [HttpClientFactory](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests).

<!-- snippet: DependencyInjectionWithHttpFactory -->
<a id='snippet-dependencyinjectionwithhttpfactory'></a>
```cs
ServiceCollection services = new();
services.AddHttpClient();
services.AddSingleton(
    _ =>
    {
        var clientFactory = _.GetRequiredService<IHttpClientFactory>();
        return new HttpCache(CachePath, () => clientFactory.CreateClient());
    });

using var provider = services.BuildServiceProvider();
var httpCache = provider.GetRequiredService<HttpCache>();
Assert.NotNull(httpCache);
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L60-L75' title='Snippet source file'>snippet source</a> | <a href='#snippet-dependencyinjectionwithhttpfactory' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Get a string

<!-- snippet: string -->
<a id='snippet-string'></a>
```cs
var content = await httpCache.StringAsync("https://httpbin.org/json");
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L234-L238' title='Snippet source file'>snippet source</a> | <a href='#snippet-string' title='Start of snippet'>anchor</a></sup>
<a id='snippet-string-1'></a>
```cs
var lines = new List<string>();
await foreach (var line in httpCache.LinesAsync("https://httpbin.org/json"))
{
    lines.Add(line);
}
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L246-L254' title='Snippet source file'>snippet source</a> | <a href='#snippet-string-1' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Get bytes

<!-- snippet: bytes -->
<a id='snippet-bytes'></a>
```cs
var bytes = await httpCache.BytesAsync("https://httpbin.org/json");
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L262-L266' title='Snippet source file'>snippet source</a> | <a href='#snippet-bytes' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Get a stream

<!-- snippet: stream -->
<a id='snippet-stream'></a>
```cs
using var stream = await httpCache.StreamAsync("https://httpbin.org/json");
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L274-L278' title='Snippet source file'>snippet source</a> | <a href='#snippet-stream' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Download to a file

<!-- snippet: ToFile -->
<a id='snippet-tofile'></a>
```cs
await httpCache.ToFileAsync("https://httpbin.org/json", targetFile);
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L289-L293' title='Snippet source file'>snippet source</a> | <a href='#snippet-tofile' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Download to a stream

<!-- snippet: ToStream -->
<a id='snippet-tostream'></a>
```cs
await httpCache.ToStreamAsync("https://httpbin.org/json", targetStream);
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L308-L312' title='Snippet source file'>snippet source</a> | <a href='#snippet-tostream' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Manually add an item to the cache

<!-- snippet: AddItem -->
<a id='snippet-additem'></a>
```cs
using var response = new HttpResponseMessage(HttpStatusCode.OK)
{
    Content = new StringContent("the content")
};
await httpCache.AddItemAsync(uri, response);
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L367-L375' title='Snippet source file'>snippet source</a> | <a href='#snippet-additem' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Use stale item on error

If an error occurs when re-validating a potentially stale item, then the cached item can be used as a fallback.

<!-- snippet: staleIfError -->
<a id='snippet-staleiferror'></a>
```cs
var content = httpCache.StringAsync(uri, staleIfError: true);
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L406-L410' title='Snippet source file'>snippet source</a> | <a href='#snippet-staleiferror' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Customizing HttpRequestMessage

The HttpRequestMessage used can be customized using a callback.

<!-- snippet: ModifyRequest -->
<a id='snippet-modifyrequest'></a>
```cs
var content = await httpCache.StringAsync(
    uri,
    modifyRequest: message =>
    {
        message.Headers.Add("Key1", "Value1");
        message.Headers.Add("Key2", "Value2");
    });
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L322-L332' title='Snippet source file'>snippet source</a> | <a href='#snippet-modifyrequest' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Full HttpResponseMessage

An instance of the HttpResponseMessage can be created from a cached item:

<!-- snippet: FullHttpResponseMessage -->
<a id='snippet-fullhttpresponsemessage'></a>
```cs
using var response = await httpCache.ResponseAsync("https://httpbin.org/status/200");
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L168-L172' title='Snippet source file'>snippet source</a> | <a href='#snippet-fullhttpresponsemessage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


## Influences / Alternatives

 * [Tavis.HttpCache](https://github.com/tavis-software/Tavis.HttpCache)
 * [CacheCow](https://github.com/aliostad/CacheCow)
 * [Monkey Cache](https://github.com/jamesmontemagno/monkey-cache)


## Icon

[Cyborg](https://thenounproject.com/term/cyborg/689871/) designed by [Symbolon](https://thenounproject.com/symbolon/) from [The Noun Project](https://thenounproject.com).
