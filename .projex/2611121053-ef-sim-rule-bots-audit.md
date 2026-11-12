# Audit: ef-sim-rule-bots

> **Status:** Complete (Accepted)
> **Author:** Claude Sonnet 5.5 (audit)
> **Parent:** 2611121100-ef-sim-rule-bots-patch.md
> **Sprint:** 2611121010-pack-defined-sims-sprint.md — iteration 2
> **Related Projex:** 2611121039-ef-sim-rule-bots-plan.md | 2611121100-ef-sim-rule-bots-patch.md | 2611121010-pack-defined-sims-sprint.md
> **Audit Date:** 2026-11-12 | **Work Period:** iteration 2 | **Subject:** commits `592f847` (code), `59c1c61` (patch doc)

---

## Audit Summary

**Claim:** `sim.json` gains `bots` (rule lists in the expression language); every input NER `Plan` reads is expressible as a rule; all verification passed.

**Verdict:** Verified

**Assessment:** Completeness: High | Correctness: High | Quality: High | Value: High

**Top Issues:** none actionable. Minor: only the sample bot is exercised end-to-end; NER `careful` fixture proves parse only (by plan design).

---

## Claims vs Evidence

| Claim | Evidence | Status | Notes |
|-------|----------|--------|-------|
| Plan code applied verbatim | `RuleBot.cs` text-compared to plan block: identical; diff comments/shape match plan | ✓ | |
| V1–V13 pass | re-run independently (below) | ✓ | |
| Mapping table covers every `Plan` input | re-derived line by line vs `Program.cs` `Plan` (227-282) | ✓ | executor did not; auditor did |
| `random` rows byte-identical | `random` rows equal baseline values in plan Step 0 | ✓ | |
| Engine untouched | diff vs plan-add commit over src/api/tests/csproj: 0 files | ✓ | |

---

## Objective Verification

Re-run in the worktree (not trusting the patch doc):

| ID | Result |
|----|--------|
| V1 | `-warnaserror --no-incremental` build ok, 0 errors |
| V2/V2b | exit 0; 0 `never picked`; `all checks passed`; cautious row `1.8%  54.5%  45.5%` exact |
| V3 | two runs IDENTICAL |
| V4 | `random` rows `2.8/91.6/8.4` and `37.8/54.6/45.4` = baseline |
| V5 | all 8 error lines exact text, exit 2 |
| V6 | `WARN` + 2 `FAIL` + `2 check(s) failed`, exit 1 |
| V7 | plan's `careful` fixture (extracted verbatim from plan): exit 0, 0 ERROR |
| V8 | framework sim `All checks passed` |
| V9 | sample lint 0/0; NER lint `--strict` 0/0 |
| V10 | game assembly 0 Warning(s) 0 Error(s) (scratch InstallDir, removed) |
| V11 | README keyword counts `1 3 2 1 1 1 1 1`; `is the only bot` 0 |
| V12 | `git status --porcelain` empty (before audit doc) |
| V13 | 0 files changed in engine/test/csproj paths |

C1–C10: all met.

---

## Mapping Table vs NER `Plan` (judgment check, independently re-derived)

Read `Plan` in `NightExpeditionsReborn/tests/Sim/Program.cs` and compared each branch to the plan's `careful` fixture and table:

- **Order and conditions match:** heal (234) → combat: flee (238) → read_boss/weak_point (239) → boss_read flashbang (241) → threatened flashbang (243) → threatened+cover+coin (244) → switch_target weakest (248) → `Find(shoot…flee)` (251) → standoff: reload, feed/smokes, bribe, smoke_slip, sneak/ambush (255-261) → event chain with `??` precedence (266-269: `fit ? Find("take") ?? wait-option : null` binds as the fixture's two rules) → room: reload, search, head_home/press_on (271-281).
- **id lists identical** to `Program.cs` for event rules 14/17/18/19 and combat/standoff lists.
- **RNG parity:** `coin` last in the `&&` chain draws only when NER's `random.Next(2)` draws; same shared row RNG; flashbang rules precede it in both.
- **Room-logic booleans** (`finished`, `hurt` 55, `noMeds` 70, `noRounds` ≤2, `bossNext` override `<8` / `hp*10 < max*7`) map exactly into the single head_home `when`; `heal == null` ↔ `!val.offered.heal`.
- **Greedy delta claim verified:** differs only in flee `when` (`hp <= 5` only), 55→30, 70→40, `shots <= 2`→`<= 0`, bossNext clause dropped.
- **Fall-through claim verified:** NER returns `null` only on paths where `Perform(null)` runs; `ExpeditionRun.Perform` dereferences `option.Interaction` after `if (Over) return;` and the loop guards `!run.Over` → NRE. So rule fall-through differs from NER only on NER-crash paths. `Interaction.Label` non-null claim consistent with loader.
- **Scene coverage:** NER pack's only event `scene` override is `standoff`; `val.scene.X` compares `run.Scene` exactly as `Plan` does, so non-matching scenes reach the same room logic.

No divergence found. Table is sound.

---

## Source Hygiene

**Scope:** Diff only (`592f847~1..592f847`, `.projex` excluded)

| Rule | Location (symbol) | Quote | Severity |
|------|-------------------|-------|----------|
| none | | | |

**Rule 6 gaps:** none — fall-through-on-null, lazy `coin`, `foes_with` includes defeated foes, tie-break all carry comments.
**Rule 6 vacuity:** none.
**Rationale promotion:** README § Bots documents semantics (`foes_with`, `coin` early-stop, fall-through, WARN) — supported by diff.

**Commit composition** (informational): 1 landed code commit — typed subject `feat` [1/1] | type matches diff [1/1] | `Projex:` trailer [1/1]. `59c1c61` doc-only, exempt.
**Violations:** None.

**Trailer survival on `main`:** 1/6 reachable commits carry a trailer — not meaningful here (convention newly applied on the sprint branch, which is unmerged); not a defect of this work.

---

## Gap Analysis

**Promised but not delivered:** none.
**Unhandled edge cases:**
- Bot names longer than 8 chars shift the table's bot column — cosmetic, documented in plan Risks.
- `pick` id typos only surface as `never picked` WARN (planned).

---

## Findings

### Critical
- none

### Significant
- none

### Minor
- **Fixture checks parse, not play** — semantics of the `careful` port are verified only by reading (this audit) until iteration 3 runs NER rows. Expected; iteration 3 parity run closes it.

### Positive
- Plan pre-validated by prototype; implementation reproduces exact planned output.
- Strict located errors; deterministic; `random` stream untouched.

---

## Final Verdict

**Status:** Accept

**Overall Assessment:** Completeness High | Correctness High | Quality High | Value High

**Sign-off:** Yes — all criteria independently re-verified; mapping table re-derived against NER source with no divergence.
