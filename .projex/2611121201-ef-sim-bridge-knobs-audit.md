# Audit: ef-sim bridge knobs

> **Status:** Complete (Accept)
> **Author:** Claude Sonnet 5.5 (auditor)
> **Parent:** 2611121154-ef-sim-bridge-knobs-patch.md
> **Sprint:** 2611121010-pack-defined-sims-sprint.md — iteration 4
> **Related Projex:** 2611121143-ef-sim-bridge-knobs-plan.md | 2611121154-ef-sim-bridge-knobs-patch.md | 2611121128-ef-sim-ner-pack-audit.md
> **Audit Date:** 2026-11-12 | **Subject:** commits `5cf37ad`, `d60b918`, NER `sim.json` edit (on disk)

---

## Audit Summary

**Claim:** `sim.json` `bridge` object added; NER `sim.json` mirrors `FakeBridge`; C# vs ef-sim 4000-run tables identical (46/46 cells, sprint criterion 3).

**Verdict:** Verified

**Assessment:** Completeness: High | Correctness: High | Quality: High | Value: High

**Top Issues:**
1. `lose_cash_up_to: 2147483647` overflows `most + 1` → runtime `ArgumentOutOfRangeException` on first closed trade (Low; accepted by parser).
2. Landed feature commit `5cf37ad` has no `Projex:` trailer; `main` carries 2/30 (informational, fix-forward).
3. 12 `never picked an option` WARNs on NER default run — pre-existing (iteration 3 audit recorded them), not caused by this change.

---

## Claims vs Evidence

| Claim | Evidence | Status | Notes |
|-------|----------|--------|-------|
| Parity 46/46 cells Δ 0.0 | Own re-run: NER C# `SIM_RUNS=4000 dotnet run -c Release --project tests/Sim` (exit 0, `All checks passed`) vs `ef-sim run NER --runs 4000` (exit 0, `all checks passed`); parsed cleared + knocked-out for 23 rows | ✓ | 23 rows both, row order identical, 46 cells, worst abs delta 0 |
| NER sim.json hash `7133cc99…` | sha256 matches (file is LF; CR-stripped and raw equal) | ✓ | |
| Bridge mirrors `FakeBridge` | Program.cs `FakeBridge`: `luck`→50, `roll_luck`→`random.Next(2)`, `OpenTrade`→true, `CloseTrade`→`-random.Next(0,30)`, `TakeCart`→`["cart_goods"]`, `TakeBack` ground[0], `LoseCarried`→`["something"]`. sim.json: `luck 50`, `roll_luck coin`, `opens true`, `lose_cash_up_to 29`, `cart ["cart_goods"]`, `take_back oldest`, `lose_carried ["something"]` | ✓ | Member-for-member; no tuned values. `Next(0, 29+1)` == `Next(0,30)` same RNG draw |
| Defaults preserve behaviour (V2) | sample run sha256 `b1565178…` | ✓ | |
| Build 0 warn/err (V1) | `-warnaserror` build | ✓ | |
| NER default run (V6) | exit 0, 23 rows, `packing: 6 case(s), 0 failed`, `all checks passed`, sha256 `0c40c20e…` | ✓ | |
| Lint strict (V11) | sample 0/0, NER 0/0 | ✓ | |
| Bad-input probes (V5) | 11 probes (list, unknown key, bad game value x3, bad opens, negative cash, bad take_back, bad lose_carried x2, bad cart) | ✓ | all `ERROR sim.json: …`, exit 2 |
| NER baseline untouched (V16) | `Program.cs` `d4732e8e…`, `Sim.csproj` `26ca5458…` | ✓ | |
| Scope (V15) | `git diff --name-only 205e276 5cf37ad` = README + 3 `tools/EfSim` files (+ plan doc from 205e276..HEAD range) | ✓ | |
| README documents bridge (C7) | `#### Bridge` section, example equals NER block | ✓ | |
| V3, V4, V7–V10, V12–V14 | V7/V9 re-measured above; V4 re-hashed. V3, V8-hash, V10, V12 not re-run (V8 table content verified equal to C# instead; game-assembly build not re-run — change is confined to `tools/`, which the game csproj does not compile) | ⚠ | accepted as low risk |

---

## Objective Verification

### C5 / sprint criterion 3 — parity
**Evidence:** independent runs above. **Actual:** exact equality on all 46 cells. **Verification:** ✓ Verified.
Exactness is explained by design: every RNG-drawing bridge member matches `FakeBridge` draw for draw. The result was not reached by tuning NER data or the tolerance; the only NER change is the `bridge` block.

### C1–C4, C6, C7
✓ — see table.

---

## Code/Implementation Inspection

### `tools/EfSim/SimConfig.cs`, `KitBridge.cs`, `PackRun.cs`
**Claimed:** per plan, verbatim. **Actual:** diff matches plan code; quality High. Strict parse; defaults draw nothing extra (sample hash unchanged).

**Issues Found:**
- `CloseTrade`: `-random.Next(0, most + 1)` overflows at `most == int.MaxValue`; parser only rejects negatives — Severity: Low (absurd input; also an unhandled exception rather than `ERROR sim.json`). Fix: cap at `int.MaxValue - 1` in `ReadBridge`, or a one-line patch.
- `bridge.game` int range: 2000000000000 correctly rejected by `Int`.
- `CoinGame` and `Game` may both hold a name only if declared twice in JSON (impossible with dictionary parse) — none.

**Undocumented:** none.

---

## Testing Validation

No unit-test harness for ef-sim; verification is run-based (hash/diff parity, probes). Parity against an independent C# implementation is a strong oracle. Missing: no committed regression probe for bridge parse errors (project has none for other sim.json keys either).

---

## Documentation Audit

README accurate against code (defaults, units, error behaviour, shared-RNG caveat). Complete.

---

## Source Hygiene

**Scope:** Diff only (`git show 5cf37ad -- . ':(exclude,glob)**/.projex/**'`)

| Rule | Location (symbol) | Quote | Severity |
|------|-------------------|-------|----------|
| — | — | no violations: no projex ids/labels, no `file:ln`, no changelog narration, no plan shape | — |

**Rule 6 gaps:** none. Fixed `lose_carried` list not rolling/removing is explained at `KitBridge` → `LoseCarried`; `ReadBridge` naming (CS0102 clash) trivial.
**Rule 6 vacuity:** none; comments state defaults and the RNG-draw consequence.
**Rationale promotion:** shipped in README `#### Bridge` (RNG-draw caveat included); the patch doc has no unpromoted load-bearing rationale.

**Commit composition** (informational — fix forward): 2 landed commits checked — typed subject where required 1/1 (`feat(ef-sim): …`) | `Projex:` trailer 0/1.
**Violations:** `5cf37ad` — no `Projex:` trailer.

**Trailer survival on `main`:** 2/30 of recent commits carry a `Projex:` trailer — pre-existing state of the base, not a result of this work; informational.

---

## Gap Analysis

### Promised But Not Delivered
None.

### Undocumented Issues
| Issue | Severity | Affects |
|-------|----------|---------|
| 12 `never picked an option` WARNs in NER default run | Low (pre-existing; recorded in iteration 3 audit) | NER bot tuning |
| int.MaxValue overflow in `lose_cash_up_to` | Low | edge input |

### Unhandled Edge Cases
- `lose_cash_up_to` = 2147483647 → runtime exception on trade close.

---

## Quality Assessment

**Completeness:** High. **Correctness:** High (exact parity re-measured). **Code Quality:** High. **Value:** High — sprint criterion 3 closed with equality, not merely within ±3.
**Caveat:** NER `sim.json` edit is unversioned on disk (NER has no commits); only the stated hash records it. Plan acknowledged this.

---

## Open Findings

- NER C# sim builds the engine from `main`; parity holds because the sprint branch touches no `src/`. Re-run parity at sprint close if `src/` changes.
- Future: Gunsmith loot check (iteration 5) independent.

---

## Findings

### Critical (Must Address)
None.

### Significant (Should Address)
None.

### Minor (Nice to Fix)
- **`lose_cash_up_to` int.MaxValue overflow** → small patch: reject values ≥ `int.MaxValue` in `ReadBridge` (or compute with `long`). Patch-fixable.
- **`5cf37ad` missing `Projex:` trailer** → informational, fix forward; no history rewrite.

### Positive
- Exact parity with an independent implementation; strict parser; defaults byte-identical; README example identical to NER block.

---

## Final Verdict

**Status:** Accept

**Sign-off:** Yes — sprint criterion 3 independently reproduced (46/46 cells, worst delta 0.0); bridge settings faithfully mirror `FakeBridge`.
