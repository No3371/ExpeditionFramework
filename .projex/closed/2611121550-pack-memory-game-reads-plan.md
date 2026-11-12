# Pack Memory: `game.day` / `game.clears` Reads [PATCHED]

> **Status:** Complete
> **Author:** Claude Opus 5.5 (plan)
> **Parent:** 2611121459-pack-memory-sprint.md
> **Sprint:** 2611121459-pack-memory-sprint.md — iteration 3
> **Source:** Sprint iteration objective (`2611121459-pack-memory-sprint.md` § Current Position, item 3)
> **Related Projex:** 2611121459-pack-memory-sprint.md | 2611121531-pack-memory-map-reads-plan.md | 2611121540-pack-memory-map-reads-audit.md | 2611120227-persistence-game-integration-expansion-explore.md
> Partial Execution: full plan executed via 2611121556-pack-memory-game-reads-patch.md
> **Worktree:** No — execute in the sprint work-root on `projex/sprint/2611121459-pack-memory`

---

## Summary

Makes `game.day`, `game.clears` and `game.clears.AREA` readable from pack data, at run time and on the map. `day` = the game's own day counter (`StoreStation.GetDayCounter()`). `clears` = how often the player has cleared the reading area (the framework's existing per-area `ef_clears` counter); `clears.AREA` = the same for AREA of the reading area's pack. The name → area mapping is a pure Core helper (`GameReads.ClearsArea`) so the framework sim can test it; the game side (`GameValues`) gains the area it is read for. No ef-sim change: sim.json `bridge.game` already serves any `game.NAME` (keys `day`, `clears`, `clears.AREA`).

**Scope:** `src/Core/Model.cs` (IBridge doc + new `GameReads`), `src/Game/GameBridge.cs` (`GameBridge` ctor, `GameValues`), `src/Game/ExpeditionLocation.cs` (3 call sites), `tests/Sim/Program.cs` (1 new test). One projex scope (repo root `.projex/`).
**Estimated Changes:** 4 files; 1 new internal static class (1 fn), 1 doc comment, 1 ctor signature + 1 field, `GameValues.Get` gains a param + 2 reads + 1 new fn, 3 game call-site lines, 1 new test fn + 1 `Main` line.

---

## Objective

### Problem / Gap / Need
`game.NAME` resolves through `IRun.Game` → `IBridge.GameValue` (run) or `MapContext`'s `Func<string,int> game` (map), both ending in `GameValues.Get` (`src/Game/GameBridge.cs`), which knows only `luck|roll_luck|wounds|maimed`. Packs can't read what day it is or whether an area has been cleared. The framework already counts clears per area (`ef_clears`) but only `unlocked_by` sees it, and `unlocked_by` is ANDed with `hidden_when`, so "unlock on X's clear OR on a remembered event" is not expressible (flagged in `2611121540-pack-memory-map-reads-audit.md`).

### Success Criteria
- [ ] SC1: `GameReads.ClearsArea(name, areaId)` (Core, internal): `"clears"` → `areaId`; `"clears.AREA"` → `"<pack of areaId>:AREA"`; any other name (incl. `"clears."`, `"clearsX"`, `"day"`, null) → null. Tested in `tests/Sim`.
- [ ] SC2: An area's `hidden_when` using `game.clears.AREA` and `memory.*` loads with no problem/warning, the map host receives the name `clears.AREA`, and the area is unhidden by either a clear (host returns ≥1) or the memory value (OR unlock). Tested in `tests/Sim`.
- [ ] SC3: Game: `GameValues.Get(name, areaId)` returns `StoreStation.GetDayCounter()` for `day` and `GameValues.Clears(GameReads.ClearsArea(...))` for clears names; `GameValues.Clears` reads `PlayerStore.Instance.afterhourState` without `GetAfterhourState` (no save entry created on read); `GameBridge` is built with the `AreaDef` and passes `area.Id`; the map's `MapContext` passes `area.Id`; `IsDiscovered` reads `unlocked_by` clears through `GameValues.Clears`.
- [ ] SC4: `IBridge.GameValue` carries a doc comment naming every game read (`luck`, `roll_luck`, `wounds`, `maimed`, `day`, `clears`, `clears.AREA`).
- [ ] SC5: All gates green: framework `tests/Sim`, game-assembly build 0 warnings / 0 errors, `ef-sim lint --strict` 0/0 on sample + NER, `ef-sim run` on sample, NER 4000-run table identical to baseline; nothing under `tools/`, `samples/`, `README.md` changed.

### Out of Scope
- README (`game.day`, `game.clears`, `game.clears.AREA`, sim.json `bridge.game` keys for them, `hidden_when` OR-unlock idiom) — iteration 5. Carry forward together with iteration 2's list (intel `when`, memory in `hidden_when`, `IntelRoller.Roll`/`MapContext` C# signature notes).
- ef-sim / lint: no change. A lint check that `game.clears.AREA` names a real area of the pack, and lint "memory read but never written" — iteration 4 at the earliest (the former is optional; note it there).
- Cross-pack clears (`game.clears.otherpack:area`): `:` is not an expression word char, so the parser can't carry it. Not needed for v1.
- Other `game.*` reads from the explore (`best_depth`, `cash`, `trust`, `store_level`).
- Vanilla (non-framework) location clears — the game keeps no general clears record; only framework areas count.

### Decisions
- **D1 — `clears` is per area, with a named form.** A pack-wide total would not answer "has *this* area been beaten", and the bare per-area form alone cannot express the OR unlock the map-reads audit asked for. `clears.AREA` costs one string op and resolves within the reading area's pack (packs can't read each other's area ids anyway).
- **D2 — `day` = `StoreStation.GetDayCounter()`.** Decompiled: `public int dayCounter` (`[ES3Serializable]`, saved), `StartDay()` does `dayCounter++`, static `GetDayCounter()` returns 0 when `Instance == null`. `IntelCards` already keys its daily roll on the same counter. No side effects.
- **D3 — read clears without `GetAfterhourState`.** Decompiled `PlayerStore.GetAfterhourState(id)` inserts a new empty `AfterhourLocationState` into the save's `afterhourState` dictionary for any unknown id. A mistyped `game.clears.AREA` evaluated every map refresh would write junk entries into the save. `afterhourState` is a public field; read it with `TryGetValue`. `IsDiscovered` switches to the same helper so the counter is read one way (also stops it creating an empty state for an `unlocked_by` area never visited — no consumer depends on that entry).
- **D4 — mapping in Core, reads in Game.** `GameValues` needs Unity/game types and can't run in `tests/Sim`; the name → qualified-area mapping is pure and is where a mistake would hide, so it lives in Core (`internal`, same assembly as Game; `tests/Sim` compiles Core sources directly).

---

## Context

### Current State (HEAD `b3c398c`, `tests/Sim` → `All checks passed` at plan time)
- `src/Core/Model.cs` L395–430: `public interface IBridge`; L419 `int GameValue(string name);` has no doc comment. L432–454 `IMemory`, `MemoryStore`. File already `using System;` (IBridge uses `Func`).
- `src/Core/Expr.cs` L48–54 `Split` cuts at the first `.` → `game.clears.vault` = head `game`, name `clears.vault`; L121 `case "game": return s => s.Run.Game(name);` accepts any name. L302 `IsWordChar` includes `.` but not `:`. No Expr change needed.
- `src/Core/ExpeditionRun.cs` L353 `public int Game(string name) => Bridge.GameValue(name);`; `MapContext` L1106 `Func<string, int> game`, L1152 `public int Game(string name) => game(name);`. No change needed.
- `src/Game/GameBridge.cs`:
  - L15 `private readonly string pack;` L20–26 ctor `GameBridge(Content content, string pack, TradeCounter counter, GameSounds sounds)` sets `reader`, `this.pack`, `counter`, `sounds`. `pack` is used at L70 (`TryAcceptAllOrDestroy(Ground, item, pack)`).
  - L161 `public int GameValue(string name) => GameValues.Get(name);`
  - L193–207 `internal static class GameValues { public static int Get(string name) { switch (name) { luck | roll_luck | wounds | maimed | default 0 } } }`.
- `src/Game/ExpeditionLocation.cs`:
  - L13 `internal const string ClearsCounter = "ef_clears";` L65 `internal static string LocationId(string areaId) => areaId.Replace(':', '_');`
  - L59 `map = new MapContext(area.Id, (pack, key, args) => ModHelper.GetLocalized(pack, key, args), GameValues.Get, new SaveMemory());`
  - L101–106 `IsDiscovered`: L105 `return PlayerStore.HasInstance && PlayerStore.Instance.GetAfterhourState(LocationId(area.UnlockedBy)).GetCounter(ClearsCounter) > 0;`
  - L124 `run = new ExpeditionRun(content, area, new GameBridge(content, area.Pack, trade, sounds), sides => RNG.GetRandomInt(1, sides),`
  - L193–196 `Leave`: `GetAfterhourState(Id)` then increments `ClearsCounter` on `cleared` — the counter `clears` reads. Unchanged.
- `src/Game/DevTools.cs` L35 writes `ClearsCounter` via `GetAfterhourState` (a write; creating the entry is correct there). Unchanged.
- Area ids are qualified `pack:local` (`PackLoader` `RefOrNull`; `content.Areas["a:a"]` in tests).
- Callers: `GameValues.Get` — only `GameBridge.cs:161`, `ExpeditionLocation.cs:59`. `new GameBridge` — only `ExpeditionLocation.cs:124`.
- Decompiled game (`ilspycmd`): `StoreStation.GetDayCounter()` static, null-safe; `PlayerStore.afterhourState` = `public Dictionary<string, AfterhourLocationState>`; `AfterhourLocationState.GetCounter(string key, int fallback = 0)`.
- ef-sim: `KitBridge.GameValue` reads `settings.Game[name]` (default 0) or coin; `SimConfig` accepts any key under `bridge.game` → `"clears.vault": 1` works unchanged. NER has no `game.clears`/`game.day` use → NER balance can't move.
- `tests/Sim/Program.cs`: `Main` list L26–44 ends `TestMapMemory();` (L44). `TestMapMemory` L554–606 is the pattern to follow (inline `PackSet`, `set.Compile`, `MapContext` with a lambda `game`, `MemoryStore`). Helpers: `Check(bool, string)`.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `src/Core/Model.cs` | Engine ↔ host contract | Doc on `IBridge.GameValue`; new `internal static class GameReads` with `ClearsArea` |
| `src/Game/GameBridge.cs` | Game host | Ctor takes `AreaDef`; `GameValue` passes area id; `GameValues.Get(name, areaId)` + `day` + clears; new `GameValues.Clears` |
| `src/Game/ExpeditionLocation.cs` | Map location | Map `game` lambda passes `area.Id`; `IsDiscovered` uses `GameValues.Clears`; `new GameBridge(content, area, …)` |
| `tests/Sim/Program.cs` | Framework tests | New `TestGameReads`, called from `Main` |

### Dependencies
- **Requires:** iterations 1–2 (memory store, map-time memory) — done, verified.
- **Blocks:** iteration 5 README (documents these reads); iteration 4 may lint `clears.AREA` names.

### Constraints
- Game layer can't be run or tested; it must compile with `-warnaserror` against the real game assemblies.
- `IBridge` is public: don't change its signatures (only doc).
- Source Hygiene (SKILL.md): no projex ids / plan labels / decision labels (`D1`…) in code; present tense; rationale comments name the rejected alternative.

### Assumptions
- A1: `ExpeditionFramework.csproj` compiles `src\**\*.cs` into one assembly, so an `internal` Core class is visible to `src/Game` (verified: `<Compile Include="src\**\*.cs" />`).
- A2: `AfterhourLocationState` and `StoreStation` are global-namespace types already reachable from `GameBridge.cs` (both used elsewhere in `src/Game` without extra `using`). If the build says otherwise, add the needed `using`, nothing else.

### Impact Analysis
- **Direct:** the 4 files above.
- **Adjacent:** `IsDiscovered` behavior identical except no empty state is created on read. `GameBridge.pack` still set from `area.Pack`.
- **Downstream:** pack data may now use `game.day`, `game.clears`, `game.clears.AREA`. External C# that constructs `GameBridge` — none (internal class).

---

## Implementation

### Step 1: Core — `GameReads.ClearsArea` + `IBridge.GameValue` doc

**Objective:** Pure, testable mapping from a `game.NAME` name to the area whose clears it counts; document all game reads at the contract.
**Confidence:** High
**Depends on:** None
**Do-Projex:** Encouraged

**Files:** `src/Core/Model.cs`

**Changes:**

```csharp
// Before (L419):
        int GameValue(string name);

// After:
        /// <summary>
        /// The player fact game.NAME reads, 0 for a name the host doesn't know. The game knows luck, roll_luck, wounds,
        /// maimed, day (the game's own day counter) and clears / clears.AREA (how often the player has cleared the reading
        /// area / AREA of its pack, see GameReads). A sim reads whatever its config lists for the name.
        /// </summary>
        int GameValue(string name);
```

Insert directly after the closing `}` of `IBridge` (before the `IMemory` doc comment):

```csharp
    /// <summary>
    /// The game.NAME reads the framework itself defines. game.clears counts how often the player has cleared the reading
    /// area; game.clears.AREA counts AREA of the reading area's pack. The named form lets hidden_when unlock an area on
    /// another area's clear OR on memory: unlocked_by is ANDed with hidden_when, so it can't say OR.
    /// </summary>
    internal static class GameReads
    {
        private const string Clears = "clears";

        /// <summary>Qualified id of the area whose clears game.NAME counts, or null when NAME isn't a clears read.</summary>
        public static string ClearsArea(string name, string areaId)
        {
            if (name == Clears) return areaId;
            if (name == null || areaId == null || name.Length <= Clears.Length + 1 || !name.StartsWith(Clears + ".", StringComparison.Ordinal)) return null;
            int colon = areaId.IndexOf(':');
            return areaId.Substring(0, colon + 1) + name.Substring(Clears.Length + 1);
        }
    }
```

(`colon == -1` → empty prefix → bare local id; area ids are always qualified in practice.)

**Rationale:** D4. Keeping the name grammar in Core lets `tests/Sim` pin it; `IBridge.GameValue` is the one place every host implementer reads.

**Verification:** V1 (after Step 3), V2.

**If this fails:** `git -C <work-root> checkout -- src/Core/Model.cs`.

---

### Step 2: Game — `day`, `clears`, area-aware `GameValues`

**Objective:** Serve the new reads in the game host, for run and map, reading clears without touching the save.
**Confidence:** High (compile-checked only; can't run in-game)
**Depends on:** Step 1
**Verify-Projex:** Encouraged

**Files:** `src/Game/GameBridge.cs`, `src/Game/ExpeditionLocation.cs`

**Changes — `src/Game/GameBridge.cs`:**

```csharp
// Before (L15, L20–26):
        private readonly string pack;
        ...
        public GameBridge(Content content, string pack, TradeCounter counter, GameSounds sounds)
        {
            reader = new ItemReader(content);
            this.pack = pack;
            this.counter = counter;
            this.sounds = sounds;
        }

// After:
        private readonly string pack;
        private readonly string areaId;
        ...
        public GameBridge(Content content, AreaDef area, TradeCounter counter, GameSounds sounds)
        {
            reader = new ItemReader(content);
            pack = area.Pack;
            areaId = area.Id;
            this.counter = counter;
            this.sounds = sounds;
        }
```

```csharp
// Before (L161):
        public int GameValue(string name) => GameValues.Get(name);
// After:
        public int GameValue(string name) => GameValues.Get(name, areaId);
```

```csharp
// Before (L193–207):
    /// <summary>The player facts packs can read as <c>game.NAME</c>. Anything else is read through a pack's own C# effect or condition.</summary>
    internal static class GameValues
    {
        public static int Get(string name)
        {
            switch (name)
            {
                case "luck": return HealthData.GetLuck();
                case "roll_luck": return HealthData.RollLuck() ? 1 : 0;
                case "wounds": return PlayerStore.HasInstance ? PlayerStore.Instance.healthData.woundState : 0;
                case "maimed": return StartingPerk.IsMaimed() ? 1 : 0;
                default: return 0;
            }
        }
    }

// After:
    /// <summary>
    /// The player facts packs can read as <c>game.NAME</c>, for the area they are read from. Anything else is read through
    /// a pack's own C# effect or condition.
    /// </summary>
    internal static class GameValues
    {
        public static int Get(string name, string areaId)
        {
            string clearsOf = GameReads.ClearsArea(name, areaId);
            if (clearsOf != null) return Clears(clearsOf);
            switch (name)
            {
                case "luck": return HealthData.GetLuck();
                case "roll_luck": return HealthData.RollLuck() ? 1 : 0;
                case "wounds": return PlayerStore.HasInstance ? PlayerStore.Instance.healthData.woundState : 0;
                case "maimed": return StartingPerk.IsMaimed() ? 1 : 0;
                case "day": return StoreStation.GetDayCounter();
                default: return 0;
            }
        }

        /// <summary>
        /// How often the player has cleared an area. Reads the save's afterhour states directly: GetAfterhourState adds an
        /// empty state to the save for any id it is asked about, so a mistyped game.clears.AREA read on every map refresh
        /// would fill the save with junk entries.
        /// </summary>
        public static int Clears(string areaId)
        {
            Dictionary<string, AfterhourLocationState> states = PlayerStore.HasInstance ? PlayerStore.Instance.afterhourState : null;
            if (states == null || !states.TryGetValue(ExpeditionLocation.LocationId(areaId), out AfterhourLocationState state) || state == null) return 0;
            return state.GetCounter(ExpeditionLocation.ClearsCounter);
        }
    }
```

(`System.Collections.Generic` is already imported in `GameBridge.cs`.)

**Changes — `src/Game/ExpeditionLocation.cs`:**

```csharp
// Before (L59):
            map = new MapContext(area.Id, (pack, key, args) => ModHelper.GetLocalized(pack, key, args), GameValues.Get, new SaveMemory());
// After:
            map = new MapContext(area.Id, (pack, key, args) => ModHelper.GetLocalized(pack, key, args), name => GameValues.Get(name, area.Id), new SaveMemory());
```

```csharp
// Before (L105):
            return PlayerStore.HasInstance && PlayerStore.Instance.GetAfterhourState(LocationId(area.UnlockedBy)).GetCounter(ClearsCounter) > 0;
// After:
            return GameValues.Clears(area.UnlockedBy) > 0;
```

```csharp
// Before (L124):
            run = new ExpeditionRun(content, area, new GameBridge(content, area.Pack, trade, sounds), sides => RNG.GetRandomInt(1, sides),
// After:
            run = new ExpeditionRun(content, area, new GameBridge(content, area, trade, sounds), sides => RNG.GetRandomInt(1, sides),
```

Touch nothing else in `src/Game` (`Leave`'s `GetAfterhourState(Id)` and `DevTools` are writes — creating the entry there is correct).

**Rationale:** D2, D3. Passing `AreaDef` instead of adding a second string param keeps the single caller simple and pack/area consistent.

**Verification:** V2, V3, V4, V5; J1.

**If this fails:** `git -C <work-root> checkout -- src/Game/GameBridge.cs src/Game/ExpeditionLocation.cs`. If only a type is unresolved (A2), add the missing `using` and rebuild.

---

### Step 3: Framework test `TestGameReads`

**Objective:** Pin the name grammar and prove `game.clears.AREA` + memory OR-unlock on the map.
**Confidence:** High
**Depends on:** Step 1
**Do-Projex:** Encouraged

**Files:** `tests/Sim/Program.cs`

**Changes:**

```csharp
// Before (L44):
        TestMapMemory();
// After:
        TestMapMemory();
        TestGameReads();
```

Insert the new function directly after `TestMapMemory`'s closing `}` (before the `// An ammo item is worth its `rounds`…` comment):

```csharp
    // game.clears reads the reading area's clears and game.clears.AREA another area of its pack, so hidden_when can
    // unlock an area on a clear OR on memory.
    private static void TestGameReads()
    {
        Check(GameReads.ClearsArea("clears", "a:cellar") == "a:cellar", "game.clears counts the reading area");
        Check(GameReads.ClearsArea("clears.vault", "a:cellar") == "a:vault", "game.clears.AREA counts AREA of the reading area's pack");
        Check(GameReads.ClearsArea("clears.", "a:cellar") == null && GameReads.ClearsArea("clearsvault", "a:cellar") == null
            && GameReads.ClearsArea("day", "a:cellar") == null && GameReads.ClearsArea(null, "a:cellar") == null, "other names aren't clears reads");

        var set = new PackSet(new Registry());
        set.Add("a", "ExpeditionFramework/main.json", f => @"{
          ""rooms"": { ""r"": { ""text"": ""k"" } },
          ""areas"": {
            ""vault"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ] },
            ""cellar"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ],
              ""hidden_when"": ""game.clears.vault < 1 && memory.key < 1"" } }
        }");
        Content content = set.Compile(out Dictionary<string, List<string>> problems, out Dictionary<string, List<string>> warnings);
        string all = string.Join("; ", problems.Values.SelectMany(l => l).Concat(warnings.Values.SelectMany(l => l)));
        Check(problems.Values.All(l => l.Count == 0) && warnings.Values.All(l => l.Count == 0), "game reads pack loads cleanly: " + all);
        if (problems.Values.Any(l => l.Count > 0)) return;

        AreaDef cellar = content.Areas["a:cellar"];
        var asked = new List<string>();
        int vaultClears = 0;
        var store = new MemoryStore();
        var map = new MapContext(cellar.Id, (p, k, a) => k, name => { asked.Add(name); return name == "clears.vault" ? vaultClears : 0; }, store);
        Check(cellar.HiddenWhen(map.Root), "the cellar stays hidden before the vault is cleared or the key remembered");
        Check(asked.Contains("clears.vault"), "game.clears.AREA reaches the host as clears.AREA: " + string.Join(",", asked));
        vaultClears = 1;
        Check(!cellar.HiddenWhen(map.Root), "clearing the vault unlocks the cellar");
        vaultClears = 0;
        store.Set("a", "key", 1);
        Check(!cellar.HiddenWhen(map.Root), "remembering the key also unlocks the cellar");
    }
```

**Rationale:** Covers SC1–SC2 deterministically; follows `TestMapMemory`'s shape.

**Verification:** V1.

**If this fails:** if the pack fails to load, read the printed problem and fix only the test JSON (e.g. a required area field) — not loader code. `git -C <work-root> checkout -- tests/Sim/Program.cs` to back out.

---

### Step 4: Gates + commit

**Objective:** Run the full suite, clean leftovers, commit code on the sprint branch.
**Confidence:** High
**Depends on:** Steps 1–3

Run V1–V10. Then confirm `git -C <work-root> status --porcelain` lists only the 4 files (bin/obj are ignored; delete any other untracked leftovers you created, e.g. `ner-after.txt` if written inside the repo). Commit with `stage-n-commit.ps1 <work-root> "feat(game): game.day and game.clears reads for packs" <4 files>` plus the trailer `--trailer Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` (dispatched model's name).

---

## Verification Plan

### Deterministic Suite

Run from `<work-root>`. PowerShell for V1, V2, V6, V7; bash blocks below for the rest.

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V1 | SC1, SC2, SC5; Steps 1, 3 | `dotnet run -c Release --project tests/Sim` | exit 0; last line `All checks passed`; no `FAIL` line | wrong name grammar, dotted game name not routed, OR unlock broken, regressions |
| V2 | SC3, SC4, SC5; Steps 1–2 | PowerShell: `dotnet build ExpeditionFramework.csproj -c Release --no-incremental -warnaserror -p:GameManaged="E:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed\\" -p:InstallDir="$env:TEMP\ef-pack-memory-install\\"` | exit 0 (`-warnaserror` ⇒ 0 warnings, 0 errors) | missed caller of `GameValues.Get`/`GameBridge` ctor, wrong game API names, doc warnings |
| V3 | SC3; Step 2 | bash block V3 | prints `3` | an `ExpeditionLocation` call site not updated |
| V4 | SC3; Step 2 | bash block V4 | prints `4` | `day`/clears not wired in `GameValues`, `GameValue` not passing area |
| V5 | SC3; Step 2 (D3) | bash block V5 | prints `0` then `1` (the remaining one is `Leave`'s write) | clears read through `GetAfterhourState` (save-writing) in `GameBridge.cs`; `IsDiscovered` still using it |
| V6 | SC5 | `dotnet run -c Release --project tools/EfSim -- lint samples/SampleArea --strict` then `dotnet run -c Release --no-build --project tools/EfSim -- lint "E:\SteamLibrary\steamapps\common\Probably Stolen\Modding\NightExpeditionsReborn" --strict` | both exit 0; lines `lint sample_area: 0 error(s), 0 warning(s)` and `lint night_expeditions_reborn: 0 error(s), 0 warning(s)` | Core change breaking real packs |
| V7 | SC5 | `dotnet run -c Release --no-build --project tools/EfSim -- run samples/SampleArea` | exit 0; last line `ef-sim sample_area: all checks passed` | sample runtime regression |
| V8 | SC5 | bash block V8 | `diff` prints nothing, exit 0 | NER balance change |
| V9 | Source Hygiene | bash block V9 | no output | unshipped refs / plan shape / changelog wording in added lines |
| V10 | SC5, Out of Scope | bash block V10 | no output | edits to ef-sim, sample, README |

Bash blocks (run from `<work-root>`):

```bash
NER="E:/SteamLibrary/steamapps/common/Probably Stolen/Modding/NightExpeditionsReborn"
BASE="C:/Users/BA/AppData/Local/Temp/claude/a48a32985dda25c5/057518b9-283c-4d89-bc27-9ae607bcf871/scratchpad/ner-baseline-4000.txt"

# V3 — the three ExpeditionLocation call sites
grep -cE 'name => GameValues\.Get\(name, area\.Id\)|return GameValues\.Clears\(area\.UnlockedBy\) > 0;|new GameBridge\(content, area, trade, sounds\)' src/Game/ExpeditionLocation.cs

# V4 — GameValues wiring
grep -cE 'GameValue\(string name\) => GameValues\.Get\(name, areaId\);|GameReads\.ClearsArea\(name, areaId\)|case "day": return StoreStation\.GetDayCounter\(\);|GetCounter\(ExpeditionLocation\.ClearsCounter\)' src/Game/GameBridge.cs

# V5 — clears never read through the save-writing accessor (code lines only; the Clears doc comment names it)
grep -vE '^\s*//' src/Game/GameBridge.cs | grep -c 'GetAfterhourState'
grep -vE '^\s*//' src/Game/ExpeditionLocation.cs | grep -c 'GetAfterhourState'

# V8 — NER 4000-run rows identical to the pre-sprint baseline
dotnet run -c Release --no-build --project tools/EfSim -- run "$NER" --runs 4000 > "$TEMP/ner-after.txt"
diff <(grep -E '^ne_' "$BASE") <(grep -E '^ne_' "$TEMP/ner-after.txt")

# V9 — no unshipped references, plan shape or changelog wording in lines added by this iteration
git diff b3c398c -- src tools tests | grep -E '^\+' | grep -nE 'projex|2611[0-9]{6}|Step [0-9]|SC[0-9]|iteration|\bD[0-9]\b|\bA[0-9]\b|no longer|used to'

# V10 — nothing outside the four planned files changed (prints any stray path)
git diff --name-only b3c398c -- . ':!.projex' | grep -vxE 'src/Core/Model\.cs|src/Game/GameBridge\.cs|src/Game/ExpeditionLocation\.cs|tests/Sim/Program\.cs'
```

### Judgment Checks

| ID | Check | Covers | Why not deterministic |
|----|-------|--------|-----------------------|
| J1 | Game diff: `GameValues.Clears` returns 0 with no `PlayerStore`, null dictionary, missing or null state; `Get` checks clears before the switch; `day` has no side effect; `IsDiscovered` logic otherwise unchanged (`hidden_when` first, null `unlocked_by` → true) | SC3 | Game-only code; can't run without the game, sprint can't play in-game |
| J2 | New comments are present tense and carry rationale naming the rejected alternative (`GetAfterhourState` creating save entries; `unlocked_by` AND vs OR) | Source Hygiene rule 6 | Prose quality |

### Acceptance Criteria Validation
| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| SC1 | V1 (`TestGameReads` grammar checks) | pass |
| SC2 | V1 (clean load; host asked `clears.vault`; hidden → unhidden by clear; unhidden by memory) | pass |
| SC3 | V2, V3, V4, V5, J1 | exit 0 / `3` / `4` / `0`,`1` / diff read |
| SC4 | V2 (doc compiles), diff read of `Model.cs` | doc lists all 7 reads |
| SC5 | V1, V2, V6, V7, V8, V10 | all green, NER rows identical, no stray paths |

---

## Rollback Plan

1. Uncommitted: `git -C <work-root> checkout -- src/Core/Model.cs src/Game/GameBridge.cs src/Game/ExpeditionLocation.cs tests/Sim/Program.cs`. Committed: `git -C <work-root> revert <commit>`.
2. Remove non-ignored leftovers; `git -C <work-root> status --porcelain` must be empty.

---

## Notes

### Risks
- `game.day` with no `StoreStation` (main menu, early load) reads 0. Map conditions are evaluated in the store/map scene where the station exists; README (iteration 5) should state "0 outside a loaded save".
- `game.clears` counts clears recorded at `Leave`, so during a run it reads clears from earlier nights, not tonight's. Intended; README should say so.
- A mistyped `game.clears.AREA` silently reads 0 (as any unknown `game.NAME` does). Iteration 4 may add a lint check against the pack's area ids.
- `IsDiscovered` no longer materializes an empty afterhour state for an unvisited `unlocked_by` area. Nothing reads such an empty entry (counters fall back to 0); the area's own state is created when it is visited (`Leave`).

### Open Questions
- (none)

### Split Decision
No split — single scope, within size budget (4 steps, ~300 lines).
