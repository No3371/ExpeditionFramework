# Execution Log: Runtime Safety Fixes

> **Status:** Complete
> **Started:** 20261112 15:10
> **Repo Root:** ExpeditionFramework (repo root; absolute path redacted)
> **Plan File:** .projex/2611120258-runtime-safety-fixes-plan.md
> **Parent:** 2611120258-runtime-safety-fixes-plan.md
> **Base Branch:** main

## Pre-Check Results
```
PASS  Plan is committed (8721cfc projex(revise): apply blue team Must Fix items to Phase 1 hardening plans)
PASS  Working tree is clean
PRE-CHECK PASSED
```
Manual: `ls-files` precondition ✓ (PackLoader.cs | Program.cs | README.md tracked). Baseline V1 ✓ `All checks passed` exit 0 | V2 ✓ `All checks passed` exit 0. Checkout mode (plan `Worktree: No`). Status → In Progress committed on main `d2ade22`; branch `projex/2611120258-runtime-safety-fixes`.

## Steps

### 20261112 15:15 - Step 1: Shared effect always leaves its recursion level
**Action:** `PackLoader.CompileSharedCall` runtime lambda: body wrapped `try { … } finally { run.LeaveShared(); }` with one-line rationale comment (as plan). Added `TestSharedEffectSurvivesThrow()` to `tests/Sim/Program.cs` (called after `TestPublicApi`): registry with `t:boom` throwing `InvalidOperationException`, pack effects `risky`/`fine`, second `PackLoader` on same content + same registry compiles `t:risky`/`t:fine`, 40 throwing calls, then `fine`.
**Result:** `SIM_RUNS=50` V1 → `All checks passed`, exit 0. Mutation check: PackLoader.cs temporarily restored to HEAD → `FAIL a throwing shared effect should not use up the recursion limit` (test discriminates); fixed file restored, diff confirmed.
**Status:** Success

### 20261112 15:25 - Step 2: Engine-kept `run.*` names can't be assigned
**Action:** `ExpeditionRun.EngineVars` (8 names, beside `Get`). `Builtins.Assign`: after read/write null check, `run.<engine name>` → `c.Error(path + " is kept by the engine; set and add can't change it")`, no-op effect. `PackLoader.CompileArea`: after `d.Vars = Stats(...)`, each engine-name key → `Problem(at + ".vars." + name, "run." + name + " is kept by the engine; vars can't set it")`. Added `TestEngineVarsAreReadOnly()` (set per engine name, add run.turn, set run.alarm clean, area vars turn → 1 problem w/ `vars.turn`, vars alarm clean, run built from vars alarm=7 → `Get("alarm")==7`, `Set(name,999)` → `Get(name)!=999` for all 8).
**Result:** V1 (`SIM_RUNS=50`) `All checks passed` exit 0. V2 (NER sim, full) `All checks passed` exit 0 — no NER content writes an engine var. Mutation check: Builtins.cs + PackLoader.cs reverted to step-1 HEAD → 10 engine-var FAILs (8 set, add, area vars) as expected (plus `never offered: interrupt_foreman` — sample-size noise at `SIM_RUNS=10`); fixed files restored.
**Status:** Success

### 20261112 15:35 - Step 3: `spawn` outside a fight takes `else`
**Action:** `spawn` built-in runtime lambda (`Builtins.cs`): leading guard `s.Run.Scene != "combat"` → `Log("bad", "spawn does nothing outside a fight")`, `none(s)`, return. Added `TestSpawnOutsideFight()` (enemy `rat` w/ intent `idle`, room `r` enter = spawn w/ then/else setting `run.then`/`run.else`).
**Result:** V1 (`SIM_RUNS=50`) ✓ exit 0; V2 ✓ exit 0. Test asserts then=0, else=1, exactly one `bad` line containing "outside a fight". Mutation (Builtins.cs → HEAD): both spawn checks FAIL; restored. Only framework-content spawn = sample `call_help` intent (in combat) → unaffected.
**Status:** Success

### 20261112 15:45 - Step 4: Log an item the game couldn't give
**Action:** `IInventory.Give` (`ExpeditionRun.Api.cs`): `!string.IsNullOrEmpty(itemId) && name == null` → `Log("bad", "could not give " + itemId + " (unknown item id, or no room on the ground)")`, with plan's rationale comment. Framework `FakeBridge`: `Unknown` set; `GiveItem` mirrors `GameBridge` (`IsNullOrEmpty || Unknown` → null). `TestPublicApi`: bridge kept in local `bridge`; `Give("ghost")` → null + last log `bad` naming ghost; `Give("")`/`Give(null)` add no line.
**Result:** V1 (`SIM_RUNS=50`) ✓ exit 0; V2 ✓ exit 0. Mutation (Api.cs → HEAD): `FAIL an item the game can't give is logged`; silent-empty check still passes (expected — old code also silent); restored.
**Status:** Success

### 20261112 16:00 - Step 5: Rendering never rolls
**Action:** A2 check first: `AddOption` evaluates `When`, `Preview`, label/confirm `Lines.Render` only; `Offer` evaluates `Where`/`Select`; `EnsureFocus` sets `focused` stat (no roll); `Hud` evaluates row `When`, parts, `Span`, `IntentLabel`; `PanelTitle` renders/`Choose`s a line — no effects anywhere → A2 holds. Only bypass of `Roll` = static intel `Roll(AreaDef, roll)` (map-time, unrelated). Game callers (`src/Game/ExpeditionLocation.cs`) use only `PanelTitle`, `Hud`, `Options` → three wrappers cover them. Edit (`ExpeditionRun.cs`): `private int rendering` + `Roll` returns 1 while `rendering > 0` (plan comment kept); `Options`/`Hud`/`PanelTitle` bodies → private `BuildOptions`/`BuildHud`/`BuildPanelTitle(areaName)`, public methods `rendering++; try { return Build…(); } finally { rendering--; }`. Added `TestRenderingDoesNotRoll()` (roll delegate counts calls and returns `n`; room interaction `v` label `variants: 3`, `when: "1d6 >= 1"`; area hud row `when: "1d6 > 0"`, `enemies.line`; 10× each of `Options`/`Hud`/`PanelTitle("x")`).
**Result:** V1 full (5000 runs) ✓ `All checks passed`, exit 0; outcomes identical to baseline (`knocked_out=3762, cleared=1062, left=176; longest run 27 steps`) → sample content never rolled during render. V2 ✓ exit 0, final summary line identical to baseline. Mutation (ExpeditionRun.cs → HEAD): `FAIL rendering rolled 30 times` + `FAIL … first text: k_2`; restored. Verification: self-verified (Verify-Projex was Encouraged, not Required) via mutation evidence + A2 code read.
**Status:** Success

### 20261112 16:15 - Step 6: README — run variables and render dice
**Action:** A3 per call site: every candidate writes via `run.Set`/`s.Run.Set`/`SetText` or unqualified `Set` inside `ExpeditionRun` (partial incl. `.Api.cs`) → run vars/texts: `natural`/`total` (`Builtins` check + `roll`), `$scheduled` (`schedule`), `$lost` (`lose_items`), `cleared_ground`, `$taken` + `found_items` (`take_back`), `given` (`Found`, `give_loot drops_of`), `healed` (`ICombat.Heal`), `outcome_<outcome>` (`Finish`), `last_damage` (`Damage`), `won` (`EndFight`), `trade_cash`/`trade_bought`, `cart_taken`, `$pick` (`PackLoader.ScheduledText`). All confirmed; non-run writes (`gun.Set`, `decision.Set`, `Hero/e/focus.Set`) excluded. README edits: § Vocabulary engine-kept list (+`ground`, `scheduled`; set/add/area `vars` = problem) + ambient-names paragraph; § Interactions render-dice paragraph (dice count as 1, `variants` first text, `chance` gate always shows / high-roll never shows, not rechecked on press — `Perform` has no `When` check, confirmed — roll into `run.` var on room entry); under Effects table `spawn` outside a fight sentence; § The C# API `IRun` `Set` no-op on engine names.
**Result:** V4: `` `ground` ``=1 | `kept by the engine`=1 | `count as 1`=1 | `never shows`=1 | `outside a fight`=1 (all ≥1, each phrase on one line). Judgment: names verified; `vars` + `IRun`/`Set` sentences present.
**Status:** Success

### 20261112 16:30 - Completion: full verification
**Action:** V1 full | V2 | V3 (`--no-incremental` rerun to force compile) | V4 (Step 6) | spec compliance vs plan SC1–SC7 | quality review of `git diff main..HEAD` | cleanup.
**Result:**
- V1: `All checks passed`, exit 0 (outcomes `knocked_out=3762, cleared=1062, left=176; longest run 27 steps` = baseline).
- V2: NER sim `All checks passed`, exit 0.
- V3: `dotnet build -c Release ExpeditionFramework.csproj -p:InstallDir=obj/verify-install/` exit 0, 0 warnings, 0 errors; Api + main dll built.
- SC1 ✓ `TestSharedEffectSurvivesThrow` | SC2 ✓ `TestEngineVarsAreReadOnly` + V2 | SC3 ✓ `TestSpawnOutsideFight` | SC4 ✓ `TestPublicApi` additions | SC5 ✓ `TestRenderingDoesNotRoll` (0 rolls, `k_0`) | SC6 ✓ V1/V2/V3 | SC7 ✓ V4 + judgment. Each test shown to fail against pre-fix code (mutation checks in Steps 1–5).
- Deferred judgment check: in-game (variant labels stable, no stray `bad` lines) — needs the game; not runnable here.
- Quality: added comments name hazard/rationale, no projex refs, no plan shape; no dead code; wrappers keep public doc comments.
- Cleanup: removed `obj/verify-install/` (V3 install target, gitignored). Scratch copies used for mutation checks live outside the repo (session scratchpad). Working tree clean.
**Status:** Success

## Deviations
- Step 4: plan's local `int lines` → `beforeEmpty` (`TestPublicApi` already declares `lines` later in the same scope → CS0128). No outcome effect. Check also asserts `Tone == "bad"` (SC4 wording).
- Step 6: plan places the ambient-names sentence after the `IRun` sentence ("Then…") without naming a section; placed in § Vocabulary next to the engine-kept list (data authors are its audience). Render-dice paragraph placed in § Interactions (it is about buttons). `outcome_<name>` written as `outcome_<outcome>`; `$`-texts written in their readable form `run.$lost` etc. (Expr reads texts as `run.$name`).

## Issues Encountered

## Data Gathered

## User Interventions
