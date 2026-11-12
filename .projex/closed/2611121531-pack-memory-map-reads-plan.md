# Pack Memory: Map-Time Reads

> **Status:** Complete
> **Author:** Claude Opus 5.5 (plan)
> **Parent:** 2611121459-pack-memory-sprint.md
> **Sprint:** 2611121459-pack-memory-sprint.md — iteration 2
> **Source:** Sprint iteration objective (`2611121459-pack-memory-sprint.md` § Current Position, item 2)
> **Related Projex:** 2611121459-pack-memory-sprint.md | 2611121506-pack-memory-runtime-store-plan.md | 2611121522-pack-memory-runtime-store-audit.md
> **Executed:** [PATCHED] all steps via 2611121537-pack-memory-map-reads-patch.md (code commit 684b090)
> **Worktree:** No — execute in the sprint work-root on `projex/sprint/2611121459-pack-memory`

---

## Summary

Makes `memory.NAME` / `memory.$NAME` readable on the night map. `MapContext` gets an optional `IMemory` and becomes an `IMemoryHost`, so area `hidden_when` and intel line `cases` read the pack's memory; the game passes its save-backed `SaveMemory`. Intel slots gain an optional `when` condition, checked on the map when the day's intel is rolled: false → slot names nothing that day. Area unlock by memory = the existing `hidden_when` (now memory-aware); no new area field (see Decision D1). Proven by one new framework sim test.

**Scope:** `src/Core` (ExpeditionRun.cs `MapContext`/`IMemoryHost` doc/`IntelRoller`, Model.cs `IntelSlot`, PackLoader.cs `CompileIntel`, Schema.cs `Intel`) + `src/Game` (ExpeditionLocation.cs, IntelCards.cs) + `tests/Sim/Program.cs`. One projex scope (repo root `.projex/`).
**Estimated Changes:** 7 files; 1 ctor + 1 explicit interface member, 1 roller param + 1 guard, 1 model field, 1 loader line, 1 schema field, 2 game call sites, 1 new test fn.

---

## Objective

### Problem / Gap / Need
`MapContext` is not an `IMemoryHost`, so `Expr.MemoryOf` returns null on the map: `memory.*` reads 0 / `""` in `hidden_when` and intel line cases. Intel slots have no condition at all, so a pack can't make the street talk about something only after a remembered event.

### Success Criteria
- [ ] SC1: A `MapContext` built with an `IMemory` resolves `memory.NAME` / `memory.$NAME` of the condition's pack: area `hidden_when` and intel line `cases` `when` see store values; a `MapContext` built without one reads 0 / `""` (unchanged behavior for existing callers).
- [ ] SC2: Intel slot `when` (any condition form, compiled with the area's pack) loads with no problem/warning; `IntelRoller.Roll` gives a slot whose `when` is false on the given scope an empty pick list and rolls normally when true; slots without `when` roll exactly as before.
- [ ] SC3: Game: `ExpeditionLocation` builds its `MapContext` with a `SaveMemory`; `IntelCards` rolls with `map.Root`.
- [ ] SC4: All existing gates stay green: framework `tests/Sim`, game-assembly build (0 warnings/0 errors), `ef-sim lint --strict` on sample + NER, `ef-sim run` on sample, NER 4000-run table identical to baseline.

### Out of Scope
- New area field `unlocked_when` / changes to `unlocked_by` (D1).
- `game.day` / `game.clears` — iteration 3.
- ef-sim: sim.json memory seed/carry/expectation, lint read-but-never-written; `tools/EfSim/PackRun.cs` `MapContext` stays store-less (packing checks read no memory) — iteration 4.
- README (intel `when`, memory in `hidden_when`, `IntelRoller.Roll`/`MapContext` C# signature notes) — iteration 5. Carry this list forward.
- Map-time memory **writes**: map has no effects (intel `effect` is a rendered line), so writes there have no data path; `MapContext` forwards them to the store anyway via the shared `Expr` writer, no special handling.
- Patched `hidden_when` / intel `when` compiled under a patching pack's id (patch-pack attribution of memory) — existing patch semantics, untouched.

### Decision D1 — area unlock condition = memory-aware `hidden_when`
Nav position item 2 says "areas gain an unlock condition". `hidden_when` already gates `IsDiscovered` and ANDs with `unlocked_by` (`ExpeditionLocation.IsDiscovered`). Once `MapContext` reads memory, `"hidden_when": "memory.debt_paid < 1"` is exactly a memory unlock condition. A separate `unlocked_when` would be `!hidden_when` under a second name: more schema, docs and lint surface, zero new capability (YAGNI/KISS). The goal criterion "read on the map (intel and area-unlock conditions)" is met by SC1 + SC2. Iteration 5 README should show `hidden_when` as the unlock-by-memory idiom.

---

## Context

### Current State
- `src/Core/ExpeditionRun.cs`:
  - L126–133: `internal interface IMemoryHost { IMemory Memory { get; } }`; its doc says "The map has no store, so memory there reads 0 and "" and writes are dropped."
  - L179: `ExpeditionRun : IRun, ITextHost, IMemoryHost`; L353 `IMemory IMemoryHost.Memory => Bridge.Memory;`
  - L1097–1111: `public sealed class MapContext : IRun, ITextHost`, fields `localize`, `game`, `texts`, `nobody`; ctor `MapContext(string areaId, Localizer localize, Func<string, int> game)` sets `Root = new Scope(this, nobody, null, null)`. L1151 `public string Render(Line line) => Lines.Render(line, Root);`
  - L1171–1192: `public static class IntelRoller`, `Roll(AreaDef area, Func<int, int> roll)`: per slot `result[slot.Id] = picks;` then `if (!area.Pools.TryGetValue(...)) continue;` then rolls `HowMany` and picks.
- `src/Core/Expr.cs` L98: `MemoryOf(IScope s) => (s.Run as IMemoryHost)?.Memory;` — all memory heads go through it; no change needed.
- `src/Core/Model.cs` L245–260: `IntelSlot` (Id, Pool, Chance, Counts, Title, Description, Effect, Tone, GoodPicks, MaxPicks). `AreaDef.HiddenWhen` (`Condition`).
- `src/Core/PackLoader.cs` L964–983 `CompileIntel(Node n, AreaDef area, string at)`: `pack = area.Pack`, `CheckShape(n, Schema.Intel, at)`, builds slot. `Condition(object node, string pack, string at)` (L1460) compiles with the pack → memory heads compile. Line `cases` `when` also compile via `Condition(entry["when"], pack, …)` (L1302).
- `src/Core/Schema.cs` L62–63: `Intel` shape: id, pool, chance, counts, title, description, effect, tone, good. Unknown keys → warning (lint `--strict` fails on it).
- `src/Game/ExpeditionLocation.cs` L59: `map = new MapContext(area.Id, (pack, key, args) => ModHelper.GetLocalized(pack, key, args), GameValues.Get);` L60 `intel = new IntelCards(area, Id, map);` L101–106 `IsDiscovered`: `hidden_when` on `map.Root`, then `unlocked_by` vs `ef_clears`.
- `src/Game/IntelCards.cs` L108 (in `Rolled()`): `IntelRoller.Roll(area, sides => RNG.GetRandomInt(1, sides));` — once per game day; later loop reads `picks[slot.Id]` for every slot (so every slot must stay a key). Field `map` already held.
- `src/Game/GameBridge.cs` L209–222: `internal sealed class SaveMemory : IMemory`, stateless (each call goes to `ModHelper`). Decompiled `ModHelper.GetModData` uses `ModDataStore(warn: false)` and returns fallback with no save → map-time reads never warn.
- `IntelRoller.Roll` callers: only `IntelCards.Rolled` (grep: no tests/tools call it). `MapContext` ctor callers: `ExpeditionLocation.cs:59`, `tools/EfSim/PackRun.cs:87`, `tests/Sim/Program.cs:334,797`.
- NER uses `intel` (no `when`) and has no `memory.` usage; ef-sim never rolls intel (no `Intel`/`IntelRoller` in `tools/EfSim`) → NER balance cannot move.
- `tests/Sim/Program.cs`: `Main` test list L26–43 ends `TestMemory();`. L502–503 two stacked comments above `TestMemory` — L502 ("An ammo item is worth its `rounds`…") belongs to `TestReloadUsesWholeItems` (L554, which has no comment): iteration-1 misplacement, fixed here while touching the file.
- Baseline at plan time (HEAD `f00d8ae`): `tests/Sim` → `All checks passed`; worktree clean.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `src/Core/ExpeditionRun.cs` | `MapContext`, `IMemoryHost`, `IntelRoller` | MapContext takes optional `IMemory`, implements `IMemoryHost`; IMemoryHost doc; `Roll` takes scope, skips slot whose `When` fails |
| `src/Core/Model.cs` | `IntelSlot` | add `Condition When` |
| `src/Core/PackLoader.cs` | `CompileIntel` | compile `when` |
| `src/Core/Schema.cs` | `Intel` shape | add `when` |
| `src/Game/ExpeditionLocation.cs` | game map card | pass `new SaveMemory()` |
| `src/Game/IntelCards.cs` | daily roll | pass `map.Root` to `Roll` |
| `tests/Sim/Program.cs` | framework tests | new `TestMapMemory`; move misplaced comment |

### Dependencies
- **Requires:** iteration 1 (`IMemory`, `MemoryStore`, `IMemoryHost`, `SaveMemory`, pack-threaded memory heads) — on branch.
- **Blocks:** iteration 5 sample/README (documents these fields).

### Constraints
- `IMemoryHost` stays internal; `MapContext` implements it explicitly (public class implementing internal interface is legal C#).
- Existing `MapContext` callers must compile unchanged → new ctor param optional, default null.
- Game build runs `-warnaserror`; don't add `<param>` XML tags to only some ctor params (CS1573 risk). Keep doc in `<summary>`.
- Source Hygiene (SKILL.md): no projex ids / step labels / "iteration" in code comments; non-obvious decisions carry rationale naming the rejected alternative.

### Assumptions
- A1: `Lines.Render` of a `cases` line evaluates `LineCase.When(scope)` with the `Root` scope passed → memory in cases works once `MapContext` is an `IMemoryHost`. Verified by test (SC1 assertion on `Render`).
- A2: `PackSet.Compile(out problems, out warnings)` reports an unknown intel key as a warning under the pack → the test's zero-warnings check proves the `Schema.Intel` edit. (`PackSet.cs` L39–69.)
- A3: Intel pool may hold room ids; `CompileIntel` doesn't check pool id kinds. If loading the test pack reports a problem about the pool, change the pool's entry to whatever kind the message asks for — not the engine.

### Impact Analysis
- **Direct:** files above.
- **Adjacent:** `Expr.MemoryOf` (now non-null on map), `Lines.Render` on map, `IsDiscovered`, `IntelCards.Rolled/Today/EnsureToday` (consume roll result; empty list for gated slot = existing "no pick" path → card ended / not started, absent from `run.Intel`).
- **Downstream:** public C# signature changes: `MapContext` ctor (additive, optional) and `IntelRoller.Roll` (new required param — breaking for any external caller; none in repo). README note in iteration 5.

---

## Implementation

### Overview
Core first (store on map, intel `when`), then game wiring, then test. Each step compiles on its own except Step 2→3 (roller signature change and its game caller); run V1 after Step 4, V2 after Step 3.

### Step 1: MapContext reads memory
**Objective:** `MapContext` carries an optional store and is an `IMemoryHost`.
**Confidence:** High
**Depends on:** None
**Do-Projex:** Encouraged

**Files:** `src/Core/ExpeditionRun.cs`

**Changes:**

```csharp
// Before (L126-129):
    /// <summary>
    /// Where memory.* paths find the store. Kept off IRun, which is the public API: putting it there would make every
    /// IRun implementation outside this assembly provide a store. The map has no store, so memory there reads 0 and "" and writes are dropped.
    /// </summary>

// After:
    /// <summary>
    /// Where memory.* paths find the store. Kept off IRun, which is the public API: putting it there would make every
    /// IRun implementation outside this assembly provide a store. A run that isn't a host, or a host without a store, reads
    /// memory as 0 and "" and drops writes.
    /// </summary>
```

```csharp
// Before (L1097-1111):
    /// <summary>Lets the map show lines and check conditions when no run is going.</summary>
    public sealed class MapContext : IRun, ITextHost
    {
        private readonly Localizer localize;
        private readonly Func<string, int> game;
        private readonly Dictionary<string, string> texts = new Dictionary<string, string>();
        private readonly Entity nobody = new Entity("nobody", "", true, new string[0]);

        public MapContext(string areaId, Localizer localize, Func<string, int> game)
        {
            AreaId = areaId;
            this.localize = localize;
            this.game = game;
            Root = new Scope(this, nobody, null, null);
        }

// After:
    /// <summary>
    /// Lets the map show lines and check conditions when no run is going. Given a memory, memory.* there reads the same
    /// store runs write, so a condition can hide an area or quiet the street until something has happened on an earlier night.
    /// Without one, memory reads 0 and "".
    /// </summary>
    public sealed class MapContext : IRun, ITextHost, IMemoryHost
    {
        private readonly Localizer localize;
        private readonly Func<string, int> game;
        private readonly IMemory memory;
        private readonly Dictionary<string, string> texts = new Dictionary<string, string>();
        private readonly Entity nobody = new Entity("nobody", "", true, new string[0]);

        public MapContext(string areaId, Localizer localize, Func<string, int> game, IMemory memory = null)
        {
            AreaId = areaId;
            this.localize = localize;
            this.game = game;
            this.memory = memory;
            Root = new Scope(this, nobody, null, null);
        }
```

And add, next to the existing explicit `ITextHost.Localize` member (L1149):

```csharp
        IMemory IMemoryHost.Memory => memory;
```

**Rationale:** Same seam runs use (`IMemoryHost`), so `Expr` needs no change. Optional param keeps `PackRun.cs` and existing tests compiling; store-less map keeps today's 0/"" behavior for them. Store passed in (not a bridge) because the map has no bridge.

**Verification:** V1 (TestMapMemory SC1 asserts), V2.

**If this fails:** `git -C <work-root> checkout -- src/Core/ExpeditionRun.cs`.

---

### Step 2: Intel slot `when`
**Objective:** Intel slots accept `when`; the roller skips a slot whose `when` fails on the given scope.
**Confidence:** High
**Depends on:** None (Step 3 must follow before a game build)
**Verify-Projex:** Encouraged

**Files:** `src/Core/Model.cs`, `src/Core/Schema.cs`, `src/Core/PackLoader.cs`, `src/Core/ExpeditionRun.cs`

**Changes:**

`src/Core/Model.cs` — in `IntelSlot`, after `public HashSet<string> GoodPicks …;`:
```csharp
        /// <summary>Whether the street talks about this at all today, checked on the map when the day is rolled. Null means always.</summary>
        public Condition When;
```

`src/Core/Schema.cs` L62–63:
```csharp
// Before:
        public static readonly Shape Intel = new Shape(("id", Field.Text), ("pool", Field.Text), ("chance", Field.Number), ("counts", Field.Numbers),
            ("title", Field.Any), ("description", Field.Any), ("effect", Field.Any), ("tone", Field.IntelTone), ("good", Field.Texts));
// After:
        public static readonly Shape Intel = new Shape(("id", Field.Text), ("pool", Field.Text), ("chance", Field.Number), ("counts", Field.Numbers),
            ("title", Field.Any), ("description", Field.Any), ("effect", Field.Any), ("tone", Field.IntelTone), ("good", Field.Texts), ("when", Field.Any));
```

`src/Core/PackLoader.cs` `CompileIntel` — after the `slot.GoodPicks.UnionWith(...)` line, before `return slot;`:
```csharp
            if (n.ContainsKey("when")) slot.When = Condition(n["when"], pack, at + ".when");
```

`src/Core/ExpeditionRun.cs` `IntelRoller`:
```csharp
// Before:
        /// <summary>For each slot, the positions in its pool that were picked. <paramref name="roll"/> gives 1 to sides.</summary>
        public static Dictionary<string, List<int>> Roll(AreaDef area, Func<int, int> roll)
        {
            var result = new Dictionary<string, List<int>>();
            foreach (IntelSlot slot in area.Intel)
            {
                var picks = new List<int>();
                result[slot.Id] = picks;
                if (!area.Pools.TryGetValue(slot.Pool, out List<string> pool)) continue;

// After:
        /// <summary>
        /// For each slot, the positions in its pool that were picked. <paramref name="roll"/> gives 1 to sides. A slot whose
        /// when doesn't hold at <paramref name="at"/> picks nothing and rolls no dice, so adding a when to one slot doesn't
        /// shift what the others pick.
        /// </summary>
        public static Dictionary<string, List<int>> Roll(AreaDef area, Func<int, int> roll, IScope at)
        {
            var result = new Dictionary<string, List<int>>();
            foreach (IntelSlot slot in area.Intel)
            {
                var picks = new List<int>();
                result[slot.Id] = picks;
                if (slot.When != null && !slot.When(at)) continue;
                if (!area.Pools.TryGetValue(slot.Pool, out List<string> pool)) continue;
```
(Rest of `Roll` and `HowMany` unchanged. Add the `<paramref name="at"/>` only inside `<summary>`, no `<param>` tags.)

**Rationale:** Gate at roll time, in Core: one check per day, deterministic and testable without the game; the gated slot still gets an (empty) entry, so `IntelCards.Rolled`'s `picks[slot.Id]` lookup and its "no pick" path (counter 0 → card ended, absent from `Today()`/`run.Intel`) work unchanged. Rejected: gating `canStart` in `IntelCards` — evaluated on every card check, would let a card appear/disappear mid-day while `Today()` (from `IsActive`) and the shown cards disagree, and is game-only so untestable here. Field named `when` as every other condition key (`hud` rows, line `cases`, `if`). Required `at` param (not optional) so no caller silently skips every `when` slot.

**Verification:** V1 (TestMapMemory SC2 asserts), V4 (sample/NER still 0 warnings — schema change didn't break shapes).

**If this fails:** `git -C <work-root> checkout -- src/Core/Model.cs src/Core/Schema.cs src/Core/PackLoader.cs src/Core/ExpeditionRun.cs` (reapply Step 1 if reverted together).

---

### Step 3: Game wiring
**Objective:** The game map reads save memory and rolls intel against it.
**Confidence:** High
**Depends on:** Steps 1, 2
**Do-Projex:** Encouraged

**Files:** `src/Game/ExpeditionLocation.cs`, `src/Game/IntelCards.cs`

**Changes:**

```csharp
// ExpeditionLocation.cs L59 — Before:
            map = new MapContext(area.Id, (pack, key, args) => ModHelper.GetLocalized(pack, key, args), GameValues.Get);
// After:
            map = new MapContext(area.Id, (pack, key, args) => ModHelper.GetLocalized(pack, key, args), GameValues.Get, new SaveMemory());
```

```csharp
// IntelCards.cs L108 — Before:
            Dictionary<string, List<int>> picks = IntelRoller.Roll(area, sides => RNG.GetRandomInt(1, sides));
// After:
            Dictionary<string, List<int>> picks = IntelRoller.Roll(area, sides => RNG.GetRandomInt(1, sides), map.Root);
```

**Rationale:** `SaveMemory` is stateless, so a separate instance sees the same save data `GameBridge.Memory` writes; reads use `ModDataStore(warn: false)` → no log spam when `IsDiscovered` runs before a save loads. `map.Root` is the scope `hidden_when` already uses.

**Verification:** V2 (game build 0/0). Behavior in-game: judgment J1 (diff read; can't play).

**If this fails:** `git -C <work-root> checkout -- src/Game/ExpeditionLocation.cs src/Game/IntelCards.cs`.

---

### Step 4: Framework test
**Objective:** Prove SC1/SC2 in `tests/Sim`; fix the misplaced comment.
**Confidence:** High
**Depends on:** Steps 1, 2
**Verify-Projex:** Encouraged

**Files:** `tests/Sim/Program.cs`

**Changes:**

(a) Misplaced comment. Before (L502–504):
```csharp
    // An ammo item is worth its `rounds` in any gun, and reload uses as many items as it takes to fill the gun.
    // memory.* belongs to the pack that wrote the data and lives in the bridge's store, so it outlasts a run.
    private static void TestMemory()
```
After: delete the first line here, and put it directly above `private static void TestReloadUsesWholeItems()`:
```csharp
    // memory.* belongs to the pack that wrote the data and lives in the bridge's store, so it outlasts a run.
    private static void TestMemory()
...
    // An ammo item is worth its `rounds` in any gun, and reload uses as many items as it takes to fill the gun.
    private static void TestReloadUsesWholeItems()
```

(b) In `Main`, after `TestMemory();` add `TestMapMemory();`.

(c) Add after `TestMemory()` (before the moved comment / `TestReloadUsesWholeItems`):
```csharp
    // On the map, hidden_when, an intel slot's when and its line cases read the memory the map was given.
    private static void TestMapMemory()
    {
        var set = new PackSet(new Registry());
        set.Add("a", "ExpeditionFramework/main.json", f => @"{
          ""rooms"": { ""r"": { ""text"": ""k"" } },
          ""areas"": { ""a"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ],
            ""hidden_when"": ""memory.unlocked < 1"",
            ""pools"": { ""leads"": [ ""r"" ] },
            ""intel"": [
              { ""id"": ""tip"", ""pool"": ""leads"", ""when"": ""memory.heard >= 1"", ""description"": ""k"",
                ""title"": { ""cases"": [ { ""when"": ""memory.heard >= 2"", ""text"": ""loud"" }, { ""text"": ""quiet"" } ] } },
              { ""id"": ""rumor"", ""pool"": ""leads"", ""title"": ""k"", ""description"": ""k"" }
            ] } }
        }");
        Content content = set.Compile(out Dictionary<string, List<string>> problems, out Dictionary<string, List<string>> warnings);
        string all = string.Join("; ", problems.Values.SelectMany(l => l).Concat(warnings.Values.SelectMany(l => l)));
        Check(problems.Values.All(l => l.Count == 0) && warnings.Values.All(l => l.Count == 0), "map memory pack loads cleanly: " + all);
        if (problems.Values.Any(l => l.Count > 0)) return;

        AreaDef area = content.Areas["a:a"];
        int Picks(MapContext map, string slot) => IntelRoller.Roll(area, n => 1, map.Root)[slot].Count;

        var bare = new MapContext(area.Id, (p, k, a) => k, name => 0);
        Check(area.HiddenWhen(bare.Root) && Picks(bare, "tip") == 0, "a map without memory reads it as 0");

        var store = new MemoryStore();
        var map = new MapContext(area.Id, (p, k, a) => k, name => 0, store);
        Check(area.HiddenWhen(map.Root), "the area stays hidden until memory unlocks it");
        Check(Picks(map, "tip") == 0 && Picks(map, "rumor") == 1, "an intel slot whose when fails names nothing; one without when still rolls");
        store.Set("a", "unlocked", 1);
        store.Set("a", "heard", 1);
        Check(!area.HiddenWhen(map.Root), "memory unlocks the area on the map");
        Check(Picks(map, "tip") == 1 && Picks(map, "rumor") == 1, "an intel slot whose when holds rolls as usual");
        Check(map.Render(area.Intel[0].Title) == "quiet", "intel line cases read memory on the map: " + map.Render(area.Intel[0].Title));
        store.Set("a", "heard", 2);
        Check(map.Render(area.Intel[0].Title) == "loud", "intel line cases follow memory: " + map.Render(area.Intel[0].Title));
        var other = new MemoryStore();
        other.Set("b", "unlocked", 1);
        Check(area.HiddenWhen(new MapContext(area.Id, (p, k, a) => k, name => 0, other).Root), "another pack's memory doesn't unlock this area");
    }
```
Notes for executor: `PackSet.Compile(out problems, out warnings)` overload exists (`src/Core/PackSet.cs` L39). Local function `Picks` captures `area`; adjust syntax freely if the compiler objects, keeping each assertion. Localizer `(p,k,a)=>k` renders a text line as its key, so `"quiet"`/`"loud"` are the rendered strings. If `content.Areas["a:a"]` isn't the key, print `string.Join(",", content.Areas.Keys)` once to find it — `TestMemory` already uses `"a:a"` for the same `PackSet` shape. Per Assumption A3: if the pool of a room id raises a problem, change the pool entry only.

**Rationale:** Covers both store paths (bare vs given), the `when` gate both ways, a no-`when` slot (regression guard: rolls 1 pick with `n => 1` and default `chance` 100), cases rendering (A1), schema acceptance (zero warnings, A2), and per-pack isolation on the map.

**Verification:** V1.

**If this fails:** fix the test only if the failure is the test's own mistake (wrong key, syntax); a failing assertion on behavior means Steps 1–2 are wrong — fix there.

---

## Verification Plan

Run from `<work-root>` = `E:\SteamLibrary\steamapps\common\Probably Stolen\Modding\ExpeditionFramework\.projexwt\2611121459-pack-memory`. `NER` = `E:\SteamLibrary\steamapps\common\Probably Stolen\Modding\NightExpeditionsReborn`. Baseline `BASE` = `C:\Users\BA\AppData\Local\Temp\claude\a48a32985dda25c5\057518b9-283c-4d89-bc27-9ae607bcf871\scratchpad\ner-baseline-4000.txt`.

### Deterministic Suite

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V1 | SC1, SC2, SC4; Steps 1, 2, 4 | `dotnet run -c Release --project tests/Sim` | exit 0; last line `All checks passed`; no `FAIL` line | map memory not resolved, `when` not loaded/applied, schema warning, regressions |
| V2 | SC3, SC4; Steps 1–3 | PowerShell: `dotnet build ExpeditionFramework.csproj -c Release --no-incremental -warnaserror -p:GameManaged="E:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed\\" -p:InstallDir="$env:TEMP\ef-pack-memory-install\\"` | exit 0 (`-warnaserror` ⇒ 0 warnings, 0 errors) | game caller of `Roll` not updated, `SaveMemory` access, doc warnings |
| V3 | SC3; Step 3 | bash block V3 | prints `2` | game wiring missing either call site |
| V4 | SC4; Step 2 | `dotnet run -c Release --project tools/EfSim -- lint samples/SampleArea --strict` then `dotnet run -c Release --no-build --project tools/EfSim -- lint "<NER>" --strict` | both exit 0; lines `lint sample_area: 0 error(s), 0 warning(s)` and `lint night_expeditions_reborn: 0 error(s), 0 warning(s)` | shape/loader regressions in real packs |
| V5 | SC4 | `dotnet run -c Release --no-build --project tools/EfSim -- run samples/SampleArea` | exit 0; last line `ef-sim sample_area: all checks passed` | sample runtime regression |
| V6 | SC4 | bash block V6 | `diff` prints nothing, exit 0 | NER balance change |
| V7 | Source Hygiene | bash block V7 | no output | unshipped refs / plan shape / changelog wording in added code lines |
| V8 | Out of Scope | bash block V8 | no output | an `unlocked_when` field or other scope creep in code |

Bash blocks (run from `<work-root>`):

```bash
NER="E:/SteamLibrary/steamapps/common/Probably Stolen/Modding/NightExpeditionsReborn"
BASE="C:/Users/BA/AppData/Local/Temp/claude/a48a32985dda25c5/057518b9-283c-4d89-bc27-9ae607bcf871/scratchpad/ner-baseline-4000.txt"

# V3 — both game call sites pass the store / the scope
grep -cE 'GameValues\.Get, new SaveMemory\(\)\)|IntelRoller\.Roll\(area, .*, map\.Root\)' src/Game/ExpeditionLocation.cs src/Game/IntelCards.cs | awk -F: '{s+=$2} END {print s}'

# V6 — NER 4000-run rows identical to the pre-sprint baseline
dotnet run -c Release --no-build --project tools/EfSim -- run "$NER" --runs 4000 > "$TEMP/ner-after.txt"
diff <(grep -E '^ne_' "$BASE") <(grep -E '^ne_' "$TEMP/ner-after.txt")

# V7 — no unshipped references, plan shape or changelog wording in lines added by this iteration
git diff f00d8ae -- src tools tests | grep -E '^\+' | grep -nE 'projex|2611[0-9]{6}|Step [0-9]|SC[0-9]|iteration|D1|no longer|used to'

# V8 — no new area unlock field
grep -rn 'unlocked_when\|UnlockedWhen' src tools tests
```

### Judgment Checks

| ID | Check | Covers | Why not deterministic |
|----|-------|--------|-----------------------|
| J1 | Game diff: `ExpeditionLocation` passes `new SaveMemory()`; `IntelCards.Rolled` passes `map.Root`; nothing else in `src/Game` changed | SC3 | Game-only code; can't run without the game, and the sprint can't play in-game |
| J2 | New/changed comments are present tense and carry rationale naming the rejected alternative (roll-time vs `canStart` gating; dice not rolled for a gated slot) | Source Hygiene rule 6 | Prose quality |

### Acceptance Criteria Validation
| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| SC1 | V1 (TestMapMemory: bare map 0; hidden until set; cases quiet/loud; other pack isolated) | pass |
| SC2 | V1 (clean load; tip 0→1; rumor 1), V4 | pass / 0 warnings |
| SC3 | V2, V3, J1 | exit 0 / `2` / diff read |
| SC4 | V1, V2, V4, V5, V6 | all green, NER rows identical |

---

## Rollback Plan

1. `git -C <work-root> checkout -- src/Core/ExpeditionRun.cs src/Core/Model.cs src/Core/Schema.cs src/Core/PackLoader.cs src/Game/ExpeditionLocation.cs src/Game/IntelCards.cs tests/Sim/Program.cs` (uncommitted), or `git -C <work-root> revert <commit>` if committed.
2. Remove build leftovers that aren't ignored; `git -C <work-root> status --porcelain` must be empty.

---

## Notes

### Risks
- Intel `when` evaluated once per game day at roll: memory set by tonight's run takes effect on the next day's roll, not the same evening. Intended ("what the street says today"); README (iteration 5) should say so.
- `IntelRoller.Roll` gains a required param: breaking for any external C# caller (none known). Note in README/changelog with the iteration-1 `IBridge` note.
- In-game persistence/timing unverifiable (no in-game test) — same as iteration 1.

### Carried to later iterations
- README (iteration 5): intel `when` (rolled once per day), `hidden_when` as the memory-unlock idiom, `MapContext`/`IntelRoller.Roll` signatures, `IBridge.Memory` addition (iteration-1 audit).
- Iteration-1 audit's "`{memory.$x}` template has no test" is already covered: `TestMemory` sets `run.$greet` from line `hi_{memory.$who}` and checks `hi_you`. No action.

### Open Questions
- [ ] (none)

### Split Decision
No split — single scope, within size budget (4 steps, ~300 lines).
