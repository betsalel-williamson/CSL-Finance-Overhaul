#!/usr/bin/env bash
# Shared pre-push entry (husky + manual / agents: npm run hooks:pre-push).
# Always: layer lint, market unit+perf tests, docs.
# main (or HOOKS_FORCE_FULL=1): also format:check + mod build.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
# shellcheck source=ensure-tool-path.sh
source "$ROOT/scripts/ensure-tool-path.sh"
cd "$ROOT"

branch="$(git rev-parse --abbrev-ref HEAD 2>/dev/null || printf 'unknown')"
printf '==> pre-push on branch %s\n' "$branch"

echo "==> pre-push: layer-lint"
npm run layer:lint

echo "==> pre-push: test:unit (Core: calculations + perf)"
npm run test:unit

if [[ "${HOOKS_FORCE_FULL:-0}" == "1" || "$branch" == "main" ]]; then
  echo "==> pre-push: build:mod (needs the game; skipped without it)"
  npm run build:mod
  echo "==> pre-push: format:check"
  npm run format:check
fi

echo "==> pre-push: docs"
npm run docs
echo "==> pre-push: ok"
