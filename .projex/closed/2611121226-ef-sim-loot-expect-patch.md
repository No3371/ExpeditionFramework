# Patch: ef-sim-loot-expect

> **Status:** Complete
> **Author:** Claude Sonnet 5.5 (patcher)
> **Parent:** 2611121215-ef-sim-loot-expect-plan.md
> **Sprint:** 2611121010-pack-defined-sims-sprint.md — iteration 5
> **Related Projex:** 2611121215-ef-sim-loot-expect-plan.md | 2611121010-pack-defined-sims-sprint.md
> **Directive:** Execute plan 2611121215-ef-sim-loot-expect-plan.md in full (plan-execution mode).
> **Source Plan:** 2611121215-ef-sim-loot-expect-plan.md
> **Result:** Success

---

## Summary

`sim.json` gains `expect.loot` cases: roll named loot tables N times in one run of an area, optionally with the pack reloaded with extra mods counted as installed; fail on a forbidden id prefix or a required id that never rolls. NER `sim.json` (on disk, unversioned) gets the two cases reproducing `CheckGunsmithLoot`. Code is the plan's prototype, applied verbatim.

Code commit: `13ed7b2` `feat(ef-sim): expect.loot cases in sim.json`.

---

## Changes

### Parser

**File:** `tools/EfSim/SimConfig.cs`
**Change Type:** Modified
**What Changed:**
- `SimLoot` class; `SimConfig.Loot` list; `loot` allowed under `expect`; `LootCase` strict parser (method named `LootCase`, not `Loot`, to avoid CS0102).

**Why:** C1 — strict, located config errors (exit 2).

### Check

**File:** `tools/EfSim/PackRun.cs`
**Change Type:** Modified
**What Changed:**
- Loot areas validated with rows/packing; `CheckLoot` rolls each case (own `Random(seed)`, after all rows), reloads via `Lint.Load` for `mods` cases, prints `loot: N case(s), M failed`; wired before `problems` copy so run-time problems surface.

**Why:** C2.

### Docs

**File:** `README.md`
**Change Type:** Modified
**What Changed:**
- `### ef-sim run` checks paragraph mentions `expect.loot`; new `#### Loot` section before `#### Bridge`.

**Why:** C8.

### NER data (unversioned)

**File:** `NightExpeditionsReborn/sim.json`
**Change Type:** Modified (on disk only; NER is not under git, no git commands run there)
**What Changed:**
- Appended `expect.loot` with 2 `ne_foundry` cases, 6 tables, 2000 rolls, seed 7: case 0 `never_prefix gsx_`; case 1 `mods: [gunsmith_expansion]` with the 7 `must_roll` `gsx_` ids. sha256 `65ddd3c4…9d078`.

**Why:** C3 — last NER C# sim check (`CheckGunsmithLoot`) now has a data form.

---

## Verification

**Method:** Plan verification suite V1–V13.

**Result:**

| ID | Result |
|----|--------|
| V1 | `0 Warning(s)`, `0 Error(s)` (`-warnaserror`) |
| V2 | exit 0; `loot: 2 case(s), 0 failed` x1; 0 FAIL/ERROR; sha256 `933fe01596867350b0549036885908c749c5c86f7aeae241eed545cf8b69a17e`; `ne_` lines `b9cdd053…f646`; diff = one added line `> loot: 2 case(s), 0 failed` |
| V3 | `65ddd3c41150b0cec6f5a9c0261dfbd42d1e951e4b9d437c370d94d114d39078` |
| V4 | exit 0, `SAME` |
| V5 | 4 probes: pinned FAIL lines, exit 1 each |
| V6 | 7 probes: pinned ERROR lines, exit 2 each; `RESTORED` |
| V7 | `All checks passed`, exit 0 |
| V8 | sample 0/0; NER 0/0; NER `--mod gunsmith_expansion` 0/0 |
| V9 | `0 Warning(s)`, `0 Error(s)` (InstallDir scratch) |
| V10 | ner=0, sample=0 |
| V11 | `1 1 1 1 1 1` |
| V12 | NER `tests/Sim` hashes unchanged (`d4732e8e…`, `26ca5458…`) |
| V13 | diff files: `README.md`, `tools/EfSim/PackRun.cs`, `tools/EfSim/SimConfig.cs` |
| J1 | NER cases mirror `CheckGunsmithLoot`: area, 6 tables in order, 2000 rolls, seed 7, `gsx_` forbidden without mod, 7 ids required with `gunsmith_expansion` |
| J2 | `mods` load errors path (`failures.AddRange(errors…)`, case skipped) reviewed by reading; no data triggers it |

**Status:** PASS

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611121215-ef-sim-loot-expect-plan.md | Source plan | Status → Complete; `[PATCHED]`; moved to `.projex/closed/` |

---

## Notes

- Deviations: none from plan code. Commit trailer is `Co-Authored-By: Claude Sonnet 5.5` per plan note; no `Projex:` trailer (plan's Step 4 specified the message). Scratch/probes ran only on a copy of NER under the session scratchpad.
- NER `sim.json` edit is outside the sprint branch; restore via the pre-change copy (`7133cc99…`) if needed.
