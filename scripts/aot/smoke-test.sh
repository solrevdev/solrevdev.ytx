#!/usr/bin/env bash
# Offline smoke test for a ytx binary. Needs no network and no .NET runtime.
#
# Usage: scripts/aot/smoke-test.sh /path/to/ytx [expected-version]
#
# Checks --version, --help and the usage-error exit codes, including the stdin JSON path.
# That path deserialises JSON, so it fails if a NativeAOT build still uses reflection-based
# System.Text.Json (the binary aborts instead of exiting 2).
set -uo pipefail

bin=${1:?usage: smoke-test.sh /path/to/ytx [expected-version]}
expected_version=${2:-}
failures=0

pass() { echo "ok    $1"; }
fail() { echo "FAIL  $1" >&2; failures=$((failures + 1)); }

expect_exit() {
  local want=$1 desc=$2; shift 2
  local got
  "$@" >/dev/null 2>&1 </dev/null
  got=$?
  if [[ $got -eq $want ]]; then pass "$desc (exit $got)"; else fail "$desc: expected exit $want, got $got"; fi
}

expect_stdin_exit() {
  local want=$1 desc=$2 input=$3
  local got
  printf '%s' "$input" | "$bin" >/dev/null 2>&1
  got=$?
  if [[ $got -eq $want ]]; then pass "$desc (exit $got)"; else fail "$desc: expected exit $want, got $got"; fi
}

if [[ ! -x $bin ]]; then
  echo "Not an executable file: $bin" >&2
  exit 1
fi

if version_out=$("$bin" --version </dev/null 2>&1); then
  pass "--version prints '$version_out'"
else
  fail "--version exited non-zero: $version_out"
fi
if [[ -n $expected_version ]]; then
  # tr strips the CR that Windows console output adds.
  if [[ $(printf '%s' "$version_out" | tr -d '\r') == "ytx $expected_version" ]]; then
    pass "--version matches $expected_version"
  else
    fail "--version: expected 'ytx $expected_version', got '$version_out'"
  fi
fi

if "$bin" --help </dev/null 2>/dev/null | grep -q "Usage:"; then pass "--help shows usage"; else fail "--help did not show usage"; fi

expect_exit 2 "unknown option" "$bin" --definitely-not-an-option
expect_exit 2 "invalid URL argument" "$bin" "not a video!!"
expect_exit 2 "--help with --version" "$bin" --help --version
expect_exit 2 "no URL and empty stdin" "$bin"
expect_stdin_exit 2 "stdin JSON with invalid URL" '{"url":"not a video!!"}'
expect_stdin_exit 2 "stdin malformed JSON" '{"url":'

if [[ $failures -gt 0 ]]; then
  echo "$failures smoke test(s) failed." >&2
  exit 1
fi
echo "All smoke tests passed."
