# Investigations

Write-ups of reviews and of performance and design experiments: the question, how it was measured, the results and
what was decided. Each one says whether its recommendations have been acted on.

| Date | Topic | Status |
|---|---|---|
| October 2026 | [NativeAOT and allocations](2026-10-native-aot-and-allocations.md) | Source generation shipped in 1.0.8; native binaries ship with PR #6 |
| October 2026 | [Review of v1.0.7 to the 1.1.0 release](2026-10-ytx-review-to-1.1.0.md) | Done: issue #3 closed by 1.1.0. Source for a blog post |

Supporting code lives next to the code it measures:

- [`benchmarks/Ytx.Benchmarks/`](../../benchmarks/Ytx.Benchmarks/): BenchmarkDotNet project and output verifier.
- [`scripts/aot/`](../../scripts/aot/): publish, timing and smoke-test scripts for native builds.
- [`packaging/`](../../packaging/): Homebrew and Scoop templates for native binaries.
