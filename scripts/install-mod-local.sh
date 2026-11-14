#!/usr/bin/env bash
# Build FinanceOverhaul.dll and deploy into the local CS1 Mods folder.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="${HOME}/.dotnet:${PATH}"

DEV_IDENTITY=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    -h | --help)
      echo "Usage: $0 [--dev|-d]"
      echo "  Build + post-build copy into Mods/FinanceOverhaul."
      echo "  --dev | -d   Show assembly build/revision + Built footer (live-reload QA)"
      exit 0
      ;;
    --dev | -d)
      DEV_IDENTITY=1
      shift
      ;;
    *)
      echo "Unknown option: $1" >&2
      echo "Usage: $0 [--dev|-d]" >&2
      exit 1
      ;;
  esac
done

MANAGED="${CitiesManaged:-${HOME}/Library/Application Support/Steam/steamapps/common/Cities_Skylines/Cities.app/Contents/Resources/Data/Managed}"
MODS="${CITIES_MODS:-${HOME}/Library/Application Support/Colossal Order/Cities_Skylines/Addons/Mods}"
DEST="${MODS}/FinanceOverhaul"
CSPROJ="${ROOT}/mod/FinanceOverhaul.csproj"

if [[ ! -f "${CSPROJ}" ]]; then
  echo "Missing project: ${CSPROJ}" >&2
  exit 2
fi

if [[ ! -f "${MANAGED}/ICities.dll" ]]; then
  echo "ICities.dll not found at: ${MANAGED}" >&2
  echo "Set CitiesManaged to your Cities_Data/Managed (or Contents/Resources/Data/Managed) path." >&2
  exit 1
fi

BUILD_ARGS=(
  "${CSPROJ}"
  -c Release
  "-p:CitiesManaged=${MANAGED}"
  "-p:CitiesMods=${MODS}"
)
if [[ "${DEV_IDENTITY}" == "1" ]]; then
  BUILD_ARGS+=("-p:DevBuildIdentity=true")
fi

dotnet build "${BUILD_ARGS[@]}"

mkdir -p "${DEST}"
echo "Build finished (post-build should have deployed to ${DEST})."
if [[ ! -f "${DEST}/FinanceOverhaul.Market.dll" ]]; then
  echo "Missing ${DEST}/FinanceOverhaul.Market.dll — Content Manager needs the Market library beside the mod DLL." >&2
  exit 1
fi
echo "Cities auto-reloads when AssemblyVersion changes — see docs/developer/live-reload.md"
if [[ "${DEV_IDENTITY}" == "1" ]]; then
  echo "Dev identity: Options title shows assembly build/revision; Built footer on."
else
  echo "Options title: product semver (use --dev for assembly build/revision)."
fi
echo "Inspect: tail -f \"\${TMPDIR:-/tmp}/finance-overhaul.log\""
echo "Reload/restart Cities to pick up the new DLL if Automate is off."
