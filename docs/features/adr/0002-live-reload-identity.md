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

## Consequences

Local loops are `build → copy → auto-reload`. Release builds keep clean semver chrome. Contributors
compare identity strings when diagnosing stale DLLs.
