# Save compatibility

## Value

A city saved with Finance Overhaul must still open in vanilla Cities: Skylines (mod disabled,
uninstalled, or an older mod version) without crashing and without corrupting the save. Lost
mod-only features are acceptable; a broken city is not.

## Contract

1. **UI-only features write nothing.** Typed share amount, readouts, and Options never touch
   serialized data. They need no migration.
2. **Vanilla data stays vanilla.** The mod never writes values vanilla cannot read back (no
   out-of-range enums, no unknown flags, no extra entries in vanilla arrays that vanilla would index
   blindly).
3. **Mod-only instruments are opt-in until proven.** Anything that adds state to a save (for example
   the Index stock) ships behind an Options toggle that defaults to **off** until the test matrix
   below passes in game.
4. **Degrade, then explain.** When the mod is absent, mod-only holdings may disappear, but the
   player-facing docs say so in advance, and the mod tells the player before they buy.
5. **Prefer mod-owned storage.** New instruments should keep their state in mod data
   (`ISerializableDataExtension`) that vanilla ignores, and only project it into vanilla UI at
   runtime. That design is the target for funds after the Index experiment.

## Index stock (experimental)

The Index stock reuses vanilla Investments by registering an extra `InvestmentInfo` prefab named
**Index**, so the save contains one vanilla investment slot that points at a prefab only the mod
provides.

Expected vanilla behavior without the mod, read from the game's call graph and **not yet verified in
game**:

- `PrefabCollection.BindPrefabs` logs "Unknown prefab" instead of throwing.
- `EconomyManager.LateUpdateData` clears investments whose `Info` is null.
- Result: the city loads, the Index listing disappears, and any Index shares are lost without
  refund.

Until the matrix passes, the Index toggle stays off by default.

## Test matrix (in game)

| #   | Saved with                 | Loaded with            | Pass when                                 |
| --- | -------------------------- | ---------------------- | ----------------------------------------- |
| 1   | Mod, Index off             | Vanilla (mod disabled) | Loads; investments identical              |
| 2   | Mod, Index on, no shares   | Vanilla                | Loads; no crash; Index absent             |
| 3   | Mod, Index on, shares held | Vanilla                | Loads; no crash; other holdings intact    |
| 4   | Save from case 3           | Mod again, Index on    | Loads; Index relisted; no duplicate slots |
| 5   | Mod, Index on              | Mod, Index off         | Loads; same result as case 3              |

Record results (and `Player.log` excerpts) in this shard when run.

## Results

| #   | Date       | Result          | Evidence                                                                                                                                                                                                                                                                                                                                                                     |
| --- | ---------- | --------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2   | 2026-11-13 | Pass            | `Westdale.crp` (mod on, Index listed, no shares) loaded with the mod disabled in the same session: `Unknown prefab: Index [Serialization]` logged, no exceptions from investments, Index absent. Only exceptions were vanilla `HotelChainPanel.Update` on the main menu. In game: Index absent; Deathcare 200, Healthcare 100, Oil 200 shares intact (Total value ₡237,100). |
| 3   | 2026-11-13 | Pass            | `FO-case3-shares.crp` (100 Index shares worth ₡144,582, Total ₡385,782) loaded **after a game restart** with the mod disabled: `Unknown prefab: Index [Serialization]` only; all exceptions in the run were vanilla `HotelChainPanel.Update`. Index absent, other holdings intact, Total ₡241,200 = ₡385,782 − ₡144,582 (exactly the Index holding lost, as documented).     |
| 4   | 2026-11-13 | Pass (log)      | Same save, mod re-enabled: Index registered before deserialization, `listed=True value=1445.817` equals the saved value (restored, not re-seeded); no `Adding the same investment` (no duplicate slot). Portfolio screenshot after this load still to confirm.                                                                                                               |
| 6   | 2026-11-13 | Pass (expected) | Extra case: case-2 city **saved without the mod** (`Westdale-no-mod-loaded-from-save.crp`), then loaded with the mod and Index on. Vanilla had dropped Index from that save, so the mod created a fresh Index (`mode=seed`, one history point: flat chart, 0% change until the next in-game day).                                                                            |

Still to run: cases 1 and 5; case 4 needs its portfolio screenshot.

## Acceptance

- Every feature PR states whether it writes save data, and if so links a passing matrix run.
- [City index fund](./city-index-fund.md) stays experimental until cases 1–5 pass.
