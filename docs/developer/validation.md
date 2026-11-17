# Validation

```bash
npm install
npm run hooks:agent-validate   # layer lint + unit/perf + mod build + format + docs
```

Or piecemeal: `npm run docs`, `npm run test:unit`, `npm run build:mod` (compiles the mod where the
game is installed, skipped elsewhere), `npm run format:check`, `npm run layer:lint`.

In-game checks stay on the [local MVP install](./local-mvp-install.md) path. See
[Testing](./testing.md) and [Trunk-based development](./trunk-based.md).
