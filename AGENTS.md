# AGENTS.md

This file provides guidance to coding agents such as Codex and Claude Code when working with code in this repository.

## Project Overview

This is `ytx`, a .NET Global Tool that extracts YouTube video metadata and transcripts as structured JSON. It's packaged as `solrevdev.ytx` on NuGet and targets .NET 8.0, 9.0, and 10.0.

## Core Architecture

**Main Components:**
- `src/Ytx/Program.cs` - Single-file console application with async main method
- `Input` record - Simple DTO for JSON input parsing
- `Output` class - JSON output contract (1.1.0): canonical url, videoId, metadata, caption status and track, transcriptRaw, transcript. Property order is the field order; README documents each field
- Uses YoutubeExplode library for YouTube API interactions and caption extraction

**Data Flow:**
1. Input validation (command-line args or JSON via stdin)
2. YouTube video data extraction via YoutubeExplode
3. Caption track discovery and selection (prefers English, falls back to any available)
4. Transcript formatting (raw text + markdown with timestamped links) and caption status
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

# The publishing workflow bumps the package version automatically.
# Do not edit <Version> for an ordinary patch release.
```

## CI/CD Integration

**GitHub Actions Workflow** (`.github/workflows/publish.yml`):
- Pull requests that change the project or workflow run read-only restore, build, and pack validation
- Pushes to `master` automatically use a patch bump; manual dispatch can select patch, minor, or major
- Builds and packs before publishing the exact package to NuGet with `--skip-duplicate`
- Only after NuGet publication succeeds, atomically pushes the version commit and tag
- Creates retry-safe GitHub Releases with native `gh release create`
- Grants `contents: write` only to the publishing job and serializes releases with concurrency controls

**Manual Release:** GitHub Actions → "Publish NuGet (ytx)" → "Run workflow"

## Important Implementation Details

**Caption Detection Logic:** `SelectTrack` ranks tracks by exact language code, then exact name, then code prefix (`en` matches `en-GB`), then name substring (only for preferences longer than 3 characters, because "fr" is inside "Afrikaans"). Ties prefer a manual track over an auto-generated one. Unknown languages fall back to English, then any track.

**Error Handling:** Returns specific exit codes (0=success, 1=unexpected error, 2=usage error, 3=metadata written but captions blocked or failed, 130=Ctrl+C) for scriptable integration. Caption failures set `captionStatus` and `captionError`; only YouTube and HTTP exceptions are caught there, so bugs still fail with exit 1.

**JSON Input/Output:** Supports both command-line arguments and JSON via stdin. Output uses `UnsafeRelaxedJsonEscaping` for proper Unicode handling in video descriptions.

**NuGet Packaging:** The root `README.md` is packaged into the NuGet package via `<PackageReadmeFile>` and `<None Include>` configuration. The `<PackageOutputPath>` is set to `../../nupkg` for consistent build artifacts.

## Key Dependencies

- `YoutubeExplode` 6.5.6 - Core YouTube data extraction
- .NET 8.0/9.0/10.0 target frameworks with nullable reference types enabled
- System.Text.Json for serialization
- System.Text.RegularExpressions for caption text normalization

## Repository Conventions

### Git commits

Use Conventional Commits unless explicitly instructed otherwise:

- Use a type such as `feat:`, `fix:`, `docs:`, `chore:`, `refactor:`, `test:`, `ci:`, `build:`, `perf:`, or `style:`.
- Keep the subject concise and imperative, for example `fix: handle missing captions`.
- Add a scope when useful, for example `ci(release): serialize NuGet publication`.
- Mark breaking changes with `!` and/or a `BREAKING CHANGE:` footer.

### Local .NET workload recovery

On this macOS machine, a Homebrew `dotnet-sdk` upgrade can leave a stale workload-set catalog. If a `dotnet` command reports that a workload-set version has missing manifests, do not run `dotnet workload repair`; it does not repair the catalog when no workloads are installed.

Ask the user to run this command interactively because `/usr/local/share/dotnet` is root-owned:

```bash
sudo dotnet workload update --source https://api.nuget.org/v3/index.json
```

Then verify with `dotnet --info`, `dotnet workload list`, and the original failing build or test command.
