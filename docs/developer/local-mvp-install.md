# Local MVP install

Deploy the mod into the Cities Mods folder. Last install wins.

Cloud scaffolds stop before this step — use a [local game loop](./local-game-loop.md) clone on a
machine that has Cities installed.

## Deploy

From the repository root:

```bash
./scripts/install-mod-local.sh
./scripts/install-mod-local.sh --dev   # assembly identity + Built footer
```

Requires Cities managed assemblies (`ICities.dll`). Override paths with `CitiesManaged` /
`CITIES_MODS`.

| Path                             | Role                                  |
| -------------------------------- | ------------------------------------- |
| `./scripts/install-mod-local.sh` | Build + copy → `Mods/FinanceOverhaul` |

Enable Cities Harmony, enable Finance Overhaul, load a city with a Stock Exchange, then verify
Options and the Investments panel.
