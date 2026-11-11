# Walkthrough: Runtime Safety Fixes — Shared-Effect Leak, Engine Vars, Spawn, Give, Render-Time Dice

> **Execution Date:** 2026-11-12
> **Completed By:** Opus 5.5 (execute-projex + close-projex subagents, dispatched by orchestrate-projex)
> **Source Plan:** 2611120258-runtime-safety-fixes-plan.md
> **Parent:** 2611120258-runtime-safety-fixes-plan.md
> **Log:** 2611120258-runtime-safety-fixes-log.md
> **Duration:** ~1.5 h (log 15:10 → 16:30)
> **Result:** Success
> **Base Branch:** main (squash close; branch also carries the stacked loader close `8bb29f8` + `e44710e` → one squash lands both plans on main)

---

## Summary

Five R13 runtime traps closed, each one-site fix + sim test proven to fail against pre-fix code: throwing shared effect no longer leaks `sharedDepth`; `set`/`add`/area `vars` on engine-kept `run.*` names → load problem; `spawn` outside a fight logs + runs `else`; failed `Give` of non-empty id logged; `Options`/`Hud`/`PanelTitle` never roll. README documents engine-kept + engine-set `run.*` names and render-time dice. 7/7 criteria pass (in-game judgment check deferred — needs the game).

---

## Objectives Completion

| Objective | Status | Notes |
|-----------|--------|-------|
| Shared effect leaves recursion level on throw (SC1) | Complete | `try/finally` in `CompileSharedCall` |
| Engine-kept `run.*` read-only from data (SC2) | Complete | `ExpeditionRun.EngineVars`; `Assign` error; `CompileArea` `vars` problem |
| `spawn` outside a fight → `else` (SC3) | Complete | guard at top of runtime lambda |
| Failed give logged (SC4) | Complete | `IInventory.Give`; empty id stays silent |
| Rendering never rolls (SC5) | Complete | `rendering` counter + `Build*` wrappers |
| Sims + game build green (SC6) | Complete | V1/V2/V3 exit 0 |
| README docs (SC7) | Complete | § Vocabulary, § Interactions, Effects, § The C# API |

---

## Execution Detail

> Derived from `git log main..fd2699a` (runtime commits) and the execution log. Line numbers are at `fd2699a` (pre-loader stack); loader commits later shifted `PackLoader.cs`/`Builtins.cs`/`Program.cs`.

### Step 1: Shared effect always leaves its recursion level — `28f9d7d`

**Planned:** wrap body in `try/finally { run.LeaveShared(); }`; test 40 throws then a working shared effect.
**Actual:** as planned. `PackLoader.CompileSharedCall` L480-492: args build + `body[0](…)` inside `try`, `run.LeaveShared()` in `finally` with hazard comment. `TestSharedEffectSurvivesThrow()` added (registry `t:boom` throws `InvalidOperationException`; effects `risky`/`fine`; 40 throws then `fine`).
**Deviation:** None.

| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Core/PackLoader.cs` | Modified | Yes | L480-492 `try/finally` |
| `tests/Sim/Program.cs` | Modified | Yes | +`TestSharedEffectSurvivesThrow` + call |

**Verification:** V1 (`SIM_RUNS=50`) ✓. Mutation (PackLoader.cs → HEAD) → `FAIL a throwing shared effect should not use up the recursion limit`.

### Step 2: Engine-kept `run.*` names can't be assigned — `fa62f66`

**Planned:** `EngineVars` list; `Assign` problem; `CompileArea` `vars` problem; test.
**Actual:** as planned. `ExpeditionRun.EngineVars` (L319-321: `depth rooms rooms_left turn enemies credits ground scheduled`). `Builtins.Assign` L320-324: `run.<engine>` → `c.Error(path + " is kept by the engine; set and add can't change it")`, no-op effect. `PackLoader.CompileArea` L853-856: per-key `Problem(at + ".vars." + name, …)`. `TestEngineVarsAreReadOnly()` covers set per name, `add run.turn`, clean `run.alarm`, area `vars` turn/alarm, `IRun.Set` ignored for all 8.

| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Core/ExpeditionRun.cs` | Modified | Yes | `EngineVars` beside `Get` |
| `src/Core/Builtins.cs` | Modified | Yes | `Assign` engine-var error |
| `src/Core/PackLoader.cs` | Modified | Yes | `CompileArea` `vars` check |
| `tests/Sim/Program.cs` | Modified | Yes | +`TestEngineVarsAreReadOnly` |

**Verification:** V1 ✓, V2 ✓ (no NER content writes engine vars). Mutation → 10 engine-var FAILs.

### Step 3: `spawn` outside a fight takes `else` — `065e55e`

**Actual:** `spawn` runtime lambda (`Builtins.cs` L575-580): `Scene != "combat"` → `Log("bad", "spawn does nothing outside a fight")`, `none(s)`, return. `TestSpawnOutsideFight()` asserts then=0, else=1, one `bad` line.
**Deviation:** None.
**Verification:** V1 ✓ V2 ✓; mutation → both spawn checks FAIL.

### Step 4: Log an item the game couldn't give — `0368958`

**Actual:** `IInventory.Give` (`ExpeditionRun.Api.cs` L138-139): `!IsNullOrEmpty(itemId) && name == null` → `bad` line `could not give <id> (unknown item id, or no room on the ground)` + rationale comment. Framework `FakeBridge` gains `Unknown` set, `GiveItem` mirrors `GameBridge`. `TestPublicApi`: `Give("ghost")` logs; `Give("")`/`Give(null)` silent.
**Deviation:** local `lines` → `beforeEmpty` (CS0128 name clash). No outcome effect.
**Verification:** V1 ✓ V2 ✓; mutation → `FAIL an item the game can't give is logged`.

### Step 5: Rendering never rolls — `1eaaf86`

**Actual:** A2 confirmed by reading `AddOption`/`Offer`/`EnsureFocus`/`Hud`/`PanelTitle` (no effects). `ExpeditionRun.cs`: `private int rendering`; `Roll` returns 1 while `rendering > 0` (L296-301, comment cites `MapContext` precedent); `Options`/`PanelTitle`/`Hud` → `rendering++; try { return Build…(); } finally { rendering--; }` around new private `BuildOptions`/`BuildPanelTitle`/`BuildHud`. `TestRenderingDoesNotRoll()`: counting roll delegate, `variants: 3` label, dice `when` on interaction + HUD row, 10× each call → 0 rolls, `k_0`.
**Deviation:** None.
**Verification:** V1 full ✓ with outcomes identical to baseline (`knocked_out=3762, cleared=1062, left=176; longest run 27 steps`); V2 ✓ summary identical. Mutation → `FAIL rendering rolled 30 times`.

### Step 6: README — run variables and render dice — `cfd587c`

**Actual:** A3 verified per call site (all candidates write via `run.Set`/`SetText`). README: § Vocabulary engine-kept list (+`ground`, `scheduled`; `set`/`add`/area `vars` = problem) + engine-set ambient names; § Interactions render-dice paragraph (`chance` gate always shows, high-roll never shows, not rechecked on press, roll into `run.` var on room entry); Effects table `spawn` outside a fight; § The C# API `IRun.Set` no-op on engine names.
**Deviation:** ambient-names paragraph placed in § Vocabulary (plan named no section); `outcome_<outcome>` / `run.$lost` readable spellings.
**Verification:** V4 five probes each = 1.

### Completion — `fd2699a`

Plan + log → `Complete`. V1 full / V2 / V3 (0 warnings, 0 errors) / V4 ✓. Cleanup: `obj/verify-install/` removed.

---

## Complete Change Log

> **Derived from:** `git diff --stat main..fd2699a` (runtime only). Full squash range `main..projex/2611120258-runtime-safety-fixes` also includes the loader close (`e44710e`, 16 files) — see 2611120258-loader-loud-failures-walkthrough.md.

### Files Created
None (runtime half).

### Files Modified
| File | Changes | Lines | In Plan? |
|------|---------|-------|----------|
| `src/Core/PackLoader.cs` | shared-call `try/finally`; area `vars` engine check | +15/-4 | Yes |
| `src/Core/ExpeditionRun.cs` | `EngineVars`; `rendering` guard; `Build*` wrappers | +28/-1 | Yes |
| `src/Core/ExpeditionRun.Api.cs` | failed-give log | +2 | Yes |
| `src/Core/Builtins.cs` | `Assign` engine-var error; `spawn` out-of-fight branch | +11 | Yes |
| `tests/Sim/Program.cs` | 4 new tests + `TestPublicApi` give checks; `FakeBridge.Unknown` | +133/-2 | Yes |
| `README.md` | run vars, render dice, spawn, `IRun.Set` | +16/-3 | Yes |
| `.projex/…-log.md`, `…-plan.md` | execution log, status | +72/-1 | — |

### Files Deleted
None.

### Planned But Not Changed
None.

---

## Success Criteria Verification

| Criterion | Method | Result | Evidence |
|-----------|--------|--------|----------|
| SC1 throw doesn't leak `sharedDepth` | V1 `TestSharedEffectSurvivesThrow` + mutation | Pass | log Step 1 |
| SC2 engine names → problems; `EngineVars` = `Get` set | V1 `TestEngineVarsAreReadOnly`, V2 + mutation | Pass | log Step 2 |
| SC3 spawn outside fight → else, one bad line | V1 `TestSpawnOutsideFight` + mutation | Pass | log Step 3 |
| SC4 failed give logged; empty id silent | V1 `TestPublicApi` + mutation | Pass | log Step 4 |
| SC5 render never rolls; `_0` label | V1 `TestRenderingDoesNotRoll` + mutation | Pass | log Step 5 |
| SC6 sims + game build | V1, V2, V3 exit 0 | Pass | log Completion; re-run at close (below) |
| SC7 README | V4 + judgment | Pass | log Step 6 |

**Overall:** 7/7. Judgment check "in game: variant labels stable, no stray `bad` lines" **not run** — needs the game.

**Close-time re-verification on combined tip `e44710e` (runtime + loader):**
```
SIM_RUNS=50 dotnet run -c Release --project tests/Sim                  → All checks passed, exit 0
dotnet run -c Release --project ../NightExpeditionsReborn/tests/Sim    → All checks passed, exit 0
```
(NER sim at `SIM_RUNS=50` exits 1 with `never offered: scan|valve|clean|antitoxin` — sample-size noise, same class as log Step 2's note; full run passes.)

---

## Deviations from Plan

1. **Step 4 local rename** — `lines` → `beforeEmpty` (CS0128). Impact: none.
2. **Step 6 placement** — ambient names in § Vocabulary, render dice in § Interactions; readable `run.$name` spellings. Impact: none; plan named no section.

---

## Issues Encountered

- **close-precheck ERROR** — log `Repo Root:` holds `ExpeditionFramework (repo root; absolute path redacted)`, not a path → `RESULT=ERROR` (path not found). Advisory only; close proceeded from git state, same as loader close.
- **NER sim reduced-run noise** — `SIM_RUNS=50` produces `never offered` FAILs for rare interactions; use full run for V2.

---

## Key Insights

**Rationale promoted:** README.md (engine-kept names, render-time dice gate asymmetry, `IRun.Set` no-op, spawn outside fight) — in-code comments carry the `finally` and `rendering` rationale.

### Lessons Learned
1. **Mutation check per step** — reverting the fixed file to HEAD and watching the new test fail proved each test discriminates; cheap (one rerun) and caught nothing false.
2. **Render paths need an explicit "no side effects" mode** — UI polling at 0.5 s turns any RNG call into flicker + RNG drain; a scoped counter around the three public entry points was enough.

### Pattern Discoveries
- **Public wrapper + private `Build*`** — `rendering++; try { return Build…(); } finally { rendering--; }` keeps doc comments on public API and makes the guard re-entrant.

### Gotchas / Pitfalls
- Sim sample size: reduced `SIM_RUNS` trips coverage checks (`never offered`) unrelated to the change.
- Stacked execution: loader commits shift runtime line numbers; cite symbols.

### Technical Insights
- `ExpeditionRun.Get` answers 8 names from engine state; `EngineVars` now the single list — keep in sync if `Get` grows.

---

## Recommendations

### Immediate Follow-ups
- [ ] In-game check: variant labels stable, no stray `bad` lines in normal NER play.

### Future Considerations
- Out-of-scope semantics (1-turn enemy status expiry, `remove` last enemy = win, enemy-phase spawn losing Opening intent) → later runtime phase.
- `IRun.Set` on engine names silently shadows (Accept Risk) — revisit when first C# pack writes engine names.

### Plan Improvements
- Log `Repo Root:` should hold a resolvable path for close-precheck.
- V2 should state "full run" explicitly (reduced runs are noisy).

---

## Related Projex Updates

| Document | Update |
|----------|--------|
| 2611120258-runtime-safety-fixes-plan.md | Complete; `Completed`/`Walkthrough` added → closed/ |
| 2611120258-runtime-safety-fixes-log.md | Status `Complete` confirmed → closed/ |
| 2611120448-phase1-hardening-plans-redteam.md | both plans closed → Walkthroughs link updated → closed/ |
| 2611120507-phase1-hardening-plans-blueteam.md | already closed — no change |
| 2611120250-framework-expansion-synthesis-eval.md | stays open (later phases) — Walkthroughs link added |
| 2611120227-runtime-hooks-expr-expansion-explore.md | stays (covers later runtime phases) — no change |

---

## Appendix

### Commits squashed (main..projex/2611120258-runtime-safety-fixes)
- `28f9d7d` projex: step 1 - shared effect leaves recursion level on throw
- `fa62f66` projex: step 2 - engine-kept run vars are read-only from data
- `065e55e` projex: step 3 - spawn outside a fight runs else
- `0368958` projex: step 4 - log an item the game could not give
- `1eaaf86` projex: step 5 - rendering never rolls
- `cfd587c` projex: step 6 - README run variables and render dice
- `fd2699a` projex: complete runtime-safety-fixes
- `8bb29f8` projex: start execution of loader-loud-failures
- `e44710e` feat(loader): loud failures - warnings channel, schema shapes, spec read tracking, lint (loader squash, Projex: 2611120258-loader-loud-failures)
- (this close commit) projex: close runtime-safety-fixes - add walkthrough

### References
- 2611120258-runtime-safety-fixes-log.md | 2611120258-loader-loud-failures-walkthrough.md | 2611120448-phase1-hardening-plans-redteam.md | 2611120507-phase1-hardening-plans-blueteam.md
