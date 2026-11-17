#!/usr/bin/env bash
# Build the release (non-dev) mod and zip it for a GitHub Release.
# Usage: scripts/package-release.sh            -> dist/FinanceOverhaul-<package.json version>.zip
# Then:  gh release create v<version> dist/FinanceOverhaul-<version>.zip --notes-file <notes>
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="$(node -p "require('$ROOT/package.json').version")"
BIN="$ROOT/mod/bin/Release/net35"
OUT="$ROOT/dist"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

bash "$ROOT/scripts/install-mod-local.sh" >/dev/null # release build (no --dev), also installs locally

mkdir -p "$STAGE/FinanceOverhaul" "$OUT"
for f in FinanceOverhaul.dll FinanceOverhaul.Core.dll CitiesHarmony.API.dll; do
  [[ -f "$BIN/$f" ]] || { echo "Missing $BIN/$f" >&2; exit 1; }
  cp "$BIN/$f" "$STAGE/FinanceOverhaul/"
done
cp "$ROOT/mod/PreviewImage.png" "$STAGE/FinanceOverhaul/"

ZIP="$OUT/FinanceOverhaul-$VERSION.zip"
rm -f "$ZIP"
(cd "$STAGE" && zip -qr "$ZIP" FinanceOverhaul)
echo "Packaged $ZIP"
unzip -l "$ZIP"
