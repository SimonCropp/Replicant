# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Replicant is a .NET HttpClient wrapper that caches HTTP responses to disk. It respects standard HTTP cache headers (Cache-Control, ETag, Last-Modified, Expires) and manages cached entries with automatic cleanup based on last-access times.

## Build and Test Commands

```bash
# Build
dotnet build src --configuration Release

# Run all tests
dotnet test src --configuration Release

# Run a single test
dotnet test src --configuration Release --filter "FullyQualifiedName~Tests.HttpCacheTests.MethodName"
```

Tests target net10.0 only. The library targets net48, net7.0, net8.0, net9.0, and net10.0.

## Build Settings

- `TreatWarningsAsErrors: true` — the build fails on any warning
- `EnforceCodeStyleInBuild: true` — code style violations are build errors
- `LangVersion: preview` — latest C# preview features are available
- Strong-name signed (key.snk)
- Centralized package versions in `src/Directory.Packages.props`

## Architecture

**Single namespace:** All library code is in the `Replicant` namespace under `src/Replicant/`.

**Partial class pattern:** `HttpCache` is split across multiple files by concern:
- `HttpCache.cs` — constructor, core download logic, `DownloadAsync`
- `HttpCache_AddItem.cs`, `HttpCache_Bytes.cs`, `HttpCache_String.cs`, `HttpCache_Stream.cs`, `HttpCache_File.cs`, `HttpCache_Response.cs`, `HttpCache_Lines.cs` — public API methods
- `HttpCache_Cleanup.cs` — timer-based background purge (every 10 minutes)
- `HttpCache_Dispose.cs` — resource cleanup

**Key types:**
- `IHttpCache` — public interface with async/sync dual APIs
- `HttpCache` — main implementation; also exposes a static `HttpCache.Default` singleton
- `Result` — wraps a download outcome (file reference or HttpResponseMessage) with cache state flags
- `FilePair` — manages paired `{content.bin, metadata.json}` cache files
- `Timestamp` — parses/generates cache filenames: `{sha1hash}_{timestamp}_{etag}.bin`
- `MetaData` — JSON-serialized HTTP headers stored alongside cached content
- `DeriveCacheStatus` — decision logic for cache behavior based on HTTP headers

**Cache storage format:** Each cached response is two files in the cache directory — a `.bin` content file and a `.json` metadata file. File last-write-time encodes expiry; last-access-time tracks staleness for cleanup.

## Testing

- **Framework:** NUnit with Verify (snapshot testing)
- **Snapshot files:** `*.verified.txt` files in `src/Tests/` — these are committed and compared on test runs
- **On test failure:** `*.received.*` files are generated showing actual output. Compare against the corresponding `.verified.txt` to see differences. Accept new snapshots by replacing `.verified.txt` with `.received.txt`.
- Tests are marked `[NonParallelizable]` at assembly level to avoid cache directory conflicts
- `MockHttpClient.cs` provides a mock for unit tests; `IntegrationTests.cs` hits httpbin.org

## Documentation

The readme is auto-generated from code snippets via MarkdownSnippets.MsBuild. Test methods with `#region` markers (e.g., `#region Construction`) are extracted into `readme.md`. Edit snippets in test code, not directly in the readme.
