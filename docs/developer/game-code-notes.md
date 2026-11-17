# Game code notes (buildings, investments, trade)

What the Cities: Skylines 1 code actually does, read from the IL of `Assembly-CSharp.dll` (Steam
build of 2026-11) for [Local IPOs](../features/local-ipos.md) and the later external markets and
commodities. Method: a small IL dumper built on `System.Reflection.Metadata` (no decompiler);
findings name the type and method so they can be checked again after a game patch.

Everything below is read from IL unless marked **unverified** or **inference**.

---

## 1. Per-building "success score": what the game tracks

All fields are on `Building` (struct in `BuildingManager.m_buildings.m_buffer[ushort id]`). Types
are from `E_building.txt`.

| Field                                                     | Type                                                                               | Meaning for C/O/I (from IL)                                                                                                                                                                                                                                                                                         |
| --------------------------------------------------------- | ---------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `m_level`                                                 | byte                                                                               | Current level. `ItemClass.Level` 0-based in prefab, `m_level` set to `max(m_level, info.m_class.m_level)` in `PrivateBuildingAI.BuildingUpgraded`.                                                                                                                                                                  |
| `m_levelUpProgress`                                       | byte                                                                               | Two nibbles: low = "wealth/education" progress, high = land-value progress (`CommercialBuildingAI.CheckBuildingLevel` writes `p1 \| p2<<4`). 255 has special meaning (skipped in sim checks).                                                                                                                       |
| `m_problems`                                              | `Notification.ProblemStruct` (`m_Problems1`, `m_Problems2`, both ulong flag enums) | Current notification icons. See problem bits below.                                                                                                                                                                                                                                                                 |
| `m_workerProblemTimer`                                    | byte                                                                               | `PrivateBuildingAI.HandleWorkers`: +1 per active step while workers < workplaces / 2; `NoWorkers` at 64 or more, plus `MajorProblem` at 128 or more.                                                                                                                                                                |
| `m_incomingProblemTimer`                                  | byte                                                                               | Commercial: goods (`m_customBuffer1`) at 0 gives `NoGoods`, plus `MajorProblem` at 64 or more. Industrial: raw materials empty gives `NoResources`, plus `MajorProblem`.                                                                                                                                            |
| `m_outgoingProblemTimer`                                  | byte                                                                               | Commercial: sold-goods buffer full and few visitors gives `NoCustomers` at 128 or more, plus `MajorProblem` at 192 or more. Industrial: output buffer full gives `NoPlaceforGoods` at 128 or more, plus `MajorProblem` at 192 or more.                                                                              |
| `m_taxProblemTimer`                                       | byte                                                                               | Tax rate at or above the level threshold + 4 gives `TaxesTooHigh` at 32 or more, plus `MajorProblem` at 96 or more (Commercial and Industrial `SimulationStepActive`).                                                                                                                                              |
| `m_serviceProblemTimer`                                   | byte                                                                               | Commercial `CheckBuildingLevel`: level 1 with land value 10 or less, or level 2 with land value 30 or less, gives `LandValueLow` at 4 or more, plus `MajorProblem` at 8 or more.                                                                                                                                    |
| `m_majorProblemTimer`                                     | byte                                                                               | `PrivateBuildingAI.SimulationStepActive`: +1 per step while `MajorProblem` bit set; at ≥64 the building is **abandoned** (unless `BuildingManager.m_abandonmentDisabled`). Reset to 0 when no major problem.                                                                                                        |
| `m_happiness`                                             | byte 0-100                                                                         | Commercial: health/wellbeing of workers+visitors (`Citizen.GetHappiness`) ×15%, +20 if no MajorProblem, +25 if no problems at all, + goods-stock ratio, − sold-buffer ratio, ± tax vs level band, ×0.85 under one specialization policy. Industrial writes `m_happiness` too (same shape, not traced line by line). |
| `m_health`, `m_citizenCount`                              | byte                                                                               | Avg worker/visitor health; workers+visitors present.                                                                                                                                                                                                                                                                |
| `m_customBuffer1`                                         | ushort                                                                             | Commercial: goods stock (`CommercialBuildingAI.GetGoodsAmount` returns it). Industrial: incoming raw material stock.                                                                                                                                                                                                |
| `m_customBuffer2`                                         | ushort                                                                             | Commercial: amount sold waiting for outgoing transfer (when the class has an outgoing reason). Industrial: produced goods waiting for export/delivery.                                                                                                                                                              |
| `m_educated0..3Unemployed`, `m_allUnemployed`             | byte                                                                               | Despite the name, for workplaces these are **% of job slots unfilled** per education level (`100 − 100*filled/slots`) written by `HandleWorkers`. `m_allUnemployed` ≈ total vacancy %. Good staffing signal.                                                                                                        |
| `m_tempImport/m_tempExport → m_finalImport/m_finalExport` | byte                                                                               | Commercial: import = incoming guest vehicles, export = tourist visitors (`BehaviourData.m_touristCount`). Industrial: rolled every 16 frames in `IndustrialBuildingAI.SimulationStep`.                                                                                                                              |
| `m_adults`                                                | byte                                                                               | Commercial only (inference from IL order): set to the effective sales/production rate at the end of `CommercialBuildingAI.SimulationStepActive`. Not a citizen count for shops.                                                                                                                                     |
| `m_productionRate`                                        | byte                                                                               | Budget/slider for player buildings; **not** private building output. No private AI writes it.                                                                                                                                                                                                                       |
| `m_flags`                                                 | `Building.Flags`                                                                   | See section 2. `Active` (0x20000) set when producing, `RateReduced` (0x400) when consumption < 100%.                                                                                                                                                                                                                |
| `m_buildIndex`                                            | uint                                                                               | `BuildingManager.m_currentBuildIndex` at creation. Changes only when a new building is created: useful as an id-reuse guard.                                                                                                                                                                                        |
| `m_education1..3`                                         | byte                                                                               | **unverified** for workplaces (not traced).                                                                                                                                                                                                                                                                         |

Land value is not on `Building`: read with
`ImmaterialResourceManager.instance.CheckLocalResource(ImmaterialResourceManager.Resource.LandValue /*14*/, data.m_position, out int value)`
— this is exactly what `CommercialBuildingAI.CheckBuildingLevel` does.

Relevant `Notification.Problem1` bits (ulong): `LandValueLow 0x200`, `NoCustomers 0x40000`,
`NoResources 0x80000`, `NoGoods 0x100000`, `NoPlaceforGoods 0x200000`, `NoWorkers 0x400000`,
`NoEducatedWorkers 0x800000`, `TaxesTooHigh 0x4000000`, `MajorProblem 0x4000000000000000`,
`FatalProblem 0x8000000000000000`. Also service problems (`Electricity`, `Water`, `Garbage`,
`Crime`, `Fire`, `TooFewServices`, `RoadNotConnected`…).

Level-up rule (Commercial; Office/Industrial analogous, not traced in detail): `CheckBuildingLevel`
computes a target level from customer wealth (gated by `UnlockManager.Feature.EducationLevel2/3`)
and from land value (<21 → L1, <41 → L2, else L3), takes the min, and calls
`PrivateBuildingAI.StartUpgrading` only if `m_problems` is **None**, `GetUpgradeInfo` returns a
prefab, and the area is not evacuating. Runs once every 16 simulation frames per building and only
when `m_lastBuildIndex == m_currentBuildIndex`.

Max levels (`ZonedBuildingWorldInfoPanel.GetMaxLevel` switch): commercial/office/generic industry =
3, residential = 5, **specialized industry (forestry/farming/oil/ore) = 1** (default branch). So
"level 3" filters out specialized industry entirely.

Verdict on design idea 1: all inputs exist. Level, staffing (vacancy %), problem bits and timers,
happiness, goods buffers and land value are readable per building with no patches. "How long it
stayed healthy" is **not** tracked by the game (the timers reset); the mod must sample and keep its
own streak counter.

## 2. Historical: what it blocks, and how it is set

Flag: `Building.Flags.Historical = 0x80000000` (int −2147483648).

How the info panel sets it: `ZonedBuildingWorldInfoPanel` checkbox `"CheckboxHistorical"` →
`OnHistoricalValueChanged` → `SimulationManager.AddAction(lambda)` → lambda calls
`building.Info.m_buildingAI.SetHistorical(buildingID, ref buffer[id], value)`.

- `BuildingAI.SetHistorical` is empty; `PrivateBuildingAI.SetHistorical` sets/clears the bit.
- `BuildingAI.IsHistorical(id, ref data, out bool canToggle)` returns false/false;
  `PrivateBuildingAI.IsHistorical` returns `(flags & Historical) != 0` and `canToggle = true`. So
  only zoned (PrivateBuildingAI) buildings can be historical. Mod should call the same
  `SetHistorical` from the simulation thread (or via `AddAction`).

Every place the bit is read (searched `PrivateBuildingAI`, `CommonBuildingAI`, `BuildingAI`,
`BuildingManager`, `Building`, `ZoneBlock`, `ZoneManager`, `BuildingTool`, `DefaultTool`,
`BulldozeTool`, `ResidentialBuildingAI`; other types not searched):

| Where                                                                                                 | Effect when Historical                                                                                                                                                                                                                                                                        |
| ----------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `PrivateBuildingAI.StartUpgrading`                                                                    | **Levels up in place**: clears `LevelUpEducation/LevelUpLandValue`, `m_level++`, calls `BuildingUpgraded` + `ManualActivation`. No `Upgrading` flag, no new prefab, mesh unchanged. Non-historical path sets `Upgrading` and later swaps the prefab.                                          |
| `PrivateBuildingAI.SimulationStep` (branch on `Abandoned \| Downgrading \| Collapsed`, mask 0x640000) | **Skips automatic replacement**. Non-historical abandoned (after `m_majorProblemTimer` reaches 255) / collapsed buildings get a new building created on the spot via `BuildingManager.CreateBuilding` and the old one flagged `Demolishing`. A historical one just stays abandoned/collapsed. |
| `CommonBuildingAI.SimulationStep`                                                                     | Only suppresses tutorial guides (`m_buildingDestroyed`, `m_buildingAbandoned1/2`).                                                                                                                                                                                                            |
| `BulldozeTool.SimulationStep` / `DeleteBuildingImpl`                                                  | Drag/area bulldoze skips historical buildings (inference: a direct single-building delete still works; not fully traced).                                                                                                                                                                     |

Not blocked by Historical (no check in the code paths):

- **Abandonment** itself (`PrivateBuildingAI.SimulationStepActive` → `Abandoned` at major-problem
  timer ≥64).
- **Fire / burning down / disaster collapse** (`CommonBuildingAI.HandleFire`, `BuildingCollapsed`).
- **Zoning mismatch demolition** (`PrivateBuildingAI.SimulationStep`: `ZonesUpdated` +
  `Building.CheckZoning` fails → `Demolishing`).
- **District style mismatch** (`PrivateBuildingAI.SimulationStep`, `DistrictStyle.Contains` false →
  `Demolishing`).
- **Industry specialization mismatch** (`IndustrialBuildingAI.SimulationStep` → `Demolishing`).
- Manual bulldozing of the single building.

Verdict on idea 2: mostly right, with two corrections. (a) Historical does **not** freeze the level
— the building still levels up, in place, keeping its id and mesh. (b) Historical does not stop
abandonment, but it stops the game from replacing the abandoned shell, so the company would sit on a
dead building until the player bulldozes it. Also rezoning/style/specialization changes still
demolish it. The IPO design must handle "listed building became Abandoned" as an event.

## 3. Identity, lifecycle events and names

- Id: `ushort` index into `BuildingManager.instance.m_buildings.m_buffer` (array pool;
  `CreateItem`/`ReleaseItem`). Ids are **reused** after release. Guard with `m_buildIndex` (set at
  creation) plus `m_infoIndex`/position.
- **Level-up keeps the id.** `CommonBuildingAI.BuildingCompleted` (when `Upgrading` set):
  `GetUpgradeInfo` → `BuildingManager.UpdateBuildingInfo(id, newInfo)` (swaps prefab in place) →
  `newInfo.m_buildingAI.BuildingUpgraded(id, ref data)`. Historical level-up also keeps id.
- **Replacement creates a new id**: abandoned/collapsed rebuild, zoning/style/specialization
  demolition all go through `BuildingManager.CreateBuilding` (new id) and set `Demolishing` on the
  old one, which is later released.
- Events:
  - `BuildingManager.EventBuildingCreated` (invoked at end of `CreateBuilding`),
    `EventBuildingReleased` (end of `ReleaseBuildingImplementation`), `EventBuildingRelocated`
    (`RelocateBuildingImpl`). Signature delegates `BuildingCreatedHandler/BuildingReleasedHandler`
    take the ushort id (**unverified** arity).
  - ICities: `BuildingExtensionBase` / `IBuildingExtension.OnBuildingCreated/Released/Relocated`
    wired through `BuildingWrapper.BindEvents` to the three events above, plus `OnCalculateSpawn`.
  - `LevelUpWrapper` (ICities `ILevelUpExtension`): `OnCalculateCommercialLevelUp` etc. are called
    from `CheckBuildingLevel` (observe or alter target level).
  - No event for **upgraded** or **abandoned**: need polling (`m_level`, `m_flags`) or a Harmony
    postfix on `PrivateBuildingAI.BuildingUpgraded` / `StartUpgrading`.
- Names: `BuildingManager.GetBuildingName(id, InstanceID caller)` returns
  `InstanceManager.GetName(InstanceID{Building=id})` if `Flags.CustomName` (0x8) is set, else
  `GenerateName` → `BuildingAI.GenerateName`. Commercial/Industrial `GenerateName` seed a
  `Randomizer(buildingID)` and pick from `BUILDING_NAME[prefabName]` or
  `COMMERCIAL_LOW_NAME / INDUSTRIAL_NAME[level or subService]` locale lists. So the default name
  **changes when the prefab changes on level-up**. `BuildingManager.SetBuildingName(id, name)` is an
  IEnumerator (run via `SimulationManager.AddAction`) that sets `CustomName` and
  `InstanceManager.SetName`. Release calls `InstanceManager.ReleaseInstance` (custom name dropped).

Verdict on idea 3: keep the company as mod data keyed by its own company id, holding
`(buildingId, buildIndex)` as the current HQ, and snapshot the name at IPO (do not rely on the
generated name). Detect HQ loss via `EventBuildingReleased`, plus polling for `Abandoned`,
`Collapsed`/`BurnedDown` (same bit 0x400000) and `Demolishing`.

## 4. Vanilla investments (Financial Districts)

Type tree (`types.txt`): `InvestmentAI` ← `AirplaneInvestmentAI`, `DeathCareSectorAI`,
`FuelCompanyAI`, `HealthCareSectorAI`, `IndustrySectorAI`, `ShipInvestmentAI`, `TrafficJellyAI`,
`VeryLegitCompanyAI`, `RandomRiskInvestmentAI` ← (`ChirpCoinAI`, `HealthCareSectorRandomAI`). No
other subclasses exist.

Common machinery (`InvestmentAI`):

- `EconomyManager.SimulationStepInvestments` calls `UpdateInvestment` once per **game day** for
  every investment with flag 2 (created). History length `kPastValueCount = 30`, newest first.
- `SafeCalculateValue`: `v = CalculateValue()`; if `v < 10` → random 10..2200 (!); else if
  `CalculateFlatLineIndex > 0.05` (last ≥5 values nearly flat) → `AddRandomVariance(v, 0.15*idx)`.
- `AddRandomVariance(v, f)` = uniform int in `[v − v·f, v + v·f]`.
- `UpdateInvestment`: if `!IsAvailable` (milestone `m_info.m_unlockingMilestone` lost), the value is
  scaled by `daysLeft / m_availableDaysBeforeBankrupt (7)` toward bankruptcy; notifies once.
  Price-change chirp when `|CalculateChangeRate(5 days)| ≥ m_notifyOnPriceChange (15%)` and
  `UnlockManager.Feature.Investments (52)` is unlocked.
- `AvailableForBuyingCount = max(0, m_maximumShareCount (1000) − owned)`.
  `CalculatePrice = round(value × shares)`. `IsAvailable()` = milestone passed (or no milestone).
  The milestone itself is prefab data (**unverified** which milestone per investment).

Per subclass `CalculateValue` (all read city-wide `District 0`):

| Class                                                                         | Formula                                                                                                                                                                                                              | Random                   |
| ----------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------ |
| `IndustrySectorAI` (`m_type`: Generic 0, Farming 1, Forestry 2, Ore 3, Oil 4) | `(District.GetXIndustryProductivity()*10 + GetXIndustryArea()*0.05) * 2`, then ±8%                                                                                                                                   | yes ±8% each day         |
| `HealthCareSectorAI`                                                          | `(100 − m_residentialData.m_finalHealth)*10 + GetHealthCareProductivity()*5 + GetHealCapacity()/10`                                                                                                                  | only base flat-line rule |
| `HealthCareSectorRandomAI` (RandomRisk)                                       | base 1000 × (1 + influences of pop/sick, pop/healCapacity, heal/sick, productivity) then random-state walk                                                                                                           | state machine            |
| `DeathCareSectorAI`                                                           | `GetDeathCareProductivity()*10 + (GetDeadCapacity()/100 + GetCremateCapacity())/10`                                                                                                                                  | base rule                |
| `FuelCompanyAI`                                                               | `VehicleManager.m_vehicleCount`                                                                                                                                                                                      | base rule                |
| `TrafficJellyAI`                                                              | `VehicleManager.m_lastTrafficFlow² / 3`                                                                                                                                                                              | base rule                |
| `VeryLegitCompanyAI`                                                          | `50 + District0.m_finalCrimeRate * 21`                                                                                                                                                                               | base rule                |
| `AirplaneInvestmentAI`                                                        | `log10(1 + 0.1·(resident+tourist avg plane passengers)) × 4000` (`TransportManager.m_passengers[4]`)                                                                                                                 | base rule                |
| `ShipInvestmentAI`                                                            | same with ship `m_passengers[3]`, ×3000                                                                                                                                                                              | base rule                |
| `ChirpCoinAI`                                                                 | `m_baseValue` (starts 1000) × `(1 + m_randomInfluence/100)`, clamped to `[m_minPossibleValue, m_maxPossibleValue]`; random state machine (`m_stateData` probabilities); rebases when influence passes 70% of min/max | heavy                    |

Industry productivity source: `District.AddIndustrialData` /
`AddIndustryData(subService, area, productivity)` from `IndustrialBuildingAI` /
`IndustrialExtractorAI` (zoned, subservices 5-9) and `ExtractingFacilityAI` (Industries DLC,
subservices 29-32). `GetGenericIndustryProductivity` =
`m_finalGenericIndustryProductivity / m_finalGenericIndustryCount` (average).

Verdict on idea 4: yes, vanilla industry sector stocks already track city industry (average
productivity ×10 plus total area ×0.05, ±8% daily noise). Our local IPO prices should not
double-count them, and the "value < 10 → random 10..2200" behaviour is a reminder to floor our own
fundamentals.

## 5. Per-building economics

- **No per-building income, profit or tax is stored.** Income is computed each active step and
  pushed straight to the city:
  - Commercial:
    `EconomyManager.AddPrivateIncome(amount, Service.Commercial, subService, level, taxRate)`
    returns the tax (`amount × taxRate × m_taxMultiplier / 1e6`, added to
    `m_income`/`m_cashAmount`); then the tourist share (by `m_touristCount`) is booked as
    `AddResource(TourismIncome 6, …)` and the rest as `AddResource(CitizenIncome 5, …)` (accounting
    role **unverified**).
  - Office:
    `EconomyManager.AddResource(PrivateIncome 4, amount, service, subService, level, taxRate)`;
    `OfficeFinancial` gets a bonus from `ImmaterialResource.TaxBonus (30)` ×
    `StockExchangeAI.taxBonus`.
  - Industrial / Extractor: `AddResource(PrivateIncome 4, amount, service, subService, level)`.
  - None of these calls carries the building id, so a Harmony patch on `EconomyManager` cannot
    attribute income. The amount comes from
    `PrivateBuildingAI.GetConsumptionRates(level, Randomizer(buildingID), productionRate, out …, out incomeAccumulation)`,
    which the mod can call itself to **estimate** a building's revenue, or the mod can postfix
    `CommercialBuildingAI/OfficeBuildingAI/IndustrialBuildingAI.SimulationStepActive`.
- Production rate is a local in `SimulationStepActive` (from `HandleWorkers` efficiency × goods
  availability × `HandleCommonConsumption` %). Visible proxies: `Flags.Active`, `Flags.RateReduced`,
  commercial `m_adults`, buffers.
- Workers: `PrivateBuildingAI.CalculateWorkplaceCount(level, Randomizer(id), w, l, out l0..l3)`
  gives slots; `m_allUnemployed`/`m_educatedNUnemployed` give vacancy %. `CitizenUnit` walk gives
  exact employees (**not traced**).
- Visitors: `CommercialBuildingAI.CalculateVisitplaceCount`; current visitors only via
  `GetVisitBehaviour` / citizen units (**not traced**).

## 6. Industries DLC areas (brief, for commodities later)

- `DistrictPark` (in `DistrictManager.m_parks`), `m_parkType`
  (`Industry 5, Farming 6, Forestry 7, Ore 8, Oil 9`), `m_parkLevel` (`Level1..Level5`),
  `m_totalProductionAmount` (ulong, drives level-up), `m_finalWorkerCount`, `m_finalStorageDelta`,
  per-resource `DistrictAreaResourceData`
  (`m_grainData, m_logsData, m_oreData, m_oilData, m_animalProductsData, m_floursData, m_paperData, m_planedTimberData, m_petroleumData, m_plasticsData, m_glassData, m_metalsData, m_luxuryProductsData`),
  each with
  `m_final{Consumption,Production,BufferAmount, IncomingTransfer,BufferCapacity,Import,Export}`
  (uint). Arrays `m_finalIncome/m_finalImport/ m_finalExport`. Methods: `IndustrySimulationStep`,
  `AddProductionAmount`, `AddExportAmount`, `AddImportAmount`, `GetIncomeAmount`,
  `GetProductionFactors`, `LevelUp`.
- Building AIs: `IndustryBuildingAI` (base, `m_industryType`), `ExtractingFacilityAI`,
  `ProcessingFacilityAI` (`m_inputResource1..4`, `m_outputResource`, rates), `WarehouseAI`
  (`m_storageCapacity`, `m_storageType`), `UniqueFactoryAI`, `MainIndustryBuildingAI`,
  `AuxiliaryBuildingAI`. These are player (service) buildings, not zoned, so they cannot be
  Historical and are not IPO candidates under the zoned design.

## Addendum: offices (traced by the main session)

- `OfficeBuildingAI.CheckBuildingLevel`: target level = the lower of two scores.
  - Education: district workers' education, weighted (educated1 + 2×educated2 + 3×educated3),
    thresholds 15 / 30 → levels 1 / 2 / 3.
  - Services: `OfficeBuildingAI.CalculateServiceValue` reads
    `ImmaterialResourceManager .CheckLocalResources` (a weighted sum of local service coverage),
    thresholds 45 / 90.
- Problems seen in `OfficeBuildingAI.SimulationStepActive`: service problem timer, tax problem timer
  (`TaxesTooHigh`); worker problems come from the shared `PrivateBuildingAI` path. No goods or
  customer problems (offices do not ship goods).
- Income: `EconomyManager.AddResource(PrivateIncome, …)` with the city's office subservice; the
  financial office subservice gets a bonus from `ImmaterialResource.TaxBonus` ×
  `StockExchangeAI .taxBonus` — the Stock Exchange already boosts nearby financial offices.

---

## Part 2: the world outside the city (for external exchanges)

New dumps: `dec/T4_*.txt` (OutsideConnectionAI, TransferManager, TouristAI, ZoneManager,
StatisticsManager, DistrictManager, District, DistrictTouristData, DistrictResourceData,
CargoStationAI), `dec/T4_IBAI_Price.txt` (IndustryBuildingAI price/exchange),
`dec/T4_CBAI_Export.txt`, `dec/E_t4.txt` (TransferReason, StatisticType, District fields),
`dec/ar/*.txt` (every type that calls `EconomyManager.AddResource`). Call graph: `calls/all.txt`.

## 7. Outside connections and trade

**Short answer: outside connections are bottomless, stateless sources and sinks.** There is no
outside stock, price curve or demand model for zoned goods. The only prices that exist are fixed
constants for Industries-DLC goods.

### 7.1 How an outside connection makes offers

- `OutsideConnectionAI` fields: `m_cargoCapacity`, `m_residentCapacity`, `m_touristFactor0/1/2`,
  `m_dummyTrafficReason`, `m_dummyTrafficFactor`, `m_transportInfo`, name flags. **No counters, no
  stock, no money fields.** Subclasses for road/train/ship/plane are prefab variants of the same AI
  (no subclasses found in `types.txt`; only `OutsideConnectionAI : BuildingAI`).
- `OutsideConnectionAI.SimulationStep` →
  `AddConnectionOffers(buildingID, ref data, productionRate, cargoCapacity, residentCapacity, touristFactor0..2, dummyReason, dummyFactor)`
  where `productionRate = GetProductionRate(100, EconomyManager.GetBudget(class))` — the only
  "throttle" is the player's budget slider for that transport service.
- Modifiers in `AddConnectionOffers`: district policy `CityPlanning.BoostConnections (524288)` adds
  +20% to the three tourist factors; `BuildingManager.m_finalMonumentEffect[5]` (road connections,
  service 9) or `[8]` (subservice 13) adds a % to tourist factors (which monuments feed those slots
  is **unverified**). All counts then ×productionRate/100.
- With `Building.Flags.Outgoing (128)` (traffic can enter the city from it): `AddOutgoingOffer` (=
  supply, i.e. **imports**) for exactly 11 reasons:
  `Ore 14, Oil 13, Grain 16, Logs 15, Goods 17, Coal 19, Petrol 31, Food 32, Lumber 37, SortedMail 94, IncomingMail 96`.
  Offer has `set_Unlimited(true)`, `Priority 0`, amount ∝ `m_cargoCapacity`. Same block adds move-in
  offers (`Family0..3 20-23`, `Single0..3 24-27`, `Single0B..3B 47-50`) scaled by
  `ZoneManager.GetIncomingResidentDemand()` × `m_residentCapacity`, and tourist/visitor offers
  (`AddIncomingOffer` with `Shopping/ShoppingB-H`, `Entertainment/B-D`, `TouristA-D 88-91`, and with
  the Hotels DLC `BusinessA-D 119-122`, `NatureA-D 123-126`).
- With `Building.Flags.Incoming (64)`: `AddIncomingOffer` (= demand, i.e. **exports**) for the same
  11 cargo reasons, `Unlimited`, `Active=false`, `Priority 0`, amount ∝ `m_cargoCapacity`.
- Offers are re-added every simulation step; nothing is decremented. In
  `TransferManager .MatchOffers`, when both sides are `Unlimited` the matched amount is `Max`
  instead of `Min`. Outside offers use priority 0 (lowest); inference: in-city partners win when
  they exist.
- Industries-DLC goods (`AnimalProducts 97 … LuxuryProducts 105`, `Fish 108`) are **not** in the
  outside-connection offer lists above; they reach the outside through cargo stations / warehouses
  (`WarehouseAI`, `WarehouseStationAI`, `CargoStationAI`; export path **not traced**).
- `StartConnectionTransferImpl` turns a match into a vehicle (cargo truck/train/ship/plane via
  `VehicleManager.GetRandomVehicleInfo`), immigrants (`CitizenManager.CreateCitizen`, education set
  from the reason), or tourists (§8). Dummy reasons `DummyCar/Train/Ship/Plane 43-46` spawn pure
  through-traffic scaled by `DummyTrafficProbability()` (vehicle/citizen headroom), no economic
  effect.

### 7.2 What the city earns or pays

- **Zoned industry/commerce (the IPO candidates): nothing per shipment.**
  `IndustrialBuildingAI/IndustrialExtractorAI/OfficeBuildingAI.StartTransfer` call
  `CommonBuildingAI.ExportResource` (stats only). Commercial imports of `Goods` cost nothing. The
  only money is the tax from `AddPrivateIncome`/`AddResource(PrivateIncome)` (§5). The zoned
  `FetchResource` calls in `CommercialBuildingAI/IndustrialBuildingAI.SimulationStepActive` are
  `PolicyCost (9)`, not trade.
- **Industries DLC: fixed prices.** `IndustryBuildingAI.GetResourcePrice(reason, service)` (static
  table, then `UniqueFacultyAI.IncreaseByBonus(7, price)` — a campus faculty bonus): Oil 400, Ore
  300, Logs 200, Grain 200, Goods 1500 only when the seller is `Fishing (25)` else 0,
  Coal/Petrol/Food/Lumber 0, AnimalProducts/Flours/Paper/PlanedTimber 1500, Petroleum/Plastics 3000,
  Glass/Metals 2250, LuxuryProducts 10000, Fish 600 (units: per 100 resource units, see below).
  `IndustryBuildingAI.ExchangeResource(reason, amount, source, target)` (called from
  `CargoTruckAI.ArriveAtTarget` / `ChangeVehicleType`): `price = amount × GetResourcePrice / 100`
  (+5% for Fish with `SustainableFishing`). If the seller is a player industry building and the
  buyer is private or an outside connection (`Flags & 192`) →
  `AddResource(ResourcePrice 15, price, sellerClass)` (export income). If the seller is
  private/outside and the buyer a player industry building → `FetchResource(ResourcePrice 15, …)`
  (import cost). Transfers between two different industry areas book both sides (net zero) and
  update `DistrictPark.AddExportAmount / AddImportAmount`. `PublicTransport (19)` endpoints (cargo
  stations) are pass-through. The price never depends on volume, time or the outside world.

### 7.3 Statistics kept

- `District.m_importData` / `m_exportData` (`DistrictResourceData`): `m_temp/m_final/m_average` ×
  `{Ore, Oil, Forestry, Agricultural, Goods, Mail, Fish}`. Writers:
  `OutsideConnectionAI .ImportResource` (district at the connection's position; no Fish bucket) and
  `CommonBuildingAI .ExportResource` (district of the exporting building). Mapping:
  Oil/Petrol/Petroleum/Plastics → Oil; Ore/Coal/Glass/Metals → Ore; Logs/Lumber/Paper/PlanedTimber →
  Forestry; Grain/Food/AnimalProducts/Flours → Agricultural; Goods/LuxuryProducts → Goods; mail
  92-96 → Mail; Fish → Fish (export only). `DistrictResourceData.Update`:
  `average = (average×18 + final + temp + 18) / 20` (slow EMA).
- `District.SimulationStep` writes city statistics `StatisticType.ImportedGoods (71)` = sum of
  `m_importData.m_average*` / 100 and `ExportedGoods (72)` = same for exports (incl. Fish).
  `OutsideConnectionsInfoViewPanel.UpdatePanel` reads the same `m_average*` fields of district 0.
- **No per-connection counters** (`BuildingManager.m_outsideConnections` is just the list).
- `DistrictPark` keeps per-area import/export for Industries areas (§6).

## 8. Tourism and visitors

- **Generation** (`OutsideConnectionAI.AddConnectionOffers`):
  `attr = ImmaterialResourceManager.CheckActualTourismResource()`, which is
  `x = CheckGlobalResource(Attractiveness 15) + CheckTotalResource(14)` (enum 14 = `LandValue` in
  `E_res.txt`; the intent is **unverified**),
  `attr = clamp(100·x / max(x+200, 200) − District[0].m_hotelData.m_finalBonus, 0, 100)`.
  `popScale = (100·H + 20000) / max(H +   20000, 20000)`with`H = District[0].m_residentialData   .m_finalHomeOrWorkCount`(so
  1 → ~100 as the city grows). Per wealth level`n =   popScale·attr·touristFactorN / 160000`
  (+random), summed, then scaled by the connection's capacity/budget, and offered as the
  tourist/shopping/entertainment reasons.
- **Spawn** (`StartConnectionTransferImpl`): `CitizenManager.CreateCitizen`,
  `Citizen.m_flags |= Tourist (2)`, `m_touristType` from the reason (TouristA-D 0, Entertainment 1,
  Shopping 2, Business 3, Nature 4), wealth drawn by weights `m_touristFactor0/1/2`
  (low/medium/high), hotel room via `HotelAI` when the target is a hotel.
  `StatisticsManager.Acquire(IncomingTourists 0) [wealth].Add(1)`.
- **Behaviour** (`TouristAI`): `FindVisitPlace` picks shopping/entertainment/business/nature
  targets, `GetLeavingReason` sends them home (out of the map). `TouristAI.SimulationStep` adds 1 to
  `District.m_tourist{1,2,3}Data.m_tempCount` (by wealth) for the district they are in;
  `AddTouristVisit` / `AddTouristVisitNode` add to `m_tempVisits` of the visited building's district
  (and pedestrian-zone parks). `DistrictTouristData.Update`: `final = temp`,
  `average = (average×8 + final + temp + 8) / 10`. `StatisticType.TouristVisits (34)` is set from
  `m_tourist1Data.m_averageVisits` etc.
- **Money** — only one place books `EconomyManager.Resource.TourismIncome (6)`:
  `CommercialBuildingAI.SimulationStepActive` (tourist share of shop income by
  `BehaviourData.m_touristCount`; confirmed by scanning every `AddResource` caller in `dec/ar/`).
  Everything else tourists pay is `PublicIncome (7)`: transport tickets (`HumanAI.EnterVehicle` via
  `VehicleAI.GetTicketPrice`, `TaxiAI.UnloadPassengers`), park fees (`HumanAI.EnterParkArea`,
  `DistrictPark.AddIncoming/OutgoingPassengers`), hotels (`HotelAI.SimulationStepActive`, class
  Hotel 27), events (`ConcertAI`, `SportMatchAI`, `RaceEventAI`). The Economy panel's "tourism
  income" for transport is an estimate: `EconomyPanel.GetTouristIncome` = public transport income ×
  `m_touristPassengers / (resident + tourist)` from `TransportManager.m_passengers[type]`.

## 9. Does the game simulate other cities?

**No.** Nothing models other cities' size, success or demand. Everything outside the map is the
outside-connection offer generator above, driven only by this city's own numbers (budget,
population, attractiveness, `GetIncomingResidentDemand`) and fixed prefab capacities. Outside
connection names (`GenerateName`, `m_useClose/Medium/FarNames`) are cosmetic.

- **Zone demand** (`ZoneManager.SimulationStepImpl`, all from `District[0]`):
  - `CalculateResidentialDemand` / `CalculateIncomingResidentDemand`: jobs (C+I+O+player
    `m_finalHomeOrWorkCount`), empty jobs, homes, empty homes, unemployed, homeless.
  - `CalculateCommercialDemand`: filled commercial workplaces vs. housed residents, plus commercial
    visit slots `m_visitorData` (filled vs. empty, clamped 0..50). Tourists occupy visit slots, so
    they raise commercial demand only indirectly (**inference**).
  - `CalculateWorkplaceDemand`: residents vs. filled/empty jobs and unemployment.
  - Each result goes through `DemandWrapper.OnCalculate*Demand` (ICities) and is clamped 0..100;
    `UpdateDemand` moves `m_actual*Demand` toward the target by at most ±2 per step, then
    `District.Calculate*DemandOffset` adds tax effects (`AddTaxEffect` →
    `EconomyManager .GetTaxRate`, taxation policies) and level mix. `m_fullDemand` forces full
    demand.
  - **No outside workers or shoppers.** Outside connections offer no `Worker0-3` reasons; the only
    people they send are immigrants and tourists. Workplace demand is purely internal.
- **Global economy state** (`EconomyManager`):
  - `m_taxMultiplier` (base 10000 = 100%) is a hidden **rubber band**, recomputed in
    `SimulationStepImpl`: with `x = (m_lastCashDelta − 200000) × 20000 / totalExpenses`, target
    `= 5000 + 5e7/(x+10000)` when profitable (falls toward 50%) or `20000 + 2e8/(x−20000)` when
    losing (rises toward 200%), then `× 1e9/(1e9+cash)` and `× 1e6/(1e6+population)`, clamped
    100..30000, EMA 99%/1%. It scales all private tax income (`AddPrivateIncome`). Rich, large,
    profitable cities get taxed less. This is the closest thing to a "national economy" variable but
    it reflects only this city.
  - Loans: `m_loans` (`Loan.m_amountTaken/m_amountLeft/m_interestRate/m_interestPaid/m_length`),
    offers from fixed `EconomyProperties.m_banks[].m_loanOffers` via `GetLoanInfo`. Interest rates
    are static prefab data, not a market rate.
  - Vanilla investments (`m_investments`, Financial Districts, §4) are the only "market".
- Inference for the mod: an external market must be **invented** by the mod (per-save or
  cross-save), optionally seeded from game signals that are real for this city: tourist inflow
  (`IncomingTourists`), import/export averages, `m_taxMultiplier`, attractiveness, and the
  Industries-DLC resource price table.

## 10. ICities hooks

From `ICities.dll` (types listed with `tlist`):

- `IEconomyExtension` (`EconomyExtensionBase`): `OnUpdateMoneyAmount`, `OnPeekResource`,
  `OnFetchResource`, `OnAddResource(EconomyResource, int amount, Service, SubService, Level)`, cost
  hooks (`OnGetConstructionCost`, `OnGetMaintenanceCost`, `OnGetRelocationCost`,
  `OnGetRefundAmount`). Called from
  `EconomyManager.AddResource / AddPrivateIncome / FetchResource / PeekResource` via
  `EconomyWrapper`. Can observe and change amounts, e.g. sum `TourismIncome`/`PrivateIncome` per
  service. **No building id**. ICities `EconomyResource` has no `ResourcePrice`:
  `EconomyWrapper.Convert` maps only game resources 0..14, so Industries trade money (resource 15)
  reaches extensions as the default case (likely `None`; **unverified**).
- `IDemandExtension` (`DemandExtensionBase`): `OnCalculateResidentialDemand`,
  `OnCalculateCommercialDemand`, `OnCalculateWorkplaceDemand`, `OnUpdateDemand` — a clean place to
  read demand or to push a mod-driven "national economy" effect into zone demand.
- `IEconomy` only exposes `currentMoneyAmount` / `internalMoneyAmount`; `IDemand` exposes nothing
  but `managers`. `IThreadingExtension.OnAfterSimulationTick` etc. remain the polling hook.
- **Not in ICities** (need direct reads or Harmony): outside-connection offers
  (`OutsideConnectionAI.AddConnectionOffers` postfix), import/export stats
  (`District.m_importData/m_exportData`, or `StatisticsManager` `ImportedGoods/ExportedGoods`),
  tourist counts (`District.m_tourist*Data`, `IncomingTourists`), Industries trade
  (`IndustryBuildingAI.ExchangeResource`), `m_taxMultiplier`. All are plain fields readable from the
  simulation thread without patching.

## 11. Event dialogs and UI sounds

For the IPO dialog and bell in [Local IPOs](../features/local-ipos.md#the-ipo-moment-planned).
Method for this section: `Assembly-CSharp.dll` and `ColossalManaged.dll` decompiled with ILSpy, and
the names of audio clips read as text from the game's `sharedassets*.assets` files. No sound was
listened to.

### The academic year report (Campus)

- The panel is `AcademicYearReportPanel` (a `ToolsModifierControl`), fetched with
  `UIView.library.Get<AcademicYearReportPanel>("AcademicYearReportPanel")`.
- `DistrictManager.OnAcademicYearEnded` runs on the simulation thread, updates each campus, then
  dispatches to the main thread and calls `PopupPanel(activeCampi, 0, wasTriggeredByButton: false)`.
  `CampusWorldInfoPanel` calls the same method with `wasTriggeredByButton: true` to reopen the
  report.
- `PopupPanel` returns without showing when it was not triggered by a button and
  `OptionsGameplayPanel.hideCampusAreaYearReport` is true. Otherwise it pauses the simulation
  (`SimulationManager.SimulationPaused = true`), centres the panel and fades it in over
  `m_ShowHideTime` (0.15 s).
- The checkbox is the `UICheckBox` named `StopBotheringMe`; `OnCheckChanged` writes
  `hideCampusAreaYearReport`, a `SavedBool` in the game settings file
  (`Settings.hideCampusLevelUpNotifications`) that Options → Gameplay also shows.
- `Hide` sets `SimulationPaused = false` whatever the state was before, then fades out. Esc and
  Return close the panel (`OnKeyDown`). `OnViewCampus` moves the camera to the campus.
- **The report plays no sound:** `AcademicYearReportPanel` has no `AudioClip` field and no
  `PlaySound` call.

### The Stock Exchange level-up dialog (Financial Districts)

- `StockExchangeWorldInfoPanel.OnUpgradeButtonClicked` calls `StockExchangeAI.LevelUp`, then
  `ParkAreaUnlockingPanel.OnMilestoneUnlocked` with the next level's milestone.
- `ParkAreaUnlockingPanel` serves parks, industry areas, airports and the Stock Exchange
  (`PanelType.StockExchange`). Its checkbox (also named `StopBotheringMe`) writes
  `OptionsGameplayPanel.hideStockExchangeUnlockingNotifications`.
- `ParkAreaUnlockingPanel`, `StockExchangeAI` and `StockExchangeWorldInfoPanel` have no `AudioClip`
  field and no `PlaySound` call.

### How the game plays UI sounds

- `AudioManager.PlaySound(AudioClip clip, float volume)` (and overloads taking `AudioInfo` or a loop
  flag) adds the clip to the manager's default group.
- The default group's volume is the UI slider (`Settings.uiAudioVolume`); `PlayAudioImpl` updates it
  with the main volume (`Settings.mainAudioVolume`) and does not silence it while the simulation is
  paused. The effect group (world sounds) is silenced while paused or loading, or when `MuteAll` is
  set.
- Callers on panels: `BuySellInvestmentPanel.ShowPanel` plays `m_openingSound`; its
  `OnButtonClicked` plays `m_buyingSound`, or `m_sellingSoundGood` / `m_sellingSoundBad`;
  `EconomyPanel` plays `m_SwooshInSound` / `m_SwooshOutSound`; `ChirpPanel` plays
  `m_NotificationSound` at the Chirper volume; `TutorialPanel` plays `m_NotificationSound`. These
  are public `AudioClip` fields filled in on the prefabs, so the code does not name the clips.
- Buttons play click sounds through `UIComponent.clickSound` and `UIView.playSoundDelegate`
  (`ColossalManaged.dll`).

### Bell and Stock Exchange audio

- **No stock exchange bell by name.** The word "bell" appears in no type, field or string of
  `Assembly-CSharp.dll`. Among asset names the only bell is the clip `tram_bell`
  (`sharedassets11.assets`, with the vehicle and building sounds).
- **`stock_exchange_levelup`** is a clip name in `sharedassets9.assets`, stored with the UI clips
  `gain`, `gain2`, `loss`, `loan_take`, `economy_tab`, `window_swoosh_1`, `window_swoosh_2`,
  `tutorial_alert`, `button_click` and `bulldozer_click`. Level-up clips elsewhere:
  `level_up_fanfare`, `building_level_up`, `industry_area_level_up` (`sharedassets11.assets`).
- **Unverified:** what `stock_exchange_levelup` sounds like (a bell or not); which prefab field
  holds it and when the game plays it (no code names it, and the panels above have no sound field);
  that `gain`, `gain2` and `loss` are the buy and sell sounds of `BuySellInvestmentPanel` (inference
  from the names); how a mod best gets a clip that no public field is known to hold (finding the
  loaded clip by name is the fallback; not tried in game).
