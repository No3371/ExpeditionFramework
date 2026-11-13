# Audit: ef-sim Reach + Item-Id Guard

> **Status:** Complete
> **Author:** Claude Sonnet 5.5 (audit step)
> **Parent:** 2611140220-ef-sim-reach-and-item-guard-plan.md
> **Related Projex:** 2611140220-ef-sim-reach-and-item-guard-plan.md | 2611140220-ef-sim-reach-and-item-guard-log.md | 2611140152-ef-ner-next-steps-proposal.md (NER)

> **Audit Date:** 2026-11-14 | **Subject:** branch `projex/2611140220-ef-sim-reach-and-item-guard` (`d265455` feat, `ca56873` close-out) vs base `d00225a`
> Method: built `main` base and branch from `git archive` copies in scratch; ran both side by side. No NER build, no install into the game.

---

## Audit Summary

**Claim:** ef-sim rows take `game|days|intel|expect`; rows without them run unchanged; unknown game item id gives nothing and is logged once per id (3 spawn sites); docs updated.

**Verdict:** Verified (one plan criterion met only by substitute gate, see Finding M1)

**Assessment:** Completeness: High | Correctness: High | Quality: High | Value: High

**Top Issues:**
1. SC2/V6a as written ("Showcase `--runs 4000` all checks passed") unattainable: pre-existing `runs[1]` memory.visits 203 vs 4003. Real, pre-existing, correctly handled by substitute gate; no regression. Follow-up fix to Showcase row 2 advisable.
2. Showcase new row's negative control and one bound are weaker than the log suggests (Minor).
3. Plan + log set `Complete` before audit/close (process, Minor).

No Critical or Significant findings.

---

## Claims vs Evidence

| Claim | Evidence | Status | Notes |
|---|---|---|---|
| Rows without new keys run exactly as before | `ef-sim run` before (`d00225a` build) vs after, stdout+exit compared with `cmp`: NER `--runs 4000` identical (exit 0/0); Starter `--runs 4000` identical (0/0); Showcase `--runs 4000` identical (1/1, same single FAIL) using the base Showcase file | ✓ | Showcase default-count: base and branch both exit 0, first 2 table lines identical. I did not complete default-count NER (stopped; 4000 identical covers it). Code reading agrees: `settings`=`config.Bridge`, `world`/`intelRandom`/`intel` null, `reputation` still one fresh dict per night, no extra draws |
| Deviation 1: Showcase `--runs 4000` fails on `memory.visits` 203 before and after | Reproduced on base build: `FAIL runs[1]: memory.visits should be 203, got 4003`; same on branch | ✓ | Pre-existing: 3 start + `--runs` override 4000. Plan assumption in Step 0/V6a wrong, executor's substitute gates sound |
| NER unaffected, `lint --strict` 0/0 ×3 | Ran NER, Starter, Showcase (branch Showcase incl. new row) | ✓ | each `0 error(s), 0 warning(s)` |
| `tests/Sim` exits 0 | `dotnet run -c Release --project tests/Sim` | ✓ | `All checks passed` |
| New keys parse strictly (SC5) | 12 probes on scratch Showcase copies | ✓ | `days:1`, `rep.nobody`, `intel.X` w/o `intel`, `intel.nope`, min>max, `intel.` empty slot, `memory.visits` in row expect, `game.x:1.5` all exit 2 with key-naming message |
| `days` carries rep + day (SC6) | Showcase new row passes; scratch copy without `days`: `FAIL runs[2]: rep.black_market should be at least 26, got 25`; scratch with exact bounds: black_market 62, intel.fence 116 nights (of 200) | ✓ | Margins large (62 vs 26) so bound robust. See M2 on the control's weakness |
| `intel` rolls (SC7) | `intel.fence >= 1` passes; 116 nights observed | ✓ | `IntelRoller.Roll` keys every slot (comment in Core), so `picks[slot.Id]` indexer safe |
| B1: single `ItemSpawner.Spawn` call in `src/Game`; `ModHelper.SpawnItem` gone | grep over `src`/`api`: only `ItemIds.cs`; no other `DirectoryMaster`, `CreateEmptyItem`, `SpawnFromTable` use | ✓ | 3 call sites routed: `GameBridge.GiveItem`, `TradeCounter.EnsureCart`, `DevTools.GiveKit` |
| A-1/A-2 compile (V7) | Rebuilt `ExpeditionFramework.csproj` from archive copy, `GameManaged` absolute, `InstallDir` = scratch | ✓ | 0 warnings, 0 errors; game `Mods/ExpeditionFramework/ExpeditionFramework.dll` timestamp unchanged (02:20:31) |
| Guard matches game's own check | decomp: `ItemSpawner.Spawn` = prebuilt-factory else `DirectoryMaster.Item` (dummy `CreateEmptyItem` + warning on miss); game checks `!Has<GameItem> && !preBuiltItems.ContainsKey` at its own validation site | ✓ | `Known()` is the same predicate |
| Unknown id gives nothing + reported (human decision) | `ItemIds.Spawn`: null/"" → null silently; unknown → one `ModEntry.Log.Warning` per id (static `HashSet`), returns null → `PutOnGround(null)` → null → Core `IInventory.Give` logs tone `bad` "could not give … (unknown item id, or no room on the ground)" for non-empty id | ✓ (code) | Not exercised in game (out of scope per plan, human follow-up). Empty id stays silent, as Core's comment requires. Trade cart and debug kit skip nulls (`if (item != null)`) |
| Docs match behaviour | read `docs.diff` vs code | ✓ | overlay rule, day start, `--runs` caveat, intel doc, items/getting-started wording all accurate; V9 stale phrases absent |
| Cleanup contract | worktree `git status` clean | ✓ | no `bin/obj` left |

---

## Objective Verification

- **A1 per-row `game`**: `SimBridge.With` copies every `SimBridge` field (Game, CoinGame, TradeOpens, LoseCashUpTo, Cart, TakeOldest, LoseCarried); row `rep.F`/`rep_exact.F` drops both file spellings. Probe `game: {rep.black_market: 50}` over a file with rep_exact works. ✓
- **A2 `days`**: `world.Day = firstDay + n`, `GameValue("day")` reads it after the rep branch; rep dictionary shared via `SimWorld`. ✓
- **A3 `intel`**: own `Random(seed*7919+1)`, `MapContext` over `bridge.GameValue` + the night's memory, positions mapped to pool entries as `IntelCards.Today` does (minus afterhour-event gating, stated in the comment). ✓
- **A4 `expect`**: `CheckRow` reads last night's `KitBridge.GameValue` (no draw for rep names) and the intel tally; exact/min/max wording consistent with memory expects. ✓
- **B1**: ✓ (above). **F docs**: ✓.

---

## Code/Implementation Inspection

**`tools/EfSim/SimConfig.cs`, `KitBridge.cs`, `PackRun.cs`** — matches plan nearly verbatim. Quality: High. `ReadGame`/`ReadRange` dedup done; `MemoryOf` error texts unchanged.

**`src/Game/ItemIds.cs` + 3 sites** — as planned. Quality: High.

**Undocumented / deviations from plan text:** commit trailer is `Projex: 2611140220-ef-sim-reach-and-item-guard` (no `-plan.md`) — matches the repo's existing trailer style (`2611122012-optional-cross-pack-memory`); single commit instead of the plan's two-commit option (allowed); commit subject differs from plan text. None harmful.

**Edge cases found (all Low):**
- `"days": true` with `game.day: "coin"`: `world.Day` wins silently; the coin is ignored and `firstDay` ignores coin. Not documented, no pack does it.
- A coin game name read in an intel `when` draws from the row's random (documented in `RollIntel` comment, but not in `testing.md`).
- Row `game` accepts `rep.nobody` without error (same as `bridge.game` today); only `expect` validates the faction. Consistent with prior behaviour.

---

## Testing Validation

**Coverage:** the only committed regression for the new keys is the Showcase row (checked by running `ef-sim run`); `tests/Sim` compiles `Lint.cs` only and does not touch row parsing. The 12 error-path probes (V5) were scratch-only, not committed.
**Execution:** all gates I re-ran pass as expected (above). No flakiness (seeded).
**Missing:** no committed test for strict-parse errors; no in-game B1 check.

---

## Documentation Audit

**Completeness:** Complete. **Accuracy:** Yes; the Showcase example row in `testing.md` is the committed row. **Quality:** High.

---

## Source Hygiene

**Scope:** Diff only (`d00225a..d265455`, `.projex` excluded)

| Rule | Location (symbol) | Quote | Severity |
|---|---|---|---|
| — | none found | grep of added lines for projex ids, step labels, `file:ln`, banners, `.md` → no hits | — |

**Rule 6 gaps:** none. Non-obvious points all carry rationale: `SimBridge.With` (why rep spellings are dropped), `Play` intel random (why separate), `ItemIds` doc (why not `ItemSpawner.Spawn` alone), `RollIntel` (afterhour gating difference).
**Rule 6 vacuity:** none.
**Rationale promotion:** shipped docs carry the user-facing rationale (`testing.md` Nights in a row, `items.md` Item ids). Walkthrough not yet written (close step) — check its `Rationale promoted:` line then.

**Commit composition** (informational): 1 landed code commit — typed subject (`feat(ef-sim)`) 1/1 | type matches diff 1/1 | `Projex:` trailer 1/1. Close-out commit `ca56873` is doc-only.
**Violations:** None.

**Trailer survival on `main`:** 4/30 recent commits carry a `Projex:` trailer — OK (non-zero; many recent commits are doc-only or from other sessions).

---

## Gap Analysis

### Promised But Not Delivered
| Promise | Status | Impact |
|---|---|---|
| SC2 "Showcase `--runs 4000` … all checks passed" | Not met as written (pre-existing fail on row 2); substitute gates met | Low |
| In-game B1 test | Deferred by plan | Low (human follow-up) |

### Undocumented Issues
| Issue | Severity | Affects |
|---|---|---|
| Showcase row 2 `memory.visits: 203` breaks under any `--runs` (docs for `--runs` already say use min/max) | Low | Showcase as an example; the verify gate in the plan |

### Unhandled Edge Cases
- `days` + coin `game.day` ignored silently — Impact: none today.

---

## Quality Assessment

### Completeness: High — every plan step landed.
### Correctness: High — old rows byte-identical; new keys behave; guard predicate equals the game's.
### Code Quality: High — strict parsing, shared helpers, rationale comments. Tech debt: none.
### Value Delivered: High — NER's rep/week/intel rows (next plan) can now be written against this; typo'd item ids stop yielding blank "unknown" items.

---

## Open Findings

- Downstream: NER plan 2611140220-ner-rep-week-intel-sim-rows-plan.md may use `days`, `game.day`, `rep.F`/`intel.SLOT` expects; all verified working.
- Risks: B1 runtime path (`DirectoryMaster.Has<GameItem>` at give time) only compile-checked; Likelihood of problem: Low (same call the game makes).
- Improvement: add a committed negative-parse sample or a `tests/Sim` case for row keys.

---

## Stakeholder Impact

| Stakeholder | Promised | Reality | Impact |
|---|---|---|---|
| Pack authors (NER) | Sim sees rep/day/intel | Yes, opt-in | Positive |
| Players / pack testers | Bad id gives nothing + logged | Yes (code) | Positive |
| Existing sim.json files | Unchanged | Byte-identical | Neutral |

---

## Findings

### Critical (Must Address)
- None.

### Significant (Should Address)
- None.

### Minor (Nice to Fix)
- **M1 — Showcase `--runs 4000` fails by design of row 2** (`memory.visits: 203`, expect is an exact number while `--runs` overrides count). Pre-existing; the plan's SC2/V6a/Step 0 assumed otherwise and the log recorded it honestly as Deviation 1. → Optional patch: change Showcase row 2 `"visits": 203` to `{ "min": 203 }`. `--runs 4000` gives 4003 ≥ 203 and default gives 203, so `{ "min": 203 }` passes both. Then `--runs 4000` is a clean gate for the NER chain. Outside this plan's scope; decide at close.
- **M2 — Showcase new row's controls are weaker than described.** `rep_exact.security <= -4050` can only fail through a bug (run_end never raises security), and the `days`-removed negative control reads only the *last* night's rep (25 + that night's deals), so it fails only because the last night had no deal (observed 25). Positive side is robust (62 vs bound 26, 116 intel nights vs 1). → Optional: tighten to `rep.black_market` min ~40; not required.
- **M3 — Status `Complete` set on plan and log before audit and close.** Plan stays in `.projex/` (not `closed/`); Status set by execute step. Process only; the close workflow should re-check status.
- **M4 — Low edge cases:** `days` + coin `day` ignored silently; strict-parse errors have no committed test; B1 untested in game (human follow-up already listed in plan Notes).

### Positive
- Byte-identical regression evidence for old rows; negative control actually run; compile verified with install redirected and game DLL timestamp confirmed; honest deviation reporting; comments obey Source Hygiene; guard keeps empty-id behaviour Core relies on.

---

## Recommendations

**Immediate:** none required. Optionally patch M1 before the NER plan relies on `--runs 4000` for Showcase.
**Future:** in-game bad-id check (human); committed parse-error test; B2 load-time id check.
**Process:** state in plans that `--runs` overrides exact `memory` expects when choosing gates.

---

## Final Verdict

**Status:** Accept (conditions optional)

- Completeness: High | Correctness: High | Quality: High | Value: High

**Conditions:**
- [ ] (optional) M1 patch to Showcase row 2 expect
- [ ] Human in-game check of bad id (plan follow-up)

**Sign-off:** Yes — all checkable claims reproduced; no defects found that block merge.
