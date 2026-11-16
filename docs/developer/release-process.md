# Release process

**Audience:** maintainers shipping Finance Overhaul to the Steam Workshop. Adapted from the Trackpad
Camera Control release process (same author, same Mac setup). Paste-ready store text:
[Workshop storefront](./workshop-storefront.md).

## Version and build identity

| Surface                              | Value                                        |
| ------------------------------------ | -------------------------------------------- |
| Options and Content Manager title    | `package.json` semver (`0.1.0`)              |
| `AssemblyVersion`                    | `Major.Minor.*` (Cities reloads on change)   |
| Dev identity (`--dev` installs only) | assembly build/revision and the Built footer |

Release builds (`./scripts/install-mod-local.sh` without `--dev`) never include the dev-only pieces:
the game UI test runner, frame capture, the Test Stock (TST) option and the many-stocks stress test.

Options are split in two. **Preferences** (how a shipped feature looks or behaves: ticker style,
brightness, info views, order lifetime, Chirper notices) show in every build. **Feature flags**
(whether a feature exists at all: each order type, the Stock Monitor, share charts, Index stock,
city bonds, the test stock) show only in dev builds under "Feature flags (dev builds only)"; release
builds use `ModSettings.PinReleaseFlags`: every shipped feature on (orders, Stock Monitor, share
charts, Index stock, city bonds), the test stock off.

## Pre-ship checklist

1. `npm run hooks:agent-validate` passes (layer lint, Market and Mod tests, csharpier, docs).
2. `CHANGELOG.md` has the new version; `package.json` carries the same version.
3. In game with a release install: Options title reads **Finance Overhaul <version>**, no Built
   footer, no feature-flags group (preferences only); Content Manager shows the preview image; a
   city loads with no mod warnings.
4. `mod/PreviewImage.png` is 512×512 (at most 644×644) and shows the current build.
5. The player guide (`docs/client`) matches the release; it publishes to
   [GitHub Pages](https://betsalel-williamson.github.io/CSL-Finance-Overhaul/) on push.

## 1. GitHub Release (the build we share)

Every Workshop upload comes from a GitHub Release, so what players get is always a tagged,
downloadable build.

```bash
scripts/package-release.sh            # release build, zipped to dist/FinanceOverhaul-<version>.zip
git tag -a v<version> -m "Finance Overhaul <version>" && git push origin v<version>
gh release create v<version> dist/FinanceOverhaul-<version>.zip \
  --title "Finance Overhaul <version>" --notes-file <notes from CHANGELOG.md>
```

The zip holds one `FinanceOverhaul/` folder: `FinanceOverhaul.dll`, `FinanceOverhaul.Market.dll`,
`CitiesHarmony.API.dll` and `PreviewImage.png`. 0.1.0:
[v0.1.0](https://github.com/betsalel-williamson/CSL-Finance-Overhaul/releases/tag/v0.1.0).

## 2. Install the release build locally

```bash
scripts/install-release.sh            # latest release; or: scripts/install-release.sh v0.1.0
```

Downloads the release zip with `gh`, moves the current (dev) install out of the Mods folder to
`Cities_Skylines/FinanceOverhaul-backups/<timestamp>`, and installs the release into
`Addons/Mods/FinanceOverhaul`. Back to dev afterwards: `scripts/install-mod-local.sh --dev`.

## 3. First publish (Share), done once

The first upload creates the Workshop item. After that, always **Update** the same item.

1. Step 2 (release installed locally).
2. Cities → Content Manager → Mods → **Finance Overhaul** (local) → **Share**.
3. Title, description and tags from [Workshop storefront](./workshop-storefront.md); the preview is
   taken from `PreviewImage.png`. Publish.
4. On the Steam page: set visibility (hidden or friends only for the soft launch), add **Harmony**
   as a required item, check the description renders.
5. Record the published file ID in [Workshop storefront](./workshop-storefront.md) and commit.
6. Subscribe to the item, then move the local `Addons/Mods/FinanceOverhaul` folder aside so only the
   Workshop copy loads (two copies of the same mod conflict).

## 4. Later releases: Update the same item

Do **not** Share again: that creates a second item.

1. Quit Cities. If the Workshop row has no **Update** button, add the Steam launch option
   `--refreshWorkshop` (exact spelling) and relaunch.
2. Keep `Addons/Mods/FinanceOverhaul` absent while updating; stay subscribed to the item.
3. Steps 1 and 2 for the new version, then copy the four files into the staging folder that
   **Update** opens (`WorkshopStagingArea/<id>/Content/`, with `PreviewImage.png` beside `Content/`,
   not inside it), then move the local Mods folder aside again.
4. Click **Update**, wait for "Committing changes", confirm the change note on Steam, remove
   `--refreshWorkshop`.

Patching `steamapps/workshop/content/…` only changes what this Mac loads; it never uploads.

## Related

- [Live reload](./live-reload.md)
- [Testing](./testing.md)
- [Save compatibility](../features/save-compatibility.md)
