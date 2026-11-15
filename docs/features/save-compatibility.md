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

The Index's one-time rebase marker lives in mod data (`FinanceOverhaul.IndexRebase`, text `v1`);
vanilla ignores it, and without it the mod simply checks the level once more.

## City bonds (experimental)

The bond book lives only in mod data (`FinanceOverhaul.Bonds`, text `v1`), behind an Options flag
that defaults to off. Without the mod the save loads as vanilla: the bonds and the payments they
still owe disappear, and the cash already received stays. Not yet run through the matrix below.

## Situations

Who loads a Finance Overhaul save without the mod, and what they keep. "Mod data" means the blocks
the mod writes beside the city (orders, bonds, markers), which the game ignores.

### 1. Retiring the mod (the player is done with it)

- **Expect:** every old save opens; the city, money, loans and vanilla holdings are intact.
- **Lost by design:** Index shares (their slot points at a stock only the mod provides; the game
  drops it, no refund), open orders, and city bonds (the debt vanishes; the cash raised stays, a
  windfall).
- **After the first save without the mod** the file is plain vanilla; the mod's blocks may stay
  inside, unused.
- **Idea:** an Options button "Prepare to remove Finance Overhaul" that sells Index shares at the
  daily price, repays or lists the bonds, and cancels open orders, so nothing is lost unfairly.

### 2. Forgot to turn the mod on (will turn it back on soon)

- **Expect:** nothing is lost when the mod comes back.
- **Today:** orders and bonds come back only if the game keeps the mod's blocks when it saves
  without the mod (to verify in game). **Index shares are lost**: the game drops the slot on load
  and saving makes that permanent (Alloy check P3 finds this trace).
- **Fixes:** keep a copy of the Index holding in mod data and restore it on load; keep a "last seen"
  game day so the mod knows days were played without it, and ask the player once how to handle the
  missed bond payments (the **Welcome back** dialog below). Orders past their lifetime expire on the
  first day back and are chirped.
- **Autosaves** while the mod is off overwrite the good save, so the fix must not depend on the
  player reloading an older file.

### 3. Tester switching things on and off

- **Mod off and on**, as in 2.
- **Index flag off** while holding shares: the mod runs but does not register the Index, so the game
  drops the slot just like case 2; the same copy-and-restore fix covers it.
- **City bonds flag off:** only stops new sales; existing bonds keep paying (by design).
- **Hot reload** of the mod: state is re-read from mod data; no save involved.
- **Dev test stock (TST):** test saves only; same rules as the Index.

### 4. Other situations

- **Sharing a save** (Steam Workshop, a friend) with someone who does not have the mod: case 1 for
  them.
- **Older mod opens a newer save** (downgrade): unknown codec versions and lines are skipped (unit
  tested); confirm once in game.
- **Newer mod opens an older save** (upgrade): one-time migrations, like the Index rebase marker.
- **Game update** changes how unknown prefabs or investments load: re-run the checks after big game
  patches.
- **Crash or power loss while saving:** a cut-off mod block parses as empty or skips bad lines; the
  game handles its own data.
- **Loading an older save of the same city:** mod data travels with each save, so it always matches
  that city state; player settings (Options, Stock Monitor) are shared across cities.
- **Other mods** that change investments or Chirper: out of scope, note conflicts when reported.

## Welcome back dialog (designed, Figma band 14)

Shown once, the first time a city loads with the mod after game days were played without it, and
only if a bond payment was missed. Very simple wording:

> **Welcome back**
>
> Finance Overhaul was off for 23 days in this city. Your city bonds did not make their payments
> while it was off. How do you want to handle them?
>
> **[Pay what was missed (₡44,000)]** Pays the 23 days of bond payments today, as if the mod had
> been on.
>
> **[Start fresh from today]** Skips the missed payments. Payments carry on as normal from today.
>
> This choice only affects this city.

If the Index copy restored shares, one line says so ("Your Index shares (120) are back.").

## Formal model (Alloy)

`formal/saves.als` models the save file, the running game and every order of player actions (load
with the mod, with the Index flag off, or without the mod; play a day; buy Index shares; save; quit)
and checks each promise above against every trace up to the bound. Policies are switches, so the
same model shows today's behaviour and the proposed fixes: mirror the Index holding, bond catch-up
versus skip, and whether the game keeps mod data when saving without the mod. Results: see
[Formal results](#formal-results).

## Formal results

Run with `scripts/check-formal.sh` (Alloy 6, Glucose solver; every trace up to 10 actions). "Holds"
means Alloy found no counterexample; "gap" means it found one, as expected for today's design.

| #   | Promise                                                     | Situation        | Today                                                    | With the fixes                                                                     |
| --- | ----------------------------------------------------------- | ---------------- | -------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| P1  | A vanilla load never keeps a reference to the missing Index | all              | **holds**                                                | holds                                                                              |
| P2  | Once saved without the mod, the file is plain vanilla       | retire           | **holds**                                                | holds                                                                              |
| P3  | Index shares saved with the mod come back with the mod      | forgot to enable | **gap**: lost (trace below)                              | holds with the Index copy, if the game keeps mod data; still lost if it drops it   |
| P4  | The bond book comes back with the mod                       | forgot to enable | holds if the game keeps mod data; **gap** if it drops it | same: depends on the game (verify in game)                                         |
| P5  | The city never pays a coupon that is not due                | all              | **holds**                                                | holds                                                                              |
| P6  | Coming back never pays more than one coupon in a day        | forgot / tester  | **gap** with catch-up (two coupons on day one back)      | holds when skipping; with the dialog, a lump is paid only if the player chooses it |
| P7  | Index flag off and on keeps saved shares                    | tester           | **gap** (same as P3)                                     | holds with the Index copy                                                          |

A reachability run confirms the "forgot to enable" situation is possible in the model, so the passes
are not vacuous.

**Today's P3 trace (shortest found):** load with the mod → buy Index shares → save → quit → load
with the Index flag off (or without the mod) → save. The slot was dropped on load and the save made
it permanent; loading with the mod again shows no shares.

**Alloy also caught a wording mistake:** the first P3 said shares bought with the mod always come
back; Alloy answered with "buy, quit without saving, reload". Those shares were never saved, so the
promise now speaks of shares that were saved.

**What the model does not cover:** the real save file format, other mods, crashes mid-save, and the
game's actual behaviour. Its two assumptions about the game (an unknown prefab is dropped on load;
mod data is kept or dropped on save) are exactly what the in-game checks below must confirm.

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
| 4   | 2026-11-13 | Pass            | Same save, mod re-enabled: Index registered before deserialization, `listed=True value=1445.817` equals the saved value (restored, not re-seeded); no `Adding the same investment` (no duplicate slot). In game: Index 100 shares, ₡146,529 invested, ₡144,582 (−₡1,947); Healthcare ₡32,600, Oil ₡26,400; Total ₡385,782, identical to the moment of saving.                |
| 6   | 2026-11-13 | Pass (expected) | Extra case: case-2 city **saved without the mod** (`Westdale-no-mod-loaded-from-save.crp`), then loaded with the mod and Index on. Vanilla had dropped Index from that save, so the mod created a fresh Index (`mode=seed`, one history point: flat chart, 0% change until the next in-game day).                                                                            |

Still to run: cases 1 and 5.

## Acceptance

- Every feature PR states whether it writes save data, and if so links a passing matrix run.
- [City index fund](./city-index-fund.md) stays experimental until cases 1–5 pass.
