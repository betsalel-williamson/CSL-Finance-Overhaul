# ADR 0004 — The project boundary is the game

## Context

The mod project was compiled two ways: with the game's assemblies, and without them so three
identity tests could run in CI. Nearly every file was wrapped in `#if HAS_CITIES`, and the guarded
code was never compiled in CI anyway.

## Decision

1. **Two projects, split by whether they reference Cities.** The library never does. The mod always
   does, has no `#if` guards, and does not build without the game.
2. **Every calculation lives in the library**, market rules and UI calculations (ordering, fitting,
   layout, wording) alike. The mod reads game state, calls the library and draws: the calculation is
   handed to the Cities-facing code, never the other way round.
3. **A mod class that needs a unit test** holds a calculation that belongs in the library.
4. **Tests:** the library's unit tests run everywhere. The mod is checked by `npm run build:mod`
   (compiles it where the game is installed, skipped elsewhere) and by the game UI scenarios.

This replaces item 3 of [ADR 0003](./0003-market-library-vs-mod-surface.md) (the mod test project)
and the `EnableCitiesRefs=false` build named in [ADR 0001](./0001-reflective-stock-hooks.md).

## Consequences

CI cannot compile the mod; the pre-push hook and `npm run hooks:agent-validate` do it on a machine
with the game. One place for logic, one test project, no guards.
