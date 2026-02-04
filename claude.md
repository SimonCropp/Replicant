# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Replicant is a .NET NuGet library providing disk-based HTTP response caching for HttpClient. It implements RFC 7234 HTTP caching semantics including Expires, Cache-Control (max-age, no-store, no-cache), Last-Modified, and ETag headers.

**Two usage patterns:**
- **HttpCache**: Direct API for caching HTTP responses (string, bytes, stream, file)
- **CachingHandler**: DelegatingHandler for transparent HttpClient pipeline integration

## Build Commands

```bash
# Build
dotnet build src --configuration Release

# Test
dotnet test src --configuration Release

# Build and test
dotnet build src --configuration Release && dotnet test src --configuration Release --no-build --no-restore
```

## Architecture

### Key Files
- `src/Replicant/HttpCache.cs` - Core caching class (split across partial files: HttpCache_*.cs)
- `src/Replicant/CachingHandler.cs` - DelegatingHandler for pipeline integration
- `src/Replicant/Timestamp.cs` - Cache entry metadata encoded in filename format: `{uriHash}_{date}_{etag}`
- `src/Replicant/FilePair.cs` - Manages paired content (.bin) + metadata (.json) files
- `src/Replicant/MetaData.cs` - JSON-serialized response headers for cache revalidation
- `src/Replicant/Extensions.cs` - HTTP header parsing helpers (GetExpiry, IsNoCache, etc.)

### Design Patterns
- **Partial classes**: HttpCache split across 10 files by concern (AddItem, Bytes, Cleanup, Dispose, File, Lines, Response, Stream, String)
- **Filename-based state**: Timestamp struct encodes expiry/ETag in filename, avoids separate metadata lookups
- **FilePair atomic operations**: Content and metadata managed together for consistency
- **Thread-safe purging**: Timer-based cleanup with atomic file moves for locked files

### Target Frameworks
- Library: `net48;net7.0;net8.0;net9.0;net10.0` (net48 Windows only)
- Tests: `net10.0`

## Testing

Tests use NUnit with Verify for snapshot testing. Code snippets in readme.md are auto-extracted from test files and verified on each push.

```bash
# Run all tests
dotnet test src

# Run a single test
dotnet test src --filter "FullyQualifiedName~TestMethodName"
```

## Code Style

- EditorConfig enforced with `TreatWarningsAsErrors=true` and `EnforceCodeStyleInBuild=true`
- LangVersion is `preview`
- Uses `CharSpan` alias for `System.ReadOnlySpan<System.Char>`
- Warnings CS1591, NU1608, NU1109 suppressed globally
