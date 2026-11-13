# Walkthrough: ef-sim Reach + Item-Id Guard

> **Execution Date:** 2026-11-14
> **Completed By:** Claude Sonnet 5.5 (execute, audit, patch, close)
> **Source Plan:** 2611140220-ef-sim-reach-and-item-guard-plan.md
> **Parent:** 2611140220-ef-sim-reach-and-item-guard-plan.md
> **Duration:** ~35 min execute + audit + patch
> **Result:** Success

---

## Summary

`ef-sim run` rows now take `game`, `days`, `intel`, `expect`, so a pack test can see the reputation, day and intel the game gives it. A misspelled game item id now gives nothing and logs one warning per id instead of a blank "unknown" item. Old rows are byte-identical; audit verdict Accept; audit M1 fixed by a patch.

---

## Objectives Completion

| Objective | Status | Notes |
|---|---|---|
| A1 per-row `game` overlay | Complete | `SimBridge.With` copies every field; row `rep.F`/`rep_exact.F` drops both file spellings |
| A2 `days` (day +1/night, rep carries) | Complete | `SimWorld` shared across nights; `GameValue("day")` reads it |
| A3 `intel` (nightly roll) | Complete | Own `Random(seed*7919+1)`; never draws from the row's random |
| A4 row `expect` (`rep.F`, `rep_exact.F`, `intel.SLOT`) | Complete | Strict parse; unknown faction/slot exit 2 |
| B1 item-id guard | Complete (code) | One `ItemSpawner.Spawn` call, in `ItemIds.cs`; not exercised in game |
| F (EF docs) | Complete | `testing.md`, `intel.md`, `game.md`, `items.md`, `getting-started.md` |

---

## Execution Detail

Execution followed the plan step for step (Steps 0-7); the log has per-step action/result. Only departures are in Deviations.

### Step 0: Worktree + baseline
Baselines saved outside the repo (NER and Showcase `--runs 4000`, plus Showcase default counts). NER passed; Showcase `--runs 4000` already failed on `runs[1]` memory (Deviation 1).

### Steps 1-3: SimConfig, KitBridge, PackRun
**Files (ACTUAL, all planned):** `tools/EfSim/SimConfig.cs` (+107/-, `SimRow` keys, `SimGame`, `SimRowCheck`, `SimBridge.With`, shared `ReadGame`/`ReadRange`, `RowExpect`) | `tools/EfSim/KitBridge.cs` (+12, `SimWorld` optional ctor arg) | `tools/EfSim/PackRun.cs` (+72, per-row settings, world, `RollIntel`, `CheckRow`, intel-slot validation).
**Verification:** `dotnet build -c Release tools/EfSim` 0 warnings, 0 errors; NER `--runs 4000` stdout `cmp`-identical to baseline.

### Step 4: Showcase row
`docs/examples/Showcase/sim.json`: one appended row using `days`, `intel`, `expect`. Negative control (drop `days`): `FAIL runs[2]: rep.black_market should be at least 26, got 25`.

### Step 5: Item-id guard
New `src/Game/ItemIds.cs` (`Known`, `Spawn(id, where)`); `GameBridge.GiveItem`, `TradeCounter.EnsureCart`, `DevTools.GiveKit` routed through it (`ModHelper.SpawnItem` gone). Compile check with `InstallDir` redirected to scratch: 0/0; game DLL timestamp unchanged.

### Step 6: Docs
Five docs edited; V9 stale-phrase grep clean.

### Step 7: Gates
V1-V9 run; see Success Criteria.

---

## Complete Change Log

> **Derived from:** `git diff --stat main...projex/2611140220-ef-sim-reach-and-item-guard` (16 files, +365/-51, incl. projex docs).

### Files Created
| File | Purpose | In Plan? |
|---|---|---|
| `src/Game/ItemIds.cs` | Item-id guard (27 lines) | Yes |
| `.projex/closed/2611140258-showcase-memory-expect-min-patch.md` | Patch record for audit M1 | No (audit follow-up) |

### Files Modified
| File | Changes | In Plan? |
|---|---|---|
| `tools/EfSim/SimConfig.cs` | row keys, overlay, shared parsers | Yes |
| `tools/EfSim/KitBridge.cs` | optional `SimWorld` | Yes |
| `tools/EfSim/PackRun.cs` | per-row run settings, intel, checks | Yes |
| `src/Game/GameBridge.cs` | `GiveItem` via guard | Yes |
| `src/Game/TradeCounter.cs` | `EnsureCart` via guard | Yes |
| `src/Game/DevTools.cs` | `GiveKit` via guard | Yes |
| `docs/examples/Showcase/sim.json` | +1 row; row 2 `visits` -> `{ "min": 203 }` (patch) | Row yes; patch no |
| `docs/testing.md` | row keys, `## Nights in a row`, check list; Memory example mirrors Showcase (patch) | Yes |
| `docs/intel.md`, `docs/game.md`, `docs/items.md`, `docs/getting-started.md` | new behaviour | Yes |

### Files Deleted
None.

### Planned But Not Changed
None.

---

## Success Criteria Verification

| Criterion | Method | Result | Evidence |
|---|---|---|---|
| SC1 NER `--runs 4000` byte-identical | `cmp` vs baseline; audit re-ran base vs branch | Pass | exit 0; audit identical (0/0) |
| SC2 Showcase `--runs 4000` | old lines identical + new row; literal "all checks passed" unattainable pre-patch | Pass (after patch) | Deviation 1; patch `26bd86f`: default and `--runs 4000` both `all checks passed` |
| SC3 lint `--strict` 0/0 x3 | ran NER, Starter, Showcase | Pass | `0 error(s), 0 warning(s)` each |
| SC4 `tests/Sim` | run | Pass | `All checks passed` |
| SC5 strict parse | 12 probes on scratch copies | Pass | exit 2 with key-naming message each |
| SC6 `days` carries | Showcase row + negative control | Pass | with 62 vs bound 26 (audit); without: 25 |
| SC7 `intel` rolls | Showcase `intel.fence >= 1` | Pass | 116 of 200 nights (audit) |
| SC8 B1 single call + compile | grep; redirected compile | Pass | only `ItemIds.cs`; 0 warnings, 0 errors |
| SC9 docs | stale-phrase grep + audit read | Pass | none stale |

**Overall:** 9/9.

---

## Deviations from Plan

1. **Showcase `--runs 4000` failed before and after the change.** Plan Step 0/V6a assumed a pass. Row 2 expected exact `memory.visits` 203 (3 + 200), `--runs` overrides to 4003. Executor used substitute gates (byte-identical table + fail list). Audit M1 -> patch `26bd86f`: expect is now `{ "min": 203 }`; both default and `--runs 4000` pass. Upper bound dropped on purpose (any `max` breaks at larger `--runs`).
2. Plan `In Progress` committed on `main` (`d00225a`); worktree base is that commit.
3. No Verify-Projex or Do-Projex sub-subagents; executed directly.
4. Single code commit (plan allowed one or two); trailer `Projex: 2611140220-ef-sim-reach-and-item-guard` (repo style).
5. Plan and log were stamped `Complete` by the execute step before audit and close (audit M3). Close re-checked both; status stands; `Completed`, `Walkthrough` fields and SC checkboxes filled; log got a Close Notes section.

---

## Issues Encountered

- Shell heredoc quoting error once; scripts moved to scratch. A `DevTools.cs` edit doubled a CR; fixed before commit. Both Low.

---

## Key Insights

**Rationale promoted:** `docs/testing.md` (Nights in a row: why intel has its own random, carry rules, `--runs` caveat) and `docs/items.md` (Item ids: guard behaviour); `ItemIds` and `RollIntel` carry in-code rationale. No further promotion needed.

### Lessons Learned

1. **Check that a gate can pass before building on it.** The plan's Showcase `--runs 4000` gate was never run before planning; it was already red. Run each gate once on the untouched base during planning.
2. **`--runs` overrides row counts.** Exact `memory` expects are run-count-bound; examples that are checked with `--runs` need `min`/`max`.

### Gotchas / Pitfalls

1. **Negative control reads the last night only.** The `days`-removed control fails only because the last night had no deal (25 vs bound 26); weaker than it looks (audit M2).

---

## Recommendations

### Immediate Follow-ups
- [ ] In-game bad-id check (human, after install): a misspelled id in `give_item` gives nothing and logs one warning.
- [ ] NER plan 2611140220-ner-rep-week-intel-sim-rows-plan.md (NER `.projex/`) may now run against EF `main`.

### Open audit findings (left, not fixed)
- **M2** Showcase new row's controls weak: `rep_exact.security <= -4050` can only fail through a bug; tighten `rep.black_market` min to ~40 (robust margin: 62 observed). Optional.
- **M3** Status stamped `Complete` before audit/close: process only. Reconciled here. Possible process fix: execute step leaves `Complete` to close for plans with an audit step.
- **M4** Low edge cases: `days` + coin `game.day` ignored silently; no committed test for strict-parse errors (`tests/Sim` compiles `Lint.cs` only); B1 untested in game.

### Future Considerations
- Committed negative-parse sample or `tests/Sim` case for row keys; B2 load-time id check; state in plans that `--runs` overrides exact `memory` expects.

### Plan Improvements
- Baseline every gate on the untouched base before writing it into success criteria.

---

## Related Projex Updates

| Document | Update |
|---|---|
| 2611140220-ef-sim-reach-and-item-guard-plan.md | Complete; walkthrough + log linked; moved to `closed/` |
| 2611140220-ef-sim-reach-and-item-guard-log.md | Complete; Close Notes added; moved to `closed/` |
| 2611140252-ef-sim-reach-and-item-guard-audit.md | Resolved by this close (Accept); moved to `closed/` |
| 2611140258-showcase-memory-expect-min-patch.md | Already in `closed/` (born closed) |
| 2611140152-ef-ner-next-steps-proposal.md (NER) | Not touched: other repo's `.projex/` |

---

## Appendix

### References
- Commits on branch: `d265455` feat(ef-sim) | `ca56873` projex: complete | `26bd86f` fix(docs) Showcase carried-memory expect | `90bde5d` projex(patch) add showcase-memory-expect-min
- Baseline for NER `--runs 4000`: executor's pre-change capture (scratch); re-compared after merge at close.
