# Audit: ef-sim skeleton

> **Status:** Complete
> **Author:** Claude Sonnet 5.5 (audit)
> **Parent:** 2611121035-ef-sim-skeleton-patch.md
> **Sprint:** 2611121010-pack-defined-sims-sprint.md — iteration 1
> **Related Projex:** 2611121012-ef-sim-skeleton-plan.md | 2611121035-ef-sim-skeleton-patch.md
> **Audit Date:** 2026-11-12 | **Subject:** commits 84cced3, b77b0ef on `projex/sprint/2611121010-pack-defined-sims`

---

## Audit Summary

**Claim:** patch executed plan steps 1–6; all of V1–V14 pass.

**Verdict:** Verified

**Assessment:** Completeness: High | Correctness: High | Quality: High | Value: High

**Top Issues:** none blocking. Minor: README prose placement; `Wander` 5% head_home unexplained; `cleared`/`extracted` overlap only documented in README.

---

## Claims vs Evidence

All checks re-run independently (Git Bash, built once, `--no-build`; fixtures in `mktemp`, removed).

| Claim | Evidence | Status |
|-------|----------|--------|
| V1 build, 0 EfSim warnings, `ef-sim.dll` | `--no-incremental` build exit 0, no warning lines, dll present | ✓ |
| V2 single lint source | `tests/Sim/Lint.cs` absent, `tools/EfSim/Lint.cs` present, include count 1 | ✓ |
| V3 lint identity (sample, broken fixture, NER) | 3 × SAME (exit 0 / 1 / 0) | ✓ |
| V4 sample lint strict | `lint sample_area: 0 error(s), 0 warning(s)`, exit 0 | ✓ |
| V5 expectation set == framework sim list | MATCH, 16 ids | ✓ |
| V6 run table | exit 0; knife 2.8/91.6/8.4, everything 37.8/54.6/45.4; `ef-sim sample_area: all checks passed` | ✓ (numbers identical to executor's) |
| V7 determinism | IDENTICAL | ✓ |
| V8 `--runs 7` | both rows show 7 | ✓ |
| V9 config errors | exit 2 both messages as specified | ✓ |
| V10 live checks | `FAIL never offered: no_such_option` exit 1; `FAIL missing text sample_area:sa_area_title` exit 1 | ✓ |
| V11 framework sim | `All checks passed`, exit 0 | ✓ |
| V12 README | counts 1/1/1/4 | ✓ |
| V13 game assembly | 0 warnings / 0 errors, installed to scratch dir only | ✓ |
| V14 clean tree | `git status --porcelain` empty before and after my run | ✓ |

C1–C11 all satisfied via the above. Judgment checks: README schema section is short and clear; `KitBridge` members match `IBridge` contract (no deviation spotted; `OpenTrade` returns false so trades never occur — consistent with the plan's out-of-scope list).

---

## Code/Implementation Inspection

- `tools/EfSim/PackRun.cs` — matches plan; deterministic (single seeded `Random` per row shared by roll, bridge, bot). Exceptions caught per run and reported as FAIL. Problems deduped via set + ordinal sort → stable output. Quality: High.
- `tools/EfSim/KitBridge.cs` — straightforward; `item.ApplyStats` then kit props win (documented in README). Quality: High.
- `tools/EfSim/SimConfig.cs`, `Program.cs` — strict reader, error exit 2. Quality: High.
- `tools/EfSim/Lint.cs` — git rename with `ReadId`/`Load` extraction; visibility widened to `internal`; behaviour proven identical by V3.
- No TODO/hack markers. No undocumented files in commit `84cced3` (9 files, all in plan Key Files).
- Observation: run outcome `cleared` and `extracted` overlap (a cleared run that leaves counts in both), so row percentages can exceed 100% in sum. Documented in README ("cleared runs included"); not a defect.

---

## Source Hygiene

**Scope:** diff only (`git show 84cced3`, `.projex/**` excluded).

| Rule | Location | Quote | Severity |
|------|----------|-------|----------|
| — | none found | | |

All added/modified comments checked: no projex IDs, no plan labels, no `file:ln`, present tense, no banners. One `file`-pointer-free cross reference ("the same way tests/Sim finds it") names a shipped path, allowed.

**Rule 6 gaps:** `PackRun` → `Wander` — 5% `head_home` probability differs from `tests/Sim` (3%) with no note on why; Low, a reader may mistake it for drift. (NER sim uses 5%, which is presumably the source.)
**Rule 6 vacuity:** none.
**Rationale promotion:** none needed — design rationale is in the shipped README section; walkthrough-only content is verification record.

**Commit composition** (informational): 84cced3 `feat(ef-sim): …` with `Projex: 2611121035-ef-sim-skeleton` trailer and Co-Authored-By; type matches diff. b77b0ef and a2fe3b6 are doc-only (exempt). Violations: None.
**Trailer survival on `main`:** 1/30 recent commits carry `Projex:` — informational only: convention appears recent on `main`; sprint squash-close message must carry the trailer to avoid losing the channel (check at sprint close).

---

## Gap Analysis

Promised but not delivered: none. Undocumented issues: none found. Edge cases: `--runs` rejects non-positive ints with usage exit 2 (fine); an area key naming another pack's area is accepted by `:` rule; kit item without `in_hand` is equipped by `Equip` only — acceptable for skeleton.

---

## Findings

### Critical
none

### Significant
none

### Minor
- **README sentence placement** — line "`tests/Sim -- lint` runs the same code." is appended to the lint paragraph without a break; reads as a stray fragment. Fixable by a trivial patch (optional).
- **`Wander` rationale** — add a one-line comment that 5% `head_home` matches NER's random policy vs 3% in `tests/Sim`. Fixable by trivial patch (optional).
- **Trailer survival (informational)** — `main` 1/30; ensure sprint squash message carries `Projex:` trailer. Not actionable now.

### Positive
- Strict, typo-safe config reader; deterministic output; lint identity proven on three inputs including NER.
- Executor claim of V1–V14 fully reproduced, identical numbers.

---

## Final Verdict

**Status:** Accept

**Sign-off:** Yes — all success criteria independently reproduced; only optional cosmetic findings. Working tree clean; no leftovers.
