# Execution Log: melee-weapon-facts

> **Status:** Complete
> **Started:** 20261115 01:20
> **Repo Root:** E:/SteamLibrary/steamapps/common/Probably Stolen/Modding/ExpeditionFramework
> **Plan File:** .projex/2611150108-melee-weapon-facts-plan.md
> **Parent:** 2611150108-melee-weapon-facts-plan.md
> **Base Branch:** main
> **Worktree Path:** ExpeditionFramework/.projexwt/2611150108-melee-weapon-facts

## Pre-Check Results
PASS  Plan is committed (171564b projex(plan): add melee-weapon-facts)
PASS  Working tree is clean
PRE-CHECK PASSED

## Steps

### 20261115 01:15 - Step 0: Preflight + Baselines
**Action:** Plan status set In Progress on main (fde196b). Worktree cut from fde196b. Recorded NER head/status hash, live game dll mtime (1794657202; Mods/ExpeditionFramework/ExpeditionFramework.dll 2026-11-14 19:53:22). Base tree archived to scratch ef-base; built base EfSim and worktree EfSim (both exit 0); ran tests/Sim.
**Result:** V0a: `git diff --stat a985df6 HEAD -- src api tools tests docs ExpeditionFramework` empty. V0c: `All checks passed`. Base = fde196b.
**Status:** Success

### 20261115 01:17 - Step 1: Core.Footprint + Test
**Action:** Added `src/Core/Footprint.cs` (verbatim from plan), `TestFootprint()` and its call in `tests/Sim/Program.cs`. Files on disk are CRLF; edits keep CRLF.
**Result:** V1a: tests/Sim ends `All checks passed`. V1b: `grep -c 'TestFootprint()'` = 2. Self-verified from `git diff`.
**Status:** Success

### 20261115 01:20 - Step 2: ItemReader.ReadFacts
**Action:** Edited `src/Game/ItemReader.cs` verbatim from plan (usings, `WeaponType`, `FactNames`, `TaggedTypes`, `ReadFacts` call after `ApplyStats`, `ReadFacts`, `Describe`). Compiled via the redirected build (`-p:InstallDir=<scratch>/game-install/ -p:GameManaged=... -o <scratch>/ef-build`).
**Result:** build exit 0, 0 warnings 0 errors; the built dll contains `ReadFacts`, so game members (A1) are accessible. V2b: 6 names, 1 `ReadFacts(weapon, item)` right after `ApplyStats(content);`. Live `Mods/ExpeditionFramework/ExpeditionFramework.dll` mtime still 1794657202. Self-verified from `git diff`.
**Status:** Success

### 20261115 01:24 - Step 3: Debug entry "Expeditions: log melee weapon facts"
**Action:** Edited `src/Game/DevTools.cs` (using System, `WeaponDirectories`, `AddFramework`, `LogMeleeFacts`, all verbatim from plan) and `src/Game/PackRegistry.cs` (`DevTools.AddFramework(content);` after the per-pack loop). Rebuilt with the redirected command.
**Result:** build exit 0, 0 warnings 0 errors; dll contains `LogMeleeFacts`. V4: counts 1, 1, 1. Live game dll mtime unchanged (1794657202). `content` is in scope after the loop and no early return precedes the call. Not provable offline: A2 (`DirectoryMaster.Create` side effects) and the in-game behaviour. Self-verified from `git diff`.
**Status:** Success

### 20261115 01:28 - Step 4: Docs
**Action:** Edited `docs/items.md` (weapon facts table after the capacity_bonus paragraph), `docs/testing.md` (kit weapons take the facts as props), `docs/packs.md` (debug entry), text verbatim from plan. Files stay CRLF.
**Result:** V6: each of the six names appears in items.md (>=1); `log melee weapon facts` appears in packs.md and items.md; `breadth` in testing.md = 1. Anchors `items.md#what-the-game-reports` (heading "What the game reports" exists) and `packs.md#debug-panel` (heading "Debug panel" exists). Self-verified from `git diff`.
**Status:** Success

### 20261115 01:36 - Step 5: Compile + Parity Proof + Cleanup
**Action:** V2a: redirected `dotnet build ExpeditionFramework.csproj -c Release -p:InstallDir=<scratch>/game-install/ -p:GameManaged=<game>/Probably Stolen_Data/Managed/ -o <scratch>/ef-build`. tests/Sim, EfSim build. Parity: base (`ef-base` copy of fde196b) vs new, back to back, one sim at a time: lint --strict, run --runs 2000, run --runs 2000 --digest, against the NER sprint worktree (read-only). Cleanup: removed bin/obj of root, api, tools/EfSim, tests/Sim in the worktree.
**Result:** V2a: exit 0, 0 warnings, 0 errors. V1a: `All checks passed`. EfSim build exit 0. V5a: `lint ok`, `run ok`, `digest ok` (byte-identical; lint 64 B exit 0 `0 error(s), 0 warning(s)`; run 6125 B exit 0; digest 12448 B exit 0). V5b: NER HEAD moved during the work (454ed39 at start, be469ec at the end: sprint commits d26a0c3 and be469ec at 01:27-01:28); porcelain status hash unchanged. Base and new ran back to back on the same state, which the plan accepts. V7: no output. V8: exit 0 (`main.json` unchanged). Live `Mods/ExpeditionFramework/ExpeditionFramework.dll` mtime 1794657202 = 2026-11-14 19:53:22, `.Api.dll` 19:53:21: untouched. Worktree `git status --porcelain --ignored` empty after cleanup.
**Status:** Success

## Deviations
- First parity pass wrote the new-binary outputs from an unquoted `$W` (path has a space), so `new/*.txt` held a path error (85 B). Re-ran the whole pass with quoted argv arrays, interleaved base/new, after clearing those files. The reported result is the second pass; the first pass base outputs had the same sizes (12448 / 6125 / 64 B).
- Commit 1 (Footprint step) was made without a body (step-commit lint accepts it); steps 2-4 have bodies. The branch forbids amend, so it stays.
- Plan Author line says Opus 5.5; commits carry the Sonnet 5.5 co-author line as instructed by the caller. Log timestamps are approximate (clock read at the end).

## Issues Encountered
- A background polling command hit the tool's 180 s auto-background limit twice; no effect on the results.
- The in-game checks (entry appears, 13 lines plus possibly `wrench_orange`, `kinds`/`purposes`/`price` vs predictions, A2 factory side effects) cannot be checked offline. Not done.

## Data Gathered
None offline; the game's melee shapes come from the human step in the plan's Post-Close Handoff.

## User Interventions
None.
