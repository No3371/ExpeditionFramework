# Pack Memory: Run-Time Store

> **Status:** Complete (Patched)
> **Author:** Claude Opus 5.5 (plan)
> **Parent:** 2611121459-pack-memory-sprint.md
> **Sprint:** 2611121459-pack-memory-sprint.md — iteration 1
> **Source:** Sprint iteration objective (`2611121459-pack-memory-sprint.md` § Current Position)
> **Related Projex:** 2611121516-pack-memory-runtime-store-patch.md | 2611121459-pack-memory-sprint.md | 2611120227-persistence-game-integration-expansion-explore.md
> **Worktree:** No — execute in the sprint work-root on `projex/sprint/2611121459-pack-memory`

---

## Summary

Adds `memory.NAME` (int) and `memory.$NAME` (text) as a variable head beside `run.*` in run-time expressions, `set`/`add`/`set_text` effects, and `{…}` text templates/args. Values are scoped to the pack that authored the data and live behind a new host store seam `IMemory` reached through `IBridge.Memory`: the game stores them in the player's save via `ModHelper` mod data under the framework's mod id; ef-sim and framework tests use an in-memory `MemoryStore`. Proven by a new framework sim test.

**Scope:** `src/Core` (Expr, Builtins, PackLoader, Model, ExpeditionRun) + `src/Game/GameBridge.cs` + bridge implementers in `tools/EfSim/KitBridge.cs` and `tests/Sim/Program.cs` + one new test. One projex scope (repo root `.projex/`).
**Estimated Changes:** 7 files; ~5 Expr entry points, 2 builtins, 5 PackLoader call sites, 1 new interface + 1 store class + 1 internal host interface, 1 game store class, 3 bridge members, 1 test fn.

---

## Objective

### Problem / Gap / Need
Packs cannot keep any value across nights from data. `Expr.Reader/Writer/TextWriter/TextReader` know `run|arg|game|val|role` + entity/item heads only, take no pack, and no host seam exists for a persistent store.

### Success Criteria
- [ ] SC1: In a run, `memory.NAME` reads/writes an int through `set`, `add`, and any expression/condition (`value`, `when`); `memory.$NAME` reads/writes text through `set_text` (`var`, `value`) and `{memory.$NAME}` in text-key templates and line `args`.
- [ ] SC2: Memory is per pack: same name written by packs `a` and `b` gives two independent values; a pack has no syntax to touch another pack's memory.
- [ ] SC3: Values persist in the bridge's store across runs (two runs sharing one `MemoryStore` see the first run's writes); a fresh store starts at 0 / `""`.
- [ ] SC4: Load-time problems: `set`/`add` on `memory.$x` and `set_text` on `memory.x` are each exactly one problem; `Expr.Compile("memory.x", …)` without a pack is a compile error.
- [ ] SC5: Game: `GameBridge.Memory` stores ints/texts with `ModHelper` mod data under mod id `expedition_framework`, key `memory:<pack>:<name>` (int) / `memory:<pack>:$<name>` (text).
- [ ] SC6: All existing gates stay green: framework `tests/Sim`, game-assembly build (0 warnings/0 errors), `ef-sim lint --strict` on sample + NER, `ef-sim run` on sample, NER 4000-run table identical to baseline.

### Out of Scope
- Map-time reads (`MapContext`, intel conditions, area-unlock condition), `game.day`, `game.clears` — iteration 2. `MapContext` gets no memory now; memory paths there read 0 / `""` and writes no-op.
- ef-sim `sim.json` memory seed / carry-across-runs / expectation; lint "memory read but never written" — iteration 3. KitBridge only gets a fresh per-run `MemoryStore`.
- Sample pack, README — iteration 4.
- Public C# API (`IRun`/`ISpec` memory access) — deferred; later derive judges.
- Dynamic key names (`memory.spared_{foe}`), save-schema versioning, `GetModDataFolder`.
- SimConfig bot `when` expressions (`tools/EfSim/SimConfig.cs`): stay pack-less, so `memory.*` there is a compile error.

---

## Context

### Current State
- `src/Core/Expr.cs`: `Compile(text, out error, named, hasMod)` builds `new Expr(text, named, hasMod)`; `Atom()` calls `Reader(word, named)` and fails with `unknown path '…' (paths start with self., target., player., item., with., run., role., arg., game. or val.)`. `Reader(path, named, raw)` switch on head (`run` → `s.Run.Get`). `Writer(path)`: `run` → `s.Run.Set`. `TextWriter(path)` splits on last `.$`; `run` → `s.Run.SetText`. `TextReader(path, fieldRequired)`: `run` with `$var` → `s.Run.GetText`. Identifier chars (`IsWordChar`) exclude `$`, so `memory.$x` never parses as a numeric path inside an expression; only `set`/`add`'s raw `var` string reaches `Reader`/`Writer` with a `$`.
- `src/Core/Builtins.cs`: `set_text` (~L267–287) calls `Expr.TextWriter(var)` / `Expr.TextReader(value, fieldRequired: false)`; `Assign` (~L338–355, used by `set`/`add`) calls `Expr.Reader(path, raw: true)` / `Expr.Writer(path)`. `ISpec.PackId` = authoring pack (`PackLoader.Spec` ctor).
- `src/Core/PackLoader.cs`: `TextKey(key, pack, at, variants)` → `Template(text.Key, at)` (L1213/L1219) → `Expr.TextReader(path, fieldRequired: false)` (L1244). `TextArg(arg, pack, at)` → `Expr.TextReader(text, fieldRequired: true)` (L1312). `ValueOf(node, pack, at)` → `Expr.Compile(text, out error, name => NamedValue(pack, name, at), HasMod)` (L1419); `Condition(...)` string/number nodes go through `ValueOf`.
- `src/Core/Model.cs` L394–426: `public interface IBridge` (implementers: `GameBridge`, `tools/EfSim/KitBridge`, `tests/Sim/Program.cs` `FakeBridge`).
- `src/Core/ExpeditionRun.cs`: L120–124 `internal interface ITextHost`; L170 `public sealed partial class ExpeditionRun : IRun, ITextHost`; `Bridge` property L227. `MapContext` (L1088) implements `IRun, ITextHost`, no bridge.
- `src/Game/GameBridge.cs`: `GameBridge : IBridge`; `GameValue` L161; `internal static class GameValues` L191–205. `ModHelper` already used in `src/Game` (e.g. `ExpeditionLocation.cs` L59).
- Game API (decompiled `ModHelper`): `SetModData(modId, key, value)` / `GetModData(modId, key, fallback)` / `SetModDataInt` / `GetModDataInt(modId, key, fallback = 0)` on `PlayerStore.Instance.ModData()`, key `modId + ":" + key`; Set warns + no-ops with no save loaded; Get returns fallback.
- Manifest id = `expedition_framework` = `ExpeditionRun.FrameworkPack`.
- Baseline (verified at plan time on this branch): `tests/Sim` → `All checks passed`; game build 0 warnings/0 errors; `ef-sim lint --strict` sample + NER → `0 error(s), 0 warning(s)` exit 0; `ef-sim run samples/SampleArea` → `ef-sim sample_area: all checks passed`.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `src/Core/Model.cs` | Engine↔host contracts | Add `IMemory`, `MemoryStore`; `IBridge.Memory` |
| `src/Core/ExpeditionRun.cs` | Run; internal host interfaces | Add `internal interface IMemoryHost`; `ExpeditionRun` implements it via `Bridge.Memory` |
| `src/Core/Expr.cs` | Path heads, expression compiler | `pack` threaded into `Compile`/`Reader`/`Writer`/`TextWriter`/`TextReader`; `memory` head |
| `src/Core/Builtins.cs` | `set`/`add`/`set_text` | Pass `c.PackId`; error texts mention memory |
| `src/Core/PackLoader.cs` | Compiles data | Pass `pack` to `Expr.Compile`, `Template`, both `TextReader` calls |
| `src/Game/GameBridge.cs` | Game host | `SaveMemory : IMemory` over `ModHelper` mod data; `GameBridge.Memory` |
| `tools/EfSim/KitBridge.cs` | ef-sim host | `Memory` = fresh `MemoryStore` per bridge |
| `tests/Sim/Program.cs` | Framework tests | `FakeBridge` gets optional store; new `TestMemory()` |

### Dependencies
- **Requires:** nothing beyond the sprint branch head.
- **Blocks:** sprint iterations 2 (map-time reads reuse `IMemory`/`IMemoryHost`), 3 (ef-sim seeds/carries `MemoryStore`), 4.

### Constraints
- Core (`src/Core`) stays Unity-free: compiled into net8.0 test/ef-sim exes and netstandard2.1 game dll. `IMemory` lives in Core; `SaveMemory` in `src/Game`.
- `api/ExpeditionApi.cs` (public API assembly) untouched.
- All new `pack` params are optional trailing params defaulting to `null`, so existing callers (`tests/Sim`, `SimConfig`) still compile.
- Source Hygiene (SKILL.md): no projex ids/step labels in code comments; non-obvious decisions get a self-contained rationale naming the rejected alternative.
- Commit trailer: `Co-Authored-By: Claude <Model> <noreply@anthropic.com>` with the executing model's name. Never commit in the base checkout.

### Assumptions
- A1: Data compiled for a pack is compiled with that pack's id as `pack`/`ISpec.PackId` (patched-in content compiles under the patched target pack, as `val.NAME` already does). Memory therefore follows the pack whose definition holds the expression. Verify early: `grep -n "new Spec(this, pack" src/Core/PackLoader.cs` shows the authoring pack flows to specs.
- A2: Pack ids contain no `:` (qualified ids are `pack:name`), so `memory:<pack>:<name>` is unambiguous.
- A3: NER and the sample pack use no `memory` head today → their behavior and the NER table are unchanged. Verify: `grep -rn "memory\." samples "E:/SteamLibrary/steamapps/common/Probably Stolen/Modding/NightExpeditionsReborn" --include=*.json` → no hits.

### Impact Analysis
- **Direct:** files in Key Files.
- **Adjacent:** every expression compile (`ValueOf`) now carries a pack — no behavior change for non-memory paths. `MapContext` scopes: memory reads 0 / `""`, writes no-op.
- **Downstream:** iteration 2 (`MapContext` takes an `IMemory` and implements `IMemoryHost`), iteration 3 (ef-sim seeds/inspects `MemoryStore`; may add enumeration then).

---

## Implementation [PATCHED] — see 2611121516-pack-memory-runtime-store-patch.md

### Overview
1. Store seam in Core (`IMemory`, `MemoryStore`, `IBridge.Memory`, `IMemoryHost` on `ExpeditionRun`) + all three bridge implementers, so everything compiles.
2. `memory` head in `Expr` with optional `pack`.
3. Thread the authoring pack from `Builtins` and `PackLoader`.
4. Test.

### Step 1: Store seam and bridge implementers

**Objective:** Introduce `IMemory`/`MemoryStore`, expose via `IBridge.Memory`, reach it from a run through internal `IMemoryHost`; implement in all three bridges.
**Confidence:** High
**Depends on:** None
**Do-Projex:** Encouraged

**Files:**
- `src/Core/Model.cs`
- `src/Core/ExpeditionRun.cs`
- `src/Game/GameBridge.cs`
- `tools/EfSim/KitBridge.cs`
- `tests/Sim/Program.cs`

**Changes:**

`src/Core/Model.cs` — in `IBridge`, after `int GameValue(string name);`:
```csharp
        /// <summary>Where packs keep memory.* across nights. The game keeps it in the player's save; a sim can keep it in a MemoryStore.</summary>
        IMemory Memory { get; }
```
After the closing `}` of `IBridge` (before `public delegate string Localizer`):
```csharp
    /// <summary>
    /// Values a pack keeps across nights, as memory.NAME (numbers) and memory.$NAME (text). Each pack has its own names,
    /// so two packs using the same name never see each other's value. Unset numbers read 0 and unset text "".
    /// </summary>
    public interface IMemory
    {
        int Get(string pack, string name);
        void Set(string pack, string name, int value);
        string GetText(string pack, string name);
        void SetText(string pack, string name, string value);
    }

    /// <summary>Memory held in this process only, for sims and tests.</summary>
    public sealed class MemoryStore : IMemory
    {
        private readonly Dictionary<(string, string), int> numbers = new Dictionary<(string, string), int>();
        private readonly Dictionary<(string, string), string> texts = new Dictionary<(string, string), string>();

        public int Get(string pack, string name) => numbers.TryGetValue((pack, name), out int v) ? v : 0;
        public void Set(string pack, string name, int value) => numbers[(pack, name)] = value;
        public string GetText(string pack, string name) => texts.TryGetValue((pack, name), out string v) ? v : "";
        public void SetText(string pack, string name, string value) => texts[(pack, name)] = value ?? "";
    }
```
(`Model.cs` already has `using System.Collections.Generic;` — confirm; add if missing.)

`src/Core/ExpeditionRun.cs` — after `ITextHost` (L120–124):
```csharp
    /// <summary>
    /// Where memory.* paths find the store. Kept off IRun, which is the public API: putting it there would make every
    /// IRun implementation outside this assembly provide a store. The map has no store, so memory there reads 0 and "" and writes are dropped.
    /// </summary>
    internal interface IMemoryHost
    {
        IMemory Memory { get; }
    }
```
Class line: `public sealed partial class ExpeditionRun : IRun, ITextHost` → `public sealed partial class ExpeditionRun : IRun, ITextHost, IMemoryHost`. Next to `public int Game(string name) => Bridge.GameValue(name);` (~L343) add:
```csharp
        IMemory IMemoryHost.Memory => Bridge.Memory;
```

`src/Game/GameBridge.cs` — in `GameBridge`, after `public int GameValue(string name) => GameValues.Get(name);`:
```csharp
        public IMemory Memory { get; } = new SaveMemory();
```
After `GameValues` class (end of namespace):
```csharp
    /// <summary>
    /// Pack memory in the player's save, through the game's mod data under the framework's own mod id, so it stays with
    /// that save and lasts across nights. Text names carry a "$" so memory.x and memory.$x never share a key. Mod data
    /// is per save; the mod data folder is shared by every save and would leak memory between them.
    /// </summary>
    internal sealed class SaveMemory : IMemory
    {
        private static string Key(string pack, string name) => "memory:" + pack + ":" + name;

        public int Get(string pack, string name) => ModHelper.GetModDataInt(ExpeditionRun.FrameworkPack, Key(pack, name));
        public void Set(string pack, string name, int value) => ModHelper.SetModDataInt(ExpeditionRun.FrameworkPack, Key(pack, name), value);
        public string GetText(string pack, string name) => ModHelper.GetModData(ExpeditionRun.FrameworkPack, Key(pack, "$" + name), "");
        public void SetText(string pack, string name, string value) => ModHelper.SetModData(ExpeditionRun.FrameworkPack, Key(pack, "$" + name), value ?? "");
    }
```

`tools/EfSim/KitBridge.cs` — after the `GameValue` member (L81–82):
```csharp
    // A fresh store per run, so memory never carries between runs.
    public IMemory Memory { get; } = new MemoryStore();
```

`tests/Sim/Program.cs` — `FakeBridge`:
```csharp
// Before:
        public FakeBridge(Random random, bool withEverything = false)
        {
            this.random = random;
// After:
        public FakeBridge(Random random, bool withEverything = false, IMemory memory = null)
        {
            this.random = random;
            Memory = memory ?? new MemoryStore();
```
and next to `public int GameValue(string name) => 0;` add `public IMemory Memory { get; }`.

**Rationale:** Store on `IBridge` because the bridge already is "what the engine needs from the game", and every run gets one; a separate ctor arg would touch every `new ExpeditionRun` site. Internal `IMemoryHost` follows the `ITextHost` precedent and avoids changing the public `IRun` (C# API is deferred). `MemoryStore` in Core is shared by ef-sim and tests (iteration 3 extends it).

**Verification:** V1 (builds + existing tests), V2 (game build).

**If this fails:** `git -C <work-root> checkout -- <files>`; most likely cause is a missed implementer — `grep -rn ": IBridge" src tools tests`.

---

### Step 2: `memory` head in `Expr`

**Objective:** `memory.NAME` / `memory.$NAME` resolve against the pack passed in; no pack → path unknown.
**Confidence:** High
**Depends on:** Step 1
**Verify-Projex:** Encouraged

**Files:**
- `src/Core/Expr.cs`

**Changes:**

Class summary (L9–13): add `memory` to the path list: `Paths start with self, target, player, item, with, run, memory, role.NAME, arg, game or val.`

Field + ctor + Compile:
```csharp
// Before:
        private readonly Func<string, bool> hasMod;
        private int at;
        ...
        private Expr(string text, Func<string, Value> named, Func<string, bool> hasMod)
        {
            this.text = text;
            this.named = named;
            this.hasMod = hasMod;
        }

        /// <summary>
        /// Returns null and sets the error when the text doesn't parse. <paramref name="named"/> resolves val.NAME and
        /// <paramref name="hasMod"/> answers mod(id).
        /// </summary>
        public static Value Compile(string text, out string error, Func<string, Value> named = null, Func<string, bool> hasMod = null)
        {
            var parser = new Expr(text, named, hasMod);
// After:
        private readonly Func<string, bool> hasMod;
        private readonly string pack;
        private int at;
        ...
        private Expr(string text, Func<string, Value> named, Func<string, bool> hasMod, string pack)
        {
            this.text = text;
            this.named = named;
            this.hasMod = hasMod;
            this.pack = pack;
        }

        /// <summary>
        /// Returns null and sets the error when the text doesn't parse. <paramref name="named"/> resolves val.NAME,
        /// <paramref name="hasMod"/> answers mod(id), and <paramref name="pack"/> owns the memory.NAME it reads. Without a
        /// pack, memory paths don't compile.
        /// </summary>
        public static Value Compile(string text, out string error, Func<string, Value> named = null, Func<string, bool> hasMod = null, string pack = null)
        {
            var parser = new Expr(text, named, hasMod, pack);
```

Helpers — add right after `ReadItem(...)` (before `Reader`):
```csharp
        private static IMemory MemoryOf(IScope s) => (s.Run as IMemoryHost)?.Memory;

        // A "$" name is text: as a number, memory.$x would read and overwrite the text value's key.
        private static bool IsMemoryNumber(string pack, string name) => pack != null && !name.StartsWith("$");
```

`Reader`:
```csharp
// Before:
        /// <summary>Reader for a path, or null if the head is unknown. With raw, entity stats leave out what statuses add.</summary>
        public static Value Reader(string path, Func<string, Value> named = null, bool raw = false)
        ...
                case "run": return s => s.Run.Get(name);
// After:
        /// <summary>
        /// Reader for a path, or null if the head is unknown. With raw, entity stats leave out what statuses add.
        /// memory.NAME reads <paramref name="pack"/>'s memory and is unknown without a pack.
        /// </summary>
        public static Value Reader(string path, Func<string, Value> named = null, bool raw = false, string pack = null)
        ...
                case "run": return s => s.Run.Get(name);
                case "memory":
                    if (!IsMemoryNumber(pack, name)) return null;
                    return s => MemoryOf(s)?.Get(pack, name) ?? 0;
```

`Writer`:
```csharp
// Before:
        /// <summary>Writer for a path, or null if the head is unknown.</summary>
        public static Action<IScope, int> Writer(string path)
        ...
            if (head == "run") return (s, v) => s.Run.Set(name, v);
// After:
        /// <summary>Writer for a path, or null if the head is unknown. memory.NAME writes <paramref name="pack"/>'s memory.</summary>
        public static Action<IScope, int> Writer(string path, string pack = null)
        ...
            if (head == "run") return (s, v) => s.Run.Set(name, v);
            if (head == "memory")
            {
                if (!IsMemoryNumber(pack, name)) return null;
                return (s, v) => MemoryOf(s)?.Set(pack, name, v);
            }
```

`TextWriter`:
```csharp
// Before:
        /// <summary>Writer for a text path like run.$name, self.$name or role.boss.$name, or null.</summary>
        public static Action<IScope, string> TextWriter(string path)
        ...
            if (head == "run") return (s, v) => s.Run.SetText(name, v);
// After:
        /// <summary>Writer for a text path like run.$name, memory.$name, self.$name or role.boss.$name, or null.</summary>
        public static Action<IScope, string> TextWriter(string path, string pack = null)
        ...
            if (head == "run") return (s, v) => s.Run.SetText(name, v);
            if (head == "memory")
            {
                if (pack == null || name.Length == 0) return null;
                return (s, v) => MemoryOf(s)?.SetText(pack, name, v);
            }
```

`TextReader`:
```csharp
// Before:
        public static Func<IScope, string> TextReader(string path, bool fieldRequired)
        ...
            if (head == "run") return variable != null ? s => s.Run.GetText(variable) : (Func<IScope, string>)null;
// After:
        public static Func<IScope, string> TextReader(string path, bool fieldRequired, string pack = null)
        ...
            if (head == "run") return variable != null ? s => s.Run.GetText(variable) : (Func<IScope, string>)null;
            if (head == "memory") return variable != null && pack != null ? s => MemoryOf(s)?.GetText(pack, variable) ?? "" : (Func<IScope, string>)null;
```
Also extend its `<summary>` with one sentence: `memory.$var reads <paramref name="pack"/>'s memory.`

`Atom`:
```csharp
// Before:
            Value reader = Reader(word, named);
            if (reader == null)
            {
                Fail("unknown path '" + word + "' (paths start with self., target., player., item., with., run., role., arg., game. or val.)");
// After:
            Value reader = Reader(word, named, pack: pack);
            if (reader == null)
            {
                Fail("unknown path '" + word + "' (paths start with self., target., player., item., with., run., memory., role., arg., game. or val.)");
```

**Rationale:** Optional trailing `pack` keeps every existing caller compiling and behaving the same. Null-safe `MemoryOf` makes memory inert on hosts without a store (map) instead of throwing; iteration 2 gives the map a store. The pack is fixed at compile time, so data has no way to name another pack's memory (SC2).

**Verification:** V1, V3 (no pack-less memory callers remain after Step 3).

**If this fails:** revert `src/Core/Expr.cs`.

---

### Step 3: Thread the authoring pack

**Objective:** Every data-facing compile path passes the authoring pack.
**Confidence:** High
**Depends on:** Step 2
**Do-Projex:** Encouraged

**Files:**
- `src/Core/Builtins.cs`
- `src/Core/PackLoader.cs`

**Changes:**

`src/Core/Builtins.cs` `set_text`:
```csharp
// Before:
                Action<IScope, string> write = Expr.TextWriter(c.Require("var") ?? "");
                if (write == null)
                {
                    c.Error("\"var\" must be a path like run.$name or self.$name");
                ...
                Func<IScope, string> read = Expr.TextReader(c.Require("value") ?? "", fieldRequired: false);
// After:
                Action<IScope, string> write = Expr.TextWriter(c.Require("var") ?? "", c.PackId);
                if (write == null)
                {
                    c.Error("\"var\" must be a path like run.$name, memory.$name or self.$name");
                ...
                Func<IScope, string> read = Expr.TextReader(c.Require("value") ?? "", fieldRequired: false, pack: c.PackId);
```
`Assign`:
```csharp
// Before:
            Value read = Expr.Reader(path, raw: true);
            Action<IScope, int> write = Expr.Writer(path);
            if (read == null || write == null)
            {
                c.Error("\"var\" must be a path like run.alarm or self.armor");
// After:
            Value read = Expr.Reader(path, raw: true, pack: c.PackId);
            Action<IScope, int> write = Expr.Writer(path, c.PackId);
            if (read == null || write == null)
            {
                c.Error("\"var\" must be a path like run.alarm, memory.debt or self.armor");
```

`src/Core/PackLoader.cs`:
```csharp
// L1213 Before:
            if (brace >= 0) text.Dynamic = Template(text.Key, at);
// After:
            if (brace >= 0) text.Dynamic = Template(text.Key, pack, at);

// L1219 Before:
        private Func<IScope, string> Template(string key, string at)
// After:
        private Func<IScope, string> Template(string key, string pack, string at)

// L1244 Before:
                Func<IScope, string> reader = Expr.TextReader(path, fieldRequired: false);
// After:
                Func<IScope, string> reader = Expr.TextReader(path, fieldRequired: false, pack: pack);

// L1312 Before (TextArg):
                Func<IScope, string> reader = Expr.TextReader(text, fieldRequired: true);
// After:
                Func<IScope, string> reader = Expr.TextReader(text, fieldRequired: true, pack: pack);

// L1419 Before (ValueOf):
                Value value = Expr.Compile(text, out string error, name => NamedValue(pack, name, at), HasMod);
// After:
                Value value = Expr.Compile(text, out string error, name => NamedValue(pack, name, at), HasMod, pack);
```
In `TextKey`, pass the `pack` **parameter** (authoring pack), not `text.Pack` — a key written `other:key_{memory.$x}` localizes in `other` but reads the author's memory.

**Rationale:** These are all the call sites of the four entry points under `src/` (verified by grep at plan time); `Condition` string nodes route through `ValueOf`, so `when` conditions are covered.

**Verification:** V1, V3.

**If this fails:** revert both files.

---

### Step 4: Framework test

**Objective:** Prove SC1–SC4 in `tests/Sim`.
**Confidence:** High
**Depends on:** Steps 1–3
**Verify-Projex:** Encouraged

**Files:**
- `tests/Sim/Program.cs`

**Changes:** In `Main`, after `TestReloadUsesWholeItems();` add `TestMemory();`. Add, e.g. after `TestSpawnOutsideFight`:

```csharp
    // memory.* belongs to the pack that wrote the data and lives in the bridge's store, so it outlasts a run.
    private static void TestMemory()
    {
        string Pack(string enter) => @"{
          ""rooms"": { ""r"": { ""text"": ""k"", ""enter"": " + enter + @" } },
          ""areas"": { ""a"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ] } }
        }";
        var set = new PackSet(new Registry());
        set.Add("a", "ExpeditionFramework/main.json", f => Pack(@"[
            { ""add"": { ""var"": ""memory.visits"", ""value"": 1 } },
            { ""set"": { ""var"": ""run.seen"", ""value"": ""memory.visits"" } },
            { ""if"": { ""when"": ""memory.visits >= 2"", ""then"": [ { ""set"": { ""var"": ""run.again"", ""value"": 1 } } ] } },
            { ""set_text"": { ""var"": ""memory.$who"", ""value"": ""player.name"" } },
            { ""set_text"": { ""var"": ""run.$who"", ""value"": ""memory.$who"" } },
            { ""set_text"": { ""var"": ""run.$greet"", ""line"": ""hi_{memory.$who}"" } }
        ]"));
        set.Add("b", "ExpeditionFramework/main.json", f => Pack(@"[ { ""set"": { ""var"": ""memory.visits"", ""value"": 100 } } ]"));
        Content content = set.Compile(out Dictionary<string, List<string>> problems);
        Check(problems.Values.All(l => l.Count == 0), "memory packs: " + string.Join("; ", problems.Values.SelectMany(l => l)));
        if (problems.Values.Any(l => l.Count > 0)) return;

        ExpeditionRun Play(string area, IMemory memory)
        {
            var run = new ExpeditionRun(content, content.Areas[area], new FakeBridge(new Random(1), memory: memory), n => 1, (p, k, a) => k, "you");
            run.Begin();
            return run;
        }
        var store = new MemoryStore();
        ExpeditionRun first = Play("a:a", store);
        Check(first.Get("seen") == 1 && first.Get("again") == 0, "first run sees memory.visits 1: " + first.Get("seen"));
        ExpeditionRun second = Play("a:a", store);
        Check(second.Get("seen") == 2 && second.Get("again") == 1, "second run sees the first run's memory: " + second.Get("seen"));
        Check(second.GetText("who") == "you" && second.GetText("greet") == "hi_you", "memory text reads back: " + second.GetText("who") + " / " + second.GetText("greet"));
        Play("b:a", store);
        Check(store.Get("b", "visits") == 100 && store.Get("a", "visits") == 2, "packs keep separate memory: a=" + store.Get("a", "visits") + " b=" + store.Get("b", "visits"));
        Check(store.GetText("a", "who") == "you" && store.GetText("b", "who") == "", "memory text is per pack too");
        Check(Play("a:a", new MemoryStore()).Get("seen") == 1, "a fresh store starts empty");

        string Enter(string effect) => "{\"rooms\":{\"r\":{\"text\":\"k\",\"enter\":[" + effect + "]}}}";
        List<string> p = ProblemsIn(Enter("{\"set\":{\"var\":\"memory.$x\",\"value\":1}}"));
        Check(p.Count == 1, "set on memory.$x is one problem: " + string.Join("; ", p));
        p = ProblemsIn(Enter("{\"add\":{\"var\":\"memory.$x\",\"value\":1}}"));
        Check(p.Count == 1, "add on memory.$x is one problem: " + string.Join("; ", p));
        p = ProblemsIn(Enter("{\"set_text\":{\"var\":\"memory.x\",\"value\":\"player.name\"}}"));
        Check(p.Count == 1, "set_text on memory.x is one problem: " + string.Join("; ", p));
        Expr.Compile("memory.x", out string packless);
        Check(packless != null, "memory.x without a pack should not compile");
        Expr.Compile("memory.x + 1", out string owned, pack: "t");
        Check(owned == null, "memory.x with a pack compiles: " + owned);
    }
```
Notes for executor: `PackSet.Compile(out problems)` overload exists (used in `TestPatches`); `ExpeditionRun.GetText` exists (IRun). If `problems` reports something unexpected (e.g. a shape warning promoted), fix the test JSON, not the engine, unless the message shows an engine bug.

**Rationale:** Uses the existing in-repo patterns (`PackSet` two-pack compile from `TestPatches`/`TestWarningsDontVoid`; run-and-inspect from `TestSpawnOutsideFight`); localizer `(p,k,a)=>k` makes `hi_{memory.$who}` render as its resolved key, proving template interpolation.

**Verification:** V1.

**If this fails:** a failing `Check` names the broken criterion; fix the engine step it points at.

---

## Verification Plan

Run from `<work-root>` = `E:\SteamLibrary\steamapps\common\Probably Stolen\Modding\ExpeditionFramework\.projexwt\2611121459-pack-memory`. `NER` = `E:\SteamLibrary\steamapps\common\Probably Stolen\Modding\NightExpeditionsReborn`. Baseline file `BASE` = `C:\Users\BA\AppData\Local\Temp\claude\a48a32985dda25c5\057518b9-283c-4d89-bc27-9ae607bcf871\scratchpad\ner-baseline-4000.txt`.

### Deterministic Suite

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V1 | SC1–SC4, SC6; Steps 1–4 | `dotnet run -c Release --project tests/Sim` | exit 0; last line `All checks passed`; output has no `FAIL` line | broken memory semantics, regressions in existing tests, compile errors in Core/tests |
| V2 | SC5, SC6; Step 1 | PowerShell: `dotnet build ExpeditionFramework.csproj -c Release --no-incremental -warnaserror -p:GameManaged="E:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed\\" -p:InstallDir="$env:TEMP\ef-pack-memory-install\\"` | exit 0 (with `-warnaserror`, exit 0 ⇔ 0 warnings and 0 errors) | `SaveMemory`/`GameBridge` not compiling against game API; new warnings |
| V3 | SC2, Step 3 | bash block V3 below | prints `0` | a data-facing call site left pack-less (memory would fail to compile there) |
| V4 | SC6 | `dotnet run -c Release --project tools/EfSim -- lint samples/SampleArea --strict` then `dotnet run -c Release --no-build --project tools/EfSim -- lint "<NER>" --strict` | both exit 0; lines `lint sample_area: 0 error(s), 0 warning(s)` and `lint night_expeditions_reborn: 0 error(s), 0 warning(s)` | load-time regressions in real packs |
| V5 | SC6 | `dotnet run -c Release --no-build --project tools/EfSim -- run samples/SampleArea` | exit 0; last line `ef-sim sample_area: all checks passed` | runtime regression in the sample |
| V6 | SC6 | bash block V6 below | `diff` prints nothing, exit 0 | any change to NER balance |
| V7 | Source Hygiene | bash block V7 below | no output | unshipped references / plan shape in code comments |
| V8 | A3 | bash block V8 below | no output | an existing pack already using a `memory` head (would change behavior) |

Bash blocks (run from `<work-root>`; pipes here are real shell pipes):

```bash
NER="E:/SteamLibrary/steamapps/common/Probably Stolen/Modding/NightExpeditionsReborn"
BASE="C:/Users/BA/AppData/Local/Temp/claude/a48a32985dda25c5/057518b9-283c-4d89-bc27-9ae607bcf871/scratchpad/ner-baseline-4000.txt"

# V3 — every Expr entry-point call in Builtins/PackLoader ends with a pack argument
grep -nE 'Expr\.(Reader|Writer|TextReader|TextWriter|Compile)\(' src/Core/Builtins.cs src/Core/PackLoader.cs | grep -vcE '(pack|c\.PackId)\);'

# V6 — NER 4000-run rows identical to the pre-sprint baseline
dotnet run -c Release --no-build --project tools/EfSim -- run "$NER" --runs 4000 > "$TEMP/ner-after.txt"
diff <(grep -E '^ne_' "$BASE") <(grep -E '^ne_' "$TEMP/ner-after.txt")

# V7 — no unshipped references or plan shape in added code lines
git diff main -- src tools tests | grep -E '^\+' | grep -nE 'projex|2611[0-9]{6}|Step [0-9]|SC[0-9]|iteration'

# V8 — no pack already uses a memory head
grep -rln 'memory\.' samples "$NER" --include=*.json
```

### Judgment Checks

| Check | Covers | Why not deterministic |
|-------|--------|-----------------------|
| `SaveMemory` uses `ExpeditionRun.FrameworkPack` and keys `memory:<pack>:<name>` / `memory:<pack>:$<name>` via `ModHelper` mod data (not `GetModDataFolder`) | SC5 | Game-only code; nothing can run it outside the game and the sprint can't play in-game. Read the diff. |
| New comments carry rationale naming the rejected alternative (IRun public API, mod data folder, `$`-key collision) | Source Hygiene rule 6 | Prose quality |

### Acceptance Criteria Validation
| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| SC1 | V1 (TestMemory: seen/again/who/greet) | pass |
| SC2 | V1 (a=2, b=100; text per pack), V3 | pass / `0` |
| SC3 | V1 (second run sees 2; fresh store sees 1) | pass |
| SC4 | V1 (three one-problem checks + packless compile) | pass |
| SC5 | V2 + judgment | exit 0 + diff review |
| SC6 | V1, V2, V4, V5, V6 | all green, NER rows identical |

---

## Rollback Plan

Per-step rollback above. To abandon entirely: `git -C <work-root> checkout main -- src tools tests` (restores all code files to the sprint base content), leaving `.projex/` untouched; or revert the execution commit(s) on the sprint branch with `git -C <work-root> revert <sha>`.

---

## Notes

### Risks
- Game persistence timing: mod data lands in `PlayerStore`'s dictionary immediately; whether it reaches disk depends on the game's own save points (e.g. a quit mid-night). Not verifiable here (no in-game testing). Mitigation: documented in iteration 4's README.
- `memory.$x` in a numeric `set` var used to be impossible to express; now an explicit problem — intended.
- Patched content: memory follows the pack whose definition compiles it (A1). If a later iteration wants patch authors to own memory written by their patch, that's a separate change.

### Split Decision
`No split — single scope, within size budget` (4 steps, < 500 lines).

### Open Questions
- [ ] (none)
