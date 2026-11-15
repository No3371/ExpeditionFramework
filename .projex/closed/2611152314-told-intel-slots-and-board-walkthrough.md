# Walkthrough: Told Intel Slots + Internal Board (`told: true`, `IntelBoard`)

> **Execution Date:** 2026-11-16
> **Completed By:** Claude Fable 5.2 (execute agent; close agent wrote this record)
> **Source Plan:** 2611152314-told-intel-slots-and-board-plan.md
> **Parent:** 2611152314-told-intel-slots-and-board-plan.md
> **Log:** 2611152314-told-intel-slots-and-board-log.md
> **Duration:** ~15 min execution (04:57 → 05:12) + close
> **Result:** Partial Success — all deterministic criteria pass; in-game criteria (SC5, SC6 play half) unverified by design, human runs them after the whole plan set lands

---

## Summary

Intel slots take `told: true`. Core `IntelRoller` gained a 4-arg `Roll` (`toldOnDemand`), `ToldDay`, `TellCandidates`, `Tell`; game side gained `IntelBoard` (claims, pack → cards registry, cross-area tell) and the told path in `IntelCards`, plus a debug-panel claimant and docs. Unclaimed packs and packs without told slots: dice, picks, lint and sim output unchanged (re-proven at close). Game never launched: 4 in-game checks open (list below).

---

## Objectives Completion

| Objective | Status | Notes |
|-----------|--------|-------|
| SC1 `told` boolean slot field | Complete | `tests/Sim` `TestToldIntel` |
| SC2 3-arg `Roll` unchanged | Complete | tests + 6 ef-sim baselines identical; V7 literal check fails as written (see Deviations) |
| SC3 `toldOnDemand` skips told slots without dice | Complete | `TestToldIntel` dice log |
| SC4 `TellCandidates` / `Tell` rules | Complete | `TestToldIntel` |
| SC5 in-game claim / tell / held / discovery | Partial | compiles; J1–J3, J6 not run |
| SC6 debug-panel entries | Partial | compiles, wired; J2 not run |
| SC7 docs | Complete | V4 + read |
| SC8 compiles, nothing deployed, surfaces untouched | Complete | scratch build; live mod folder listing + md5 identical |

---

## Execution Detail

> Derived from `git log` / `git diff main..projex/2611152314-told-intel-slots-and-board` and the execution log. Differences from plan called out.

### Step 0: Baselines (V0)
**Planned:** EfSim build; lint + run × Starter | Showcase | SampleArea before Step 1.
**Actual:** as planned; 6 files in scratch `before/`. `git diff --stat 492d5cc HEAD -- src tests tools docs api` empty → no source drift.
**Deviation:** None.

### Step 1: `told` slot field — `325ac5d`
**Planned:** `IntelSlot.Told`, `("told", Field.Flag)`, `Bool(n, "told", false)`.
**Actual:** as planned.
**Deviation:** None.
**Files Changed (ACTUAL):**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Core/Model.cs` | Modified | Yes | +5 @ :294 — `public bool Told` + summary |
| `src/Core/Schema.cs` | Modified | Yes | `Schema.Intel` :63–64 — `("told", Field.Flag)` |
| `src/Core/PackLoader.cs` | Modified | Yes | `CompileIntel` :1177 — `Told = Bool(n, "told", false)` |
**Verification:** V1 `All checks passed`.

### Step 2: `IntelRoller` — `e39fad9`
**Planned:** 3-arg `Roll` → thunk to 4-arg; `ToldDay`, `TellCandidates`, `Tell` between `Limit` and `HowMany`.
**Actual:** plan code verbatim (+66 −1 in `src/Core/ExpeditionRun.cs`).
**Deviation:** None in code. V7's grep cannot pass against this code (Deviation 5).
**Verification:** V1; self-verified (verify-projex "Encouraged", not dispatched).

### Step 3: `TestToldIntel` — `5eee467`
**Planned:** 9 check groups in `tests/Sim/Program.cs`, wired after `TestIntelMax`.
**Actual:** +117 lines; plan checks 1–9 plus two extras (unclaimed told roll dice `100,3,100,3,100,3,2,100,3`; non-told slot id filter → none).
**Deviation:** None (extras only).
**Issues:** first insert wrote a stray `undefined` line → CS1585 → fixed before commit.
**Verification:** V1, V6.

### Steps 4 + 5: `IntelBoard` + `IntelCards` told path — `79e066f` (one commit)
**Planned:** Step 4 new `src/Game/IntelBoard.cs`; Step 5 `IntelCards` ctor `discovered`, claimed roll, `Day` / `Keep` / `TellCandidates` / `Tell` / `Show` / `Held`, counters `_held`, `_budget`; `ExpeditionLocation` passes `IsDiscovered`.
**Actual:** as planned, with Deviations 1–4.
**Files Changed (ACTUAL):**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Game/IntelBoard.cs` | Created | Yes | 72 lines — `ToldCard`; `Claim / IsClaimed / HasPack / PacksWithToldSlots / Add / Reset / Tell / Show` |
| `src/Game/IntelCards.cs` | Modified | Yes | +118 −8 — ctor :27–38; `canStart` :54; `EnsureToday` :85; `Rolled` :118–130; new members :135–229; counters :243–246 |
| `src/Game/ExpeditionLocation.cs` | Modified | Yes | :60 — `new IntelCards(area, Id, map, IsDiscovered)` |
**Verification:** V3 (0 errors, 0 warnings). Game behaviour: not run.

### Step 6: `PackRegistry.Stop` → `IntelBoard.Reset()` — `f750c90`
**Actual:** one line after `DevTools.RemoveAll();` (:38). **Deviation:** None. **Verification:** V3.

### Step 7: Debug-panel claimant — `c36a860`
**Actual:** `src/Game/DevTools.cs` +11 −1: per pack with a told slot → "Expeditions: claim intel (pack)" | "Expeditions: tell intel (pack)"; tell logs `Intel: told {slot} -> {pick} in {area}` or `Intel: nothing to tell for {pack}`. **Deviation:** None. **Verification:** V3, V6; J2 not run.

### Step 8: Docs — `a98e8bc`
**Actual:** `docs/intel.md` +38 (`told` row, `## Told slots`) | `docs/testing.md` `intel` row | `docs/csharp-api.md` 4-arg `Roll`, `TellCandidates` / `Tell` / `ToldDay`. ZoH-verbs sentence written without link (the provider plan adds it). **Deviation:** None. **Verification:** V4 1 | 1 | 1.

### Completion — `b6d50b1`
Plan → `Complete`, log finalized.

---

## Complete Change Log

> **Derived from:** `git diff --numstat main..projex/2611152314-told-intel-slots-and-board` (15 files, +533 −15).

### Files Created
| File | Purpose | Lines | In Plan? |
|------|---------|-------|----------|
| `src/Game/IntelBoard.cs` | claims + pack → cards registry + cross-area tell | 72 | Yes |
| `.projex/2611152314-told-intel-slots-and-board-log.md` | execution log | 95 | Yes (workflow) |

### Files Modified
| File | Changes | +/− | In Plan? |
|------|---------|-----|----------|
| `src/Core/ExpeditionRun.cs` | `IntelRoller`: 4-arg `Roll`, `ToldDay`, `TellCandidates`, `Tell` | +66 −1 | Yes |
| `src/Core/Model.cs` | `IntelSlot.Told` | +5 | Yes |
| `src/Core/Schema.cs` | `told` flag in `Schema.Intel` | +2 −1 | Yes |
| `src/Core/PackLoader.cs` | `CompileIntel` reads `told` | +1 | Yes |
| `src/Game/IntelCards.cs` | told path, held cards, counters | +118 −8 | Yes |
| `src/Game/ExpeditionLocation.cs` | passes `IsDiscovered` | +1 −1 | Yes |
| `src/Game/PackRegistry.cs` | `IntelBoard.Reset()` in `Stop` | +1 | Yes |
| `src/Game/DevTools.cs` | claim / tell entries | +11 −1 | Yes |
| `tests/Sim/Program.cs` | `TestToldIntel` | +117 | Yes |
| `docs/intel.md` | `told` row, `## Told slots` | +38 | Yes |
| `docs/testing.md` | `intel` row note | +1 −1 | Yes |
| `docs/csharp-api.md` | roller API note | +3 −1 | Yes |
| `.projex/2611152314-told-intel-slots-and-board-plan.md` | status, log link | +2 −1 | Yes (workflow) |

### Files Deleted
None.

### Planned But Not Changed
None. `api/`, `tools/EfSim`, `ExpeditionFramework.csproj`, `manifest.xml` untouched as required (V5).

---

## Success Criteria Verification

Close agent re-ran V1–V6 on the branch tip (`b6d50b1`) rather than relying on the log.

| Criterion | Method | Result | Evidence |
|-----------|--------|--------|----------|
| SC1 | V1 `dotnet run -c Release --project tests/Sim` | Pass | last line `All checks passed` |
| SC2 | V1 + V2 (EfSim rebuilt; lint `--strict` + run × Starter, Showcase, SampleArea vs pre-Step-1 baselines) | Pass | `cmp` exit 0 ×6. V7 literal: fail as written — see Deviation 5; diff shows the 3-arg line kept with name, parameters, return type unchanged, now `=> Roll(area, roll, scope, false);` |
| SC3 | V1 | Pass | `TestToldIntel` dice-log checks |
| SC4 | V1, V6 | Pass | `TestToldIntel` ×2 in `tests/Sim/Program.cs` |
| SC5 | V3 + J1–J3, J6 | **Unverified (in-game)** | compile ✓ (0 warnings, 0 errors); J1–J3, J6 not run |
| SC6 | V3, V6 + J2 | **Partly unverified** | `IntelBoard` refs: IntelCards 4, PackRegistry 1, DevTools 2; J2 not run |
| SC7 | V4 + J4 | Pass | 1, 1, 1; J4 self-read by executor |
| SC8 | V3, V5, V8 | Pass | build → scratch `InstallDir`: `ExpeditionFramework.dll` 273920 B; live `Mods/ExpeditionFramework` listing + per-file md5 identical before/after; `git diff --quiet main -- api tools/EfSim ExpeditionFramework.csproj manifest.xml` → 0; `git status --porcelain` empty |

**Overall:** 6/8 fully passed; SC5 and SC6 pass their deterministic half, in-game half open.

### In-game checks open (human)

Setup: build/deploy the mod the usual way (the scratch build is not deployed); a local throwaway pack, not committed, with one `told: true` slot. Debug panel → Modding category.

1. **J1 — unclaimed pack unchanged.** Do: load with the throwaway pack, do not press claim, start a day, open the map. Expect: the told slot's card appears at the day's roll like any other slot.
2. **J2 — claim, tell, budget, reload (A1).** Do: on a new day before the map/day roll, press "Expeditions: claim intel (pack)"; open the map; press "Expeditions: tell intel (pack)" repeatedly; then save/reload mid-day. Expect: told card absent after claim; each tell shows a card on the area's map card right then, without a day change, and logs `Intel: told {slot} -> {pick} in {area}`; past the slot's budget the log says `Intel: nothing to tell for {pack}`; after reload the told card is still there.
3. **J3 — next day.** Do: after J2, go to the next day (claim again if the game was restarted: claims are per session). Expect: yesterday's told card gone; told slot absent again until told.
4. **J6 — undiscovered areas never told.** Do: throwaway pack with a told slot in an area locked by `unlocked_by` (not yet cleared); claim; press tell repeatedly; then unlock the area and tell again. Expect: only discovered areas' cards are told; with only the locked area, `nothing to tell`; after unlocking, its told cards come through.

---

## Deviations from Plan

### Deviation 1: Free-card guard (Step 5)
- **Planned:** `Tell` takes the first unused card index with `First(…)`; plan comment "budget <= MaxPicks, so one is free".
- **Actual:** `IntelCards.TellCandidates` drops slots where `FreeCard(slot) < 0`; `Tell` uses `FreeCard`.
- **Reason:** on the plan's accepted late-claim path (claim after today's roll, A3) rolled picks already hold the cards while the budget is rolled afresh → `First` would throw `InvalidOperationException`.
- **Impact:** such a slot is not a candidate (`nothing to tell`). No effect when the claim precedes the roll. Confirmed in the diff (`IntelCards.cs` :186, :216–223).
- **Recommendation:** keep; accepted by the orchestrator.

### Deviation 2: `Keep` writes through `BudgetCounter(slot)`
- Iterates `area.Intel` instead of rebuilding key strings from `day.Budget` keys. Same counters; one definition of the key.

### Deviation 3: `IntelBoard` compile fixes
- Lambda param renamed (plan's `slot` → CS0136 against the deconstructed local); null `packId` guards in `HasPack` / `Tell` (null `Dictionary` key throws).

### Deviation 4: Steps 4 + 5 share one commit (`79e066f`)
- `IntelBoard.cs` references `IntelCards` members added in Step 5; a Step-4-only commit would not compile.

### Deviation 5: V7 fails as written
- **Planned:** grep count of removed 3-arg signature line = 0.
- **Actual:** 1. The plan's own After code turns the declaration into an expression-bodied thunk on the same line, so the old line shows as removed.
- **Impact:** none on SC2 — signature unchanged; `tools/EfSim` (untouched) and existing tests compile against it; baselines identical.
- **Recommendation:** a check of this kind should grep the resulting file for the signature, not the diff for removal.

### Deviation 6: V3 strengthened; verify-projex not dispatched
- Per-file md5 of the live mod folder compared as well as `ls -l`. verify-projex ("Encouraged") skipped for Steps 2, 5; self-verified.

---

## Issues Encountered

### Issue 1: CRLF working copy vs LF tool view
- **Description:** `core.autocrlf=true`; first multi-line edits found no match. **Severity:** Low. **Resolution:** CRLF-aware replace; no line-ending churn in the diff. **Prevention:** match on single lines or normalise before multi-line replace in this repo.

### Issue 2: Commit lint `type-boundary`
- `projex: steps 4-5 - …` rejected → committed as `projex: step 4 - … (step 5)`. **Severity:** Low.

---

## Key Insights

**Rationale promoted:** class summaries of `IntelBoard` (claims off the areas: load order; claims not saved), `IntelBoard.Tell` (slot first, then position), `IntelCards` comments (`Day()` rebuilt not cached; `_held` counter not a sign; free-card guard); `docs/intel.md` § Told slots.

### Gotchas / Pitfalls

1. **A budget is not a free card.** Budget (Core) and map-card indices (Game) are separate resources; they agree only when the claim precedes the day's roll. Any future caller of `IntelCards.Tell` must go through `TellCandidates`.
2. **`IntelBoard.Reset()` clears claims too.** A claimant must claim again if the framework is stopped and started within a session — matters for 2611152314-zoh-verb-provider-plan.md.
3. **`IntelBoard.Tell` does not require a claim.** On an unclaimed pack (or after a late claim) a told slot that rolled fewer picks than `MaxPicks` can still hand out a card from a freshly rolled budget. Covered by accepted risk A3; the verb provider should claim at load.

### Technical Insights
- `src/Game` is in no test project; every testable told rule sits in `IntelRoller`, with `ToldDay` passed in by the caller.
- Area ids are `pack:area`; `ToldCard.Pick` is the pool entry as stored (qualified).

---

## Recommendations

### Immediate Follow-ups
- [ ] Human: run the 4 in-game checks above after the plan set lands.
- [ ] 2611152314-zoh-verb-provider-plan.md: claim at mod load; re-claim after framework restart; link `docs/intel.md`'s ZoH-verbs sentence to zoh.md.

### Plan Improvements
- V7: grep the file for the signature instead of the diff for its removal.
- State budget vs card-index separately when a plan reasons "one is free".

---

## Related Projex Updates

| Document | Update |
|----------|--------|
| 2611152314-told-intel-slots-and-board-plan.md | Complete; walkthrough + log linked; → closed |
| 2611152314-told-intel-slots-and-board-log.md | Complete; → closed |
| 2611152305-told-intel-and-zoh-verbs-proposal.md | stays active (§ 2 open under 2611152314-zoh-verb-provider-plan.md); walkthrough linked |
| 2611152314-zoh-verb-provider-plan.md | stays active; walkthrough linked |

---

## Appendix

### Test Output (close re-run)
```
tests/Sim:            All checks passed
game assembly build:  0 warnings, 0 errors → scratch InstallDir (ExpeditionFramework.dll 273920 B)
live Mods folder:     listing + md5 cmp=0
ef-sim:               Starter | Showcase | SampleArea × lint, run: cmp=0 ×6
V4: 1 | 1 | 1   V5: 0   V6: 2 ; 4 | 1 | 2
```

### References
- Commits (squashed at close): `d4ac4f8` start | `325ac5d` step 1 | `e39fad9` step 2 | `5eee467` step 3 | `79e066f` steps 4–5 | `f750c90` step 6 | `c36a860` step 7 | `a98e8bc` step 8 | `b6d50b1` complete
