# Patch: Pack Memory — ef-sim Memory Model + Read-Never-Written Warning

> **Status:** Complete
> **Author:** Claude Sonnet 5.5 (patch)
> **Parent:** 2611121613-pack-memory-sim-memory-plan.md
> **Sprint:** 2611121459-pack-memory-sprint.md — iteration 4
> **Directive:** Execute 2611121613-pack-memory-sim-memory-plan.md
> **Source Plan:** 2611121613-pack-memory-sim-memory-plan.md
> **Related Projex:** 2611121459-pack-memory-sprint.md | 2611121613-pack-memory-sim-memory-plan.md
> **Result:** Success

---

## Summary

Executed the plan in full, as written (all 4 steps, 7 files, commit `9360b37`). Loading a pack now warns once per `memory.NAME` / `memory.$NAME` it reads but never writes; `ef-sim run` gained `runs[n].memory` (`start`, `carry`, `expect`). Every verification row V1–V11 passed on first run; no deviation from the plan's code.

---

## Changes

### Core bookkeeping + warning

**Files:** `src/Core/Expr.cs`, `src/Core/PackLoader.cs`, `src/Core/Builtins.cs`
**Change Type:** Modified
**What Changed:**
- `Expr`: `memoryRead` field + ctor param; public `Compile` forwards to a new internal overload taking `Action<string> memoryRead`; `Atom` reports each `memory.*` word read.
- `PackLoader`: `memoryReads` / `memoryWrites` fields; `NoteMemory`; `Finish` warns for reads without a write in the same pack; read noted in `ValueOf`, `Template`, `TextArg`; `Spec.NoteMemory`.
- `Builtins`: `set` / `add` (`Assign`) and `set_text` `var` note writes; `set_text` `value` notes a read.

**Why:** Roles (read vs write) are only known where the compiler resolves paths with their pack; a text scan in lint would duplicate the path grammar. Internal overload keeps `Expr.Compile`'s public binary signature.

### Tests

**File:** `tests/Sim/Program.cs`
**Change Type:** Modified
**What Changed:**
- `TestMemoryWarnings` added, called from `Main` after `TestGameReads`: five unwritten names warn exactly once, writes via `add` / `set` / `set_text`, other pack's write doesn't count, number and text names separate.
- `TestMapMemory` and `TestGameReads` packs gained the room writes for the names they read (so they stay warning-free).

### ef-sim memory model

**Files:** `tools/EfSim/SimConfig.cs`, `tools/EfSim/KitBridge.cs`, `tools/EfSim/PackRun.cs`
**Change Type:** Modified
**What Changed:**
- `SimConfig`: `SimRow.Memory`, `SimMemory` (start / carry / expect check), `SimMemoryCheck`, `MemoryOf` + `IsTextName` strict parse.
- `KitBridge`: optional `IMemory` ctor param (default fresh `MemoryStore`).
- `PackRun`: `Play` takes the pack id and returns the last run's store; one shared store per row with `carry`, else a fresh seeded store per run; `Run` collects expectation misses as `FAIL runs[R]: …` after run problems.

**Why:** Store creation draws no random numbers and `Random` is still created once per row, so rows without `memory` replay identically.

---

## Verification

**Method:** Plan V1–V11 from the sprint work-root; fixture built per plan block F in `$TEMP/ef-mem-fixture` (outside the repo).

**Result:**
```
V1  tests/Sim: exit 0, "All checks passed", 0 FAIL
V2  game-assembly build (-warnaserror): 0 warnings, 0 errors
V3  lint sample_area: 0 error(s), 0 warning(s) | lint night_expeditions_reborn: 0 error(s), 0 warning(s)  (strict, exit 0)
V4  ef-sim sample_area: all checks passed
V5  NER 4000-run ne_ rows (23) identical to baseline (diff empty)
V6  fixture: ef-sim mem_fixture: all checks passed (visits 15 / 11 / 1, titles you / none / "")
V7  FAIL runs[0]: memory.visits should be 99, got 15
    FAIL runs[1]: memory.$title should be "x", got "none"
    ef-sim mem_fixture: 2 check(s) failed   (exit 1)
V8  six exact ERROR sim.json lines, exit 2 each
V9  WARN … memory.ghost is read but never written by this pack, so it always reads 0
    lint mem_fixture: 0 error(s), 1 warning(s)   (--strict exit 1)
V10 no output | V11 no output
```

**Status:** PASS

Judgment checks: new comments carry rationale (Expr overload vs optional param, warning vs problem, carry default); `SimMemory.Check` wording reads naturally for exact / range / text forms. Assumption A1 held (one `enter` per run).

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611121613-pack-memory-sim-memory-plan.md | Source plan | Status → Complete, executed via this patch, moved to `.projex/closed/` |
| 2611121459-pack-memory-sprint.md | Sprint nav | Not touched (orchestrator-owned) |

---

## Notes

- `--runs N` on a carried row changes the carried count; exact `expect` over a carry row holds only at the row's own `runs` (plan D5). Iteration 5's README must say so.
- Scratch fixture and NER output live in `$TEMP`; nothing untracked left in the worktree.
- NER and sample packs were not edited.
