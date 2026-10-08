# Changelog

All notable changes to ytx are listed here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/). GitHub Releases carry the full commit notes.

## [Unreleased]

### Added

- NuGet package icon, symbols package (`.snupkg`) and SourceLink metadata.
- Dependabot for GitHub Actions and NuGet, and a CodeQL workflow.

### Changed

- GitHub Actions are pinned to commit SHAs.
- Tests run on .NET 8, 9 and 10.

## [1.0.8] - 2026-10-08

### Changed

- JSON uses source generation and streams to stdout, which makes startup faster and allocates less.
- Transcript building no longer allocates for every caption.
- Trim and AOT analyzers run in every build.

## [1.0.7] - 2026-08-24

### Changed

- Releases use NuGet Trusted Publishing instead of a stored API key.

## [1.0.6] - 2026-07-16

### Added

- Tests for command-line parsing and formatting.

## [1.0.5] - 2026-07-16

### Added

- `--help`, `--version`, `--url`, `--language`, `--metadata-only` and `--compact`.

## [1.0.4] - 2026-07-16

### Changed

- Modernized the NuGet publishing workflow.

## [1.0.3] - 2025-11-17

### Added

- .NET 10 target.

### Changed

- YoutubeExplode 6.5.6.

## [1.0.1] - 2025-08-30

### Added

- First release: title, description, raw transcript and Markdown transcript as JSON.

[Unreleased]: https://github.com/solrevdev/solrevdev.ytx/compare/v1.0.8...HEAD
[1.0.8]: https://github.com/solrevdev/solrevdev.ytx/compare/v1.0.7...v1.0.8
[1.0.7]: https://github.com/solrevdev/solrevdev.ytx/compare/v1.0.6...v1.0.7
[1.0.6]: https://github.com/solrevdev/solrevdev.ytx/compare/v1.0.5...v1.0.6
[1.0.5]: https://github.com/solrevdev/solrevdev.ytx/compare/v1.0.4...v1.0.5
[1.0.4]: https://github.com/solrevdev/solrevdev.ytx/compare/v1.0.3...v1.0.4
[1.0.3]: https://github.com/solrevdev/solrevdev.ytx/compare/v1.0.1...v1.0.3
[1.0.1]: https://github.com/solrevdev/solrevdev.ytx/releases/tag/v1.0.1
