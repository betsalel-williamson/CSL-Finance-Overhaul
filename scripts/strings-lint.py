#!/usr/bin/env python3
"""Fail if code shows the player text that is not in the string table.

Wording belongs in src/FinanceOverhaul.Core/Text/EnglishStrings.cs, read with Strings.Get,
Strings.Format or Strings.Plural, so it can be translated. This checks the mod (all of it
but Dev and Harmony) and the library's wording (Text, Views): a string literal that reads
as prose, or that is given to a label, button, tooltip or Options control, is a finding.

Not text the player reads, so skipped: log lines (ModLog, LogOnce), exceptions, sprite,
shader and component names, and a line marked `// not shown` (say why next to it).
"""
from __future__ import annotations

import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
MOD = ROOT / "mod"
CORE = ROOT / "src" / "FinanceOverhaul.Core"
MOD_SKIPPED = ("Dev", "Harmony", "bin", "obj")
CORE_CHECKED = ("Text", "Views")
# The table itself, and the build identity pasted into bug reports (kept in English).
CORE_SKIPPED = ("EnglishStrings.cs", "BuildInfoText.cs")
MOD_SKIPPED_FILES = ("ModBuildInfo.cs",)

LITERAL = re.compile(r'"((?:[^"\\]|\\.)*)"')
PROSE = re.compile(r"[A-Za-z]{2,}[,.:]? [A-Za-z(₡]")
WORD = re.compile(r"[A-Za-z]{2}")
# A whole statement that never reaches the player.
UNSHOWN_STATEMENT = re.compile(
    r"ModLog\.|LogOnce\(|throw new|new GameObject\(|Shader|CreateUnlitMaterial\(|\.Error\s*="
)
# What a literal is handed to when the player will read it.
SHOWN_TARGET = re.compile(r"\.(text|tooltip)\s*=\s*$")
SHOWN_CALL = re.compile(r"\b(Label|Button|Row|Wrapped|Add(Checkbox|Dropdown|Group|Button|Title|Tab))\(")
TABLE_CALL = re.compile(r"Strings\.(Get|Format|Plural)\(\s*$|\?\s*$|:\s*$")
NAME_TARGET = re.compile(r"(Sprite\w*|\.name|spriteName|Name|Prefix|Key)\s*(=|\+=)\s*$")


def statements(text: str):
    """(first line number, statement text) for each `;`- or brace-ended chunk."""
    start = 1
    chunk: list[str] = []
    for number, line in enumerate(text.splitlines(), start=1):
        stripped = line.strip()
        if stripped.startswith("//") or "// not shown" in line:
            continue
        if not chunk:
            start = number
        chunk.append(line)
        if stripped.endswith((";", "{", "}")):
            yield start, "\n".join(chunk)
            chunk = []
    if chunk:
        yield start, "\n".join(chunk)


def findings(path: pathlib.Path) -> list[str]:
    out = []
    for start, statement in statements(path.read_text(encoding="utf-8")):
        if UNSHOWN_STATEMENT.search(statement):
            continue
        in_table_call = "Strings." in statement
        shown_call = SHOWN_CALL.search(statement)
        for match in LITERAL.finditer(statement):
            value = match.group(1)
            if not WORD.search(value):
                continue
            before = statement[: match.start()]
            after = statement[match.end() :].lstrip()
            if NAME_TARGET.search(before):
                continue
            # An id inside a Strings call: its first argument, or one arm of a `? :` there.
            if in_table_call and (TABLE_CALL.search(before) or re.fullmatch(r"[a-z][\w.]*", value)):
                continue
            joined = before.rstrip().endswith("+") or after.startswith("+")
            shown = (
                PROSE.search(value)
                or SHOWN_TARGET.search(before)
                or (shown_call and not joined and match.start() > shown_call.start())
            )
            if shown:
                line = start + before.count("\n")
                out.append(f'{path.relative_to(ROOT)}:{line}: "{value}"')
    return out


def main() -> int:
    found: list[str] = []
    for path in sorted(MOD.rglob("*.cs")):
        parts = path.relative_to(MOD).parts
        if parts[0] in MOD_SKIPPED or path.name in MOD_SKIPPED_FILES:
            continue
        found.extend(findings(path))
    for folder in CORE_CHECKED:
        for path in sorted((CORE / folder).rglob("*.cs")):
            if path.name not in CORE_SKIPPED:
                found.extend(findings(path))

    if found:
        print("Text shown to the player must come from the string table:", file=sys.stderr)
        for row in found:
            print(f"  {row}", file=sys.stderr)
        return 1

    print("strings-lint: ok (the mod and the library's wording use the string table)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
