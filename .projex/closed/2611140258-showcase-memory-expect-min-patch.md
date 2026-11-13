# Patch: Showcase memory expect survives --runs

> **Status:** Complete
> **Author:** Claude Sonnet 5.5 (patcher)
> **Parent:** 2611140252-ef-sim-reach-and-item-guard-audit.md
> **Related Projex:** 2611140220-ef-sim-reach-and-item-guard-plan.md
> **Directive:** Audit finding M1: Showcase `sim.json` row 2 expects exact `memory.visits` 203, so Showcase fails at `--runs 4000`. Fix without weakening the check.
> **Source Plan:** Direct
> **Result:** Success

---

## Summary

Carried row: `memory.visits` = start 3 + runs. `--runs 4000` overrides the row's 200 → 4003, so exact 203 failed. Changed expect to `{ "min": 203 }`: default counts still require every run's write to land (203 reached); larger `--runs` passes. Exact count was inherently run-count-bound, as `docs/testing.md` Memory section already advises (use `min`/`max`).

## Changes

**File:** `docs/examples/Showcase/sim.json` (row 2 `memory.expect`) | `docs/testing.md` (Memory example, same row)
**Change Type:** Modified
**What Changed:** `"expect": { "visits": 203 }` → `"expect": { "visits": { "min": 203 } }`
**Why:** Keep the example runnable at any `--runs`; docs example mirrors Showcase. Upper bound is dropped: unavoidable, any `max` would fail at larger `--runs`. Under-count (a run losing its write) is still caught.

Commit: `26bd86f`.

## Verification

**Method:** `ef-sim run` Showcase at default and `--runs 4000`; strict lint.

**Result:**
```
run (default):      ef-sim ef_showcase: all checks passed
run --runs 4000:    ef-sim ef_showcase: all checks passed
lint --strict:      0 error(s), 0 warning(s)
```
NER output untouched (only Showcase and its doc example changed).

**Status:** PASS

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611140252-ef-sim-reach-and-item-guard-audit.md | Parent (finding M1) | None; close handles it |

## Notes

M2–M4 untouched.
