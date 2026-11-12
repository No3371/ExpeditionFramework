# Audit: Pack Memory Sample Counter + README

> **Status:** Complete
> **Author:** Claude Sonnet 5.5
> **Parent:** 2611121641-pack-memory-sample-readme-patch.md
> **Sprint:** 2611121459-pack-memory-sprint.md — iteration 5
> **Related Projex:** 2611121637-pack-memory-sample-readme-plan.md | 2611121641-pack-memory-sample-readme-patch.md | 2611121459-pack-memory-sprint.md
> **Audit Date:** 2026-11-12 | **Subject:** commit `2dad985` (sample counter + README) vs plan SC1-SC5 and the sprint iteration 5 objective

---

## Audit Summary

**Claim:** Sample pack carries a `memory.foreman_beaten` counter checked by a 4th `sim.json` row; README documents memory, map reads, `game.day`/`game.clears`, the read-never-written warning, `runs[n].memory`, C#/host notes; two stale claims fixed; all gates green; no code changes.

**Verdict:** Verified

**Assessment:** Completeness: High | Correctness: High | Quality: High | Value: High

**Top Issues:** (none blocking)
1. Prose: README ef-sim `game` sentence reads "Names are written as after `game.`" — clumsy.
2. Prose: warnings sentence (README top) wraps mid-list into a short orphan line.
3. Commit subject `docs+sample:` lacks the `<type>(<scope>)` form (informational, fix forward).

---

## Claims vs Evidence

| Claim | Evidence | Status | Notes |
|---|---|---|---|
| Only 4 files changed, no code | `git show --stat 2dad985`: README.md, main.json, en.csv, sim.json | ✓ | Worktree clean before audit doc |
| SC1 sample writes + reads counter | Foreman `on.defeated` `add memory.foreman_beaten`; office `enter` `if memory.foreman_beaten > 0` → `say sa_foreman_grudge` with count arg; key in `en.csv` | ✓ | New effects draw no randomness |
| SC2 row 4 carries + expects | `runs[3]` `memory {start:1, carry:true, expect min 2}`; run passes; mutated `min` 9999 with `--runs 20` → `FAIL runs[3]: memory.foreman_beaten should be at least 9999, got 9` (counter really accumulates) | ✓ | |
| SC3 rows 1-3 and `tests/Sim` unchanged | Rows 1-3 identical to the plan's captured table; `tests/Sim`: `Outcomes: knocked_out=3742, cleared=1049, left=209; longest run 28 steps`, `All checks passed` | ✓ | |
| SC5 lint `--strict` 0/0 on sample + NER | Re-ran: both `0 error(s), 0 warning(s)` | ✓ | |
| SC5 `ef-sim run` sample + NER pass | Re-ran: `ef-sim sample_area: all checks passed`; NER 4000 runs `all checks passed` | ✓ | |
| SC5 NER 4000-run table identical | Row diff vs `ner-baseline-4000.txt`: empty | ✓ | |
| SC5 game-assembly build 0/0 | `dotnet build` with scratch `InstallDir`: 0 warnings, 0 errors | ✓ | Incremental; no source changed since iteration 4 |
| NER untouched | Only `README.md` newer than 14:00, mtime 14:50 (before sprint start 14:59); `sim.json` mtime 12:22 | ✓ | |
| Two stale claims fixed | Path heads now list `memory`; bots `when` carve-out for `memory.` | ✓ | Bot `when` compiles without a pack (plan probe) |

## README Claims vs Code

| README claim | Check | Status |
|---|---|---|
| Warning text `memory.NAME is read but never written by this pack, so it always reads 0`; `lint --strict` fails on it | Scratch copy reading `memory.ghost`: `WARN … memory.ghost is read but never written by this pack, so it always reads 0`, `1 warning(s)`; `Lint` returns 1 when strict and notes exist | ✓ |
| `unlocked_by` + `hidden_when` both required; OR idiom via `hidden_when` | `ExpeditionLocation.IsDiscovered`: hidden_when true → false; then `UnlockedBy == null` → true else `GameValues.Clears > 0` | ✓ |
| `game.clears` / `game.clears.AREA` same pack; during a run counts earlier nights | `GameReads.ClearsArea` prefixes the reading area's pack; `ExpeditionLocation.Leave` increments on `cleared` outcome or `run.cleared` > 0 | ✓ |
| `game.day` | `GameValues.Get` `"day"` → `StoreStation.GetDayCounter()`; 0-without-save relies on the iteration 3 decompile (not re-decompiled here) | ✓ (carried) |
| Intel slot `when` fails → shows nothing that day | `IntelRoller.Roll` skips the slot when `When(scope)` is false | ✓ |
| sim `memory` forms, FAIL text, errors | `SimMemory.Check` text `should be at least N, got M`; `memory.` in a name → `ERROR … leave out memory.`; `min` above `max` → `ERROR … is above max`; `start` per run vs carried per `PackRun.Play` | ✓ |
| `--runs` breaks exact carried `expect` | `PackRun` uses `runsOverride` for the row's run count | ✓ |
| ef-sim `game` names `"day"`, `"clears.AREA"` | `KitBridge.GameValue` looks up the raw name | ✓ |
| `set_text` on `memory.$rival` and `if memory.foreman_beaten >= 3` examples | Scratch copy with both effects: lint `--strict` 0/0 | ✓ |
| C#/host notes (`IBridge.Memory`, `MemoryStore`, `MapContext` optional store, `IntelRoller.Roll(AreaDef, Func<int,int>, IScope)`) | `IntelRoller.Roll` signature seen; others verified in iteration 1-2 audits | ✓ |

---

## Source Hygiene

**Scope:** `git show 2dad985` (diff only; `.projex/` excluded)

| Rule | Location (symbol) | Quote | Severity |
|------|-------------------|-------|----------|
| 6 (vacuity, light) | `main.json` → Foreman `on.defeated` | "outlasts the run: it is kept in the player's save, so the count grows night after night" | Low |

**Rule 6 gaps:** none — the one non-obvious choice (memory vs `run.`) has a comment.

**Rule 6 vacuity:** the comment says why memory persists but names no rejected alternative (`run.` would reset each run). Mild; reader can infer.

**Rationale promotion:** none needed — README § Memory across nights carries the author-facing rationale; no code rationale lives only in the projex.

**Commit composition** (informational): 1 landed non-projex commit — typed `<type>(<scope>)` subject 0/1 (`docs+sample: …`) | type matches diff 1/1 | `Projex:` trailer 1/1.
**Violations:** `2dad985` — subject is `docs+sample:` not `<type>(<scope>)`; fix forward, do not rewrite.

**Trailer survival on `main`:** 3/30 recent commits carry a `Projex:` trailer (sprint branch: 6/30; every sprint code commit carries one). Not zero; sprint not yet merged, so the channel is alive — OK.

---

## Prose Review (pack-author reader)

Reads plainly: short sentences, concrete examples, matches the surrounding style. The new `## Memory across nights` section is clear; the OR-via-`hidden_when` example is the right help.

Rough spots:
- ef-sim `game` paragraph: "Names are written as after `game.`" — reword to "Names are what follows `game.`". The sentence also landed mid-way into an already very long line.
- Warnings sentence near the top of the README: the list now wraps with an orphan short line; reflow.
- "bots can't read `memory.`" says so without a hint of what to do instead; minor.
- `--runs` note covers exact `expect`; `min` can also fail under a very small `--runs` (the sample's `min: 2` needs at least one Foreman defeat). Minor.

---

## Findings

### Critical
- none

### Significant
- none

### Minor
- **README "written as after `game.`"** → reword.
- **Orphan line in warnings sentence** → reflow.
- **`--runs` caveat** → mention small `--runs` can fail `min` too (optional).
- **Sample comment** → optionally name `run.` as the reset alternative.

### Positive
- Every claim in the plan's claims table traced to code; mutated expectation, error cases and warning probe behave as documented.
- NER baseline rows byte-identical; framework sim outcomes unchanged.

---

## Final Verdict

**Status:** Accept

**Overall Assessment:** Completeness High | Correctness High | Quality High | Value High — iteration 5 objective (sample carried counter checked by ef-sim, README, full gate set) met; every sprint goal criterion is satisfied in code.

**Conditions:** none (minor prose fixes optional)

**Sign-off:** Yes — all success criteria reproduced independently; no code changes; hygiene findings low or informational.
