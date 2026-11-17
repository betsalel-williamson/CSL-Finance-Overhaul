# ADR 0003 — Market library vs CSL mod surface

## Context

Stock math, index weights, and ticker layout must be unit-tested and performance- budgeted without
Cities managed assemblies. Harmony and `IUserMod` cannot load in CI.

## Decision

1. **`src/FinanceOverhaul.Core`** owns index, ticker, settings and input rules — no `ICities`,
   Harmony, or UnityEngine.
2. **`mod/`** owns Content Manager entry, Options, Harmony patch wiring, and log/deploy — it
   references the Market library and supplies game assemblies at runtime.
3. **Tests split:** `FinanceOverhaul.Core.Tests` (unit + perf) always run in CI and agent hooks;
   `FinanceOverhaul.Mod.Tests` cover host identity without Cities refs; in-game Stock Exchange
   checks stay on the local game loop.
4. **`scripts/layer-import-lint.py`** fails the gate if Market sources import Cities types.

## Consequences

Agents and CI prove treasury math independently of a local Cities install. Local machines still own
Harmony discovery QA after clone.
