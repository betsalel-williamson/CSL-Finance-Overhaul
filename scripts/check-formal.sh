#!/usr/bin/env bash
# Runs the Alloy models under formal/ and compares each command's outcome with its "expect":
# expect 0 = no counterexample (the promise holds), expect 1 = a counterexample / instance is
# expected (a known gap or a reachability run). Exit 1 on any surprise.
# Usage: scripts/check-formal.sh [formal/saves.als]   (needs Java; Alloy 6 jar via FO_ALLOY_JAR)
set -euo pipefail
cd "$(dirname "$0")/.."
JAR="${FO_ALLOY_JAR:-$HOME/.cache/factory/org.alloytools.alloy.dist.jar}"
[ -f "$JAR" ] || { echo "Alloy jar not found at $JAR (set FO_ALLOY_JAR)"; exit 2; }
MODEL="${1:-formal/saves.als}"
OUT="$(mktemp -d)"
trap 'rm -rf "$OUT"' EXIT
java -jar "$JAR" commands "$MODEL" 2>/dev/null | grep -E '^[0-9]+ *\.' >"$OUT/commands.txt"
failed=0
while IFS= read -r line; do
  idx="${line%%.*}"; idx="${idx// /}"
  name="$(echo "$line" | awk '{print $4}')"
  expect="$(grep -E "^(check|run) $name\b" -A6 "$MODEL" | grep -oE 'expect [01]' | head -1 | awk '{print $2}')"
  start=$SECONDS
  java --enable-native-access=ALL-UNNAMED -jar "$JAR" exec -f -q -s glucose -c "$idx" -t text \
    -o "$OUT/$idx" "$MODEL" >"$OUT/$idx.log" 2>&1 || true
  if ls "$OUT/$idx" 2>/dev/null | grep -q solution; then found=1; else found=0; fi
  verdict="ok"
  if [ "$found" != "$expect" ]; then verdict="SURPRISE"; failed=1; fi
  case "$found" in 1) what="counterexample/instance" ;; *) what="none found" ;; esac
  printf '%-9s %-40s expect %s, %-24s %4ss\n' "$verdict" "$name" "$expect" "$what" "$((SECONDS - start))"
done <"$OUT/commands.txt"
exit "$failed"
