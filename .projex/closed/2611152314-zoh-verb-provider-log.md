# Execution Log: EF as ZoH Verb Provider

> **Status:** Complete
> **Started:** 20261116 05:38
> **Repo Root:** ExpeditionFramework (main checkout)
> **Plan File:** .projex/2611152314-zoh-verb-provider-plan.md
> **Parent:** 2611152314-zoh-verb-provider-plan.md
> **Base Branch:** main

## Pre-Check Results
```
PASS  Plan is committed (26f1195 feat(intel): told slots handed out per tell through an internal board)
PASS  Working tree is clean
PRE-CHECK PASSED
```
Manual: repo + base `main` correct | no open questions | deps met — sibling plan merged (26f1195), ZF `uses:` plan merged to ZF master (5d9c6a3) | Current State re-checked against csproj, manifest, `ModEntry`, `IntelBoard`, ZF `api/StoryApi.cs`: no drift.
Context: orchestrated run. Builds → scratch dir `Q` outside repo, never live `Mods/`. Game never launched — judgment checks J1–J4 returned to human.

## Steps

### [20261116 05:40] - Step 0: Baselines (plan V5 `$Q/before`)
**Action:** `dotnet build -c Release tools/EfSim`; `lint --strict` + `run` for `docs/examples/Starter`, `docs/examples/Showcase`, `samples/SampleArea` → `$Q/before/*-{lint,run}.txt`. Taken on the ephemeral branch before any source edit (tree = main @ 26f1195 + plan status).
**Result:** build 0 errors; 6 files written, every command exit 0.
**Status:** Success

### [20261116 05:40] - Step 1: csproj — copy-local `ZohFramework.Api` + install copy
**Action:** `ExpeditionFramework.csproj`: `ZohApi` property after `InstallDir` | `Reference Include="ZohFramework.Api"` (`Private` true) in a new ItemGroup | `CheckZohApi` target | `$(OutDir)ZohFramework.Api.dll` added to the `InstallMod` copy. As planned, verbatim.
**Result:**
- V1 ✓ — build to `-p:InstallDir=$Q/game-install/`: exit 0, 0 warnings, 0 errors. `ls -lR` of live `Mods/ExpeditionFramework` before/after: `cmp` exit 0 (live install untouched; `-R` added to the plan's `ls -l` so subfolders are covered). `cmp $Q/game-install/ZohFramework.Api.dll ../../Mods/ZohFramework/ZohFramework.Api.dll` exit 0. `$Q/game-install/` holds `ExpeditionFramework.dll`, `ExpeditionFramework.Api.dll`, `ZohFramework.Api.dll` (6144 B), `manifest.xml`, `Localization/`, `ExpeditionFramework/`.
- V2 ✓ (count differs) — `-p:ZohApi=$Q/missing.dll`: build exit 1; message `error : ZohFramework.Api.dll not found at …/missing.dll. Install ZoH Framework into Mods\ZohFramework\ or pass -p:ZohApi=<path to its ZohFramework.Api.dll>.` `grep -c` = 2, plan expected 1: msbuild prints an error inline and again in its summary. `$Q/none/` not created.
- Side finding: the scratch ZF build of another plan in this set (`ZohFramework.Api.dll` rebuilt from ZF master) is **not** byte-identical to the installed `Mods/ZohFramework/ZohFramework.Api.dll` (`cmp`: differ at char 137, same 6144 B size). EF ships the installed copy, per plan. See Issues.
**Status:** Success

### [20261116 05:42] - Step 2: manifest — list the api dll
**Action:** `manifest.xml`: `<Assembly>ZohFramework.Api.dll</Assembly>` first in `<Assemblies>`. Rebuilt to `$Q/game-install/`.
**Result:**
- V3 ✓ (first run) — ScanCheck on `$Q/game-install`: `Scanning … (ZohFramework.Api.dll, ExpeditionFramework.Api.dll, ExpeditionFramework.dll)` / `HARD FLAGS: 0` / `CAPABILITIES: none`, exit 0. No type uses the api yet, so this run proves the listed-payload half only; the reference half is the Step 4 rerun.
- V4 (manifest part) ✓ — `<Assembly>ZohFramework.Api.dll</Assembly>` count 1 (repo and installed copy); `<Prerequisites />` count 1.
- Deviation: ScanCheck run from a copy of ZF's `tests/ScanCheck` (`Program.cs`, `ScanCheck.csproj`, unchanged) in `$Q/ScanCheck` with `-p:GameManaged=<abs>`, not `--project $ZF/tests/ScanCheck`: building in place writes `bin/` `obj/` into the ZF repo, which is read-only for this run.
**Status:** Success

### [20261116 05:45] - Step 3: `IntelRoller.CardKey` (Core) + `TestCardKey`
**Action:** `src/Core/ExpeditionRun.cs`: `IntelRoller.CardKey(template, areaId, slotId, pick)` after `Tell`, as planned verbatim. `tests/Sim/Program.cs`: `TestCardKey()` called after `TestToldIntel();`, defined after it — the plan's five cases plus two more (a placeholder used twice; all ids null → `"__"`).
**Result:** V5 (tests half) ✓ — `dotnet run -c Release --project tests/Sim | tail -n 1` → `All checks passed`. V6 (first half) ✓ — `grep -c 'TestCardKey' tests/Sim/Program.cs` = 2.
**Status:** Success

### [20261116 05:48] - Step 4: `ModEntry : IStoryExtension` + `StoryProvider`
**Action:** `src/Game/ModEntry.cs`: `using ZohFramework.Api;`, `: IMod, IStoryExtension`, `RegisterStory` → `StoryProvider.Register`, type comment. `src/Game/StoryProvider.cs` (new): `Register`, `Rumour`, `Tell`, `Reserve`, `Today`, `Named` — code as planned. Rebuilt to `$Q/game-install/`.
**Result:**
- V1 rerun ✓ — build exit 0, 0 warnings, 0 errors; api copy `cmp` exit 0; live `Mods/ExpeditionFramework` listing unchanged (`cmp` exit 0).
- V3 rerun ✓ (settles B1 with the reference in use) — `HARD FLAGS: 0` / `CAPABILITIES: none`, exit 0.
- V7 ✓ — `ZohFramework.dll` count 0 in csproj, manifest, every `src/Game/*.cs` | `StoryPass.(Setup|Schedule)` 0 | `IStoryExtension` in `ModEntry.cs` 2 | `git diff --quiet main -- api tools/EfSim` → 0. V6 second half ✓ — `CardKey` in `StoryProvider.cs` 1. `tests/Sim` → `All checks passed`.
- Extra check, not in plan (scratch `$Q/ProviderCheck`, net8 console loading the built `ExpeditionFramework.dll` with a fake `IStoryRegistrar` / `IStoryCall`): 13/13 ✓ — assembly references `ZohFramework.Api` and not `ZohFramework` | `ModEntry is IStoryExtension` | `RegisterStory` twice → exactly `ef.rumour`, `ef.tell`, no passes (Live) | consumer that is no pack: `ef.rumour` → `null`, one `Warn` across three calls (`ef.rumour: not_a_pack is not an expedition pack, so there is no intel to tell`), `Named` null tolerated | `ef.tell` → `false` for no arg, unknown key, non-string | `IntelBoard.IsClaimed(consumer)` true after registering. The pack path (`IntelBoard.Tell` / `Show`) needs the running game → J2–J4.
- B3 read from ZF master source: `ExtensionSet.ForConsumer` → `Absorb(provider.Key, extension)` → `extension.RegisterStory(new Registrar(this))`, set built per consumer id → `ConsumerId` = the consumer's mod id. Holds on paper; J2 confirms in game.
- Deviations (comments only, plus one null guard): (1) `ModEntry` comment drops the plan's "once per such story" — ZF's README states `RegisterStory` can run more than once for the same story — and adds why no prerequisite. `Register` gets a summary saying it can repeat. (2) Comments extended to name the rejected alternative: claim only known packs | separate ticket instead of the key | `StoryVerbException`. (3) `Tell` guards `call.Args != null`, as `Named` already guards `call.Named`.
- Verification by self (verify-projex was Encouraged, not Required): diff read, build, scanner, harness above.
**Status:** Success

### [20261116 05:53] - Step 5: Docs
**Action:** `docs/zoh.md` (new): what the pack ships | verbs table with `nothing` / `false` cases | reservations | text keys and placeholders, lint caveat | two example stories (plain tell; `tell: false` + `/choose` + `/ef.tell`) | `required_verbs` listed by name, never `"*"` | no prerequisite on ZF, behaviour without ZF | in-game checks. Links: `docs/README.md` Reference row | `README.md` reference list | `docs/intel.md` § Told slots sentence replaced with the link. `docs/contributing.md` § Building: `ZohFramework.Api.dll` needed for the game build, `-p:ZohApi=`, not needed by `tests/Sim` / `ef-sim`.
**Result:** V4 ✓ — manifest greps 1 / 1; `test -f docs/zoh.md` exit 0; `zoh.md` count 1 each in `README.md`, `docs/README.md`, `docs/intel.md`; `ZohApi` in `docs/contributing.md` 1. Anchors used (`intel.md#told-slots`, `intel.md#the-picked-id-in-text`, `#the-text-keys`) exist.
- Example syntax sources: ZF master README (header, `/ps.entry; -> *entry;`, `/if *x, is: ?, …;;` as its fixtures write it, `/choose/ … /; -> *pick;`). The `smalltalk:` header and `@smalltalk_arrive` label come from 2611152320-zoh-customer-small-talk-plan.md (ZohFramework), **not yet on ZF master** at writing time; the page points at ZF's README as the reference. J5 open: re-read once that plan lands.
- Example stories were not run: no offline story run from this repo (ZF's harness lives in the ZF repo, read-only here).
**Status:** Success

### [20261116 05:56] - Completion: full verification
**Action:** Whole suite rerun on the final tree; spec compliance against 2611152305-told-intel-and-zoh-verbs-proposal.md § 2; quality review of the diff `main..projex/2611152314-zoh-verb-provider`.
**Result:**
| Check | Result |
|---|---|
| V1 | ✓ build exit 0, 0 warnings / 0 errors; live `Mods/ExpeditionFramework` listing identical before/after; installed api copy byte-identical to `Mods/ZohFramework/ZohFramework.Api.dll`; `$Q/game-install/` = `ExpeditionFramework.dll`, `ExpeditionFramework.Api.dll`, `ZohFramework.Api.dll`, `manifest.xml` (+ `Localization/`, `ExpeditionFramework/`) |
| V2 | ✓ build exit 1 with the message; `grep -c` 2 not 1 (msbuild repeats errors in its summary) |
| V3 | ✓ `HARD FLAGS: 0`, `CAPABILITIES: none` (after Step 2, after Step 4, final) |
| V4 | ✓ |
| V5 | ✓ `All checks passed`; `cmp` exit 0 ×6 (Starter, Showcase, SampleArea × lint, run) |
| V6 | ✓ 2; 1 |
| V7 | ✓ 0 everywhere; 0; 2; 0 |
| V8 | ✓ `git status --porcelain` empty after the step commits; `main..HEAD` touches only the planned files + this log |

Success criteria: SC1 ✓ | SC2 ✓ | SC3 ✓ offline (V7 + ProviderCheck), J1 J2 pending in game | SC4 non-pack path ✓ offline, rest J2–J4 pending | SC5 J3 pending (unknown / non-string key → `false` ✓ offline) | SC6 ✓ | SC7 ✓ V4, J5 pending | SC8 ✓.
Proposal § 2 compliance: every bullet met. One wording difference: proposal says EF claims "if `ConsumerId` is an EF pack id"; the plan (followed) claims always, since packs load after registration.
J6 (comments): present tense; rejected alternatives named — vendored / rebuilt dll (csproj), prerequisite (`ModEntry`), claim only known packs, separate ticket, `StoryVerbException` (`StoryProvider`). No projex references, no line numbers in source.
Cleanup: nothing created inside the repo besides committed files (`bin/`, `obj/` pre-existing, ignored). Scratch outside the repo left for the human's install: `$Q/game-install/` (the built mod), `$Q/before`, `$Q/after`, `$Q/ScanCheck`, `$Q/ProviderCheck`. No process left running. ZF repo untouched (`git status` clean there).
**Status:** Success

## Deviations
- V2 expected count `1` → `2`: msbuild prints the error inline and in the summary. Outcome unchanged.
- V1 `ls -l` → `ls -lR` on the live mod folder (subfolders covered).
- V3 run from a scratch copy of ZF's `tests/ScanCheck`, not in the ZF repo (read-only for this run).
- Step 3: two test cases beyond the plan's five.
- Step 4: comments reworded / extended (repeat `RegisterStory`; rejected alternatives); `call.Args != null` guard. Logic otherwise verbatim.
- Extra offline check `ProviderCheck` (scratch, not committed).
- Baselines taken on the ephemeral branch before the first source edit rather than on `main`: same tree.

## Issues Encountered
- **Rebuilt ZF api dll differs from the installed one.** The scratch build of ZF master made by the ZF plan of this set holds a `ZohFramework.Api.dll` that is not byte-identical to the installed `Mods/ZohFramework/ZohFramework.Api.dll` (same size 6144, differs from char 137). EF ships the installed copy (SC1). If the rebuilt ZF is installed over the live folder with its own api dll, EF's copy and ZF's differ in bytes while sharing name and version; the first loaded serves both (plan § Risks), but ZF's README rule is "copy, never rebuild". Suggested: when installing the new ZF, keep the already-installed `ZohFramework.Api.dll`, or rebuild EF afterwards so the copies match again. Not resolved here (ZF-side install step, human).
- **Claim lost when EF is disabled and enabled again in one session.** `PackRegistry.Stop` → `IntelBoard.Reset` clears claims; ZF calls `RegisterStory` again only when it rebuilds a consumer's set. Until then told slots roll with the day while the verbs still tell. `StoryProvider`'s reservations also outlive the reset, pointing at the old `IntelCards`; they lapse with the day. Outside the plan's steps; left as is, recorded for close.
- `docs/zoh.md` example uses the `smalltalk:` header from a ZF plan still executing; recheck against ZF's README when it lands (J5).
- **Log file damaged mid-run, then rebuilt.** A tool hook replaced the text of a whole-file read of this log with a stub once the file passed ~4 KB; the append helper wrote that stub back, so the log as committed in the Step 4 and Step 5 commits (483b2a3, 66a22eb) is truncated. Source files in those commits are unaffected. Rebuilt in the completion commit: header and Steps 0–3 from the Step 3 commit (fdf64bb), later entries from the executor's own text, unchanged in substance. Appends now go through the shell.

## Data Gathered
In-game checks left for the human (game never launched): J1 (EF loads with ZF absent; with ZF present, one `ZohFramework.Api loaded from …` line), J2 (verbs registered, card appears at the tell), J3 (`tell: false` / `/ef.tell`, filters, non-pack warning), J4 (budget / `intel_max` spent → `nothing`), J5 (doc usable, syntax matches ZF README).

## User Interventions
None.
