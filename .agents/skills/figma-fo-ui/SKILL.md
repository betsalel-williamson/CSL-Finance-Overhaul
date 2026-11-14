---
name: figma-fo-ui
description: >-
  Edit the Finance Overhaul Figma library (UI Map, Foundations, Components) with
  annotation-pin heuristics. Use whenever changing
  figma.com/design/ZJ0oXFLXCOFOXQsLxE86Vh, UI Map pins, FO components, or design
  tokens for this mod. Pins are red numbered dots tied to a legend — never
  overlapping annotation cards stacked on the artboard.
---

# Finance Overhaul Figma UI

**File:** [CSL-Mod-Financial-Markets-Mod](https://www.figma.com/design/ZJ0oXFLXCOFOXQsLxE86Vh/CSL-Mod-Financial-Markets-Mod)  
**Also load:** Figma plugin `figma-use` (every `use_figma`); `figma-generate-library` /
`figma-generate-design` when creating tokens or screens.

Docs: `docs/developer/ui-figma-map.md`, `docs/developer/ui-design-tokens.md`,
`docs/features/ui-catalog.md`.

## Definition of a pin

A **pin** is a **red circular marker** with a **unique number** on (or next to) a UI
element. That number ties to a **title + description** in a legend for the same
frame.

| Property | Rule |
| --- | --- |
| Appearance | Red filled circle (~20–24px) + white (or high-contrast) numeral |
| Identity | Integer **unique within its frame** (Options may use 1…n; Investments may also use 1…n) |
| Legend | Ordered list: `N · Title` then short description (docs/code/status allowed as secondary lines) |
| Spider lines | Optional thin lines from pin center toward its legend row — only when they clarify; never required clutter |
| Placement | Near the control, **not** covering important text/icons; pins never overlap each other |
| Clarity | A human must tell at a glance which element each number marks |

**Not a pin:** large status cards, chips stacked in a wrap grid, or side columns of
full `pin_id` / docs / code blocks. Catalog ids (`opt.title`, `host.row-value`)
belong in the **legend copy**, not as the on-canvas marker.

## Hard heuristics

1. **Pins never overlap** each other or bury critical UI text.
2. **One number space per annotated frame.** Restart at 1 for each Options /
   Investments / Planned frame. Do not require global uniqueness across the page.
3. **Band layout (default):**

   ```text
   [ Section title ]
   [ Annotated surface (screenshot or wireframe)  |  Legend 1…N ]
   ```

   Gap ≥ 24 between surface and legend. If the surface is wide, put the legend
   **under** the surface (still numbered 1…N), never a colliding stack on top of
   the screenshot.

4. **Spider lines** (when used): thin (~1px), muted stroke, from pin to legend
   row; do not cross through unreadably dense bundles — prefer fewer lines or
   legend-only when many pins.
5. **Root hugs content.** No fixed heights that clip pins or legends.
6. **Reference screenshots** max width ~480, `FIT`. Prefer pinning the
   **wireframe** (clear hit targets) and keep the photo as unlabeled reference
   beside it when space allows.
7. **Tokens.** Use FO semantic colors for panels/text; pin fill is **annotation
   red** (dedicated token `color/pin/marker` when present).
8. **Catalog sync.** Legend titles map to `docs/features/ui-catalog.md` /
   `docs/developer/ui-figma-map.md` field ids. Renaming ids updates docs in the
   same change.
9. **Planned frames** use the same pin grammar; mark legend rows as planned /
   out of MVP in the description, not with overlapping ghost cards.
10. **Stop gate.** Screenshot the annotated frame after edits. If any pin
    overlaps, covers text, or the number→legend link is ambiguous, fix before
    ending the turn.

## Pages

| Page | Owns |
| --- | --- |
| UI Map | Annotated screens + per-frame pin legends |
| Foundations | Variables + specimen |
| Components | `FO/*` including `FO/AnnotationPin` (red numbered dot) |

## Anti-patterns

- Treating “pin” as a 280px docs card on the canvas
- Side column of 8+ full cards next to a short wireframe
- Global pin numbers across the whole page (forces huge integers; breaks clarity)
- Pins centered on labels so text is unreadable
- Spider-line spaghetti through the middle of the screenshot

## Edit recipe (one band)

1. `create_session` → file `ZJ0oXFLXCOFOXQsLxE86Vh`.
2. Switch to **UI Map** once per `use_figma` call.
3. Place/update red dots on the wireframe; rebuild that frame’s legend 1…N.
4. Optional: add thin connector lines only where helpful.
5. Screenshot; fix overlap/ambiguity; sync Merlon pin tables if ids changed.
