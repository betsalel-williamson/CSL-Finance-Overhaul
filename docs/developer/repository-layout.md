# Repository layout

| Path                                | Role                                                          |
| ----------------------------------- | ------------------------------------------------------------- |
| `docs/`                             | Merlon Docs shards (features, client, developer, glossary)    |
| `src/FinanceOverhaul.Core/`         | Every calculation; never references Cities                    |
| `tests/FinanceOverhaul.Core.Tests/` | Unit and perf tests for the library                           |
| `mod/`                              | The mod: hooks, save data, drawing; builds only with the game |
| `tests/game-ui/`                    | Scenarios run in the game by `scripts/game-ui-test.sh`        |
| `scripts/`                          | Install, release, hooks, layer lint                           |

The two projects are split by whether they reference the game
([ADR 0004](../features/adr/0004-project-boundary-is-the-game.md)). The mod reads game state, calls
the library and draws; a calculation worth a unit test belongs in the library.

## Library folders (`src/FinanceOverhaul.Core/`)

One flat namespace, `FinanceOverhaul.Core`; the folders only group files.

| Folder      | Holds                                                                                |
| ----------- | ------------------------------------------------------------------------------------ |
| `Market/`   | Rules: orders, bonds, the Index, price bias, cost basis, ticker feed, symbols        |
| `Views/`    | What a window shows: Stock Monitor rows, sorting and filters, stock detail           |
| `Text/`     | Wording: the string table (`Strings`, `EnglishStrings`), order, bond and notice text |
| `Layout/`   | UI calculations: label fit, scroll fade, sparkline, caret, slider thumb, LED text    |
| `Settings/` | Options and their file format                                                        |

## Mod folders (`mod/`)

| Folder         | Holds                                                                                                                                                                                                                        |
| -------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| (root)         | What Cities calls: `Mod`, Options page, settings file, build info, loading extension                                                                                                                                         |
| `Harmony/`     | Every Harmony patch: `Patcher` applies and removes them, `Patcher.*` hold the hooks by feature; `GameInternals` holds every private game member reached by reflection                                                        |
| `Kit/`         | Reusable UI, one class per Figma component: `FoTheme` (colours), `FoWindow` (title bar, opacity), `FoButton`, `FoPill`, `FoCheckbox`, `FoText` (labels, fitting, heavy weight), `FoSparkline`, `FoPointer`; knows no feature |
| `Exchange/`    | The Stock Exchange building: LED boards and the ticker feed                                                                                                                                                                  |
| `Windows/`     | The mod's own windows: Stock Monitor, Welcome back                                                                                                                                                                           |
| `Investments/` | Changes inside the game's Economy panel: order ticket, typed share amount, Your Portfolio cards, share charts, City bonds section                                                                                            |
| `Engines/`     | No UI, owns save data: order desk, bond desk, Index listing, symbols, Chirper notices                                                                                                                                        |
| `Dev/`         | Dev builds only: game UI test runner (`UiTestRunner.cs` runs a scenario; its commands are in `UiTestRunner.*.cs`, one file per group), captures, test stock, stress stocks                                                   |

Surfaces and engines use the Kit and the library; the Kit uses only the library.

## Deploy identity

| Surface               | Name              |
| --------------------- | ----------------- |
| Mods folder           | `FinanceOverhaul` |
| Content Manager title | Finance Overhaul  |
| Assembly              | `FinanceOverhaul` |
