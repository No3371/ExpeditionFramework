# Execution Log: Told Intel Slots + Internal Board

> **Status:** Complete
> **Started:** 20261116 04:57
> **Repo Root:** <repo-root> (ExpeditionFramework)
> **Plan File:** .projex/2611152314-told-intel-slots-and-board-plan.md
> **Parent:** 2611152314-told-intel-slots-and-board-plan.md
> **Base Branch:** main

## Pre-Check Results
```
PASS  Plan is committed (fe7aeb6 projex(plan): add told-intel and zoh-verb-provider plans and proposal)
PASS  Working tree is clean

PRE-CHECK PASSED
```

Manual validation: repo ✓ | base `main` ✓ | open questions: none | deps: none | drift: base now `fe7aeb6` → `d4ac4f8` (plan/proposal commits + status change only; plan cites `492d5cc`) — no source drift.
Task tool: none in this environment; this log is the task record.
Ephemeral branch: `projex/2611152314-told-intel-slots-and-board` (checkout mode).

## Steps
### 20261116 05:00 - Step 0: Baselines (V0)
**Action:** `dotnet build -c Release tools/EfSim`; `lint --strict` + `run` for Starter | Showcase | SampleArea → scratch `before/` (outside repo). `git diff --stat 492d5cc HEAD -- src tests tools docs api`.
**Result:** build 0 warnings 0 errors; 6 baseline files written (lint 1 line each; run 3/7/6 lines). Diff vs plan's cited base: empty → no source drift.
**Status:** Success

### 20261116 05:04 - Step 1: `told` slot field (Model, Schema, PackLoader)
**Action:** `IntelSlot.Told` + summary (Model.cs) | `("told", Field.Flag)` in `Schema.Intel` | `Told = Bool(n, "told", false)` in `CompileIntel`. As planned.
**Result:** `tests/Sim` → `All checks passed` (incl. new SC1 checks from Step 3: clean load, `told: "yes"` → one problem at `intel[1].told` `must be true or false`, default false). Self-verified via diff.
**Status:** Success
### 20261116 05:05 - Step 2: `IntelRoller` — claimed roll, `ToldDay`, `TellCandidates`, `Tell`
**Action:** 3-arg `Roll` → thunk to new 4-arg overload (`toldOnDemand` skips told slots before `When`, no dice); `ToldDay`, `TellCandidates`, `Tell` added between `Limit` and `HowMany`. Code as in plan, verbatim.
**Result:** `tests/Sim` → `All checks passed`. 3-arg signature line kept (diff removes only its `{` continuation into the thunk). Self-verified (verify-projex "Encouraged" — not dispatched: logic is plan-verbatim and covered by deterministic dice-log tests).
**Status:** Success
### 20261116 05:06 - Step 3: `TestToldIntel` in `tests/Sim`
**Action:** `TestToldIntel()` added after `TestIntelMax` and wired into the test list. Pack: pool `leads` r|r2|r3; slots `first` | `told1` (told, chance 100) | `told2` (told, counts [0,0,100], `when: memory.open >= 1`) | `last`; `intel_max: memory.cap`. Covers plan checks 1–9, plus: unclaimed told roll dice `100,3,100,3,100,3,2,100,3`; non-told slot id filter → none.
**Result:** `dotnet run -c Release --project tests/Sim | tail -n 1` → `All checks passed` (V1 ✓). First insert attempt wrote a stray `undefined` line (lost script variable) → build error CS1585 → corrected before commit.
**Status:** Success
### 20261116 05:08 - Step 4: `IntelBoard` (new)
**Action:** `src/Game/IntelBoard.cs` created: `ToldCard` | `IntelBoard.Claim / IsClaimed / HasPack / PacksWithToldSlots / Add / Reset / Tell / Show`. Plan code, with: `Add` body filled in; lambda param renamed (`s`) — plan's `slot` would collide with the deconstructed local (CS0136); null `packId` guarded in `HasPack` / `Tell` (a `Dictionary` lookup with null key throws); `ToldCard` field notes as doc comments; class summary also names the rejected saved-claims alternative; `Tell` summary carries the slot-then-position rationale.
**Result:** compiles with Step 5 (see below). Game behaviour → human judgment checks.
**Status:** Success (compile) — in-game pending

### 20261116 05:08 - Step 5: `IntelCards` — claimed roll, told state, tell, held cards
**Action:** ctor takes `Func<bool> discovered` (`ExpeditionLocation` passes `IsDiscovered`) and calls `IntelBoard.Add(this)`; `Pack`, `HasToldSlots`, `AreaIs`; `canStart` and `EnsureToday` exclude held picks; `Rolled()` uses 4-arg `Roll` with `IntelBoard.IsClaimed`, resets budget + held counters; `Dice`, `SetTone` extracted; `Day`, `Keep`, `TellCandidates`, `Tell`, `Show(ToldCard)`, `Held`, `HeldCounter`, `BudgetCounter` added; class summary extended.
**Result:** `dotnet build -c Release ExpeditionFramework.csproj -p:InstallDir=<scratch>/game-install/` → 0 errors, 0 warnings; `ExpeditionFramework.dll` (273920 B) in scratch. Live `Mods/ExpeditionFramework`: `ls -l` and per-file md5 identical before/after. Deviation: free-card guard (see Deviations). Self-verified by diff read (verify-projex "Encouraged" — not dispatched; the open risk is game behaviour, which no subagent can check).
**Status:** Success (compile) — in-game pending
### 20261116 05:09 - Step 6: `PackRegistry.Stop` → `IntelBoard.Reset()`
**Action:** one line after `DevTools.RemoveAll();`.
**Result:** in the same scratch build: 0 errors.
**Status:** Success
### 20261116 05:09 - Step 7: Debug-panel claimant (`DevTools`)
**Action:** `DevTools.Add`: per pack with a told slot → "Expeditions: claim intel (pack)" + "Expeditions: tell intel (pack)" (logs `Intel: told {slot} -> {pick} in {area}` or `Intel: nothing to tell for {pack}`). Class summary names the new entries.
**Result:** in the same scratch build: 0 errors. A1 (mid-day `AfterhourEvents.Start` shows the card) unverified — needs the game.
**Status:** Success (compile) — in-game pending
### 20261116 05:10 - Step 8: Docs
**Action:** `docs/intel.md`: `told` row in slot table + new `## Told slots` (after "When it is rolled"): claiming, per-tell roll, budget = slot's dice, `when` at tell time, `intel_max` counts showing cards, one id once a day, undiscovered areas never told, nothing hidden / nothing carries over, late claim, ef-sim, debug-panel stand-in, ZoH verbs sentence without link. `docs/testing.md` `intel` row: told slots roll with the rest. `docs/csharp-api.md`: 4-arg `Roll`, `TellCandidates` / `Tell` / `ToldDay`.
**Result:** V4: `## Told slots` 1 | `told` in testing.md 1 | `TellCandidates` in csharp-api.md 1.
**Status:** Success
### 20261116 05:12 - Completion: full verification
**Action:** Deterministic suite V1–V8 on the finished branch; spec compliance vs 2611152305-told-intel-and-zoh-verbs-proposal.md § 1 via the plan's SC list; quality read of the whole diff; cleanup.
**Result:**
- V1 ✓ `All checks passed` (after all steps)
- V2 ✓ EfSim rebuilt (0 errors); lint + run × Starter | Showcase | SampleArea: `cmp` exit 0 ×6 vs pre-Step-1 baselines
- V3 ✓ game assembly build → scratch `InstallDir`: 0 errors 0 warnings, `ExpeditionFramework.dll` present; live `Mods/ExpeditionFramework` listing + per-file md5 unchanged
- V4 ✓ 1 | 1 | 1
- V5 ✓ `git diff --quiet main -- api tools/EfSim ExpeditionFramework.csproj manifest.xml` → 0
- V6 ✓ `TestToldIntel` ×2; `IntelBoard` in IntelCards 4 | PackRegistry 1 | DevTools 2
- V7 ✗ literal (returns 1, expected 0) — see Deviations; intent ✓ (3-arg `Roll` still declared, same parameters; untouched `tools/EfSim` and existing tests compile against it)
- V8 ✓ `git status --porcelain` clean after commits; only planned files + `.projex/` docs in `main..HEAD`
- SC1–SC4, SC7, SC8 ✓ by the above. SC5, SC6: compile ✓; in-game J1–J3, J6 **not run** (game never launched by agent — human's checks). J4, J5 self-read ✓.
- Cleanup: nothing started; scratch output lives outside the repo; repo `bin/` `obj/` are the projects' usual ignored output.
**Status:** Success — in-game judgment checks pending with the human

## Deviations
- **Free-card guard (Step 5):** `IntelCards.TellCandidates` drops a candidate slot with no unused map card (`FreeCard`), and `Tell` uses `FreeCard` instead of the plan's `Enumerable…First(…)`. Why: plan comment "budget <= MaxPicks, so one is free" fails on the plan's own accepted late-claim path (claim after today's roll): rolled picks already hold the cards while the budget is rolled afresh → `First` throws `InvalidOperationException` from a debug button / later a ZoH verb. Now: such a slot is not a candidate (`nothing to tell`). No effect when the claim precedes the roll.
- **`Keep` (Step 5):** iterates `area.Intel` and writes through `BudgetCounter(slot)` instead of rebuilding the key string from `day.Budget` keys — same counters, one definition of the key.
- **`IntelBoard` (Step 4):** lambda param renamed (plan code → CS0136); null `packId` guards in `HasPack` / `Tell`.
- **Steps 4 + 5 one commit** (`79e066f`): `IntelBoard.cs` references `IntelCards` members added in Step 5 — a Step-4-only commit would not compile. Steps 1–3 were implemented together, then committed one per step.
- **V7 as written cannot pass:** the plan's own After code turns the 3-arg declaration line into `… IScope scope) => Roll(area, roll, scope, false);`, so the old line shows as removed in the diff. Signature (name, parameters, return type) unchanged.
- **V3 strengthened:** per-file md5 of the live mod folder compared as well as `ls -l`.
- **verify-projex not dispatched** for Steps 2, 5 ("Encouraged", not "Required"): self-verified.

## Issues Encountered
- Source files are CRLF in the working copy (`core.autocrlf=true`) while shell tools showed LF → first multi-line edits found no match; switched to a CRLF-aware replace. No line-ending churn (`git diff --stat` shows only intended lines).
- Commit lint rejects a `projex: steps 4-5 - …` subject (`type-boundary`) → committed as `projex: step 4 - … (step 5)`.
- For 2611152314-zoh-verb-provider-plan.md: `IntelBoard.Reset()` (on `PackRegistry.Stop`) also clears claims — a claimant must claim again if the framework is stopped and started within a session.

## Data Gathered
In-game checks for the human (never run by the agent): J1 unclaimed told slot rolls with the day | J2 claim → told card absent → tell shows it at once (assumption A1), budget exhaustion logs `nothing to tell`, reload keeps the card | J3 next day told card gone | J6 undiscovered area never told. Install for these: the scratch build is not deployed; the human builds/deploys the mod as usual.

## User Interventions

Orchestrator facts (handoff): never launch the game — J1–J3, J6 returned to human as list | builds → scratch dir only.
