# Audit: Pack Memory — ef-sim Memory Model + Read-Never-Written Warning

> **Status:** Complete (Accept)
> **Author:** Claude Sonnet 5.5 (audit)
> **Parent:** 2611121622-pack-memory-sim-memory-patch.md
> **Sprint:** 2611121459-pack-memory-sprint.md — iteration 4
> **Related Projex:** 2611121459-pack-memory-sprint.md | 2611121613-pack-memory-sim-memory-plan.md | 2611121622-pack-memory-sim-memory-patch.md

> **Audit Date:** 2026-11-12 | **Subject:** commit `9360b37` (7 files, +214/-19) on `projex/sprint/2611121459-pack-memory`

---

## Audit Summary

**Claim:** `ef-sim run` models memory (`runs[n].memory` start / carry / expect) and loading a pack warns once per `memory.NAME` read but never written; all gates green.

**Verdict:** Verified

**Assessment:** Completeness: High | Correctness: High | Quality: High | Value: High

**Top Issues:**
1. `start` / `expect` names with spaces or `$$` are accepted (never readable by a pack) — Low.
2. Non-carry row: `expect` checks only the last run's store; undocumented (README is iteration 5) — Low.
3. Exact `expect` over a carried row breaks under `--runs N` (plan D5, already carried to iteration 5) — Low.

---

## Claims vs Evidence

| Claim | Evidence | Status | Notes |
|-------|----------|--------|-------|
| tests/Sim green | re-ran `dotnet run -c Release --project tests/Sim`: exit 0, `All checks passed`, 0 FAIL | ✓ | `TestMemoryWarnings` present, called from `Main` |
| Game-assembly build 0/0 | re-ran with `-warnaserror`, scratch `InstallDir`: 0 warnings, 0 errors | ✓ | build was incremental (0.58s) |
| lint --strict 0/0 sample + NER | re-ran both: `0 error(s), 0 warning(s)` | ✓ | |
| `ef-sim run` sample passes | re-ran: all checks passed | ✓ | |
| NER 4000-run rows identical | re-ran `run <NER> --runs 4000`; 23 `ne_` rows diff empty vs `ner-baseline-4000.txt`; "all checks passed" | ✓ | |
| Strict parse (SC1) | probes: `min`>`max` → `min is above max`; `start.x` 1.5 → `must be a whole number`; `expect.$x` 5 → `must be text` | ✓ | other four forms in the patch's V8; not re-run |
| carry/start/expect (SC2/3) | probes: carry+start 2/expect 2 passes; `expect.x:1` → `FAIL runs[0]: memory.x should be 1, got 0` | ✓ | exit path via `failures` list |
| Warning once per name (SC4) | `PackLoader.Finish` loop over `memoryReads` minus `memoryWrites`, keyed (pack, name), first read location kept | ✓ | read sites: `ValueOf`, `Template`, `TextArg`, `Expr.Compile` overload, `set_text` `value`; write sites: `Assign` (`set`/`add`), `set_text` `var` |
| Only planned files changed | `git show --stat 9360b37`: exactly the 7 planned files | ✓ | worktree clean, no untracked leftovers |

---

## Objective Verification

### Objective: ef-sim memory model (per-row seed, carry, expectation)

**Evidence:** `tools/EfSim/SimConfig.cs` (`SimMemory`, `SimMemoryCheck`, `MemoryOf`, `IsTextName`), `PackRun` (`Play`, `Run`), `KitBridge` ctor.
**Findings:**
- Actual: strict parse, `Start(pack)` seeds numbers by name and text without `$`; `Play` uses one shared store when `carry`, else a fresh seeded store per run; `Check` runs after the last run and emits `FAIL runs[R]: …`; failures appended after run problems.
- Missing: nothing against SC1-SC3.
- Quality: High. Rows without `memory` allocate a fresh `MemoryStore` per run, as before; store creation draws no randomness, so NER output is identical (verified).
**Verification:** ✓ Verified
**Issues:** see Gap Analysis.

### Objective: lint warning for memory read but never written

**Evidence:** `PackLoader` (`memoryReads`, `memoryWrites`, `NoteMemory`, `Finish`), `Builtins`, `Expr` overload.
**Findings:** warning (not problem) emitted per (pack, name) so another pack's write cannot mask it; `$` retained in the name so number/text stay separate; messages `… always reads 0` / `… always reads ""`. Real packs (sample, NER) produce zero warnings, so no false positives on existing content.
**Verification:** ✓ Verified

---

## Code/Implementation Inspection

### Core + ef-sim

**Claimed:** 7 files as planned. **Actual:** matches — Quality: High.

**Issues Found:**
- `IsTextName` rejects only `memory.` prefix, empty and bare `$`; `"a b"` and `"$$x"` are accepted (probed: sim passes). Plan SC1's "bad name" is defined in the plan as exactly those refusals, so not a deviation. Severity: Low.
- `SimMemory.Check` on a non-carry row checks only the final run's store. Severity: Low (semantics follow from "after the row's last run").

**Undocumented:** none.

**Metrics:** Readability High | Technical Debt None.

---

## Testing Validation

**Execution:** `tests/Sim` all pass, re-run by auditor. `TestMemoryWarnings` covers five warn-once names, `add`/`set`/`set_text` writes, cross-pack non-masking, number/text separation; `TestMapMemory` / `TestGameReads` packs gained writes to stay warning-free.
**Missing:** no unit test for `SimMemory` parse/check inside `tests/Sim` (covered only by the executor's out-of-repo fixture, V6–V9, which is not committed). Iteration 5's sample counter checked by `ef-sim run` will give in-repo coverage.

---

## Documentation Audit

README untouched by design (iteration 5). Carry-forward items (`--runs` caveat, warning, `memory` forms) are recorded in the plan's Notes. Doc debt: Pending — expected.

---

## Source Hygiene

**Scope:** Diff only (`git show 9360b37 -- . ':(exclude,glob)**/.projex/**'`).

| Rule | Location (symbol) | Quote | Severity |
|------|-------------------|-------|----------|
| — | — | no rule 1-5 violations found; scanned all added comment lines (no projex/ID tokens, `file:ln`, `Step N`, banners, changelog wording) | — |

**Rule 6 gaps:** none — `Expr.Compile` overload (rationale: binary signature), `PackLoader` warning-vs-problem, `PackRun.Play` carry default (independent nights), `IsTextName` (seeds a name never read) all carry rationale naming the alternative/consequence.

**Rule 6 vacuity:** none. Minor: `Builtins` `Assign` comment ("add reads its own var too, but only to change it") explains the choice but not the alternative consequence (false "read" warnings) — acceptable.

**Rationale promotion:** plan D5 (`--runs` caveat) is queued for the iteration-5 README; patch's walkthrough-equivalent lists no promotion line, but nothing load-bearing exists only in projex beyond that queued item.

**Commit composition** (informational): 1 landed code commit checked (`9360b37`) — typed subject `feat(ef-sim): …` [1/1] | type matches diff [1/1] | `Projex:` trailer [1/1]
**Violations:** None

**Trailer survival on `projex/sprint/2611121459-pack-memory`:** 7/30 of recent commits carry a `Projex:` trailer (every code-changing commit does; the rest are doc-only) — OK. Base-branch `main` not sampled; sprint history is the relevant range.

---

## Gap Analysis

### Promised But Not Delivered
None.

### Undocumented Issues
| Issue | Severity | Affects |
|-------|----------|---------|
| `start`/`expect` names with spaces or `$$` accepted silently | Low | sim.json authors (typo in `start` is silent; in `expect` it fails loudly) |

### Unhandled Edge Cases
- Exact `expect` on a carried row + `--runs N` — Impact: spurious FAIL; known, queued for README.
- Pack writing memory only from C# — Impact: false warning; no C# memory API exists.

---

## Quality Assessment

**Completeness:** High. **Correctness:** High — all behaviours probed match the plan. **Code Quality:** High, comments are rationale-bearing. **Value:** High — sample counter (iteration 5) can now be simulated and asserted.

---

## Findings

### Critical
None.

### Significant
None.

### Minor
- Add an in-repo `tests/Sim` case or committed sample covering `sim.json` memory parse/expect → iteration 5 sample does this.
- Optionally reject names that cannot be read (whitespace, `$$`) in `IsTextName`.
- README must state `--runs` caveat and non-carry `expect` = last run.

### Positive
- NER 4000-run table byte-identical; zero false-positive warnings on real packs; clean worktree; no deviations.

---

## Final Verdict

**Status:** Accept

**Overall Assessment:** Completeness High | Correctness High | Quality High | Value High

**Conditions:**
- [ ] None blocking; fold the minor README / test items into iteration 5.

**Sign-off:** Yes — all five success criteria independently verified.
