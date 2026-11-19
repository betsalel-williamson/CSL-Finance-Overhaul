#!/usr/bin/env python3
"""Fail if the mod names a colour anywhere but FoTheme.

Every colour the mod draws is a named entry in mod/Kit/FoTheme.cs (matching the Figma
library's variables where one exists), so a colour changes in one place. A literal colour
elsewhere (`new Color32(255, 255, 255, 255)`, `new Color(1f, 1f, 1f)`, `Color.white`) is a
finding. A colour made from a theme colour (`new Color32(colour.r, colour.g, colour.b, 200)`)
is fine: only numbers in every channel count as a literal.
"""
from __future__ import annotations

import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
MOD = ROOT / "mod"
THEME = MOD / "Kit" / "FoTheme.cs"
SKIPPED_DIRS = ("bin", "obj")

NUM = r"\s*(?:0x[0-9A-Fa-f]+|[0-9.]+f?)\s*"
LITERAL = re.compile(
    rf"new\s+Color(?:32)?\({NUM},{NUM},{NUM}(?:,{NUM})?\)"
    r"|\bColor\.(?:white|black|red|green|blue|yellow|cyan|magenta|gray|grey|clear)\b"
)


def main() -> int:
    findings = []
    for path in sorted(MOD.rglob("*.cs")):
        rel = path.relative_to(ROOT)
        if path == THEME or any(part in SKIPPED_DIRS for part in rel.parts):
            continue
        for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            if LITERAL.search(line):
                findings.append(f"{rel}:{number}: {line.strip()}")
    if findings:
        print("Colours belong in mod/Kit/FoTheme.cs; name this one there:", file=sys.stderr)
        for row in findings:
            print(f"  {row}", file=sys.stderr)
        return 1
    print("colour-lint: ok (every colour is named in FoTheme)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
