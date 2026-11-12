# Patch: pack-memory-game-reads

> **Status:** Complete
> **Author:** Claude Sonnet 5.5 (patch)
> **Parent:** 2611121550-pack-memory-game-reads-plan.md
> **Sprint:** 2611121459-pack-memory-sprint.md — iteration 3
> **Related Projex:** 2611121550-pack-memory-game-reads-plan.md | 2611121459-pack-memory-sprint.md
> **Directive:** Execute 2611121550-pack-memory-game-reads-plan.md (original prompt: "Sprint it")
> **Source Plan:** 2611121550-pack-memory-game-reads-plan.md
> **Result:** Success

---

## Summary

Packs can read `game.day`, `game.clears`, `game.clears.AREA` at run time and on the map. Code commit `4cb0e4d` (trailer `Projex: 2611121556-pack-memory-game-reads`). Plan followed as written; only line numbers drifted (HEAD `09f25ed`).

---

## Changes

**`src/Core/Model.cs`** (Modified) — doc comment on `IBridge.GameValue` listing all 7 game reads; new `internal static class GameReads.ClearsArea(name, areaId)`: `clears` → areaId, `clears.AREA` → `<pack>:AREA`, else null.

**`src/Game/GameBridge.cs`** (Modified) — `GameBridge` ctor takes `AreaDef` (sets `pack`, new `areaId`); `GameValue` → `GameValues.Get(name, areaId)`; `GameValues.Get` checks `GameReads.ClearsArea` first, adds `day` → `StoreStation.GetDayCounter()`; new `GameValues.Clears(areaId)` reads `PlayerStore.Instance.afterhourState` via `TryGetValue` (no `GetAfterhourState`, so no save entry created on read).

**`src/Game/ExpeditionLocation.cs`** (Modified) — map `game` lambda passes `area.Id`; `IsDiscovered` uses `GameValues.Clears(area.UnlockedBy) > 0`; `new GameBridge(content, area, …)`.

**`tests/Sim/Program.cs`** (Modified) — `TestGameReads`: name grammar, clean load of `hidden_when` using `game.clears.vault` + `memory.key`, host receives `clears.vault`, OR unlock by clear or memory.

---

## Verification

| ID | Result |
|----|--------|
| V1 `tests/Sim` | `All checks passed` PASS |
| V2 game-assembly build `-warnaserror` | 0 warnings, 0 errors PASS |
| V3 / V4 | `3` / `4` PASS |
| V5 | `0` then `1` (Leave's write) PASS |
| V6 lint --strict | sample_area 0/0; night_expeditions_reborn 0/0 PASS |
| V7 ef-sim run sample | `ef-sim sample_area: all checks passed` PASS |
| V8 NER 4000-run rows vs baseline | diff empty PASS |
| V9 / V10 | no output PASS |
| J1 / J2 | Clears returns 0 for no store / null dict / missing or null state; clears checked before switch; `day` side-effect free; comments present tense with rejected alternatives PASS |

**Status:** PASS. Not verified in-game (game layer compile-checked only). Worktree status clean after commit (bin/obj ignored).

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611121550-pack-memory-game-reads-plan.md | Source plan | Marked `[PATCHED]`, status Complete, moved to closed/ |

---

## Notes

- README for these reads deferred to iteration 5 (with iteration 2's carry-forward list); document "day 0 outside a loaded save" and "clears counts earlier nights, not tonight".
- Iteration 4 may lint `game.clears.AREA` against pack area ids.
- Sprint nav untouched (orchestrator-owned).
