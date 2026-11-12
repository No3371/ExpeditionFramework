# Patch: Pack Memory Map-Time Reads

> **Status:** Complete
> **Author:** Claude Sonnet 5.5
> **Parent:** 2611121531-pack-memory-map-reads-plan.md
> **Sprint:** 2611121459-pack-memory-sprint.md — iteration 2
> **Related Projex:** 2611121531-pack-memory-map-reads-plan.md | 2611121459-pack-memory-sprint.md
> **Directive:** Execute 2611121531-pack-memory-map-reads-plan.md
> **Source Plan:** 2611121531-pack-memory-map-reads-plan.md
> **Result:** Success

---

## Summary

`MapContext` takes an optional `IMemory` and implements `IMemoryHost`, so `memory.*` resolves on the night map (`hidden_when`, intel line `cases`). Intel slots gain optional `when`; `IntelRoller.Roll` takes a scope and gives a gated slot an empty pick list without rolling dice. Game passes `new SaveMemory()` and `map.Root`. Area unlock by memory = memory-aware `hidden_when` (plan D1). All plan steps 1-4 done as written. Code commit: `684b090`.

---

## Changes

### Core
**Files:** `src/Core/ExpeditionRun.cs`, `src/Core/Model.cs`, `src/Core/PackLoader.cs`, `src/Core/Schema.cs`
**Change Type:** Modified
**What Changed:**
- `IMemoryHost` doc updated; `MapContext` gets `IMemory memory = null` ctor param, field, `IMemoryHost.Memory` explicit member; doc.
- `IntelSlot.When` (`Condition`); `Schema.Intel` accepts `when`; `CompileIntel` compiles it with the area's pack.
- `IntelRoller.Roll(area, roll, IScope scope)`: skips slot when `When` fails. Plan named the param `at`; renamed `scope` — `at` collided with the inner loop local (CS0136).

**Why:** Plan Steps 1-2.

### Game
**Files:** `src/Game/ExpeditionLocation.cs`, `src/Game/IntelCards.cs`
**Change Type:** Modified
**What Changed:** `MapContext` built with `new SaveMemory()`; `IntelRoller.Roll(..., map.Root)`.
**Why:** Plan Step 3.

### Test
**File:** `tests/Sim/Program.cs`
**Change Type:** Modified
**What Changed:** New `TestMapMemory` (registered after `TestMemory`); misplaced ammo comment moved above `TestReloadUsesWholeItems`.
**Why:** Plan Step 4.

---

## Verification

| ID | Result |
|----|--------|
| V1 `tests/Sim` | `All checks passed`, 0 FAIL lines — PASS |
| V2 game build `-warnaserror` | 0 warnings, 0 errors (installed to scratch dir) — PASS |
| V3 | `2` — PASS |
| V4 lint --strict | `lint sample_area: 0 error(s), 0 warning(s)`; `lint night_expeditions_reborn: 0 error(s), 0 warning(s)` — PASS |
| V5 | `ef-sim sample_area: all checks passed` — PASS |
| V6 NER 4000 runs | 23 `ne_` rows, `diff` identical to baseline — PASS |
| V7 hygiene grep | no matches — PASS |
| V8 scope grep | no matches — PASS |
| J1 | Game diff is exactly the two call-site edits — PASS |
| J2 | New comments present tense; `Roll` doc names rejected `canStart` gating and the no-dice rationale — PASS |

**Status:** PASS

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611121531-pack-memory-map-reads-plan.md | Source plan | Marked complete via this patch; set Complete, moved to closed/ |

---

## Notes

- Code commit trailer reads `Projex: 2611121545-pack-memory-map-reads`; the document is `2611121537-pack-memory-map-reads-patch.md` (timestamp guessed ahead of `new-projex`; commit can't be amended under the repo hook). Match by name `pack-memory-map-reads`.
- Carry-forward for iteration 5 README: intel `when`, memory in `hidden_when` as the unlock idiom, `MapContext` optional memory and `IntelRoller.Roll` scope param. Iteration 4: `tools/EfSim/PackRun.cs` `MapContext` stays store-less; ef-sim memory seed/lint.
- Nothing verifiable in-game.
