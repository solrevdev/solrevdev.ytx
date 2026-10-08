# Investigations

Write-ups of performance and design experiments: the question, how it was measured, the results and
what was decided. Each one says whether its recommendations have been acted on.

| Date | Topic | Status |
|---|---|---|
| October 2026 | [NativeAOT and allocations](2026-10-native-aot-and-allocations.md) | Exploratory, not yet decided |

Supporting code lives next to the code it measures:

- [`benchmarks/Ytx.Benchmarks/`](../../benchmarks/Ytx.Benchmarks/): BenchmarkDotNet project and output verifier.
- [`scripts/aot/`](../../scripts/aot/): publish, timing and smoke-test scripts for native builds.
- [`packaging/`](../../packaging/): Homebrew and Scoop templates for native binaries.
