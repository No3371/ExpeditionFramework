# Runtime Safety Fixes — Shared-Effect Leak, Engine Vars, Spawn, Give, Render-Time Dice

> **Status:** In Progress
> **Author:** Opus 5.5 (plan-projex subagent, dispatched by orchestrate-projex)
> **Source:** Direct request ("Plan hardening first") — Phase 1 of 2611120250-framework-expansion-synthesis-eval.md § Recommended Sequence (Runtime R13 + § Incidental Bugs)
> **Parent:** 2611120250-framework-expansion-synthesis-eval.md
> **Related Projex:** 2611120258-loader-loud-failures-plan.md (sibling, Phase 1 loader half — executes **after** this plan) | 2611120227-runtime-hooks-expr-expansion-explore.md (R13, silent traps) | 2611120448-phase1-hardening-plans-redteam.md | 2611120507-phase1-hardening-plans-blueteam.md (dispositions applied 2026-11-12)
> **Worktree:** No — NightExpeditionsReborn's sim compiles `../../../ExpeditionFramework/src/Core` by relative path, so it only tests the main checkout.

---

## Summary

Five small runtime fixes from R13, each a one-site change plus a sim test: a shared effect that throws no longer leaks its recursion counter; `set`/`add` on engine-kept `run.*` names (and an area's `vars` naming one) becomes a load problem instead of a silent no-op; `spawn` outside a fight logs and takes its `else` branch instead of running `then` with an enemy that never arrived; giving a non-empty item id the game can't create is logged; rendering buttons/HUD no longer rolls dice (no flicker, no RNG drain). README documents the engine-kept and engine-set `run.*` names and what render-time dice mean for `when` gates.

**Scope:** `src/Core/PackLoader.cs` (`CompileSharedCall`, `CompileArea` `vars` check), `src/Core/ExpeditionRun.cs`, `src/Core/ExpeditionRun.Api.cs`, `src/Core/Builtins.cs`, `tests/Sim/Program.cs`, `README.md`.
**Estimated Changes:** 6 files, ~8 functions.

---

## Objective

### Problem / Gap / Need
Verified in code this session:
- `CompileSharedCall` (`PackLoader.cs`): `if (!run.EnterShared(name)) return; … body[0](…); run.LeaveShared();` — no `finally`. An exception in the body leaks one level of `sharedDepth`; after 32 leaks every shared effect logs "called itself too deeply" and does nothing for the rest of the run. (`ExpeditionRun.Guarded` already uses `finally` for C# handlers.)
- `ExpeditionRun.Get` answers `depth, rooms, rooms_left, turn, enemies, credits, ground, scheduled` from engine state; `Set` writes the vars dict → `set run.turn` etc. is silently ignored (`Builtins.Assign` → `Expr.Writer`).
- `spawn` built-in: `Make` + `Combat.Spawn` + `then(...)`. `ExpeditionRun.Spawn` returns early when `Scene != "combat"`, but the built-in still runs `then` with the never-added arrival as target.
- `IInventory.Give` (`ExpeditionRun.Api.cs`): `Bridge.GiveItem` returns null for an unknown id (or no room on the ground) → nothing logged.
- `Options()`, `Hud()`, `PanelTitle()` are called by the game every refresh (`ExpeditionLocation.Tick`, 0.5 s). Lines with `variants` (`Lines` render path `scope.Run.Roll(chosen.Variants)`) and dice in `when`/`where`/HUD conditions call `Run.Roll` → labels flicker, unseeded RNG consumed by UI.

### Success Criteria
- [ ] SC1 A shared effect whose body throws leaves `sharedDepth` unchanged: after 40 throwing calls, a working shared effect still runs and no "too deeply" line is logged.
- [ ] SC2 `set`/`add` with `var` `run.<name>` for each engine-kept name → load problem naming the var; an area `vars` key naming an engine-kept name → load problem at `<area>.vars.<name>`; `ExpeditionRun.EngineVars` lists exactly the names `Get` answers from engine state.
- [ ] SC3 `spawn` outside a fight: `then` not run, `else` run, one `bad` log line.
- [ ] SC4 `Inventory.Give(id)` returning null for a non-empty id logs one `bad` line naming the id; `null` or `""` id logs nothing.
- [ ] SC5 `Options()`, `Hud()`, `PanelTitle()` never call the run's roll delegate; a `variants` label renders as `_0`.
- [ ] SC6 Framework sim and NER sim exit 0; game assembly builds.
- [ ] SC7 README lists engine-kept `run.*` names (read-only via `set`/`add`/area `vars`; C# `IRun.Set` on them is ignored) and engine-set ambient names, and notes render-time dice incl. the gate asymmetry (`chance` gates always show, high-roll gates never show; roll into a `run.` var on room entry instead).

### Out of Scope
- 1-turn enemy-applied status expiring before the player acts; `remove` of the last enemy ending the fight as won; spawn during enemy phase losing its Opening intent (semantic changes → later runtime phase).
- Catching exceptions from data hooks (`Run` hooks propagate to `Perform`'s caller today; unchanged).
- Loader-side warnings, schema checks, lint → 2611120258-loader-loud-failures-plan.md.
- `ef_best_depth` write-only, clear-commit-on-crash (persistence phase).

---

## Context

### Current State
- `ExpeditionRun.Roll(int sides) => sides <= 0 ? 0 : roll(sides);` (`roll` = ctor delegate; game passes `RNG.GetRandomInt`, sims pass `random.Next` or `n => 1`). `MapContext.Roll` already returns `sides > 0 ? 1 : 0` for the same reason (map cards are render-only).
- `Options()` builds `RunOption`s via `Offer` → `AddOption` (evaluates `When`, `Where`, `Uses`, label render, preview). `EnsureFocus` (called first) does not roll. No internal caller of `Options()`/`Hud()`/`PanelTitle()`; callers: `ExpeditionLocation` (game), both sims.
- Runtime logs content mistakes as English `bad` lines (precedent: `Find` → `"unknown " + kind + " " + id`, `EnterShared` → "called itself too deeply").
- Both `FakeBridge`s (framework, NER sims) return `itemId` from `GiveItem`, so SC4's log never fires in existing sims.
- Planning grep of NER: no `set`/`add` on engine-kept names; NER `spawn` only inside the in-combat `call_for_help` intent; NER `variants` only in `say` lines; no dice in NER interaction `when`/HUD conditions → NER RNG sequence and behavior expected unchanged.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `src/Core/PackLoader.cs` | shared-effect call; area compile | `try/finally` around body; `CompileArea` rejects engine-kept `vars` keys |
| `src/Core/ExpeditionRun.cs` | run state | `EngineVars`; `rendering` guard on `Roll`, `Options`, `Hud`, `PanelTitle` |
| `src/Core/ExpeditionRun.Api.cs` | `IInventory.Give` | log failed give |
| `src/Core/Builtins.cs` | `Assign`, `spawn` | engine-var problem; out-of-fight branch |
| `tests/Sim/Program.cs` | framework sim | 5 tests; `FakeBridge.Unknown` |
| `README.md` | docs | run vars, render dice |

### Dependencies
- **Requires:** ~~framework repo's first commit (Phase 0; zero commits today — human has not decided)~~ — superseded: done, `8152b9b` on `main`, full tree incl. `.projex/` (human decision, blue team F2). Precondition before `execute-projex`: `git -C <repo-root> ls-files src/Core/PackLoader.cs tests/Sim/Program.cs README.md` lists all three (code is tracked, so Rollback Plan works). Both sims passing.
- **Blocks:** 2611120258-loader-loud-failures-plan.md. Shares `PackLoader.CompileSharedCall`, `tests/Sim/Program.cs`, `README.md` with it. ~~execute sequentially (either order)~~ — superseded: **order fixed — this plan first, loader plan second**, never in parallel (blue team F20: loader Step 3's error-vs-unread-warning test exercises the `Assign` engine-var error path this plan's Step 2 adds).

### Constraints
- NER files read-only; NER sim must still pass.
- `src/Core` stays Unity-free; game build verified with `-p:InstallDir=obj/verify-install/` (avoids copying into the game's `Mods/`).
- No projex references in code comments (SKILL.md § Source Hygiene).

### Assumptions
- A1: `ExpeditionRun.SharedDepthLimit` is 32 (read at planning: `private const int SharedDepthLimit = 32;`); test uses 40 throws.
- A2: Nothing inside `Options()`/`Hud()`/`PanelTitle()` needs a real roll (they only display). Verify by reading `AddOption`, `Hud` part rendering before Step 5.
- A3: The engine-set ambient names below are `run` vars (not entity/decision props). Verify each call site in Step 6 before documenting.

### Impact Analysis
- **Direct:** 4 Core files, sim, README.
- **Adjacent:** C# mods calling `IRun.Roll` from an `On`/interceptor triggered during rendering would now get 1 — none can be (rendering runs no effects).
- **Downstream:** game UI: variant labels stop changing every 0.5 s (always `_0`); a button `when` with dice resolves as if every die rolled 1 (documented). NER: no expected change.

---

## Implementation

### Overview
Five independent fixes, each with its own test, then docs. Order is arbitrary; listed cheapest first.

### Step 1: Shared effect always leaves its recursion level

**Objective:** SC1.
**Confidence:** High
**Depends on:** None
**Do-Projex:** Encouraged

**Files:** `src/Core/PackLoader.cs`, `tests/Sim/Program.cs`

**Changes:**

```csharp
// CompileSharedCall — Before:
return s =>
{
    var run = (ExpeditionRun)s.Run;
    if (!run.EnterShared(name)) return;
    var args = new Dictionary<string, int>();
    foreach (KeyValuePair<string, Value> v in values) args[v.Key] = v.Value(s);
    body[0](Scope.From(s).WithArgs(args));
    run.LeaveShared();
};
// After:
return s =>
{
    var run = (ExpeditionRun)s.Run;
    if (!run.EnterShared(name)) return;
    try
    {
        var args = new Dictionary<string, int>();
        foreach (KeyValuePair<string, Value> v in values) args[v.Key] = v.Value(s);
        body[0](Scope.From(s).WithArgs(args));
    }
    finally
    {
        // Without this, every exception that passes through leaves the run one level closer to refusing all shared effects.
        run.LeaveShared();
    }
};
```

Test `TestSharedEffectSurvivesThrow()`: registry `For("t").AddEffect("boom", spec => s => throw new InvalidOperationException("boom"))`; pack `{effects: {risky: [{"t:boom": {}}], fine: [{set: {var: run.ok, value: 1}}]}, rooms: {r: {text: k}}, areas: {a: {title: k, player: {hp: 1}, layout: [{room: r}]}}}`; `run.Begin()`; compile `{"t:risky": {}}` and `{"t:fine": {}}` with a second `PackLoader` on the same content and the **same `registry`** (the one holding `t:boom` — not `FightForeman`'s pattern, which builds a fresh empty `new Registry()` and would fail with an unknown-effect problem); call `risky(run.RootScope)` 40× inside `try/catch (InvalidOperationException)`; call `fine(run.RootScope)`; `Check(run.Get("ok") == 1 && !run.Log_.Any(l => l.Text.Contains("too deeply")))`.

**Rationale:** Same pattern `Guarded` already uses; exception still propagates (catching data exceptions is out of scope).

**Verification:** V1.

**If this fails:** revert the lambda.

---

### Step 2: Engine-kept `run.*` names can't be assigned

**Objective:** SC2.
**Confidence:** High
**Depends on:** None
**Do-Projex:** Encouraged

**Files:** `src/Core/ExpeditionRun.cs`, `src/Core/Builtins.cs`, `src/Core/PackLoader.cs` (`CompileArea`), `tests/Sim/Program.cs`

**Changes:**

```csharp
// ExpeditionRun.cs, next to Get():
/// <summary>The run.* names the engine answers itself. Writing one would only shadow it in the vars, which nothing reads.</summary>
public static readonly string[] EngineVars = { "depth", "rooms", "rooms_left", "turn", "enemies", "credits", "ground", "scheduled" };

// Builtins.Assign — after the read/write null check:
if (path.StartsWith("run.") && Array.IndexOf(ExpeditionRun.EngineVars, path.Substring(4)) >= 0)
{
    c.Error(path + " is kept by the engine; set and add can't change it");
    return s => { };
}

// PackLoader.CompileArea — right after `d.Vars = Stats(n, "vars", d.Pack, at);` (area vars keys are bare names; Begin() copies them into run vars):
foreach (string name in d.Vars.Keys)
{
    if (Array.IndexOf(ExpeditionRun.EngineVars, name) >= 0) Problem(at + ".vars." + name, "run." + name + " is kept by the engine; vars can't set it");
}
```

C# `IRun.Set` stays unguarded (Accept Risk, blue team F7: the engine itself calls `Set` on non-engine names, and a runtime guard for C# authors is heavier than a no-op shadow) — documented in Step 6 instead.

Test `TestEngineVarsAreReadOnly()`: for each name in `ExpeditionRun.EngineVars`, `ProblemsIn("{\"rooms\":{\"a\":{\"text\":\"k\",\"enter\":[{\"set\":{\"var\":\"run.<name>\",\"value\":1}}]}}}")` has 1 problem containing `run.<name>`; `add` on `run.turn` likewise; `set run.alarm` → 0 problems. Area vars: `ProblemsIn` of a minimal valid area (`title k, player {hp 1}, layout [{room r}]` + room `r`) with `vars {turn: 3}` → exactly 1 problem containing `vars.turn`; same area with `vars {alarm: 7}` → 0 problems. Consistency: a run built from a minimal area where `vars` sets `alarm` to 7 has `Get("alarm") == 7`, and for each `EngineVars` name `Set(name, 999)` leaves `Get(name) != 999` (proves the list matches `Get`'s switch).

**Rationale:** Always a bug, never meaningful → problem, not warning (no dependency on the sibling plan's warning channel). `vars` is pack data with the same audience as `set`/`add` → same severity. Planning grep: no NER or framework content writes these (NER area `vars` = `floors`, `hazard_dc`, `hazard_die`, `search_dc`, `loot_tier`, `tip`, `jackpot`).

**Verification:** V1, V2.

**If this fails:** revert; if NER sim (V2) reports a new problem, NER writes an engine var → Blocked, list the location as an NER follow-up and ask the user (error vs warning).

---

### Step 3: `spawn` outside a fight takes `else`

**Objective:** SC3.
**Confidence:** High
**Depends on:** None
**Do-Projex:** Encouraged

**Files:** `src/Core/Builtins.cs`, `tests/Sim/Program.cs`

**Changes:**

```csharp
// spawn built-in, runtime lambda — Before:
return s =>
{
    IReadOnlyList<string> open = source(s);
    if (open.Count == 0) { none(s); return; }
    ...
// After:
return s =>
{
    if (s.Run.Scene != "combat")
    {
        s.Run.Log("bad", "spawn does nothing outside a fight");
        none(s);
        return;
    }
    IReadOnlyList<string> open = source(s);
    if (open.Count == 0) { none(s); return; }
    ...
```

Test `TestSpawnOutsideFight()`: pack with enemy `rat` (hp, behavior one intent), room `r` whose `enter` is `[{spawn: {enemy: rat, then: [{set: {var: run.then, value: 1}}], else: [{set: {var: run.else, value: 1}}]}}]`; `run.Begin()` → `Get("then") == 0 && Get("else") == 1 && Log_.Any(l => l.Tone == "bad" && l.Text.Contains("outside a fight"))`.

**Rationale:** Matches `ExpeditionRun.Spawn`'s own `Scene != "combat"` guard; nothing arrived, so `else` ("none came") is the honest branch.

**Verification:** V1, V2.

**If this fails:** revert the guard.

---

### Step 4: Log an item the game couldn't give

**Objective:** SC4.
**Confidence:** High
**Depends on:** None
**Do-Projex:** Encouraged

**Files:** `src/Core/ExpeditionRun.Api.cs`, `tests/Sim/Program.cs`

**Changes:**

```csharp
// IInventory.Give — Before:
string name = itemId != null ? Bridge.GiveItem(itemId) : null;
Found(name);
return name;
// After:
string name = itemId != null ? Bridge.GiveItem(itemId) : null;
// GameBridge.GiveItem treats "" as nothing to give, so an empty id is an empty roll, not a failed give.
if (!string.IsNullOrEmpty(itemId) && name == null) Log("bad", "could not give " + itemId + " (unknown item id, or no room on the ground)");
Found(name);
return name;
```

~~Guard `itemId != null && name == null`~~ — superseded (blue team F3): `""` (empty `GameTable` roll or a `loot` interceptor blanking the id) would log a false `could not give  (…)`.

Framework sim `FakeBridge`: add `public readonly HashSet<string> Unknown = new HashSet<string>();` and `GiveItem(string itemId) => string.IsNullOrEmpty(itemId) || Unknown.Contains(itemId) ? null : itemId;` (mirrors `GameBridge.GiveItem`). In `TestPublicApi` keep the bridge in a local, `bridge.Unknown.Add("ghost")`, then `Check(api.Inventory.Give("ghost") == null && run.Log_.Last().Text.Contains("ghost"))`; then `int lines = run.Log_.Count; api.Inventory.Give(""); api.Inventory.Give(null);` → `Check(run.Log_.Count == lines)` (silent for empty/null). Existing `Give("thing")` check unchanged.

**Rationale:** `GiveItem` null covers unknown id and full ground (`GameBridge.PutOnGround`); both are worth a visible line. A null or empty `itemId` (empty loot roll) stays silent — matches `GameBridge.GiveItem`'s own `string.IsNullOrEmpty` contract.

**Verification:** V1, V2.

**If this fails:** revert.

---

### Step 5: Rendering never rolls

**Objective:** SC5.
**Confidence:** Medium — must wrap only render entry points.
**Depends on:** None
**Verify-Projex:** Encouraged — touches what every button and HUD line evaluates.

**Files:** `src/Core/ExpeditionRun.cs`, `tests/Sim/Program.cs`

**Changes:**

```csharp
// ExpeditionRun.cs — Before:
public int Roll(int sides) => sides <= 0 ? 0 : roll(sides);
// After:
// While the UI asks what to show, dice read as 1 and variants as the first, like MapContext: the game redraws every
// half second, so real rolls here would flicker labels and use up the run's randomness.
private int rendering;
public int Roll(int sides) => sides <= 0 ? 0 : rendering > 0 ? 1 : roll(sides);

// Options(), Hud(), PanelTitle(): rename each existing body to a private BuildOptions()/BuildHud()/BuildPanelTitle(areaName)
// and make the public method:
public List<RunOption> Options()
{
    rendering++;
    try { return BuildOptions(); }
    finally { rendering--; }
}
// same for Hud() and PanelTitle(string areaName).
```

Test `TestRenderingDoesNotRoll()`: roll delegate `n => { rolls++; return n; }`; pack room `r` with interaction `{id: v, label: {text: k, variants: 3}, when: "1d6 >= 1", do: []}`, area with `hud {rows: [{when: "1d6 > 0", parts: ["k"]}], enemies: {line: "k"}}`; localizer returns the key; `run.Begin()`; `int before = rolls;` call `Options()`, `Hud()`, `PanelTitle("x")` 10× each → `rolls == before` and every `Options()` label for `v` equals `"k_0"`.

Check A2 first: read `AddOption` and the `Hud` row/part path; confirm they only evaluate conditions/lines (no effects).

**Rationale:** Single choke point (`Roll`) + three entry guards; mirrors existing `MapContext.Roll`. Real actions (`Perform`) run outside the guard and roll normally.

**Verification:** V1, V2.

**If this fails:** revert `Roll` and the three wrappers.

---

### Step 6: README — run variables and render dice

**Objective:** SC7.
**Confidence:** High
**Depends on:** Steps 2, 3, 5
**Do-Projex:** Encouraged

**Files:** `README.md`

**Changes:** § Vocabulary, replace "`run` also has `depth`, `rooms`, `rooms_left`, `turn`, `enemies` and `credits`." with: `run` also has `depth`, `rooms`, `rooms_left`, `turn`, `enemies`, `credits`, `ground` and `scheduled`, kept by the engine — `set`/`add` on them, or an area's `vars` naming them, is a problem. In § The C# API (near `IRun`): "`Set` on a name the engine keeps does nothing — `Get` keeps answering from the engine." Then a sentence listing the names the engine sets for data to read — candidates found at planning by grepping `Set("…")`/`SetText("…")` in `src/Core`: `won`, `last_damage`, `healed`, `given`, `found_items`, `natural`, `total`, `trade_cash`, `trade_bought`, `cart_taken`, `cleared_ground`, `outcome_<name>`, texts `$lost`, `$taken`, `$scheduled`, `$pick`. Include a name only after reading its call site and confirming it targets the run (A3). Add: "Labels, `when`/`where` on buttons, previews and HUD lines are drawn without rolling: dice count as 1 and `variants` show the first text. So a button whose `when` uses `chance` always shows, and one that needs a high roll never shows (the gate isn't checked again when pressed) — roll into a `run.` var when entering the room and test that instead." Under § Effects (or near the effects table): "`spawn` outside a fight does nothing and runs its `else`."

Each V4 probe phrase (`` `ground` ``, `kept by the engine`, `count as 1`, `never shows`, `outside a fight`) must sit on one README line — README is hard-wrapped, and a phrase split across lines fails `grep -F`.

**Verification:** V4; judgment: names verified; `vars` and `IRun`/`Set` sentences present.

**If this fails:** revert README.

---

## Verification Plan

Commands from the framework repo root.

### Deterministic Suite

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V1 | SC1–SC5, Steps 1–5 | `dotnet run -c Release --project tests/Sim` | exit 0; last line `All checks passed` | each fix's test; regressions |
| V2 | SC6 | `dotnet run -c Release --project ../NightExpeditionsReborn/tests/Sim` | exit 0; last line `All checks passed` | NER broken by engine-var problem, spawn branch, render change |
| V3 | SC6 | `dotnet build -c Release ExpeditionFramework.csproj -p:InstallDir=obj/verify-install/` | exit 0 | game assembly compile break |
| V4 | SC7 | ``for p in '`ground`' "kept by the engine" "count as 1" "never shows" "outside a fight"; do grep -c -F -e "$p" README.md; done`` | five counts, each ≥ 1 (all five are 0 at `8152b9b`) | docs step skipped |
| ~~V5~~ | ~~SC1~~ | ~~`grep -c "finally" src/Core/PackLoader.cs`~~ | — | superseded: matches any future `finally`; SC1 evidence is V1's `TestSharedEffectSurvivesThrow` alone |

### Judgment Checks

| Check | Covers | Why not deterministic |
|-------|--------|-----------------------|
| Ambient var list verified per call site | SC7 | requires reading whether each `Set` targets run vs entity/decision |
| README states area `vars` rule and C# `Set` no-op on engine names | SC7 | prose |
| In game: variant labels stable, no stray `bad` lines on normal play | SC5, SC3, SC4 | needs the game |

### Acceptance Criteria Validation

| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| SC1 | V1 (`TestSharedEffectSurvivesThrow`) | working shared effect after 40 throws |
| SC2 | V1 (`TestEngineVarsAreReadOnly`), V2 | problem per engine name (`set`/`add` and area `vars`); list matches `Get` |
| SC3 | V1 (`TestSpawnOutsideFight`) | else ran, then not, one bad line |
| SC4 | V1 (`TestPublicApi` addition) | bad line names `ghost`; `Give("")`/`Give(null)` add no line |
| SC5 | V1 (`TestRenderingDoesNotRoll`) | zero rolls; `k_0` label |
| SC6 | V1, V2, V3 | all exit 0 |
| SC7 | V4, judgment | documented, verified names |

---

## Rollback Plan

1. Revert the 6 files to the pre-execution commit (or `projex-abandon` the ephemeral branch).
2. Rerun V1 and V2 to confirm baseline.

---

## Revision Log

- **2026-11-12:** Applied blue team Must Fix items owned by this plan — trigger: 2611120507-phase1-hardening-plans-blueteam.md § Defense Plan (dispositions settled by human + orchestrator). F2: Dependencies record first commit `8152b9b` (full tree) + `ls-files` precondition. F20: order fixed (this plan first; Blocks loader plan). F3: Step 4 guard → `!string.IsNullOrEmpty(itemId)`, `FakeBridge` mirrors `GameBridge`, `Give("")`/`Give(null)` silent check; SC4 reworded. F7: Step 2 adds `CompileArea` `vars` engine-name problem + test; `IRun.Set` Accept Risk documented (Step 6, Risks); SC2/SC7/Scope/Key Files updated. F8: Step 6 README sentence spells out `chance`-always-shows / high-roll-never-shows + `run.`-var pattern; Risks updated. F10: Step 1 test note drops misleading `FightForeman` reference (same `registry`); V4 probes replaced (old `rooms_left`/`scheduled` already matched at `8152b9b`); V5 superseded; one-line probe rule added to Step 6. Status stays `Ready` — all decisions recorded.

---

## Notes

### Risks
- Step 5 changes what players see: a button whose `when` uses dice now shows as if all dice rolled 1 (previously random per refresh) — `chance` gates always open, high-roll gates always closed, and `Perform` doesn't recheck `when`. No shipped content does this (planning grep; red team re-verified); documented with the `run.`-var pattern (Step 6). Load-time warning on dice in `when` rejected — new code for a hazard no content has.
- C# `IRun.Set` on an engine-kept name silently shadows (Accept Risk); reverse when the first C# pack writes engine names.
- Step 2 could break a third-party pack that writes engine names: it was already a no-op; the problem surfaces the bug. Escalation path in Step 2.
- New `bad` log lines are English-only, like existing runtime content-error lines.

### Split decision
`No split — single scope, within size budget` (6 steps, all independent one-site fixes; ~300 lines). Sibling 2611120258-loader-loud-failures-plan.md holds the loader half of Phase 1.

### Open Questions
- (none)

### Decisions Recorded
- Phase 0: full-tree first commit `8152b9b` on `main` (human, blue team F2).
- Execution order: this plan first, then 2611120258-loader-loud-failures-plan.md (blue team F20). Worktree: No (unchanged).
- Loader-plan decisions (SC8 fix, wrong-typed scalars = problems, misplaced `weight`/`when`/`tags` = warnings) are recorded in the sibling; none affect this plan's steps.
