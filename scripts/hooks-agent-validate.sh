#!/usr/bin/env bash
# Agentic gate — agents MUST run this (or npm run hooks:agent-validate) before
# claiming a trunk merge is ready. Unit tests need no game; the mod itself is compiled where the game is installed.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
# shellcheck source=ensure-tool-path.sh
source "$ROOT/scripts/ensure-tool-path.sh"
cd "$ROOT"

export HOOKS_FORCE_FULL=1
echo "==> agent-validate: layer-lint"
npm run layer:lint
echo "==> agent-validate: test:unit"
npm run test:unit
echo "==> agent-validate: build:mod (needs the game; skipped without it)"
npm run build:mod
echo "==> agent-validate: format:check"
npm run format:check
echo "==> agent-validate: docs"
npm run docs
echo "==> agent-validate: ok"
