#!/usr/bin/env bash
# Publishes ytx in several deployment shapes so their size and startup can be compared.
#
# Usage: scripts/aot/publish-variants.sh [variant...]
#   Variants: fdd fdd-r2r sc-trim sc-trim-r2r aot aot-size   (default: all)
#
# Environment:
#   REPO   repository root to build from        (default: the git root of this script)
#   OUT    output folder, one subfolder/variant  (default: $REPO/artifacts/aot)
#   RID    runtime identifier                    (default: detected from this machine)
#   TFM    target framework                      (default: net10.0)
#   PREFIX label prefix for the subfolders       (default: empty, e.g. "v1.0.7-")
#
# Each variant is published to $OUT/<prefix><variant>/ with its log in $OUT/logs/.
# Trim and AOT warnings are printed after each publish. They are expected on v1.0.7 (IL2026/IL3050).
set -euo pipefail

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO=${REPO:-$(git -C "$script_dir" rev-parse --show-toplevel)}
OUT=${OUT:-$REPO/artifacts/aot}
TFM=${TFM:-net10.0}
PREFIX=${PREFIX:-}
PROJECT=$REPO/src/Ytx

detect_rid() {
  local os arch
  case "$(uname -s)" in
    Darwin) os=osx ;;
    Linux) os=linux ;;
    MINGW* | MSYS* | CYGWIN*) os=win ;;
    *) echo "Unsupported OS: $(uname -s)" >&2; return 1 ;;
  esac
  case "$(uname -m)" in
    arm64 | aarch64) arch=arm64 ;;
    x86_64 | amd64) arch=x64 ;;
    *) echo "Unsupported architecture: $(uname -m)" >&2; return 1 ;;
  esac
  echo "$os-$arch"
}
RID=${RID:-$(detect_rid)}

publish() {
  local name=$1; shift
  local dest=$OUT/$PREFIX$name log=$OUT/logs/$PREFIX$name.log
  rm -rf "$dest"
  echo "== $PREFIX$name ($RID, $TFM)"
  # NU1902 is the known AngleSharp advisory from YoutubeExplode 6.5.6 and is not about publishing.
  if ! dotnet publish "$PROJECT" -c Release -f "$TFM" -o "$dest" "$@" 2>&1 \
      | grep -v NU1902 | sed "s|$REPO/||g" > "$log"; then
    echo "   publish failed, see $log" >&2
    return 1
  fi
  grep -E "warning (IL|NETSDK)|error" "$log" | sort -u || true
  du -sh "$dest" | sed 's/^/   size: /'
}

mkdir -p "$OUT/logs"
variants=("$@")
[[ ${#variants[@]} -eq 0 ]] && variants=(fdd fdd-r2r sc-trim sc-trim-r2r aot aot-size)

for v in "${variants[@]}"; do
  case $v in
    fdd)         publish fdd --no-self-contained ;;
    fdd-r2r)     publish fdd-r2r --no-self-contained -r "$RID" -p:PublishReadyToRun=true ;;
    sc-trim)     publish sc-trim --self-contained -r "$RID" -p:PublishSingleFile=true -p:PublishTrimmed=true \
                   -p:InvariantGlobalization=true ;;
    sc-trim-r2r) publish sc-trim-r2r --self-contained -r "$RID" -p:PublishSingleFile=true -p:PublishTrimmed=true \
                   -p:InvariantGlobalization=true -p:PublishReadyToRun=true -p:EnableCompressionInSingleFile=false ;;
    # TrimmerSingleWarn=false lists warnings per call site instead of one line per assembly.
    aot)         publish aot -r "$RID" -p:PublishAot=true -p:InvariantGlobalization=true -p:TrimmerSingleWarn=false ;;
    aot-size)    publish aot-size -r "$RID" -p:PublishAot=true -p:InvariantGlobalization=true \
                   -p:OptimizationPreference=Size -p:UseSystemResourceKeys=true -p:StackTraceSupport=false ;;
    *) echo "Unknown variant: $v" >&2; exit 2 ;;
  esac
done
