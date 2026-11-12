# Sprint: Pack Memory Across Nights

> **Status:** Complete (Goal Reached)
> **Goal:** ExpeditionFramework packs remember values across nights from data alone: `memory.NAME` (int) and `memory.$NAME` (text) read and write wherever `run.*` does (expressions, `set`/`add`/text effects), and read on the map (intel and area-unlock conditions); values live in the player's save through the game's mod data (`ModHelper.*ModData*`), namespaced per pack; `game.day` and `game.clears` are readable. `ef-sim` models memory (sim.json rows can seed it and carry it across consecutive runs, with an expectation that checks it), lint warns on memory read but never written, and the sample pack demonstrates a carried counter that `ef-sim` checks. README documents it. Framework sim tests, `ef-sim run` on NER and the sample, lint `--strict` on both, and the game-assembly build stay green; NER's 4000-run balance table is unchanged.
> **Base Branch:** main
> **Step:** standard
> **Strategy:** plan<opus>, patch<sonnet>!, audit<sonnet>, [patch<sonnet>] — derive<opus>
> **Sprint Branch:** projex/sprint/2611121459-pack-memory
> **Worktree Path:** ExpeditionFramework/.projexwt/2611121459-pack-memory
> **Parent:** User
> **Started:** 2026-11-12 14:59

## Vision

Data packs gain a persistent, per-save, per-pack store so content ideas that span nights (debts, standing, rival, companions, the Hollow, lore) can be written in JSON with no pack C#.

Done looks like:
- `memory.*` is a variable head beside `run.*` in the expression and effect layer; ints and `$text` both work; writes go straight to the game's per-save mod data under the framework's mod id, keyed per pack, so packs cannot clobber each other.
- Memory reads work outside a run (map-time conditions: intel, area unlock).
- `game.day` and `game.clears` are readable from data.
- C# packs reach the same store through the public API (if the plan judges it cheap; not a goal criterion on its own).
- `ef-sim` keeps memory in an in-memory store; `sim.json` can seed memory per row, carry it across a row's runs, and assert on it; lint flags memory read but never written.
- Sample pack shows a counter carried across runs, checked by `ef-sim`.
- README documents memory, the new `game.*` reads, and the sim.json additions.

Verified by: framework `tests/Sim` passing; `ef-sim run` on `samples/SampleArea` and `../NightExpeditionsReborn` passing; `ef-sim lint --strict` 0/0 on both; game-assembly build 0 warnings / 0 errors; NER 4000-run table identical to before the sprint.

Established facts (from pre-sprint review, re-verify as needed):
- `ModHelper.SetModData/GetModData` store into `PlayerStore.Instance.ModData()` — the player save — keyed `modId + ":" + key`; it warns and no-ops when no save is loaded. `GetModDataFolder` is a global on-disk folder (not per save) and is out of scope.
- Variable heads live in `Expr.Reader` / `Writer` / `TextWriter` / `TextReader` (`src/Core/Expr.cs`).
- Per-enemy dynamic key names (`memory.spared_{foe}`) are out of scope for v1; fixed names only.
- Save-schema versioning / id migration is out of scope.

## Current Position
**As of 2026-11-12 (iteration 6, derive): goal reached.**
Iterations 1-5 all succeeded (audits `2611121522-…` through `2611121650-…` Verified/Accept). Derive 6 re-checked every goal clause independently against the sprint branch (`a3f2927`, clean tree):
- `memory.NAME` / `memory.$NAME` in expressions, `set`/`add`/`set_text` (`tests/Sim/Program.cs` ~517-545 cover text read/write and `set` on `memory.$x` rejected); map reads via intel slot `when` (`Schema.cs`) and `hidden_when`; storage `ModHelper.Get/SetModData[Int]` under the framework mod id with key `memory:{pack}:{name}` (`src/Game/GameBridge.cs` 236-241); `game.day` (`StoreStation.GetDayCounter`) and `game.clears` readable.
- ef-sim `runs[n].memory` start/carry/expect (`tools/EfSim/SimConfig.cs`); `PackLoader` read-never-written warning (`PackLoader.cs` 579).
- Sample: `memory.foreman_beaten` added on Foreman defeat and read in a later `say` (`samples/SampleArea/ExpeditionFramework/main.json` 116-121, 210-211); `sim.json` row 46 seeds 1, carries, expects `min 2`.
- README: lint note (25), `hidden_when` (108), `game.day`/`game.clears` (181-183), "Memory across nights" (226-259), C# notes (331-337), ef-sim "Memory" (453-470).
- Gates re-run by derive 6: `tests/Sim` All checks passed (outcomes unchanged: knocked_out=3742, cleared=1049, left=209); `ef-sim run` sample and NER all checks passed; NER `--runs 4000` output identical line-for-line to `ner-baseline-4000.txt`; `lint --strict` 0/0 on sample and NER; game-assembly `--no-incremental` build 0 warnings / 0 errors.
Remaining: none toward the goal. Left out on purpose (not goal criteria): rejecting unreadable `start`/`expect` names (`a b`, `$$x`); C# API memory calls (vision marks it optional). Nothing could be checked in-game.
## Iterations

| # | Objective | Result | Artifacts |
|---|-----------|--------|-----------|
| 1 | `memory.NAME`/`memory.$NAME` run-time heads + per-pack save-backed store | Success | 2611121506-pack-memory-runtime-store-plan.md · 2611121516-pack-memory-runtime-store-patch.md · 2611121522-pack-memory-runtime-store-audit.md |
| 2 | Map-time memory reads (`MapContext` store, intel slot `when`; memory-aware `hidden_when` as area unlock) | Success | 2611121531-pack-memory-map-reads-plan.md · 2611121537-pack-memory-map-reads-patch.md · 2611121540-pack-memory-map-reads-audit.md · 2611121543-pack-memory-roll-guards-patch.md |
| 3 | `game.day`, `game.clears`, `game.clears.AREA` readable in runs and on the map (no save writes on read) | Success | 2611121550-pack-memory-game-reads-plan.md · 2611121556-pack-memory-game-reads-patch.md · 2611121559-pack-memory-game-reads-audit.md |
| 4 | ef-sim memory (`runs[n].memory` start/carry/expect) + loader warning on memory read but never written | Success | 2611121613-pack-memory-sim-memory-plan.md · 2611121622-pack-memory-sim-memory-patch.md · 2611121626-pack-memory-sim-memory-audit.md |
| 5 | Sample carried Foreman counter checked by ef-sim; README documents memory, map reads, `game.*`, sim.json memory; full gate set | Success | 2611121637-pack-memory-sample-readme-plan.md · 2611121641-pack-memory-sample-readme-patch.md · 2611121650-pack-memory-sample-readme-audit.md |
| 6 | — (derive: goal reached) | Nothing | — |

## Closing Assessment

Goal reached in 5 work iterations (all Success, 1 fix round), 0 failures. Orchestrator re-checked the NER 4000-run table: 23 `ne_` rows identical to the pre-sprint baseline.

Delivered: `memory.NAME`/`memory.$NAME` (run + map), per-pack keys in the player save via mod data; intel slot `when`; memory-aware `hidden_when` as area unlock (no new area field, D1 of iteration 2); `game.day`, `game.clears`, `game.clears.AREA`; ef-sim `runs[n].memory` start/carry/expect; loader warning on memory read but never written; sample Foreman counter; README.

Left out (not goal criteria): C# API memory calls; rejecting unreadable sim.json memory names (`a b`, `$$x`); bot `when` cannot read memory (README says so); dynamic per-enemy names; save-schema migration. Unverified: anything in-game, incl. when mod data reaches disk.

## Open Questions

- [ ] (none yet)

## Revision Log

| Date | Iter | Summary |
|------|------|---------|
| 2026-11-12 | — | Sprint started |
| 2026-11-12 | 1 | Derive: position verified against code (no memory head, pack-less Expr path entry points, no store seam on `IBridge`, no area-unlock condition exists). Objective: `memory.*` int/text in run-time expressions and effects, per-pack, behind a host store (game: ModHelper mod data; ef-sim/tests: in-memory), proven by framework sim tests. |
| 2026-11-12 | 2 | Derive: iteration 1 verified in code (store, heads, `SaveMemory`, `TestMemory`); map time still has no store (`MapContext` not an `IMemoryHost`), intel slots have no condition field, area unlock is still a fixed `unlocked_by`. Objective: map-time memory reads — `MapContext` backed by the host store, an intel-slot condition and an area unlock condition that can read `memory.*`, proven by framework sim tests. `game.day`/`game.clears` split out to their own iteration. |
| 2026-11-12 | 3 | Derive: iterations 1-2 verified in code (run-time and map-time memory, intel `when`, memory-aware `hidden_when`). `GameValues.Get` still lacks `day`/`clears`; ef-sim memory, lint warning, sample, README untouched. Objective: `game.day` and `game.clears` readable from data in run and map time, defined and documented in code, game-assembly build green. |
| 2026-11-12 | 4 | Derive: iterations 1-3 verified in code (store, map reads, `game.day`/`game.clears`). ef-sim still discards memory per run and `sim.json` has no memory keys; no memory lint warning. Objective: ef-sim memory model (per-row seed, carry across a row's consecutive runs, expectation on final memory) + lint warning for memory read but never written, proven by framework/sim checks with NER output unchanged. Sample + README + full gates left for iteration 5. |
| 2026-11-12 | 5 | Derive: iterations 1-4 verified in code (iteration 4 audit Accept, gates green). Sample pack has no memory; README documents none of the sprint's features. Objective: sample pack carried counter checked by `ef-sim` (`runs[n].memory` carry + expect), README for memory / map reads / `game.*` / sim.json memory / lint warning, and the full gate set incl. NER 4000-run table unchanged. Expected to be the final iteration. |
| 2026-11-12 | 6 | Derive: goal reached. Every goal clause re-verified in code, sample, and README; all gates re-run green (framework sim, ef-sim run sample + NER, lint --strict 0/0 on both, game build 0/0, NER 4000-run output identical to baseline). No objective emitted. |
| 2026-11-12 | — | Finalize: closing assessment; Status Complete (Goal Reached); squash-closed into `main` |
