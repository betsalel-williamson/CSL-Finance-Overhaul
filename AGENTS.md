# Agent instructions — Finance Overhaul

Trunk-based development on `main`. Short-lived `cursor/*-5acf` branches; squash-merge when gates are
green.

## Required before merge / “done”

```bash
npm run hooks:agent-validate
```

That runs layer lint, Market unit+perf tests, Mod host tests (no Cities), csharpier check, and
Merlon docs. Do **not** claim merge-ready without it.

## Changesets

Every player-visible change since the last release (`v0.1.0`) gets a `.changeset/*.md` in the same
commit: `patch` for a bug fix, `minor` for a feature. One short paragraph in player terms (what was
wrong, what happens now). They become the next release's `CHANGELOG.md` section and are deleted when
it ships.

## Test layers

| Command             | Layer                                     | Cities assemblies              |
| ------------------- | ----------------------------------------- | ------------------------------ |
| `npm run test:unit` | `FinanceOverhaul.Market` math/ticker/perf | Never                          |
| `npm run test:mod`  | Mod host identity                         | Off (`EnableCitiesRefs=false`) |
| Local game loop     | Harmony + Stock Exchange                  | Required on your machine       |

Stock business logic and performance budgets live only under `src/FinanceOverhaul.Market/` and
`tests/FinanceOverhaul.Market.Tests/`. Cities Harmony / `ICities` stay in `mod/Host/`.

## Git hooks

- **pre-commit** — lint-staged (prettier + csharpier)
- **commit-msg** — commitlint conventional commits
- **pre-push** — layer lint + `test:unit` + docs; on `main` also `test:mod` + format:check

## Docs

Edit Merlon shards under `docs/`, then `npm run docs`. Follow `.agents/skills/merlon-docs/`.

## Figma UI Map

Editing the Finance Overhaul Figma library (UI Map / Foundations / Components): follow
`.agents/skills/figma-fo-ui/` (and `.cursor/rules/figma-fo-ui.mdc`). Pins are red numbered dots on
the screenshot and the new UI (not legend cards). No overlapping pins; hug layout.
