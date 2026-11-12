# Patch: ef-sim-ner-pack

> **Status:** Complete
> **Author:** Claude Sonnet 5.5 (executor)
> **Parent:** 2611121110-ef-sim-ner-pack-plan.md
> **Sprint:** 2611121010-pack-defined-sims-sprint.md — iteration 3
> **Related Projex:** 2611121110-ef-sim-ner-pack-plan.md | 2611121010-pack-defined-sims-sprint.md
> **Directive:** Execute 2611121110-ef-sim-ner-pack-plan.md (plan-execution mode)
> **Source Plan:** 2611121110-ef-sim-ner-pack-plan.md
> **Result:** Success

---

## Summary

`sim.json` rows accept a kit list cycled by run index; `expect.packing` checks `MapContext.Missing`. NER gets its `sim.json`; `ef-sim run` on NER exits 0, output byte-identical to the plan's prototype. Code commit `853846b` on `projex/sprint/2611121010-pack-defined-sims`. Plan code and data applied verbatim; no deviation.

---

## Changes

**`tools/EfSim/SimConfig.cs`** (Modified) — `SimRow.Kits` list + computed `Kit` label, `SimPacking`, `Packing` list, shared `Items(...)` parser for kits and bags, `kit` string-or-list parsing with located errors, `expect.packing` parsing.
**`tools/EfSim/PackRun.cs`** (Modified) — areas keyed by name (rows + packing cases), `AreaId`, `CheckPacking`, `Play` takes `List<SimKit>` and uses `kits[n % kits.Count]`.
**`README.md`** (Modified) — checks list, kit-list paragraph, `#### Packing` section (file is CRLF; preserved).
**`NightExpeditionsReborn/sim.json`** (Created, OUTSIDE the repo, uncommitted; NER has no commits) — path `E:\SteamLibrary\steamapps\common\Probably Stolen\Modding\NightExpeditionsReborn\sim.json`, sha256 (CR stripped) `36c3c1603d7542d020ea2b21d4f2448421ed2a904d016bb9de23cf48e3360d82` (V3 match). NER `tests/Sim` untouched (V14).

---

## Verification

**Method:** plan V1–V14, run in the work-root.

| ID | Result |
|----|--------|
| V1 | `0 Warning(s)`, `0 Error(s)` — PASS |
| V2 | sample output `exit=0`, `SAME` as baseline (`b1565178…`) — PASS |
| V3 | `36c3c160…` — PASS |
| V4 | `exit=0`; 23 rows; 1 `packing: 6 case(s), 0 failed`; 0 FAIL/ERROR; last line `ef-sim night_expeditions_reborn: all checks passed`; sha256 `ba138a66…` equals expected; 12 expected `never picked` WARNs — PASS |
| V5 | second run `IDENTICAL` — PASS |
| V6 | all 8 probes print the expected `ERROR` line, `exit=2` — PASS |
| V7 | `FAIL expect.packing[2]` / `[5]` lines as expected, `exit=1` (empty bag ≠ no backpack) — PASS |
| V8 | framework `tests/Sim`: `All checks passed`, `exit=0` — PASS |
| V9 | lint sample and NER `--strict`: `0 error(s), 0 warning(s)` each — PASS |
| V10 | game assembly `0 Warning(s)`, `0 Error(s)` (InstallDir scratch) — PASS |
| V11 | README keyword counts `1 3 1 2 1 1` — PASS |
| V12 | `git status --porcelain` clean after code commit apart from this doc — PASS |
| V13 | changed files vs plan add commit: `README.md`, `tools/EfSim/PackRun.cs`, `tools/EfSim/SimConfig.cs` — PASS |
| V14 | NER `tests/Sim` hashes `d4732e8e…`, `26ca5458…` unchanged — PASS |

**Status:** PASS

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611121110-ef-sim-ner-pack-plan.md | Source plan | Status → Complete; execution note; moved to `closed/` |

---

## Notes

- Code commit carries `Projex: 2611121121-ef-sim-ner-pack` (stamped before the doc was written; matches this doc's id). Both trailers survived `stage-n-commit`.
- Judgment check (greedy vs careful in exactly the four mapped places; kits match `FakeBridge`): data is the plan's pinned bytes; not independently re-derived from NER source.
- NER is not versioned, so `sim.json` appears in no commit; delete that one file to roll back.
