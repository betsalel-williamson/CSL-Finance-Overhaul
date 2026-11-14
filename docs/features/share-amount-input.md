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
4. **Enter submits, never buys.** While the field has focus, Enter applies the number; press Buy (or
   Enter again after the field loses focus) to trade.
5. **Slider and ± stay in sync.** Moving the slider or the ± steppers updates the field.
6. **Fail soft.** If the vanilla members are missing, the mod logs once and leaves the slider-only
   dialog untouched.
7. **Save-neutral.** UI only; nothing is written to the save
   ([Save compatibility](./save-compatibility.md)).

## Code

- Rules: `ShareAmountInput.TryParse` in the Market library (unit tested).
- Host: `mod/Host/ShareAmountField.cs` plus `Patcher` postfixes on `BuySellInvestmentPanel.Awake` /
  `SetSliderValue` and a prefix on `OnKeyDown`.

## Acceptance

- Unit tests cover parsing, separators, clamping, and rejection.
- In game: type `250` + Enter → slider and Price update; type more than the maximum → clamps; Enter
  does not buy while typing; slider drag updates the field.
