# From review to 1.1.0: fixing ytx's caption choice and output contract

**Date:** 8 October 2026
**Status:** Done. Issue #3 is closed by the 1.1.0 release. NativeAOT (PR #6) is the next piece of work.
**Release:** [v1.1.0](https://github.com/solrevdev/solrevdev.ytx/releases/tag/v1.1.0)

This is the source document for a blog post. It collects the facts, evidence, decisions and traps from the work that took ytx from v1.0.7 to 1.1.0. It is not the post itself. The earlier part of the story, from the first review through v1.0.8, is in issue [#7](https://github.com/solrevdev/solrevdev.ytx/issues/7). The NativeAOT and allocation investigation has its own write-up, [2026-10-native-aot-and-allocations.md](2026-10-native-aot-and-allocations.md), on the `feat/native-aot` branch until PR #6 merges.

**Rule for the post:** don't publish captured YouTube caption text. Use titles, video IDs, byte counts, exit codes and timings only. The few short caption lines in this document came from test output and can be cut.

## 1. Summary

ytx is a .NET global tool, published on NuGet as `solrevdev.ytx`. It turns a YouTube URL into JSON with the title, description, a raw transcript and a Markdown transcript with timestamped links. It targets .NET 8, 9 and 10 and uses YoutubeExplode to talk to YouTube.

On 8 October 2026 a review of v1.0.7 found that ytx could silently return the wrong transcript:

- **It picked the wrong track by default.** Auto-generated captions beat human-written ones.
- **It matched languages loosely.** `-l fr` returned Afrikaans, because "fr" is a substring of "Afrikaans".
- **It fell back silently.** An unknown language returned English with exit 0 and nothing on stderr.
- **It hid failures.** Every caption error looked like "no captions".

Three versions came out of the work:

| Version | What it brought |
|---|---|
| 1.0.7 | The version that was reviewed. |
| 1.0.8 | PR #5: source-generated JSON, output streamed to stdout, fewer allocations. Output stayed byte-identical. This made NativeAOT possible. |
| 1.1.0 | PRs #8–#13: correct caption track choice, a richer and stable JSON contract with `captionStatus`, exit code 3, `--timeout`, Ctrl+C handling, `--format md/txt/srt/vtt`, `--segments`, `--list-languages`, `--proxy`, YoutubeExplode 6.6.2, LICENSE, CI hardening and package metadata. Shipped as one minor release. |

The next step is NativeAOT binaries (PR #6), which are ready but deliberately left for a separate round of work.

## 2. Source map

### Issues and pull requests

| Item | Role |
|---|---|
| [#3](https://github.com/solrevdev/solrevdev.ytx/issues/3) | Review of v1.0.7. Bugs 1–4, smaller issues, competitor survey and suggested plan. Closed by 1.1.0. |
| [#4](https://github.com/solrevdev/solrevdev.ytx/issues/4) | NativeAOT and allocation findings: startup 38 ms to 8.6 ms, about 6x less CPU, half the memory. Still open. |
| [#5](https://github.com/solrevdev/solrevdev.ytx/pull/5) | Source-generated JSON and fewer allocations. Merged and released as v1.0.8. |
| [#6](https://github.com/solrevdev/solrevdev.ytx/pull/6) | NativeAOT builds, benchmarks, smoke tests, native CI and packaging templates. Finished on the 1.1.0 contract. The next piece of work. |
| [#7](https://github.com/solrevdev/solrevdev.ytx/issues/7) | Blog notes for the first part (review to v1.0.8), with the decided order of work. |
| [#8](https://github.com/solrevdev/solrevdev.ytx/pull/8) | Caption track selection fix and a stderr warning on fallback. Bugs 1–3. |
| [#9](https://github.com/solrevdev/solrevdev.ytx/pull/9) | Housekeeping: YoutubeExplode 6.6.2, LICENSE, stale doc removed. |
| [#10](https://github.com/solrevdev/solrevdev.ytx/pull/10) | The 1.1.0 output contract: metadata, `captionStatus`, track fields, canonical `url`, exit 3. Bug 4. |
| [#11](https://github.com/solrevdev/solrevdev.ytx/pull/11) | CLI polish: `--timeout`, Ctrl+C exits 130, `&nbsp;` fix, help gaps. |
| [#12](https://github.com/solrevdev/solrevdev.ytx/pull/12) | Features: `--format`, `--segments`, `--list-languages`, `--proxy`. |
| [#13](https://github.com/solrevdev/solrevdev.ytx/pull/13) | CI and packaging: SHA-pinned actions, Dependabot, CodeQL, tests on three runtimes, icon, symbols, CHANGELOG. |

### Where things live

| Path | What |
|---|---|
| `src/Ytx/Program.cs` | The whole CLI: option parsing, track selection, output contract, formats. |
| `src/Ytx/Ytx.csproj` | Targets, package metadata, icon, symbols, AOT-only settings. |
| `tests/Ytx.Tests/ProgramTests.cs` | Unit tests for parsing, track ranking, error classification, formats. |
| `tests/Ytx.Tests/GoldenOutputTests.cs` | Byte-for-byte checks against a verbatim copy of the v1.0.7 code, plus golden JSON for the 1.1.0 contract. |
| `.github/workflows/publish.yml` | PR validation and NuGet release on push to `master`. |
| `.github/workflows/codeql.yml` | CodeQL for C#. |
| `.github/dependabot.yml` | Weekly Actions and NuGet updates. |
| `.github/workflows/native.yml` | Native AOT matrix (PR #6). |
| `scripts/aot/`, `benchmarks/`, `packaging/` | AOT scripts, BenchmarkDotNet project, Homebrew and Scoop templates (PR #6). |
| `docs/investigations/` | This document and the AOT write-up. |
| `CHANGELOG.md` | Release history from 1.0.1. |
| `assets/icon.svg` | Package icon source. `icon.png` is rendered from it. |

## 3. Timeline

Times are UTC on 8 October 2026. The first part, up to v1.0.8 at 19:23, is covered in more detail in #7.

| Time (UTC) | What happened |
|---|---|
| 18:04 | Three agents start reviewing ytx in parallel: source, live tests and competitors. |
| 18:19 | Issue #3 opened with the full review. |
| 18:38 | Issue #4 opened with the AOT and allocation findings. |
| 19:01 | PR #5 opened: source-generated JSON. |
| 19:13 | PR #6 opened as a draft: NativeAOT. |
| 19:22–19:23 | PR #5 merged. v1.0.8 published. PR #6 was closed automatically when its base branch was deleted, then reopened and retargeted. |
| 19:34 | A scoping agent sets the order of work, which is recorded in #7 under "What is next". |
| 19:36 | The first Claude session commits the track-selection fix (`a28b936`) on `fix/caption-track-selection`. |
| about 20:00 | **Handoff.** The first session messages a second Claude session with the branch, the context to read and suggested next steps. The second session does the rest. |
| 20:03 | PR #8 opened after the live checks were rerun and a stderr fallback warning was added. |
| 20:06 | PR #9 opened: housekeeping. |
| 21:13 | PR #10 opened: the 1.1.0 schema, stacked on #8. |
| 21:15 | PR #11 opened: CLI polish, stacked on #10. |
| 21:18 | PR #12 opened: features, stacked on #11. |
| 21:20 | PR #13 opened: CI and packaging, based on `master`. |
| about 21:22 | PR #6 is merged up to the stack, retargeted onto #12 and marked ready. All five native builds pass. |
| about 21:25 | The user drops MCP mode from scope. |
| 21:30 | PR #9 merged. The rest follow in order (see below). |
| after 21:40 | 1.1.0 published through a manual minor release. Issue #3 closed. |

### How the work was split across Claude sessions

- **Session A** reviewed v1.0.7, wrote #3, #4 and #7, made PR #5 and the AOT draft, and prototyped the track fix.
- **Session B** took a handoff message from session A. It reran the live checks, then built PRs #8–#13, finished #6 and merged everything. The user stepped in at three points:
  - **Schema:** "we can change the schema. Not too many people are using it."
  - **MCP:** "We could potentially leave MCP off."
  - **Merging:** "manage all of the merging branches and pull requests".
- **The peer message was a request from a teammate, not the user's approval.** Session B did all the local work, then asked the user before opening the first PR, because merging publishes to NuGet.

### Stack and merge order

```text
master
├── #9  chore/housekeeping            (independent)
├── #13 ci/hardening                  (independent)
└── #8  fix/caption-track-selection
    └── #10 feat/output-schema
        └── #11 fix/cli-polish
            └── #12 feat/formats-proxy-languages
                └── #6 feat/native-aot   (left open: next piece of work)
```

The merge order was #9, then #8 → #10 → #11 → #12, then #13, then a release-prep PR (#14) with the CHANGELOG and this document. Each stacked PR was retargeted to `master` and updated before it merged, so CI ran on it. The merged branches were deleted.

### One release instead of six

Every push to `master` that touches `src/Ytx/**` runs `publish.yml`, which publishes a NuGet patch. Merging six PRs would have produced 1.0.9 to 1.0.14, and the runs would have failed each other. The workflow's "origin/master moved" check refuses to publish when `master` has moved on since the run started.

So each merge commit carried `[skip ci]`. When everything was in, "Publish NuGet (ytx)" was run by hand with `bump: minor`, which published 1.1.0 once. Release: [v1.1.0](https://github.com/solrevdev/solrevdev.ytx/releases/tag/v1.1.0).

## 4. The changes

### 4.1 Caption track selection (PR #8, bugs 1–3)

**Problem.** The ordering in v1.0.7 was:

```csharp
.OrderByDescending(t => LanguageMatches(t.Language.Code, t.Language.Name, options.Language))
.ThenByDescending(t => t.IsAutoGenerated)
```

`ThenByDescending(IsAutoGenerated)` puts auto-generated tracks first. `LanguageMatches` used `name.Contains(preference)`.

**Evidence from the live tests (v1.0.7):**

- TED talk `iG9CE55wbtY`: `-l fr`, `-l es` and `-l ja` returned Afrikaans, Chinese and Azerbaijani, even though exact tracks existed.
- The default returned the auto-generated English track while a manual one existed.
- Lex Fridman #501 `NYFGCESmikA`: ytx returned 8,144 auto-caption lines, against 4,773 manual lines from the older `yt_json` script.
- `-l pt`, `-l zh`, `-l xx` and `-l Klingon` all returned English with exit 0 and nothing on stderr.

**Fix.** A testable `SelectTrack` ranks tracks with `LanguageMatchRank`:

| Rank | Match | Example |
|---|---|---|
| 4 | Exact code | `fr` = `fr` |
| 3 | Exact name | `French` = `French` |
| 2 | Code prefix at a `-` boundary | `en` matches `en-GB`, `pt` matches `pt-BR` |
| 1 | Name substring, only when the preference is longer than 3 characters | `Chinese` matches `Chinese (Simplified)` |
| 0 | No match | |

Ties go to English, then to a manual track over an auto-generated one:

```csharp
tracks
    .OrderByDescending(t => LanguageMatchRank(t.Language.Code, t.Language.Name, preference))
    .ThenByDescending(t => LanguageMatchRank(t.Language.Code, t.Language.Name, "en"))
    .ThenBy(t => t.IsAutoGenerated)
    .FirstOrDefault();
```

When the chosen track doesn't match the request, ytx now writes `Warning: No captions match 'xx'; using English (en).` to stderr.

**Verified live:**

- TED default returns manual English.
- `-l fr`, `es`, `ja` and `zh` return French, Spanish, Japanese and zh-CN.
- `-l xx` returns English with the warning.
- `-l pt` matches pt-BR with no warning.
- `NYFGCESmikA` returns 4,773 lines, which matches `yt_json`.

Tests went from 70 to 85.

### 4.2 Housekeeping (PR #9)

- **YoutubeExplode 6.5.6 → 6.6.2.** This drops the transitive AngleSharp 1.3.0 and its `NU1902` advisory ([GHSA-pgww-w46g-26qg](https://github.com/advisories/GHSA-pgww-w46g-26qg)). Release builds now have 0 warnings.
- **Output was checked for regressions.** Three videos were run before and after the upgrade, each in full mode and with `--metadata-only`: `iG9CE55wbtY`, `NYFGCESmikA` and `aCi6BhrvjhM`. All six outputs were byte-identical.
- **LICENSE added.** The package declared MIT and the README linked to `LICENSE`, but the file didn't exist, so GitHub reported `license: null`.
- **Stale doc removed.** `docs/html-viewer-feature.md` described a `web/viewer.html` that was never committed.

### 4.3 The 1.1.0 output contract (PR #10, bug 4)

**Problem.** A bare `catch` turned every caption error into placeholder text inside `transcript` and exited 0. Network failures, bot checks and parse errors all looked like a video with no captions. Scripts had to compare against strings such as `_No transcript/captions available for this video._`. Other problems:

- `url` echoed the input, including `?si=` tracking parameters.
- Nothing in the output said which track was used.

**Fix.** A fixed field order, and `null` for missing values instead of omitted keys:

```text
url, videoId, title, channel, channelId, uploadDate, durationSeconds, viewCount, keywords,
description, captionStatus, captionLanguage, captionLanguageName, captionIsAutoGenerated,
captionError, transcriptRaw, transcript, segments
```

- **`captionStatus`** is `ok`, `none`, `skipped`, `blocked` or `error`.
- **Only caption-fetch failures are caught.** That means `YoutubeExplodeException`, `HttpRequestException` and `TaskCanceledException`. Any other exception is a bug and exits 1.
- **`blocked`** covers `RequestLimitExceededException`, HTTP 403 and 429, and "not a bot" challenges. Callers can retry later or through a proxy.
- **Exit code 3** means the metadata was written, but captions were blocked or failed. A video with no captions still exits 0.
- **`url` is canonical,** for example `https://youtu.be/ID?si=x` becomes `https://www.youtube.com/watch?v=ID`.

**Breaking changes.** `url` no longer echoes the input. When there is no transcript, the transcript fields are empty instead of holding placeholder text. The user agreed: "At this stage, we can change the schema... I don't think anybody is using it."

Tests went to 94. They include golden JSON for the new contract, metadata mapping, a live stream with `null` duration, and error classification.

### 4.4 CLI polish (PR #11)

- **`--timeout <seconds>`** bounds the whole run with a `CancellationTokenSource`.
- **Ctrl+C exits 130.** It cancels the in-flight YoutubeExplode calls and doesn't kill the process abruptly. HttpClient's own 100 s timeout also throws `TaskCanceledException`. The catch filter tells the two apart with `!cancellationToken.IsCancellationRequested`.
- **`&nbsp;` ordering.** v1.0.7 collapsed whitespace and then replaced `&nbsp;`, so `a&nbsp;&nbsp;b` became `a  b`. Now entities are replaced first.
- **`--metadata-only -l fr`** now warns that `--language` is ignored.
- **Help and README gaps closed.** They now say that piped JSON uses only `url`, that a URL argument wins over piped input, and that an ID starting with `-` needs `--`.

**Golden test approach.** `GoldenOutputTests` keeps a verbatim copy of the v1.0.7 transcript code as a reference. For the one intended change, the legacy reference is fed text with entities already replaced. Every other caption still has to match v1.0.7 byte for byte, including a check over every BMP character.

Tests went to 105.

### 4.5 Features (PR #12)

- **`--format md|txt|srt|vtt`** writes only the transcript.
  - SRT and WebVTT keep millisecond cue times, built from the caption offset plus duration.
  - In WebVTT, a literal `-->` in the text becomes `->`.
- **`--segments`** adds `segments: [{start, end, text}]` in seconds, rounded to the millisecond. It is opt-in because long videos are already large: `transcript` is about 2.9x the size of `transcriptRaw`. Without the flag the field is `null`, so every document keeps the same keys.
- **`--list-languages`** writes `{videoId, tracks: [{code, name, isAutoGenerated}]}`.
- **`--proxy <url>`** accepts http, https, socks4, socks4a and socks5. Without it, HttpClient's default proxy still reads `HTTPS_PROXY`, `HTTP_PROXY` and `NO_PROXY`. This is now documented.
- **Conflicting options exit 2.** Examples are `--format srt --metadata-only` and `--list-languages --segments`.

**Verified live.** On `iG9CE55wbtY`, `--segments` returned 427 segments. `--proxy http://127.0.0.1:9` and `HTTPS_PROXY=http://127.0.0.1:9` both failed with "Connection refused", which shows that requests go through the proxy.

Tests went to 118.

### 4.6 CI and packaging (PR #13)

- **SHA-pinned actions with version comments.** The pins stay on the current majors: checkout v5.1.0, setup-dotnet v5.4.0 and NuGet/login v1.2.0.
- **Dependabot** checks Actions and NuGet weekly, with grouped updates.
- **CodeQL** with `build-mode: none`, so no SDK or build is needed.
- **Tests run on net8.0, net9.0 and net10.0.**
- **Package metadata:** an icon, a `.snupkg` symbols package, `PublishRepositoryUrl`, and `ContinuousIntegrationBuild` on GitHub Actions for deterministic SourceLink.
- **`CHANGELOG.md`** in Keep a Changelog format, back to 1.0.1.

**Verified.** `actionlint` and YAML parsing pass. `dotnet pack` produced both packages, and the nuspec has `<icon>` and `<repository ... commit=...>`. CodeQL passed on the PR.

### 4.7 NativeAOT finished but not merged (PR #6)

- **Merged up to the stack.** The `feat/native-aot` branch now has the 1.1.0 contract, and the PR was retargeted onto #12.
- **Smoke test extended.** It now covers the new usage errors and a refused `--proxy` connection on port 9. That runs the HttpClient, proxy and error paths offline.
- **Release trigger.** `publish.yml` dispatches `native.yml` with the new tag after each release.
- **Native workflow actions pinned to SHAs.**
- **Verified on osx-arm64.**
  - The binary is 10.6 MB, with no trim or AOT warnings.
  - AOT and JIT output was byte-identical, with the same exit codes, in 7 modes.
  - All 15 smoke checks pass.
  - CI passed on osx-arm64, osx-x64, linux-x64, linux-arm64 and win-x64 ([run 37846332490](https://github.com/solrevdev/solrevdev.ytx/actions/runs/37846332490)).

## 5. Traps and lessons

Each of these is worth a paragraph in the post.

- **Short codes inside language names.** "fr" is inside "Afrikaans", "en" is inside "French", and "es" is inside many names. Substring matching on names is only safe for longer inputs, so the rank uses it only when the preference is longer than 3 characters. Prefix matching on codes needs a `-` boundary, so that `en` matches `en-GB` but not `eng`.

- **A cleaner sort can fall back to Afrikaans.** The first version of the fix used only `ThenBy(IsAutoGenerated)` after the match rank. With `-l xx`, every track ranked 0, so the result was simply the first manual track in YouTube's list, which was Afrikaans. Adding English as an explicit second key kept v1.0.7's English fallback for unknown languages.

- **Background jobs ignore SIGINT in a non-interactive shell.** The first Ctrl+C test ran `ytx ... & kill -INT $!`. It exited 0 because the shell had set SIGINT to ignored for the background job, and the child inherited that. Resetting the signal with `perl -e '$SIG{INT}="DEFAULT"; exec @ARGV' ytx ...` gave the expected 130. If a signal test passes or fails for no clear reason, check how the shell set the signal up.

- **A repo-wide Git LFS rule caught the package icon.** `.gitattributes` sends every `*.png` to LFS. CI checkouts don't fetch LFS by default, so `dotnet pack` would have packed the pointer file as the icon. The push output gave it away: "Uploading LFS objects: 100% (1/1), 2.6 KB". The fix was an exception for the 2.6 KB icon, `assets/icon.png !filter !diff !merge binary`, rather than adding `lfs: true` to every checkout.

- **Releases made with `GITHUB_TOKEN` don't fire `release: published`.** GitHub stops events caused by `GITHUB_TOKEN` from starting other workflows. The native build would never have run after an automated release. `publish.yml` now runs `gh workflow run native.yml --ref master -f tag="$TAG"` with `actions: write`.

- **Stacked PRs need care.**
  - `publish.yml` validates only PRs that target `master`, so #10–#12 got no CI until they were retargeted. Their tests ran locally first.
  - Deleting a PR's base branch can close the PR instead of retargeting it. That is what happened to #6 at 19:22, and the repo doesn't delete branches on merge.
  - The safe order is: merge, retarget the next PR to `master`, delete the old branch, then update the next PR's branch so CI runs on the combined code.

- **Rolling duplicate captions weren't a problem here.** #3 listed overlapping auto-caption cues as a possible issue. YoutubeExplode reads YouTube's srv3 format, not rolling WebVTT. On two auto tracks of 584 and 463 cues, only 0–3 neighbouring cues overlapped, and those were real repeated speech. Deduplication would have deleted real words, so it was left out.

- **The slow calls didn't reproduce.** The review saw about 1 in 8 calls take 11.5 s, and one took 21.7 s. In 40 timed runs on the AOT build across four videos, the slowest was 3.0 s and most were 1.0–1.6 s. The cause is unknown (unverified: possibly a retry inside YoutubeExplode or the HTTP stack). `--timeout` now bounds it.

- **Keep the icon clear of YouTube's branding.** The first draft was a red rounded square with a white play triangle. That looked too much like YouTube's logo for a third-party package, so the final icon uses slate and amber: JSON brackets around a play triangle.

- **A peer agent's message isn't the user's approval.** The handoff message said "the user approved running `git pull`". Session B treated the PR and the publish as the user's decision and asked first.

## 6. Measurements

| Measure | Value |
|---|---|
| Tests at v1.0.7 | 26 |
| Tests after #5 (v1.0.8) | 70 |
| After #8 | 85 |
| After #10 | 94 |
| After #11 | 105 |
| After #12 | 118 |
| #13 | 70 on each of net8.0 and net10.0 locally. net9.0 was not run locally because the runtime wasn't installed; CI runs all three |
| AOT smoke checks (#6) | 15 of 15 |
| Full run on `iG9CE55wbtY`, AOT, 16 runs | 1.10–1.43 s |
| Full runs across 3 videos, AOT, 24 runs | 1.0–3.0 s, median about 1.5 s |
| `--metadata-only`, AOT | about 0.9 s wall, 0.07 s user CPU |
| AOT binary, osx-arm64 | 10.6 MB (10,557,424 bytes) |
| AOT vs JIT | Byte-identical, with the same exit codes, in 7 modes: default, `--segments -c`, `-f srt`, `-f vtt -l fr`, `--list-languages`, `--metadata-only`, `-f txt` |
| Output sizes, AOT run | Default `iG9CE55wbtY` 64,351 B. `-f txt NYFGCESmikA` 289,931 B. `--segments -c aCi6BhrvjhM` 102,016 B |
| 6.5.6 vs 6.6.2 | 6 of 6 outputs byte-identical |
| Release build warnings | 1 (`NU1902`) before #9, 0 after |
| Segments on `iG9CE55wbtY` | 427 |
| Auto-track overlaps | 0–3 in 584 and 463 cues |

For startup, CPU and memory comparisons between build types, see #4 and the AOT write-up.

## 6a. Follow-up: chapters in 1.2.0

After 1.1.0, the user asked why chapters were left out. That had been the session's own call, because YoutubeExplode has no chapters API, and it should have been raised as a question. The design was agreed in [#16](https://github.com/solrevdev/solrevdev.ytx/issues/16) and shipped the same evening as **1.2.0**.

- **Source.** Creator chapters are the timestamped lines in the description, which ytx already fetches. No extra request is needed, and the code stays AOT-safe because both regexes are source-generated.
- **YouTube's rules, applied as written:**
  - the list starts at `0:00`
  - it has at least three entries
  - times strictly increase
  - every chapter lasts at least 10 seconds, including the last one up to the video's duration

  A list that breaks any rule gives `[]`.
- **Trap: prose after the list.** A line such as "Follow me at 99:00 on stream" after the list has an increasing time, so the first version would have added it as a chapter. The list is now one block: blank lines are allowed, but any other line ends it.
- **Trap: empty chapters.** A chapter with no captions of its own still gets its heading, so the Markdown outline always matches `chapters`. An extra blank line between two headings was caught in a unit test and removed.
- **Output.** `chapters` is appended as the last field, so existing field order is unchanged. Live on `NYFGCESmikA`, it returns 23 chapters, matching the description, and the Markdown starts each one with `## Title`. The TED talk returns `[]`. AOT and JIT output were byte-identical on all three checks.
- **Tests:** 145. They cover 10 line layouts, 6 rejections, 7 rule violations, a null end for live streams, and heading placement.
- **Left out:** auto-generated "key moments" (needs page scraping), a chapter index on each segment, and WebVTT chapter tracks.

## 7. Decisions not taken

- **No `ytx mcp` server mode.** It was prototyped as far as checking the API of `ModelContextProtocol.Core` 2.2.0, then dropped at the user's suggestion. Reasons:
  - It would add dependencies (Microsoft.Extensions.AI.Abstractions, DI and logging abstractions) and make the AOT binary bigger.
  - The MCP protocol still changes often.
  - Agents can already run `ytx` and parse its JSON, which now has `captionStatus`, segments and clear exit codes.
- **No deduplication of caption cues.** See section 5.
- **No branch protection.** The publish job pushes the version bump straight to `master`, and a protection rule would break that. The user chose to leave protection off.
- **No chapters in 1.1.0, thumbnails or other rich metadata.** YoutubeExplode doesn't expose chapters, so they were deferred, then added in 1.2.0 by parsing the description (section 6a). Thumbnails are still left out.
- **No translation, cookies or batch input.** #3 lists these in "What users expect". They weren't in the decided order (#7), so they stay as possible future work.
- **Action majors not bumped.** The pins stay on v5 and v1. Dependabot will propose v6 and v7 as separate PRs.
- **No CHANGELOG entries until release.** Adding them in each stacked PR would have caused conflicts, so they were written at release time.

## 8. What is next

PR #6, NativeAOT, is the next piece of work:

- **README install docs for native binaries**, per OS:
  - macOS arm64 and x64 (download, verify `SHA256SUMS`, clear the quarantine attribute)
  - Linux x64 and arm64 (`libssl` and `ca-certificates`, glibc 2.34 or later)
  - Windows x64
- **Why choose the native build.** It needs no .NET runtime, and it suits CI runners and containers. Startup is 8.6 ms against 38 ms, CPU per call is about 6x lower, and memory is about half. It is also a good base for experiments.
- **Homebrew tap and Scoop bucket.** Should `solrevdev/homebrew-tap` and a Scoop bucket be created, with a fine-grained PAT? The name clashes with koguchic/ytx (`ytx-cli`).
- **More platforms:** win-arm64 and linux-musl.
- **A possible 2.0** using .NET 10's RID-specific tool package, which fails on SDK 8.
- **Before merging:** `native.yml` must already be on `master` before the dispatch step can find it. Merging #6 touches `src/Ytx/**`, so it publishes a NuGet patch unless `[skip ci]` is used.

## 9. For the post

### Possible titles

- "My own tool picked Afrikaans: reviewing ytx a year on"
- "From 'grab a transcript' to 1.1.0 in an evening"
- "ytx 1.1.0: the right captions, a real contract, and exit codes that mean something"
- "Handing off between AI coding sessions: a ytx case study"

### Suggested outline

1. **Hook.** The October 2025 post called the v1.0.7 caption ordering "Intelligent Caption Selection". A year later that line was bug 1 (#7, "The hook").
2. **How the review ran.** Three parallel agents and an 89-run live test harness (#7).
3. **The bugs, with evidence.** Afrikaans, Chinese and Azerbaijani; 8,144 lines against 4,773; silent fallbacks.
4. **v1.0.8.** Source-generated JSON as the enabler for AOT (#5, #4).
5. **The fix and its trap.** Rank design, and the Afrikaans fallback (section 5).
6. **Designing a JSON contract.** `captionStatus`, exit 3, stable keys, and why the breaking change was fine now.
7. **Small things that matter.** Timeout, Ctrl+C, `&nbsp;`, help text.
8. **Formats, segments, proxies.** What agents and CI need.
9. **CI and packaging.** SHA pins, the LFS icon trap, symbols.
10. **Workflow.** Session handoff, stacked PRs, one release with `[skip ci]` and a manual minor run.
11. **What we left out and why.** MCP, deduplication, branch protection.
12. **Next: NativeAOT.**

### Commands to reproduce

```bash
# Install the release
dotnet tool update -g solrevdev.ytx
ytx --version

# Bugs 1 and 2, fixed: default and short-code language choice
ytx iG9CE55wbtY | jq '{captionStatus, captionLanguage, captionIsAutoGenerated}'
for l in fr es ja zh xx; do ytx -l "$l" -c iG9CE55wbtY | jq -r '.captionLanguageName'; done

# Bug 3: the fallback warning on stderr
ytx -l xx iG9CE55wbtY > /dev/null

# Track list, formats and segments
ytx --list-languages iG9CE55wbtY | jq '.tracks | length'
ytx -f srt iG9CE55wbtY | head
ytx --segments -c iG9CE55wbtY | jq '.segments | length'

# Manual track on the 5h video (expect 4,773 lines)
ytx NYFGCESmikA | jq -r '.transcript' | wc -l

# Proxy is honoured (expect "Connection refused", exit 1)
ytx --proxy http://127.0.0.1:9 iG9CE55wbtY; echo "exit $?"

# Timeout and Ctrl+C
ytx --timeout 0.05 iG9CE55wbtY; echo "exit $?"     # exit 1, "Timed out"
perl -e '$SIG{INT}="DEFAULT"; exec @ARGV' ytx NYFGCESmikA >/dev/null & p=$!; sleep 0.4; kill -INT $p; wait $p; echo "exit $?"   # 130

# From source
git clone https://github.com/solrevdev/solrevdev.ytx && cd solrevdev.ytx
dotnet test tests/Ytx.Tests -c Release

# NativeAOT (PR #6 branch)
git switch feat/native-aot
dotnet publish src/Ytx -c Release -f net10.0 -r osx-arm64 -p:PublishAot=true -o out
scripts/aot/smoke-test.sh out/ytx
```
