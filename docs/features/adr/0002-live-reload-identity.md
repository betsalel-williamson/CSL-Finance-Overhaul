# ADR 0002 — Live reload identity

## Context

Cities Automate / Advanced Mod Setup reloads a mod when `AssemblyVersion` changes. Players still
need a stable product semver in Options.

## Decision

Follow the Trackpad Camera Control pattern:

- `AssemblyVersion` = `Major.Minor.*` with `Deterministic=false` so each build gets a new
  build/revision.
- `FileVersion` / product display = package.json semver.
- `DevBuildIdentity` MSBuild flag shows assembly identity and build timestamp in Options when
  installing with `--dev`.

- The same `AssemblyVersion` rule applies to **every assembly the mod ships**
  (`FinanceOverhaul.Market.dll` too). A dependency that keeps a fixed version reloads under the
  same identity, and Mono keeps resolving the first-loaded copy, so the reloaded host calls stale
  code. Guarded by `AssemblyIdentityTests`.
- Host features attach UI lazily and idempotently (for example on first dialog show), remove
  stale objects from older assemblies by name, and undo everything in `OnDisabled`.

## Consequences

Local loops are `build → copy → auto-reload`. Release builds keep clean semver chrome. Contributors
compare identity strings when diagnosing stale DLLs.
