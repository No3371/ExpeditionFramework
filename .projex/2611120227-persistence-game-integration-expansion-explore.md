# Persistence & Game Integration: Expansion Exploration

> **Status:** Complete (Done)
> **Author:** Claude Sonnet 5.5 (Explorer)
> **Parent:** Orchestrator
> **Type:** Domain / Architectural
> **Related Projex:** 2611120240-content-expansion-synthesis-eval.md (in NightExpeditionsReborn/.projex; its § Unverified Capabilities and § Convergences "cross-run persistence wall" are the prompt for this exploration)

---

## Summary

Framework ↔ game seam = `IBridge` (Core/Model.cs) implemented by `GameBridge` (Game/GameBridge.cs), plus `ExpeditionLocation` (save counters), `IntelCards` (daily state), `TradeCounter`, `ItemReader`, `PackRegistry`, `HandsWindow`.

**Persistence today = int counters on `AfterhourLocationState` only**, all framework-owned, 3 kinds: `ef_clears`, `ef_best_depth`, `ef_intel_*` (day + picks). **Data packs have zero write access** to any persisted state, and read access to just 4 `game.*` values (`luck`, `roll_luck`, `wounds`, `maimed`) — not even the framework's own clear count. Run vars (`run.*`) die with the run. Confirms the synthesis eval's "persistence wall" — and it is deeper than the eval stated: cross-run memory is not merely missing, it is unreachable from data.

**The base game already offers the missing substrate**: `ModHelper.Get/Set/Remove/Has/ClearModData` (+ `Int/Bool/Float` variants, `GetModDataKeys`, `Write/ReadModDataFileText`), a per-mod string KV store. Three sibling mods in this workspace already use it (ProbablySecondHand, CharacterProbe, ZohFramework GameHost). ExpeditionFramework uses none of it. Best expansion = a **pack-namespaced persistent store** exposed to data as `memory.*` read + `remember`/`forget` effects. ~100-150 LOC in Game/ + Core, no new game hooks needed.

Other useful, cheaper expansions: more `game.*` reads (`clears`, `best_depth`, `day`, per-area), 2-3 extra `IBridge` verbs (heal, status on player, item-by-id check), run-end hooks that survive crashes, `unlocked_by` generalised to a condition. Ranked table in § Findings → Expansion Candidates.

**Guiding Questions:**
1. What survives a run today, and through what mechanism?
2. What can data read of / write to the game, and where are the gaps?
3. Which game capabilities are available but untapped (hooks, ModData, item APIs)?
4. Which expansions are worth building, at what cost, with what risk?

**Scope:** In: Game/ files named in the dispatch + the `IBridge` contract + `game.*` path in Expr + the base game's ModHelper/ModHook surface (read via `strings` on Assembly-CSharp.dll and sibling mods' usage, not decompiled). Out: pack schema/loader details, hooks/expression engine design, UI, authoring/sim tooling (sibling explorations). No code was changed.

---

## Context

**Why Now:** NER content expansion ideas (Ledger, Tithe/standing, Veteran Debts, Hollow-from-spared-enemies, rival, companions) all assume cross-run memory. Eval says it needs "a small C# store in ExpeditionFramework". This asks what is truly available and what to build.

**Current State:** Framework is 1886 LOC in Game/. No ModData use. 1 Harmony group (bag menu), 4 ModHook subscriptions (all in HandsWindow).

**Stakeholders:** NER content authors (data-only), future packs, framework maintainer.

---

## Investigation Targets

### Target: Persisted state inventory (ExpeditionLocation, IntelCards, DevTools)
**Rationale:** Defines what "survives a run" means now.
**Status:** Done
**Findings:**
- Store: `PlayerStore.Instance.GetAfterhourState(locationId)` → int counters `GetCounter/SetCounter` (ExpeditionLocation.cs:105,193-196; IntelCards.cs:104-135). Per-location (location id = area id with `:` → `_`).
- Written keys: `ef_clears` (+1 in `Leave()` when `run.Outcome == "cleared"` or run var `cleared > 0`, ExpeditionLocation.cs:196), `ef_best_depth` (max, :194), `ef_intel_day`, `ef_intel_{slot}_{i}` (stores position+1 in pool; 0 = none).
- Read for: `IsDiscovered()` (`unlocked_by` → previous area's `ef_clears > 0`, :105); intel `Pick`; nothing else. **`ef_best_depth` is write-only** — no data path, no UI reads it (grep: only :194).
- Writes happen only in `Leave()` (and intel rolls). Consequence: if the game quits / crashes / the player alt-F4s mid-run, the clear is not counted. Mid-run save/load: `Arrive()` always builds a fresh `ExpeditionRun` (:121-127) → run state is not serialized; reload mid-run = restart (inferred from code; not played).
- `DevTools` writes `ef_clears = 1` on the chain of `unlocked_by` ancestors to unlock an area for testing (DevTools.cs:33-35).
- Counter ints only, location-scoped: a pack cannot key by anything but the area it ships.

### Target: What data can read (game.* path)
**Rationale:** The read side of integration.
**Status:** Done
**Findings:**
- `game.NAME` → `Run.Game(name)` → `IBridge.GameValue` → `GameValues.Get` switch (GameBridge.cs:184-197): `luck`, `roll_luck` (consumes a roll!), `wounds` (`healthData.woundState`), `maimed`. Default 0 — unknown names silently read 0 (Expr.cs:107; README says so), so a typo'd `game.clear` fails silently.
- Also exposed through run vars: `credits` (Bridge.Credits), `ground` count (ExpeditionRun.cs:326-327). Items via `carried`/`best_item`.
- Map-time (`MapContext`) shares the same `game` func, so `hidden_when` and intel can read `game.*` too (ExpeditionLocation.cs:59,103).
- Not readable: clear count of any area, best depth, day number, trust/level of store, cash tier, item inventory outside the carried set, whether another mod is enabled (that exists only at pack-load: `PackSet.HasMod`).

### Target: What data can write (IBridge verbs)
**Rationale:** The write side; which real-game mutations exist.
**Status:** Done
**Findings:** `IBridge` (Model.cs:392-421) verbs: `Consume`, `GiveItem`(spawn → ground), `RollTable`, `Equip`, `AddCredits`/`SpendCredits`, `ClearGround`, `TakeBack`, `LoseFound`, `LoseCarried(percent)`, `Wound(minor|major)`, `NightLog`, sounds, `OpenTrade/CloseTrade/TakeCart`.
- **Credits**: `AddCredits` → `ModHelper.AddCredit`; `SpendCredits` → `PlayerStore.ModCash(-n, false)` guarded by balance check. Fine.
- **Items**: all items arrive on the **ground** (`TryAcceptAllOrDestroy(Ground, …, pack)`), not the bag. Failure to accept = item destroyed silently with null return. Items outlive the run only if the player picks them up before leaving; `LoseFound` removes what was found.
- **Wounds**: only inflict (`ReceiveMajorWound`/`Minor`). No heal / cure verb, and no read of which wound. HP is run-local (`player.hp`), never written back; the run's HP is a separate resource from the game's wound state.
- **Death**: `Depart()` → `ReturnNow()` if hp ≤ 0 else `Return()` (ExpeditionLocation.cs:437-441). No framework-applied death consequence beyond what the pack does itself (pack can `wound`, `lose_items`).
- **Nothing for**: store trust/relations, spawning a *customer* later (CharacterProbe shows the game supports queued visits via `ModHook.OnGenerateCustomerVeryLate` + `SetClientSource`), item modification/tags on owned gear, unlocking shop stock, XP/perks, day-skip/time cost of an expedition.
- Verbs live behind data effects (`give_item`, `credits`, `lose_items`, `wound`, `night_log`, …); a pack adding one needs C# via `IExpeditionExtension.AddEffect` (api/ExpeditionApi.cs) — which breaks "data-only".

### Target: Cross-run memory — what exists in the base game
**Rationale:** The eval's key claim is that persistence needs a new store.
**Status:** Done
**Findings:**
- `ModHelper` exports (strings in Assembly-CSharp.dll): `GetModData`, `SetModData`, `RemoveModData`, `HasModData`, `ClearModData`, `GetModDataKeys`, `GetModDataInt/Bool/Float`, `SetModDataInt/Bool`, `WriteModDataFileText`, `ReadModDataFileText`, `ModDataFileExists`, `DeleteModDataFile`, `ListModDataFiles`, `GetModDataFolder`. Signature pattern `(modId, key[, value])`.
- Used by siblings: ProbablySecondHand `BatchStore.cs` (JSON blobs, call/purchase state, plus file-based rates cache), CharacterProbe `Visits.cs` (stage/day ints, `ClearModData` on reset), ZohFramework `GameHost.cs:115-132` (generic string KV wrapper for its consumer mod — an existing precedent of a *framework* wrapping ModData for a data-driven consumer).
- **Unverified:** whether ModData is per-save or global-per-mod. Evidence suggests per-save (CharacterProbe resets via `ClearModData` and "reload replays same day cleanly"; ProbablySecondHand ties it to `dayCounter`), but a 5-line probe is needed (write key, load another save, read). Must resolve before design: a global store would leak Tithe/standing between saves.
- `AfterhourLocationState` counters are the framework's present choice; they are per-location ints and tied to the game's afterhour save — safe, per-save for certain, but limited to ints and one location scope.
- Items, credits, wounds, night_log are not memory: items can be lost, credits mix with real economy, night_log is a text feed (`PlayerStore.AddNightLog`) not readable back.

### Target: Hooks and events (HandsWindow, BagMenuPatches, ModHook surface)
**Rationale:** What triggers exist to attach persistence to and what is untapped.
**Status:** Done
**Findings:**
- Framework subscribes: `OnModAssetsLoaded` (pack discovery), `OnAfterhourArriveEarly`, `OnReturningStoreLate`, `OnGoingSleepEarly`, `OnShutterOpenedEarly` (HandsWindow.cs:23-26; hands put back).
- Available but unused (add_On* list in Assembly-CSharp): `OnAfterhourLeaveEarly/Late`, `OnAfterhourArriveLate`, `OnAfterhourEventStarted/Ended`, `OnNewGameStarted`, `OnGameLoadedEarly/Normal/Late/Init`, `OnGoingSleepLate`, `OnWakingUpEarly/Late`, `OnLeavingStoreEarly/Late`, `OnShutterClosed*`, `OnLootRolled`, `OnLootTablesLoaded`, `OnGenerateCustomer*` (5 phases), `OnItemInstalled`, `OnTrustRefreshed`, `OnNetworkUpgradeUnlocked`, `OnDifficultyApplied`, `OnHandlingNightlyServices*`, `OnPlaceInventorInventory*`. Instance events `OnItemSold/OnItemBought/OnItemTraded` also exist.
- Run end commit is in `Leave()` only. `OnAfterhourLeaveEarly` / `OnGoingSleepEarly` would be crash-resistant safety nets, or commit points for incremental writes.
- Bag-menu is Harmony-patched because the game has no hook (BagMenuPatches.cs). So the framework already accepts Harmony when a hook is missing.

### Target: Items & loot (ItemReader, LootRegistry use)
**Rationale:** Item integration is half the game surface.
**Status:** Done
**Findings:**
- `ItemReader.Read` turns `GameItem` → `RunItem`: weapons (`FIREARM`/`MELEE_WEAPON`/tag `melee`) one-per-gun with per-gun stats read via `GunHelper` tags; everything else grouped by `identifier` into a stack. RunItem persists inside a run via `known` dict (gun magazine across the trip) — per-run, not persisted.
- Stats come from pack `item_stats` (`ApplyStats(content)`); only `capacity_bonus` is read from attachments. Unknown base-game item facts (value, rarity, condition/durability, owner flag) are not exposed to data.
- `GiveItem(itemId)` = `ItemSpawner.Spawn`; `RollTable` uses `LootRegistry.HasGroup/Roll/RollGroup` — game loot tables addressable by name; pack tables are separate.
- **Quirks:** `ItemReader.Matching` hardcodes .45/12ga → 10mm (game sells none). `OpenAmmoBoxes()` auto-unpackages ammo boxes on arrival (side-effect not data-controlled). `LoseCarried` destroys *items*, not damages them. `IsPartOf` handles guns holding magazines. Everything given lands on the ground — no "put directly in bag" verb.
- Missing: item **mutation** (condition, tag, rename), spawning with a quantity/state, `give_item` into hands/bag, reading value/price for a `#valuable` rule, removing a specific stack member vs destroying all-of-kind. "Trophy" (NER idea) = a named item; works if its id exists in the game.

### Target: Trade (TradeCounter)
**Rationale:** Most complex bridge; synthesis eval flagged "pure-buy trade with empty stock (Fence)".
**Status:** Done
**Findings:**
- Hijacks the shop's counters: trader becomes `store.currentClientInstance`, stock spawned onto the front counter, player drags to back counter. One `StoreClient` per cart key (budget memory), `ALL_TYPE` buying tag = trader buys anything the budget covers.
- `Open` fails (returns false, no state changed) if the store already has a client — pack must handle it.
- `Close` returns `Bought` names + `CashChange`; leftover goods go back to the cart unless owned.
- `EnsureCart`: stock comes from `request.Stock` ids at first open; **empty Stock with budget = pure-buy trader works by construction** (no code requires a non-empty stock) — resolves that eval item at the Game layer; whether the game's negotiation UI tolerates an empty front counter is not verified.
- Cart is per `ExpeditionLocation` instance, discarded at `Leave()` (`trade.Discard()`); unsold stock does not persist across runs, and the budget resets. A persistent trader (selling back next night, standing-based prices) is not supported.
- Faction is fixed `FACTION_LOWER_LEVEL`, sprite random scav; budget conversion fixed (100). Pack cannot choose trader face, faction, or intent (`SELLNBUY` fixed; no pure-sell or pure-buy intent).

### Target: Intel (IntelCards)
**Rationale:** Four content lenses converge on intel; what limits it at the game layer.
**Status:** Done
**Findings:**
- Intel cards are the game's own afterhour events (`ModHelper.AddAfterhourEvent`) registered per slot × `MaxPicks` at construction; `dailyChancePercent: 101` + `canStart` driving them. Daily roll cached in `ef_intel_day` against `StoreStation.dayCounter`; picks are stored as pool position + 1 → **a pool reorder or edit between game updates remaps saved picks** (positions, not ids, are persisted). Out-of-range yields null (safe), but a wrong pick can show.
- Intel is **area-scoped**: cards on one area's map card, drawn from that area's pools; no cross-area/global intel, no card that reads memory.
- Intel pick text = `pick` text var + `Render(Line)`; condition on card = none beyond non-null pick (a slot has `chance`, `counts`, pool, `good`).
- `intel(slot)` read in-run gives the qualified ids showing today (`Today()`), so memory-free forecast already works: Tonight Cards / Tide forecast / Rumor Broker are feasible *within* current intel; "third intel card" = add a slot (up to `MaxPicks` slots) — no cap in this layer (cap, if any, in PackLoader; sibling scope).
- Events sit on `AfterhourEvents.Start/End` → effect `ModHelper.AfterhourEffect(...)` can render text only; the card cannot mutate anything.

### Target: Pack discovery & lifecycle (PackRegistry, ModEntry)
**Rationale:** Cross-pack and cross-run behaviour depend on load/unload.
**Status:** Done
**Findings:**
- Packs discovered by file `ExpeditionFramework/main.json` in any enabled mod; all packs compile together on the first `OnModAssetsLoaded`; late-appearing packs warn "Restart the game". `Stop()` removes events and locations per pack and `DevTools.RemoveAll()`.
- `PackSet.HasMod` lets data check other mods at load time.
- Problem packs are skipped whole (area not added) — and their *saved counters stay in place*, so an uninstalled/broken pack keeps `ef_clears` orphaned; re-adding restores them (benign).
- Pack ids key every counter path through `area.Id` (`pack:area`), so **renaming an area id orphans clears/best depth** and breaks `unlocked_by`. No migration facility (version, rename map) exists.
- The framework itself is a pack (`ModEntry.ModId` first in load order), giving a place for a "core" memory schema.

### Target: Sibling precedent and adjacent test surface
**Rationale:** Avoid inventing what the workspace already proved.
**Status:** Done
**Findings:** ZohFramework `GameHost` wraps `ModData` as `Get/Set/Remove/Keys` for its *consumer* mod id, i.e. a framework persisting on behalf of a data pack — the same shape needed here. CharacterProbe `Visits` shows queued customer visits across days using ModData-held stage + `dayCounter`, i.e. a cross-night recurring character mechanism that could serve "stranger returns / recurring rival" outside the expedition UI. ProbablySecondHand is the heaviest user (JSON blobs, file store) and gives a template for versioned serialization. tests/Sim contains a fake `IBridge` (tests/Sim/Program.cs) — any new `IBridge` member must also be implemented there (sibling scope: tooling).

---

## Patterns & History

**Patterns Found:**
- **Counters on the game's own afterhour state:** safe and automatically saved, but ints-only and location-scoped. Framework chose the least risky persistence.
- **Read-only integration surface for data:** the only way a pack affects the world is via IBridge verbs wrapped as effects; the only way it learns the world is `game.*` (4 names) and run vars.
- **Silent defaults:** unknown `game.*` → 0; null item spawn → null; counters missing → 0; failure modes are quiet. Good for robustness, bad for authoring errors.
- **Commit at exit only:** `Leave()` is the single persistence point.
- **Positional persistence:** intel picks stored as pool index.

**Evolution:** Not examined by history (no git log read). Code comments suggest iterations driven by game quirks (e.g. the `100` rounding for `Random.Range`, the 10mm fallback).

---

## Findings

### Discoveries
1. **The persistence wall is real but narrower than feared:** the game already ships a per-mod KV store (`ModData`) and three workspace mods use it. Framework just has not wired it. (§ ModData target.)
2. **Data has no write path to anything persistent**, and a read path to 4 values only. Even the framework's own `ef_clears`/`ef_best_depth` are invisible to data; `ef_best_depth` is wholly unused. A "game.clears" read is nearly free.
3. **Commit-on-Leave only:** crashes/force-quits lose clears and (future) memory. Cheap fix with `OnAfterhourLeaveEarly` or write-through.
4. **Persisted intel picks are positional**, and area ids are the persistence key — authoring changes can silently orphan or remap saved state. No schema version/migration.
5. **Hooks are mostly untapped:** ~40 `ModHook` events exist; framework uses 5. The ones valuable for cross-run features: `OnNewGameStarted`/`OnGameLoaded*` (reset/migrate), `OnGoingSleepEarly` (end-of-day tick for decay), `OnGenerateCustomer*` + `SetClientSource` (recurring characters), `OnLootRolled`, `OnItemSold`.
6. **Everything given lands on the ground; wounds are one-way; trader identity is fixed.** These are the gaps content authors will hit next.
7. **Pure-buy trader is plausible at this layer** (empty `Stock`), UI tolerance unverified.
8. **The framework's own API assembly (`ExpeditionFramework.Api`) gives C# packs access, so persistence can arrive first as `IExpeditionRegistrar`-level service** (e.g. `registrar.Memory`) and later as data-level effects.

### Expansion Candidates (ranked: value ÷ cost; risk noted)

| # | Expansion | What | Cost | Risk | Unblocks |
|---|---|---|---|---|---|
| E1 | **`memory.*` store (persistent pack-scoped ints/strings)** | New `IBridge` members `GetMemory(key)`, `SetMemory(key,int)`, optional text; backed by `ModHelper.Get/SetModDataInt` (or one JSON blob) under the framework's mod id with keys `{pack}.{key}`; data: path `memory.NAME` read, effects `remember {key, value|add}`, `forget`, condition via the Value/Expr path; map-time readable (MapContext), so `hidden_when`, intel titles, `unlocked_by` can use it | ~120 LOC + loader paths (sibling: schema) + sim fake | Verify ModData is per-save (probe first); key collision across packs handled by prefix; stale keys of removed packs linger | Ledger, Tithe/standing, Veteran Debts, spared-enemy Hollow, rival, companions (the eval's whole "mercy/memory" convergence) |
| E2 | **More `game.*` reads** | `game.clears(area)`/`clears`, `best_depth`, `day` (`StoreStation.dayCounter`), `cash`, `trust` (if the game exposes), `store_level` | ~10 LOC each in `GameValues.Get`; area-arg needs expression support (`game.clears` can be a `Value` with `area` argument; else expose per-area names) | Low; `roll_luck`-style side-effect reads must stay explicit | Milestone content, "veteran" flavor without E1, UI showing best depth |
| E3 | **Write-through + safety commit** | Commit clears/best-depth/memory at event time and/or on `OnAfterhourLeaveEarly`; unify via `Leave`-like `Commit()` | ~20 LOC | Double-count clears (guard with a run-committed flag) | Crash-resistance for E1 |
| E4 | **Per-pack save schema version + id migration map** | A `memory._version`, `renamed: {old:new}` in `main.json`; copy counters on mismatch; persist intel by id not index | ~60 LOC | Moderate: touches loader | Safe content iteration (NER renames/reorders) |
| E5 | **Player mutation verbs** | `heal` (wound state down), `status` on game player, `give_item` to bag/hands (not only ground), item spawn with count | ~15-40 LOC each; needs game API read for wound reduce (`HealthData`) | Balance (heal erodes the framework's costs); wound state is vanilla-integrated | Medic/rest areas, bandage loops, Fence/commission reward delivery |
| E6 | **Recurring visitors via ModData + customer hooks** | Pack-declared `visitor` that queues a store client on a later day with a stored memory state (per CharacterProbe pattern) | ~150-250 LOC; touches `OnGenerateCustomer*`, `SetClientSource` | Highest: shared-store collisions with other client generators, art assets | "Stranger returns", rival, commissions outside the night |
| E7 | **Item facts & mutation** | Expose `value`, `rarity`, durable tags (`item.value`), set a tag/rename on owned item ("marked cache", "trophy engraving"), `consume` a specific stack member | ~60 LOC | Medium: item-model internals (`GameItem` tags) | `#trophy`, commissions, gear wear |
| E8 | **Trader options** | `trade` fields for faction, intent (`buy_only`/`sell_only`), sprite id, persistent cart per pack (opt-in via E1 key) | ~40 LOC for options; persistence needs serialization of stock ids | Medium (game UI assumptions) | Fence, standing-based traders, pure-buy |
| E9 | **Quit mid-run recovery** | Serialize `ExpeditionRun` vars+scene under ModData and offer resume | large | High (item handles, RNG, rooms) | Marginal; skip unless players complain |
| E10 | **Global (cross-area) intel and memory-reading intel cards** | Pack-level `intel` slots not bound to an area; intel conditions reading `memory.*` | ~50 LOC given E1 | Low-mid | Rumor Broker, forecast channel across areas |

**Ordering:** E1 (after the per-save probe) → E3 → E2 → E4. E5/E7/E8 are independent, per-need. E6 is a separate project. E9 skip.

### Mental Model
Three layers meet at `GameBridge`: *engine* (Core, pure, run-local vars) ↔ *bridge* (game objects, immediate effects) ↔ *save* (afterhour counters, plus unused ModData). Today only the bridge crosses runs, and only as the game's own economy (items, credits, wounds), with the framework's counters as a bolted-on clear tally. Anything that wants to remember a *story* has no home. ModData is the natural home: a per-save string KV already designed for mods, namespaced by mod id, and the framework's "pack" concept maps onto key prefixes. Everything else in this exploration is incremental widening of the same two channels (more reads via `game.*`, more writes via bridge verbs).

### Implications
- Cross-run content ideas can proceed data-only once E1 exists; no NER code needed. E1 should be specced (Plan) after a one-hour ModData probe.
- E2 gives immediate wins to NER (milestone unlocks, "best depth" HUD) and may be shippable independently.
- Id-keyed persistence (E4) should precede large NER content renames.
- Every new `IBridge` member also needs: Sim fake (tests/Sim), MapContext no-op, README/Api doc — plan these into the same change.

---

## Answers

**1. What survives a run today?** Game economy (items, credits, wounds, night_log text) plus framework-owned afterhour counters `ef_clears`, `ef_best_depth`, intel day/picks, committed in `ExpeditionLocation.Leave()`. Nothing pack-defined persists.

**2. What can data read/write?** Read: `game.luck|roll_luck|wounds|maimed`, `credits`, `ground`, carried items, `intel(slot)`. Write: `give_item|give_loot|credits|lose_items|lose_found|take_back|clear_ground|wound|night_log|trade|equip|consume`. No persistent write, no read of clears/day/trust/best depth.

**3. Untapped capabilities?** ModData KV (per-mod, Int/Bool/Float/string/file); ~35 unused ModHook events (Leave, Sleep, WakingUp, NewGame, GameLoaded, LootRolled, GenerateCustomer, TrustRefreshed); `StoreStation.dayCounter`; customer injection via `SetClientSource`; Harmony as fallback.

**4. Which expansions worth it?** E1 > E3 > E2 > E4 first; E5/E7/E8 as content demands; E6 as a separate project; skip E9. See table.

---

## Open Questions

- [ ] Is ModData per-save (vs global per mod)? Probe: set key, new game / other save, read. Blocks E1 design.
- [ ] Does ModData persist on abrupt exit, or only on game save? (Affects E3 value.)
- [ ] Does the negotiation UI tolerate empty front counter (pure-buy Fence)?
- [ ] Does the game expose store trust/level readable as ints (for E2)?
- [ ] Heal verb: which `HealthData` API lowers `woundState`?
- [ ] Are `AfterhourLocationState` counters saved only on game save or at once (crash-loss window)?
- [ ] Does `Arrive()` after mid-run reload ever see stale `run` state? (Inferred fresh; not played.)
- [ ] Namespace convention for `memory.*` vs `run.*` (eval conflict "run.* naming"): pack-prefix keys, framework-reserved `ef.` prefix?

---

## Appendix

**Sources:** src/Game/GameBridge.cs, ExpeditionLocation.cs, IntelCards.cs, TradeCounter.cs, ItemReader.cs, PackRegistry.cs, ModEntry.cs, DevTools.cs, BagMenuPatches.cs, HandsWindow.cs (hook lines); src/Core/Model.cs (IBridge, AreaDef), ExpeditionRun.cs (`Game`, `Finish`, `MapContext`, `IntelRoller`), Builtins.cs (effect registrations), Expr.cs (`game.` path); api/ExpeditionApi.cs; README.md; sibling mods ProbablySecondHand/src/BatchStore.cs, CharacterProbe/src/Visits.cs, ZohFramework/src/Game/GameHost.cs; `strings` over Probably Stolen_Data/Managed/Assembly-CSharp.dll for ModHelper/ModHook member names; NightExpeditionsReborn/.projex/2611120240-content-expansion-synthesis-eval.md.

**Limitations:** Base-game assembly inspected by symbol names only (not decompiled), so ModData save scope, ModHook argument shapes and the negotiation UI's empty-counter behavior are unverified. Nothing run or played in-game. Did not read Core/PackLoader.cs end-to-end (sibling scope), so claims about slot caps or `unlocked_by` being a single id rest on `AreaDef.UnlockedBy` being a `string` (Model.cs:290). The git history was not consulted.
