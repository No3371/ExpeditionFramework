# Patch: ef-sim-rule-bots

> **Status:** Complete
> **Author:** Claude Sonnet 5.5 (executor)
> **Parent:** 2611121039-ef-sim-rule-bots-plan.md
> **Sprint:** 2611121010-pack-defined-sims-sprint.md — iteration 2
> **Related Projex:** 2611121039-ef-sim-rule-bots-plan.md | 2611121010-pack-defined-sims-sprint.md
> **Directive:** Execute 2611121039-ef-sim-rule-bots-plan.md (plan-execution mode)
> **Source Plan:** 2611121039-ef-sim-rule-bots-plan.md
> **Result:** Success

---

## Summary

`sim.json` gains `bots`: ordered rule lists (`when` expression + `pick` / `first_except`, optional `target: "weakest"`), reading bot-side inputs via `val.NAME`. Code commit `592f847` on `projex/sprint/2611121010-pack-defined-sims`. Plan code applied verbatim; no deviation.

---

## Changes

**`tools/EfSim/RuleBot.cs`** (Created) — `BotRule`, `RuleBot` (`Named` val resolver, `Choose`, `Find`, `Weakest`).
**`tools/EfSim/KitBridge.cs`** (Modified) — `HasGun`, `Loaded`, `ShotsLeft`.
**`tools/EfSim/SimConfig.cs`** (Modified) — `RandomBot` const, `Bots` dictionary, `bots` key, `Bot(...)` strict parser, row-bot validation.
**`tools/EfSim/PackRun.cs`** (Modified) — rule-bot dispatch, no-pick `FAIL`, never-picked `WARN`; `random` path unchanged.
**`samples/SampleArea/sim.json`** (Modified) — `bots.cautious` (11 rules) + third row.
**`README.md`** (Modified) — § ef-sim run: three text edits + `#### Bots` section.

---

## Verification

| ID | Result |
|----|--------|
| Step 0 baseline | two `random` rows as expected |
| V1 | exit=0 (`-warnaserror`, `--no-incremental`) |
| V2 | exit=0; 1 cautious row; 0 `never picked`; `all checks passed` |
| V2b | `flooded_cellar     everything   cautious   2500     1.8%        54.5%      45.5%` exact |
| V3 | IDENTICAL |
| V4 | SAME |
| V5 | all 8 errors exact, exit=2 each |
| V6 | WARN + 2 FAIL lines + `2 check(s) failed`, exit=1 |
| V7 | exit=0; 0 ERROR; `all checks passed` |
| V8 | `All checks passed`, exit=0 |
| V9 | sample and NER lint `--strict`: 0 error(s), 0 warning(s), exit=0 |
| V10 | 0 Warning(s), 0 Error(s) |
| V11 | counts `1 3 2 1 1 1 1 1`; `is the only bot` = 0 |
| V12 | clean after doc commit |
| V13 | 0 files changed in src, api, tests, csproj |

**Status:** PASS

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611121039-ef-sim-rule-bots-plan.md | Source plan | Status → Complete; moved to `closed/` |

---

## Notes

- Patch doc id `2611121100` matches the code commit's `Projex:` trailer (the commit was stamped before the doc scaffold ran).
- Judgment check (mapping table vs `Plan`): fixture follows `Plan`'s branch order; V7 parses it. Not independently re-derived from NER source.
- NER untouched; V9 only linted it.
