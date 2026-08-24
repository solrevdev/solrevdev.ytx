# ytx — YouTube → JSON (title, description, transcript)

A .NET global tool that extracts YouTube video metadata and transcripts as JSON.

```json
{
  "url": "https://youtube.com/...",
  "title": "Video title",
  "description": "Video description...",
  "transcriptRaw": "Full transcript as one string...",
  "transcript": "- [00:03](https://www.youtube.com/watch?v=...&t=3s) First line\n- [00:07](...) Next line"
}
```

## Features

- 🎯 **Single JSON output** — Clean, structured data for easy parsing
- 🌍 **Captions-aware** — Prefers English captions, falls back to any available
- 📝 **Markdown transcript** — Human-readable format with timestamped links
- 🚀 **Cross-platform** — Works on macOS, Windows, Linux (.NET 8/9/10)
- ⚡ **Fast & lightweight** — No dependencies beyond .NET and YoutubeExplode

## Quick Start

### Install from NuGet

```bash
dotnet tool install -g solrevdev.ytx
```

### Usage

```bash
# Existing/primary usage: the URL does not require a switch
ytx "https://www.youtube.com/watch?v=dQw4w9WgXcQ"

# Via JSON input
echo '{"url":"https://www.youtube.com/watch?v=dQw4w9WgXcQ"}' | ytx

# Save to file
ytx "https://www.youtube.com/watch?v=dQw4w9WgXcQ" > video-data.json

# Show command help or the installed package version
ytx --help
ytx --version
```

The positional URL and piped JSON forms are the original interfaces and remain supported. `--url` is available when an explicit option is more convenient.

### Options

```text
-u, --url <value>         Specify the YouTube URL or video ID explicitly
-l, --language <value>    Prefer captions matching a language name or code
                          (default: English)
    --metadata-only       Skip caption retrieval and return metadata only
-c, --compact             Write compact JSON instead of indented JSON
-h, -?, --help            Show help and exit
-v, --version             Show version and exit
```

Examples:

```bash
# A bare 11-character YouTube video ID is also accepted
ytx dQw4w9WgXcQ

# Prefer French captions, falling back to another available caption track
ytx --language fr "https://www.youtube.com/watch?v=dQw4w9WgXcQ"

# Retrieve only the title and description and emit one-line JSON
ytx --metadata-only --compact "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
```

### Upgrade

```bash
dotnet tool update -g solrevdev.ytx
```

## Output Format

| Field | Description |
|-------|-------------|
| `url` | Input YouTube URL (echoed back) |
| `title` | Video title |
| `description` | Full video description |
| `transcriptRaw` | All caption text as a single normalized string |
| `transcript` | Markdown list with `[HH:MM:SS](link) text` format |

> **Note**: If captions are unavailable (private videos, disabled captions, etc.), `transcriptRaw` will be empty and `transcript` will explain why.

## Development

### Local Build & Test

```bash
# Clone the repository
git clone https://github.com/solrevdev/solrevdev.ytx.git
cd solrevdev.ytx

# Restore dependencies
dotnet restore src/Ytx

# Build
dotnet build src/Ytx -c Release

# Run the unit tests
dotnet test tests/Ytx.Tests -c Release

# Test locally (choose an installed target framework)
dotnet run --project src/Ytx --framework net10.0 -- "YOUR_YOUTUBE_URL"
dotnet run --project src/Ytx --framework net10.0 -- --help

# Pack for local installation
dotnet pack src/Ytx -c Release
dotnet tool install -g solrevdev.ytx --add-source ./nupkg
```

### Project Structure

```
├── src/Ytx/Program.cs              # CLI and extraction implementation
├── src/Ytx/Ytx.csproj              # Package and target-framework metadata
├── tests/Ytx.Tests/                 # CLI parsing and formatting unit tests
├── .github/workflows/publish.yml   # Validation and release automation
├── docs/                            # Additional project documentation
└── README.md                        # User and maintainer documentation
```

## CI/CD

This project uses `.github/workflows/publish.yml` for validation and publishing:

- Pull requests that change the source, tests, or workflow restore, build, test, and pack the project without publishing.
- A push to `master` that changes the source or publishing workflow automatically performs a **patch** release.
- A manual workflow run can instead select a patch, minor, or major release.
- The publishing job reads the current `<Version>` from `Ytx.csproj`, calculates the next version, builds and tests it, packs that version, and publishes it to NuGet with a short-lived key from NuGet Trusted Publishing.
- Only after NuGet accepts the package does the job commit the new `<Version>` to `master`, create the matching `vX.Y.Z` tag, and create a GitHub Release.
- Publishing is serialized, checks that `master` has not moved, and uses `--skip-duplicate` plus release-state checks so a failed run can be retried safely.

Do not manually edit `<Version>` for an ordinary patch release. Merge or push the source change to `master` and let the workflow bump it exactly once. A version should only be edited deliberately when changing the release process itself.

### Manual Release

Go to Actions → "Publish NuGet (ytx)" → "Run workflow" and choose your version bump type.

### Maintainer release checklist

1. Test, build, and pack locally with `dotnet test tests/Ytx.Tests -c Release`, `dotnet build src/Ytx -c Release`, and `dotnet pack src/Ytx -c Release`.
2. Open a pull request for validation, or push an approved change to `master` for an automatic patch release.
3. Watch the "Publish NuGet (ytx)" workflow. A successful run publishes NuGet first, then pushes the version commit and tag.
4. Confirm the new version on NuGet or with `dotnet tool update -g solrevdev.ytx`, then run `ytx --version`.

The GitHub Actions workflow uses NuGet Trusted Publishing and does not need a stored API key. If publication fails, fix the cause and rerun the same workflow; do not manually bump the project version before retrying.

## Exit Codes

- `0` — Success
- `1` — Unexpected error (network, permissions, invalid video, etc.)
- `2` — Usage error (missing or invalid URL)

## Known Limitations

- Private, age-restricted, or region-blocked videos may not provide transcripts
- Videos with disabled captions will show "No transcript available"
- Timestamps are based on caption segment start times
- Large transcripts are not truncated (full output provided)

## Dependencies

- [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) — YouTube video data extraction
- .NET 8.0+ SDK (for development)

## License

MIT License - see [LICENSE](LICENSE) for details.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

Issues and feature requests are welcome!

---

**Made with ❤️ by [@solrevdev](https://github.com/solrevdev)**
