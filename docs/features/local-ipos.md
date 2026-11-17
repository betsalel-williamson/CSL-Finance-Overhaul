# Local IPOs (design, Level 1)

Priority 2 of the [Roadmap](./roadmap.md): a business that has done well in the city for a long time
can go public and trade as a stock. Status: **designed; first library slice built** (see
[Build progress](#build-progress)), nothing in the game yet. Grounded in the game code
([Game code notes](../developer/game-code-notes.md)); design rule: fun first, with real-world
patterns as the source of the rules.

## Build progress

Built one slice at a time (maths with proofs → engine → Figma → UI → game UI test → docs), behind
the Local IPOs flag.

| Slice                                                     | Where                                       | Status               |
| --------------------------------------------------------- | ------------------------------------------- | -------------------- |
| Candidate rule and healthy-day count; the rare daily draw | `Market/IpoCalendar.cs`, `IpoCalendarTests` | **Built 2026-11-17** |
| Offering price and first-day pop                          | `Market/IpoPricing.cs`, `IpoPricingTests`   | **Built 2026-11-17** |
| Buy-outs: chance, premium, payout                         | `Market/IpoBuyout.cs`, `IpoBuyoutTests`     | **Built 2026-11-17** |
| Drift and volatility after listing                        | Library                                     | Next                 |
| Company registry, moving and delisting, save data         | Library                                     | Not started          |
| Reading businesses from the game; the listing; the flag   | Mod engine                                  | Not started          |
| IPO dialog, bell, chirps, Figma                           | Mod windows                                 | Not started          |

The first slice: `CandidateBook` counts healthy days per building (id plus build index, so a
building that reuses an id starts from nothing) and lists the candidates in a fixed order;
`IpoCalendar` gives every candidate the same daily chance, set so the reference pool lists once in
the target years, and draws at most one a day from a repeatable dice (`IpoDice`) whose state is
saved with the city. Unit tests simulate 60,000 city-years per pool size and check the average, that
fewer candidates are rarer and none never lists, that every candidate is equally likely, and that
nothing is drawn while an IPO runs or at the listing cap.

## Levels and boundaries

Each level has its own Options flag and only starts when the one before is proven.

| Level | Scope                                                                                                         | Writes to                                                                                                   | Status       |
| ----- | ------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- | ------------ |
| **1** | **Local IPOs: one city, one save**                                                                            | One mod data block in the save; one game investment slot per listed company; the building's Historical flag | **This doc** |
| 2     | One country: companies from the player's other cities on an external exchange, shared calendar, seeded prices | A shared file outside every save (first feature to do so)                                                   | Later        |
| 3     | Commodities and a national-economy mood from trade and tourism                                                | Reads trade and tourism signals                                                                             | Later        |

Level 1 writes nothing outside the save.

### Size (IFPUG function points, first cut)

| Type                                       | Items                                                                                            | Points  |
| ------------------------------------------ | ------------------------------------------------------------------------------------------------ | ------- |
| Internal logical files (mod-maintained)    | company registry, candidate list                                                                 | 14      |
| External interface files (game-maintained) | buildings, investment table, economy and tax                                                     | 15      |
| External inputs                            | daily candidate scan, IPO event, buy at the IPO price, relocation, delisting and payout, options | 21      |
| External outputs                           | daily company price, IPO and relocation chirps                                                   | 9       |
| External queries                           | company details in Economy → Investments and the Stock Monitor                                   | 3       |
| **Total (unadjusted)**                     |                                                                                                  | **~62** |

City bonds were about the same size. The IPO dialog, its preference and the bell
([The IPO moment](#the-ipo-moment-planned)) were added after this count and are not in it.

## What can go public

- **Zoned businesses only:** commercial, office and industrial buildings (`PrivateBuildingAI`).
  Industries DLC area buildings and unique buildings are city service buildings: they cannot be
  Historical and have no business income of their own, so they are out of Level 1 (industry areas
  belong to commodities, Level 3).
- **At the top level of its kind:** level 3 for generic commercial, office and industry; level 1 for
  specialisations that only have one level (leisure, tourism, organic, IT cluster, financial,
  farming, forestry, oil, ore). The game levels offices by the district's workers' education and
  local service coverage, shops mainly by land value.
- **Healthy for a long time:** no problem icons (`Building.m_problems` is none), fully staffed, and
  above a land-value floor, every day for the required number of days. The game does not count
  healthy days, so the mod does (candidate list).
- **Big enough:** at least a minimum number of jobs, so corner shops never list.

A building that meets all of this becomes a **candidate** (name and building id recorded). Losing
any condition resets its healthy-day count; it stays a candidate only while healthy.

## How often (rare by design)

Target: **about one IPO every 10 game years** in a city with the right conditions; a city without
level-3 businesses that stay healthy never gets one.

- Each candidate has the same small daily chance, so the odds follow the city's mix: a city with
  many healthy offices sees office IPOs, a shopping city sees shop IPOs (the "numbers game").
- The chance is set from the target: with the reference pool size of candidates, the expected time
  between IPOs equals the target years. Fewer candidates make it rarer, more make it more common,
  never more than one IPO in progress at a time.
- A unit test simulates thousands of city-years to check the average matches the target.

## Unlocking, slots and pacing (proposed 2026-11-17)

Replaces the fixed "one every ten years" target once agreed. The player builds towards a sector's
first listing, sees the progress in Economy → Investments, and is never flooded.

1. **Gate:** a kind of business (commercial, office, industry, and each specialisation: IT cluster,
   leisure, tourism, organic, farming, forestry, oil, ore) can list only once the city has enough of
   it: 200 squares of its buildings, the size the game asks for its Forestry, Oil and Ore stocks.
2. **Slots:** past the gate, a kind earns one listing slot per so many top-level buildings (25
   offices, 40 shops, 30 industrial buildings; one per 15 for a specialisation). A listing uses a
   slot; a buy-out or a delisting gives it back.
3. **Chance:** only a kind with an open slot and at least one candidate can list. Each candidate
   then has the daily chance from [How often](#how-often-rare-by-design), set so an open slot is
   filled in about one to two game years.
4. **Pacing:** at most 2 IPOs in a game year, and never two offering windows at once, so switching
   the feature on in a large city fills the board over years, not in one week.
5. **Overall cap:** 2 listings per map tile the city owns, at most 12.

### In Economy → Investments

Figma: UI Map band 16 (proposal). Under "Purchasable Investments", one card per kind of business, in
the style of the game's locked stocks (name, requirement, a bar and "N / target"):

| State            | The card shows                                                                         |
| ---------------- | -------------------------------------------------------------------------------------- |
| Locked           | "Build 200 squares of offices to unlock office listings", bar and "132 / 200"          |
| Open slot        | "1 slot open · 14 candidates", and the bar towards the next slot ("31 / 50 buildings") |
| Offering         | The company, its symbol and price, days left, and Buy                                  |
| Slots full       | "2 of 2 listed", and the bar towards the next slot                                     |
| Paused by pacing | "Next listing possible from 01/2112 (2 a year)"                                        |

A footer line gives the whole board: "Local listings 3 of 6 · 2 per map tile (3 owned) · at most 2
IPOs a year". Listed local companies appear in the list like any stock.

### What the game already keeps (checked in the code, 2026-11-17)

| Needed                                 | Where                                                                                                                                                                                                                                                               | Status                                                                               |
| -------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| Buildings and built area per kind      | City-wide `District` data: `m_commercialData`, `m_officeData`, `m_industrialData`, `m_leisureData`, `m_touristData`, `m_organicData`, `m_hightechData`, `m_farmingData`, `m_forestryData`, `m_oreData`, `m_oilData` (`m_finalBuildingCount`, `m_finalBuildingArea`) | Fields exist; the unit of the area (squares or m²) is **unverified**                 |
| How many are at each level             | `m_finalLevel1` … `m_finalLevel5` on the commercial, office and industrial data                                                                                                                                                                                     | Fields exist; whether they count buildings or something else is **unverified**       |
| Industry area by type                  | `District.GetOilIndustryArea()` and its siblings (what the game's own stocks use)                                                                                                                                                                                   | Confirmed in use by the game                                                         |
| Map tiles owned                        | `GameAreaManager.m_areaCount`, `m_maxAreaCount`                                                                                                                                                                                                                     | Fields exist                                                                         |
| A "N / target" bar for a locked stock  | `InvestmentMilestone` (`m_targetCount`, `GetProgress`, text "{0} / {1}")                                                                                                                                                                                            | Confirmed; how the locked card looks was not captured (none locked in the test city) |
| Healthy days, jobs filled per business | Not kept by the game                                                                                                                                                                                                                                                | The mod counts them in its daily scan (`CandidateBook`)                              |

If a tally turns out not to mean what its name suggests, the mod's own daily scan of businesses
gives the same numbers at the cost of one pass over the buildings a day.

## The IPO

1. **Announcement:** a chirp from City Brokerage, "{Company} is going public at ₡X a share. The city
   can buy at the offering price for 7 days. #ipo", and the IPO dialog with the opening bell
   ([The IPO moment](#the-ipo-moment-planned)).
2. **Listing:** a game investment slot (like the Index) named after the business, with a unique
   symbol. The building is marked **Historical** (it then is not replaced when abandoned or
   collapsed, and drag-bulldoze skips it; it can still level, be abandoned, burn, be rezoned or be
   bulldozed on its own, so the company is kept apart from it, see Moving).
3. **Offering window:** for the offer days the city can buy up to the game's 1,000-share cap at the
   offering price.
4. **First trading day:** the price opens with a **first-day pop** drawn from a spread modelled on
   US IPOs (most rise modestly, about +15% on average; a few soar; a few fall).

### IPO price

From what the business earns: the game books income to the city by category only, so the mod
measures the building's own income (the game's income formula for its type and level × its activity,
or the city's income just before and after the building's step). Offering price = annual earnings ×
a price-to-earnings multiple × the city's stake (10%) ÷ 1,000, in whole ₡, kept inside ₡1 to
₡100,000 (the range of the game's own stocks).

**Why a 10% stake over 1,000 shares (decided 2026-11-17):** the game has no share count and no
volume. A stock is its last 30 daily prices plus the city's own holding (shares owned, money spent),
and each stock type caps that holding (`InvestmentAI.m_maximumShareCount`, 1,000). The city is the
only trader, and it owns nothing in the corporate sense: no votes, no meetings, no control. That is
a passive public investor, and real ones are capped (Norway's sovereign fund holds at most 10% of a
company's voting shares). So the full 1,000-share holding is a 10% stake: a business earning ₡26,000
a year is worth ₡520,000, the city can put in at most ₡52,000, and it lists at ₡52 a share
(`IpoPricing.OfferingPriceCents`; the stake is the option "City's largest stake", default 10%). The
game's own stocks have no company value behind them and stay as they are.

**First-day pop (built):** a log-normal jump with the two options as its average (+15%) and standard
deviation (25%), cut off at −50% and +300%. In 400,000 simulated listings about 3 in 10 open below
the offering price, more than half open between 0% and +50%, and about 9 in 100 open more than 50%
up (`IpoPricing.FirstDayPop`, `IpoPricingTests`).

### After the IPO

- **Drift** follows the building's health: staffed, problem-free and earning → gentle rise; problems
  → decline. Not tied to the game's sector stocks (they already follow city industry).
- **Volatility** starts high and settles over the first year, like real new listings.
- **Moving:** the company is stored separately from its building (building id plus the game's build
  index, since ids are reused). If the building is abandoned, demolished or replaced, the company
  moves its headquarters to the best healthy building of the same kind, keeps its name, symbol and
  shareholders, and chirps "{Company} moved its headquarters". If none exists, it stays listed for a
  grace period with a falling price, then delists and the city's shares are paid out at the last
  price.

### Buy-outs (decided 2026-11-17; maths built)

Now and then a listed company is bought and leaves the exchange, and the city is paid for its shares
above the price: a windfall, rare and not expected. It is the most common reason a real company
delists ([Evidence](#evidence-for-the-sizes-2026-11-17)).

- **How often:** every company listed for at least a year has the same small daily chance, 4% over a
  year. One listed company is bought about once in 25 game years; a full board of 12 sees a buy-out
  about every two. At most one a day.
- **No warning:** nothing announces it beforehand. On the day, a chirp from City Brokerage names the
  company, the buyer's price and what the city received.
- **What the city gets:** every share is paid at the price plus a premium drawn afresh each time:
  40% on average, now and then much more (about one in ten is above 60%), never under 10% nor over
  150%. A holding of 1,000 shares at ₡52 with a 40% premium pays ₡73,000.
- **Afterwards:** the company is delisted and its slot is free for a later IPO; the building stays
  Historical. A city that holds no shares still gets the chirp.
- **Built:** `IpoBuyout` (chance, draw, premium, payout) with unit tests that simulate 60,000
  city-years for 1, 6 and 12 listings. Not built: the engine that applies it, the chirp, the save
  data.

## The IPO moment (planned)

An IPO happens about once in 10 game years, so it is presented as an event, not only as a line in
Chirper: a dialog announces the listing and the game's bell sound plays once as it opens. Status:
**designed, not built**, like the rest of this shard. The game facts this section relies on, and
what could not be confirmed, are in
[Game code notes, section 11](../developer/game-code-notes.md#11-event-dialogs-and-ui-sounds).

### The model: the game's own event dialogs

| Game panel                                     | When the game shows it                           | What the IPO dialog takes from it                                                                                     |
| ---------------------------------------------- | ------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------- |
| `AcademicYearReportPanel` (Campus)             | When an academic year ends, for every campus     | Illustrated summary of one event, centred, fades in, pauses the game, a "don't show" checkbox, a "view" button, close |
| `ParkAreaUnlockingPanel` (Financial Districts) | When the Stock Exchange levels up (among others) | The same checkbox pattern, used by the Stock Exchange itself                                                          |

Both panels tie their checkbox to a setting on the game's own Options → Gameplay page, so the player
can turn the dialog back on. The IPO dialog follows that pattern with the mod's Options.

### What the dialog shows

Drawn in Figma: UI Map band 16, "The IPO dialog" (2026-11-17), built from the library's modal
header, key-value rows, buttons and checkbox; the header art is a placeholder.

One dialog per listing, opened on the day of the announcement (step 1 of [The IPO](#the-ipo)), when
the offering window opens and the player can still act on it.

| Region             | Content                                                                                                                                                         | Source in this design                        |
| ------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------- |
| Title              | "{Company} is going public"                                                                                                                                     | Company name, saved at the IPO               |
| Header art         | An illustration for the listing (open decision 6)                                                                                                               | Not decided                                  |
| Symbol and sector  | The ticker symbol and the sector (commercial, office, industry or the specialisation)                                                                           | Save data: symbol, sector                    |
| Headquarters       | The building's name and district                                                                                                                                | Save data: building id and build index       |
| Offering price     | ₡X a share                                                                                                                                                      | [IPO price](#ipo-price)                      |
| Offering window    | "The city can buy at the offering price for 7 days", with the last day                                                                                          | Offering window option                       |
| What the city gets | Up to 1,000 shares at the offering price (a 10% stake, with what that costs); the price moves freely from the first trading day; the building is now Historical | Steps 2 to 4 of [The IPO](#the-ipo)          |
| Buy                | "Buy at the offering price": closes the dialog and opens the game's buy dialog for the new listing                                                              | The listing is a game investment slot        |
| View headquarters  | Closes the dialog and moves the camera to the building                                                                                                          | Same idea as the report's view-campus button |
| Don't show again   | Checkbox: "Don't show this for future IPOs"                                                                                                                     | [Hiding the dialog](#hiding-the-dialog)      |
| Close              | × in the header; Esc and Return also close                                                                                                                      | Same keys as the report                      |

The dialog states only what is already fixed on the announcement day. It does not show the first-day
pop, a forecast or a recommendation: the pop is drawn on the first trading day.

Behaviour:

- **Pause:** the simulation pauses while the dialog is open, as it does for the academic year
  report, so the offering window does not run while the player reads. Closing restores the speed and
  pause state the player had before (the game's report always un-pauses; the mod does not copy
  that). Open decision 5.
- **Closing is safe:** closing without buying changes nothing. The listing stays in Economy →
  Investments and the offering price holds for the rest of the window.
- **Not during a load:** the dialog never opens while a city is loading, and a listing announced
  before a save is not announced again after loading it.
- **Tests:** like notices, the dialog and the bell are off while a game UI test runs unless the
  scenario opts in.

### Hiding the dialog

- **On the dialog:** the checkbox "Don't show this for future IPOs". Ticking it takes effect from
  the next listing; the open dialog stays until closed.
- **In Options:** a preference, "Show a dialog when a local company goes public", on by default. It
  is a preference (how a shipped feature behaves), so it sits under **Finance Overhaul** in every
  build once Local IPOs ships, and is shown only while Local IPOs is on (see
  [UI catalog](./ui-catalog.md)). It is separate from the Local IPOs feature flag and the tuning
  values under [Options](#options-level-1-while-tuning-the-feel).
- **One setting:** the checkbox and the preference are the same saved value, read in opposite
  senses. Changing either changes the other.
- **With the dialog hidden:** the IPO still happens in full (listing, Historical flag, offering
  window, first trading day). There is no dialog, no pause and no bell, and the announcement is the
  City Brokerage chirp alone ([Notices](./notices.md)).

### The opening bell

One bell when the dialog opens, named for the bell that opens a trading day on a real exchange.

- **Which sound:** a sound the game already ships, not a new audio file. The game has no sound named
  as a stock exchange bell (checked; see the game code notes). The first candidate is the Financial
  Districts clip `stock_exchange_levelup`; whether it sounds like a bell has not been checked by
  ear. Open decision 4.
- **Volume:** played as a UI sound through the game's audio manager, so it follows the player's Main
  and UI volume sliders and is silent when either is at zero. The mod adds no volume control of its
  own. UI sounds keep playing while the game is paused; world effect sounds do not, which is why the
  bell is not played as a building effect at the Stock Exchange.
- **Tied to the dialog:** no dialog, no bell. That covers the hidden preference, a game UI test and
  a listing that arrives while the city is loading.
- **Once:** the bell plays once per dialog and never loops.

### More than one listing

Level 1 allows one IPO in progress at a time, so two announcements on one day cannot happen there.
The rule is written now because Level 2 lists outside companies on a shared calendar, and because a
dialog can be waiting when another is due.

- **Same game day:** all listings announced on one game day share one dialog, with a row per company
  (name, symbol, sector, offering price, Buy). The bell plays once for that dialog.
- **A dialog is already open:** the game is paused, so nothing new can list until it closes.
- **Another full-screen dialog is open** (the game's own, or the mod's Welcome back dialog): the IPO
  dialog waits and opens when that one closes. Listings that arrive while it waits join the same
  dialog. The bell plays when it opens, once.

### Where it lives in the code

| Part                                                         | Place                                              |
| ------------------------------------------------------------ | -------------------------------------------------- |
| The dialog window                                            | `mod/Windows/`, beside the Welcome back dialog     |
| Reusable pieces (the "don't show again" row, the art header) | `mod/Kit/`, one per component in the Figma library |
| Finding the game's clip and playing it                       | `mod/Windows/`, with the dialog                    |
| When to announce (collected on the simulation thread)        | `mod/Engines/`, the company engine                 |
| What the dialog shows for a listing (rows, amounts, dates)   | `src/FinanceOverhaul.Core/Views/`                  |
| The combine and wait rule, and "bell at most once"           | `src/FinanceOverhaul.Core/Market/`                 |
| Every string in the dialog and the preference label          | `src/FinanceOverhaul.Core/Text/`                   |
| The preference and its saved value                           | `src/FinanceOverhaul.Core/Settings/`               |

The combine rule and the wording are unit tested in the library without the game; the dialog, the
checkbox and the bell call are checked by a game UI scenario.

## Formulas (as built in the library)

Every formula the built slices use, in one place. Code: `src/FinanceOverhaul.Core/Market/`
(`IpoCalendar.cs`, `IpoPricing.cs`, `IpoBuyout.cs`); each has unit tests of the same name in
`tests/FinanceOverhaul.Core.Tests/`. The defaults are options. Not yet built, so not here: drift and
daily swing after listing, the gates, slots and pacing, and the building-loss delisting.

### The dice

One repeatable random source per city (`IpoDice`, SplitMix64), its state saved with the city, so the
same save played forward rolls the same numbers. `Next()` is uniform in [0, 1).
`LogNormal(mean, sd)` takes two numbers `u1`, `u2` and returns

```text
σ² = ln(1 + sd² ÷ mean²)        μ = ln(mean) − σ² ÷ 2
z  = √(−2 · ln(1 − u1)) · cos(2π · u2)          (a standard normal number)
result = e^(μ + σ · z)
```

Most results fall near the mean, a few far above it, none below zero.

### Becoming a candidate

```text
healthy today = top level AND no problem icons AND fully staffed
                AND jobs ≥ 20 AND land value ≥ 50
healthy days  = previous + 1 if healthy today, else 0 (also 0 if the building is gone or replaced)
candidate     = healthy days ≥ 365
```

### When a candidate goes public (one roll a day)

```text
p (one candidate, one day) = 1 − (1 − 1 ÷ (target years × 365))^(1 ÷ reference pool)
chance today (n candidates) = 1 − (1 − p)^n
roll = Next();  IPO today if roll < chance today
which candidate = floor(roll ÷ chance today × n)          (each equally likely)
```

Defaults: target 10 years, reference pool 20, so 20 candidates list once in 3,650 days on average.
No roll while an IPO is in progress, at the listing cap (12), or with no candidates.

### Offering price

```text
company value  = annual earnings × 20
city's stake   = company value × 10%
offering price = floor(city's stake ÷ 1,000 shares) in whole ₡, kept between ₡1 and ₡100,000
```

₡26,000 a year → ₡520,000 → ₡52,000 stake → ₡52 a share.

### First-day pop (the roll for opening up or down)

```text
jump = LogNormal(mean = 1 + 15%, sd = 25%)
pop  = jump − 1, kept between −50% and +300%
first-day price = offering price × (1 + pop), rounded to a whole ₡, between ₡1 and ₡100,000
```

With the defaults: the average pop is +15%, the middle one about +12%, about 29 in 100 listings open
**below** the offering price, more than half open between 0% and +50%, and about 5 in 100 open more
than 50% up (400,000 simulated listings).

### Buy-outs (one roll a day)

```text
p (one company, one day) = 1 − (1 − 4%)^(1 ÷ 365)
eligible = listed for at least 365 days
chance today (n eligible) = 1 − (1 − p)^n
roll = Next();  buy-out today if roll < chance today;  which = floor(roll ÷ chance today × n)
premium = LogNormal(mean = 1 + 40%, sd = 15%) − 1, kept between +10% and +150%
buyer's price a share = price × (1 + premium), rounded to a whole ₡
the city receives = shares held × buyer's price a share
```

One listed company is bought about once in 25 game years; twelve see one about every two.

## Evidence for the sizes (2026-11-17)

Two sources for how often companies list and leave, and how their prices move: the game's own
stocks, measured, and US market history. Proposed sizes follow; they are **not decided**.

### The game's own stocks (measured)

One 30-day window in one city, 14 listed stocks, read with the dev command `log-prices`
([Testing](../developer/testing.md)).

| Stock                                              | Typical daily move | Largest | 30-day low to high |
| -------------------------------------------------- | ------------------ | ------- | ------------------ |
| Index (the mod's)                                  | 1.2%               | 6%      | 6%                 |
| ChirperCrypto                                      | 2.2%               | 5%      | 6%                 |
| Deathcare and Healthcare funds                     | 2.2–2.3%           | 5–7%    | 10%                |
| Greasy Gasoline, Very Legit Company, Traffic Jelly | 2.5–2.8%           | 7%      | 10–17%             |
| Chirpy Cruises                                     | 2.6%               | 14%     | 51%                |
| Generic and Oil Industry                           | 4.9%               | 12–14%  | 16–17%             |
| Farming Industry                                   | 7.1%               | 15%     | 18%                |
| Ore, Forestry, ChirpAir                            | random             | 2,000%+ | 11 to 2,187        |

- The eleven calm stocks move about 3% a day (standard deviation about 4%) and stay inside a 10–18%
  band over a month: they wobble around a level the city sets and do not trend.
- Ore, Forestry and ChirpAir jump at random between 10 and 2,200 every day: the game's rule for a
  computed value under 10 ([Game code notes](../developer/game-code-notes.md), section 4). The city
  has almost none of what drives them.

### US market history

Sources: Jay Ritter, "IPO Statistics for 2025 and Earlier Years" (operating-company IPOs,
1980–2025); Doidge, Karolyi and Stulz, "The U.S. Listing Gap" (1975–2012); Lowry, Officer and
Schwert on the spread of initial returns; Damodaran's sector data (January 2026). Read in the source
documents unless marked.

| Question                     | Figure                                                                                                                                                 |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| New listings a year          | 9.2% of listed companies (1975–96), 5.1% (1997–2012); IPOs alone: 21 to 677 a year, about 203                                                          |
| IPOs by sector               | Technology 36%, life science 11%, everything else 53% (no finer split published)                                                                       |
| First-day return             | Average 19%; median about 7% (table header unclear); 16.5% open down, 13% flat, about 3.5% double                                                      |
| Its spread                   | Standard deviation about 30% outside the 1998–2000 bubble (126% inside it)                                                                             |
| First day by sector          | Technology 31%, life science 16%, other 12%                                                                                                            |
| Three years on               | Average +21%, median −26%; 38.5% lose over half; about 5.5% a year behind the market                                                                   |
| Larger, profitable companies | Close to the market (−3%); 26% lose over half in three years                                                                                           |
| Leaving the exchange, a year | 7.3% of listed companies (1975–96), 9.5% (1997–2012)                                                                                                   |
| …by reason                   | Merger 3.9–5.6%; for cause (failure, falling below the rules) 3.3–3.5%; voluntary 0.1–0.4%                                                             |
| Still listed after 5 years   | 60–65% of new listings                                                                                                                                 |
| Still listed after 10 years  | 37–61% (search snippet only: Fama and French 2004)                                                                                                     |
| Paid to holders when leaving | Merger: 36–48% above the price (snippet); for cause: about −30% NYSE (from memory), −55% Nasdaq (snippet)                                              |
| A stock's swing in a year    | Market average 48%; utilities 15%, banks 23%, retail 43%, machinery 45%, oil and gas 42%, software 57%, mining 78% (tool summary of Damodaran's table) |

Not found: how much more a new listing swings than a seasoned one and how fast that settles;
survival after 1 and 3 years; delisting by industry.

### Proposed sizes (not decided)

- **Price path:** a random walk with drift, unlike the game's stocks, which wobble around a level. A
  3% daily move over 365 game days is a 57% yearly swing, the size of a real single stock (48%), so
  one game day stands for one trading day and the game's own daily move (about 3%) is the yardstick.
- **Daily swing by kind:** commercial 2.3% (retail 43% a year), industry 2.4% (machinery 45%),
  office and IT cluster 3.0% (software 57%), oil and ore 3.5% (mining and energy). The first-year
  extra swing stays at the design's 2× as a guess: no figure was found.
- **Drift:** healthy and earning, about +8% a year (profitable, established listings track the
  market). With problems, down. Without a home, falling 30–55% over the grace period, then delisted
  and paid out at the last price (the for-cause figures).
- **First-day pop:** average 19% and spread 30% (now 15% and 25%); in the model that gives about 28%
  opening down and 1% doubling. IT cluster listings 31% on average, as technology does.
- **How often:** history is one new listing a year for every 11 to 20 listed companies. With the cap
  of 12 that is one IPO every one to two game years, not the design's ten.
- **Leaving:** for cause is already the building's fate (abandoned, demolished, no new home).
  Mergers are the larger real cause: **decided and built**, see
  [Buy-outs](#buy-outs-decided-2026-11-17-maths-built). Together about 60% of listings would still
  be there after five years, as in history.

## Limits and room for later

- The game holds **254 investments** (measured, scenario 51). Local IPOs get a **cap** (default 12)
  so the rest stays free for external exchanges and other investment types.
- **At the cap:** no new IPOs; candidates keep waiting until a listed company leaves (a
  [buy-out](#buy-outs-decided-2026-11-17-maths-built), or losing its home).
- **The UI is the real limit today:** the vanilla Investments panel is one long list. Going from a
  dozen to 50 listings needs the many-company interface (search, sector groups, watchlist; roadmap
  idea 14) as a slice of this epic before the cap is raised.

## Options (Level 1, while tuning the feel)

All under an experimental "Local IPOs" group, off by default; every value is saved with the player's
settings.

| Option                             | Default   | What it changes                                       |
| ---------------------------------- | --------- | ----------------------------------------------------- |
| Local IPOs                         | Off       | The whole feature                                     |
| Years between IPOs (target)        | 10        | Expected time between IPOs at the reference pool size |
| Reference pool (candidates)        | 20        | Pool size at which the target applies                 |
| Healthy days to become a candidate | 365       | How long a business must stay healthy                 |
| Minimum jobs                       | 20        | Keeps small shops out                                 |
| Land value floor                   | 50        | Minimum land value at the building                    |
| Offering window (days)             | 7         | Days to buy at the offering price                     |
| First-day pop: average / spread    | 15% / 25% | The first-day jump and how varied it is               |
| Price-to-earnings multiple         | 20        | Offering price from measured earnings                 |
| City's largest stake               | 10%       | What the full 1,000-share holding is of the company   |
| New-listing volatility             | 2×        | Extra swing in the first year (settles to normal)     |
| Local listing cap                  | 12        | Most local companies listed at once                   |
| Buy-out chance a year              | 4%        | A listed company's chance of being bought in a year   |
| Buy-out premium: average / spread  | 40% / 15% | What the buyer pays above the price                   |
| No buy-out before (days listed)    | 365       | A new listing is not bought in its first year         |
| Grace period after losing a home   | 90 days   | Before an un-homed company delists                    |

## Save data

- Mod data `FinanceOverhaul.Companies` (versioned text, like bonds): companies (name, symbol,
  sector, building id and build index, IPO date and price, status) and candidates (building id,
  build index, healthy days).
- Each company's game investment slot is protected like the Index: a copy of the holding in mod
  data, restored if the slot is dropped, and the free-slot repair for the game's load bug.
- Without the mod: the city loads; listed companies disappear from Investments (holdings lost,
  restored when the mod returns, like the Index); buildings simply stay Historical.

## Open decisions

1. Decided: buy-outs are a regular rare event for any company listed a year or more, not a rule that
   only applies at the cap.
2. Measure income by the game's formula or by the before-and-after reading.
3. Whether the city may sell IPO shares during the offering window.
4. The bell: confirm by ear that `stock_exchange_levelup` suits; if not, pick another shipped clip
   or bundle a bell with the mod (needs a licensed recording). Confirm in game how the mod reaches
   the clip.
5. Whether the IPO dialog pauses the simulation (the academic year report does).
6. The dialog's header art: the headquarters' own thumbnail, a sector icon or drawn art (Figma).
7. Whether the dialog can be reopened later, as the academic year report can from the campus panel,
   and from where (the listing in the Stock Monitor is the likely place).
8. Whether the first trading day gets its own moment (a chirp with the first-day pop). On a real
   exchange the bell belongs to that day; this design rings it at the announcement because that is
   when the dialog appears.
9. Whether the IPO chirp follows the "Order and bond notices in Chirper" preference. If it does, a
   player with notices off and the dialog hidden gets no announcement, and the label needs to cover
   IPOs.
