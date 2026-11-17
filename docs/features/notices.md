# Notices in Chirper

Priority 1 of the [Roadmap](./roadmap.md): order and bond outcomes reach the player as chirps
instead of only the mod log. Figma UI Map band 13 and component `FO/ChirpNotice`.

## What is chirped

| Event                     | Sender         | Example                                                                                  |
| ------------------------- | -------------- | ---------------------------------------------------------------------------------------- |
| Limit buy filled          | City Brokerage | Limit buy filled: bought 100 IDX at ₡1,452 (limit ₡1,460) for ₡145,200. #orders          |
| Sell order filled         | City Brokerage | Stop-loss filled: sold 100 GGI at ₡6,012 (stop ₡6,050) for ₡601,200. #orders             |
| Order expired             | City Brokerage | Limit buy for 100 IDX expired after 90 days without filling. #orders                     |
| Order cancelled           | City Brokerage | Limit buy for 100 IDX cancelled: not enough cash (needs ₡145,200). #orders               |
| Many orders on one day    | City Brokerage | 4 order updates today: 3 filled (IDX, GGI, Oil), 1 expired (TJL). Details in Economy → … |
| Bond repaid at maturity   | City Treasury  | Bond #5 repaid at maturity: ₡500,000 plus the last ₡1,904.17 coupon. #citybonds          |
| Bond coupon (option, off) | City Treasury  | Bond #5 coupon paid: ₡1,904.17 (59 of 60 left). #citybonds                               |

Take-profit and trailing stop fills read like the stop-loss one ("target" or "stop"). Early bond
repayment is the player's own click, so it is not chirped.

Planned, not built: [Local IPOs](./local-ipos.md#the-ipo-moment-planned) add an IPO chirp from City
Brokerage beside an IPO dialog. When the player hides that dialog, the chirp is the only
announcement.

## Rules

- **No flooding:** more than three order notices on one game day become one summary chirp
  (`NoticeBatch`); bond notices are never collapsed.
- **Save-safe:** chirps are added to the Chirper panel only (`ChirpPanel.AddMessage` with a plain
  `IChirperMessage`), never to the game's message queue, so a mod-only message type never reaches
  the save. Chirps from before a reload or load are not kept.
- **Threads:** notices are collected during the daily order and bond steps (simulation thread) and
  posted on the main thread (`ChirpNoticePoster`).
- **Options:** "Order and bond notices in Chirper" (on by default; UI only) and "Also chirp every
  bond coupon" (off: monthly per bond is chatty).
- **Tests:** notices are muted while a game UI test runs, so tests never chirp in the player's city;
  a scenario opts in with the `notices on` step.

## Proofs and checks

`NoticesTests`: exact wording for every kind; over 3,000 seeded game days never more than three
order chirps a day, the summary's counts add up to the day's order notices and name every symbol,
bond notices pass through unchanged and in order, and the senders are right. In game: scenario 46 (a
TST limit buy fills and is chirped, seen in the Chirper panel) and scenario 47 (a ₡1,000 bond repaid
at maturity is chirped by City Treasury).

Not yet checked in game: clicking the sender name or deleting a notice chirp in the Chirper panel.
