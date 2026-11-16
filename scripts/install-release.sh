#!/usr/bin/env bash
# Replace the local (dev) mod with the published GitHub Release build, ready to Share or Update on
# the Steam Workshop. The dev install is moved to a backup outside the Mods folder.
# Usage: scripts/install-release.sh [tag]      (default: the latest release)
set -euo pipefail
REPO="betsalel-williamson/CSL-Finance-Overhaul"
TAG="${1:-}"
CITIES="${CITIES_USER_DIR:-$HOME/Library/Application Support/Colossal Order/Cities_Skylines}"
MODS="$CITIES/Addons/Mods"
DEST="$MODS/FinanceOverhaul"
BACKUPS="$CITIES/FinanceOverhaul-backups"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

command -v gh >/dev/null || { echo "Needs the GitHub CLI (gh)." >&2; exit 1; }

echo "Downloading ${TAG:-latest} release from $REPO…"
if [[ -n "$TAG" ]]; then
  gh release download "$TAG" --repo "$REPO" --pattern 'FinanceOverhaul-*.zip' --dir "$WORK"
else
  gh release download --repo "$REPO" --pattern 'FinanceOverhaul-*.zip' --dir "$WORK"
fi
ZIP="$(ls "$WORK"/FinanceOverhaul-*.zip | head -1)"
unzip -q "$ZIP" -d "$WORK/x"
for f in FinanceOverhaul.dll FinanceOverhaul.Market.dll CitiesHarmony.API.dll PreviewImage.png; do
  [[ -f "$WORK/x/FinanceOverhaul/$f" ]] || { echo "Release zip is missing $f" >&2; exit 1; }
done

if [[ -d "$DEST" ]]; then
  mkdir -p "$BACKUPS"
  BACKUP="$BACKUPS/FinanceOverhaul-$(date +%Y%m%d-%H%M%S)"
  mv "$DEST" "$BACKUP"
  echo "Moved the existing install to $BACKUP"
fi

mkdir -p "$MODS"
cp -R "$WORK/x/FinanceOverhaul" "$DEST"
echo "Installed $(basename "$ZIP") into $DEST:"
ls -1 "$DEST"
cat <<NEXT

Next (see docs/developer/release-process.md):
  First publish: Content Manager → Mods → Finance Overhaul → Share.
  Later releases: Update the existing Workshop item instead (never Share twice).
To go back to the dev build: scripts/install-mod-local.sh --dev
NEXT
