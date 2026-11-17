# Agent instructions — Finance Overhaul

Trunk-based development on `main`. Short-lived `cursor/*-5acf` branches; squash-merge when gates are
green.

## Required before merge / “done”

```bash
npm run hooks:agent-validate
```

That runs layer lint, Market unit+perf tests, a build of the mod (where the game is installed),
csharpier check, and Merlon docs. Do **not** claim merge-ready without it.

## Changesets

Every player-visible change since the last release (`v0.1.0`) gets a `.changeset/*.md` in the same
commit: `patch` for a bug fix, `minor` for a feature. One short paragraph in player terms (what was
wrong, what happens now). They become the next release's `CHANGELOG.md` section and are deleted when
it ships.

## Test layers

| Command             | Layer                                     | Cities assemblies              |
| ------------------- | ----------------------------------------- | ------------------------------ |
| `npm run test:unit` | `FinanceOverhaul.Market` math/ticker/perf | Never                          |
| `npm run build:mod` | The mod compiles (no tests of its own)    | Required; skipped without them |
| Local game loop     | Harmony + Stock Exchange                  | Required on your machine       |

Two projects, split by whether they reference the game. The library (`src/FinanceOverhaul.Market/`,
tests in `tests/FinanceOverhaul.Market.Tests/`) never does: every calculation lives there, market
rules and UI calculations (ordering, fitting, layout) alike, and everything unit tested is there.
The mod (`mod/`) always does: hooks, save data and drawing only, no `#if` guards, and it does not
build without the game. A mod class that needs a test has a calculation in it that belongs in the
library: move it there and have the mod call it.

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
by path. For manual control without a scenario, write `buy SHARES LIMIT NAME`, `cancel ID` or `list`
to the `FinanceOverhaul.order` trigger file (`docs/developer/live-reload.md`).

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
