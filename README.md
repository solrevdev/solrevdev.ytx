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
# Basic usage
ytx "https://www.youtube.com/watch?v=dQw4w9WgXcQ"

# Via JSON input
echo '{"url":"https://www.youtube.com/watch?v=dQw4w9WgXcQ"}' | ytx

# Save to file
ytx "https://www.youtube.com/watch?v=dQw4w9WgXcQ" > video-data.json
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

# Test locally (supports net8.0, net9.0, or net10.0)
dotnet run --project src/Ytx --framework net10.0 "YOUR_YOUTUBE_URL"

# Pack for local installation
dotnet pack src/Ytx -c Release
dotnet tool install -g solrevdev.ytx --add-source ./nupkg
```

### Project Structure

```
├── src/Ytx/           # Main project source
├── .github/workflows/ # CI/CD automation
├── build/             # Build artifacts
├── data/              # Test data
├── docs/              # Documentation
├── tests/             # Unit tests
└── tools/             # Development tools
```

## CI/CD

This project uses GitHub Actions for automated publishing:

- **Triggers**: Push to `master` branch or manual workflow dispatch
- **Version bumping**: Automatically increments version (patch/minor/major)
- **NuGet publishing**: Publishes to nuget.org using `NUGET_API_KEY` secret
- **GitHub Releases**: Creates tagged releases with auto-generated notes

### Manual Release

Go to Actions → "Publish NuGet (ytx)" → "Run workflow" and choose your version bump type.

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