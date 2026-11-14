#!/usr/bin/env python3
"""Fail if the Market library imports Cities / Harmony / Unity types."""
from __future__ import annotations

import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
MARKET = ROOT / "src" / "FinanceOverhaul.Market"

FORBIDDEN = re.compile(
    r"\b(ICities|HarmonyLib|CitiesHarmony|UnityEngine|ColossalFramework|ColossalManaged|SimulationManager)\b"
)


def main() -> int:
    if not MARKET.is_dir():
        print(f"missing market layer: {MARKET}", file=sys.stderr)
        return 2

    bad: list[str] = []
    for path in sorted(MARKET.rglob("*.cs")):
        text = path.read_text(encoding="utf-8")
        for i, line in enumerate(text.splitlines(), start=1):
            if FORBIDDEN.search(line):
                bad.append(f"{path.relative_to(ROOT)}:{i}: {line.strip()}")

    if bad:
        print("Market layer must stay Cities-free:", file=sys.stderr)
        for row in bad:
            print(f"  {row}", file=sys.stderr)
        return 1

    print("layer-import-lint: ok (FinanceOverhaul.Market is Cities-free)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
