# Walkthrough: Sim Tally + Area Intel Cap

> **Execution Date:** 2026-11-14
> **Completed By:** Claude Sonnet 5.5 (execute, audit, patch, close steps); plan by Claude Opus 5.5
> **Source Plan:** 2611142108-sim-tally-and-intel-cap-plan.md
> **Parent:** 2611142108-sim-tally-and-intel-cap-plan.md
> **Duration:** ~35 min execute, then audit + patch
> **Result:** Success

---

## Summary

EF gains sim.json `tally` + `ef-sim run --tally` (per-row means of run vars, no RNG, output prefix-stable) and area `intel_max` (applied in `IntelRoller.Roll` after every slot rolls, earlier slots first). Audit (2611142136-sim-tally-and-intel-cap-audit.md) verified; its fix-forward findings F1/F3/F4/F5 landed via 2611142149-sim-tally-index-and-intel-fixes-patch.md. No pack uses either key, so Starter / Showcase / NER stay byte-identical.

---

## Objectives Completion

| Objective | Status | Notes |
|---|---|---|
| Tally (`tally` key, `--tally`) | Complete | Rows carry `runs[n]` after the patch |
| Intel cap (`intel_max`) | Complete | Lint: no-intel, `run.` read, duplicate slot id (patch) |
| Tests, docs, proof bar | Complete | tests/Sim green; docs in intel/areas/testing/csharp-api |

---

## Execution Detail

Branch commits `main..projex/2611142108-sim-tally-and-intel-cap` (squashed on close):

| Commit | Content |
|---|---|
| 782c418 | step 0: baselines reconfirmed (9/9 identical, tests green) |
| 6d541a8 | step 1: `AreaDef.IntelMax`, schema key, loader + 2 warnings, `IntelRoller.Limit` |
| 77ea2d7 | step 2: `TestIntelMax` |
| 5808053 | step 3: `SimConfig.Tally`, `PackRun` tally accumulate/print, `--tally` |
| b0f88fd | step 4: docs |
| 9639358 | execution complete (log) |
| f340b43 | patch: `runs[n]` tally rows, kit width, duplicate intel id problem, tests, doc nits |
| d2ff95b | patch doc |

No deviations from the plan's design. Tally-index and dup-id handling came from the audit, not the plan.

---

## Complete Change Log

Code and docs (`git diff --stat main..HEAD`, `.projex` excluded): 12 files, +219/-13.

| File | Change |
|---|---|
| `src/Core/Model.cs`, `Schema.cs` | `IntelMax`, schema key |
| `src/Core/PackLoader.cs` | compile `intel_max`, warnings, duplicate id problem |
| `src/Core/ExpeditionRun.cs` | `Roll` applies `Limit` after the slot loop |
| `tools/EfSim/SimConfig.cs`, `PackRun.cs`, `Program.cs` | tally key, table, flag, usage |
| `tests/Sim/Program.cs` | `TestIntelMax` incl. negative regex + duplicate id |
| `docs/intel.md`, `areas.md`, `testing.md`, `csharp-api.md` | docs |

Planned but not changed: none. Not done: audit F2 (committed tally test; `tests/Sim` cannot reach EfSim, covered by probe evidence).

---

## Success Criteria Verification

| Criterion | Result | Evidence |
|---|---|---|
| SC1 tally validation | PASS | audit probes: bad names → `ERROR sim.json`, exit 2 |
| SC2 table + WARN | PASS | audit `aud/probe/t1.txt`; patch probe shows `runs[n]` rows + WARN |
| SC3 prefix, inert, no RNG | PASS | audit prefix compares; `run.Get` pure |
| SC4 cap after all slots | PASS | dice-sequence test; mutation check (11/12 killed, 1 equivalent) |
| SC5 lint | PASS | tests incl. new negative regex + dup id |
| SC6 tests | PASS | `tests/Sim` `All checks passed` (patch rerun) |
| SC7 docs | PASS | audit doc audit + patch nits |
| SC8 proof bar | PASS | patch rerun: ST/SC/NER lint, run, digest identical to `c-proof/base` / `digest2` |
| SC9 game compile | PASS | redirected `InstallDir` rebuild after patch: 0 Warning(s), 0 Error(s) |

**Overall:** 9/9.

---

## Deviations from Plan

None.

## Issues Encountered

- `stage-n-commit` `body-missing` lint on a body-less code commit; fixed with `--message-file`. Low.

---

## Key Insights

**Rationale promoted:** `docs/intel.md` § Limiting the cards (room count not known on the map, floor(rooms/2), variable layouts, distinct slot ids); `docs/testing.md` (`--tally`).

1. **Tally rows must be positional-proof.** NER repeats `area kit bot` triples; the `runs[n]` index is what lets the sprint match rows to digest lines.
2. **Negative lint test must assert on the message.** `memory.last_run.n` legitimately warns about an unloaded pack; assert absence of `run.*`.

---

## Recommendations

- NER plan: write `intel_max` as numbers (template 3, `ne_sump` 2) and order `intel` word_* then boss then rooms.
- Future: committed tally test if EfSim ever joins `tests/` (audit F2).

---

## Related Projex Updates

| Document | Update |
|---|---|
| 2611142108-sim-tally-and-intel-cap-plan.md | Complete, linked to this walkthrough |
| 2611142108-sim-tally-and-intel-cap-log.md | closed with the plan |
| 2611142136-sim-tally-and-intel-cap-audit.md | committed into `closed/` with this close (orchestrator-approved) |
| 2611142149-sim-tally-index-and-intel-fixes-patch.md | already closed |
