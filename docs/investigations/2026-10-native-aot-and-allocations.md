# NativeAOT and allocations in ytx

- **Date:** October 2026
- **Version studied:** ytx v1.0.7 (commit `07d0e12`)
- **Status:** exploratory, not yet decided
- **Tracking issue:** [#4](https://github.com/solrevdev/solrevdev.ytx/issues/4). Related review: [#3](https://github.com/solrevdev/solrevdev.ytx/issues/3).

## The questions

ytx is a small .NET global tool. It takes a YouTube URL, fetches the video's metadata and captions
with [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode), and prints one JSON document.
People run it by hand, but more and more it is run by agents and scripts, often many times in a row.

Two questions came up:

1. **Can ytx ship as a NativeAOT binary, and is it worth it?** A native binary needs no .NET runtime
   and should start faster.
2. **Is ytx wasting time or memory in its own code?** It builds large strings for long transcripts.
   Would cutting allocations make it noticeably faster?

## Short answers

- **NativeAOT works after one small code change.** The JSON code must use a source-generated
  `JsonSerializerContext`. With that, the AOT binary produced output byte-identical to v1.0.7.
- **AOT starts in about 9 ms instead of about 38 ms.** It uses about 6x less CPU per real call and
  half the peak memory. It needs no .NET runtime.
- **A real call is about 90% network.** One person running ytx once will not notice AOT. Agents and
  batch jobs that run it hundreds of times will, mostly through CPU and memory.
- **Cutting allocations is not worth chasing for speed.** ytx's own code is about 2% of a run's time
  and about 18% of its allocations. Two small, readable changes halve that cost. A full
  zero-allocation rewrite is measurable in a benchmark but invisible to users.

## Method

### Environment

| Item | Value |
|---|---|
| Machine | Apple M1, 8 cores, macOS 27.0.1 (build 26A434) |
| .NET SDKs | 10.0.401 and 8.0.425 |
| .NET runtime | 10.0.12 |
| YoutubeExplode | 6.5.6 (as shipped in v1.0.7). 6.6.2 was also checked for allocations. |
| BenchmarkDotNet | 0.15.2, default job, workstation GC |
| Linux | `mcr.microsoft.com/dotnet/sdk:10.0-noble-aot` and `ubuntu:24.04` in Docker on the same Mac (arm64) |

### Test videos

- **`aCi6BhrvjhM`:** 17 minutes, 924 caption cues. Used for most network runs.
- **`NYFGCESmikA`:** Lex Fridman #501, 5 hours 15 minutes, 16,284 caption cues, 1.1 MB of JSON output.
  Used to stress the formatting code.

Both are auto-generated caption tracks. About half of their cues are filler whose text is a single
newline, which matters for the normalisation results below.

### What was measured, and how

- **Deployment shapes.** ytx was published six ways for `net10.0` and `osx-arm64` (see the table in
  [Results](#results-net100-osx-arm64)). Each was timed as a separate process.
- **Startup.** 30 runs after 3 warm-up runs, with no network: `--help`, `--version`, and stdin JSON
  input that fails ID validation before any network call.
- **Network runs.** 8 rounds against `aCi6BhrvjhM`. Each round runs every build once, in turn, so
  network drift affects all builds equally. This was done twice.
- **Resource use.** `/usr/bin/time -l` on one `--metadata-only` call per build, for peak memory and
  instructions retired.
- **Phases.** An instrumented copy of v1.0.7 printed the time and bytes allocated in each phase
  (`GC.GetTotalAllocatedBytes`).
- **Micro-benchmarks.** BenchmarkDotNet on the formatting and output code, replaying captured
  captions so the network is excluded. Every variant was checked for byte-identical output.
- **Cold runs.** Each formatting variant also ran once per fresh process, 5 times, because a CLI
  pays JIT and warm-up costs that BenchmarkDotNet hides.

### Limits

- One machine. Windows and osx-x64 binaries were built but never run. linux-arm64 ran in Docker.
- Network numbers come from one home connection in the UK. Their spread is large. Compare medians and
  CPU time, not means.
- Nothing was committed or pushed while measuring.

## Part 1: NativeAOT

### What breaks today

Publishing v1.0.7 with `-p:PublishAot=true` succeeds, but with four warnings, all in `Program.cs`.
YoutubeExplode and AngleSharp produce none. That was checked with `TrimmerSingleWarn=false`, which
lists warnings per call site instead of one line per assembly. YoutubeExplode is marked `IsTrimmable`.

The build-time analyzer warnings, verbatim:

```text
src/Ytx/Program.cs(88,33): warning IL2026: Using member 'System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo or JsonSerializerContext, or make sure all of the required types are preserved. [src/Ytx/Ytx.csproj::TargetFramework=net10.0]
src/Ytx/Program.cs(88,33): warning IL3050: Using member 'System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling. JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use System.Text.Json source generation for native AOT applications. [src/Ytx/Ytx.csproj::TargetFramework=net10.0]
src/Ytx/Program.cs(176,24): warning IL2026: Using member 'System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo or JsonSerializerContext, or make sure all of the required types are preserved. [src/Ytx/Ytx.csproj::TargetFramework=net10.0]
src/Ytx/Program.cs(176,24): warning IL3050: Using member 'System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling. JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use System.Text.Json source generation for native AOT applications. [src/Ytx/Ytx.csproj::TargetFramework=net10.0]
```

The ILCompiler repeats them while generating native code:

```text
src/Ytx/Program.cs(88): Trim analysis warning IL2026: Program.<Main>d__1.MoveNext(): Using member 'System.Text.Json.JsonSerializer.Deserialize<Input>(String,JsonSerializerOptions)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. ...
src/Ytx/Program.cs(88): AOT analysis warning IL3050: Program.<Main>d__1.MoveNext(): Using member 'System.Text.Json.JsonSerializer.Deserialize<Input>(String,JsonSerializerOptions)' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling. ...
src/Ytx/Program.cs(176): Trim analysis warning IL2026: Program.<Main>d__1.MoveNext(): Using member 'System.Text.Json.JsonSerializer.Serialize<Output>(Output,JsonSerializerOptions)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. ...
src/Ytx/Program.cs(176): AOT analysis warning IL3050: Program.<Main>d__1.MoveNext(): Using member 'System.Text.Json.JsonSerializer.Serialize<Output>(Output,JsonSerializerOptions)' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling. ...
```

Line 88 reads JSON from stdin. Line 176 writes the output. The binary that comes out of this build
looks fine at first. `--help`, `--version` and argument errors all work. It even fetches from
YouTube. Then it fails when it writes the JSON:

```text
Error: Reflection-based serialization has been disabled for this application. Either use the source generator APIs or explicitly configure the 'JsonSerializerOptions.TypeInfoResolver' property.
```

For URL arguments, with or without `--metadata-only`, ytx catches this, prints it and exits 1,
after spending a full network round trip. (Issue #4 says `--metadata-only` exited 0. A re-check on
8 Oct 2026 with the same build exited 1 with nothing on stdout.)

The stdin JSON path is worse. It is outside the `try` block, so it crashes with an unhandled
exception and aborts (exit 134 on macOS):

```text
Unhandled exception. System.InvalidOperationException: Reflection-based serialization has been disabled for this application. Either use the source generator APIs or explicitly configure the 'JsonSerializerOptions.TypeInfoResolver' property.
   at System.Text.Json.ThrowHelper.ThrowInvalidOperationException_JsonSerializerIsReflectionDisabled() + 0x30
   at System.Text.Json.JsonSerializerOptions.ConfigureForJsonSerializer() + 0x10
```

This is why the CI smoke test pipes JSON into the binary. A test of `--version` alone would pass.

### The fix

The fix is about 10 lines. Add a `JsonSerializerContext` with `[JsonSerializable]` for `Input` and
`Output`, and pass its type info to the existing serialiser calls. After that change:

- There are no trim or AOT warnings.
- All 26 tests pass.
- Output is byte-identical to v1.0.7 for a full run, for stdin input, and for the error and
  exit-code paths.
- The ordinary, non-AOT build gets about 6 ms faster on the stdin path, because it skips
  reflection-based serialiser setup.

The change works on net8, net9 and net10. It is worth merging whatever happens with AOT. It is being
done separately, together with streaming the output and the Tier 1 changes from Part 2.

### Results (net10.0, osx-arm64)

**Build types:**

- **Framework-dependent** needs a .NET runtime installed. This is what the NuGet tool ships.
- **ReadyToRun (R2R)** adds precompiled code to cut JIT time at startup.
- **Self-contained, single file, trimmed** bundles a trimmed runtime into one file.
- **NativeAOT** compiles everything to one native executable with no JIT.

**Columns:** startup columns are means of 30 runs. Network columns are medians of 8 rounds, as wall
time / CPU time (user + sys). Peak RSS is the largest seen across all rounds.

| Build | Size (binary / tgz) | `--version` | JSON path | `--metadata-only` wall / CPU | Full run wall / CPU | Peak RSS (metadata / full) |
|---|---|---|---|---|---|---|
| Installed tool shim 1.0.7 | runtime + 1.3 MB | 38.0 ms | 55.8 ms | 1052 / 303 ms | 1497 / 416 ms | 111 / 132 MB |
| v1.0.7 net8, framework-dependent | runtime + 1.3 MB | 37.9 | 77.9 | 1049 / 355 | 1568 / 486 | 110 / 126 |
| v1.0.7 net10, framework-dependent | 1.3 / 0.5 MB | 37.8 | 55.6 | 1018 / 298 | 1709 / 400 | 111 / 132 |
| Framework-dependent + source-gen JSON | 1.3 / 0.5 MB | 37.7 | 49.9 | 1024 / 293 | 1385 / 389 | 111 / 131 |
| Framework-dependent + R2R | 3.5 / 1.2 MB | 35.7 | 47.3 | 1126 / 229 | 1439 / 323 | 109 / 127 |
| Self-contained, single file, trimmed | 18.6 / 7.7 MB | 51.9 | 107.6 | 1324 / 589 | 2046 / 742 | 78 / 90 |
| Self-contained, single file, trimmed + R2R | 35.1 / 13.7 MB | 27.4 | 33.7 | 908 / 142 | 1481 / 208 | 87 / 97 |
| **NativeAOT** | **10.3 / 4.6 MB** | **8.6** | **8.8** | **784 / 47** | **1408 / 73** | **53 / 61** |
| NativeAOT, size-optimised | 9.3 / 4.3 MB | 8.6 | 8.7 | 852 / 47 | 1307 / 105 | 53 / 61 |

All rows except the first three include the source-generated JSON fix.

`/usr/bin/time -l` on one `--metadata-only` call shows the same pattern:

| Build | Wall | User + sys CPU | Max RSS | Instructions retired | Peak memory footprint |
|---|---:|---:|---:|---:|---:|
| Framework-dependent (v1.0.7, net10) | 1.12 s | 0.35 s | 116 MB | 2.54 billion | 85 MB |
| Self-contained, trimmed + R2R | 1.17 s | 0.20 s | 85 MB | 1.17 billion | 64 MB |
| NativeAOT | 0.85 s | 0.04 s | 54 MB | 0.47 billion | 39 MB |

**Reading the results:**

- **AOT wins on every startup path.** `--help`, `--version` and bad input all take about 9 ms,
  against 38–56 ms for the shipped tool. Peak memory at startup is about 11 MB against 42 MB.
- **AOT uses about 6x less CPU on real calls.** 47–73 ms against 300–400 ms. That CPU is JIT
  compilation and type loading, which the runtime pays on every process start.
- **AOT halves peak memory.** 53–61 MB against 111–132 MB.
- **Wall time on real calls is mostly network.** On the AOT `--metadata-only` call, about 780 ms of
  wall time breaks down as about 9 ms of startup, about 47 ms of CPU, and network for the rest.
- **Avoid trimmed self-contained without R2R.** It was the worst build here. Trimming drops the
  framework's precompiled code, so everything is JIT-compiled at run time.
- **Skip the size-optimised AOT build.** It saves 1 MB. `UseSystemResourceKeys` replaces exception
  messages with resource keys, which would make ytx's `Error: {ex.Message}` output worse.
- **The net8 build is slower on the JSON path** (78 ms against 56 ms). Newer runtimes start
  System.Text.Json faster.

### Raw startup results

Means, medians and spread in milliseconds, 30 rounds after 3 warm-ups. Labels: `orig-*` is v1.0.7,
`fdd-srcgen` is framework-dependent with source-gen JSON, `sc-trim` is self-contained single-file
trimmed.

| Build | `--help` mean / med / sd | `--version` mean / med / sd | stdin JSON mean / med / sd | Peak RSS (`--version`) |
|---|---|---|---|---|
| tool-shim | 38.0 / 36.8 / 4.4 | 38.0 / 37.6 / 1.7 | 55.8 / 55.6 / 1.1 | 42.3 MB |
| orig-net8 | 38.1 / 37.4 / 2.1 | 37.9 / 37.7 / 0.7 | 77.9 / 77.5 / 1.6 | 38.5 MB |
| orig-net10 | 37.4 / 37.0 / 1.8 | 37.8 / 37.5 / 0.7 | 55.6 / 55.5 / 0.9 | 42.3 MB |
| fdd-srcgen | 37.5 / 37.2 / 1.8 | 37.7 / 37.5 / 0.9 | 49.9 / 49.5 / 1.6 | 42.4 MB |
| fdd-r2r | 35.6 / 35.5 / 1.0 | 35.7 / 35.6 / 0.5 | 47.3 / 47.1 / 1.2 | 42.1 MB |
| sc-trim | 47.2 / 46.7 / 1.6 | 51.9 / 51.6 / 1.0 | 107.6 / 106.6 / 2.5 | 20.0 MB |
| sc-trim-r2r | 27.6 / 27.2 / 1.9 | 27.4 / 27.1 / 1.0 | 33.7 / 33.6 / 0.8 | 36.1 MB |
| aot | 8.4 / 8.3 / 0.4 | 8.6 / 8.5 / 0.2 | 8.8 / 8.7 / 0.3 | 11.5 MB |
| aot-size | 8.6 / 8.3 / 1.0 | 8.6 / 8.6 / 0.3 | 8.7 / 8.7 / 0.2 | 11.3 MB |

All stdin runs exited 2, as expected. A later re-run of `scripts/aot/run-bench.sh startup` on the
same machine measured 7.3 ms for AOT and 33.4 ms for framework-dependent `--help`. Absolute numbers
move by a few milliseconds between sessions. The ratio does not.

### Raw network results

Two separate runs of 8 rounds each against `aCi6BhrvjhM`. Wall time in milliseconds.

**Run 2** (the one in the summary table, with CPU time):

| Build | `--metadata-only` med / mean / sd | CPU med | Full run med / mean / max | CPU med |
|---|---|---:|---|---:|
| tool-shim | 1052 / 1093 / 109 | 303 | 1497 / 1550 / 1911 | 416 |
| orig-net8 | 1049 / 1051 / 177 | 355 | 1568 / 2847 / 11340 | 486 |
| orig-net10 | 1018 / 1235 / 555 | 298 | 1709 / 1791 / 2287 | 400 |
| fdd-srcgen | 1024 / 1028 / 118 | 293 | 1385 / 1665 / 2967 | 389 |
| fdd-r2r | 1126 / 1066 / 141 | 229 | 1439 / 3900 / 21306 | 323 |
| sc-trim | 1324 / 1280 / 146 | 589 | 2046 / 3641 / 11910 | 742 |
| sc-trim-r2r | 908 / 985 / 206 | 142 | 1481 / 3800 / 20229 | 208 |
| aot | 784 / 793 / 166 | 47 | 1408 / 3795 / 11345 | 73 |
| aot-size | 852 / 788 / 166 | 47 | 1307 / 1535 / 3284 | 105 |

**Run 1** (no CPU column):

| Build | `--metadata-only` med / mean | Full run med / mean / max |
|---|---|---|
| tool-shim | 1142 / 1098 | 1491 / 1634 / 2617 |
| orig-net8 | 1218 / 1207 | 1525 / 1883 / 4623 |
| orig-net10 | 1109 / 1196 | 1426 / 1420 / 1700 |
| fdd-srcgen | 992 / 1063 | 1501 / 1492 / 1665 |
| fdd-r2r | 1015 / 1107 | 1796 / 2275 / 5140 |
| sc-trim | 1468 / 1555 | 3386 / 4544 / 12546 |
| sc-trim-r2r | 913 / 915 | 1410 / 2133 / 7425 |
| aot | 932 / 933 | 1656 / 2646 / 9437 |
| aot-size | 903 / 1095 | 1144 / 2043 / 7182 |

Look at the maximums. Single calls took 10–21 seconds for every kind of build, against a typical
1.5 seconds. That is the "random slow calls" problem from #3, somewhere in YoutubeExplode or the
HTTP stack. It swamps every startup difference, which is why the summary uses medians and CPU time.

### Platform findings

**Linux.**

- linux-arm64 AOT was built in `mcr.microsoft.com/dotnet/sdk:10.0-noble-aot`. The binary is 11.5 MB,
  plus an 18 MB `.dbg` symbol file. It works end to end.
- It links only `libc` and `libm`, but it loads OpenSSL at run time for HTTPS. A bare `ubuntu:24.04`
  image fails with `The SSL connection could not be established, see inner exception.` until
  `libssl3t64` and `ca-certificates` are installed.
- A binary built on Ubuntu 24.04 (noble) needs `GLIBC_2.34` or newer, so `debian:11` cannot run it.
  The workflow builds on Ubuntu 22.04 to reach more systems and prints the highest glibc symbol
  version the binary needs.
- With `InvariantGlobalization`, ICU (`libicu`) is not needed.

**macOS.**

- osx-x64 cross-compiles from an arm64 Mac with no extra setup. Xcode ships both toolchains.
- The x64 binary could not be run locally because the test machine has no Rosetta. CI runs it on
  GitHub's Intel runner instead.
- The binaries are only ad-hoc signed. That is fine for Homebrew or `curl`, which do not set the
  quarantine flag. A browser download is quarantined and Gatekeeper blocks it. Notarising needs an
  Apple Developer ID.

**Windows.** win-x64 needs a Windows runner, because NativeAOT cannot cross-compile between operating
systems. It is untested so far.

**Debug symbols.** By default the publish folder contains a `.dSYM` (about 30 MB on macOS), `.dbg`
(Linux) or `.pdb` (Windows). `CopyOutputSymbolsToPublishDirectory=false` removes it. This was verified
on osx-arm64: the publish folder holds only the executable.

### The csproj change

```xml
<!-- In the perf/source-gen-json branch: turns on the trim and AOT analyzers for every build. -->
<PropertyGroup>
  <IsAotCompatible>true</IsAotCompatible>
</PropertyGroup>

<!-- In the AOT branch: only affects `dotnet publish -p:PublishAot=true`. -->
<PropertyGroup Condition="'$(PublishAot)' == 'true'">
  <InvariantGlobalization>true</InvariantGlobalization>
  <CopyOutputSymbolsToPublishDirectory>false</CopyOutputSymbolsToPublishDirectory>
</PropertyGroup>
```

The conditional group leaves the NuGet tool package alone. With it applied to v1.0.7, all 26 tests
pass and `dotnet pack` produces a package with the same files and sizes.

Publish command:

```bash
dotnet publish src/Ytx -c Release -f net10.0 -r <rid> -p:PublishAot=true
```

### Packaging options

**Option A, recommended now: GitHub Release assets plus package managers.**

- Keep `solrevdev.ytx` on NuGet exactly as it is. It still targets net8, net9 and net10.
- Attach a NativeAOT archive per platform, plus `SHA256SUMS`, to each GitHub Release.
- Point a Homebrew tap formula, a Scoop manifest and possibly a winget manifest at those assets.
- See `packaging/README.md` for the templates, the secrets each channel needs, and the name clash
  with [koguchic/ytx](https://github.com/koguchic/ytx), an unrelated Rust CLI that also installs `ytx`.

**Option B, maybe at 2.0: a RID-specific .NET tool.**

.NET 10 can pack a tool per runtime identifier, with NativeAOT inside and a framework-dependent
fallback. It was tested with a separate project that compiles the same `Program.cs`:

```xml
<PublishAot>true</PublishAot>
<ToolPackageRuntimeIdentifiers>osx-arm64;osx-x64;linux-x64;linux-arm64;win-x64;any</ToolPackageRuntimeIdentifiers>
```

- `dotnet pack` builds a 2 KB pointer package.
- `dotnet pack -r osx-arm64` builds an 11 MB AOT package for that RID.
- `dotnet pack -r any -p:PublishAot=false` builds a 0.5 MB framework-dependent fallback.
- Installing with SDK 10 links straight to the native binary, with no shim. `--version` then takes
  7.3 ms, against 33.5 ms for the current shim.

It has real costs:

- **It breaks users on the .NET 8 SDK.** Installing fails with:

  ```text
  The settings file in the tool's NuGet package is invalid: Format version is higher than supported. This tool may not be supported in this SDK version. Update your SDK.
  ```

- Each RID package must be built on its own OS. Cross-architecture works, cross-OS does not.
- The RID packages must be pushed before the pointer package.
- The net8 and net9 targets become pointless.

So option B cannot ship as a minor release.

### CI shape

`.github/workflows/native.yml` builds on five runners:

| RID | Runner | Notes |
|---|---|---|
| osx-arm64 | `macos-latest` | arm64 (M1) |
| osx-x64 | `macos-latest` | Cross-architecture build, then run on `macos-15-intel` |
| linux-x64 | `ubuntu-22.04` | Older glibc floor |
| linux-arm64 | `ubuntu-22.04-arm` | Native arm64 runner |
| win-x64 | `windows-latest` | |

Runner availability was checked against GitHub's documentation on 8 Oct 2026:

- `ubuntu-22.04-arm` is available for public repositories.
- `macos-13` no longer exists. It was retired in December 2025. `macos-15-intel` is the Intel
  replacement. GitHub has said Intel macOS runners end when the macOS 15 image retires, announced for
  fall 2027. After that, osx-x64 can still be cross-compiled but not tested on GitHub.

Each binary is smoke-tested offline (`scripts/aot/smoke-test.sh`): `--version` must print the
release version, `--help` must print usage, and bad input must exit 2 on both the argument and stdin
JSON paths. The stdin JSON check is the one that catches the reflection problem above.

One trap: `publish.yml` creates releases with `GITHUB_TOKEN`, and events caused by `GITHUB_TOKEN`
do not start other workflows. So `on: release: published` will not fire for automated releases.
`native.yml` therefore also accepts a `tag` input, and `publish.yml` can dispatch it with
`gh workflow run native.yml -f tag="$TAG"`, which is allowed.

### Is AOT worth it?

- **For a person running ytx once: no.** It saves about 0.2 s on a 1–2 s call that waits on the network.
- **For agents and batch jobs: yes.**
  - About 6x less CPU per call: 47–73 ms against 300–400 ms.
  - Half the memory: 53–61 MB against 111–132 MB.
  - Fast failure paths: `--help` and bad input drop from 38–56 ms to about 9 ms.
- **For distribution: yes.** No .NET runtime is needed, which helps Homebrew, containers and CI.

## Part 2: Allocations and CPU

### Where the time and memory go

An instrumented copy of v1.0.7 timed each phase on .NET 10 with workstation GC.

**5 hour 15 minute video (16,284 cues):**

| Phase | Time | Allocated | Owner |
|---|---|---|---|
| `Videos.GetAsync` (watch page) | 980–1,370 ms | 43 MB | YoutubeExplode + network |
| Caption manifest | 170–310 ms | 1.2–1.6 MB | YoutubeExplode + network |
| Caption track fetch and XML parse | 610–790 ms (spikes to 10 s) | 106–109 MB | YoutubeExplode + network |
| ytx: build transcript | 28 ms | 7.9 MB | ytx |
| ytx: serialise and write | 15 ms | 26.3 MB | ytx |
| **Total** | **2.1–2.2 s** | **186 MB** (22 gen0 and 4 gen2 collections) | |

**17 minute video (924 cues), one run:**

| Phase | Time | Allocated |
|---|---:|---:|
| Watch page | 1,189 ms | 30.8 MB |
| Caption manifest | 280 ms | 3.5 MB |
| Caption track | 140 ms | 6.4 MB |
| ytx: build transcript | 3.2 ms | 0.5 MB |
| ytx: serialise and write | 14.0 ms | 1.3 MB |
| **Total** | **1,627 ms** | **42.5 MB** (3 gen0, 2 gen1, 2 gen2) |

About 98% of a run is network and YoutubeExplode. Perfect code on ytx's side would save 20–25 ms out
of about 2 s. Network jitter alone moves the same caption fetch between 0.5 s and 10 s.

YoutubeExplode 6.6.2 allocates about the same: 39–44 MB for the watch page and about 97 MB for the
captions. Its release notes since 6.5.6 mention no performance work. It is still worth upgrading for
the client fixes in 6.5.7 and 6.6.2 and the HTML parser concurrency fix in 6.6.1, and because 6.5.6
pulls in AngleSharp 1.3.0, which has a moderate advisory (NU1902).

### What the current code does

- **`NormalizeCaption` allocates for almost every caption.** The pattern `\s+` matches every single
  space, so `Regex.Replace` returns a new string even when nothing changes. The static
  `Regex.Replace` also looks up the regex cache on every call.
- **Serialising to a string costs 26 MB on a cold 5-hour run.** `JsonSerializer.Serialize` to a
  string sizes its buffer for the worst-case escaping of the whole value, about 6 bytes per character
  of a 1.1 million character string. It then produces a 2.2 MB UTF-16 string, which
  `Console.WriteLine` encodes to UTF-8 again.
- **Each caption creates four temporary strings:** the timestamp, the link, the formatted line and
  the normalised text. The `StringBuilder` grows in chunks, and `ToString().Trim()` copies the
  result once more.
- **A new `JsonSerializerOptions` is built on every run,** and reflection-based setup is a real part
  of the 13–14 ms cold serialise time. The source-generated context from Part 1 fixes this.

### Benchmark subjects

- **Baseline** is the v1.0.7 code, copied verbatim.
- **Tier 1** is three readable changes, shown below.
- **Tier 2** is a pooled-buffer rewrite with hand-written digit formatting and a chunked
  `Utf8JsonWriter`.

Every variant was checked for byte-identical output against Baseline, in indented and compact mode,
on both videos, plus edge cases: `&nbsp;`, NBSP, U+2028, U+0085, U+3000, emoji, and offsets over
100 hours. The checker is the `verify` command in `benchmarks/Ytx.Benchmarks`.

### BenchmarkDotNet results

Apple M1, .NET 10.0.12, BenchmarkDotNet 0.15.2, network excluded by replaying captured captions.

| Method | Video | Mean | Allocated | Alloc ratio |
|---|---|---:|---:|---:|
| Baseline_Normalize (16k cues) | 5 h | 3,408 µs | 754,614 B | 1.00 |
| Tier1_Normalize | 5 h | **581 µs** | **904 B** | 0.001 |
| Baseline_Format | 5 h | 5,208 µs | 8,236,196 B | 0.78 |
| Tier1_Format | 5 h | 1,778 µs | 4,471,553 B | 0.43 |
| Tier2_Format | 5 h | 1,232 µs | 128 B | 0.00 |
| **Baseline_All** (format, serialise, write) | 5 h | **7,947 µs** | **10,506,442 B** | 1.00 |
| Tier1_All | 5 h | 3,974 µs | 4,469,011 B | 0.43 |
| Tier1SourceGen_All | 5 h | 3,923 µs | 4,469,010 B | 0.43 |
| Tier2_All | 5 h | 3,440 µs | 58,224 B | 0.006 |
| Baseline_All | 17 min | 420 µs | 626,002 B | 1.00 |
| Baseline_Format | 17 min | 292 µs | 486,113 B | 0.78 |
| Tier1_All | 17 min | 171 µs | 282,016 B | 0.45 |
| Tier1_Format | 17 min | 80 µs | 281,799 B | 0.45 |
| Tier1SourceGen_All | 17 min | 171 µs | 282,014 B | 0.45 |
| Tier2_Format | 17 min | 53 µs | 128 B | 0.00 |
| Tier2_All | 17 min | 149 µs | 48,024 B | 0.08 |

The `_Format` alloc ratios are relative to `Baseline_All` for the same video, because BenchmarkDotNet
groups them together.

### Cold runs

BenchmarkDotNet measures warm, steady-state code. A CLI runs once, cold, and pays for JIT
compilation and serialiser warm-up. So each variant also ran once in a fresh process, 5 times, on the
5-hour data with no network:

| Variant | Wall time | Allocated |
|---|---:|---:|
| Baseline | 42–43 ms | 34.2 MB |
| Tier 1 (reflection JSON) | 29 ms | 28.6 MB |
| Tier 1 + source-gen JSON | 23 ms | 28.5 MB |
| Tier 2 | 20 ms | 5.3 MB |

The synthetic dataset in `benchmarks/Ytx.Benchmarks` reproduces this closely: 46, 33, 25 and 23 ms.

### Recommended changes (Tier 1)

**1. Skip the regex when a caption is already clean.** Normalisation drops from 754 KB to under
1 KB and gets 6x faster.

```csharp
[GeneratedRegex(@"\s+")] private static partial Regex Whitespace();

internal static string NormalizeCaption(string text)
{
    if (string.IsNullOrWhiteSpace(text)) return "";
    // \s+ matches every ordinary space, so the regex would allocate for nearly every caption.
    if (NeedsNormalizing(text)) text = Whitespace().Replace(text, " ").Trim();
    return text.Contains("&nbsp;") ? text.Replace("&nbsp;", " ") : text;
}

static bool NeedsNormalizing(ReadOnlySpan<char> t)
{
    if (char.IsWhiteSpace(t[0]) || char.IsWhiteSpace(t[^1])) return true;
    for (int i = 1; i < t.Length; i++)
        if (t[i] == ' ' ? t[i - 1] == ' ' : char.IsWhiteSpace(t[i])) return true;
    return false;
}
```

**2. Format numbers straight into the `StringBuilder`.** The interpolated-string handler for
`StringBuilder.Append` formats in place, which removes the temporary strings per caption.

```csharp
if (rawSb.Length > 0) { rawSb.Append(' '); mdSb.Append(Environment.NewLine); }
rawSb.Append(text);
var ts = caption.Offset; int h = (int)ts.TotalHours;
if (h > 0) mdSb.Append($"- [{h:00}:{ts.Minutes:00}:{ts.Seconds:00}");
else       mdSb.Append($"- [{ts.Minutes:00}:{ts.Seconds:00}");
mdSb.Append($"](https://www.youtube.com/watch?v={videoId}&t={(int)ts.TotalSeconds}s) ").Append(text);
```

**3. Serialise straight to stdout with the source-generated context.** This is shared with the AOT fix.

```csharp
using var stdout = Console.OpenStandardOutput();
JsonSerializer.Serialize(stdout, output, (compact ? Compact : Indented).Output);
stdout.Write(NewLineBytes);
```

Together these are about 30 lines. They stay readable and keep the output byte-identical. They halve
ytx's own CPU time and allocations and save about 20 ms on a cold 5-hour run.

A detail for whoever implements this: v1.0.7 separates Markdown lines with `AppendLine` and ends the
output with `Console.WriteLine`. Both use `Environment.NewLine`, so on Windows the transcript lines
are joined with `\r\n` inside the JSON string, and the document ends with `\r\n`. Writing a literal
`\n` instead is arguably better, but it changes Windows output. Decide on purpose.

### Not worth doing

- **Tier 2.** It cuts allocations by 99% but saves only about 3 ms more than Tier 1 on a 5-hour video,
  which is invisible next to 2 s of network. It is also harder to keep correct. It has to avoid
  splitting surrogate pairs across JSON segments, copy the exact trim behaviour, and fall back to the
  old code for `&nbsp;` because v1.0.7 replaces it after collapsing whitespace.
- **Reducing YoutubeExplode's allocations** (about 150 MB on the 5-hour video). That would mean
  forking it or bypassing its parsing. GC pauses here cost a few milliseconds.

## What is worth doing, and what is vanity

| Change | Effort | User-visible effect | Verdict |
|---|---|---|---|
| Source-generated `JsonSerializerContext` | ~10 lines | Unblocks AOT. ~6 ms faster stdin path everywhere. | Do it |
| Tier 1 formatting and streaming output | ~30 lines | ~20 ms on a cold 5-hour run. Halves ytx's own allocations. | Do it, with a golden-output test |
| `IsAotCompatible=true` | 1 line | None directly. Catches AOT regressions at build time. | Do it |
| NativeAOT release assets | One workflow | 6x less CPU, half the memory, no runtime. Matters for agents, CI and Homebrew users. | Worth it, decision pending |
| Homebrew tap and Scoop bucket | Two repos, one PAT | Easier install without .NET | Worth it once assets exist |
| winget | Manual first PR, then a PAT | Reaches default Windows users | Later |
| RID-specific .NET tool (option B) | csproj and release rework | 7 ms startup via `dotnet tool` | Only at 2.0, if at all |
| Size-optimised AOT | 3 properties | Saves 1 MB, worse error messages | Vanity |
| Tier 2 zero-allocation rewrite | ~150 lines | ~3 ms more than Tier 1 | Vanity |
| Cutting YoutubeExplode's allocations | A fork | A few ms of GC | Vanity |

The bigger wins for users are not in this document. The caption track bugs in #3 return the wrong
transcript with exit 0. The 10–21 second outliers seen in the network runs cost more than every
startup saving here combined. Both deserve attention before shaving milliseconds.

## Decision status

**Exploratory, not yet decided.**

- The source-gen JSON change, Tier 1 and `IsAotCompatible` are in
  [#5](https://github.com/solrevdev/solrevdev.ytx/pull/5) (`perf/source-gen-json`), with
  golden-output tests. It keeps `Environment.NewLine`, so Windows output is unchanged.
- The AOT work sits on top of that branch in `feat/native-aot`: the csproj change, `native.yml`,
  the scripts in `scripts/aot/`, the benchmark project and the packaging templates. Nothing
  publishes yet.
- On that branch, the osx-arm64 AOT binary passes `scripts/aot/smoke-test.sh`. A live run on
  `aCi6BhrvjhM` produced output byte-identical to the framework-dependent build, in 1.58 s wall time
  with 0.08 s of user CPU.
- Open questions:
  - Is the Homebrew and Scoop audience big enough to justify a tap, a bucket and a PAT?
  - Should `publish.yml` dispatch `native.yml` automatically, or should native assets stay manual
    at first?
  - Is a 2.0 with a RID-specific tool package wanted, given it drops .NET 8 SDK users?

## How to reproduce

Everything below runs from the repository root on macOS or Linux with the .NET 10 SDK and Python 3.

**Deployment shapes and timings** (see `scripts/aot/README.md`):

```bash
# Publish all variants for this machine (output in artifacts/aot/, which is gitignored)
scripts/aot/publish-variants.sh

# Optional: the v1.0.7 baseline from a worktree
git worktree add ../ytx-v1.0.7 v1.0.7
REPO=../ytx-v1.0.7 OUT=$PWD/artifacts/aot PREFIX=v1.0.7- scripts/aot/publish-variants.sh fdd

# Measure. Startup needs no network; the other two call YouTube.
EXTRA="tool-shim=$HOME/.dotnet/tools/ytx" scripts/aot/run-bench.sh startup
scripts/aot/run-bench.sh network
scripts/aot/run-bench.sh rusage

# Offline smoke test of one binary
scripts/aot/smoke-test.sh artifacts/aot/aot/Ytx
```

**Formatting benchmarks** (see `benchmarks/Ytx.Benchmarks/README.md`):

```bash
cd benchmarks/Ytx.Benchmarks
dotnet run -c Release -- verify                 # byte-identical check on synthetic data
dotnet run -c Release -- capture NYFGCESmikA    # optional: real captions, kept out of git
dotnet run -c Release -- --filter '*'           # full BenchmarkDotNet run
```

The repository holds no real caption data. The synthetic datasets have the same cue counts and shape
as the two videos above, and give results within a few percent of them.

**Linux in Docker** (arm64 on an Apple Silicon Mac):

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0-noble-aot \
  dotnet publish src/Ytx -c Release -f net10.0 -r linux-arm64 -p:PublishAot=true -o /src/artifacts/linux-arm64
docker run --rm -v "$PWD/artifacts/linux-arm64":/app ubuntu:24.04 /app/Ytx --version
# A full run needs: apt-get update && apt-get install -y libssl3t64 ca-certificates
```
