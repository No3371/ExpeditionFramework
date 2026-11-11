# Authoring Tooling, Simulation & Testing — Expansion Exploration

> **Author:** Sonnet 5.5 (Explorer)
> **Type:** Domain
> **Status:** Complete (Done)
> **Parent:** Orchestrator
> **Related Projex:** 2611120227-persistence-game-integration-expansion-explore.md | 2611120227-runtime-hooks-expr-expansion-explore.md | 2611120229-ui-presentation-expansion-explore.md (sibling slices) | NightExpeditionsReborn `2611120240-content-expansion-synthesis-eval.md` § Cross-Cutting Costs (prior claims, re-verified below)

---

## Summary

How a pack author builds, validates, balances content today, and where tooling is thin. Verified by reading both sims + loader + game-side registry and running both sims (all pass; NER 2000 runs ≈ 35 s incl. build).

**Today:** two console sims (`tests/Sim` in framework; `NightExpeditionsReborn/tests/Sim`), compiled against `src/Core` + `api` with a `FakeBridge`. Loader reports problems as JSON-path strings; in-game they go to the mod log only; a pack with any problem adds no areas. Debug panel has two entries per pack: test kit, unlock area. No CI, no commits yet in the framework repo, no shared test library, no schema, no hot reload.

**Key findings (ranked by leverage):**
1. **Sim harness is duplicated, not shared.** Both Program.cs files re-implement text loading, `Loc`, `FakeBridge`, play loop, `expected`-id list. NER already diverged (richer bridge, 3 policies, wounds table). A pack-agnostic `Sim.Core` library + per-pack `sim.json` manifest would let any pack get validation + coverage + bots free.
2. **Coverage is by option-id only, hand-maintained.** NER `expected` = 59 ids typed by hand; nothing reports which events (18), encounters (16), rooms, enemies (22), loot tables (26), loot entries, or choices were actually reached. Content can be unreachable/dead without failing.
3. **Bots are scripted by hard-coded option ids** (`Plan()` in NER: `"smoke_run","flee","read_boss",...`). New content falls into the fallthrough `options.First(id != "counter")`/random — confirms the prior "bots don't play signature rules" claim, and the cause is architectural: policies are code, not data. Generic bots (greedy-by-tag, "pick the option that changes most state", per-scene priority tables from data) would scale.
4. **Validation gaps:** (a) game item ids never checked — `GameBridge.GiveItem` returns null on unknown id, silently; typo = nothing happens; (b) text parity en↔zh is unchecked (sim reads en only; currently clean: 750 keys each, 0 placeholder mismatches — by luck); (c) orphan keys (in CSV, never referenced) unreported — e.g. `ne_event_peddler_buy/haggle/*`, `ne_bribe_poor`, `ne_status_line` left after the README's "unreachable peddler branches are gone"; (d) no unused/unreachable definition lint; (e) text keys checked in sim only, not at game load (`GetLocalized` on a missing key at runtime is game-defined).
5. **Error messages are json-paths only** (`rooms.a.enter[0]: unknown effect "nope"`); syntax errors alone carry a line. No did-you-mean; unknown-effect message does not list known effects (unknown-hook message does — inconsistent).
6. **Sim fidelity holes that bias balance:** `OpenTrade`→true, `CloseTrade`→random cash loss, `RollTable` returns the table name (loot never resolves to items/stats), luck fixed 50%, `LoseCarried`→`["something"]`. `smg-nomed` ≈ `smg` (91.3 vs 92.0 careful clear) strongly suggests a **0-count bandage still offered** — `Select()` filters by tag only, and the sim's `Add("bandage_item", 0, ...)` leaves a Count-0 item in the bag; the "no medicine" scenario is likely not testing what it says. Unverified by experiment (read-only).
7. **Debug panel is minimal:** kit + unlock only. No start-at-room/event, set var, force dice, spawn encounter, dump run state/log, reload pack. Framework README: in-game side "has not been played".

---

**Guiding Questions:**
1. How does an author build/validate/balance a pack today, end to end?
2. What do the sims cover, and where are they blind (incl. the prior eval's claims)?
3. Which tooling expansions would be useful, and what do they cost/depend on?

**Scope:** `tests/`, `samples/`, `api/`, `src/Game/DevTools.cs`, NER's sim, loader problem reporting, pack validation. Out: schema design (loader sibling), runtime hooks, persistence, UI. No code written; sims run read-only.

---

## Context

**Why Now:** Content-expansion imagination round (NER) proposes many new areas/enemies/NPCs; the synthesis names sim blind spots and localization cost as cross-cutting. Tooling is the multiplier for all five content lenses.

**Current State:** Sims pass. Game never played in-game. Framework repo has zero commits (`git status`: everything untracked; `git log` → no commits).

**Stakeholders:** pack authors (NER author first), framework maintainer, future third-party pack authors.

---

## Investigation Targets

### Target: Framework sim (`tests/Sim/Program.cs`, 492 lines)
**Rationale:** the framework's own regression suite and the template for pack sims.
**Status:** Done
**Findings:**
- Compiles `Program.cs` + `src/Core/**` + `api/*.cs` only (Sim.csproj) — no game types; hence `IBridge` fake.
- Stages: load framework + `samples/SampleArea` → `content.Problems` → text-key check (`content.TextKeys` vs CSV) → unit tests: expressions (7 checks), bad-content reporting (6 cases incl. line in broken JSON), optional data / `mod()` / bad includes, patches (order independence, guarded drop, patcher blame), C# mod effect parity, public API (`IRun`, decisions `damage/status/heal/check/chance/loot/intent`, hooks, scheduling, rosters, re-entrancy guard, throwing listener), `MapContext` → weak-point options → `Play` (random, 5000 runs, `SIM_RUNS`).
- Assertions are hand-rolled `Check(bool,msg)` + a failure counter; exit code 0/1. No test runner, no filtering, no per-test timing; a failing check in the middle can't be run alone.
- `Play` expects 16 hand-listed option ids ("never offered and picked: ..."), random policy only, `head_home` suppressed to 3%.
- Seed fixed (`Random(42)`), so runs are reproducible; failures print message but no replayable trace (no seed+step log).
- Result at 500 runs: `knocked_out=374, cleared=108, left=18; longest 26`. Passes.

### Target: NER sim (`NightExpeditionsReborn/tests/Sim/Program.cs`, 364 lines)
**Rationale:** the only real pack-level balance tool; the prior eval's "sim blind spots" claim lives here.
**Status:** Done
**Findings:**
- Same skeleton (copy, not reference — csproj compiles `..\..\..\ExpeditionFramework\src\Core\**` directly; repo-relative path coupling).
- Checks: loader problems → text keys → `CheckPacking` (6 asserts on `map.Missing`) → `CheckGunsmithLoot` (rolls 6 tables ×2000 with/without `HasMod`) → `Play` matrix: 3 areas × random; then careful & greedy × ~10 (area,kit) combos (handmade/smg/shotgun/knife/no-meds) → `expected` 59 option ids → missing-text and unfilled-label (`{`) detection.
- Output: per-row cleared/KO/extracted %, credits, items, depth, longest; "wounds" table. README balance table is **hand-transcribed** from this output (verified current output matches its ballpark, e.g. Sump SMG careful clear 92% vs README 93.5).
- Policies: `Wander` (random, `head_home` 5%), `Plan(...)` priority code with scene branches `combat/standoff/event/room`. Event scene list `Find("spare","search") ?? ... ?? Find("card","scan","light","cut","pry","food",...)` — every non-combat interaction must be named here to be exercised in a purposeful order; otherwise the fallthrough is `options.First(id != "counter")`.
- Bridge fakes: `OpenTrade`→true; `CloseTrade`→`CashChange = -rand(0,30)` (peddler economy never exercised beyond a random debit); `RollTable(table)`→table id (no loot resolution); `GameValue("luck")`=50, `roll_luck`=coin flip; `LoseCarried`→`["something"]`; `TakeCart`→`["cart_goods"]`. `NightLog` throws on unfilled `{`.
- Seed = `areaId.Length*31 + policy.Length` — not unique per (area,policy,kit); every kit in the same area/policy shares one seed (fine, but kits aren't independently sampled).
- **Probable sim bug:** `Smg(2, 0)` "smg-nomed" adds `bandage_item` with `Count = 0`; `Select()` (ExpeditionRun.cs ~line 833) filters via `use.Filter` with no count check and `Consume` only removes when `Count <= 0` *after* subtracting. Results: smg careful clear 92.0%/KO 2.0% vs smg-nomed 91.3%/1.7% (greedy 95.7 vs 95.0). Indistinguishable → either meds don't matter on the Sump with SMG (possible) or the heal option still fires on a 0-count stack. Worth an experiment before trusting any "no medicine" conclusion.
- Ran 300 and 2000 runs: all pass; ~35 s wall incl. `dotnet` build for 2000.

### Target: Loader validation & error reporting (`PackLoader.cs`, `PackSet.cs`, `Json.cs`)
**Rationale:** the first line of defense for authors; defines what "validated" means.
**Status:** Done
**Findings:**
- ~90 `Problem(at, msg)` sites. Messages are `pack/file.json.section.id.field[i]: msg`. Deferred reference checks (`Defer(...)` → `Exists(kind,id)`) catch dangling ids across packs; templates/behaviors cycles detected; unknown sections/keys/hooks/effects/conditions/filters detected; `values` self-dependency; pool/`tag` empties; `texts` must be keys; `{path}` fill-ins resolved at load (`can't fill in {...}`); expression compile errors surfaced with message.
- Only JSON *syntax* errors carry a line (`Json.cs` `line N:`); semantic problems have no file line or column. The parser tolerates `//` comments (NER files start with one), so standard JSON tools/schemas choke (my python `json.load` fails on the sample file) — editor tooling must handle JSONC.
- Hook-unknown message lists known hooks; effect/condition/filter-unknown messages do not. No did-you-mean anywhere.
- `PackSet.Compile` tries each patcher's patches in order, re-compiling the whole world per attempt (O(patchers × world)); rejects patchers that add problems; blame attribution to the patcher. Good design; cost grows with pack count, fine today.
- Missing categories (no code found): unused definitions (enemy never in an encounter/pool, event never in a pool, loot table never referenced, intent never in a behavior), unreachable rooms/choices, unused values/statuses/templates, vars read but never set (`run.*` typos — expression paths like `run.act` presumably evaluate to 0; the prior synthesis flagged `run.*` namespace chaos across five authors), item ids vs game, duplicate text across keys, CSV key parity across locales.
- Text-key check only exists in the sims. `TextKeys` is populated by the loader (with `variants` expansion `key_0..n`), so an orphan-key report is cheap: CSV keys − `content.TextKeys`. My crude grep suggests >300 CSV keys not literally in NER json, but many are derived (variants, per-enemy conventions), so the figure is not trustworthy; a loader-driven report would be. Confirmed true orphans (stated removed branches): peddler buy/haggle keys, `ne_bribe_poor`, `ne_status_line`.

### Target: Debug panel & in-game authoring loop (`DevTools.cs`, `PackRegistry.cs`, `GameBridge.cs`)
**Rationale:** only in-game test aid; defines iteration speed.
**Status:** Done
**Findings:**
- `DevTools.Add`: per pack, `Expeditions: give test kit (pack)` from `debug_kit` (spawns via `ModHelper.SpawnItem` → `MoveItemToInventory`), and `Expeditions: unlock <area>` for areas with `unlocked_by` (sets prerequisite area clears counters to 1). Registered through `ModHelper.AddDebugFunction` in category `DEBUG_CATEGORY_MODDING`; removed on disable.
- Packs load **once** (`PackRegistry.LoadAll` on first `OnModAssetsLoaded`; later arrivals only log "Restart the game"). Every JSON/CSV tweak therefore needs a game restart — the dominant authoring-loop cost for in-game checks. Problems go to `ModEntry.Log.Warning`; a broken pack silently adds no areas (visible only in the log).
- `GameBridge.GiveItem` → `ItemSpawner.Spawn(id)`; null result → nothing given, nothing logged. `RollTable` → `LootRegistry.HasGroup/RollGroup/Roll`. So game-side item/loot ids are validated only by playing.
- No debug hooks for: start in a given room/event, force a decision/dice, set `run.*` vars, dump run state or log, jump to boss, grant credits, replay seed, hot-reload.
- Framework README: in-game side "builds and passes the mod scanner, but has not been played" — all tooling findings sit under that uncertainty.

### Target: Samples and API (`samples/SampleArea`, `api/ExpeditionApi.cs`)
**Rationale:** onboarding path and C# testing surface.
**Status:** Done
**Findings:**
- `samples/SampleArea` (242-line main.json + 37-key CSV; 0 orphan keys by crude check) exercises weak points, `give/rob/ignore/pry/search_pump/claim/feed_rat`, flooded cellar. It is the framework's only fixture; every new engine feature needs sample coverage to be tested, but the sample is a single area — no fixtures for trading/carts, standoff scene, patches (patch tests use inline JSON strings), mod-gated includes (tested via the framework's own `gunsmith.json`).
- API (`IExpeditionRegistrar`, `IRun`, `IScope`, decisions) is testable headlessly — `TestPublicApi` proves it. The roll delegate passed to `ExpeditionRun` (`n => 1` or `random.Next`) allows forced-dice boundary tests (all-1s / all-max) — unused by any current check.
- README "Building and testing" documents one command; no authoring workflow doc (add file → run sim → read problems).

### Target: Prior-claim re-verification (synthesis § Cross-Cutting Costs)
**Rationale:** instructed to treat as untested.
**Status:** Done
**Findings:**
- "Sim fails on missing keys" — true (en only).
- "~10 keys/enemy, en + zh" — NER has en.csv and zh.csv, 750 keys each, 0 placeholder mismatches, 8 identical lines. But **the sim never loads zh.csv**: parity is unenforced, and adding content doubles the manual burden with no safety net.
- "Bots don't play signature rules, NPC interactions, or parley" — true and structural (id-coded policies; trade and loot faked). Win-rate tables for new content will mislead exactly as claimed.
- "Balance table in README needs re-run" — true; table is hand-copied from console output, no script produces it or diffs against the original-game baseline.
- "Never played in-game" — true (framework README).

---

## Patterns & History

**Patterns Found:**
- **Copy-adapt over share:** NER sim began as a fork of the original sim; each pack copies a ~400-line Program.cs.
- **Console-only, human-read output:** every result is a printed table; nothing machine-diffable, no golden files, no thresholds that fail the build (except hand-picked "never offered" ids).
- **Stringly option ids as the test vocabulary:** correct but costly — coverage, bot policy, and `expected` all key on interaction ids.
- **Validation lives at compile (loader) and sim, not at the game boundary:** items/loot ids and localization are the unchecked seam.
- **Whole-world recompile for patch trials:** simple and correct, linear in patchers.

**Evolution:** loader was hardened with patch blame, optional-data includes (`mod()`), bad-include reporting — each with a sim test. Tooling after the loader (coverage, lint, debug) has not had the same attention. (No git history available: repo has no commits.)

---

## Findings

### Discoveries
1. **No shared sim library.** Duplication → every pack author pays ~400 lines; fixes (like a bridge fidelity fix) don't propagate.
2. **No content coverage beyond option ids.** Dead/unreachable content passes silently.
3. **Bots are code, not data** → blind to new content (confirmed cause of prior claim).
4. **Unchecked seams:** game item ids, zh parity, orphan keys, unused definitions, `run.*` var typos.
5. **Diagnostics are paths without lines; no suggestions.**
6. **Sim fidelity gaps (trade, loot resolution, luck, 0-count items) can invert conclusions; "no medicine" scenario is suspect.**
7. **In-game authoring loop = restart per edit; debug panel is two buttons.**
8. **Balance table is manual.** No regression guard; "parity vs original" (a stated house value in the NER synthesis) has no automation.

### Mental Model
Author edits JSON/CSV → (optional) sim compiles Core + pack, loader checks structure and references, sim checks text keys, bots play → human reads tables → restart game, debug-panel kit, play. The expensive/uncertain links are (a) deciding whether bots played the new thing, (b) anything involving real game items/loot/trade/luck, (c) the restart loop. Tooling expansions fall into four layers: **static lint** (cheap, loader-adjacent) → **headless coverage + bots** (medium) → **balance regression** (medium, needs fidelity) → **in-game aids** (needs game-side C#, unverifiable without playing).

### Implications
Static lint and shared sim library are independent of the game and give immediate multiplier for the 5 content lenses; bot generalization is what keeps win-rate claims honest. In-game aids and item-catalog validation need game-side work and coordination with the persistence/integration sibling.

---

## Candidate Expansions (descriptive, not prescriptive; ordered by value/cost)

| # | Expansion | What it does | Needs | Cost | Dependencies/risks |
|---|---|---|---|---|---|
| A | **Lint pass in `Content`** | unused definitions, unreachable events/rooms/choices, `run.*` vars read-never-written, orphan text keys (CSV − `TextKeys`), en↔zh key + placeholder parity, hook-unknown-style "known: ..." lists for effects/conditions/filters, did-you-mean | loader/Core only; CSV readers in sim | S-M | Warnings vs problems: must not fail the "any problem → no areas" rule; add a separate `Warnings` list |
| B | **Shared `Sim.Core` + pack manifest** | extract text loading, `Loc`, run loop, coverage, `FakeBridge` (configurable kits) into a library project; pack declares `tests/sim.json` (areas, kits, policies, expected-ids, locales) | new csproj; refactor two Program.cs | M | Both sims still use direct source includes; pick project-reference vs source-include |
| C | **Coverage report** | count visits per event/choice/encounter/enemy/loot entry/room/interaction/status/intent; print never-seen; fail under threshold; seed-sweep | run hooks or a `RunObserver` in Core | M | Needs a tiny observer interface in `ExpeditionRun`; overlaps runtime sibling |
| D | **Data-driven bot policies** | policy = per-scene ordered `prefer`/`avoid` id/tag lists + HP-threshold rules in JSON; plus generic bots: *explorer* (maximize distinct options), *state-delta greedy*, *fuzz* (uniform), *completionist* (never leaves) | policy loader in sim | M | Fixes "bots don't play signature rules" without editing C# per area |
| E | **Failure replay** | on any failing run print seed + choice list; `SIM_REPLAY=seed:steps` re-runs and dumps the log/HUD each step | sim only | S | Needs RNG per run seeded deterministically (currently one shared `Random`) |
| F | **Higher-fidelity `FakeBridge`** | resolve loot tables to real items via `item_stats`; configurable luck (0/15/50%); realistic trade (cart pricing, haggle outcomes); carry-loss model; 0-count item hygiene | bridge only | S-M | First fix/verify the 0-count bandage question |
| G | **Balance regression** | sim emits JSON; `balance.json` golden + tolerance; script regenerates README table and flags drift vs the original-game baseline row-by-row | script + golden | M | Statistical tolerance (binomial) to avoid flaky builds |
| H | **Item/loot id validation via catalog export** | debug function dumps all spawnable item ids/tags (and loot group ids) from the game to a JSON file; sim/lint checks pack ids against it offline | game-side C# (`ModHelper`/`ItemSpawner` enumeration) | M | Requires a real game session; unverified API; ties to integration sibling |
| I | **In-game debug expansion** | start-at-room/event, set var, force dice sequence, dump run state/log, jump to boss, reload packs (hot reload) | `DevTools`, `PackRegistry` (re-`Compile` + re-add locations), run state setters | M-L | Hot reload must unregister/re-add `AddAfterhourLocation` safely; interacts with persistence sibling; unverifiable without playing |
| J | **Diagnostics upgrade** | line/column for semantic problems (needs `Node` source spans), did-you-mean, severity levels, one-line summary per pack, optional in-game toast/log panel on pack failure | `Json.cs` spans, UI | M | Span plumbing touches every `Problem(at,…)` call site indirectly (path→span map) |
| K | **JSON Schema + editor support** | generated schema for sections/keys (effects/conditions are registry-driven), VS Code `.jsonc` mapping | schema generator from loader tables | M | Registry-extensible effects need dynamic schema; coordinate with schema sibling |
| L | **CI/automation** | `dotnet run` both sims on commit; fail on lint/coverage/balance; framework repo has no commits yet | git + workflow | S | Needs first commit (user's call) |
| M | **Authoring guide + `pack new` scaffold** | README workflow; template pack folder with CSV/manifest/DLL stub (README notes the empty-DLL requirement) | docs | S | — |

---

## Answers

**1. How does an author build/validate/balance today?**
Write JSON + en/zh CSV → run `dotnet run -c Release --project tests/Sim` (real loader, problems, missing text, bots) → read console tables → hand-copy into the README balance table → copy into the game folder and restart → use debug-panel kit/unlock. Evidence: both Program.cs files, README § Building and testing, `DevTools.cs`, `PackRegistry.cs`.

**2. What do the sims cover / where blind?**
Cover: structure, dangling references, expressions, patches, API/decisions, text-key presence, option-id reachability, win/KO/clear rates for scripted kits, wound rates. Blind: any content not named in policies, trade/economy, loot resolution, luck realism, zh parity, orphan keys, dead definitions, game item ids, the 0-count "no medicine" scenario (suspect), UI/Hud beyond string render, persistence. The prior eval's sim-blind-spot, zh-cost and unplayed claims all hold.

**3. Which expansions are useful?**
Highest leverage per cost: A (lint) + B (shared sim) + D (data-driven bots) + C (coverage) — headless, game-independent, directly serve the five content lenses. F/G keep balance honest. H/I/J/K need game-side or schema coordination.

---

## Open Questions

- [ ] Does a Count-0 `bandage_item` still offer `heal` in NER? (Experiment: run `smg-nomed` and count `heal` options, or filter `item.count > 0`.) If yes, README balance rows for no-meds conclusions are void, and a framework-level "empty stack" rule may be wanted (loader sibling).
- [ ] What does `ModHelper.GetLocalized` return/log for a missing key in the real game? (determines whether runtime key check is needed)
- [ ] Can the game enumerate spawnable item ids (for H)? Needs in-game API discovery.
- [ ] Is hot reload feasible (`RemoveAfterhourLocations`/`AddAfterhourLocation` mid-session)? Persistence/integration sibling territory.
- [ ] Should lint output be warnings only, preserving "any problem = no areas"?
- [ ] Should sim live in the framework repo as a reusable tool packs reference by path, or be a published NuGet-style project? (NER currently reaches into the framework folder by relative path.)
- [ ] Do other packs besides NER exist or are planned (changes value of B)?

---

## Appendix

**Sources:** `tests/Sim/Program.cs`; `NightExpeditionsReborn/tests/Sim/Program.cs`; `src/Core/PackLoader.cs`, `PackSet.cs`, `Json.cs`, `Model.cs` (`Content`), `ExpeditionRun.cs` (`Options`, `Select`, `AddOption`); `src/Game/DevTools.cs`, `PackRegistry.cs`, `GameBridge.cs`, `ModEntry.cs`; `api/ExpeditionApi.cs`; `samples/SampleArea`; both READMEs; NER Localization en/zh csv; synthesis eval. Executed: framework sim (500 runs, pass), NER sim (300 and 2000 runs, pass).

**Limitations:** read-only; the 0-count-bandage hypothesis is inferred, not run. Orphan-key counts from crude grep are unreliable (derived keys). Game-side API behavior (GetLocalized, ItemSpawner, debug functions) not verifiable without the game. Repo has no git history. Sibling slices (schema, runtime, persistence, UI) not read.
