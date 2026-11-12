# Patch: ef-sim skeleton

> **Status:** Complete
> **Author:** Claude Sonnet 5.5 (patch, plan-execution mode)
> **Parent:** 2611121012-ef-sim-skeleton-plan.md
> **Sprint:** 2611121010-pack-defined-sims-sprint.md — iteration 1
> **Directive:** Execute 2611121012-ef-sim-skeleton-plan.md (all steps 1–6)
> **Source Plan:** 2611121012-ef-sim-skeleton-plan.md
> **Result:** Success

---

## Summary

New net8.0 tool `tools/EfSim` (assembly `ef-sim`): `lint` (moved from `tests/Sim/Lint.cs`, shared by include) and `run` (`sim.json` v1, kit bridge, random bot, built-in checks, balance table). Proven on `samples/SampleArea`. Plan implemented as specified; code commit `84cced3` (trailer `Projex: 2611121035-ef-sim-skeleton`).

---

## Changes

### Lint move + helpers
**File:** `tools/EfSim/Lint.cs` (git mv from `tests/Sim/Lint.cs`), `tests/Sim/Sim.csproj`
**Change Type:** Modified (moved)
**What Changed:**
- `Framework`/`MainFile` now `internal`; usage string names `ef-sim`; `Run` body split into `ReadId` + `Load` (reused by `run`)
- `tests/Sim/Sim.csproj` compiles `..\..\tools\EfSim\Lint.cs`
**Why:** one lint source; `run` reuses manifest/compile/missing-text logic.

### New tool
**Files:** `tools/EfSim/EfSim.csproj`, `Program.cs`, `SimConfig.cs`, `KitBridge.cs`, `PackRun.cs`
**Change Type:** Created
**What Changed:** exactly the code in plan Steps 1–4 (dispatch, strict `sim.json` v1 reader, kit-driven `IBridge`, run matrix + random bot + checks + table).

### Sample data + docs
**Files:** `samples/SampleArea/sim.json` (created), `README.md` (lint command → `tools/EfSim`, new `### ef-sim run` section)
**Why:** C4 / C11.

---

## Verification

**Method:** plan Verification Plan, Git Bash, built once then `--no-build`.

**Result:**
| Check | Result |
|---|---|
| V1 | build exit 0, 0 EfSim warnings, `ef-sim.dll` present — PASS |
| V2 | `tests/Sim/Lint.cs` gone, `tools/EfSim/Lint.cs` present, include present once — PASS (see Notes on grep pattern) |
| V3 | lint output + exit identical `ef` vs `tests/Sim` on sample, broken fixture, NER folder: `SAME` x3 — PASS |
| V4 | `lint sample_area: 0 error(s), 0 warning(s)`, exit 0 — PASS |
| V5 | `MATCH` (16 ids) — PASS |
| V6 | exit 0; 2 table rows; last line `ef-sim sample_area: all checks passed` — PASS. Rows: knife 2.8% / 91.6% / 8.4%; everything 37.8% / 54.6% / 45.4% (cleared / knocked out / extracted) |
| V7 | two runs `IDENTICAL` — PASS |
| V8 | `--runs 7` → 2 rows — PASS |
| V9 | unknown kit → `ERROR sim.json: runs[0].kit: no kit named nope` exit 2; no sim.json → `ERROR sim.json: no sim.json in the pack folder` exit 2 — PASS |
| V10 | bogus id → exit 1 + `FAIL never offered: no_such_option`; missing text → exit 1, 1 `FAIL missing text` — PASS |
| V11 | `tests/Sim` → `All checks passed`, exit 0 — PASS |
| V12 | README: `tools/EfSim -- run` 1, `tools/EfSim -- lint` 1, `"version": 1` 1, `expect` 4 — PASS |
| V13 | game assembly: `0 個警告` / `0 個錯誤` (0 Warning(s), 0 Error(s)), installed to scratch dir only — PASS |
| V14 | `git status --porcelain` empty after code commit — PASS |

**Status:** PASS

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611121012-ef-sim-skeleton-plan.md | Source plan | marked `[PATCHED]`, Status Complete, moved to `closed/` |

---

## Notes

- No deviation from plan code. A2 (all 16 ids offered with kit bridge) and A3 (no runtime-text / Hud failures on sample) held.
- Tooling quirks: `new-projex.ps1` rejects absolute `-ProjexDir`; its generated uid (`2611121022`) differed from the code-commit trailer uid (`2611121035`), so the doc was renamed to match the trailer. V2's `grep 'tools\\EfSim\\Lint.cs'` returned 0 under this harness (backslash mangling); confirmed with `grep -F 'tools\EfSim\Lint.cs'` → 1.
- Build/test output (`bin/`, `obj/`) is gitignored; scratch fixtures lived in `mktemp` dirs, removed.
