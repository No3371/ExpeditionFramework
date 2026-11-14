# Execution Log: sim-tally-and-intel-cap

> **Status:** Complete
> **Started:** 20261114 21:30
> **Repo Root:** E:/SteamLibrary/steamapps/common/Probably Stolen/Modding/ExpeditionFramework
> **Plan File:** .projex/2611142108-sim-tally-and-intel-cap-plan.md
> **Parent:** 2611142108-sim-tally-and-intel-cap-plan.md
> **Base Branch:** main

## Pre-Check Results
PASS  Plan is committed (4a2c801 projex(plan): add sim-tally-and-intel-cap)
PASS  Working tree is clean
PRE-CHECK PASSED

## Steps

### [20261114 21:35] - Step 0: Preflight + Baseline Reconfirm
**Action:** HEAD 89ee247 (start-execution); `git diff --stat 77d5504 HEAD` over src/api/tools/tests/docs/ExpeditionFramework/Localization/samples/csproj empty; porcelain empty; NER `0aa7964`, NER porcelain empty; A2 grep (EF + NER) no output. Live dll stat recorded (`1794657202 262144`). Built tools/EfSim; ran lint --strict, run, run --digest for ST SC NER; compared to c-proof base and digest2. tests/Sim.
**Result:** 9 of 9 compares identical (ST/SC/NER lint, run, digest). tests/Sim: `All checks passed`. Outputs in d-proof/s0/.
**Status:** Success

### [20261114 21:50] - Step 1: `intel_max` Model, Schema, Loader, Roller
**Action:** Added `AreaDef.IntelMax`, schema key `intel_max`, `CompileArea` compile via `ValueOf` + two warnings (no intel slots; reads `run.`), `RunRead` regex, `IntelRoller.Limit` applied after the full roll with `Math.Max(0, ...)`, extended `Roll` summary. Files edited with CRLF preserved.
**Result:** `dotnet build -c Release tools/EfSim` 0 warnings, 0 errors. ST and SC `lint --strict` and `run` identical to c-proof base. Self-verified by diff review (4 files, 30 insertions, 2 deletions); no verify subagent used.
**Status:** Success

### [20261114 22:00] - Step 2: Tests `TestIntelMax`
**Action:** Added `TestIntelMax();` after `TestMapMemory();` in `Main` and the method in `tests/Sim/Program.cs`: loads clean with/without `intel_max`; cap 9 → 1,2,1; cap 2 → 1,1,0 with the kept pick equal to the first pick; cap 0 → 0,0,0 with all slots keys; cap -3 → 0,0,0; dice log identical at cap 9 and cap 1; keyless area has `IntelMax == null` and 1,2,1; lint: no-intel warning once, `run.` warning once, `1 +` one problem at `.intel_max`.
**Result:** `dotnet run -c Release --project tests/Sim | tail -n 1` = `All checks passed`; `grep -c 'TestIntelMax()'` = 2. Mutation check: disabling the `Limit` call made 5 checks fail; restored, green again.
**Status:** Success

### [20261114 22:15] - Step 3: ef-sim `tally` + `--tally`
**Action:** `SimConfig`: `Tally` list, strict `tally` key (non-empty, distinct, `^[A-Za-z_][A-Za-z0-9_]*$`). `PackRun`: `--tally` flag, `ERROR sim.json: --tally needs ...` exit 2, per-run `run.Get` sums in `Play` (after the digest add), `RowResult.Tally`, `TallyLines` helper printed after digest lines only when the load had no problems. `Program` usage line.
**Result:** Probe on a Showcase copy with `tally: [rooms, depth, deals, nosuch]` (d-proof/probe): `tally key inert` (plain run == base), `plain prefix`, `digest prefix`, `digest unchanged`, `tally deterministic`; 3 `night_market` rows; `WARN tally: run.nosuch` once, none for rooms/depth; `--tally` without key and `run.heat` name both print ERROR, exit 2; duplicate name errors. Long name `credits_found` aligns. Starter and Showcase lint and run identical to base. EfSim build 0 warnings 0 errors.
**Status:** Success

### [20261114 22:25] - Step 4: Docs
**Action:** `docs/intel.md` new `## Limiting the cards` (incl. room-count finding and `run.` trap); `docs/areas.md` `intel_max` key row; `docs/testing.md` `--tally` usage and paragraph with example, `tally` key row, `intel.SLOT` counted after `intel_max`; `docs/csharp-api.md` `IntelRoller.Roll` sentence. CRLF preserved.
**Result:** V4a: `intel_max` counts intel 4 / areas 1 / testing 1 / csharp-api 1; `--tally` in testing.md 4; `## Limiting the cards` 1. Example table columns computed with the same widths ef-sim uses.
**Status:** Success

### [20261114 22:50] - Step 5: Proof Bar, Probes, Game Compile
**Action:** Rebuilt EfSim; ran lint --strict, run, run --digest for ST SC NER sequentially into d-proof/after; tests/Sim; Showcase probe copies (tally key, flag, prefix and determinism checks, error cases, intel_max 0 and `run.rooms` cap probes); redirected game build `-p:InstallDir=$Q/game-install/`; live dll stat; git diff checks.
**Result:** 9 of 9 compares identical to c-proof base and digest2 (V5a-c). tests/Sim `All checks passed` (V5j). Probes: tally key inert, plain prefix, digest prefix, digest unchanged, tally deterministic; 3 night_market rows, nosuch WARN once, no WARN for rooms/depth; `--tally` without key and `run.heat` both ERROR (exit 2). Cap probe: lint --strict clean exit 0, run exit 1 with `FAIL runs[2]: intel.fence should be at least 1, got 0` once; `intel_max: "run.rooms"` lint prints one `run.*` warning (V5d, V5e). Game build: `Build succeeded.` 0 Warning(s), 0 Error(s), `game-install/ExpeditionFramework.dll` exists (V5f). Live dll stat identical before and after (`1794657202 262144`) (V5g). `git diff --stat 77d5504 -- api src/Game '*.csproj'` empty (V5h); no removed `public`/`protected` line in src/Core (V5i). `git status --porcelain` clean before this commit (V5k).
**Status:** Success

## Deviations
- Edits to CRLF files done with a small script preserving CRLF, not the plan's raw snippets verbatim; content as planned. Step 1 and Step 3 self-verified (Encouraged, not Required); no subagents used.
- Docs example for `--tally` omits the WARN line so the sample stays self-consistent.

## Issues Encountered
- First step 4 commit attempt was rejected by the lint (`trailer-required`, docs outside `.projex/`); re-run with the `Projex:` trailer.
- Plan's V3 probes were also run right after Step 3 as early verification; the Step 5 run repeated them against the final build.

## User Interventions
None.
