#!/usr/bin/env bash
# Drives the running game's UI through scenario scripts (needs a --dev build loaded in a
# city with a Stock Exchange). Usage: scripts/game-ui-test.sh [tests/game-ui/x.uitest ...]
# Each scenario is copied to the mod's trigger file; the mod runs it and logs
# "uitest: PASS name" or "uitest: FAIL name step N: reason". Exit code 1 if any fail.
set -euo pipefail
DATA="${FO_GAME_DATA:-$HOME/Library/Application Support/Colossal Order/Cities_Skylines}"
LOG="${TMPDIR:-/tmp}/finance-overhaul.log"
TIMEOUT="${FO_UITEST_TIMEOUT:-180}"
cd "$(dirname "$0")/.."
# Scenarios named *-tst-* need the dev test stock (TST, test save only); skip them unless
# FO_TST=1 or they are passed explicitly.
if [ "$#" -eq 0 ]; then
  set --
  for f in tests/game-ui/*.uitest; do
    case "$f" in *-tst-*) [ "${FO_TST:-0}" = "1" ] || continue ;; esac
    set -- "$@" "$f"
  done
fi
[ -f "$LOG" ] || { echo "No mod log at $LOG — is the game running with the --dev build?"; exit 2; }

failed=0
for scenario in "$@"; do
  start=$(wc -c < "$LOG" | tr -d ' ')
  cp "$scenario" "$DATA/FinanceOverhaul.uitest"
  result=""
  end=$((SECONDS + TIMEOUT))
  while [ "$SECONDS" -lt "$end" ]; do
    result=$(tail -c "+$((start + 1))" "$LOG" | grep -E "uitest: (PASS|FAIL)" | head -1 || true)
    [ -n "$result" ] && break
    sleep 1
  done
  tail -c "+$((start + 1))" "$LOG" | grep "uitest: " | grep -vE "uitest: (PASS|FAIL)" | sed 's/^.*uitest: /    /' || true
  if [[ "$result" == *"uitest: PASS"* ]]; then
    echo "PASS  $scenario"
  else
    echo "FAIL  $scenario  ${result#*uitest: FAIL }"
    [ -z "$result" ] && echo "      (timed out after ${TIMEOUT}s; is a city loaded?)"
    failed=1
  fi
done
echo "Captures: $DATA/FinanceOverhaul-uitest-*.png"
exit "$failed"
