# Trunk-based development

`main` is the trunk. Feature work uses short-lived branches (`cursor/<name>-5acf`), opens a PR,
waits for CI, then **squash-merges**.

## Gates

| Gate                       | Where                                        |
| -------------------------- | -------------------------------------------- |
| lint-staged + commitlint   | husky pre-commit / commit-msg                |
| Market unit + perf         | husky pre-push + CI + `hooks:agent-validate` |
| Mod host tests (no Cities) | pre-push on `main` + agent-validate + CI     |
| Layer import lint          | pre-push + agent-validate + CI               |
| Merlon docs                | pre-push + agent-validate + CI               |

CI runs on **pull_request** only (Validate does not re-run after squash to `main`). Agents must
still run `npm run hooks:agent-validate` before merge.

## Merge checklist

1. `npm run hooks:agent-validate`
2. Squash-merge the PR into `main` (or `git merge --squash` locally when the forge UI is waiting on
   approval).
3. Delete the feature branch after merge.
4. Continue in-game work from a [local game loop](./local-game-loop.md) clone of `main`.
