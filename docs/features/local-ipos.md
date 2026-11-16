# Local IPOs (design, Level 1)

Priority 2 of the [Roadmap](./roadmap.md): a business that has done well in the city for a long time
can go public and trade as a stock. Status: **designed, not built**. Grounded in the game code
([Game code notes](../developer/game-code-notes.md)); design rule: fun first, with real-world
patterns as the source of the rules.

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

City bonds were about the same size.

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

## The IPO

1. **Chirp** from City Brokerage: "{Company} is going public at ₡X a share. The city can buy at the
   offering price for 7 days. #ipo".
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
a price-to-earnings multiple ÷ shares, kept inside the range of the game's own stocks.

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

## Limits and room for later

- The game holds **254 investments** (measured, scenario 51). Local IPOs get a **cap** (default 12)
  so the rest stays free for external exchanges and other investment types.
- **At the cap:** no new IPOs; candidates keep waiting. Option: a listed company can be **acquired**
  (taken private at a premium, the city's shares paid out) to free a slot.
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
| New-listing volatility             | 2×        | Extra swing in the first year (settles to normal)     |
| Local listing cap                  | 12        | Most local companies listed at once                   |
| Acquisitions at the cap            | Off       | Free a slot by taking a company private               |
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

1. At the cap: wait only, or acquisitions too (on by default?).
2. Measure income by the game's formula or by the before-and-after reading.
3. Whether the city may sell IPO shares during the offering window.
