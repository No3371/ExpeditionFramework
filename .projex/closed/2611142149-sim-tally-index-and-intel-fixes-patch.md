# Patch: Sim Tally Index + Intel Fixes

> **Status:** Complete
> **Author:** Claude Sonnet 5.5 (patch step)
> **Parent:** 2611142136-sim-tally-and-intel-cap-audit.md
> **Directive:** Fix audit findings F1, F3, F4, F5 of the sim-tally-and-intel-cap audit; F2 only if tests/Sim can reach EfSim.
> **Source Plan:** Direct (follow-up to 2611142108-sim-tally-and-intel-cap-plan.md)
> **Result:** Success

---

## Summary

Fix-forward on branch `projex/2611142108-sim-tally-and-intel-cap` before the NER sprint consumes the tally. Code commit `f340b43`.

- **F1:** tally rows get a `runs[n]` prefix (same index as digest lines); kit column sized from longest label. Header row labelled `row`. Plain `run` table untouched (separate `string.Format` at `Run`, verified byte-identical).
- **F3:** negative lint test `intel_max: "memory.last_run.n"` must not produce the `run.*` warning (kills mutant M11). Asserts on the `run.*` text, not zero warnings: that expression does warn that pack `last_run` is not loaded.
- **F4:** `PackLoader` area intel loop: repeated slot id → problem `…intel[i].id: repeats intel id "x"; each slot in an area needs its own`. Test: second `first` slot → exactly one problem at `intel[2].id`.
- **F5:** `docs/intel.md` § Limiting the cards: floor(rooms/2) stated, `when`/`unique` layouts can lay out fewer rooms, distinct slot ids. `docs/testing.md`: example table with `runs[0]` + `row` header, rewrapped WARN paragraph, malformed `tally` errors for plain `run` too.
- **F2 skipped:** `tests/Sim` cannot compile EfSim. Tally is covered by the probe evidence (audit `aud/probe`, executor `d-proof/probe`) plus this patch's probe (`--tally` on a scratch pack: `runs[0..2]` rows aligned, WARN line).

---

## Changes

**File:** `tools/EfSim/PackRun.cs` (Modified) — `tallyRows` tuple gains `Index`; `TallyLines` builds `runs[n]` label, kit width = max(len("kit"), longest `row.Kit`), index width likewise. Comment explains why (labels repeat; digest parity).
**File:** `src/Core/PackLoader.cs` (Modified) — duplicate id problem in the area `intel` loop, comment names why (`Roll`/`Limit` key by id).
**File:** `tests/Sim/Program.cs` (Modified) — two checks appended to `TestIntelMax`.
**File:** `docs/intel.md`, `docs/testing.md` (Modified) — F5.

---

## Verification

**Method:** sequential from `tools/EfSim` and `tests/Sim`; baselines `c-proof/base` (lint, plain run) and `c-proof/digest2` (digest lines); game compile redirected.

**Result:**
```
build exit 0 (tools/EfSim)
ST/SC/NER: lint exit 0 + identical; run exit 0 + identical; digest lines identical
tests/Sim: exit 0, "All checks passed"
dotnet build -c Release ExpeditionFramework.csproj -p:InstallDir=<scratch>/d-proof/game-install/ : 0 Warning(s) 0 Error(s)
probe --tally:
row     area               kit   bot        runs  fights
runs[0] night_market       blade random       50    0.00
runs[1] night_market       blade careful      50    0.00
WARN tally: run.fights was 0 in every run
```
Evidence dir: session scratchpad `pa/` (`proof.out`, `*-lint.txt`, `*-run.txt`, `*-d.txt`, `sim2.txt`, `game-build.txt`).

**Status:** PASS

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|---|---|---|
| 2611142136-sim-tally-and-intel-cap-audit.md | Parent | F1, F3, F4, F5 fixed; F2 skipped (see Summary). Untracked until close. |
| 2611142108-sim-tally-and-intel-cap-plan.md | Plan under close | none |

---

## Notes

- Commits via `stage-n-commit` with a multi-line body need `--message-file`; a body-less message trips `body-missing`.
