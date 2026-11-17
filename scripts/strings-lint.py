#!/usr/bin/env python3
"""Fail if migrated mod UI code shows text that is not in the string table.

Wording belongs in src/FinanceOverhaul.Core/Text (Strings / EnglishStrings) so it can be
translated. Folders listed in MIGRATED must not assign a literal to text the player reads;
the rest of the mod is counted, and the count may only go down (PENDING_MAX).
"""
from __future__ import annotations

import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
MOD = ROOT / "mod"
MIGRATED = ("Windows", "Kit")
SKIPPED = ("Dev", "Harmony", "bin", "obj")
# Literals still to move, outside the migrated folders. Lower it as files are migrated.
PENDING_MAX = 38

# A literal with at least two letters in a row, in a place the player reads.
SHOWN = re.compile(
    r"""(\.(text|tooltip)\s*=\s*[^;]*"[^"]*[A-Za-z]{2}[^"]*")"""
    r"""|(\bLabel\([^;]*"[^"]*[A-Za-z]{2}[^"]*[ ][^"]*")"""
    r"""|(\bAdd(Checkbox|Dropdown|Group|Button|Title)\(\s*"[^"]*[A-Za-z]{2})"""
    r"""|(^\s*"[^"]*[A-Za-z]{3,}[ ][^"]*[A-Za-z]{2}[^"]*",?\s*$)"""
)
IGNORED = re.compile(r"ModLog\.|Sprite|\.name\s*=|Prefix\s*\+|^\s*//|Strings\.(Get|Format|Plural)\(")


def findings(path: pathlib.Path) -> list[str]:
    out = []
    for i, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        if SHOWN.search(line) and not IGNORED.search(line):
            out.append(f"{path.relative_to(ROOT)}:{i}: {line.strip()}")
    return out


def main() -> int:
    migrated: list[str] = []
    pending: list[str] = []
    for path in sorted(MOD.rglob("*.cs")):
        parts = path.relative_to(MOD).parts
        if parts[0] in SKIPPED:
            continue
        (migrated if parts[0] in MIGRATED else pending).extend(findings(path))

    if migrated:
        print("Text shown to the player must come from the string table:", file=sys.stderr)
        for row in migrated:
            print(f"  {row}", file=sys.stderr)
        return 1

    if len(pending) > PENDING_MAX:
        print(
            f"{len(pending)} literal texts outside the migrated folders, more than the "
            f"{PENDING_MAX} still to move. New text goes in the string table:",
            file=sys.stderr,
        )
        for row in pending:
            print(f"  {row}", file=sys.stderr)
        return 1

    print(
        f"strings-lint: ok ({', '.join(MIGRATED)} use the string table; "
        f"{len(pending)} literal texts still to move, limit {PENDING_MAX})"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
