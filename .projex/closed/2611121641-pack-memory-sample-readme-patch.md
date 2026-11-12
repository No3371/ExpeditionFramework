# Patch: Pack Memory Sample Counter + README

> **Status:** Complete
> **Author:** Claude Sonnet 5.5
> **Parent:** 2611121637-pack-memory-sample-readme-plan.md
> **Sprint:** 2611121459-pack-memory-sprint.md — iteration 5
> **Related Projex:** 2611121637-pack-memory-sample-readme-plan.md | 2611121459-pack-memory-sprint.md
> **Directive:** Execute 2611121637-pack-memory-sample-readme-plan.md
> **Source Plan:** 2611121637-pack-memory-sample-readme-plan.md
> **Result:** Success

---

## Summary

Executed the whole plan as written (commit `2dad985`, trailer `Projex: 2611121641-pack-memory-sample-readme`). Sample gains a carried `memory.foreman_beaten` counter (written on Foreman defeat, read on office entry, checked by a 4th `sim.json` row). README documents memory, map reads, `game.day`/`game.clears`, the read-never-written warning, `runs[n].memory` and C#/host notes; two stale claims fixed. No code changes.

---

## Changes

### Sample pack

**Files:** `samples/SampleArea/ExpeditionFramework/main.json` | `samples/SampleArea/Localization/en.csv` | `samples/SampleArea/sim.json`
**Change Type:** Modified
**What Changed:**
- Foreman `on.defeated` + `add memory.foreman_beaten 1`, with a rationale comment.
- Office `enter`: `if memory.foreman_beaten > 0` → `say sa_foreman_grudge` (count as arg), before the fight.
- `en.csv`: + `sa_foreman_grudge` after `sa_foreman_down`.
- `sim.json`: row 4 (seed 45, 500 runs, random bot) with `memory: { start, carry: true, expect min 2 }`.

**Why:** Sprint goal needs a sample that demonstrates a carried counter `ef-sim` checks. New effects draw no random numbers, so rows 1-3 and `tests/Sim` stay identical.

### README

**File:** `README.md`
**Change Type:** Modified
**What Changed:** R1-R11 as in the plan: warnings list, `hidden_when` and `intel` rows, path heads, `game` reads, new `## Memory across nights`, C# notes, ef-sim checks/defaults/bots-`when` lines, new `#### Memory`, bridge `game` names.

**Why:** Pack authors had no memory docs; two claims (path heads, bots `when`) were stale.

---

## Verification

**Method:** Plan V1-V10 from the work-root.

**Result:**
```
V1  Outcomes: knocked_out=3742, cleared=1049, left=209; longest run 28 steps / All checks passed (exit 0)
V2  build 0 warnings, 0 errors (exit 0)
V3  lint sample_area 0/0, night_expeditions_reborn 0/0 (--strict, exit 0)
V4  rows 1-4 identical to probe; "ef-sim sample_area: all checks passed"
V5  NER 4000-run rows diff empty; "ef-sim night_expeditions_reborn: all checks passed"
V6  FAIL runs[3]: memory.foreman_beaten should be 1, got 230 (exit 1)
V7  ERROR sim.json: bots.cautious[10].when: unknown path 'memory.foreman_beaten' ... (exit 2)
V8  all ok, no MISSING/STALE
V9  no output (only the 4 files)
V10 no output
```

**Status:** PASS. Judgment checks J1-J3: README text applied as written (each claim in the plan's claims table); wording plain; sample comment present tense.

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611121637-pack-memory-sample-readme-plan.md | Source plan | Marked `[PATCHED]`, status Complete, moved to `.projex/closed/` |

---

## Notes

- CRLF files (main.json, en.csv, README.md) kept CRLF; `sim.json` is LF.
- Scratch dirs under `$TEMP` removed; bin/obj gitignored.
