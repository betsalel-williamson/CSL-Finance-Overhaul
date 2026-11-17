# Repository layout

| Path                                  | Role                                                       |
| ------------------------------------- | ---------------------------------------------------------- |
| `docs/`                               | Merlon Docs shards (features, client, developer, glossary) |
| `src/FinanceOverhaul.Market/`         | Stock, index and ticker math (Cities-free)                 |
| `mod/`                                | The mod: hooks, save data, UI (builds only with the game)  |
| `tests/FinanceOverhaul.Market.Tests/` | Unit + perf for Market library                             |
| `scripts/`                            | Install, hooks, layer lint                                 |

## Deploy identity

| Surface               | Name              |
| --------------------- | ----------------- |
| Mods folder           | `FinanceOverhaul` |
| Content Manager title | Finance Overhaul  |
| Assembly              | `FinanceOverhaul` |
