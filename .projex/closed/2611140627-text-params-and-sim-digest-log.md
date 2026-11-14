# Execution Log: Text Params for Shared Effects + ef-sim Run Digest

> **Status:** Complete
> **Started:** 20261114 07:00
> **Repo Root:** E:/SteamLibrary/steamapps/common/Probably Stolen/Modding/ExpeditionFramework
> **Plan File:** .projex/2611140627-text-params-and-sim-digest-plan.md
> **Parent:** 2611140627-text-params-and-sim-digest-plan.md
> **Base Branch:** main

## Pre-Check Results
PASS  Plan is committed (7e659e9 projex(plan): add text-params-and-sim-digest)
PASS  Working tree is clean

PRE-CHECK PASSED

Proof dir (scratchpad): `C:\Users\BA\AppData\Local\Temp\claude\a48a32985dda25c5\057518b9-283c-4d89-bc27-9ae607bcf871\scratchpad\c-proof\` (base/, digest/, after/). Baseline dir: `scratchpad\c-baseline\`.

## Steps

### [20261114 07:05] - Step 0: Preflight + Baseline Capture
**Action:** NER `1277ab0`, `ExpeditionFramework/ Localization/ sim.json` clean. `dotnet build -c Release tools/EfSim`. Captured lint --strict + run for NER, Showcase, Starter to `scratchpad\c-proof\base\` (script `step0.sh`, output `step0.out`). `dotnet run -c Release --project tests/Sim`.
**Result:** all four `cmp` vs `scratchpad\c-baseline\` ok (NER run, Showcase run, Starter run, NER lint) -> A1/A2 hold. tests/Sim: `All checks passed`. V0a V0b V0c pass.
**Status:** Success

### [20261114 07:30] - Step 1: Digest Instrument
**Action:** Added `ExpeditionRun.Logged` callback (called in `Log`), `MemoryStore.Numbers`/`Texts`, `KitBridge.NightLines`/`ReputationNow`, new `tools/EfSim/RunDigest.cs` (SHA-256 over outcome, log lines, night lines, memory, reputation), `--digest` in `PackRun.cs` (digest lines printed after the summary line), usage text in `Program.cs`. `dotnet build -c Release tools/EfSim` -> 0 errors.
**Result:** builds. Judgment check J2 (src/Game / api use of the new members) still to do at Step 6.
**Status:** Success

### [20261114 07:40] - Step 2: Prove Digest + Capture Digest Baseline
**Action:** `step2.sh` in `scratchpad/c-proof/`: for ST, SC, NER: plain run, `--digest` twice, cmp checks. Digest baseline lines to `scratchpad/c-proof/digest/*-digest-base.txt`.
**Result:** V2a ok x3 (plain == base run). V2c ok x3 (d1 == d2). V2d counts NER 39, SC 3, ST 1. V2b: the script's `grep -v '^digest '` reported a diff at char 81 line 1 for all three, because Git Bash `grep` strips the CR of the CRLF output, so the filtered stream differs from the CRLF plain file. Re-ran with `grep -U` (keeps CR): ST, SC, NER all identical to plain. Tool artifact, not a digest effect. Digest-base files were written with plain grep (CR stripped); Step 6 uses the same pipeline, so the comparison is like for like.
**Status:** Success

### [20261114 08:10] - Step 3: C1 Text Params in the Loader
**Action:** `PackLoader.cs`: fields (`expanding`, `uncalledText`, `TextValue`, `TextParamName`, `Placeholder`, `BareTextArg`), `DeclareShared` (param name/default checks, `ScanTextUse` on every shared body, numeric-only effects keep the compile-once path unchanged, text effects register a per-call-site compiler), `ScanTextUse`, `Fill`, `CompileSharedCall` (`rawBody` arg, text arg resolution + errors, cycle set, per-site compile), `Finish` (once-only uncalled warning). `Expr.cs`: `arg.$NAME` has no number reader. Built `tools/EfSim`; `lint --strict` Showcase and Starter vs base.
**Result:** build 0 errors. V3a: Showcase and Starter lint byte-identical to `base/`. Self-reviewed the diff: numeric path untouched except the read-only `ScanTextUse` call; comments name rejected alternatives and carry no projex references. Full NER/Showcase/Starter proof is Step 6.
**Status:** Success

### [20261114 08:30] - Step 4: Tests
**Action:** Added `TestTextParams()` to `tests/Sim/Program.cs` (called in `Main` after `TestSharedEffectSurvivesThrow()`), covering plan cases 1-16.
**Result:** `dotnet run -c Release --project tests/Sim` -> `All checks passed`, exit 0. Three test-side corrections on the way, none weakening an assertion: (1) the bare-form case read `Rep["security"]` for a 0-amount call, which the bridge never records, so it now asserts the key is absent and the text key `k_security` registered; (2) the undeclared-placeholder case replaced all three `{arg.$f}` and so got three problems, now a single-use body gives one; (3) the "outside braces" message said `write {arg.$x}` literally, which the plan's case 10 expects to carry the real name, so the loader now names the parameter found (`write {arg.$f}`).
**Status:** Success

### [20261114 08:45] - Step 5: Docs
**Action:** `packs.md` (new "Text parameters" subsection after the numeric-parameter line, example + rules), `expressions.md` (`arg.NAME` row note), `text.md` (key templates paragraph), `testing.md` (`--digest` synopsis and option text, new "Proving a refactor" subsection).
**Result:** V5a `grep -c 'Parameters are numbers' docs/packs.md` -> 0 (sentence reworded to "Plain parameters are numbers, ..."; the literal old sentence is gone). V5b: `{arg.$` present in packs.md, expressions.md, text.md; `--digest` count in testing.md 3. J4: example uses `reputation`, `add`, `night_log`, all existing effects; error texts quoted in prose match the implemented ones (path shape, "calls itself" described, not quoted).
**Status:** Success

### [20261114 09:05] - Step 6: Full Proof Bar
**Action:** `step6.sh` in `scratchpad/c-proof/` (output `step6.out`): rebuilt `tools/EfSim`, then for ST, SC, NER: `lint --strict`, `run`, `run --digest` into `after/`; cmp against `base/` and `digest/*-digest-base.txt`; `tests/Sim`; `git diff --stat 6b99edf -- '*.csproj'`.
**Result:** V6a ok x3 (lint byte-identical). V6b ok x3 (run stdout byte-identical). V6c ok x3 (digest lines identical; NER 39, SC 3, ST 1). V6d `All checks passed`, exit 0. V6e csproj diff empty. J1: every `dotnet` command in this log names `tools/EfSim` or `tests/Sim`. J2: grep of `src/Game` and `api` for `MemoryStore`, `: ExpeditionRun`, `.Logged`, `Numbers` -> no hits, so the added members can't break them. J3: new comments state present behaviour and name rejected alternatives; none cite a projex document.
**Status:** Success

### [20261114 09:10] - Completion
SC1-SC8 met (see walkthrough inputs above). Spec compliance: parent proposal's EF step 1 (digest, C1) delivered; C2 and NER refactor out of scope per plan. Quality review: numeric shared-effect path unchanged (new read-only scan only); `Logged` adds one null-check on the log path; digest reads state only.

## Deviations
Bare `arg.$NAME` message names the actual parameter (`write {arg.$f}: ...`) instead of the literal `{arg.$x}` in the plan text, so the plan's test case 10 reads as specified.
V2b / digest-vs-plain comparison uses `grep -U` (CR-preserving); Git Bash grep otherwise strips CRs. No effect on acceptance.

## Issues Encountered

## Data Gathered

## User Interventions
