# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is `ytx`, a .NET Global Tool that extracts YouTube video metadata and transcripts as structured JSON. It's packaged as `solrevdev.ytx` on NuGet and targets both .NET 8.0 and 9.0 frameworks.

## Core Architecture

**Main Components:**
- `src/Ytx/Program.cs` - Single-file console application with async main method
- `Input` record - Simple DTO for JSON input parsing
- `Output` class - JSON output structure with 5 fields: url, title, description, transcriptRaw, transcript
- Uses YoutubeExplode library for YouTube API interactions and caption extraction

**Data Flow:**
1. Input validation (command-line args or JSON via stdin)
2. YouTube video data extraction via YoutubeExplode
3. Caption track discovery and selection (prefers English, falls back to any available)
4. Transcript formatting (raw text + markdown with timestamped links)
5. JSON serialization to stdout

## Development Commands

```bash
# Build and test locally
dotnet restore src/Ytx
dotnet build src/Ytx -c Release
dotnet run --project src/Ytx --framework net8.0 "YOUTUBE_URL"

# Package for local testing
dotnet pack src/Ytx -c Release
dotnet tool install -g solrevdev.ytx --add-source ./nupkg

# Manual version increment (triggers CI/CD)
# Edit src/Ytx/Ytx.csproj <Version> tag and commit to master
```

## CI/CD Integration

**GitHub Actions Workflow** (`.github/workflows/publish.yml`):
- Triggers on push to `master` branch or manual dispatch
- Auto-increments version in `src/Ytx/Ytx.csproj`
- Builds, packs, and publishes to NuGet using `NUGET_API_KEY` secret
- Creates GitHub releases with auto-generated notes
- Uses bash script for semantic version bumping (major/minor/patch)

**Manual Release:** GitHub Actions → "Publish NuGet (ytx)" → "Run workflow"

## Important Implementation Details

**Caption Detection Logic:** Orders available tracks by English language preference, then by auto-generated status. The updated YoutubeExplode v6.5.4 resolved transcript extraction issues that existed in earlier versions.

**Error Handling:** Returns specific exit codes (0=success, 1=unexpected error, 2=usage error) for scriptable integration.

**JSON Input/Output:** Supports both command-line arguments and JSON via stdin. Output uses `UnsafeRelaxedJsonEscaping` for proper Unicode handling in video descriptions.

**NuGet Packaging:** The root `README.md` is packaged into the NuGet package via `<PackageReadmeFile>` and `<None Include>` configuration. The `<PackageOutputPath>` is set to `../../nupkg` for consistent build artifacts.

## Key Dependencies

- `YoutubeExplode` 6.5.4 - Core YouTube data extraction
- .NET 8.0/9.0 target frameworks with nullable reference types enabled
- System.Text.Json for serialization
- System.Text.RegularExpressions for caption text normalization