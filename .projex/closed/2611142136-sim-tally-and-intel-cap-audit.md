# Audit: Sim Tally + Area Intel Cap

> **Status:** Complete (Accept)
> **Author:** Claude Sonnet 5.5 (audit step)
> **Parent:** 2611142108-sim-tally-and-intel-cap-log.md
> **Related Projex:** 2611142108-sim-tally-and-intel-cap-plan.md | 2611142108-sim-tally-and-intel-cap-log.md | 2611142022-night-set-pieces-proposal.md (NER)
> **Audit Date:** 2026-11-14 | **Subject:** branch `projex/2611142108-sim-tally-and-intel-cap`, `782c418..9639358`, base `main` @ `89ee247`; NER `master` @ `0aa7964` (read-only)

---

## Audit Summary

**Claim:** EF gains `sim.json` `tally` + `ef-sim run --tally` (no RNG, output prefix-stable) and area `intel_max` (applied in `IntelRoller.Roll` after all slots roll); no key-less pack changes; SC1-SC9 pass.

**Verdict:** Verified

**Assessment:** Completeness: High | Correctness: High | Quality: High | Value: High (one usability gap in the tally table, below)

**Top Issues:**
1. Tally table has no row index; NER has 39 rows with many identical `area kit bot` labels (5x `ne_annex random0+random1+random2 random`) so rows are told apart only by position. Long kit names also overflow the `{1,-12}` column and misalign it. Same as plain `run`, but the tally is the number the NER sprint will compare.
2. `tally` has no committed regression test (tests/Sim cannot compile EfSim); proof is scratch probes only.
3. `intel_max` lint-trap regex false-positive side untested (mutation survives).

---

## Claims vs Evidence

| Claim | Evidence | Status | Notes |
|---|---|---|---|
| SC8 identity on 3 packs | Own rerun, sequential: `aud/bar.out` (ST/SC/NER lint, run, digest all `ok`, exit 0) vs `c-proof/base`, `c-proof/digest2` | ✓ | Rebuilt `tools/EfSim` (0 warn, 0 err) |
| SC6 tests pass | `aud/sim.txt` → `All checks passed`, exit 0 | ✓ | |
| SC1 tally validation | Own probes: `run.heat`, `[]`, `["a","a"]`, `["1x"]`, `"rooms"`, `[3]` all `ERROR sim.json: ...` exit 2 | ✓ | A bad `tally` also fails plain `run` (no flag) |
| SC2 table + WARN | `aud/probe/t1.txt`: header, column row, 3 rows, `WARN tally: run.nosuch was 0 in every run`; `--tally` w/o key → exit 2 | ✓ | |
| SC3 prefix, inert, no RNG | `plain.txt` == `SC-run.txt`; `head -c` prefix for plain and digest; t1==t2; `run.Get` pure (`ExpeditionRun.cs:335`) | ✓ | |
| SC4 cap after all slots, order | Code `ExpeditionRun.cs:1283,1290`; test mutation table below; dice-log test | ✓ | |
| SC5 lint | Test cases (no intel, `run.`, cut-off); own probe: template `memory.x` warns normally | ✓ | |
| SC7 docs | `docs/intel.md:56`, `areas.md:46`, `testing.md` (`--tally`, `intel.SLOT`), `csharp-api.md:135` | ✓ | |
| SC9 compile | `d-proof/game-build.txt` `0 Error(s)`; game-install dll contains `IntelMax`, built 21:29:56 after last code commit 21:24:01; live dll stat identical before/after (`1794657202 262144`, also equal now); `git diff --stat api src/Game *.csproj` empty; no removed `public|protected` line in `src/Core` | ✓ | Not rebuilt by me (instructed) |
| Template override of `intel_max` (A1) | Own probe: template 0 → fence starved; template 0 + area 9 → passes; template 9 + area 0 → starved; all lint exit 0 | ✓ | |

---

## `intel_max` Semantics

- **After all slots, list order:** `Roll` (`src/Core/ExpeditionRun.cs:1283`) runs the slot loop completely, then `Limit` (`:1290`) walks `area.Intel` in order with a cumulative `left`. Earlier slot wins; within a slot the first picks. Kept picks are always a prefix, so card indices stay dense (0..k-1).
- **Dice log unchanged:** the limit draws no dice and is evaluated after the loop. Test compares the full die sequence at cap 9 vs cap 1 (equal, non-empty). Moving the evaluation before the loop is an equivalent mutant (M12), as expected: it never rolls.
- **Game `IntelCards`** (`src/Game/IntelCards.cs:108`): calls `IntelRoller.Roll`, so honours it. Trimmed index `i >= picks.Count` → position -1 → counter 0 → `Pick` null → `canStart` false / `Show` ends an active card. `toneOverride` is set per kept index from its own pick, unaffected by tail trims. Cards are still registered for every `MaxPicks` index (`:30`), so no registration change. No other `src/Game` file touches intel. Caveats, not defects: the cap applies at the next daily roll (counters are day-keyed, `DayCounter`), so a mid-day pack update keeps the old cards; map card order is registration order, the cap never reorders what shows.
- **ef-sim `RollIntel`** (`tools/EfSim/PackRun.cs:301`): same `Roll`; `intel.SLOT` nights count after the cap (documented). Own e2e probe: `intel_max` 0 starves the `fence` expect, 9 restores it.
- **Negative / zero:** `Math.Max(0, ...)`; every slot stays a key (needed by `IntelCards.Rolled`, which indexes the result per slot).

## Tally

- **No RNG:** only `run.Get` (switch over engine vars, else dictionary read) and `Bridge.Credits/GroundCount` reads. Digest unchanged with the flag (`SC-digest-base` compare in executor evidence and my probe prefix check).
- **Prefix:** tally lines print after the digest lines (`PackRun.cs:104` build, printed after `:108`). `--tally` without key exits before any output (`PackRun.cs:32`).
- **Error paths:** SC1 list above. `WARN` only for names 0 in every run of every row; never changes exit code. A run that throws still contributes (comment says so; `run` is assigned before `try`).
- **Readable?** Header + aligned columns for short labels (`aud/probe/t1.txt`). Gaps: no `runs[n]` index (digest lines have one); label triples repeat in NER (`aud/NER-run.txt`: 5x annex random, 4x annex smg-5med careful, ...); `{1,-12}` kit column overflows for `random0+random1+random2`, shifting values off the header. See F1.

---

## Code/Implementation Inspection

### `src/Core` (ExpeditionRun, Model, PackLoader, Schema)
**Claimed/Actual:** 35 lines, matches plan. Quality High. `Model.cs` `IntelMax` null = no limit; `Schema.cs` key allowed.
**Issues:** F4 (duplicate slot ids double-walk).
**Undocumented:** none.

### `tools/EfSim`
**Actual:** `SimConfig` parse + `PackRun` accumulate/print; `Program` usage string. Quality High; `--runs` override respected (`runsOverride` carried into the row record).

---

## Testing Validation

**Execution:** `tests/Sim` all pass, own rerun. **Coverage:** SC4-SC5 in `tests/Sim/Program.cs` `TestIntelMax` (wired at `:48`). No test for tally (EfSim not in the test csproj).

**Mutation check** (scratch copy `aud/mut`, 13 variants of `Limit`/`Roll`/lint, `aud/mut.out`):

| Mutation | Result |
|---|---|
| M0 control | pass |
| M1 drop `Math.Max(0, ..)` | caught (ArgumentOutOfRange crash) |
| M2 no cumulative `left` | caught (2 fails) |
| M3 keep last picks in slot | caught |
| M4 reverse slot order | caught |
| M5 limit +1 | caught (4 fails) |
| M6 cap removed | caught |
| M7 per-slot constant limit | caught |
| M8 drop key when slot emptied | caught (KeyNotFound crash) |
| M9 no-intel warning removed | caught |
| M10 `run.` warning removed | caught |
| M11 regex lookbehind removed (flags `memory.last_run.x`-style reads) | **survives** (F3) |
| M12 evaluate limit before the roll | survives, equivalent mutant |

Weak spots: M1 and M8 are caught by a crash, not a clean assertion (still fails the run). A numeric literal `intel_max` is never rolled in the suite (only expression `memory.cap`); covered by the e2e probe, not by a committed test.

---

## Documentation Audit

**Accuracy:** Yes; template override claim confirmed by probe. **Why `intel_max` can't read `run.*`:** clear. `docs/intel.md:69` says cards roll before any run and rooms are laid out only at run begin, so `run.rooms` is unknown; names the lint warning. Prior paragraph (`:53`) already says `run.` reads 0 on the map.
**Gaps (Low):** "2 is half of 5 rooms" is floor(rooms/2) unstated; no note that layouts with `when` or `unique` pools can yield fewer rooms than the constant you wrote down (the plan's finding, not carried into the doc). `testing.md:91` WARN sentence is a ~150-column line against the file's wrapping. A malformed `tally` fails plain `run` too, not documented.

---

## Source Hygiene

**Scope:** Diff only `89ee247..9639358`, `.projex/` excluded.

| Rule | Location (symbol) | Quote | Severity |
|---|---|---|---|
| none | - | - | - |

Sweep of added comment lines for projex ids, labels (`SC`, `PU`, `Step`), `file:ln`, banners: no hits. Rule 6: `IntelRoller.Limit` names the rejected alternative ("Trimming slot by slot instead..."); `PackLoader` run-read warning names the constraint; `PackRun` tally comments state why order and purity. No gaps found. **Rule 6 gaps:** none. **Vacuity:** none. **Rationale promotion:** shipped in `docs/intel.md` § Limiting the cards (room-count finding) and `docs/testing.md`; supported by diff.

**Commit composition** (informational): 5 landed step commits; `Projex:` trailer 4/5 (`782c418` baseline-only log commit has none, doc-only `.projex` change, exempt); `9639358` completion commit `.projex`-only, exempt. Subjects are `projex: step N` ephemeral form, allowed. A typed subject (`feat(...)`) is the landing commit's job.
**Trailer survival on `main`:** 6/30 recent commits carry `Projex:`. OK; the rest are `.projex`-only doc commits.

---

## Gap Analysis

**Promised but not delivered:** none. **Undocumented issues:** F1-F4. **Edge cases:** duplicate slot ids (F4); mid-day pack change keeps the day's rolled cards (by design of the day counter).

---

## Findings

### Critical (Must Address)
None.

### Significant (Should Address)
- **F1 Tally rows indistinguishable** — `tools/EfSim/PackRun.cs:281` (`Line`), rows at `:285` — label is `area kit bot` only; NER repeats triples (5x `ne_annex random0+random1+random2 random`), kit width fixed at 12 misaligns long names. The NER sprint's "area mean over committed rows" must match rows by position. → Prefix each row with `runs[n]` (matches digest lines) and size the kit column from the longest label; or add a per-area mean line. Fix forward before the NER plan leans on it.

### Minor (Nice to Fix)
- **F2 No committed test for tally** — `tools/EfSim` is outside `tests/Sim`. Proof lives in scratch probes only. → Add a tiny probe pack or a `tests/` script that runs `--tally` against Showcase (prefix + inert key + error cases); the SC3 checks are cheap.
- **F3 Lint regex false-positive side untested** — `src/Core/PackLoader.cs:171`; M11 survives. → Add one negative case in `TestIntelMax`: `"intel_max": "memory.last_run.n"` must not warn.
- **F4 Duplicate intel slot ids** — `src/Core/ExpeditionRun.cs:1272` overwrites `result[slot.Id]`, and `Limit` (`:1290`) walks `area.Intel`, visiting the shared list twice and over-trimming. Loader has no id-uniqueness check (`PackLoader.cs` `CompileIntel`); duplicate ids are already broken (the first slot's picks are lost). → Lint problem for duplicate slot ids.
- **F5 Doc nits** — `docs/intel.md:70` say "floor(rooms/2)" and note variable layouts; wrap `docs/testing.md:91`; mention bad `tally` errors without the flag.

### Positive
- Cap applied after the roll, dice-sequence test, mutation-hardened (11 of 12 real mutants killed).
- Proof bar reproduced exactly, including NER digests; live game untouched; template-override verified.
- Comments exemplify rule 6 (name rejected alternative).

---

## Recommendations

**Immediate:** none blocking; F1 before the NER sprint plan consumes the tally.
**Future:** F2-F4.
**Process:** none.

## Final Verdict

**Status:** Accept

**Overall Assessment:** Completeness High | Correctness High | Quality High | Value High

**Conditions:** none required. Recommended: F1 fix-forward (small patch).

**Sign-off:** Yes. Own rerun of lint/run/digest on Starter, Showcase, NER and `tests/Sim` matches baselines; semantics verified in engine, game and ef-sim paths.

**Evidence:** `C:\Users\BA\AppData\Local\Temp\claude\70184e206a1ee857\057518b9-283c-4d89-bc27-9ae607bcf871\scratchpad\aud\` (`bar.out`, `*-lint.txt`, `*-run.txt`, `*-d.txt`, `sim.txt`, `probe\`, `mut.out`); executor's `d-proof\`.
