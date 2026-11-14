# ADR 0001 — Reflective stock hooks

**Status:** superseded (2026-11-14). The reflective discovery, its catch-all price postfix and the
MVP probe bias option were removed. The mod now patches named vanilla members only (Index listing,
typed share field, live ticker), each failing soft on its own. Old settings files with
`probeBiasEnabled` still load; the key is ignored.

## Context

Financial Districts stock types live in `Assembly-CSharp` and differ by game build. Hard-coding
every method name would break CI environments without Cities managed assemblies and would couple the
MVP to one decompile snapshot.

## Decision

Discover candidate stock / investment types and update methods with reflection at enable time. Apply
Harmony patches only when a safe target is found. Keep price-bias math in a pure helper so tests run
with `EnableCitiesRefs=false`.

## Consequences

Hooks degrade gracefully when the DLC is absent. Contributors can iterate on bias and fund formulas
without a full Cities install. Stronger typed bindings can replace reflection once a stable surface
is confirmed in-game.
