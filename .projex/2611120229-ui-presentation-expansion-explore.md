# UI & Presentation Expansion — Status Quo + Candidate Primitives

> **Author:** Claude Sonnet 5.5 (Explorer)
> **Parent:** Orchestrator
> **Type:** Domain
> **Status:** Complete (Done)
> **Related Projex:** NightExpeditionsReborn `2611120240-content-expansion-synthesis-eval.md`, `2611120214-night-run-shape-and-conditions-imagine.md`, `2611120214-combat-depth-foes-and-bosses-imagine.md` (prior artifacts; claims re-verified below, not assumed)

---

## Summary

Slice: how a run is shown and played in-game (`src/Game/*` presentation + the Core model the UI reads). Read-only; no code changed.

**Status quo, in one paragraph.** One run = 3 draggable windows laid out by `PanelLayout`: **info** (top centre, scrolling HUD labels, title), **log** (left, one TMP label of the last 80 of 200 lines), **actions** (right, button rows grouped under tone-coloured group headings). HUD = pack-defined rows of tinted spans (`hud.rows`, per-part `when`/`each_item`/`cases`/`tone`, `separator`), plus a hardcoded enemy block. Presentation primitives available to packs: 6 tone colours, per-line `tone`, `bar` text meters, button `preview` percent, `confirm` dialog (game's own), `sound`/`sound_else`, one `art` sprite per area, `ambience` id per area, intel cards on the map, `hint` (once-per-run), `say`, `night_log`. Everything else (icons, per-room art, mid-run ambience, tooltips, disabled-with-reason buttons, submenus) does not exist.

**Key findings (ranked by leverage for "richer runs"):**
1. **Button explosion is the real readability cliff, not tell volume.** `ItemUse.Each` × `Targets:"enemy"` creates one button per (item × enemy). Actions window is a plain column with **no scroll**; `PanelLayout.Arrange` clamps its height to available space, so overflow is likely clipped (unverified in-game).
2. **Tells are already doubly surfaced** (log line from `Telegraph` + a HUD intent row per enemy) — the prior "tells swamp the log" worry is real only for the log; the HUD intent row is hardcoded (indent + `IntentLabel`, no pack control, no interruptible marker).
3. **Tones are colour-only and a closed set** (`ToneColors`: good/bad/dim/heading/enemy/hint; unknown tone → silently plain). No glyph fallback (accessibility), no pack-defined tones, no log priority.
4. **No log structure**: flat lines; only `heading` adds a blank line. No turn/room dividers, filters, or collapse; roll detail inlined as dim suffix.
5. **Art is single-sprite-per-area**, set once at `StageObject`; no condition-driven swap, overlays, or per-scene art. Ambience is a fixed id; no mid-run change.
6. **Unavailable options vanish** (no "locked: needs X" display), hurting discoverability in item-gated content like NER.
7. Pack has **never been played in-game** (NER README) — all layout/clipping claims here are code-read, not observed.

---

## Guiding Questions

1. What can a pack express visually/aurally today, and where are the hard limits?
2. Which limits bite when runs get richer (more foes, conditions, items, events)?
3. Which new presentation primitives are useful, and at what layer (pack-only / loader+Core / Unity glue)?
4. Are the prior projex readability tensions (tell overload, HUD crowding, third intel card) real per code?

**Scope:** In — `HandsWindow`, `PanelLayout`, `GameSounds`, `BagMenuPatches`, `ExpeditionLocation` (UI half), `IntelCards`, HUD/Line/Tone model, localization handling. Out — schema/loader internals, hooks/expressions, persistence, sim tooling (sibling slices; touched only where presentation depends on them).

## Context

**Why Now:** Content-expansion imagination round proposed conditions, more foes/bosses, parley, extra intel cards; all assume the UI can carry them.
**Current State:** UI written against NER's 3 areas (Foundry/Annex/Sump); sample pack minimal.
**Stakeholders:** Pack authors (NER), player reading a night run.

---

## Investigation Targets

### Target: Window layout (`PanelLayout.cs`, `ExpeditionLocation.Arrange/OpenLog/OpenActions`)
**Rationale:** Capacity limit for richer runs.
**Status:** Done
**Findings:**
- Constants: `PanelWidth=500`, `LogWidth=460`, `MinPanelHeight=420`, `InfoMinHeight=140`, `ButtonSpacing=4`. Everything is capped shares of the game-picture rect (log ≤28% width, actions ≤32%, info ≤ 40% height). Measured in world space; handles letterbox (`LetterboxManager`) and avoids the vanilla ground window (`GroundRect`/`BottomClear`).
- Log: left column, `MinPanelHeight` tall or down to ground window; actions: right, top-anchored, height = needed content height (`actionsHeight`, recomputed in `RefreshButtons`) but **`Mathf.Max(top - needed, actionsFloor)`** — i.e. never taller than the space above the ground window.
- Actions window content is `BeginColumn` (no `BeginScrollFlexible`); info and log both use `BeginScrollFlexible`. So info/log scroll, actions don't → many buttons would exceed the window. **Unverified in-game; consequence inferred from code.**
- Windows draggable (`SetDraggable(true)`); auto-arrange only runs for 1 s after open / on canvas resize, so the player's drag sticks.
- Buttons wrap into rows by measured text width (`FlowIntoRows`/`MeasureWidth`), group heading forces a new row. A 460–500-unit-wide window fits ~2–3 mid-length buttons per row.
- Close buttons hidden on all three; run can only end via the run's own leave button (`Depart`).

### Target: Info window / HUD pipeline (`Model.cs` HudDef, `ExpeditionRun.Hud()`, `ExpeditionLocation.RefreshHud/RowText`)
**Rationale:** Where conditions/acts/meters would appear.
**Status:** Done
**Findings:**
- `Hud()` returns `List<List<HudSpan>>`; empty row = separator. `RefreshHud` splits into `hudBlocks` labels (1 + number of **static** `separator` rows at `BuildPanel` time), joins spans with 3 spaces, colours via `Paint`. If more separators appear than blocks, extra rows silently merge into the last block (`blocks.Count < hudBlocks` guard).
- Row parts: `line` (cases, tone, args, `bar`), `when`, `each_item`. Row `when`, part `when`. Per-part tone only via the line's `tone` (cases carry their own) — NER hand-writes 3 near-identical HP lines to get green/yellow/red (areas.json ~L88–93, ~9 lines of JSON for one threshold-coloured bar). → **primitive gap: threshold tone on bars.**
- Enemy block is hardcoded: marker `"> "`/`"  "` (heading tone) + `hud.enemies.line`, then an indented `"    " + IntentLabel(e)` row tinted by `Intent.Tone`. Pack can't restyle the intent row, can't show an "interruptible" marker, can't show tell turn-count/telegraph progress. In `pending_scenes` the `pending` line replaces it. `DefaultHud` (no `hud` block) prints HP/turn/effects/enemies.
- `bar` = ASCII `[####......]` (`Lines.Bar`, 10 cells default). No icons/sprites/sub-cell precision.
- Refresh model: polled every 0.5 s + `dirty`; `SetLabel` skips equal text → cheap. HUD height unbounded but scrolls.
- Info title: `hud.title` cases (NER switches on `run.cleared`).
- Prior claim "Act-based HUD, zero framework change" (night-run-shape imagine) — **confirmed feasible** for a text row; but it competes for the same fixed text block, no emphasis hierarchy beyond tone.

### Target: Log (`ExpeditionRun.Log/Say`, `ExpeditionLocation.LogText/FormatLine`)
**Rationale:** Tell/narration volume; richer runs = more lines.
**Status:** Done
**Findings:**
- `LogLine(tone,text,roll)`; run keeps 200, UI shows last 80 (`LogLinesShown`), joined into **one TMP label** `ef_log`, rewritten on any change; scroll pinned to bottom for 3 frames (`scrollFrames`).
- Formatting: `heading` → blank line + bold + tone; `hint` → italic; roll detail → ` <size=85%>…</size>` dim suffix; else tone colour. No per-actor tag, no turn index, no collapse. Rooms announce via `Say("heading", room.Text)` (`ExpeditionRun` L~509).
- Tells: `Telegraph(enemy)` → `Say(intent.Tone, intent.Tell)` — one log line per enemy intent chosen. 3 enemies ⇒ 3 tells + their outcomes interleave with player lines in one wall (prior projex's "swamp" concern ✓ plausible). Mitigation available today only in data: shorter tells, `Interruptible` intents. No framework cap exists.
- `hint` effect: logs once per run per text (`FirstTime("hint:"+text)`), tone `hint`.
- Rich text works wherever a loc string passes through TMP (`<b>`, `<size>`, `<color>`); packs could already embed it but it's undocumented and bypasses tone palette.

### Target: Actions window / option model (`ExpeditionLocation.CurrentButtons/RefreshButtons`, `RunOption`, `Interaction`)
**Rationale:** Scale limit under richer content.
**Status:** Done
**Findings:**
- One `RunOption` per bound interaction (owner × target × item × paired). `Interaction.Group` (default `"move"`), `AreaDef.GroupOrder` for ordering; group heading localized `ef_group_<id>` (else raw id). Unlisted groups sort last.
- Button signature = `Group+Label` joined; rebuild only on change (good: no flicker). Click guarded by `dirty` (ignored for ≤1 tick after a prior click).
- Label may carry a `preview` percent (`Interaction.Preview` → `percent` arg, clamped 0–100). `Confirm` (`ConfirmDef`: when/text/yes/no) uses the game's `GenericConfirmationUIManager`, re-checks `run == asked`.
- **Options failing `When` are not shown at all** — no locked/greyed variant, no reason. (Confirmed: option list is built through `Options()` L771; failing conditions drop.) For NER-style "carry the right tool" design this hides the very information (what you're missing) the puzzle depends on, except via `packing` card on the map.
- No tooltips on buttons (builder only exposes `AddButton(label, action)` in code read here).
- No submenu / second-step picker: item-keyed actions multiply buttons. `ItemUse.Limit` caps, `Order` sorts — the only mitigation.
- Trade: `run.Trading` swaps buttons to deal/finish and **hides the info window** (`window.Hide()` in `Choose`) because game counters occupy the screen centre.
- End: `run.Over` → single leave button, label `ef_leave_<outcome>` if defined.

### Target: Sound (`GameSounds.cs`, `sound` effect, `Interaction.Sound/SoundElse`, `ItemStats.Sound`)
**Rationale:** Audio feedback channel for tells/events.
**Status:** Done
**Findings:**
- Pack names a **game audio id** (`AudioManager.CanPlay`); no pack-bundled audio path observed. `Play2` for ordinary sounds; firearms use `Play3` with first-shot detection (`FirstShotLength`, sample-envelope analysis, cached per clip) so one action = one shot.
- De-dup/stagger: same id in one frame dropped; first sound `Now`, others `Later` by 0.45 s (`LaterSoundDelay`). Ensures one press doesn't stack, but means a combo (hit + tell stinger + status) plays serialized — long action chains get slow-audible.
- `PlayItem(item, fallback)`: item sound if playable else fallback.
- No sound tied to **tone**/log category (e.g. a tell stinger), no UI click sounds, ambience fixed per area (`AmbienceId => area.Ambience ?? "dumping_ground"`).

### Target: Art / stage (`ExpeditionLocation.StageObject`, `ImageKey`, `area.Art`)
**Rationale:** Image primitives.
**Status:** Done
**Findings:**
- One sprite: `CustomUIManager.GetModSprite(pack, ArtId)` from `Textures/Items/<art>.png` (NER ships 3: ne_annex/ne_foundry/ne_sump). Wrapped in a `SpriteRenderer` stage, width `SceneWidthUnits=9.6`, cached in `stage`.
- Null art → `StageObject` returns null (silently no background). Not condition-aware: no swap by depth/scene/boss/condition, no overlay layer (e.g. flood, red-alert tint), no enemy portraits, no item art in HUD.
- Panels sit over this art; layout avoids only the vanilla ground window, not the art's focal point.

### Target: Map-side presentation (`IntelCards.cs`, `GetTitle`, `Missing`, `packing.card`)
**Rationale:** Pre-run forecast channel (conditions, third card question).
**Status:** Done
**Findings:**
- Intel cards = game afterhour events, per slot up to `MaxPicks` cards, rolled once per day, kept in `PlayerStore` counters. Tone is game enum Good/Bad/Mixed only (slot `tone`, `good` list override). Text via `title`/`description`/`effect` Lines with `{run.$pick}` style text templating.
- Card count scales with `Counts` array (NER's `rooms` slot allows up to 2); a **third card** is just `Counts` length — no framework limit seen; layout on the game's map card is the game's, untested (as the prior projex said).
- `GetTitle` appends missing-gear text (`packing.card`) to map title — only map channel for "you're missing X".
- Intel readable in-run via `intel(slot)` expression — so the HUD/log can echo the forecast (pack-only).

### Target: Text & localization (`ModEntry.L`, `Localization/en.csv`, `FrameworkText`, `area.Texts`, `Lines.Render`)
**Rationale:** Text growth and overrides.
**Status:** Done
**Findings:**
- Framework strings: 53 lines in `Localization/en.csv`, keys `ef_*`; areas override by `texts` map (NER overrides found/roll/group/log/actions/trade/leave keys). Fallback `FrameworkPack`. Unknown group label falls back to the raw group id.
- Line variants: `variants: N` picks `key_0..key_{N-1}` via run RNG (so text variation consumes RNG; sim determinism consideration — outside slice).
- No plural/gender helpers, no markup helpers; arg substitution only. Single-locale in repo (en.csv); game localization mechanism is `ModHelper.GetLocalized`.
- Colour: `ToneColors` hard-coded dictionary (hex); alternatives not pack-configurable.

### Target: Hands window & bag menu (`HandsWindow.cs`, `BagMenuPatches.cs`)
**Rationale:** Only pre-run UI the framework adds.
**Status:** Done
**Findings:**
- Harmony postfixes on `StoreResponse.ShowAfterhourResponse`, `ResponseList.CreateAfterhoursResponse` (wraps each choice to set `choosing`), `EmporiumEntry.HideResponseWindow`. Window: 2 slots "pocket1/pocket2" via `MachineHelper.CreateMachineryWindow`, placed under the bag choice (`Place`), items move to the night-screen hands on `OnAfterhourArriveEarly` if `UsesAfterhourBag`; returned on shutter/sleep/return/menu close. Slot labels hardcoded to vanilla `item_expedition_box_label_pocket*` keys.
- Fixed at two slots; not data-driven. Low relevance to run readability; relevant only if "loadout/pre-run" presentation expands (e.g. a pre-run briefing panel would be a sibling window with the same open/close lifecycle patches).

---

## Revisit (second pass)

Re-read whole doc; gaps found and closed:
- **Gap:** Does the sim exercise any presentation? `tests/Sim/Program.cs` touches `Options()`, `Log_` (tone asserted for unknown-id error) — but never `Hud()` or the button/log formatting; layout/format (`Paint`, `FormatLine`, `FlowIntoRows`) live in `src/Game` (Unity) and have no tests. → HUD text correctness (including `cases` arg rendering) is only checkable by playing the game. See Implication P-7 (cross-slice with authoring tooling).
- **Gap:** Is tone validated at load? `PackLoader` L691/918/1211/1224 read `tone` via `Str(n,"tone")`, no allow-list → typos silent. → candidate P-3 includes loader warning.
- **Deepened:** Actions overflow claim — re-read `RefreshButtons`/`Arrange`: `actionsHeight` is *needed* height; `SetBox` height = `top - needed` bounded below by `actionsFloor`; if needed > available the window shrinks below content. Clipping behaviour unknown (depends on `CustomUIBuilder` container mask). Marked unverified.
- **Confirmed prior-claim check:** "tells are text lines in the log" ✓ (`Telegraph`); "HUD rows support when/cases" ✓; "intel card third card unknown" ✓ still unknown in UI.

---

## Patterns & History

**Patterns Found:**
- **Everything is a `Line`** (text key + args + variants + tone + cases). Presentation grows by extending `Line` consumers, not by new widget types. Any new primitive that reuses `Line` costs little in the loader.
- **Poll-and-diff UI** (`dirty` + 0.5 s recheck + signature/string equality) — keeps Unity API calls minimal; new displays should follow the same diff pattern.
- **Layout by measured world rect, capped shares** — robust to resolution but all-or-nothing: three fixed windows, no extra region for new displays.
- **Tone as the only semantic channel** — both HUD and log encode meaning through 6 colours.
- **UI is game-glue-heavy and untestable offline**: Core is pure/simulatable; `src/Game` is not. Presentation primitives are cheapest where they live in Core (Hud(), Options(), Log) and are rendered generically.

**Evolution:** Not traced (no VCS in this repo; `git` not initialised). Comments in `PanelLayout`/`ExpeditionLocation` record earlier hard-won fixes (letterboxing, ground window arriving late, layout settling a frame after text change) — the layout code is defensive because it was tuned against real game behaviour.

---

## Findings

### Discoveries
1. **No scroll on the actions window, but button count is multiplicative** (`items × targets`); NER content growth (more guns/consumables/foes) pushes this first. Largest single readability risk; unverified in game.
2. **HUD enemy/intents block is not pack-configurable** (marker, indent, intent line format, interruptible marker) while everything else in HUD is.
3. **Threshold-coloured meters cost 3 hand-written cases each** (NER HP). Every new meter (tide, clock, heat) repeats it.
4. **Log lacks structure** (turn/room dividers, tell priority, filtering) — flat 80-line wall.
5. **Tone is a closed colour-only vocabulary**; unknown tones silently degrade; no glyph/colour-blind fallback.
6. **Unavailable actions are invisible** — hurts item-puzzle readability and teaches nothing.
7. **Art/ambience are static per area**; no condition-driven swap, no overlay layer.
8. **Sound is game-id-only, de-duplicated and staggered 0.45 s**; no tone/tell-linked stingers.
9. **Presentation code has zero automated coverage**; sim never calls `Hud()`.
10. **Confirm dialog + preview % + hint + intel cards exist** — risk-signalling primitives are better than they look; the gap is *information about unavailability* and *structure*, not warnings.

### Mental Model
Core produces three pure streams per tick: `Hud()` (rows of spans), `Log_` (lines), `Options()` (grouped buttons). `ExpeditionLocation` is a thin Unity renderer for each stream into one window each, diffing and relayouting on change; `PanelLayout` places windows in free screen space; `GameSounds` serialises audio requests; `IntelCards` and `GetTitle` are the pre-run channel through the game's own map card. A new presentation feature is cheapest when it is (a) another field on `Line`/`HudDef`/`Interaction`, (b) rendered by the existing renderer for that stream. It gets expensive when it needs a new window region or Unity widget the game's `CustomUIBuilder` may not expose.

### Implications — Candidate Expansions

Layer: **P** pack-only, **C** Core+loader (pure C#, sim-visible), **G** Unity glue (`src/Game`, only testable in game). Value/Cost: H/M/L, my judgment.

| # | Candidate | Layer | Value | Cost | Notes / risk |
|---|-----------|-------|-------|------|--------------|
| P-1 | **Scrollable actions window** (`BeginScrollFlexible`) + sticky group headers | G | H | L | Removes clip risk; trivial if builder supports it (info/log already do). Fix before any content that increases buttons. |
| P-2 | **Submenus / "pick a verb, then a target/item"** (`menu` group or `Interaction.Opens` scene); alternatively `Collapse` groups with count badge | C+G | H | M–H | Structural answer to item×target explosion. Interacts with scene system (`In`), `Stay`, `TakesTurn`. Needs sim support (options become 2-step). Prefer collapsible group first (cheap) over true submenus. |
| P-3 | **Pack-defined tones + glyph fallback**: `main.json`/area `tones: {name: {color, glyph, bold}}`; loader warns on unknown tone | C+G | M–H | L | Fixes silent typos and colour-only encoding; lets packs add `tell`, `loot`, `alarm`, `system`. Keep the 6 built-ins as defaults. |
| P-4 | **Threshold tone on `bar`**: `{ "bar": [v,max], "tone_by": [[0.5,"heading"],[0.6,...]] }` or `tone_steps` on a part | C | M | L | Collapses NER's 3-case HP pattern; reusable for tide/clock meters. Pure Core → sim-testable. |
| P-5 | **HUD intent row control**: `hud.enemies.intent` Line (+marker config), auto-glyph for `Interruptible`, optional "turns until" counter | C | M–H | L–M | Makes tells legible without log; prior projex's cap-simultaneous-tells idea becomes presentation not rules. |
| P-6 | **Log structure**: turn/round dividers (`Say` heading-light), per-line actor tag, tell tone with distinct treatment, optional collapse of older rooms; keep `LogLinesShown` configurable per area | C+G | M | M | Cheap partial: new `LogLine.Kind` ("tell"/"roll"/"narration") rendered differently; dividers from `turn_start`. |
| P-7 | **Text preview in sim**: render `Hud()`/`Options()`/log tail to console per step, `--show` flag | C (tooling) | H | L | Only way to check HUD `cases`/args and groups without launching the game. Cross-slice with authoring tooling; flag to that sibling. |
| P-8 | **Locked options with reason**: interaction `locked` Line shown as disabled button (or dim row) when `When` fails but `show_locked: true` | C+G | M | M | Needs builder support for disabled buttons/tooltips (unknown). Fallback: render as dim label row in actions window (works with existing `AddLabel`). Risk: clutter; opt-in per interaction. |
| P-9 | **Condition-driven art** (`art` as Line cases) + **overlay sprites** (`overlays: [{art, when}]`) | C+G | M | M | Stage swap on change; overlay as extra SpriteRenderer layers with sorting orders. Pack must supply more PNGs (art-cost). Conditions (flood, blackout) get a visual for free. |
| P-10 | **Mid-run ambience / stinger**: `ambience` cases or `ambience` effect; `sound` tied to tone/Line (`sound` on Line) | C+G | M | M | Needs AudioManager capability check (unknown if ambience can swap while in a location). Easier half: `Line.sound` (tell stinger). Interacts with `GameSounds` stagger. |
| P-11 | **Pre-run briefing / recap panel** (show intel + conditions + packing at arrival; end-of-run summary card) | P (log lines at `run_start`/`run_end`) then G | M | L (P) / M (G) | Pack-only version works today via `say`/`hint` at `run_start`; a framework panel adds structure. |
| P-12 | **Pack-supplied audio** (loaded alongside `Textures/`) | G | L–M | H | Unknown if game's `AudioManager` exposes mod clip registration; check only if content demands. |
| P-13 | **Localization helpers** (plural, ordinal, list-join) | C | L | L | `{items}` list args exist; plural is the visible gap. Low demand. |
| P-14 | **HUD sections with titles / alignment** (labelled blocks, 2-column rows) | C+G | L–M | M | Info is one text label per block; columns would need table layout; current spans + 3-space gap suffice for NER. |

Dependencies: P-1 → prerequisite to P-2/P-8. P-3 → used by P-5, P-6, P-10. P-4 and P-5 are pure Core and can be done first and exercised by sim (P-7).

---

## Answers

**1. What can a pack express today, and where are the limits?**
Text HUD rows (conditional, per-item, tinted, ASCII bars), flat tinted log with once-hints and roll suffixes, grouped text buttons with preview % and confirm dialog, game-id sounds, one art sprite and one ambience id per area, intel cards on the map. Hard limits: 6 fixed tones, no icons/portraits, no per-scene art, no tooltips, no disabled/locked options, no submenus, no scroll on the actions window, hardcoded enemy HUD block, no pack audio, no in-run ambience change.

**2. Which limits bite with richer runs?**
(a) Actions window height/button multiplication (code-read, unverified in game). (b) Log wall when multiple enemies tell per turn. (c) Information hiding of locked options. (d) Colour-only tone for state-heavy HUD (conditions, clocks). Panel real estate for extra HUD rows is *not* a bottleneck — the info window scrolls and is cheap to extend.

**3. Which new primitives are useful, at what layer?**
Highest leverage per cost: P-1 (scroll), P-4 (threshold tone), P-5 (intent row control), P-3 (tones+glyph), P-7 (sim text preview). Next tier: P-2 (collapse/submenu), P-6 (log structure), P-8 (locked options), P-9 (art by condition). Later/speculative: P-10, P-12, P-14.

**4. Are the prior projex readability tensions real per code?**
- Tell overload: **Partly.** Tells hit the log one line per intent per enemy (✓); but HUD already shows per-enemy intent labels (the prior doc didn't mention it), so the *current* intent is visible without reading the log. Risk is the log wall, not hidden information. Cap-on-interruptible is a rules choice; framework gives no cap.
- HUD crowding from conditions: **Mostly no** — info window scrolls; an added row is cheap. The real crowding is the actions window (target #4).
- Third intel card: **Unknown in-game**; framework doesn't limit it.
- "One condition per night as one HUD row reusing existing bar grammar": **feasible with zero changes** (text rows + `bar`); P-4 would make it cheaper.

---

## Open Questions

- [ ] Does `CustomUIBuilder` expose disabled buttons, tooltips, scroll containers inside a column, icon images? (Needed for P-1, P-8, P-14; API is the game's, not in this repo.)
- [ ] Actual overflow behaviour of the actions window at, say, 12–15 buttons in game (clip, overlap, or auto-grow beyond ground window).
- [ ] Can `AudioManager` swap ambience while a location is active? Can mods register clips?
- [ ] Does the game's map card tolerate a third intel card without layout breakage?
- [ ] Does unscaled screen resolution (960×540 canvas) leave the log legible at 80 lines with richer markup?
- [ ] Is colour-blind accessibility a goal of the project? (determines weight of P-3 glyph fallback)

## Appendix

**Sources:** `src/Game/{HandsWindow,PanelLayout,GameSounds,BagMenuPatches,ExpeditionLocation,IntelCards}.cs`; `src/Core/Model.cs` (Line, Interaction, HudDef, IntentDef, AreaDef); `src/Core/ExpeditionRun.cs` (Options, Hud, Log, Say, Telegraph, Lines); `src/Core/Builtins.cs` (say/hint/sound effects); `src/Core/PackLoader.cs` (tone/art/hud parsing); `Localization/en.csv`; `README.md`; NER `ExpeditionFramework/areas.json` (hud block), NER README, NER `.projex` imagine/eval docs; `tests/Sim/Program.cs` greps.
**Limitations:** No in-game observation (pack unplayed; game not run). `CustomUIBuilder`/`AudioManager`/game UI APIs are not in this repo, so P-1/P-8/P-10/P-12 feasibility rests on assumption. Repo has no git → no history. Line numbers from a single read; `PackLoader.cs` (1618 lines) read only via targeted greps. Value/cost ratings are judgment, not measured.
