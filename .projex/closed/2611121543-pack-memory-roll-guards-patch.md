# Patch: Pack Memory Roll Guards

> **Status:** Complete
> **Author:** Claude Sonnet 5.5
> **Parent:** 2611121540-pack-memory-map-reads-audit.md
> **Sprint:** 2611121459-pack-memory-sprint.md — iteration 2
> **Related Projex:** 2611121540-pack-memory-map-reads-audit.md | 2611121537-pack-memory-map-reads-patch.md | 2611121531-pack-memory-map-reads-plan.md
> **Directive:** Address audit findings 1 and 2: untested dice-isolation property of a `when`-gated intel slot; uncommented `result[slot.Id] = picks;` ordering in `IntelRoller.Roll`. Findings 3 and 4 left for a later iteration.
> **Source Plan:** Direct
> **Result:** Success

---

## Summary

Audit gaps closed. `tests/Sim` now proves a gated slot rolls no dice (the property `Roll`'s doc credits); `Roll` carries a rationale comment on why every slot is registered before the early `continue`s. No behavior change.

---

## Changes

### `src/Core/ExpeditionRun.cs`

**Change Type:** Modified
**What Changed:**
- `IntelRoller.Roll`: 2-line comment above `result[slot.Id] = picks;` — gated/poolless slots must still be keys because `IntelCards.Rolled` indexes every slot; moving it below the `continue`s → `KeyNotFoundException` in game.

**Why:** Audit Source Hygiene rule 6 gap — non-obvious ordering, no framework test would catch a tidy-up.

### `tests/Sim/Program.cs`

**Change Type:** Modified
**What Changed:**
- `TestMapMemory`: logging `roll` records requested sides. Gated `tip` → log `100,1` (only `rumor`'s count roll + pick roll) and `tip` stays a key with 0 picks. Open `tip` → `100,1,100,1`.

**Why:** Old test used `n => 1`; rolling dice before the `when` check would stay green.

---

## Verification

**Method:** framework sim; game build; ef-sim lint/run on sample.

**Result:**
```
tests/Sim: All checks passed
game build -c Release -warnaserror (GameManaged/InstallDir flags): 0 warnings, 0 errors
ef-sim lint samples/SampleArea --strict: 0 error(s), 0 warning(s)
ef-sim run samples/SampleArea: all checks passed
```

**Status:** PASS

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611121540-pack-memory-map-reads-audit.md | Parent | Findings 1, 2 addressed here; none edited |

---

## Notes

- Code commit `3394ad2` trailer `Projex: 2611121543-pack-memory-roll-guards` matches this doc.
- `when` with `counts`/`chance` slots still unexercised (audit Testing "Missing" 2nd bullet) and the trailer-id mismatch (finding 3) are untouched.
- Regression check by mutation not run; assertions compare exact dice logs.
