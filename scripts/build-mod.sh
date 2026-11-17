#!/usr/bin/env bash
# Compile the mod (the Cities-facing project) without installing it. The mod only builds
# against the game's assemblies, so this is skipped where the game is not installed (CI).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="${HOME}/.dotnet:${PATH}"
MANAGED="${CitiesManaged:-${HOME}/Library/Application Support/Steam/steamapps/common/Cities_Skylines/Cities.app/Contents/Resources/Data/Managed}"
if [[ ! -f "${MANAGED}/ICities.dll" ]]; then
  echo "build:mod skipped: Cities: Skylines not found at ${MANAGED} (set CitiesManaged to build the mod)."
  exit 0
fi
dotnet build "${ROOT}/mod/FinanceOverhaul.csproj" -c Release --nologo --verbosity minimal \
  "-p:CitiesManaged=${MANAGED}" -p:SkipModDeploy=true
echo "build:mod: ok"
