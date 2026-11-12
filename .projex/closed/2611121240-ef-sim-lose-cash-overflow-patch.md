# Patch: ef-sim-lose-cash-overflow

> **Status:** Complete
> **Author:** Claude Sonnet 5.5 (patcher)
> **Parent:** 2611121201-ef-sim-bridge-knobs-audit.md
> **Sprint:** 2611121010-pack-defined-sims-sprint.md — iteration 4
> **Related Projex:** 2611121201-ef-sim-bridge-knobs-audit.md | 2611121154-ef-sim-bridge-knobs-patch.md
> **Directive:** Audit finding 1 — `bridge.trade.lose_cash_up_to: 2147483647` passes `ReadBridge`, then `KitBridge.CloseTrade` computes `random.Next(0, most + 1)` → int overflow at first closed trade. Reject or handle safely; existing packs byte-identical, parity unchanged.
> **Source Plan:** Direct
> **Result:** Success

---

## Summary

Parser now rejects `lose_cash_up_to == int.MaxValue` (negatives already rejected). Max accepted: 2147483646, so `most + 1` cannot overflow. Rejection at parse time = loud, early failure; chosen over widening `KitBridge` arithmetic since a 2.1-billion cash loss has no meaningful use.

## Changes

### Parser bound

**File:** `tools/EfSim/SimConfig.cs` (`ReadBridge`)
**Change Type:** Modified
**What Changed:**
- Condition `< 0` → `< 0 || == int.MaxValue`; message now `must be between 0 and 2147483646`
- Added comment naming the `Next(0, most + 1)` overflow as the reason

**Why:** Removes the overflow input at the only entry point.

### Docs

**File:** `README.md`
**Change Type:** Modified
**What Changed:** `lose_cash_up_to` description notes range `0 to 2147483646`.

## Verification

**Method:** (1) framework sim run before/after, output diffed; (2) `ef-sim run` on a scratch copy of the sample pack with `bridge.trade.lose_cash_up_to` set to 2147483647 and 2147483646.

**Result:**
```
before/after tests/Sim output: IDENTICAL (All checks passed)
2147483647 -> ERROR sim.json: bridge.trade.lose_cash_up_to must be between 0 and 2147483646
2147483646 -> ef-sim sample_area: all checks passed
```
Sample pack `sim.json` has no `bridge` block, so its behavior cannot change. NER not touched (read-only; its `sim.json` values are well below the bound; NER run not repeated since the change only adds a rejection of one value).

**Status:** PASS

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611121201-ef-sim-bridge-knobs-audit.md | Parent audit, finding 1 | none (audit is a record) |

## Notes

Code commit: e92e015. Scratch pack lived in the session scratchpad, not the repo.
