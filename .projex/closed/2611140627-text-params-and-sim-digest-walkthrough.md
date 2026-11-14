# Walkthrough: Text Params for Shared Effects + ef-sim Run Digest

> **Execution Date:** 2026-11-14
> **Completed By:** Claude Opus 5.5 (execute, audit); Claude Sonnet 5.5 (audit-fix patch, close)
> **Source Plan:** 2611140627-text-params-and-sim-digest-plan.md
> **Parent:** 2611140627-text-params-and-sim-digest-plan.md
> **Duration:** ~2h (07:00–09:10 execution; audit 07:43; patch 08:00)
> **Result:** Success

---

## Summary

`ef-sim run --digest` (per-row SHA-256 over observable run results) landed and baselined before any language change; C1 text params for shared effects (`$NAME` params, `{arg.$NAME}` filled into body values, per-call-site compile) landed after. Showcase, Starter and NER give identical `lint --strict`, byte-identical `run` stdout and identical digests before vs after C1. The audit (`2611140743-text-params-and-sim-digest-audit.md`, Accept with Conditions) found the digest blind to credits/items/ground pile and a broken `packs.md` example; patch `2611140800-text-params-audit-fixes-patch.md` fixed both conditions plus all minors, and **superseded the digest baseline** (see Key Insights).

---

## Objectives Completion

| Objective | Status | Notes |
|-----------|--------|-------|
| (P) `ef-sim run --digest`, baselined before C1 | Complete | Digest later widened by the patch (credits, items, ground pile) |
| (C1) text params for shared effects, no pack data change | Complete | Numeric-only effects keep compile-once path |
| Balance proof: identical lint + run stdout + digest, 3 packs | Complete | Re-proved after patch against `digest2/` |
| Tests + docs | Complete | `TestTextParams`; `packs.md`, `expressions.md`, `text.md`, `testing.md` |

---

## Execution Detail

Derived from the log and git history (`main..projex/2611140627-text-params-and-sim-digest`). Commits: `cbc2f84` step 0, `c95f16d` step 1, `d99d283` step 3, `2e1661e` step 4, `a579ff8` step 5, `9de7421` completion, `f2001dd` + `8d50a6b` patch.

### Step 0: Preflight + Baseline Capture
**Actual:** NER at `1277ab0`, clean. Captured lint `--strict` + `run` for NER, Showcase, Starter into `c-proof\base\`; all four `cmp` against `c-baseline\` identical; `tests/Sim` green.
**Deviation:** None. **Files:** none (scratch only).

### Step 1: Digest Instrument
**Actual:** `ExpeditionRun.Logged` observe-only callback in `Log`; `MemoryStore.Numbers`/`Texts`; `KitBridge.NightLines`/`ReputationNow`; new `tools/EfSim/RunDigest.cs`; `--digest` in `PackRun.cs` (digest lines after the summary line); usage text.
**Deviation:** None.

### Step 2: Prove Digest + Capture Digest Baseline
**Actual:** plain run == base; `--digest` twice identical; digest-minus-lines == plain; counts NER 39, Showcase 3, Starter 1. Baseline to `c-proof\digest\` (now superseded).
**Deviation:** Git Bash `grep` strips CR, so V2b was re-run with `grep -U`. Tool artifact, no effect on acceptance.

### Step 3: C1 Text Params in the Loader
**Actual:** `PackLoader.cs`: `DeclareShared` param name/default checks and `ScanTextUse`; text effects register a per-call-site compiler, numeric-only keep the compile-once path; `Fill` substitution; `CompileSharedCall` text-arg resolution, cycle set; once-only uncalled-effect warning in `Finish`. `Expr.cs`: `arg.$NAME` has no number reader.
**Deviation:** none; Showcase/Starter lint byte-identical after step.

### Step 4: Tests
**Actual:** `TestTextParams()` in `tests/Sim/Program.cs`, cases 1-16; `All checks passed`.
**Deviation:** three test-side corrections, none weakening an assertion (bare-form asserts absent key + registered text key; single-use body for undeclared placeholder; message names the actual parameter).

### Step 5: Docs
**Actual:** `packs.md` "Text parameters" subsection, `expressions.md` `arg.NAME` note, `text.md` key-template paragraph, `testing.md` `--digest` + "Proving a refactor".

### Step 6: Full Proof Bar
**Actual:** lint, `run`, `run --digest` for all 3 packs byte/hash-identical to base and digest baseline; `tests/Sim` green; csproj diff empty; J1-J3 judgment checks pass.

### Post-plan: Audit fixes (patch `2611140800-text-params-audit-fixes-patch.md`, `f2001dd`, `8d50a6b`)
- Digest records `C credits`, `I id<TAB>count` (sorted), `G id` (ground, in order); `KitBridge.GroundNow` read-only view; class comment + `testing.md` state coverage and exclusions (run vars, anything the ef-sim bridge does not model).
- `packs.md` bare-form example: `$short` default `"sec"`, text explains bare form fills only first parameter.
- `Expr.cs`: `arg.$NAME` in expression fails with `arg.$NAME is text, not a number: write {arg.$NAME} ...`.
- `PackLoader.cs`: unpassed non-`a-z0-9_` default → no-op effect (one problem, at declaration); `uncalledText.Remove` ordering rationale comment; `BareTextArg` left boundary `(?<![a-z0-9_.])`.
- `tests/Sim/Program.cs`: expression-error message assertion; unpassed-bad-default test; one-param bare-form test asserts `Rep["security"] == 1`.

---

## Complete Change Log

> Derived from `git diff --numstat main..projex/2611140627-text-params-and-sim-digest` (code and docs only listed; projex docs below).

### Files Created
| File | Purpose | In Plan? |
|------|---------|----------|
| `tools/EfSim/RunDigest.cs` | SHA-256 accumulator over per-run canonical records (+48) | Yes |

### Files Modified
| File | Changes | In Plan? |
|------|---------|----------|
| `src/Core/PackLoader.cs` | text params: declare checks, scan, fill, per-site compile, guards (+118/-7) | Yes |
| `src/Core/Expr.cs` | `arg.$NAME` no number reader + helpful message (+7/-1) | Yes |
| `src/Core/ExpeditionRun.cs` | `Logged` callback (+6/-1) | Yes |
| `src/Core/Model.cs` | `MemoryStore.Numbers`/`Texts` (+4) | Yes |
| `tools/EfSim/PackRun.cs` | `--digest` flag, digest lines (+17/-4) | Yes |
| `tools/EfSim/KitBridge.cs` | night lines, reputation, ground views (+10/-1) | Yes |
| `tools/EfSim/Program.cs` | usage line (+1/-1) | Yes |
| `tests/Sim/Program.cs` | `TestTextParams` (+94) | Yes |
| `docs/packs.md`, `docs/testing.md`, `docs/text.md`, `docs/expressions.md` | text params, `--digest`, proving a refactor (+40, +26, +3, +1) | Yes |

### Projex Documents
Plan, log, audit, patch, and this walkthrough close together; parent proposal updated in place.

### Planned But Not Changed
None. No pack data, no csproj.

---

## Success Criteria Verification

| Criterion | Method | Result | Evidence |
|-----------|--------|--------|----------|
| SC1 `--digest` one line per row; plain stdout unchanged | cmp vs base, 3 packs | Pass | log Step 2/6; audit rerun |
| SC2 digest stable, RNG-neutral, baseline before C1 | two runs identical; minus-lines == plain; pre-C1 build digest == post-C1 | Pass | audit § Digest; `patch-proof.out` ("digest stable", "== plain") |
| SC3 text params work, compile-once kept | tests 1-5, Starter equivalence probes (audit E1/E2) | Pass | `tests/Sim` |
| SC4 guards | tests 6-16, lint probes | Pass | audit; patch added expression-message and bad-default tests |
| SC5 identical lint/run/digest after C1 | step 6; patch re-proof vs `digest2/` | Pass | `step6.out`, `patch-proof.out` |
| SC6 `tests/Sim` green | run | Pass | `All checks passed`, exit 0 |
| SC7 docs updated | diffs; patch fixed bare-form example and coverage wording | Pass | audit condition 2 resolved |
| SC8 no game csproj build | csproj diff empty; every `dotnet` names `tools/EfSim`/`tests/Sim` | Pass | log J1 |

**Overall:** 8/8. Final proof (post-patch) `patch-proof.out`: lint ok, run byte-identical to `c-proof\base\`, digest stable, digest-minus-lines == plain for ST/SC/NER (lines 1/3/39); credits mutation now changes the digest; `All checks passed`.

---

## Deviations from Plan

1. **CR handling in digest compare** — `grep -U`; harmless (audit verified bytewise CRLF compare).
2. **Error wording** — names the actual `$` parameter; improvement.
3. **`packs.md` sentence reworded** for V5a ("Plain parameters are numbers, ...").
4. **Digest scope widened post-audit** — credits, items, ground pile added; recommendation: none, plan complete.

---

## Issues Encountered

- **Digest blind to credits/inventory** (audit Significant 1, Medium): a `credits` `2d10` → `2d12` mutation changed neither table nor digest. Fixed by `f2001dd`; mutation now differs.
- **`packs.md` bare-form example fails to load** (audit Significant 2, Low): fixed in patch.
- **Test case 15 vacuous** w.r.t. the `Expr.cs` edit (audit): `$` already failed as `unknown path 'arg.'`; error message made helpful, test asserts message.
- **Log path typo** (`c-proofase`, a `\b` escape): corrected to `c-proof\base`.

---

## Key Insights

**Rationale promoted:** none needed (digest scope and text-param rules live in `docs/testing.md` and `docs/packs.md`).

1. **The digest baseline moved.** The new baseline is `scratchpad\c-proof\digest2\{ST,SC,NER}-digest-base.txt` and supersedes `c-proof\digest\`. Hash values changed by design (new `C`/`I`/`G` records); line counts did not (1/3/39). The NER refactor and C2 plans must compare against `digest2\`, not `digest\`.
2. **A proof tool needs its own mutation test.** The audit found the blind spot only by mutating Starter copies and diffing digests. Any future digest record should ship with a mutation case.
3. **Digest limits are documented:** it cannot see run vars or anything the ef-sim bridge does not model (`docs/testing.md`).
4. **Git Bash `grep` strips CR** of CRLF output; use `grep -U` when comparing against CRLF files.
5. **Open items not changed:** `memory is MemoryStore` guard, k^d per-site compile cost (informational in the audit).

---

## Recommendations

### Immediate Follow-ups
- [ ] NER `rep_book` refactor plan (needs C1 + digest; baseline = `digest2\`; adds the two missing-sign loc lines drafted in the parent proposal).
- [ ] C2 hit-context plan in EF (needs digest).

### Future Considerations
- C3 `event.*` parked until a second event needs 3+ resets.

### Plan Improvements
- State digest coverage as an explicit acceptance item and require a mutation check per record; the plan's SC2 proved stability, not sensitivity.

---

## Related Projex Updates

| Document | Update |
|----------|--------|
| 2611140627-text-params-and-sim-digest-plan.md | Complete; closed with walkthrough |
| 2611140627-text-params-and-sim-digest-log.md | Closed with plan |
| 2611140743-text-params-and-sim-digest-audit.md | Closed (both conditions met by patch) |
| 2611140800-text-params-audit-fixes-patch.md | Born closed; already in `closed/` |
| 2611140500-authoring-language-text-params-hit-context-proposal.md | Stays In Progress; walkthrough added to Related Projex, progress line "C1 + digest done" |

---

## Appendix

### References
- Branch `projex/2611140627-text-params-and-sim-digest`: `cbc2f84`, `c95f16d`, `d99d283`, `2e1661e`, `a579ff8`, `9de7421`, `f2001dd`, `8d50a6b`.
- Proof outputs (scratch, not committed): `c-proof\step0.out`, `step2.out`, `step6.out`, `patch-proof.out`; baselines `c-proof\base\`, `c-proof\digest2\`.
