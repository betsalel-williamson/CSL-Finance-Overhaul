# Validation

```bash
npm install
npm run hooks:agent-validate   # layer lint + unit/perf + mod tests + format + docs
```

Or piecemeal: `npm run docs`, `npm run test:unit`, `npm run test:mod`, `npm run format:check`,
`npm run layer:lint`.

In-game checks stay on the [local MVP install](./local-mvp-install.md) path. See
[Testing](./testing.md) and [Trunk-based development](./trunk-based.md).
