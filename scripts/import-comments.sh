#!/usr/bin/env bash
# Keep the comment on every `using` line current (scripts/ImportComments).
#   scripts/import-comments.sh          rewrite the comments (npm run imports)
#   scripts/import-comments.sh --check  change nothing; exit 1 if any are out of date (npm run imports:check)
# Projects: the Core library and its tests, the tool itself, and the mod when the game is
# installed (the mod only compiles against the game's assemblies; skipped elsewhere, as in CI).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
# shellcheck source=ensure-tool-path.sh
source "$ROOT/scripts/ensure-tool-path.sh"
cd "$ROOT"

MODE="--write"
[[ "${1:-}" == "--check" ]] && MODE="--check"

MANAGED="${CitiesManaged:-${HOME}/Library/Application Support/Steam/steamapps/common/Cities_Skylines/Cities.app/Contents/Resources/Data/Managed}"
PROJECTS=(
  src/FinanceOverhaul.Core/FinanceOverhaul.Core.csproj
  tests/FinanceOverhaul.Core.Tests/FinanceOverhaul.Core.Tests.csproj
  scripts/ImportComments/ImportComments.csproj
)
EXTRA=()
if [[ -f "${MANAGED}/ICities.dll" ]]; then
  PROJECTS+=(mod/FinanceOverhaul.csproj)
  EXTRA=("-p:CitiesManaged=${MANAGED}" -p:SkipModDeploy=true)
else
  echo "imports: mod skipped (Cities: Skylines not found; set CitiesManaged to include it)."
fi

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
JSONS=()
for p in "${PROJECTS[@]}"; do
  name="$(basename "$p" .csproj)"
  # Build first: project references and generated files (BuildInfo.g.cs) must exist.
  dotnet build "$p" --nologo --verbosity quiet "${EXTRA[@]}" >"$WORK/$name.build.log" 2>&1 || {
    cat "$WORK/$name.build.log" >&2
    exit 1
  }
  dotnet msbuild "$p" -nologo "${EXTRA[@]}" -t:ResolveAssemblyReferences \
    -getItem:Compile -getItem:ReferencePath \
    -getProperty:DefineConstants -getProperty:LangVersion \
    -getProperty:MSBuildProjectDirectory -getProperty:IntermediateOutputPath \
    >"$WORK/$name.json"
  JSONS+=("$WORK/$name.json")
done

dotnet run --project scripts/ImportComments/ImportComments.csproj --no-build -- "$MODE" "${JSONS[@]}"
