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

## Dev captures (`--dev` builds)

Trigger files next to `FinanceOverhaul.settings.txt`
(`~/Library/Application Support/Colossal Order/Cities_Skylines/` on macOS) let you check visuals
without touching the game window:

| Trigger file              | Effect                                                                                                                                      |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| `FinanceOverhaul.capture` | Saves the rendered frame to `FinanceOverhaul-capture.png`.                                                                                  |
| `FinanceOverhaul.focus`   | Frames the facade of Stock Exchange _k_ of _n_ in the loaded city (text: optional `k`, default 1; add `capture` to save a frame after 3 s). |

If the city has no Stock Exchange, `focus` logs `dev focus: this city has no Stock Exchange` and
leaves the camera alone. Example:

```bash
echo "1 capture" > ~/Library/Application\ Support/Colossal\ Order/Cities_Skylines/FinanceOverhaul.focus
```
