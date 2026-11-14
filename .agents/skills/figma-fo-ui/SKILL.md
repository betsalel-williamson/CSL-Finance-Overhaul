---
name: figma-fo-ui
description: >-
  Edit the Finance Overhaul Figma library (UI Map, Foundations, Components) with
  annotation-pin heuristics. Use whenever changing
  figma.com/design/ZJ0oXFLXCOFOXQsLxE86Vh, UI Map pins, FO components, or design
  tokens for this mod. Pins are red numbered dots parented onto screenshots and
  new UI surfaces — never floating docs cards.
---

# Finance Overhaul Figma UI

**File:** [CSL-Mod-Financial-Markets-Mod](https://www.figma.com/design/ZJ0oXFLXCOFOXQsLxE86Vh/CSL-Mod-Financial-Markets-Mod)  
**Also load:** Figma plugin skills `figma-use` + `figma-generate-design` (every mutating
`use_figma`); `figma-generate-library` when creating tokens or components.

Docs: `docs/developer/ui-figma-map.md`, `docs/developer/ui-design-tokens.md`,
`docs/features/ui-catalog.md`.

## Definition of a pin

A **pin** is a **red circular marker** with a **unique number**, **attached to the
screenshot or new-UI surface it annotates** (child of that surface frame, or an
overlay frame that shares the same bounds). The number ties to a **title +
description** in a legend for the same band.

| Property | Rule |
| --- | --- |
| Appearance | Red filled circle (~20–24px) + white (or high-contrast) numeral |
| Attachment | **On** the screenshot and/or **on** the new UI wireframe — not in a free-floating docs column |
| Identity | Integer **unique within its band** (Options 1…n; Investments 1…n; etc.) |
| Legend | Separate list beside/under the surfaces: `N · Title` + short description |
| Connectors | Optional thin lines only pin→legend when readable; never replace on-surface placement |
| Placement | Near the control, **not** covering important text/icons; pins never overlap each other |
| Clarity | At a glance, each number marks a specific control on the photo or wireframe |

**Not a pin:** large status cards, chips stacked in a wrap grid, or side columns of
full `pin_id` / docs / code blocks. Catalog ids (`opt.title`, `host.row-value`)
belong in the **legend copy**, not as the on-canvas marker.

## Hard heuristics

1. **Pins live on the surfaces.** Parent pins to the screenshot frame and/or the
   new-UI frame (or a same-size overlay). Do not park pins only on the page or in
   the legend column.
2. **Pins never overlap** each other or bury critical UI text.
3. **One number space per annotated band.** Restart at 1 for each Options /
   Investments / Planned band.
4. **Band layout (default):**

   ```text
   [ Section title ]
   [ Screenshot with pins on it ]  [ New UI with matching pins ]  [ Legend 1…N ]
   ```

   Same pin numbers may appear on both surfaces when they mark the same feature
   (vanilla host vs planned FO chrome). Gap ≥ 24 between columns. Legend is copy
   only — never a substitute for on-surface dots.
5. **Root and legends hug content.** No fixed heights that clip pins or legends.
    Every `legend-N` row must use **vertical HUG** (`counterAxisSizingMode: AUTO` /
    `layoutSizingVertical: HUG`). Fixed row heights (e.g. 10px after removing a pin
    glyph) stack text on top of itself — never leave a legend row FIXED-height.
6. **Reference screenshots** max width ~480, `FIT`. Prefer also pinning the
   **new UI / wireframe** so the map shows host chrome and FO intent together.
7. **Tokens.** Use FO semantic colors for panels/text; pin fill is **annotation
   red** (`color/pin/marker` when present). Prefer `FO/AnnotationPin` instances.
8. **Catalog sync.** Legend titles map to `docs/features/ui-catalog.md` /
   `docs/developer/ui-figma-map.md` field ids. Renaming ids updates docs in the
   same change.
9. **Planned frames** use the same pin grammar; mark legend rows as planned /
   out of MVP in the description.
10. **Stop gate.** Screenshot the annotated band after edits. If any pin is not
    clearly on a screenshot/new-UI control, overlaps, or the number→legend link
    is ambiguous, fix before ending the turn.

## Pages

| Page | Owns |
| --- | --- |
| UI Map | Annotated screens + per-band pin legends |
| Foundations | Variables + specimen |
| Components | `FO/*` including `FO/AnnotationPin` (red numbered dot) |

## Anti-patterns

- Treating “pin” as a 280px docs card on the canvas
- Pins only in the legend column (disconnected from screenshot / new UI)
- Side column of 8+ full cards next to a short wireframe
- Global pin numbers across the whole page
- Pins centered on labels so text is unreadable
- Connector spaghetti through the middle of the screenshot

## Edit recipe (one band)

1. `create_session` → file `ZJ0oXFLXCOFOXQsLxE86Vh`.
2. Switch to **UI Map** once per `use_figma` call.
3. Place/update `FO/AnnotationPin` instances **on** the screenshot and **on** the
   new UI; rebuild that band’s legend 1…N.
4. Optional: thin connector lines pin→legend only where helpful.
5. Screenshot; fix overlap/ambiguity; sync Merlon pin tables if ids changed.
