# Sprint: Pack Memory Across Nights

> **Status:** In Progress
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

**As of 2026-11-12 (iteration 0):**

Nothing built. Persistence today: framework-owned afterhour counters only (`ef_clears`, `ef_best_depth`, intel picks); data reads `game.luck|roll_luck|wounds|maimed` only. See `2611120227-persistence-game-integration-expansion-explore.md` § E1/E2.

## Iterations

| # | Objective | Result | Artifacts |
|---|-----------|--------|-----------|

## Open Questions

- [ ] (none yet)

## Revision Log

| Date | Iter | Summary |
|------|------|---------|
| 2026-11-12 | — | Sprint started |
