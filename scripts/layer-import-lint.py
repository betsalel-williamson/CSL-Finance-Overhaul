#!/usr/bin/env python3
"""Fail if the Core library imports Cities / Harmony / Unity types."""
from __future__ import annotations

import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
CORE = ROOT / "src" / "FinanceOverhaul.Core"

FORBIDDEN = re.compile(
    r"\b(ICities|HarmonyLib|CitiesHarmony|UnityEngine|ColossalFramework|ColossalManaged|SimulationManager)\b"
)


def main() -> int:
    if not CORE.is_dir():
        print(f"missing core library: {CORE}", file=sys.stderr)
        return 2

    bad: list[str] = []
    for path in sorted(CORE.rglob("*.cs")):
        text = path.read_text(encoding="utf-8")
        for i, line in enumerate(text.splitlines(), start=1):
            if FORBIDDEN.search(line):
                bad.append(f"{path.relative_to(ROOT)}:{i}: {line.strip()}")

    if bad:
        print("The Core library must stay Cities-free:", file=sys.stderr)
        for row in bad:
            print(f"  {row}", file=sys.stderr)
        return 1

    print("layer-import-lint: ok (FinanceOverhaul.Core is Cities-free)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
