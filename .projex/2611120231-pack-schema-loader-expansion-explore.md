# Pack Schema & Loader Expressiveness — Where Packs Hit Limits

> **Author:** Sonnet 5.5 (explore-projex subagent)
> **Type:** Domain
> **Status:** Complete (Done)
> **Parent:** Orchestrator
> **Related Projex:** 2611120227-runtime-hooks-expr-expansion-explore.md | 2611120227-persistence-game-integration-expansion-explore.md | 2611120229-ui-presentation-expansion-explore.md | 2611120231-authoring-tooling-sim-testing-expansion-explore.md (sibling slices) | NightExpeditionsReborn/.projex/2611120240-content-expansion-synthesis-eval.md (source of the unverified-capability claims; filename only, other repo)

---

## Summary

Pack schema = 20 top-level sections (`PackLoader.Sections`), strict at section level, **lax inside every definition**. Loader is solid on cross-pack plumbing (include/requires, `pack:id` qualification, pre-compile patching with per-pack rollback). Its limits cluster in five places:

1. **Silent acceptance** — unknown keys, wrong-typed keys, and unvalidated references inside definitions are dropped without a problem. `unlocked_by: [list]` silently makes an area always unlocked. `weight: "expr"` silently becomes 1.
2. **Weight/when live on the definition, not the pool membership** — so per-area depth curves force duplicate definitions (NER already has `enforcer_annex` / `enforcer_foundry` / `enforcer_welder`).
3. **Reuse is shallow and int-only** — `extends` is a top-level key replace (no deep merge); shared `effects` take int params only; shared `interactions` take none; areas cannot extend areas.
4. **Patching cannot reach into lists** — no index/id addressing, no insert-at, no patch on list-typed definitions (`behaviors`, `item_groups`), no per-patch `requires`.
5. **Fixed shapes** — layout is a pre-rolled linear list (no slot `when`/`chance`); loot is one item per roll with static int weights; intel picks are uniform, unconditional, and readable only as a count; `unlocked_by` is a single area id; one problem anywhere voids all of a pack's areas.

Of the 9 "Unverified Capabilities" the synthesis eval listed, this slice settles 3 outright (intel count, `unlocked_by` shape, per-area override) and confirms the rest need the sibling runtime slice. Verdicts in § Answers.

**Guiding Questions:**
1. What can a pack declare today, and what does the loader enforce vs ignore?
2. Where do real packs (NER) already strain the schema?
3. Which schema/loader expansions would be useful, and what do they cost?
4. Which synthesis-eval capability claims touching schema are true?

**Scope:** `src/Core/PackLoader.cs`, `Model.cs`, `PackSet.cs`, `Json.cs`, `README.md`, `api/ExpeditionApi.cs` (`ISpec`), `src/Game/PackRegistry.cs` (load flow), NER pack as evidence. Out: effect/hook/expression semantics (runtime sibling), persistence/game bridge, UI rendering, sim tooling (siblings). Read-only; nothing was built or run — every claim is from source reading, `file:ln` at time of reading.

---

## Context

**Why Now:** Human wants to return to NER content expansion. Five imagine docs assumed engine capabilities without reading the loader. Orchestrator asked 5 parallel explores on framework expansion; this one owns the declarative surface.

**Current State:** Framework 0.1.0, simulator passes, never played in-game. NER = 5157 lines of JSON/CSV across 8 files; 22 enemies (9 bosses), 3 areas.

**Stakeholders:** Pack authors (NER; future packs), C#-extension authors, the content-expansion effort.

---

## Investigation Targets

### Target: Load pipeline & pack boundary
**Rationale:** Defines what "a pack" is and how failures propagate.
**Status:** Done
**Findings:**
- Contract = file `ExpeditionFramework/main.json` in an enabled mod (`PackRegistry.cs` `MainFile`). Framework's own pack loads first, then mods in `ModLoader.Mods` order (`PackRegistry.LoadAll`).
- `PackLoader.Add` reads main + `include` tree (`ReadFile`, recursive, `seen` set rejects double include; include paths resolve against **main file's folder** `folder`, not the including file's). `CompileAll` = (1) patches, (2) `Declare` (behaviors/templates/interactions/values stubs/shared effects, all packs), (3) `CompileFile` per file, then `Finish()` runs deferred ref/pool/value-cycle checks.
- Top-level keys checked against `Sections` (`ReadFile`): `unknown section "x"`. So **main.json has nowhere for pack metadata** — no `version`, `schema`, `requires_framework`, `description`. Adding any such key today = error.
- Include grammar: string | `{file, requires:[mod ids]}` only (`Include`). `requires` = all-of, mods only. No any-of, no not-installed, no framework-version/feature gate.
- JSON dialect (`Json.cs`): `//` comments + trailing commas, duplicate key = hard error with line number, numbers → `double`. No `/* */`, no multi-line strings. Semantic problems carry a **JSON path** (`pack/file.rooms.a.enter[0]`), never a line (nodes are plain `Dictionary<string,object>`, no position info).
- **Failure granularity = whole pack.** `PackRegistry.LoadAll`: any problem for a pack → log + that pack's areas not added. No severity levels; a missing translation (if it were checked in-game) and a broken effect cost the same. `PackSet.Compile` rolls back *only* a pack's `patch` sections on trial failure, then reports.
- `PackSet` recompiles the entire content once per patching pack (`Attempt` per patcher, O(patchers × content)); fine today.

### Target: Section schemas — what each definition reads
**Rationale:** The actual declarable surface vs what README claims.
**Status:** Done
**Findings:** (keys read by `Compile*`; anything else in the object is ignored)

| Kind | Keys read | Notes |
|---|---|---|
| any (via `Section`) | `extends`, `weight` (int), `tags`, `when` | `weight` via `Int()`: string/expr → silently 1. Duplicate id across files in a pack → `already defined`. |
| status | `name`, `turns`, `rooms`, `intent`, `stats` (numbers only), `interactions`, `on` | stats can't be expressions (unlike enemy stats). |
| intent | `label`, `tell`, `tone`, `interruptible`, `next`, `do` | |
| loot | `items[]` (id string or `{item｜loot｜table, weight, when}`) | No count/quantity, no "nothing" entry, weight static int. |
| cart | `name`, `budget`, `stock[{item｜loot[], index, count}]` | |
| enemy | `name`, `stats` (Values), `texts` (raw strings), `opening`, `drops` (one loot id), `behavior`, `interactions`, `on` | `hp` required. `texts` values are **not** registered as text keys. |
| encounter | `enemies[id｜{id,count}]`, `on` | Only `weight`/`when` for random picks. |
| event | `text`, `scene`, `enter`, `choices` | |
| room | `text`, `enter`, `interactions`, `on` | |
| kit | `interactions`, `on` | Inherits generic `when`/`weight` unused. |
| area | `title`, `unlocked_by` (**one id string**), `ambience`, `art`, `hidden_when` (cond), `player`, `vars`, `pools`, `entrance`, `kits`, `groups`, `luck`, `texts`, `packing`, `intel[]`, `hud`, `interactions`, `on`, `layout[]` | `weight`/`when` parsed by `Section`, **never read** for areas (grep: no `area.When`/`area.Weight` in Game/Core). `hp` required in `player`; layout required. |
| layout slot | `room｜tag｜from｜pool`, `count` (Value), `unique` (default true) | No `when`, no `chance`. |
| intel slot | `id`, `pool`, `chance`, `counts[]`, `title`, `description`, `effect`, `tone`, `good[]` | No `when`. `pool` must be an area pool name. |
| interaction | `id`, `label`, `group`, `in`, `targets` (`enemy｜focus`), `turn`, `once`, `stay`, `sound`, `sound_else`, `when`, `where`, `uses`, `confirm`, `do`, `check`, `after`, `preview`, `use` | |
| hud | `title`, `rows[{when, parts, separator}]`, `enemies{line,pending,none,pending_scenes}` | |
| shared effect | `params` (name → default **Value**), `do` | Only keys checked strictly (`unknown key … (a shared effect has params and do)`). |

Only four places reject unknown keys: top-level sections, `include` objects, `patch` entries, shared-effect definitions. Everywhere else (enemy, room, area, interaction, every built-in effect's `Spec`) a typo is silent: `Spec` has no consumed-key tracking; `ISpec` (`api/ExpeditionApi.cs`) offers `Has/Text/Value/…` but no "these are all the keys I understand" hook, so C#-registered effects inherit the same blindness.

### Target: Reuse mechanisms — templates, shared pieces, behaviors
**Rationale:** Authoring cost in a 5k-line pack depends on these.
**Status:** Done
**Findings:**
- `extends` (`PackLoader.Extend`): `merged[field.Key] = field.Value` over the template's keys → **shallow**. An area that sets `vars` discards every template `vars`; a boss that sets `stats` must restate all stats. Depth cap 16; works across packs (`other:tpl`); applies to every `Section` kind. Templates are the only extendable thing: **`Extend` looks up `templates` only, so an area cannot extend another area** (needed for sibling variant areas: Long Night, Skeleton Crew).
- NER usage: `extends` ×3 (all areas → `night_area`), 0 uses on enemies despite 22 enemies each restating a 10–12 key `stats` block with many zero defaults (`bleeds:0, bribe:0, weak_flash:0`; unknown stats read 0 so those are noise).
- Shared `interactions` (`use`): shallow overlay of the call-site keys on the template (`Interactions`), **no params**. NER evidence: 8 bosses each carry a near-identical `weak_point` interaction differing only in (a) `uses.select` group, (b) label text key, (c) `weak_hit` args, (d) one optional extra `say`. Text keys/groups can't be parameters, so all 8 are written out (~28 lines each; `enemies.json`).
- Shared `effects` (`DeclareShared`/`CompileSharedCall`): `params` are `Value`s; the call stores `Dictionary<string,int>` args. **Int-only**: can't pass an enemy id, item group, text key, condition, or sub-effect. Bare call `{ "name": 3 }` binds the first param. Re-entrancy guarded (`EnterShared`).
- `values`: named expressions, no params; area-specific tuning must route through `vars` (`run.loot_tier` etc.). Cycle check exists (`CheckValueCycle`).
- `behaviors`: named weighted intent lists, nestable, cycle-checked (`BehaviorList`).
- `item_groups` has no composition: NER's `#tool` group (rules.json:17-22) re-lists every member of `light/pry/cutter/scanner/keycard/food/smokes/…` by hand. Groups and `item_stats` are **global, unqualified** (no `pack:` on `#food` or on stats keys) — intentional sharing, but two packs' same-named groups silently union and there is no opt-out.
- Item filters (`Items`): `#name` and bare ids are **never validated** (game items unavailable at load; group names could be, since all groups are known after load). A mistyped `#explosve` yields a button that never appears. Same for `tags` on `has_tag`.

### Target: Patch system
**Rationale:** The designed cross-mod extension channel; README sells it.
**Status:** Done
**Findings:** (`ApplyPatch`/`PatchDefinition`/`Walk`/`Assign`)
- Ops: `replace → remove → set → add` in that order. Target `pack:kind.id` or `kind.*`. Runs on raw JSON before `Declare`, so templates, values, shared effects are all patchable.
- **List addressing absent.** `Walk`/`Assign` descend only through objects; a path segment that hits a list errors (`"x" isn't an object`). Consequences: can't `set` `layout[1].count`, can't edit one entry of `interactions[]` or `hud.rows[]` by id/index; the only list ops are append (`add`) and delete-by-match (`remove`). Order-sensitive lists (layout, hud rows, behavior weights) can append but not insert-before.
- **List-typed definitions can only `replace`.** `PatchDefinition`: `if !(section[id] is Node def)` → problem. `behaviors` entries and `item_groups` entries are lists. A mod wanting one more intent in `melee_boss` must replace the whole list (and so conflict with anyone else doing it). (`item_groups` are union-merged across packs, so group additions work by redeclaring — behaviors have no such route.)
- `remove` object matcher = exact equality on all named fields (`Equals(v, f.Value)` — numbers vs bools fine, nested objects compare by reference → never match).
- Wildcards: `*` only; no prefix/tag/`where`. Per-entry gating: none — gate by moving the patch into a `requires` include.
- Patches can't create definitions (`no kind named`), can't edit the *patching* pack's own earlier patches, and apply in include/pack order — "later wins" is the only conflict policy; no `priority`, no detection of two packs writing the same path.
- Failure model good: a pack's patches are tried together; if they add any problem anywhere they are all dropped and the target loads as written (`PackSet.Compile`). Coarse (all-or-nothing per patching pack), but safe.

### Target: Pools, layout, intel (the "world shape" schema)
**Rationale:** Synthesis eval's biggest asks (Tonight Cards, area variants, depth curves) land here.
**Status:** Done
**Findings:**
- **Weight/when belong to the definition, not the pool membership.** `Pool(...)` (`PackLoader.Pool`) returns defs; `DefPool.Pick` weights by `def.Weight` and filters by `def.When`. Area `pools` are bare id lists (`CompileArea`). So two areas that use the same encounter cannot weight it differently or gate it at different depths without duplicating it. NER already pays: `enforcer_annex` (depth≥3) / `enforcer_foundry` (≥4) / `enforcer_welder` (≥5) are the same enemy under three ids (areas.json:6-28).
- `weight` is a compile-time int. No dynamic weights (`"weight": "run.alarm + 1"` → silently 1).
- **Layout is pre-rolled.** `ExpeditionRun.BuildLayout` runs in `Begin()`: `slot.Count(root)` and each room's `When(root)` are evaluated at run start, when `run.depth` = 0 and no fight/event has happened. So (a) `when` on a *room* cannot reference depth/alarm usefully, (b) layout slots have no `when`/`chance` (can't say "50% a vault slot"), (c) strictly linear — the "area graph → tree" idea has no schema home; branching would be an event-driven `next_room` choice or a new layout form. `unique` defaults true; pool exhaustion silently shortens the layout (`total <= 0 → break`).
- **Intel** (`CompileIntel`, `IntelRoller.Roll`, `IntelCards`): any number of slots (iterated lists, no cap in framework code); each slot picks uniformly without replacement from an area pool (no weights, pool entries are bare ids and **not kind-checked** — `CheckPool` only fires for rooms/events/encounters/enemies uses); `chance` or `counts[]` static; no `when` on a slot. Data can read picks only as `intel(slot)` = **count of picks** (`Expr.cs` `case "intel"`), or via `schedule {intel}` / `spawn {intel}` / `Enemies()` (`PackLoader.Spec.Enemies`). Data cannot ask "was `flood` picked?" nor run an effect from a pick. Workaround that works today: one slot per card with a 1-element pool, `chance:N`, read with `intel(slot) > 0` — no mutual exclusion between slots, no weighting.
- `unlocked_by` = **one area id** → `RefOrNull` → `Str()`. A list (or condition) fails `v is string` → null → area always unlocked, no problem logged. `DevTools` walks the single-parent chain. `ExpeditionLocation.IsDiscovered` checks `hidden_when(map.Root)` (MapContext: `game.*` only, no `run`) then one clears-counter > 0.
- `hidden_when`/`unlocked_by` are the only area-gating fields; no `difficulty`, `description`, `recommended`, or tags for area-level grouping (`tags` parsed on area, unused).

### Target: Loot, carts, item_stats
**Rationale:** "Haul is the save file" lens and trophy/Fence ideas depend on these.
**Status:** Done
**Findings:**
- `LootEntry` = {item｜loot｜table, weight, when}. One roll → one item id (`IInventory.RollLoot` → `Give`). No `count` (3 bandages = three rolls), no `chance` of empty, no `unique` per night, no `{loot, count}` bundles. Nested `loot:` refs exist (cycle handling in runtime, not compile — `AddLootEntry` only `Ref`-checks existence).
- Tiering is hand-rolled: NER lists `["ne_tier0","ne_tier1","ne_tier2"]` with `index: run.loot_tier` at each call (events.json:12-13) — N call sites repeat the list; schema has no "tiered table" (`loot: {tiers: [...], by: "run.loot_tier"}`).
- Cart: `stock` lines by `item` or `loot[]` with `index` Value and `count` Value; `budget` Value. No buy-only / sell-only flag, no price modifier field, no per-line `when`. Whether a pure-buy cart with empty stock works is a **runtime** question (`ITrade`/`OpenTrade`) — not resolvable from schema; defer to runtime sibling.
- `item_stats`: object per item id or `#group` with `tags[]` + numeric/bool props (`CompileItemStats`); later pack replaces numbers, tags union. Values truncate `(int)d` silently (0.5 → 0). Arbitrary numeric props readable via `item.prop` (README states; `best_item` reading is runtime). No way to derive stats (`grade` from tag), no per-pack scoping.
- Zero checks that `item_stats`/loot/cart item ids exist in the game — only possible with a game-side validator (persistence/integration sibling).

### Target: Text & localization declaration
**Rationale:** Cross-cutting cost the synthesis eval flagged (~10 keys/enemy).
**Status:** Done
**Findings:**
- All player-visible strings = keys into the pack's CSV; `TextKey` records `(pack, key)` in `content.TextKeys`; `variants:N` expands to `key_0..key_N-1`. **Only `tests/Sim` compares TextKeys against the CSV** (`Program.cs:22`); the in-game loader never does.
- Dynamic keys (`ne_forecast_{run.$pick}_title`, `{target.$weak}_tell`, `{area}_title`) take the `Dynamic` path and are **not registered** — so the Sim's missing-text check misses every templated key. NER leans on them (intel titles/descriptions, boss weak-point tells). Likewise `enemy.texts` raw strings never enter `TextKeys`.
- No inline literal text: every sentence needs a CSV row. Fine for localization; heavy for prototyping.
- Per-area `texts` map overrides framework `ef_*` keys; only at area level, not pack level.

### Target: Pack-level fault tolerance & metadata (revisit pass)
**Rationale:** All-or-nothing void was noted in the first dives; revisit judged its design cost.
**Status:** Done
**Findings:**
- Problems are strings in `Content.Problems`, attributed to the pack via `current`; no category/severity. Reference/lint-type issues (unknown `#group`, dynamic-text gaps, unused definitions) can't be added as warnings without the channel split.
- Whole-pack void is simple and safe for a game never played in-game; once packs exist with 3+ areas, one bad rare event hides all of them. Per-area isolation would need a def-dependency closure; `Ref()` already records every static reference (kind, id, `at`) in deferred closures, but pool/`event` effects and `Enemies()` resolve at runtime, so a closure is approximate. A cheaper step: severity levels first.
- No pack-declared compatibility: no `framework` min version, no feature flags. A pack using a future section on an old framework fails with `unknown section` — acceptable strictness, but invisible at the mod-manifest level (`manifest.xml` `GameVersions` only).

---

## Patterns & History

**Patterns Found:**
- **Strict top, lax middle:** sections/includes/patches/shared effects reject unknown keys; definitions and `Spec`s never do. Likely historical: the strict checks were added where cross-pack contracts form (include/patch/shared), definitions were left for speed.
- **Static where it could be dynamic:** `weight`, `chance`, `counts`, `good`, `unlocked_by`, status `stats`, loot `weight` are compile-time scalars while stats/`when`/`count` accept expressions. Authors reasonably expect uniform expression support; silent fallback hides the mismatch.
- **Definition-attached policy:** weight/when/tags sit on the thing, not on its use site → duplication (`enforcer_*`) and global side effects of tuning.
- **Shallow composition:** `extends`, `use`, `patch` all work on top-level keys; anything one level down must be restated or replaced.
- **Silent-null idiom:** `Str/Int/Bool` helpers return a fallback on wrong type. Convenient; the reason a list in `unlocked_by` or an expression in `weight` is swallowed.
- **Pack-local ids, global item vocabulary:** ids are `pack:id`; item groups/stats are global — consistent with items being the shared game namespace.

**Evolution:** Framework is 0.1.0 and ~7.7k lines C#; NER's JSON (machine-formatted, no comments in `enemies.json`/`loot.json`) reads as converted from an earlier hard-coded mod, then hand-edited (`combat.json`/`rules.json` have comments and shared effects). Schema growth so far was driven by that port (kits, hud, packing, luck are all NER-shaped) — the expressiveness gaps below are exactly where the port needed workarounds.

---

## Findings

### Discoveries
1. **`unlocked_by` accepts only a string; a list is silently ignored → area always unlocked.** (`CompileArea` → `RefOrNull` → `Str`.) The synthesis eval's "takes a list or condition" is **false**; worse, the failure mode is unlocked-by-default. Cheapest lint fix in this document.
2. **Typos inside definitions never fail.** Only 4 spots reject unknown keys. `"interuptible"`, `"wieght"`, a misspelled effect arg: silent. Biggest authoring-safety gap for a 5k-line pack.
3. **Pool membership carries no weight/when**, forcing duplicate defs per area curve (NER: 3 `enforcer_*` encounters).
4. **`extends` is shallow, areas can't extend areas**, and shared effects/interactions can't take non-int params → 8 near-identical boss weak-point blocks, 22 enemies restating `stats`, 3 area `vars` blocks, hand-copied `#tool` group.
5. **Patch is object-path-only**: no list addressing or insert-at, list-typed defs (behaviors) replace-only, no per-patch `requires`, `remove` object-match is exact-equality.
6. **Layout is pre-rolled linear** with start-of-run `when`/`count`; no slot `when`/`chance`; room `when: run.depth…` is evaluated when depth is 0.
7. **Intel is count-readable only**: data can't branch on *which* pick, so "Tonight Cards" must be one slot per card; no mutual exclusion, no weights, no slot `when`. (Works today, clumsily.)
8. **Loot is one-item-per-roll with static int weights** — no quantity, no tier table, no empty result.
9. **Text-key validation has two holes**: dynamic `{…}` keys and `enemy.texts` are invisible to the Sim's missing-text check; the in-game loader has no check at all.
10. **No pack metadata or framework-version gate**; `main.json` rejects unknown keys so metadata can't even be added without a loader change.
11. **Whole-pack failure** on any problem, no severity.
12. **`area.weight`/`area.when`/area `tags` parsed and unused.**

### Mental Model
A pack is a bag of named, pack-qualified definitions in 17 kinds plus global item vocabulary. Loading is: read all → patch raw JSON → declare reusable pieces → compile each definition into closures over `IScope` → defer reference checks. The loader's contract with authors is "structure errors are loud at section level, soft below it." Reuse operates on raw JSON before compile (templates, patch) or at call time with int args (shared effects). Shape-of-world (layout, pools, intel, unlock) is deliberately simple and static; dynamism lives in `when` + vars + hooks. Most expressiveness pain is where static schema scalars meet content that wants per-area / per-night / per-pick variation.

### Implications
Expansion candidates, ordered by value/cost. Each tagged **[L]** loader/schema only, **[R]** needs runtime work (coordinate with the runtime sibling), **[G]** game-side.

| # | Expansion | Fixes | Cost | Notes |
|---|---|---|---|---|
| E1 | **Unknown-key checking in definitions** via per-kind allowed-key tables + `Spec` consumed-key tracking (+ `ISpec` opt-in for C# effects). Warn first, error later | Disc. 2, 12 | M | Biggest safety win. Allowed-key tables must cover patch-added keys. Needs a severity channel (E9) to roll out without breaking NER overnight |
| E2 | **Type-strict scalar reads** (`weight`, `chance`, `unlocked_by`, `turns`…) → problem "must be a number/string" instead of fallback | Disc. 1, 2, 8 | S | Trivial and independent; fixes `unlocked_by` list silent-unlock |
| E3 | **Deep-merge `extends`** (objects merge recursively; lists replace unless `"+key"`/`"extends_add"`); **area-extends-area**; templates for `interactions` entries | Disc. 4 | M | Backward-incompatible for authors who rely on replace of an object; opt-in via `"merge": "deep"` on the template or a `deep_extends` key |
| E4 | **Parameterized shared interactions/effects with typed params** (`params: {group: "#food", label: "text_key", lost: 2}`) substituted at JSON level before compile — string/ID/text/condition params, not just ints | Disc. 4 | M | JSON-level substitution (like `extends`) avoids touching the closure compiler. Collapses NER's 8 weak-points to 8 call lines |
| E5 | **Pool entries as objects** `{id, weight, when}` in area `pools`/`from`, overriding the definition's own weight/when per area (def values stay defaults) | Disc. 3 | M | Retires `enforcer_*` duplicates. Needs `DefPool` to carry per-entry override; intel pools could use the same shape to gain weights |
| E6 | **List-aware patch paths**: `layout[1]`, `interactions[id=weak_point]`, `insert_at`/`before`/`after`; `add` into list-typed defs (`behaviors`, `item_groups`) ; wildcard by tag/prefix; per-patch `requires` | Disc. 5 | M-L | Walk/Assign need index/predicate segments. Add per-patch `requires` is S and high value |
| E7 | **Slot `when`/`chance` in `layout`**, plus clarifying doc/diagnostic that room `when` is evaluated at run start. Optionally late-bound slots `{ "lazy": true }` resolved on entering depth N | Disc. 6 | S (when/chance) / L (lazy) | Lazy needs run-time layout builder [R]. `when` at start is enough for "50% vault room" |
| E8 | **Intel `when` + pick-aware reads**: slot `when`; `intel(slot, "id")` / `picked(slot, id)` condition; optional per-pick `on_pick` effect / `set` map; weighted pool entries (via E5) | Disc. 7 | M [R: Expr + condition] | Direct enabler for Tonight Cards, Commissions, Rumor Broker. Without it, one-slot-per-card works but has no exclusion |
| E9 | **Problem severity** (`error`/`warn`), per-pack failure policy; warnings don't void the pack; Sim/CI can fail on warn | Disc. 11 | S-M | Prereq for E1/E10; also lets lint add unused-def warnings |
| E10 | **Reference lint**: validate `#group` names against known groups, unused definitions (declared, never `Ref`'d/pooled), dynamic-text key expansion check (enumerate `{…}` over known pools/ids) | Disc. 2, 9 | M | `#group` check is S (all groups known after load); dynamic-key expansion M; unused-def needs `Ref` use tracking |
| E11 | **`loot` upgrades**: `count`/`min,max`, `nothing` weight, `tiers`+`by`, dynamic weight Value, bundle entries `{loot, count}` | Disc. 8 | M [R: `DecideLoot`/`GiveRolled`] | Dynamic weight = widen `weight` to Value; needs per-roll evaluation |
| E12 | **`unlocked_by` generalized**: list (any/all), or `unlocked_when` condition over `game.*` and clear counts; area-level `requires` | Disc. 1 | S-M [G: counters live in game bridge] | Enables non-linear map ("area graph → tree"). `DevTools` unlock walk must follow |
| E13 | **Pack metadata in `main.json`**: `{ "pack": { "name", "version", "framework": ">=0.2", "features": [...] } }`; version-gated includes (`requires_framework`) | Disc. 10 | S | Reserve the key now; cheap forward-compat |
| E14 | **Item group composition**: `"tool": { "include": ["food","light",…] }`; optional `pack:`-scoped groups; validated `#name` | Disc. 4 | S | Removes hand-maintained union list |
| E15 | **Include ergonomics**: includes relative to the including file, glob (`"events/*.json"`), `requires_any`, `unless` | — | S | Quality-of-life; no urgent content need |
| E16 | **Per-area failure isolation**: only areas whose static reference closure includes a broken def are dropped | Disc. 11 | L | Approximate (runtime-resolved pools); do E9 first and see if still wanted |
| E17 | **Strings in `vars`/pools metadata**: pack-defined `lists`/constants tables (e.g., hazard tables per area read via `val`) | pattern: "tuning via vars" | M [R] | Speculative; revisit after E5/E8 |

**Suggested first slice (all [L], low-risk):** E2 → E9 → E1 (warn) → E10 (`#group`) → E13 → E14 → E7 (when/chance). That alone makes authoring a larger NER safe without any runtime change. Second slice: E5 + E8 (+E12) unlocks Tonight Cards / variants / depth curves. Third: E3/E4/E6 (authoring-density).

---

## Answers

**Q1. What can a pack declare, and what does the loader enforce vs ignore?**
20 top-level sections (`Sections`): `include, item_groups, item_stats, templates, effects, values, interactions, behaviors, statuses, intents, loot, carts, enemies, encounters, events, rooms, kits, areas, debug_kit, patch`; 17 patchable kinds. Enforced: valid JSON (dups rejected), section names, include/patch/shared-effect keys, required fields (`hp`, `layout`, `name`, `label`…), reference existence for ids of 10 kinds (deferred), hook names, effect/condition names, expression syntax, scene names, pool names, template/behavior/value cycles, "already defined". Ignored/unverified: unknown keys inside definitions and effect specs, wrong-typed scalars (fallback), `#group` and item ids, intel pool kinds, text keys (in-game), dynamic text keys, `area.when/weight/tags`.

**Q2. Where do packs strain the schema?**
In NER: duplicate encounters per depth curve (`enforcer_annex/foundry/welder`); 8 boss weak-point blocks; 22 enemies' repeated `stats`; manual `#tool` group union; hand-repeated tier-list at loot call sites; `mod(gunsmith_expansion)` repeated per loot entry (a `requires` include or patch would do, but loot-entry `when` is fine); intel cards needing one slot per card. Everything else is working as designed.

**Q3. Which expansions are useful?** § Implications table; priority slices at its end.

**Q4. Synthesis-eval capability claims touching schema:**

| Claim | Verdict |
|---|---|
| Third `intel` card / `intel(condition)` accepted | **True for card count** (unbounded list; framework imposes no cap — the game map's card capacity is a UI/game question). `intel(slot)` returns pick **count**; there is no `intel(condition)` and no way to branch on which pick. |
| `unlocked_by` takes a list or condition | **False.** One area id; a list is silently ignored → always unlocked. |
| Per-area override of `room_table` / `enter_floor` | **Mostly true via `vars` + `extends`**: areas override via their `vars` (`run.floors`, `run.loot_tier`…); `templates` carry shared defaults; but override is shallow (area `vars` replaces template `vars` wholesale) and `room_table`/`enter_floor` themselves are NER shared effects, not framework keys. Overriding an *effect* per area is not possible except by branching on a `var`. |
| Status `on` hooks fire on `room_enter`/`damaged` | Schema accepts any hook name in `on` for statuses (`CompileHooks`, `ResolveHook`); *firing semantics* → runtime sibling. |
| `damaged` hook on enemy, `self` = enemy | Schema accepts; semantics → runtime sibling. |
| Spawn by id; remove + `schedule`; enemy intents healing allies | `spawn` takes an enemy ref (`Ref(...,"enemy")`) — schema-OK; behavior → runtime sibling. |
| `item_stats` arbitrary numeric props readable from `best_item` | Schema: **yes**, arbitrary numeric/bool props (and `#group` keys); `best_item` reading → runtime sibling. |
| Pure-buy `trade` with empty stock | Schema allows `stock` omitted (loop over empty list OK; no compile error). Behavior → runtime sibling. |
| New item ids exist in game; salvage accepted | **Unverifiable from loader** — item ids are never validated; needs game-side check. |

**Q5 (extra). Does the "Gunsmith via `requires` include" pattern work as README says?** Yes; `Include` → `HasMod`, evaluated once; tested in `TestOptionalData`. NER itself uses per-entry `when: mod(...)` instead, equivalent.

---

## Open Questions

- [ ] Does the game's map cap how many intel cards a location shows? (UI sibling) — affects how many Tonight Card slots are usable.
- [ ] Is `room.when` meant to be evaluated at run start? Evaluate whether any shipped NER room relies on `run.depth` there (grep found none in NER rooms; confirm).
- [ ] Do any consumers rely on shallow `extends` replace-of-object semantics (NER's three area blocks don't) — needed before E3.
- [ ] E1: what allowed-key tables look like for patch-added keys and C#-registered effects.
- [ ] Is whole-pack failure intentionally punitive (forces fixing) or a v0.1 simplification? Ask the user before E16.
- [ ] Verify with a throwaway pack (not done here, read-only) that `unlocked_by: ["a"]` and `"weight": "1+1"` produce no problem — read from `Str()`/`Int()` code, not executed.

---

## Appendix

**Sources:** `src/Core/PackLoader.cs` (all 1618 lines skimmed by structure; read in full: Sections, Add/CompileAll, patch, ReadFile/Include/Declare/DeclareShared/CompileFile, Section/Extend, Compile* for all kinds, Interactions/CompileInteraction, Text/Line, Ref/Exists, ValueOf/Effect/Condition/Items, Spec), `src/Core/Model.cs` (defs), `PackSet.cs`, `Json.cs`, `ExpeditionRun.cs` (BuildLayout, IntelRoller, MapContext), `Builtins.cs` (`give_loot`, effect registry list), `src/Game/PackRegistry.cs`, `IntelCards.cs`, `ExpeditionLocation.cs` (`IsDiscovered`), `api/ExpeditionApi.cs` (`ISpec`), `README.md`, `tests/Sim/Program.cs` (headers and bad-content tests), NER `ExpeditionFramework/*.json` (structure, areas.json, enemies.json via script, loot.json head, grep counts), synthesis eval § Unverified.

**Limitations:** Nothing executed (read-only mandate; building writes bin/obj) — behaviors inferred from source. Effect/hook/expression runtime semantics, persistence, UI, and sim tooling not examined (sibling documents). Full `ExpeditionRun.cs` and `Builtins.cs` not read beyond the cited parts. Line numbers cited as `file:ln` refer to files as of 2611120231 and drift with edits.
