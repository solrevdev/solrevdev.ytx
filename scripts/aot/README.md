# NativeAOT and deployment-shape scripts

These scripts publish ytx in several deployment shapes and measure their size, startup time, CPU
time and memory. They produced the numbers in
[`docs/investigations/2026-10-native-aot-and-allocations.md`](../../docs/investigations/2026-10-native-aot-and-allocations.md).

They need bash, the .NET 10 SDK and Python 3. The benchmark scripts run on macOS and Linux (they use
`os.wait4`). `smoke-test.sh` also runs in Git Bash on Windows, which is how CI uses it.

| Script | What it does |
|---|---|
| `publish-variants.sh` | Publishes ytx as framework-dependent, ReadyToRun, trimmed self-contained and NativeAOT builds |
| `run-bench.sh` | Times every published binary: startup paths, real network calls, or `/usr/bin/time` resource use |
| `bench_rr.py` | Round-robin process timer used by `run-bench.sh` |
| `smoke-test.sh` | Offline checks for one binary: `--version`, `--help` and usage-error exit codes |

All output goes to `artifacts/aot/` by default, which `.gitignore` already excludes.

## Publish the variants

```bash
scripts/aot/publish-variants.sh                 # all variants for this machine's RID
scripts/aot/publish-variants.sh aot fdd         # just these two
RID=osx-x64 scripts/aot/publish-variants.sh aot # cross-architecture (same OS only)
```

| Variant | Meaning |
|---|---|
| `fdd` | Framework-dependent. Needs a .NET runtime. What the NuGet tool ships today. |
| `fdd-r2r` | Framework-dependent with ReadyToRun precompiled code |
| `sc-trim` | Self-contained single file, trimmed, invariant globalisation |
| `sc-trim-r2r` | As above plus ReadyToRun, uncompressed |
| `aot` | NativeAOT |
| `aot-size` | NativeAOT optimised for size, without stack trace data or resource strings |

Settings: `REPO` (source tree, default the git root), `OUT` (default `$REPO/artifacts/aot`),
`RID` (default this machine), `TFM` (default `net10.0`) and `PREFIX` (label prefix).

To compare against a released version, publish it from a worktree into the same `OUT` with a prefix:

```bash
git worktree add ../ytx-v1.0.7 v1.0.7
REPO=../ytx-v1.0.7 OUT=$PWD/artifacts/aot PREFIX=v1.0.7- scripts/aot/publish-variants.sh fdd
```

On v1.0.7 the `aot` and `aot-size` variants publish with four IL2026/IL3050 warnings and the binary
fails at run time on any JSON path. That is the reflection-based System.Text.Json problem the
write-up describes. A source-generated `JsonSerializerContext` fixes it.

## Measure

```bash
scripts/aot/run-bench.sh startup   # no network: --help, --version, stdin JSON error path (30 rounds)
scripts/aot/run-bench.sh network   # real YouTube calls: --metadata-only and a full run (8 rounds)
scripts/aot/run-bench.sh rusage    # /usr/bin/time on one --metadata-only call per binary
```

Settings: `OUT`, `VIDEO` (default `aCi6BhrvjhM`), `ROUNDS` (network mode, default 8) and `EXTRA`
for binaries outside `OUT`, for example the installed tool shim:

```bash
EXTRA="tool-shim=$HOME/.dotnet/tools/ytx" scripts/aot/run-bench.sh startup
```

How to read the results:

- Every binary runs once per round, in turn, so network drift affects them all equally.
- Compare **medians** and **CPU time**. Network runs vary by seconds, and one slow call skews the mean.
- `peakRSS` is the largest resident set size seen across all rounds.
- An `exit=[-6]` (or 134) on the stdin path means the binary aborted. On an AOT build that is the
  reflection-disabled JSON error.

## Smoke test one binary

```bash
scripts/aot/smoke-test.sh artifacts/aot/aot/Ytx 1.0.7
```

It needs no network. It checks that `--version` prints the expected version, `--help` prints usage,
and that bad input exits with code 2, including through the stdin JSON path. `native.yml` runs it
on every platform.
