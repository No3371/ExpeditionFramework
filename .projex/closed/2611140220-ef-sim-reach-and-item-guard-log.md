# Execution Log: ef-sim Reach + Item-Id Guard

> **Status:** Complete
> **Started:** 20261114 02:25
> **Repo Root:** E:/SteamLibrary/steamapps/common/Probably Stolen/Modding/ExpeditionFramework
> **Plan File:** .projex/2611140220-ef-sim-reach-and-item-guard-plan.md
> **Parent:** 2611140220-ef-sim-reach-and-item-guard-plan.md
> **Base Branch:** main
> **Worktree Path:** ExpeditionFramework/.projexwt/2611140220-ef-sim-reach-and-item-guard
> **Executor:** Claude Sonnet 5.5

## Pre-Check Results

```
PASS  Plan is committed (db66dc3 projex(plan): add ef-sim-reach-and-item-guard)
PASS  Working tree is clean
PRE-CHECK PASSED
```

Plan status set to `In Progress` on `main` as `d00225a` before the worktree was cut (the worktree branches from that commit).

## Steps

### [20261114 02:28] - Step 0: Worktree + Baseline
**Action:** `git status --porcelain` on `main`: clean. `projex-worktree.ps1` created the worktree and branch `projex/2611140220-ef-sim-reach-and-item-guard`. Baselines from the untouched worktree: NER `run --runs 4000`; Showcase `run --runs 4000`; also Showcase `run` at default row counts.
**Result:** NER: exit 0, `all checks passed`. Showcase with `--runs 4000`: exit 1, `FAIL runs[1]: memory.visits should be 203, got 4003` (pre-existing, see Deviations 1). Showcase at default counts: exit 0, `all checks passed`.
**Status:** Success (with Deviation 1)

### [20261114 02:33] - Steps 1-3: SimConfig, KitBridge, PackRun
**Action:** Implemented as planned: `SimRow` keys `game|days|intel|expect`, `SimGame`, `SimRowCheck`, `SimBridge.With`, shared `ReadGame`/`ReadRange`, `RowExpect`; `SimWorld` and optional `world` ctor argument in `KitBridge`; `PackRun` per-row settings, intel-slot validation, `RowResult`, `CheckRow`, `Play` with own intel random + world day, `RollIntel`.
**Result:** `dotnet build -c Release tools/EfSim`: 0 warnings, 0 errors (also `--no-incremental`). NER `run --runs 4000` stdout byte-identical to baseline (`cmp` exit 0). Showcase `--runs 4000` before/after identical; Showcase default counts before/after identical (before Step 4).
**Status:** Success. Verify-Projex (encouraged) not used: byte-identity checks serve as the independent evidence.

### [20261114 02:36] - Step 4: Showcase row
**Action:** Appended the plan's row to `docs/examples/Showcase/sim.json` (comma added to previous row).
**Result:** Default counts: exit 0, 3 table lines (first 2 identical to before), `all checks passed`. `--runs 4000`: only `runs[1]` memory fail (pre-existing); new row bounds hold. Row `intel.fence >= 1`, `rep.black_market >= 26`, `rep_exact.security <= -4050` all pass. Negative control (`days` removed, `--runs 4000`): `FAIL runs[2]: rep.black_market should be at least 26, got 25` (plus the pre-existing runs[1] line), so carry does matter. The bot-dependent bound was not loosened.
**Status:** Success

### [20261114 02:38] - Step 5: Item-id guard
**Action:** Added `src/Game/ItemIds.cs` (`Known`, `Spawn(id, where)`); `GameBridge.GiveItem`, `TradeCounter.EnsureCart`, `DevTools.GiveKit` now go through it.
**Result:** `grep ItemSpawner.Spawn src/Game`: only `ItemIds.cs` (call plus the class doc comment). `ModHelper.SpawnItem` count in `DevTools.cs`: 0. V7 compile with `GameManaged` relative override (resolved from the worktree, A-4 held) and `InstallDir` = scratch: 0 warnings, 0 errors; output copied to the scratch install dir; game `Mods/ExpeditionFramework/ExpeditionFramework.dll` timestamp unchanged (2026-11-14 02:20:31). A-1, A-2 held (compiled). No in-game test (out of scope).
**Status:** Success

### [20261114 02:40] - Step 6: Docs
**Action:** Edited `docs/testing.md` (rows paragraph, failing-check list, new `## Nights in a row`, lint-can't-see paragraph, Bridge reputation paragraph), `docs/intel.md`, `docs/game.md`, `docs/items.md`, `docs/getting-started.md` as planned.
**Result:** V9: no stale-phrase hits; `## Nights in a row` count 1. Extra grep for other "no intel" / ef-sim-intel / silent-bad-id statements in `docs/`: none stale.
**Status:** Success

### [20261114 02:41] - Step 7: Gates
**Action:** Ran V1-V9 (V9, V8 above).
**Result:**
- V1 pass. V2: NER byte-identical; Showcase 2 old lines identical, 3 `night_market` lines now.
- V3: NER, Starter, Showcase `lint --strict` each `0 error(s), 0 warning(s)`.
- V4: `tests/Sim` exit 0, `All checks passed`.
- V5 (scratch Showcase copies): `days: 1` -> exit 2 `runs[0].days must be true or false`; `rep.nobody` -> exit 2 `unknown faction 'nobody'`; `intel.fence` without `intel` -> exit 2 `needs "intel": true on the row`; `intel.nope` -> exit 2 `area night_market has no intel nope`. Extra: row `game` with bad text -> exit 2 `runs[0].game.luck must be a whole number or coin`; `intel: "yes"` -> exit 2.
- V6a: Showcase default counts exit 0 `all checks passed`. V6b: negative control fails as expected (Step 4). V6c: scratch NER copy with the week-rollover row (`days`, `game.day` 6, `rep_week >= 1`): exit 0 `all checks passed`.
- V7, V8, V9 as above.
**Status:** Success

## Deviations

1. **Showcase `--runs 4000` cannot say `all checks passed` before or after.** Showcase row 2 expects `memory.visits` 203 (3 start + 200 runs); `--runs` overrides the count, so it reads 4003. Plan Step 0 and V6a assumed a pass. Baseline and after both fail only on that line. Gates used instead: Showcase at default counts must pass and its first two table lines must match; Showcase at `--runs 4000` must produce a byte-identical table and fail list to the baseline for old rows, with no extra failure from the new row. The Showcase file was not changed to fix this (out of scope; a pre-existing example issue worth a follow-up: use a `min` range or no `--runs` in docs).
2. Plan `Status: In Progress` was committed on `main` (`d00225a`) per the workflow, so the worktree base is `d00225a`, not `db66dc3`.
3. Verify-Projex and Do-Projex sub-subagents not used; executed directly.

## Issues Encountered

- Python heredoc through the shell hit a quoting error once; scripts written to the scratchpad instead. A first `DevTools.cs` edit doubled a CR (`\r\r\n`); fixed before commit (diff shows 6 lines).

## Data Gathered

- Baselines: NER and Showcase tables unchanged by the change (cmp/diff exit 0).
- New row at 200 nights: `rep.black_market >= 26` holds; without `days` it is 25.

## Cleanup

Removed worktree `bin/`, `obj/` (root, `api`, `tests/Sim`, `tools/EfSim`) and scratch copies/installs. Worktree clean at completion.

## User Interventions

None.

## Close Notes

- Status `Complete` was stamped on plan and log by the execute step, before the audit and close. Audit (2611140252-ef-sim-reach-and-item-guard-audit.md, Accept) found nothing blocking; status stands and the plan's `Completed`/`Walkthrough` fields were filled at close.
- Audit M1 fixed by patch 2611140258-showcase-memory-expect-min-patch.md (`26bd86f`): Showcase `--runs 4000` now passes. Deviation 1 above is resolved.
- Audit M2, M3 (this note), M4 left as follow-ups; listed in the walkthrough.
