# Testing

## Layers

| Project                      | Proves                                      | Cities? |
| ---------------------------- | ------------------------------------------- | ------- |
| `FinanceOverhaul.Core.Tests` | Index math, ticker, settings, perf budgets  | No      |
| `npm run build:mod`          | The mod compiles against the game           | Yes     |
| Local game + Automate        | Harmony patches against Financial Districts | Yes     |

## Commands

```bash
npm run test:unit              # Market only — agents/CI default
npm run build:mod              # compile the mod (needs the game; skipped without it)
npm test                       # same as test:unit
npm run hooks:agent-validate   # full trunk gate
npm run test:game              # game UI scenarios (running game, --dev build)
```

### Game UI scenarios

`scripts/game-ui-test.sh` copies each `tests/game-ui/*.uitest` script into the mod's
`FinanceOverhaul.uitest` trigger; the mod (`UiTestRunner`, `--dev` only) drives the real UI —
`show-buy`, `click`, `type` (focus, type, tab out), `run-days`, `set-flag <OrderKind|all> on|off`
(same as ticking an order type's Options checkbox), `monitor-compact on|off` (the Compact rows
setting), `monitor open|close` (sets the Stock Monitor window outright, never toggling a player's
window), `monitor remember|restore` (snapshot and put back the player's monitor settings),
`log-symbols` (logs every stock prefab with its ticker symbol and any clash), `info-view MODE` /
`info-view-swap MODE shown|hidden` (switch the info view; the swap variant checks the boards inside
the game's switch event), `live-ticker on|off`, `pause on|off` (a later scenario must resume),
`expect-ticker-full`, `ticker-in-info-views on|off`, `expect-ticker shown|hidden`,
`ticker-shows percent|price|restore`, `ticker-brightness LEVEL|restore` (all put the player's
choices back), `bonds-section show|auto` (shows the Loans tab's City bonds section and allows
selling with the flag off), `bonds-term YEARS` / `bonds-amount TEXT` (fill the section), `$bond`
(the newest bond the scenario sold), `bond-finances` (logs debt and annual income used for bond
pricing; `bond-finances DEBT INCOME` fixes them in whole ₡ until the scenario ends, the bond
counterpart of the test stock; `bond-finances real` undoes it), `issue-bond YEARS FACE` (sells a
bond, simulation paused; cash must rise by exactly the face), `bond-backdate DAYS` and
`expect-bond-coupons N` (the scenario's newest bond), `away DAYS` (pretends the mod was off: the
scenario's bonds miss DAYS of payments and the bookmark goes back),
`stress-stocks add N|fill TOTAL|slots|clear`, `stress-measure SECONDS`, `stress-panel`,
`scroll-investments bottom` and `count-investment-rows` (dev many-stocks test, scenario 51: test
save with the dev test stock option; never overwrites a held investment), `welcome-back pay|skip`
and `expect-welcome-back shown|hidden` (scenarios 48–49), `redeem-new-bonds` (cleanup: repays the
scenario's own bonds — scenario 37), `money-probe AMOUNT` (adds AMOUNT cents through the loan-taken
budget line and fetches it back, simulation paused; fails unless cash ends where it began — scenario
36), `hover CONTROL FX FY` / `hover off` (a simulated mouse at that fraction of a control, for the
Stock Monitor hover), `log-basket` (every investment's daily history, for auditing the Index math
offline), `inspect-parents CONTROL` (a control and every parent: absolute box, background, clipping
— for measuring vanilla UI), `inspect CONTROL` (one control: box, text, colour, text colour, font
and scale, and the opacity it is drawn with), `game-colors [expect]` (logs the game's own gain, loss
and value colours from the Economy panel; with `expect`, fails unless the theme's copies match —
scenario 53), `log-fonts` (every UI font the game has loaded), `keys CONTROL TOKEN…` (real key
presses through the field's own key handling, one a frame: a character types it; `home`, `end`,
`left`, `right`, `backspace`, `delete` press that key), `blur CONTROL` and `expect-caret CONTROL N`
(scenarios 57–58: typing regroups thousands), `turn-exchange DEGREES|restore` and
`expect-ticker-on-building` (turns the first Stock Exchange where it stands and checks the live
boards stand on it — scenario 55), `duplicate-alert preview|close` (shows the "switched on twice"
dialog for every installed copy without changing anything, or closes it; needs a second copy
installed), `game-escape` (runs the game's own Esc handling, as the key would — scenario 60),
`cash-set ₡`, `cash-restore`, `expect-cash ₡` and `bond-pay-due` (with the simulation paused by
`pause on`: set the city's cash outright, put back what was taken, check the cash to the cent, and
run the daily bond step now — scenario 61), `local-ipos on|off|restore`, `ipo-healthy-days N`,
`ipo-forget`, `ipo-scan` (reads every zoned business now and logs each kind's squares, top-level
buildings, candidates, slots and why businesses fail the health check) and
`expect-ipo-candidates none|some` (scenario 64), `ipo-list-now`, `ipo-first-day`, `ipo-buyout`,
`expect-company offering|trading|gone [listed|unlisted]` and `ipo-remove-test` (scenario 66: a
company lists as a stock, opens and is bought out), `expect-company-cards` (scenario 68: a card per
kind of business in Economy → Investments), `ipo-dialog on|off|close` and `ipo-buy SHARES` (scenario
67: the IPO dialog and the chirps), `ipo-remove-all` (dev clean-up: every local company removed as
if none had listed, mod data rewritten), `historical-off NAME` (takes the Historical flag off
businesses of that name), `ipo-income` (per zone: the income the game reports and the offering
prices its businesses would list at — scenario 65), `log-loan-expenses` and
`expect-loan-expenses ₡MIN` (what Economy shows as loan expenses — scenario 63), `dump-library NAME`
and `peek-library NAME` (log the tree of one of the game's own dialogs, such as
`ParkAreaUnlockingPanel`, or show it for two seconds for a capture of its look),
`expect-wheel-step LIST MIN` (pixels the mouse wheel moves a list a notch — scenario 69),
`log-prices` (logs every listed stock's kept daily prices, up to 30 and newest first, with its price
rule, for measuring how the game's stocks move), `local-ipos keep` and `ipo-list-now keep` (switch
the flag on for good and leave the company listed: for trying the feature in a test city, not for
scenarios), `ipo-view`, `expect-building-panel`, `expect-fireworks` and `expect-company-first`
(scenario 70: View headquarters opens the building's info panel with the camera framed on it,
fireworks go off, and the new company is the first card), `log-effects PATTERN` (the game's loaded
effects by name), `dump building-panel` (the tree of the game's zoned building info panel; scenario
71 checks what the mod adds to it for a listed company), `share-charts on|off` (the share chart
flag, for before/after captures) — and checks `expect-visible`, `expect-text`,
`expect-slider CONTROL [max]` (the slider's thumb is drawn where its value is; with `max`, the value
is the maximum), `expect-no-overflow` (every visible part inside the dialog or the named control,
and none of our fixed-width labels cutting its text off; inside a clipping, scrolling list only the
width is checked, against the list), `expect-at-bottom LIST` (a scrolling list that is long enough
to scroll sits at its end — scenario 52), `expect-owned-delta`, `expect-orders-delta` (orders this
scenario created) and `expect-log`. `$owned:SYMBOL` in any step becomes the shares owned now. A step
that throws fails the scenario and its cleanup still runs. Lines after `cleanup:` always run (cancel
orders, sell back, close the dialog). Results: `uitest: PASS|FAIL` in the mod log, captures as
`FinanceOverhaul-uitest-*.png`. Scenarios trade small amounts in the loaded city and undo them in
cleanup; use a test save.

**Your orders are safe:** when a scenario starts, every order already open (the player's) is frozen
— no fill, no expiry, no trailing peak — until the scenario ends, even when it runs days
(`OrderDesk.Freeze`, logged as `orders: N order(s) frozen …`). `freeze-new-orders` freezes the
scenario's own orders too (scenario `35-tst-player-orders-frozen` proves the rule). Days still pass
for the city itself.

Performance tests in `PriceBiasPerformanceTests` budget the equal-weight index (best of nine
batches, so a busy machine cannot fail them) and assert it allocates nothing. They measure Market
CPU only, not Cities frame time.

## Layer rule

`src/FinanceOverhaul.Core` must not reference Cities types — see
[ADR 0003](../features/adr/0003-market-library-vs-mod-surface.md) and `npm run layer:lint`.

## Formal models (Alloy)

`formal/saves.als` models save and load safety (situations in
[Save compatibility](../features/save-compatibility.md)). `scripts/check-formal.sh` runs every
command with the Glucose solver and compares the outcome with the command's `expect` (0 = the
promise holds, 1 = a known gap or a reachability run); it exits 1 on any surprise. Needs Java and
the Alloy 6 jar (`FO_ALLOY_JAR`, default `~/.cache/factory/org.alloytools.alloy.dist.jar`). About
eight minutes, so it is not part of `hooks:agent-validate`; run it whenever the save design changes.
