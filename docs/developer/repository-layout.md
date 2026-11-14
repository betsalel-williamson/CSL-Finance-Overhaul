# Repository layout

| Path                                  | Role                                                       |
| ------------------------------------- | ---------------------------------------------------------- |
| `docs/`                               | Merlon Docs shards (features, client, developer, glossary) |
| `src/FinanceOverhaul.Market/`         | Stock math / probe helpers (Cities-free)                   |
| `mod/`                                | CSL mod surface — Host, Harmony, Options                   |
| `tests/FinanceOverhaul.Market.Tests/` | Unit + perf for Market library                             |
| `tests/FinanceOverhaul.Mod.Tests/`    | Mod host identity (Cities refs off)                        |
| `scripts/`                            | Install, hooks, layer lint                                 |

## Deploy identity

| Surface               | Name              |
| --------------------- | ----------------- |
| Mods folder           | `FinanceOverhaul` |
| Content Manager title | Finance Overhaul  |
| Assembly              | `FinanceOverhaul` |
