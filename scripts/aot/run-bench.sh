#!/usr/bin/env bash
# Benchmarks the binaries produced by publish-variants.sh.
#
# Usage: scripts/aot/run-bench.sh startup|network|rusage
#   startup  --help, --version and the stdin JSON error path. No network. 30 rounds after 3 warm-ups.
#   network  --metadata-only and a full run against $VIDEO. $ROUNDS rounds, 1 warm-up.
#   rusage   one --metadata-only run per binary under /usr/bin/time (peak RSS, instructions on macOS).
#
# Environment:
#   REPO     repository root                     (default: the git root of this script)
#   OUT      folder publish-variants.sh wrote to (default: $REPO/artifacts/aot)
#   VIDEO    video ID for network runs            (default: aCi6BhrvjhM, a 17-minute video)
#   ROUNDS   rounds for network mode              (default: 8)
#   EXTRA    extra "label=/path/to/binary" pairs, space-separated, e.g. the installed tool shim:
#            EXTRA="tool-shim=$HOME/.dotnet/tools/ytx"
#
# Every binary found under $OUT/*/Ytx (or Ytx.exe) is included, labelled by its folder name.
# Results are printed and saved to $OUT/results/<mode>.txt.
set -euo pipefail

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO=${REPO:-$(git -C "$script_dir" rev-parse --show-toplevel)}
OUT=${OUT:-$REPO/artifacts/aot}
VIDEO=${VIDEO:-aCi6BhrvjhM}
ROUNDS=${ROUNDS:-8}
mode=${1:-}

variants=()
for bin in "$OUT"/*/Ytx "$OUT"/*/Ytx.exe; do
  [[ -x $bin ]] && variants+=("$(basename "$(dirname "$bin")")=$bin")
done
# shellcheck disable=SC2206 # EXTRA is a deliberate space-separated list.
[[ -n ${EXTRA:-} ]] && variants+=($EXTRA)
if [[ ${#variants[@]} -eq 0 ]]; then
  echo "No binaries under $OUT. Run scripts/aot/publish-variants.sh first." >&2
  exit 1
fi

mkdir -p "$OUT/results"
bench() { python3 "$script_dir/bench_rr.py" "$@"; }

case $mode in
  startup)
    {
      bench 30 3 "${variants[@]}" -- --help
      bench 30 3 "${variants[@]}" -- --version
      # Fails ID validation before any network call, so it times the stdin and JSON parse path. Exit 2.
      BENCH_STDIN='{"url":"not a video!!"}' bench 30 3 "${variants[@]}" --
    } | tee "$OUT/results/startup.txt"
    ;;
  network)
    {
      bench "$ROUNDS" 1 "${variants[@]}" -- --metadata-only "$VIDEO"
      bench "$ROUNDS" 1 "${variants[@]}" -- "$VIDEO"
    } | tee "$OUT/results/network.txt"
    ;;
  rusage)
    if [[ $(uname -s) == Darwin ]]; then time_flag=-l; else time_flag=-v; fi
    {
      for pair in "${variants[@]}"; do
        echo "== ${pair%%=*}"
        /usr/bin/time "$time_flag" "${pair#*=}" --metadata-only --compact "$VIDEO" 2>&1 >/dev/null \
          | grep -E "real|maximum resident|Maximum resident|instructions retired|peak memory|Elapsed" || true
      done
    } | tee "$OUT/results/rusage.txt"
    ;;
  *)
    sed -n '2,20p' "$0"
    exit 2
    ;;
esac
