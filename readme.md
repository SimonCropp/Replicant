# <img src="/src/icon.png" height="30px"> Replicant

[![Build status](https://ci.appveyor.com/api/projects/status/2t806jcx34s3r796/branch/master?svg=true)](https://ci.appveyor.com/project/SimonCropp/Replicant)
[![NuGet Status](https://img.shields.io/nuget/v/Replicant.svg)](https://www.nuget.org/packages/Replicant/)

A wrapper for HttpClient that caches to disk. Cached files, over the max specified, are deleted based on the last access times.


## NuGet package

https://nuget.org/packages/Replicant/


<!-- toc -->
## Contents

  * [Usage](#usage)
    * [Construction](#construction)
    * [Get a string](#get-a-string)
    * [Get bytes](#get-bytes)
    * [Get a stream](#get-a-stream)
    * [Download to a file](#download-to-a-file)
    * [Download to a stream](#download-to-a-stream)
    * [Manually add an item to the cache](#manually-add-an-item-to-the-cache)
    * [Use stale item on error](#use-stale-item-on-error)
    * [Customizing HttpRequestMessage](#customizing-httprequestmessage)
    * [Full HttpResponseMessage](#full-httpresponsemessage)
  * [Influences / Alternatives](#influences--alternatives)<!-- endToc -->


## Usage


### Construction

An instance of HttpCache should be long running. Usually added as a singleton when using dependency injection.

<!-- snippet: Construction -->
<a id='snippet-construction'></a>
```cs
HttpCache httpCache = new(
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


### Get a string

<!-- snippet: string -->
<a id='snippet-string'></a>
```cs
var content = await httpCache.String("https://httpbin.org/json");
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L162-L164' title='Snippet source file'>snippet source</a> | <a href='#snippet-string' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Get bytes

<!-- snippet: bytes -->
<a id='snippet-bytes'></a>
```cs
var bytes = await httpCache.Bytes("https://httpbin.org/json");
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L171-L173' title='Snippet source file'>snippet source</a> | <a href='#snippet-bytes' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Get a stream

<!-- snippet: stream -->
<a id='snippet-stream'></a>
```cs
await using var stream = await httpCache.Stream("https://httpbin.org/json");
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L180-L182' title='Snippet source file'>snippet source</a> | <a href='#snippet-stream' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Download to a file

<!-- snippet: ToFile -->
<a id='snippet-tofile'></a>
```cs
await httpCache.ToFile("https://httpbin.org/json", targetFile);
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L192-L194' title='Snippet source file'>snippet source</a> | <a href='#snippet-tofile' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Download to a stream

<!-- snippet: ToStream -->
<a id='snippet-tostream'></a>
```cs
await httpCache.ToStream("https://httpbin.org/json", targetStream);
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L207-L209' title='Snippet source file'>snippet source</a> | <a href='#snippet-tostream' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Manually add an item to the cache

<!-- snippet: AddItem -->
<a id='snippet-additem'></a>
```cs
using HttpResponseMessage response = new(HttpStatusCode.OK)
{
    Content = new StringContent("the content")
};
await httpCache.AddItem(uri, response);
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L263-L269' title='Snippet source file'>snippet source</a> | <a href='#snippet-additem' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Use stale item on error

If an error occurs re-validating a potentially stale item, then the fallback can be to use the cached item.

<!-- snippet: useStaleOnError -->
<a id='snippet-usestaleonerror'></a>
```cs
var content = httpCache.String(uri, useStaleOnError: true);
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L300-L302' title='Snippet source file'>snippet source</a> | <a href='#snippet-usestaleonerror' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Customizing HttpRequestMessage

The HttpRequestMessage used can be customized using a callback.

<!-- snippet: Callback -->
<a id='snippet-callback'></a>
```cs
var content = await httpCache.String(
    uri,
    messageCallback: message =>
    {
        message.Headers.Add("Key1", "Value1");
        message.Headers.Add("Key2", "Value2");
    });
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L218-L228' title='Snippet source file'>snippet source</a> | <a href='#snippet-callback' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Full HttpResponseMessage

An instance of the HttpResponseMessage can be created from a cached item:

<!-- snippet: FullHttpResponseMessage -->
<a id='snippet-fullhttpresponsemessage'></a>
```cs
var result = await httpCache.Download("https://httpbin.org/status/200");
using var httpResponseMessage = await result.AsResponseMessage();
```
<sup><a href='/src/Tests/HttpCacheTests.cs#L113-L117' title='Snippet source file'>snippet source</a> | <a href='#snippet-fullhttpresponsemessage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


## Influences / Alternatives

 * [Tavis.HttpCache](https://github.com/tavis-software/Tavis.HttpCache)
 * [CacheCow](https://github.com/aliostad/CacheCow)
 * [Monkey Cache](https://github.com/jamesmontemagno/monkey-cache)


## Icon

[Cyborg](https://thenounproject.com/term/cyborg/689871/) designed by [Symbolon](https://thenounproject.com/symbolon/) from [The Noun Project](https://thenounproject.com).
