# Ytx.Benchmarks

BenchmarkDotNet benchmarks and a byte-for-byte verification harness for the part of ytx that runs
after the network: caption normalisation, transcript formatting, JSON serialisation and output.
The results are written up in
[`docs/investigations/2026-10-native-aot-and-allocations.md`](../../docs/investigations/2026-10-native-aot-and-allocations.md).

This project is standalone. `src/Ytx` and `tests/Ytx.Tests` do not reference it, the repository has
no solution file, and CI builds, tests and packs those two projects by path. So it never runs in
CI and never ends up in the NuGet package. It is also marked `IsPackable=false`.

## What is measured

Each benchmark subject is a copy of the code, so the numbers stay comparable as `src/Ytx` changes.

| Subject | File | Description |
|---|---|---|
| Baseline | `Subjects/Baseline.cs` | ytx v1.0.7 verbatim: `Regex.Replace` per caption, interpolated strings, serialise to a string, `Console.WriteLine` |
| Tier 1 | `Subjects/Tier1.cs` | Skip the regex for clean captions, format numbers into the `StringBuilder`, serialise straight to the stream. `tier1sg` uses a source-generated `JsonSerializerContext`. |
| Tier 2 | `Subjects/Tier2.cs` | Pooled buffers, hand-written digit formatting and a chunked `Utf8JsonWriter`. A measuring stick, not a recommendation. |

Benchmark classes (in `Benchmarks.cs`):

- `NormalizeBench`: `NormalizeCaption` over every cue of the largest dataset.
- `FormatBench`: building the raw and Markdown transcript strings.
- `EndToEndBench`: format, serialise and write to `Stream.Null`.

## Data

The repository contains **no real caption data**. YouTube captions belong to the video owners.

- `generate` writes two synthetic datasets to `data/synthetic/`. They have the same shape as real
  auto-generated captions: the same cue counts as the 17-minute and 5-hour videos used in the
  write-up, alternating text and `"\n"` filler cues, about 35 characters per text cue, and emoji and
  accents in the description. The words are nonsense, generated from a fixed seed.
- `capture` fetches a real video's metadata and caption track into `data/captured/`. It picks the
  track the same way v1.0.7 does.
- `data/` is gitignored. Do not commit captured files.

Benchmarks and `verify` create the synthetic data automatically if it is missing. Every `*.json`
under `data/` becomes a benchmark parameter.

## Run it

From this folder:

```bash
dotnet build -c Release

# Optional: write the synthetic datasets explicitly (benchmarks do this on first run)
dotnet run -c Release -- generate

# Optional: add real captions for local runs only
dotnet run -c Release -- capture aCi6BhrvjhM NYFGCESmikA
dotnet run -c Release -- capture -l fr "https://www.youtube.com/watch?v=VIDEO_ID"

# Check every variant produces exactly the bytes v1.0.7 would
dotnet run -c Release -- verify

# Run the benchmarks (BenchmarkDotNet filters and options pass through)
dotnet run -c Release -- --filter '*'
dotnet run -c Release -- --filter '*EndToEnd*' --job short

# One cold run in a fresh process, the way a CLI pays for it (JIT and serialiser warm-up included)
for v in baseline tier1 tier1sg tier2; do
  bin/Release/net10.0/Ytx.Benchmarks replay $v data/synthetic/long.json > /dev/null
done
```

`--data DIR` (or `YTX_BENCH_DATA`) points every command at another dataset folder. A folder given
that way is used as is: synthetic files are only generated in the default `data/` folder.

## Verify mode

`verify` runs every variant against Baseline for each dataset, with and without extra edge-case
captions (`&nbsp;`, NBSP, U+2028, U+0085, U+3000, emoji, an offset over 100 hours), in indented and
compact mode. It prints `IDENTICAL` or `DIFF` per check and exits non-zero on any difference.
It is the starting point for a golden-output test in `tests/Ytx.Tests`.

Windows note: v1.0.7 builds the Markdown transcript with `AppendLine`, so on Windows the lines are
separated by `\r\n`, and `Console.WriteLine` ends the output with `\r\n`. The variants here copy
that with `Environment.NewLine`. If ytx switches to a fixed `\n`, update Baseline's expectations.

## Results from October 2026

Apple M1, macOS 27.0.1, .NET SDK 10.0.401, runtime 10.0.12, BenchmarkDotNet 0.15.2, real captions.
See the write-up for the full table and the cold-run numbers.

| Method | Video | Mean | Allocated |
|---|---|---:|---:|
| Baseline_All | 5 h 15 min | 7,947 µs | 10,506,442 B |
| Tier1_All | 5 h 15 min | 3,974 µs | 4,469,011 B |
| Tier1SourceGen_All | 5 h 15 min | 3,923 µs | 4,469,010 B |
| Tier2_All | 5 h 15 min | 3,440 µs | 58,224 B |

The synthetic `long` dataset gives numbers within a few percent of these (Baseline_All 7.7 ms and
10.9 MB on a short job), so it is a fair offline stand-in.
