# Agent instructions — Finance Overhaul

Trunk-based development on `main`. Short-lived `cursor/*-5acf` branches; squash-merge when gates are
green.

## Required before merge / “done”

```bash
npm run hooks:agent-validate
```

That runs layer lint, Core unit+perf tests, a build of the mod (where the game is installed),
csharpier check, the import-comments check, and Merlon docs. Do **not** claim merge-ready without
it.

## Import comments

Every `using` line ends with (or sits under) a comment naming what the file takes from that
namespace, and files using the game's global-namespace classes (`LoadingManager`, `EconomyManager`,
…) list them under the usings. Generated, not hand-written: after adding or removing usings or
names, run `npm run imports` (Roslyn, `scripts/ImportComments/`). It also removes usings that bring
in nothing. `npm run imports:check` fails when a comment is out of date.

## Changesets

Every player-visible change since the last release (`v0.1.0`) gets a `.changeset/*.md` in the same
commit: `patch` for a bug fix, `minor` for a feature. One short paragraph in player terms (what was
wrong, what happens now). They become the next release's `CHANGELOG.md` section and are deleted when
it ships.

## Test layers

| Command             | Layer                                   | Cities assemblies              |
| ------------------- | --------------------------------------- | ------------------------------ |
| `npm run test:unit` | `FinanceOverhaul.Core` math/ticker/perf | Never                          |
| `npm run build:mod` | The mod compiles (no tests of its own)  | Required; skipped without them |
| Local game loop     | Harmony + Stock Exchange                | Required on your machine       |

Two projects, split by whether they reference the game. The library (`src/FinanceOverhaul.Core/`,
tests in `tests/FinanceOverhaul.Core.Tests/`) never does: every calculation lives there, market
rules and UI calculations (ordering, fitting, layout) alike, and everything unit tested is there.
The mod (`mod/`) always does: hooks, save data and drawing only, no `#if` guards, and it does not
build without the game. Harmony patches live only in `mod/Harmony/` (`Patcher` and its per-feature
`Patcher.*` files); a patch method there only hands over to the feature's own class. Private game
members are reached only through `GameInternals` in the same folder: no `AccessTools` or
`BindingFlags` anywhere else. A surface (`Windows/`, `Investments/`, `Exchange/`) never calls into
another surface: what two of them share is a `Kit/` component, and the Kit knows no feature. A mod
class that needs a test has a calculation in it that belongs in the library: move it there and have
the mod call it.

## Wording

Text the player reads comes from the string table, never a literal in UI code: add an id and its
English text to `src/FinanceOverhaul.Core/Text/EnglishStrings.cs` and read it with `Strings.Get`,
`Strings.Format` (placeholders) or `Strings.Plural` (counts). One entry per whole sentence, with
`{0}` placeholders, never words glued together: word order differs between languages. The mod
follows the game's language (`mod/GameLanguage.cs`); a language without a table reads as English.
`npm run layer:lint` fails on a literal the player would read anywhere in the mod (outside `Dev/`
and `Harmony/`) or in the library's wording (`Text/`, `Views/`). Log lines stay English literals:
the log is for whoever fixes a bug. A literal that only looks like wording gets `// not shown` and
the reason on its line.

## Colours

Every colour the mod draws is a named entry in `mod/Kit/FoTheme.cs`, matching the Figma library's
variables where one exists; surfaces read `FoTheme.Name`, never `new Color32(…)` with numbers or
`Color.white`. A shade made from a theme colour (its r, g, b with another alpha) is fine.
`npm run layer:lint` (`scripts/colour-lint.py`) fails on a literal colour anywhere else in `mod/`.

## Game UI tests and the test stock (TST)

`scripts/game-ui-test.sh [tests/game-ui/x.uitest]` drives the running game (a `--dev` install,
enabled, city with a Stock Exchange loaded); a rebuild with `scripts/install-mod-local.sh --dev`
reloads the mod live, no restart. Reproduce a UI bug as a scenario first, then fix it. Commands:
`docs/developer/testing.md`.

Real stocks move on their own and depend on what the save holds (a scenario that assumes "the city
owns Oil" fails elsewhere). For a controlled test use the dev **Test Stock (TST)**: Options →
Feature flags → Test Stock, in a test save only. `set-price TST 100` fixes its price from the next
day, `run-days N` advances time, and buying or selling TST gives a known holding, cost basis and
order triggers. Name such scenarios `NN-tst-*.uitest`; they are skipped unless `FO_TST=1` or passed
by path. Bonds have the same control: `bond-finances DEBT INCOME` fixes the debt and annual income
that pricing sees until the scenario ends. A scenario never assumes what the loaded city holds or
earns: it sets the state up itself (buy the share, fix the finances) or reads it from the game
(`$owned:SYMBOL`), and undoes it in `cleanup:`. For manual control without a scenario, write
`buy SHARES LIMIT NAME`, `cancel ID` or `list` to the `FinanceOverhaul.order` trigger file
(`docs/developer/live-reload.md`).

## Git hooks

- **pre-commit** — lint-staged (prettier + csharpier)
- **commit-msg** — commitlint conventional commits
- **pre-push** — layer lint + `test:unit` + docs; on `main` also `build:mod` + format:check

## Docs

Edit Merlon shards under `docs/`, then `npm run docs`. Follow `.agents/skills/merlon-docs/`.

## Figma UI Map

Editing the Finance Overhaul Figma library (UI Map / Foundations / Components): follow
`.agents/skills/figma-fo-ui/` (and `.cursor/rules/figma-fo-ui.mdc`). Pins are red numbered dots on
the screenshot and the new UI (not legend cards). No overlapping pins; hug layout.
