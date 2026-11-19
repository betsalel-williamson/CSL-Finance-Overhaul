# Validation

```bash
npm install
npm run hooks:agent-validate   # layer lint + unit/perf + mod build + format + imports + docs
```

Or piecemeal: `npm run docs`, `npm run test:unit`, `npm run build:mod` (compiles the mod where the
game is installed, skipped elsewhere), `npm run format:check`, `npm run imports:check`,
`npm run layer:lint`.

`layer:lint` runs three checks: the library stays Cities-free (`layer-import-lint.py`), text the
player reads comes from the string table (`strings-lint.py`), and every colour is named in
`mod/Kit/FoTheme.cs` (`colour-lint.py`).

In-game checks stay on the [local MVP install](./local-mvp-install.md) path. See
[Testing](./testing.md) and [Trunk-based development](./trunk-based.md).
