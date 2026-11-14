# Live reload

Cities can reload a mod when `AssemblyVersion` changes (Paradox Advanced Mod Setup → Automate).

## How this repo does it

1. `AssemblyVersion` uses `Major.Minor.*` with non-deterministic builds so each compile gets a new
   build/revision ([ADR 0002](../features/adr/0002-live-reload-identity.md)).
2. `./scripts/install-mod-local.sh --dev` sets `DevBuildIdentity=true` so Options show the assembly
   identity and build timestamp.
3. Rebuild and reinstall; Automate picks up the new DLL. If Automate is off, restart Cities or
   toggle the mod.

## Sanity check

Options title with `--dev` should change build/revision between installs. If it does not, you are
looking at a stale Mods copy.
