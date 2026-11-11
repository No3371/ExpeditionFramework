# Runtime Safety Fixes — Shared-Effect Leak, Engine Vars, Spawn, Give, Render-Time Dice

> **Status:** Ready
> **Author:** Opus 5.5 (plan-projex subagent, dispatched by orchestrate-projex)
> **Source:** Direct request ("Plan hardening first") — Phase 1 of 2611120250-framework-expansion-synthesis-eval.md § Recommended Sequence (Runtime R13 + § Incidental Bugs)
> **Parent:** 2611120250-framework-expansion-synthesis-eval.md
> **Related Projex:** 2611120258-loader-loud-failures-plan.md (sibling, Phase 1 loader half) | 2611120227-runtime-hooks-expr-expansion-explore.md (R13, silent traps)
> **Worktree:** No — NightExpeditionsReborn's sim compiles `../../../ExpeditionFramework/src/Core` by relative path, so it only tests the main checkout.

---

## Summary

Five small runtime fixes from R13, each a one-site change plus a sim test: a shared effect that throws no longer leaks its recursion counter; `set`/`add` on engine-kept `run.*` names becomes a load problem instead of a silent no-op; `spawn` outside a fight logs and takes its `else` branch instead of running `then` with an enemy that never arrived; giving an item the game can't create is logged; rendering buttons/HUD no longer rolls dice (no flicker, no RNG drain). README documents the engine-kept and engine-set `run.*` names.

**Scope:** `src/Core/PackLoader.cs` (`CompileSharedCall` only), `src/Core/ExpeditionRun.cs`, `src/Core/ExpeditionRun.Api.cs`, `src/Core/Builtins.cs`, `tests/Sim/Program.cs`, `README.md`.
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
- [ ] SC2 `set`/`add` with `var` `run.<name>` for each engine-kept name → load problem naming the var; `ExpeditionRun.EngineVars` lists exactly the names `Get` answers from engine state.
- [ ] SC3 `spawn` outside a fight: `then` not run, `else` run, one `bad` log line.
- [ ] SC4 `Inventory.Give(id)` returning null for a non-null id logs one `bad` line naming the id.
- [ ] SC5 `Options()`, `Hud()`, `PanelTitle()` never call the run's roll delegate; a `variants` label renders as `_0`.
- [ ] SC6 Framework sim and NER sim exit 0; game assembly builds.
- [ ] SC7 README lists engine-kept `run.*` names (read-only) and engine-set ambient names, and notes render-time dice.

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
| `src/Core/PackLoader.cs` | shared-effect call | `try/finally` around body |
| `src/Core/ExpeditionRun.cs` | run state | `EngineVars`; `rendering` guard on `Roll`, `Options`, `Hud`, `PanelTitle` |
| `src/Core/ExpeditionRun.Api.cs` | `IInventory.Give` | log failed give |
| `src/Core/Builtins.cs` | `Assign`, `spawn` | engine-var problem; out-of-fight branch |
| `tests/Sim/Program.cs` | framework sim | 5 tests; `FakeBridge.Unknown` |
| `README.md` | docs | run vars, render dice |

### Dependencies
- **Requires:** framework repo's first commit (Phase 0; zero commits today — plan must be committed on `main` before `execute-projex`; human has not decided). Both sims passing.
- **Blocks:** none. Shares `PackLoader.CompileSharedCall`, `tests/Sim/Program.cs`, `README.md` with 2611120258-loader-loud-failures-plan.md → execute sequentially (either order), not in parallel.

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

Test `TestSharedEffectSurvivesThrow()`: registry `For("t").AddEffect("boom", spec => s => throw new InvalidOperationException("boom"))`; pack `{effects: {risky: [{"t:boom": {}}], fine: [{set: {var: run.ok, value: 1}}]}, rooms: {r: {text: k}}, areas: {a: {title: k, player: {hp: 1}, layout: [{room: r}]}}}`; `run.Begin()`; compile `{"t:risky": {}}` and `{"t:fine": {}}` with a second `PackLoader` on the same content/registry (as `FightForeman` does); call `risky(run.RootScope)` 40× inside `try/catch (InvalidOperationException)`; call `fine(run.RootScope)`; `Check(run.Get("ok") == 1 && !run.Log_.Any(l => l.Text.Contains("too deeply")))`.

**Rationale:** Same pattern `Guarded` already uses; exception still propagates (catching data exceptions is out of scope).

**Verification:** V1.

**If this fails:** revert the lambda.

---

### Step 2: Engine-kept `run.*` names can't be assigned

**Objective:** SC2.
**Confidence:** High
**Depends on:** None
**Do-Projex:** Encouraged

**Files:** `src/Core/ExpeditionRun.cs`, `src/Core/Builtins.cs`, `tests/Sim/Program.cs`

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
```

Test `TestEngineVarsAreReadOnly()`: for each name in `ExpeditionRun.EngineVars`, `ProblemsIn("{\"rooms\":{\"a\":{\"text\":\"k\",\"enter\":[{\"set\":{\"var\":\"run.<name>\",\"value\":1}}]}}}")` has 1 problem containing `run.<name>`; `add` on `run.turn` likewise; `set run.alarm` → 0 problems. Consistency: a run built from a minimal area where `vars` sets `alarm` to 7 has `Get("alarm") == 7`, and for each `EngineVars` name `Set(name, 999)` leaves `Get(name) != 999` (proves the list matches `Get`'s switch).

**Rationale:** Always a bug, never meaningful → problem, not warning (no dependency on the sibling plan's warning channel). Planning grep: no NER or framework content writes these.

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
if (itemId != null && name == null) Log("bad", "could not give " + itemId + " (unknown item id, or no room on the ground)");
Found(name);
return name;
```

Framework sim `FakeBridge`: add `public readonly HashSet<string> Unknown = new HashSet<string>();` and `GiveItem(string itemId) => Unknown.Contains(itemId) ? null : itemId;`. In `TestPublicApi` keep the bridge in a local, `bridge.Unknown.Add("ghost")`, then `Check(api.Inventory.Give("ghost") == null && run.Log_.Last().Text.Contains("ghost"))`. Existing `Give("thing")` check unchanged.

**Rationale:** `GiveItem` null covers unknown id and full ground (`GameBridge.PutOnGround`); both are worth a visible line. A null `itemId` (empty loot roll) stays silent.

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
**Depends on:** Steps 2, 5
**Do-Projex:** Encouraged

**Files:** `README.md`

**Changes:** § Vocabulary, replace "`run` also has `depth`, `rooms`, `rooms_left`, `turn`, `enemies` and `credits`." with: `run` also has `depth`, `rooms`, `rooms_left`, `turn`, `enemies`, `credits`, `ground` and `scheduled`, kept by the engine — `set`/`add` on them is a problem. Then a sentence listing the names the engine sets for data to read — candidates found at planning by grepping `Set("…")`/`SetText("…")` in `src/Core`: `won`, `last_damage`, `healed`, `given`, `found_items`, `natural`, `total`, `trade_cash`, `trade_bought`, `cart_taken`, `cleared_ground`, `outcome_<name>`, texts `$lost`, `$taken`, `$scheduled`, `$pick`. Include a name only after reading its call site and confirming it targets the run (A3). Add: "Labels, `when`/`where` on buttons, previews and HUD lines are drawn without rolling: dice count as 1 and `variants` show the first text." Under § Effects (or near the effects table): "`spawn` outside a fight does nothing and runs its `else`."

**Verification:** V4; judgment: names verified.

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
| V4 | SC7 | `for p in "rooms_left" "scheduled" "kept by the engine" "count as 1" "outside a fight"; do grep -c -F -e "$p" README.md; done` | five counts, each ≥ 1 | docs step skipped |
| V5 | SC1 | `grep -c "finally" src/Core/PackLoader.cs` | ≥ 1 | Step 1 not applied |

### Judgment Checks

| Check | Covers | Why not deterministic |
|-------|--------|-----------------------|
| Ambient var list verified per call site | SC7 | requires reading whether each `Set` targets run vs entity/decision |
| In game: variant labels stable, no stray `bad` lines on normal play | SC5, SC3, SC4 | needs the game |

### Acceptance Criteria Validation

| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| SC1 | V1 (`TestSharedEffectSurvivesThrow`), V5 | working shared effect after 40 throws |
| SC2 | V1 (`TestEngineVarsAreReadOnly`), V2 | problem per engine name; list matches `Get` |
| SC3 | V1 (`TestSpawnOutsideFight`) | else ran, then not, one bad line |
| SC4 | V1 (`TestPublicApi` addition) | bad line names `ghost` |
| SC5 | V1 (`TestRenderingDoesNotRoll`) | zero rolls; `k_0` label |
| SC6 | V1, V2, V3 | all exit 0 |
| SC7 | V4, judgment | documented, verified names |

---

## Rollback Plan

1. Revert the 6 files to the pre-execution commit (or `projex-abandon` the ephemeral branch).
2. Rerun V1 and V2 to confirm baseline.

---

## Notes

### Risks
- Step 5 changes what players see: a button whose `when` uses dice now shows as if all dice rolled 1 (previously random per refresh). No shipped content does this (planning grep); documented.
- Step 2 could break a third-party pack that writes engine names: it was already a no-op; the problem surfaces the bug. Escalation path in Step 2.
- New `bad` log lines are English-only, like existing runtime content-error lines.

### Split decision
`No split — single scope, within size budget` (6 steps, all independent one-site fixes; ~300 lines). Sibling 2611120258-loader-loud-failures-plan.md holds the loader half of Phase 1.

### Open Questions
- (none)
