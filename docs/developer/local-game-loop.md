# Local game loop

Cloud agents can scaffold docs and unit tests. **In-game builds and live reload need a machine that
has Cities: Skylines installed** (managed assemblies + Mods folder). After the MVP scaffold lands on
GitHub, switch to a local clone for the rest of the hook work.

## Why switch

| Environment | What it can do                                     | What it cannot do                          |
| ----------- | -------------------------------------------------- | ------------------------------------------ |
| Cloud agent | Merlon docs, pure tests, PR hygiene                | Reference `ICities.dll` / deploy into Mods |
| Local repo  | `dotnet build` against Cities, Automate reload, QA | —                                          |

## One-time setup

```bash
git clone https://github.com/betsalel-williamson/CSL-Finance-Overhaul.git
cd CSL-Finance-Overhaul
git checkout cursor/mvp-stock-hook-live-reload-5acf   # or main after merge
npm install
npm test
```

Default paths (macOS Steam) match Trackpad Camera Control. Override when needed:

```bash
export CitiesManaged="/path/to/Cities_Data/Managed"          # must contain ICities.dll
export CITIES_MODS="/path/to/Colossal Order/Cities_Skylines/Addons/Mods"
```

Windows: set `CitiesManaged` to `…\Cities_Data\Managed` and let the csproj use
`%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods`, or set `CITIES_MODS` explicitly.

## Loop

1. Edit under `mod/` (and docs shards when contracts change).
2. Deploy with live-reload identity:

   ```bash
   ./scripts/install-mod-local.sh --dev
   ```

3. With Cities Harmony + Finance Overhaul enabled, load a city that has a Stock Exchange (or place
   one).
4. Confirm Options title shows a new assembly build/revision each install.
5. Tail the mod log:

   ```bash
   tail -f "${TMPDIR:-/tmp}/finance-overhaul.log"
   ```

6. Check Options → **Finance Overhaul** toggles (live ticker, experimental Index) as needed.

Details: [Local MVP install](./local-mvp-install.md), [Live reload](./live-reload.md).

## Done when

- Content Manager lists **Finance Overhaul** and it enables without errors.
- Log shows Harmony patch summary and stock type candidates on level load.
- A second `--dev` install changes the Options version string (reload proof).
