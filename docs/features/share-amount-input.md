# Typed share amount

## Value

Players can type an exact share count in **Invest in: {name}** instead of dragging the slider — the
first proof that Finance Overhaul can edit native Financial Districts UI safely.

## Contract

1. **Same spot, same look.** A numeric field replaces the read-only count next to the slider
   (`LabelShareSliderNumber`), styled like a Colossal text field.
2. **Vanilla stays in charge.** Submitting the field calls the dialog's own `SetSliderValue`, so the
   slider, Price, limits, and Buy/Sell behave exactly as vanilla.
3. **Whole shares only.** Digits and thousands separators are accepted; anything else is ignored and
   the field shows the current count again. Values clamp to the slider minimum and the vanilla
   maximum (`GetMaximumShareCount`: affordable/available for Buy, owned for Sell).
4. **Typing moves the slider live.** Each valid number updates the slider and Price immediately
   (clamped); empty or partial input leaves the slider where it is.
5. **Apply or revert like a normal field** (rules in `ShareAmountEditor`):

   | Action                       | Field                               | Slider         |
   | ---------------------------- | ----------------------------------- | -------------- |
   | Enter, Tab, or click outside | Unfocuses; shows the applied value  | Typed value    |
   | Escape                       | Unfocuses; shows the original value | Original value |

   Enter and Escape are consumed while typing, so the dialog never buys or closes from them.

6. **Thumb matches the value.** Colossal's `UISlider` only redraws its thumb when the value changes,
   not when the range does, so Sell at the maximum (after a Buy left the same number on a wider
   range) drew the thumb mid-track. After every `SetSliderValue` the mod redraws the thumb
   (`SliderThumbSync`); `SliderThumb` holds the position math, and game UI scenario
   `38-sell-slider-thumb-at-max` checks it with `expect-slider`.
7. **Fail soft.** If the vanilla members are missing, the mod logs once and leaves the slider-only
   dialog untouched.
8. **Hot-reload safe.** Replacing the DLL while the game runs never stacks fields: the old copy
   detaches on disable (restoring the vanilla label), the new copy attaches on the next **Invest
   in** open, and any stale field is removed by name.
9. **Save-neutral.** UI only; nothing is written to the save
   ([Save compatibility](./save-compatibility.md)).

## Code

- Rules: `ShareAmountInput.TryParse`, `ShareAmountEditor` and `SliderThumb` in the Market library
  (unit tested).
- Host: `mod/Host/ShareAmountField.cs`, `mod/Host/SliderThumbSync.cs` plus `Patcher` postfixes on
  `BuySellInvestmentPanel.Awake`, `ShowPanel`, `SetSliderValue`, `Update` (click outside), and a
  prefix on `OnKeyDown`.

## Acceptance

- Unit tests cover parsing, separators, clamping, and rejection.
- In game (hot reload): with a city loaded, run `./scripts/install-mod-local.sh --dev`, reopen
  **Invest in** → exactly one field.
- In game: type `250` + Enter → slider and Price update; type more than the maximum → clamps; Enter
  does not buy while typing; slider drag updates the field.
