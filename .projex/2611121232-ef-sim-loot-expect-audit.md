# Audit: ef-sim loot expectations + NER Gunsmith case

> **Status:** Complete
> **Author:** Claude Sonnet 5.5 (audit, sprint iteration 5)
> **Parent:** 2611121226-ef-sim-loot-expect-patch.md
> **Sprint:** 2611121010-pack-defined-sims-sprint.md — iteration 5
> **Related Projex:** 2611121215-ef-sim-loot-expect-plan.md · 2611121226-ef-sim-loot-expect-patch.md

---

## Audit Summary

**Claim:** `expect.loot` added to `ef-sim` (commit `13ed7b2`), NER `sim.json` carries two Gunsmith cases mirroring `CheckGunsmithLoot`, criterion-4 sweep green.

**Verdict:** Verified

**Assessment:** Completeness: High | Correctness: High | Quality: High | Value: High

**Top Issues:** none blocking; two Low notes (below).

## Claims vs Evidence (all re-run independently, HEAD `eca5fba`)

| Claim | Evidence | Status |
|-------|----------|--------|
| EfSim builds 0W/0E with `-warnaserror` | build output 0/0 | ✓ |
| NER `ef-sim run` exit 0, `loot: 2 case(s), 0 failed`, output sha `933fe015…` | re-run: exit 0, sha identical | ✓ |
| Default table lines unchanged | `ne_` lines sha `b9cdd053…` identical | ✓ |
| NER `sim.json` sha `65ddd3c4…` | `sha256sum` matches | ✓ |
| Sample run byte-identical `b1565178…`, exit 0 | re-run `samples/SampleArea`: identical | ✓ |
| Case bites | copy of NER: dropping `mods` → exit 1, 7 "never rolled" FAILs; bogus must_roll id → FAIL; mistyped table → `FAIL expect.loot[0]: no loot table …` | ✓ |
| Framework sim tests | `All checks passed` | ✓ |
| `lint --strict` NER and sample | both 0 error(s), 0 warning(s) | ✓ |
| Game-assembly build 0W/0E | built with `-warnaserror`, scratch InstallDir (deleted), 0/0 | ✓ |
| NER not modified by audit; no git in NER | sha unchanged after probes; scratch copy removed | ✓ |

## Fidelity to `CheckGunsmithLoot`

- Area `ne_foundry`, seed 7, 2000 rolls, the same six tables in the same order, the same seven `gsx_` ids, `gsx_` prefix: all match `NightExpeditionsReborn\tests\Sim\Program.cs`.
- Without-mod case uses content loaded with an empty mod set (C# `without`); with-mod case reloads via `Lint.Load` with `gunsmith_expansion` and fails on load errors (C# `with.Problems`). Matches.
- Differences (Low, acceptable): C# uses a `FakeBridge` with `Smg(2,2)` kit sharing the Random, ef-sim uses `KitBridge(new SimKit())`, so the random stream could differ if tables hit game-bridge rolls; both pass with large margin (2000 rolls, 7 ids). C# also adds `""` for null rolls, ef-sim skips nulls; irrelevant to the checks. Unknown table is a failure in ef-sim (C# would silently get null).

## Source Hygiene

Scope: `git show 13ed7b2` excluding `.projex`. One rationale comment on `CheckLoot` (states what and why reload is needed); no projex IDs, `file:ln`, or plan structure in source. No rule 1–6 violations found. Rationale promotion: README `#### Loot` documents behaviour; adequate.
Commit composition (informational): `13ed7b2` typed `feat(ef-sim)`, matches diff; carries a `Sprint:` trailer but no `Projex:` trailer (consistent with the earlier sprint commits; fix-forward only, 0 Projex trailers in the recent 30 on this branch, 4 lines matched overall from older commits — the sprint's convention differs). Not actionable.

## Findings

### Critical / Significant
- None.

### Minor
- Bridge/kit difference vs the C# check (above) — Low, no fix needed.
- Sweep game-assembly build ran in under a second (incremental); outcome 0W/0E is still valid. Low.

### Positive
- Strict parse errors located; mutation probes behave as pinned; output hashes reproduce exactly.

## Final Verdict

**Status:** Accept. Criterion 2 (every NER C# sim check has a data form) and criterion 4 sweep are met as of this commit. No patch needed.
