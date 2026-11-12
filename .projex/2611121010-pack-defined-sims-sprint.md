# Sprint: Pack-Defined Sims

> **Status:** In Progress
> **Goal:** ExpeditionFramework ships `ef-sim`: one executable that lints and simulates any pack from data the pack carries (`sim.json`: kits, rule-based bots, run matrix, expectations). NightExpeditionsReborn's current C# sim is expressed as such data, and `ef-sim` on NER reproduces every check the C# sim makes plus its 4000-run balance table within ±3 points per cell. Framework sim tests, lint, and the game-assembly build stay green.
> **Base Branch:** main
> **Step:** standard
> **Strategy:** plan<opus>, patch<sonnet>!, audit<sonnet>, [patch<sonnet>] — derive on opus
> **Sprint Branch:** projex/sprint/2611121010-pack-defined-sims
> **Worktree Path:** ExpeditionFramework/.projexwt/2611121010-pack-defined-sims
> **Parent:** 2611120250-framework-expansion-synthesis-eval.md
> **Started:** 2026-11-12 10:10

## Vision

Replaces synthesis Phase 2 "shared Sim.Core" (2611120250-framework-expansion-synthesis-eval.md § Recommended Sequence) with a pack-agnostic sim tool. Human-sanctioned direction (conversation 2026-11-12):

- Framework ships the executable; packs ship data only. One `FakeBridge` in framework; bridge changes touch one place.
- Pack data covers: kits (item ids/tags + gun stats) | bot policies as ordered rules in the framework expression language | area × kit × bot × runs matrix | expectations (options that must be offered, packing cases, balance ranges as regression guards).
- Built-in checks for every pack: load problems | missing text keys | runs that never end | never-offered options.
- Optional C# hook kept for checks data can't express (e.g. NER `CheckGunsmithLoot`: load with/without another mod and compare loot rolls). No pack is required to write C#.
- Self-contained publish so authors without the .NET SDK can run it: desirable, not part of the verifiable goal.

**Done =** all of:
1. `ef-sim` (name may differ) builds from the framework repo; `lint <folder>` still works through it.
2. NER `sim.json` exists; `ef-sim` on NER exits 0 and runs every check the NER C# sim (`NightExpeditionsReborn/tests/Sim/Program.cs`) makes today — text keys, never-offered, packing, Gunsmith loot, run-never-ended guard — by data or via the C# hook.
3. 4000-run balance table (cleared / knocked out per area × kit × policy row) from `ef-sim` matches the NER C# sim's within ±3 percentage points per cell.
4. Framework sim tests pass; lint on NER + sample pack 0 errors/0 warnings; game assembly builds 0 warnings 0 errors.

Retiring NER's C# sim (deleting `tests/Sim`) is the human's call after the sprint, not part of the goal.

## Current Position

**As of 2026-11-12 (iteration 0):**

Not started. Framework `main` at `cdc322f`. Two sims exist: framework `tests/Sim` (tests + `lint` subcommand) and NER `tests/Sim` (364-line C#, own `FakeBridge`, careful/greedy/random bots).

## Iterations

| # | Objective | Result | Artifacts |
|---|-----------|--------|-----------|

## Open Questions

- [ ] (none yet)

## Revision Log

| Date | Iter | Summary |
|------|------|---------|
| 2026-11-12 | — | Sprint started |
