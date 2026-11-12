# Patch: ef-sim-bridge-knobs

> **Status:** Complete
> **Author:** Claude Sonnet 5.5 (executor)
> **Parent:** 2611121143-ef-sim-bridge-knobs-plan.md
> **Sprint:** 2611121010-pack-defined-sims-sprint.md — iteration 4
> **Related Projex:** 2611121143-ef-sim-bridge-knobs-plan.md | 2611121010-pack-defined-sims-sprint.md | 2611121121-ef-sim-ner-pack-patch.md
> **Directive:** Execute 2611121143-ef-sim-bridge-knobs-plan.md (plan-execution mode)
> **Source Plan:** 2611121143-ef-sim-bridge-knobs-plan.md
> **Result:** Success

---

## Summary

Optional `bridge` object in `sim.json` now sets how `KitBridge` answers `GameValue`, trade, `TakeBack` order and `LoseCarried`. Defaults unchanged. NER `sim.json` got the `bridge` block mirroring `FakeBridge`. NER C# vs ef-sim 4000-run tables: all 46 cells identical (worst |delta| 0.0) — sprint criterion 3 met. Code per plan, verbatim; no deviations.

Commit: `5cf37ad` feat(ef-sim): bridge settings in sim.json.

---

## Changes

### Repo (committed 5cf37ad)

- `tools/EfSim/SimConfig.cs` — `SimBridge` class, `Bridge` field, `bridge` allowed top key, `ReadBridge` strict parser. Modified.
- `tools/EfSim/KitBridge.cs` — ctor takes `SimBridge`; `GameValue`, `OpenTrade`, `CloseTrade`, `TakeCart`, `TakeBack`, `LoseCarried` read it. Modified.
- `tools/EfSim/PackRun.cs` — `config.Bridge` passed via `Play` to `KitBridge`. Modified.
- `README.md` — `#### Bridge` subsection appended. Modified.

### NER (on disk, outside repo, not committed)

- `NightExpeditionsReborn/sim.json` — `bridge` block inserted before `"runs"`; CR-stripped sha256 `36c3c160…` → `7133cc99…`. Backup of pre-edit file kept in executor scratch; restored hash path documented in the plan rollback.

**Why:** NER `FakeBridge` returns luck 50 / coin `roll_luck`, opens trades with cash `-rand(0,30)`, cart `cart_goods`, takes oldest, loses `["something"]`; ef-sim hard-coded neutral values, leaving it 1–10 pts low on cleared.

---

## Verification

| ID | Result |
|----|--------|
| V1 build `-warnaserror` | 0 Warning(s), 0 Error(s) PASS |
| V2 sample run | exit=0, byte-identical to baseline `b1565178…` (SAME) PASS |
| V3 new binary, pre-edit NER data | `ba138a669a45404b88ac9680d636feb1033b88f50aac93d45c1ecbbbdfdae996` PASS |
| V4 NER sim.json | `7133cc992841c70d5ce619cd604137ac1bfb1aaa6a04ea74feb009360cc96fcf` PASS |
| V5 10 bad-input probes | all 10 expected `ERROR sim.json: …` lines, each exit=2 PASS |
| V6 NER default run | exit=0; 23 rows; packing 6/6 (1); 0 FAIL/ERROR; `all checks passed`; sha256 `0c40c20ec5aa1c15c6d885ecb03b51e02cda19e5b79faf8cf40d1272da940bae` PASS |
| V7 C# 4000 table | `8b401fcbfa0447d3ec52b570197b85df48d3b5d42a72154305329013c4b7ffdf`, 23 rows, `All checks passed`, cs exit=0 PASS |
| V8 ef-sim 4000 table | `a8258ba4047222a79d80d4a0e93b1f2f111313156ab65843a2b765cad90cfecd`, `all checks passed`, ef exit=0 PASS |
| V9 compare | worst |delta| 0.0, PARITY OK, exit 0 PASS |
| V10 framework sim | `All checks passed`, exit=0 PASS |
| V11 lint --strict | sample 0/0; NER 0/0 PASS |
| V12 game assembly | 0 Warning(s), 0 Error(s) (InstallDir scratch, removed) PASS |
| V13 README keys | `1 1 4 2 2 1` PASS |
| V14 status | clean before this doc PASS |
| V15 scope | exactly README.md + 3 tools/EfSim files PASS |
| V16 NER baseline sim | Program.cs `d4732e8e…`, Sim.csproj `26ca5458…` unchanged PASS |

### Parity compare output (verbatim)

```
rows: cs 23 ef 23
 0 ne_sump     random0+random1+random2  random   cleared  20.7 vs  20.7 (+0.0)  knocked  65.4 vs  65.4 (+0.0)
 1 ne_annex    random0+random1+random2  random   cleared   8.3 vs   8.3 (+0.0)  knocked  76.1 vs  76.1 (+0.0)
 2 ne_foundry  random0+random1+random2  random   cleared   8.0 vs   8.0 (+0.0)  knocked  79.0 vs  79.0 (+0.0)
 3 ne_sump     handmade                 careful  cleared  42.3 vs  42.3 (+0.0)  knocked   6.9 vs   6.9 (+0.0)
 4 ne_sump     smg                      careful  cleared  92.0 vs  92.0 (+0.0)  knocked   1.9 vs   1.9 (+0.0)
 5 ne_annex    handmade                 careful  cleared   3.6 vs   3.6 (+0.0)  knocked  18.2 vs  18.2 (+0.0)
 6 ne_annex    smg-5med                 careful  cleared  40.8 vs  40.8 (+0.0)  knocked  14.7 vs  14.7 (+0.0)
 7 ne_annex    shotgun                  careful  cleared  46.0 vs  46.0 (+0.0)  knocked  13.1 vs  13.1 (+0.0)
 8 ne_foundry  smg-5med                 careful  cleared  32.1 vs  32.1 (+0.0)  knocked  20.5 vs  20.5 (+0.0)
 9 ne_foundry  shotgun                  careful  cleared  39.1 vs  39.1 (+0.0)  knocked  16.9 vs  16.9 (+0.0)
10 ne_sump     knife                    careful  cleared  12.6 vs  12.6 (+0.0)  knocked  11.6 vs  11.6 (+0.0)
11 ne_annex    knife                    careful  cleared   4.5 vs   4.5 (+0.0)  knocked  11.8 vs  11.8 (+0.0)
12 ne_sump     smg-nomed                careful  cleared  90.1 vs  90.1 (+0.0)  knocked   2.2 vs   2.2 (+0.0)
13 ne_sump     handmade                 greedy   cleared  61.8 vs  61.8 (+0.0)  knocked  20.8 vs  20.8 (+0.0)
14 ne_sump     smg                      greedy   cleared  97.0 vs  97.0 (+0.0)  knocked   1.9 vs   1.9 (+0.0)
15 ne_annex    handmade                 greedy   cleared   5.4 vs   5.4 (+0.0)  knocked  46.8 vs  46.8 (+0.0)
16 ne_annex    smg-5med                 greedy   cleared  53.1 vs  53.1 (+0.0)  knocked  30.1 vs  30.1 (+0.0)
17 ne_annex    shotgun                  greedy   cleared  61.1 vs  61.1 (+0.0)  knocked  25.6 vs  25.6 (+0.0)
18 ne_foundry  smg-5med                 greedy   cleared  45.2 vs  45.2 (+0.0)  knocked  37.6 vs  37.6 (+0.0)
19 ne_foundry  shotgun                  greedy   cleared  55.8 vs  55.8 (+0.0)  knocked  29.8 vs  29.8 (+0.0)
20 ne_sump     knife                    greedy   cleared  26.5 vs  26.5 (+0.0)  knocked  41.1 vs  41.1 (+0.0)
21 ne_annex    knife                    greedy   cleared   7.5 vs   7.5 (+0.0)  knocked  49.8 vs  49.8 (+0.0)
22 ne_sump     smg-nomed                greedy   cleared  94.6 vs  94.6 (+0.0)  knocked   2.8 vs   2.8 (+0.0)
worst |delta| 0.0
PARITY OK
```

**Judgment checks:** `bridge` block maps member by member to `FakeBridge` (luck 50 / roll_luck coin; opens true; lose_cash_up_to 29; cart_goods; oldest; ["something"]). Comments in code carry no projex ids, line numbers or plan shape.

**Status:** PASS

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611121143-ef-sim-bridge-knobs-plan.md | Source plan | Marked [PATCHED]; status Complete; moved to closed |

---

## Notes

- Git Bash heredocs with certain quoting failed in the tool wrapper; edits applied via script files in scratchpad. Mixed line endings in repo (README.md, KitBridge.cs CRLF; SimConfig.cs, PackRun.cs LF) preserved per file.
- Sprint criterion 3 (C# vs ef-sim ±3 pts at 4000 runs) satisfied with exact equality; run RNG streams line up draw for draw.
