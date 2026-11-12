# Pack Memory: ef-sim Memory Model + Read-Never-Written Warning

> **Status:** Complete
> **Author:** Claude Opus 5.5 (plan)
> **Parent:** 2611121459-pack-memory-sprint.md
> **Sprint:** 2611121459-pack-memory-sprint.md — iteration 4
> **Source:** Sprint iteration objective (`2611121459-pack-memory-sprint.md` § Current Position, item 4)
> **Related Projex:** 2611121459-pack-memory-sprint.md | 2611121506-pack-memory-runtime-store-plan.md | 2611121531-pack-memory-map-reads-plan.md | 2611121550-pack-memory-game-reads-plan.md | 2611121559-pack-memory-game-reads-audit.md | 2611121622-pack-memory-sim-memory-patch.md
> Partial Execution: all steps [PATCHED] — executed in full via 2611121622-pack-memory-sim-memory-patch.md (commit 9360b37); nothing remains open.
> **Worktree:** No — execute in the sprint work-root on `projex/sprint/2611121459-pack-memory`

---

## Summary

Two pieces. (A) `ef-sim run` models memory: a `sim.json` row gains an optional `memory` object — `start` (values the pack's memory holds before the row's first run), `carry` (one store shared by all the row's runs, so each run sees earlier runs' writes) and `expect` (what memory must hold after the row's last run; a miss is a `FAIL`). (B) Loading a pack warns once per `memory.NAME` / `memory.$NAME` the pack reads but never writes. The warning is raised by the pack loader (`PackLoader.Finish`), so `ef-sim lint` (and `ef-sim run`, and the game log) report it through the existing warnings path; `lint --strict` fails on it.

**Scope:** `src/Core/Expr.cs`, `src/Core/PackLoader.cs`, `src/Core/Builtins.cs` (warning) | `tools/EfSim/SimConfig.cs`, `tools/EfSim/KitBridge.cs`, `tools/EfSim/PackRun.cs` (sim memory) | `tests/Sim/Program.cs` (1 new test, 2 test packs gain writes). One projex scope (repo root `.projex/`).
**Estimated Changes:** 7 files. Core: 1 internal `Expr.Compile` overload + 1 field + 1 line in `Atom`; `PackLoader`: 2 fields, 1 method, 1 `Finish` block, 3 call sites, 1 `Spec` method; `Builtins`: 3 note calls. ef-sim: 2 new classes + 2 parse fns + 1 row key; `KitBridge` ctor param; `PackRun.Play` takes pack id, returns final store, + expectation check. Tests: 1 new fn + `Main` line + 2 edited JSON strings.

---

## Objective

### Problem / Gap / Need

- `KitBridge.Memory` is a fresh `MemoryStore` per run (`tools/EfSim/KitBridge.cs:83-84`), so a pack's carried counter can't be simulated or checked.
- `sim.json` `runs[n]` accepts only `area, kit, bot, runs, seed` (`SimConfig.cs:77`); no way to seed memory or assert on it.
- A pack that reads `memory.x` but never writes it silently reads 0 / "" forever (typo or missing `set`). Nothing reports it.

### Success Criteria

- [ ] SC1: `runs[n].memory = { start?, carry?, expect? }` parses strictly (unknown key, wrong type, bad name, `min` > `max` → `ERROR sim.json: …`, exit 2). Rows without `memory` behave exactly as before.
- [ ] SC2: `start` seeds the pack's memory (numbers by `NAME`, text by `$NAME`) before each run (no carry) or once before the row (carry). With `carry: true` one store serves every run of the row.
- [ ] SC3: `expect` checks the store after the row's last run — number exact (`5`) or range (`{min?, max?}`), text exact (`"x"`); each miss prints `FAIL runs[R]: memory.NAME should be …, got …` and `ef-sim run` exits 1.
- [ ] SC4: Loading warns exactly once per (pack, name) read but never written by that pack, for numbers (expressions, conditions incl. map-time `hidden_when` / intel `when`) and text (`set_text` `value`, `{memory.$x}` templates, line `args`). `set`, `add`, `set_text` `var` count as writes. A write by another pack does not count. Warning text: `memory.NAME is read but never written by this pack, so it always reads 0` (text: `… always reads ""`).
- [ ] SC5: Gates green: `tests/Sim` `All checks passed`; game-assembly build 0 warnings / 0 errors; `lint --strict` 0/0 on sample and NER; `ef-sim run` sample passes; NER 4000-run table rows identical to the pre-sprint baseline.

### Out of Scope

- Sample pack counter, README (iteration 5).
- Bot rules reading memory (`RuleBot` compiles with no pack; `memory.*` stays uncompilable there).
- `expect.*` global memory cases, per-run memory traces, printing memory in the results table.
- Lint of `game.clears.AREA` ids (audit's optional idea; not this objective).
- C# API for memory; per-enemy dynamic names.

### Decisions

- D1 — **Warning lives in the loader, not in ef-sim's `Lint`.** Only the compiler knows which strings are paths and in which role (read vs write); a text scan in `Lint` would re-implement the path grammar. Loader warnings already flow to `lint` notes (`Lint.Load` → `PackSet.Compile` warnings) and to the game log. Warning, not problem: the pack still runs.
- D2 — **`add` counts as a write only.** It reads its own target, but a name a pack only ever `add`s to is written.
- D3 — **Number and text names are separate keys** (`visits` vs `$visits`), as `MemoryStore` / `SaveMemory` keep them: writing `memory.count` does not satisfy a read of `memory.$count`.
- D4 — **`carry` defaults to false.** A row's runs are samples of independent nights; a shared store makes each run's odds depend on earlier runs. Authors opt in.
- D5 — **Expectation is per row, against the last run's store** (with carry: the shared store). `--runs N` changes how many runs a row plays, so an exact count over a carried row holds only at the row's own `runs` (same class of caveat as "never offered" at low run counts; README in iteration 5 notes it).
- D6 — **`sim.json` memory names are written as after `memory.`** (`visits`, `$rival`). A key starting `memory.` is an error, so the obvious typo can't silently seed an unused name.
- D7 — **Expr hook is a new internal `Compile` overload**, not a new optional param on the public one: `Expr` is public and adding a parameter would change its binary signature for compiled callers.

---

## Context

### Current State (HEAD `7dd0d82`, `tests/Sim` → `All checks passed` at plan time)

- Memory paths compile in `Expr.Reader` / `Writer` / `TextWriter` / `TextReader` (`src/Core/Expr.cs:98-200`), all taking `pack`. Expressions read only number memory: `Atom` (`Expr.cs:312-346`) calls `Reader(word, named, pack: pack)`; `$` is not a word char, so `memory.$x` never appears in an expression.
- Compile-time call sites with a pack: `PackLoader.ValueOf` → `Expr.Compile` (`PackLoader.cs:1414-1430`; all string conditions go through it, incl. `hidden_when` `:892`, intel `when` `:982`, line-case `when` `:1303`); `PackLoader.Template` → `Expr.TextReader` (`:1244-1247`, `{…}` pieces in text keys); `PackLoader.TextArg` → `Expr.TextReader` (`:1313-1314`, line `args`); `Builtins` `set_text` (`Builtins.cs:267-287`: `TextWriter(var)`, `TextReader(value)`); `Builtins.Assign` (`:338-355`, `set`/`add`: `Reader`+`Writer` on `var`, value via `c.Value` → `ValueOf`).
- `PackLoader.Finish` (`:555-572`) runs deferred checks after every pack compiled; `Warn(at, msg)` (`:615-621`) charges the warning to `current`. `Builtins` already reaches loader-only members via the concrete `PackLoader.Spec` (`Builtins.cs:363-365`, comment explains why not `ISpec`). `Spec` has `PackId`, `Path` (`:1560-1561`).
- ef-sim: `PackRun.Run` loops rows (`PackRun.cs:57-62`) into `Play` (`:149-190`), which builds one `KitBridge` per run (`:157`). `CheckLoot` also builds a `KitBridge` (`:123`). Failures list order: load errors, sorted problems, packing, loot, never offered (`:70-73`). `SimConfig.Read` is strict (`Only`, typed `Int`/`Str`/`Obj`); FormatException → `ERROR sim.json: <msg>`, exit 2 (`PackRun.cs:27-28`).
- Tests that must change: `TestMapMemory` (`tests/Sim/Program.cs:556-607`) and `TestGameReads` (`:611-643`) assert zero warnings while reading `memory.unlocked`, `memory.heard`, `memory.key` that their packs never write → the new warning would fail them. `TestMemory` (`:505-553`) writes everything it reads; `Main` (`:23`) fails on any framework/sample warning (neither reads memory).
- NER and sample read no `memory.*` (grep: no hits) → no new warnings, no behavior change.
- Fixture pre-check (scratch pack, see Step 4): with current ef-sim it lints 0/0 and runs 5/5 cleared — a one-room area whose `enter` runs once per run.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `src/Core/Expr.cs` | Expression compiler | Internal `Compile` overload with `memoryRead` callback; `Atom` reports `memory.*` reads |
| `src/Core/PackLoader.cs` | Pack compiler | Track memory reads/writes per pack; warn in `Finish`; note reads in `ValueOf`, `Template`, `TextArg`; `Spec.NoteMemory` |
| `src/Core/Builtins.cs` | Built-in effects | `set`/`add`/`set_text` note writes; `set_text` `value` notes a read |
| `tools/EfSim/SimConfig.cs` | sim.json reader | `SimMemory`, `SimMemoryCheck`; `runs[n].memory` parse |
| `tools/EfSim/KitBridge.cs` | Sim bridge | Ctor takes optional `IMemory` |
| `tools/EfSim/PackRun.cs` | `ef-sim run` | Per-row store (start/carry), expectation check → failures |
| `tests/Sim/Program.cs` | Framework tests | `TestMemoryWarnings`; writes added to `TestMapMemory`/`TestGameReads` packs |

### Dependencies

- **Requires:** iterations 1-3 (store, map reads, game reads) — on branch.
- **Blocks:** iteration 5 (sample counter checked by `ef-sim`, README).

### Constraints

- NER 4000-run table must be byte-identical: no change in random draws for rows without `memory` (`MemoryStore` draws nothing; construction order of `Random` untouched).
- Source hygiene (SKILL.md § Source Hygiene): no projex ids / step / decision labels in comments; present tense; non-obvious choices carry a rationale naming the rejected alternative.
- Do not edit NER, sample, README.

### Assumptions

- A1: The fixture's single-room area runs `hall.enter` exactly once per run (pre-check: 100% cleared, one room). V6's exact counts (`15`, `11`, `1`) depend on it. If V6 reports a different `visits`, first check how often `enter` runs per run before touching code; if it is >1 per run, adjust the fixture expectations to `start + runs × enters` and record it in the patch doc.
- A2: `Dictionary` enumeration in insertion order (no removals) keeps warning order deterministic.
- A3: Every `ISpec` handed to a built-in compiler is a `PackLoader.Spec` (already relied on at `Builtins.cs:365`); the plan still uses `as` + `?.` so a foreign `ISpec` only skips noting.

### Impact Analysis

- **Direct:** 7 files above.
- **Adjacent:** every pack load (game, tests, ef-sim) gains the memory read/write bookkeeping — O(paths), negligible. Packs that read memory without writing it get a new warning (none today).
- **Downstream:** iteration 5 README documents `runs[n].memory` and the warning; sample uses them.

---

## Implementation

### Overview

Step 1 adds the loader warning (Core). Step 2 adds/adjusts framework tests. Step 3 adds the ef-sim memory model. Step 4 builds the scratch fixture, runs the full suite, commits.

### Step 1: Core — memory read/write bookkeeping + warning

**Objective:** SC4.
**Confidence:** High
**Depends on:** None
Verify-Projex: Encouraged

**Files:** `src/Core/Expr.cs`, `src/Core/PackLoader.cs`, `src/Core/Builtins.cs`

**Changes — `Expr.cs`:**

```csharp
// Before (fields, ctor, Compile — lines 16-44):
        private readonly string pack;
        private int at;
        private string error;

        private Expr(string text, Func<string, Value> named, Func<string, bool> hasMod, string pack)
        {
            this.text = text;
            this.named = named;
            this.hasMod = hasMod;
            this.pack = pack;
        }

        /// <summary> …existing doc… </summary>
        public static Value Compile(string text, out string error, Func<string, Value> named = null, Func<string, bool> hasMod = null, string pack = null)
        {
            var parser = new Expr(text, named, hasMod, pack);

// After:
        private readonly string pack;
        private readonly Action<string> memoryRead;
        private int at;
        private string error;

        private Expr(string text, Func<string, Value> named, Func<string, bool> hasMod, string pack, Action<string> memoryRead)
        {
            this.text = text;
            this.named = named;
            this.hasMod = hasMod;
            this.pack = pack;
            this.memoryRead = memoryRead;
        }

        /// <summary> …existing doc unchanged… </summary>
        public static Value Compile(string text, out string error, Func<string, Value> named = null, Func<string, bool> hasMod = null, string pack = null) =>
            Compile(text, out error, named, hasMod, pack, null);

        /// <summary>
        /// As the public Compile, also handing <paramref name="memoryRead"/> each memory.NAME path the text reads. A separate
        /// overload rather than another optional parameter: Expr is public, and a new parameter changes the method's binary
        /// signature for code compiled against it.
        /// </summary>
        internal static Value Compile(string text, out string error, Func<string, Value> named, Func<string, bool> hasMod, string pack, Action<string> memoryRead)
        {
            var parser = new Expr(text, named, hasMod, pack, memoryRead);
```

(the rest of the old `Compile` body stays as is, now inside the internal overload.)

```csharp
// Before (Atom, ~339-345):
            Value reader = Reader(word, named, pack: pack);
            if (reader == null)
            {
                Fail("unknown path '" + word + "' (…)");
                return s => 0;
            }
            return reader;

// After:
            Value reader = Reader(word, named, pack: pack);
            if (reader == null)
            {
                Fail("unknown path '" + word + "' (…)");   // message unchanged
                return s => 0;
            }
            if (memoryRead != null && word.StartsWith("memory.", StringComparison.Ordinal)) memoryRead(word);
            return reader;
```

**Changes — `PackLoader.cs`:**

(a) Fields, after `warningsByPack` (line 139):

```csharp
        // memory.* names per pack: where each is first read, and which are written. Packs never see each other's memory,
        // so a name its own pack never writes always reads 0 or "" — a typo or a missing set. Finish warns about those.
        private readonly Dictionary<(string Pack, string Name), string> memoryReads = new Dictionary<(string Pack, string Name), string>();
        private readonly HashSet<(string Pack, string Name)> memoryWrites = new HashSet<(string Pack, string Name)>();
```

(b) New method, after `Warn` (after line 621):

```csharp
        /// <summary>
        /// Notes that <paramref name="pack"/> reads or writes <paramref name="path"/>. Only memory.NAME and memory.$NAME count;
        /// the $ stays in the name, as text and number memory are separate values.
        /// </summary>
        internal void NoteMemory(string pack, string path, string at, bool write)
        {
            const string head = "memory.";
            if (pack == null || path == null || path.Length <= head.Length || !path.StartsWith(head, StringComparison.Ordinal)) return;
            (string, string) key = (pack, path.Substring(head.Length));
            if (write) memoryWrites.Add(key);
            else if (!memoryReads.ContainsKey(key)) memoryReads[key] = at;
        }
```

(c) `Finish`, before `knownItemTags = null;` (line 571):

```csharp
            // A warning, not a problem: the pack still runs, the name just never holds anything.
            foreach (KeyValuePair<(string Pack, string Name), string> read in memoryReads.Where(r => !memoryWrites.Contains(r.Key)))
            {
                current = read.Key.Pack;
                Warn(read.Value, "memory." + read.Key.Name + " is read but never written by this pack, so it always reads "
                    + (read.Key.Name.StartsWith("$", StringComparison.Ordinal) ? "\"\"" : "0"));
            }
            memoryReads.Clear();
            memoryWrites.Clear();
```

(d) `ValueOf` (line 1420):

```csharp
// Before:
                Value value = Expr.Compile(text, out string error, name => NamedValue(pack, name, at), HasMod, pack);
// After:
                Value value = Expr.Compile(text, out string error, name => NamedValue(pack, name, at), HasMod, pack, path => NoteMemory(pack, path, at, write: false));
```

(e) `Template` (lines 1245-1247):

```csharp
// Before:
                Func<IScope, string> reader = Expr.TextReader(path, fieldRequired: false, pack: pack);
                if (reader == null) Problem(at, "can't fill in {" + path + "}");
                else pieces.Add(reader);
// After:
                Func<IScope, string> reader = Expr.TextReader(path, fieldRequired: false, pack: pack);
                if (reader == null) Problem(at, "can't fill in {" + path + "}");
                else
                {
                    NoteMemory(pack, path, at, write: false);
                    pieces.Add(reader);
                }
```

(f) `TextArg` (lines 1313-1314):

```csharp
// Before:
                Func<IScope, string> reader = Expr.TextReader(text, fieldRequired: true, pack: pack);
                if (reader != null) return reader;
// After:
                Func<IScope, string> reader = Expr.TextReader(text, fieldRequired: true, pack: pack);
                if (reader != null)
                {
                    NoteMemory(pack, text, at, write: false);
                    return reader;
                }
```

(g) `Spec`, after `public void Warn(string message) => loader.Warn(Path, message);` (line 1641):

```csharp
            /// <summary>Notes a memory path this spec's pack reads or writes; see PackLoader.NoteMemory.</summary>
            internal void NoteMemory(string path, bool write) => loader.NoteMemory(PackId, path, Path, write);
```

**Changes — `Builtins.cs`:**

```csharp
// Before (set_text, 267-287):
            r.Effects["set_text"] = c =>
            {
                Action<IScope, string> write = Expr.TextWriter(c.Require("var") ?? "", c.PackId);
                if (write == null)
                {
                    c.Error("\"var\" must be a path like run.$name, memory.$name or self.$name");
                    return s => { };
                }
                if (c.Has("line"))
                {
                    ILine line = c.Line("line");
                    return s => write(s, line.Render(s));
                }
                Func<IScope, string> read = Expr.TextReader(c.Require("value") ?? "", fieldRequired: false, pack: c.PackId);
                if (read == null)
                {
                    c.Error("\"value\" must be a path like item.name, or use \"line\"");
                    return s => { };
                }
                return s => write(s, read(s));
            };

// After:
            r.Effects["set_text"] = c =>
            {
                string target = c.Require("var") ?? "";
                Action<IScope, string> write = Expr.TextWriter(target, c.PackId);
                if (write == null)
                {
                    c.Error("\"var\" must be a path like run.$name, memory.$name or self.$name");
                    return s => { };
                }
                var spec = c as PackLoader.Spec;
                spec?.NoteMemory(target, write: true);
                if (c.Has("line"))
                {
                    ILine line = c.Line("line");      // its {memory.$x} pieces are noted where the line compiles
                    return s => write(s, line.Render(s));
                }
                string source = c.Require("value") ?? "";
                Func<IScope, string> read = Expr.TextReader(source, fieldRequired: false, pack: c.PackId);
                if (read == null)
                {
                    c.Error("\"value\" must be a path like item.name, or use \"line\"");
                    return s => { };
                }
                spec?.NoteMemory(source, write: false);
                return s => write(s, read(s));
            };
```

```csharp
// Before (Assign, 353):
            Value value = c.Value("value", 0);
            return s => write(s, apply(read(s), value(s)));
// After:
            // add reads its own var too, but only to change it, so set and add both count as writing it.
            (c as PackLoader.Spec)?.NoteMemory(path, write: true);
            Value value = c.Value("value", 0);
            return s => write(s, apply(read(s), value(s)));
```

**Rationale:** Recording happens exactly where paths are already resolved with their pack, so read/write roles come from the compiler, not a text scan (D1). `Finish` runs after every pack compiled, so writes anywhere in the pack (any file, any order) are known before warning. Dedupe by (pack, name) keeps one warning per name, at its first read.

**Verification:** V1 (via Step 2 tests), V2, V3.

**If this fails:** `git -C <work-root> checkout -- src/Core/Expr.cs src/Core/PackLoader.cs src/Core/Builtins.cs`.

---

### Step 2: Framework tests

**Objective:** Prove SC4; keep existing tests' "loads cleanly" meaning.
**Confidence:** High
**Depends on:** Step 1
Do-Projex: Encouraged

**Files:** `tests/Sim/Program.cs`

**Changes:**

(a) `TestMapMemory` pack (line 560): the room writes the names the map reads.

```csharp
// Before:
          ""rooms"": { ""r"": { ""text"": ""k"" } },
// After (only in TestMapMemory's pack, the first occurrence after line 559):
          ""rooms"": { ""r"": { ""text"": ""k"", ""enter"": [
            { ""set"": { ""var"": ""memory.unlocked"", ""value"": 1 } }, { ""add"": { ""var"": ""memory.heard"", ""value"": 1 } } ] } },
```

(b) `TestGameReads` pack (line 620), same edit with:

```csharp
          ""rooms"": { ""r"": { ""text"": ""k"", ""enter"": [ { ""set"": { ""var"": ""memory.key"", ""value"": 1 } } ] } },
```

Neither test begins a run, so the writes change no assertion; they only make the packs warning-free again.

(c) New test, placed after `TestGameReads`, and `TestMemoryWarnings();` added to `Main` after `TestGameReads();` (line 45):

```csharp
    // A memory name a pack reads but never writes always reads 0 or "", so loading warns once per name, at its first read.
    // Another pack's write doesn't count, and memory.count and memory.$count are different names.
    private static void TestMemoryWarnings()
    {
        var set = new PackSet(new Registry());
        set.Add("a", "ExpeditionFramework/main.json", f => @"{
          ""rooms"": { ""r"": { ""text"": ""k"", ""enter"": [
            { ""add"": { ""var"": ""memory.kept"", ""value"": 1 } },
            { ""set"": { ""var"": ""memory.count"", ""value"": 1 } },
            { ""set_text"": { ""var"": ""memory.$name"", ""value"": ""player.name"" } },
            { ""set"": { ""var"": ""run.x"", ""value"": ""memory.kept + memory.ghost + memory.ghost"" } },
            { ""set_text"": { ""var"": ""run.$y"", ""value"": ""memory.$name"" } },
            { ""set_text"": { ""var"": ""run.$z"", ""value"": ""memory.$lost"" } },
            { ""set_text"": { ""var"": ""run.$w"", ""line"": ""hi_{memory.$gone}"" } },
            { ""set"": { ""var"": ""run.v"", ""value"": ""memory.theirs"" } },
            { ""set_text"": { ""var"": ""run.$c"", ""value"": ""memory.$count"" } }
          ] } },
          ""areas"": { ""a"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ], ""hidden_when"": ""memory.ghost > 0"" } }
        }");
        set.Add("b", "ExpeditionFramework/main.json", f => @"{
          ""rooms"": { ""r"": { ""text"": ""k"", ""enter"": [ { ""set"": { ""var"": ""memory.theirs"", ""value"": 1 } } ] } }
        }");
        set.Compile(out Dictionary<string, List<string>> problems, out Dictionary<string, List<string>> warnings);
        Check(problems.Values.All(l => l.Count == 0), "memory warning packs: " + string.Join("; ", problems.Values.SelectMany(l => l)));
        List<string> a = warnings.TryGetValue("a", out List<string> wa) ? wa : new List<string>();
        string all = string.Join("; ", a);
        const string Unwritten = " is read but never written by this pack";
        foreach (string name in new[] { "ghost", "$lost", "$gone", "theirs", "$count" })
        {
            Check(a.Count(w => w.Contains("memory." + name + Unwritten)) == 1, "one warning for memory." + name + ": " + all);
        }
        Check(a.Count == 5, "exactly the five unwritten names warn: " + all);
        Check(a.Any(w => w.Contains("memory.ghost" + Unwritten + ", so it always reads 0")), "a number warning says it reads 0: " + all);
        Check(a.Any(w => w.Contains("memory.$lost" + Unwritten + ", so it always reads \"\"")), "a text warning says it reads \"\": " + all);
        Check(!warnings.TryGetValue("b", out List<string> wb) || wb.Count == 0, "a pack that only writes doesn't warn: " + string.Join("; ", wb ?? new List<string>()));
    }
```

`kept`, `count`, `$name` must not warn — covered by `a.Count == 5` plus the five per-name checks.

**Rationale:** Covers each read site (expression, map condition, `set_text` value, template piece), each write kind (`add`, `set`, `set_text`), dedupe, cross-pack isolation, number/text separation.

**Verification:** V1.

**If this fails:** If only `a.Count` is off, print `all` and find the read site not noted / double-noted in Step 1; fix Step 1, not the test. Revert: `git -C <work-root> checkout -- tests/Sim/Program.cs`.

---

### Step 3: ef-sim memory model

**Objective:** SC1–SC3.
**Confidence:** High
**Depends on:** None (independent of Steps 1–2)
Verify-Projex: Encouraged

**Files:** `tools/EfSim/SimConfig.cs`, `tools/EfSim/KitBridge.cs`, `tools/EfSim/PackRun.cs`

**Changes — `SimConfig.cs`:**

(a) `SimRow` (lines 9-14) gains a field:

```csharp
internal sealed class SimRow
{
    public string Area, Bot; public int Runs, Seed;
    public readonly List<string> Kits = new List<string>();     // run n uses Kits[n % Kits.Count]
    public SimMemory Memory;                                    // null: no "memory", every run starts with empty memory
    public string Kit => string.Join("+", Kits);
}
```

(b) New classes after `SimPacking` (line 15):

```csharp
// runs[n].memory: what the pack's memory.* holds before the row's runs, whether one run's writes reach the next, and what it
// must hold after the last run. Names are as the pack writes them after "memory." ($ for text).
internal sealed class SimMemory
{
    public readonly Dictionary<string, int> Numbers = new Dictionary<string, int>();
    public readonly Dictionary<string, string> Texts = new Dictionary<string, string>();   // keyed without the $, as the store keeps them
    public bool Carry;                                                                     // false: every run starts from the start values
    public readonly List<SimMemoryCheck> Expect = new List<SimMemoryCheck>();

    public MemoryStore Start(string pack)
    {
        var store = new MemoryStore();
        foreach (KeyValuePair<string, int> v in Numbers) store.Set(pack, v.Key, v.Value);
        foreach (KeyValuePair<string, string> v in Texts) store.SetText(pack, v.Key, v.Value);
        return store;
    }

    // One failure per expect entry the store misses.
    public List<string> Check(string pack, IMemory memory, string at)
    {
        var failures = new List<string>();
        foreach (SimMemoryCheck c in Expect)
        {
            string where = at + ": memory." + c.Name;
            if (c.Name.StartsWith("$"))
            {
                string got = memory.GetText(pack, c.Name.Substring(1));
                if (got != c.Text) failures.Add(where + " should be \"" + c.Text + "\", got \"" + got + "\"");
                continue;
            }
            int value = memory.Get(pack, c.Name);
            if (c.Min != null && c.Min == c.Max) { if (value != c.Min) failures.Add(where + " should be " + c.Min + ", got " + value); }
            else if (c.Min is int min && value < min) failures.Add(where + " should be at least " + min + ", got " + value);
            else if (c.Max is int max && value > max) failures.Add(where + " should be at most " + max + ", got " + value);
        }
        return failures;
    }
}
internal sealed class SimMemoryCheck { public string Name, Text; public int? Min, Max; }   // Name keeps its $; Text only for $ names
```

(c) Row parse (lines 77 and 93-94):

```csharp
// Before:
            Only(row, at, "area", "kit", "bot", "runs", "seed");
            …
            if (r.Runs <= 0) throw new FormatException(at + ".runs must be above 0");
            config.Rows.Add(r);
// After:
            Only(row, at, "area", "kit", "bot", "runs", "seed", "memory");
            …
            if (r.Runs <= 0) throw new FormatException(at + ".runs must be above 0");
            if (row.TryGetValue("memory", out object m)) r.Memory = MemoryOf(m, at + ".memory");
            config.Rows.Add(r);
```

(d) New parse fns, after `ReadBridge` (after line 173):

```csharp
    // runs[n].memory: { start?, carry?, expect? }. start and expect are keyed by name (visits, $rival); expect takes a whole
    // number (exactly), { min?, max? } or, for a $ name, text.
    private static SimMemory MemoryOf(object value, string at)
    {
        Dictionary<string, object> obj = Obj(value, at);
        Only(obj, at, "start", "carry", "expect");
        var memory = new SimMemory();
        if (obj.TryGetValue("carry", out object c)) memory.Carry = c is bool b ? b : throw new FormatException(at + ".carry must be true or false");
        if (obj.TryGetValue("start", out object s))
        {
            foreach (KeyValuePair<string, object> v in Obj(s, at + ".start"))
            {
                string vat = at + ".start." + v.Key;
                if (IsTextName(v.Key, vat)) memory.Texts[v.Key.Substring(1)] = Str(v.Value, vat);
                else memory.Numbers[v.Key] = Int(v.Value, vat);
            }
        }
        if (obj.TryGetValue("expect", out object e))
        {
            foreach (KeyValuePair<string, object> v in Obj(e, at + ".expect"))
            {
                string vat = at + ".expect." + v.Key;
                var check = new SimMemoryCheck { Name = v.Key };
                if (IsTextName(v.Key, vat)) check.Text = Str(v.Value, vat);
                else if (v.Value is Dictionary<string, object> range)
                {
                    Only(range, vat, "min", "max");
                    if (range.Count == 0) throw new FormatException(vat + " needs min or max");
                    if (range.TryGetValue("min", out object lo)) check.Min = Int(lo, vat + ".min");
                    if (range.TryGetValue("max", out object hi)) check.Max = Int(hi, vat + ".max");
                    if (check.Min > check.Max) throw new FormatException(vat + ".min is above max");
                }
                else if (v.Value is double) check.Min = check.Max = Int(v.Value, vat);
                else throw new FormatException(vat + " must be a whole number or {min, max}");
                memory.Expect.Add(check);
            }
        }
        return memory;
    }

    // Whether a memory name is text ($NAME). "memory.visits" would seed a name the pack never reads, so it is refused.
    private static bool IsTextName(string name, string at)
    {
        if (name.StartsWith("memory.")) throw new FormatException(at + ": leave out memory. (write visits or $rival)");
        if (name.Length == 0 || name == "$") throw new FormatException(at + ": empty memory name");
        return name.StartsWith("$");
    }
```

**Changes — `KitBridge.cs`:**

```csharp
// Before (16, 21 and 83-84):
    public KitBridge(SimKit kit, SimBridge settings, Content content, Random random, HashSet<string> problems)
    {
        this.settings = settings;
        this.random = random;
        this.problems = problems;
        Credits = kit.Credits;
    …
    // A fresh store per run, so memory never carries between runs.
    public IMemory Memory { get; } = new MemoryStore();

// After:
    public KitBridge(SimKit kit, SimBridge settings, Content content, Random random, HashSet<string> problems, IMemory memory = null)
    {
        this.settings = settings;
        this.random = random;
        this.problems = problems;
        Memory = memory ?? new MemoryStore();
        Credits = kit.Credits;
    …
    // The row's store when sim.json gives the row memory; otherwise an empty one for this run alone.
    public IMemory Memory { get; }
```

**Changes — `PackRun.cs`:**

```csharp
// Before (Run, 57-62 and 70):
            for (int r = 0; r < config.Rows.Count; r++)
            {
                SimRow row = config.Rows[r];
                config.Bots.TryGetValue(row.Bot, out RuleBot bot);
                Play(content, areas[row.Area], row, row.Kits.Select(k => config.Kits[k]).ToList(), bot, config.Bridge, runsOverride > 0 ? runsOverride : row.Runs, localize, offered, problems);
            }
            …
            failures.AddRange(problems.OrderBy(p => p, StringComparer.Ordinal));

// After:
            var memory = new List<string>();
            for (int r = 0; r < config.Rows.Count; r++)
            {
                SimRow row = config.Rows[r];
                config.Bots.TryGetValue(row.Bot, out RuleBot bot);
                IMemory last = Play(content, id, areas[row.Area], row, row.Kits.Select(k => config.Kits[k]).ToList(), bot, config.Bridge, runsOverride > 0 ? runsOverride : row.Runs, localize, offered, problems);
                if (row.Memory != null) memory.AddRange(row.Memory.Check(id, last, "runs[" + r + "]"));
            }
            …
            failures.AddRange(problems.OrderBy(p => p, StringComparer.Ordinal));
            failures.AddRange(memory);
```

```csharp
// Before (Play, 149-157 and end 188-190):
    private static void Play(Content content, AreaDef area, SimRow row, List<SimKit> kits, RuleBot bot, SimBridge settings, int runs, Localizer localize,
        HashSet<string> offered, HashSet<string> problems)
    {
        var random = new Random(row.Seed);
        string where = row.Area + " " + row.Kit + " " + row.Bot;
        int cleared = 0, knocked = 0, extracted = 0;
        for (int n = 0; n < runs; n++)
        {
            var bridge = new KitBridge(kits[n % kits.Count], settings, content, random, problems);
    …
        Console.WriteLine(string.Format(…));
    }

// After:
    // Plays a row's runs and returns the memory store its last run used.
    private static IMemory Play(Content content, string pack, AreaDef area, SimRow row, List<SimKit> kits, RuleBot bot, SimBridge settings, int runs,
        Localizer localize, HashSet<string> offered, HashSet<string> problems)
    {
        var random = new Random(row.Seed);
        string where = row.Area + " " + row.Kit + " " + row.Bot;
        int cleared = 0, knocked = 0, extracted = 0;
        // Runs share one store only when the row carries memory: rows sample independent nights, and a shared store would
        // make each run's odds depend on the runs before it.
        IMemory carried = row.Memory != null && row.Memory.Carry ? row.Memory.Start(pack) : null;
        IMemory memory = null;
        for (int n = 0; n < runs; n++)
        {
            memory = carried ?? row.Memory?.Start(pack) ?? new MemoryStore();
            var bridge = new KitBridge(kits[n % kits.Count], settings, content, random, problems, memory);
    …
        Console.WriteLine(string.Format(…));   // unchanged
        return memory;
    }
```

Everything else in `Play` (loop body, counters, table line) and `CheckLoot`'s `KitBridge` call stay unchanged.

**Rationale:** Store creation draws no random numbers and `random` is still created once per row in the same place, so rows without `memory` replay identically (NER table). Failures join the existing `FAIL` list, after run problems.

**Verification:** V4, V5, V6, V7, V8.

**If this fails:** `git -C <work-root> checkout -- tools/EfSim/SimConfig.cs tools/EfSim/KitBridge.cs tools/EfSim/PackRun.cs`.

---

### Step 4: Fixture, gates, commit

**Objective:** SC1–SC5 end to end.
**Confidence:** High
**Depends on:** Steps 1–3

1. Build the scratch fixture outside the repo (bash, block F below). It never enters the repo.
2. Run V1–V11.
3. `git -C <work-root> status --porcelain` must list only the 7 files (bin/obj are ignored). Delete any untracked leftover you created inside the repo.
4. Commit: `stage-n-commit.ps1 <work-root> "feat(ef-sim): pack memory in sim.json; warn on memory read but never written" "--trailer Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>" src/Core/Expr.cs src/Core/PackLoader.cs src/Core/Builtins.cs tests/Sim/Program.cs tools/EfSim/SimConfig.cs tools/EfSim/KitBridge.cs tools/EfSim/PackRun.cs` (use the dispatched model's name in the trailer).

**Block F — fixture** (bash, any cwd). `hall.enter` adds 1 to `memory.visits` and, once `visits >= 13`, sets `memory.$title` to the player's name (`you` in ef-sim).

```bash
FX="$TEMP/ef-mem-fixture"; rm -rf "$FX"; mkdir -p "$FX/ExpeditionFramework" "$FX/Localization"
cat > "$FX/manifest.xml" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<Manifest>
  <ID>mem_fixture</ID>
  <Prerequisites><Mod>expedition_framework</Mod></Prerequisites>
</Manifest>
EOF
cat > "$FX/ExpeditionFramework/main.json" <<'EOF'
{
  "rooms": {
    "hall": {
      "text": "mf_room_hall",
      "enter": [
        { "add": { "var": "memory.visits", "value": 1 } },
        { "if": { "when": "memory.visits >= 13", "then": [ { "set_text": { "var": "memory.$title", "value": "player.name" } } ] } }
      ]
    }
  },
  "areas": {
    "hall": { "title": "mf_area_title", "player": { "hp": 10 }, "kits": ["expedition_framework:explore"], "layout": [ { "room": "hall" } ] }
  }
}
EOF
printf 'mf_room_hall,A bare hall.\nmf_area_title,The Hall\n' > "$FX/Localization/en.csv"
cat > "$FX/sim.json" <<'EOF'
{
  "version": 1,
  "kits": { "bare": { "credits": 0 } },
  "runs": [
    { "area": "hall", "kit": "bare", "runs": 5, "seed": 1,
      "memory": { "start": { "visits": 10 }, "carry": true, "expect": { "visits": 15, "$title": "you" } } },
    { "area": "hall", "kit": "bare", "runs": 5, "seed": 2,
      "memory": { "start": { "visits": 10, "$title": "none" }, "expect": { "visits": { "min": 11, "max": 11 }, "$title": "none" } } },
    { "area": "hall", "kit": "bare", "runs": 5, "seed": 3, "memory": { "expect": { "visits": 1, "$title": "" } } }
  ]
}
EOF
cp "$FX/sim.json" "$FX/sim.good.json"
```

Expected values: row 0 carries 10 → 15 (title set on the 3rd run); row 1 restarts each run at 10 → 11, title stays `none`; row 2 starts empty each run → 1, title `""`.

**Verification:** all of V1–V11.

**If this fails:** fix the failing step; do not commit until V1–V11 pass. A V5 diff means a row without `memory` replays differently — check `Play` did not move `new Random` or add a draw.

---

## Verification Plan

### Deterministic Suite

Run from `<work-root>`. V2 in PowerShell; everything else bash (Git Bash). Build once with V1/V3; later rows may use `--no-build`.

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V1 | SC4, SC5; Steps 1–2 | `dotnet run -c Release --project tests/Sim` | exit 0; last line `All checks passed`; no `FAIL` | warning missing / duplicated / wrong pack; existing tests broken by the warning |
| V2 | SC5; Step 1 | PowerShell: `dotnet build ExpeditionFramework.csproj -c Release --no-incremental -warnaserror -p:GameManaged="E:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed\\" -p:InstallDir="$env:TEMP\ef-pack-memory-install\\"` | exit 0 | Core change breaking the game assembly; doc warnings |
| V3 | SC4, SC5 | `dotnet run -c Release --project tools/EfSim -- lint samples/SampleArea --strict` then `dotnet run -c Release --no-build --project tools/EfSim -- lint "$NER" --strict` | both exit 0; `lint sample_area: 0 error(s), 0 warning(s)` and `lint night_expeditions_reborn: 0 error(s), 0 warning(s)` | false-positive memory warning on real packs |
| V4 | SC1, SC5 | `dotnet run -c Release --no-build --project tools/EfSim -- run samples/SampleArea` | exit 0; last line `ef-sim sample_area: all checks passed` | rows without `memory` broken |
| V5 | SC1, SC5 | bash V5 | `diff` prints nothing, exit 0 | NER balance change from the `Play` rework |
| V6 | SC2, SC3 | bash V6 | exit 0; last line `ef-sim mem_fixture: all checks passed`; no `FAIL`/`WARN` | start not seeded, carry not shared, no-carry leaking, text memory not seeded/checked |
| V7 | SC3 | bash V7 | exit 1; output contains exactly `FAIL runs[0]: memory.visits should be 99, got 15` and `FAIL runs[1]: memory.$title should be "x", got "none"`; last line `ef-sim mem_fixture: 2 check(s) failed` | expectation not checked / wrong row index / wrong message |
| V8 | SC1 | bash V8 | six lines, in order: `2 ERROR sim.json: runs[0].memory.carry must be true or false` · `2 ERROR sim.json: runs[0].memory.start.$title must be text` · `2 ERROR sim.json: runs[0].memory.start.memory.visits: leave out memory. (write visits or $rival)` · `2 ERROR sim.json: runs[0].memory: unknown key seed` · `2 ERROR sim.json: runs[0].memory.expect.visits.min is above max` · `2 ERROR sim.json: runs[0].memory.expect.visits must be a whole number or {min, max}` | lax parsing of the new keys |
| V9 | SC4 | bash V9 | exit 1; one line matching `^WARN .*memory\.ghost is read but never written by this pack, so it always reads 0$`; line `lint mem_fixture: 0 error(s), 1 warning(s)` | warning not reaching `ef-sim lint` / `--strict` |
| V10 | Source Hygiene | bash V10 | no output | unshipped refs / plan shape / changelog wording in added lines |
| V11 | Scope | bash V11 | no output | edits outside the 7 planned files |

Bash blocks (run from `<work-root>`, after block F):

```bash
NER="E:/SteamLibrary/steamapps/common/Probably Stolen/Modding/NightExpeditionsReborn"
BASE="C:/Users/BA/AppData/Local/Temp/claude/a48a32985dda25c5/057518b9-283c-4d89-bc27-9ae607bcf871/scratchpad/ner-baseline-4000.txt"
FX="$TEMP/ef-mem-fixture"
EFSIM="dotnet run -c Release --no-build --project tools/EfSim --"

# V5 — NER 4000-run rows identical to the pre-sprint baseline
$EFSIM run "$NER" --runs 4000 > "$TEMP/ner-after.txt"
diff <(grep -E '^ne_' "$BASE") <(grep -E '^ne_' "$TEMP/ner-after.txt")

# V6 — fixture passes
cp "$FX/sim.good.json" "$FX/sim.json"; $EFSIM run "$FX"; echo "exit $?"

# V7 — two wrong expectations fail with row-indexed messages
sed -e 's/"visits": 15/"visits": 99/' -e 's/"\$title": "none" } } },/"$title": "x" } } },/' "$FX/sim.good.json" > "$FX/sim.json"
$EFSIM run "$FX"; echo "exit $?"
cp "$FX/sim.good.json" "$FX/sim.json"

# V8 — strict reader: each bad memory object → exit 2 + one ERROR line
for MEM in '{ "carry": "yes" }' '{ "start": { "$title": 1 } }' '{ "start": { "memory.visits": 1 } }' '{ "carry": true, "seed": 1 }' \
           '{ "expect": { "visits": { "min": 5, "max": 2 } } }' '{ "expect": { "visits": true } }'; do
  printf '{ "version": 1, "kits": { "bare": {} }, "runs": [ { "area": "hall", "kit": "bare", "runs": 1, "memory": %s } ] }\n' "$MEM" > "$FX/sim.json"
  OUT=$($EFSIM run "$FX"); echo "$? $OUT"
done
cp "$FX/sim.good.json" "$FX/sim.json"

# V9 — lint warns on a read never written, and --strict fails on it
cp "$FX/ExpeditionFramework/main.json" "$FX/main.good.json"
sed -i 's/"memory.visits >= 13"/"memory.visits + memory.ghost >= 13"/' "$FX/ExpeditionFramework/main.json"
$EFSIM lint "$FX" --strict; echo "exit $?"
cp "$FX/main.good.json" "$FX/ExpeditionFramework/main.json"

# V10 — no unshipped references, plan shape or changelog wording in lines added by this iteration
git diff 7dd0d82 -- src tools tests | grep -E '^\+' | grep -nE 'projex|2611[0-9]{6}|Step [0-9]|SC[0-9]|iteration|\bD[0-9]\b|\bA[0-9]\b|no longer|used to'

# V11 — nothing outside the seven planned files changed
git diff --name-only 7dd0d82 -- . ':!.projex' | grep -vxE 'src/Core/(Expr|PackLoader|Builtins)\.cs|tests/Sim/Program\.cs|tools/EfSim/(SimConfig|KitBridge|PackRun)\.cs'
```

V8 note: the reader stops at the first error, so each variant prints exactly one `ERROR` line; `OUT` holds that line (the kits/runs preamble is valid). V7 note: the `sed` targets row 1's `expect` (`"$title": "none" } } },` occurs only there — row 1's `start` text is followed by `}, "expect"`).

### Judgment Checks

| Check | Covers | Why not deterministic |
|-------|--------|-----------------------|
| New comments state rationale with the rejected alternative (Expr overload vs optional param; warning vs problem; carry default) and contain no changelog wording | Source Hygiene rule 5/6 | Rationale quality needs reading; V10 only catches banned tokens |
| `SimMemory.Check` messages read naturally for each expect form | SC3 | Wording judgment beyond V7's two exact lines |

### Acceptance Criteria Validation

| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| SC1 strict parse; rows without memory unchanged | V8, V4, V5 | six exact ERROR lines; sample passes; NER rows identical |
| SC2 start + carry | V6 | fixture passes (15 / 11 / 1, titles you / none / "") |
| SC3 expectation | V6, V7 | pass on good values; two exact FAIL lines, exit 1 |
| SC4 warning | V1 (TestMemoryWarnings), V9, V3 | 5 exact warnings in test; fixture lint warns once, strict exit 1; real packs 0/0 |
| SC5 gates | V1, V2, V3, V4, V5 | all green |

---

## Rollback Plan

Nothing is committed until Step 4. To abandon: `git -C <work-root> checkout -- src/Core/Expr.cs src/Core/PackLoader.cs src/Core/Builtins.cs tests/Sim/Program.cs tools/EfSim/SimConfig.cs tools/EfSim/KitBridge.cs tools/EfSim/PackRun.cs`, then `rm -rf "$TEMP/ef-mem-fixture"`. If already committed: `git -C <work-root> revert <commit>` (no amend — the user's hook blocks it).

---

## Notes

### Risks

- A1 (one `enter` per run) wrong → V6 counts differ. Mitigation: A1 procedure; the carry/no-carry contrast still proves SC2.
- A pack that writes memory only from C# (no API today) would get a false warning. Acceptable: warning only, and no C# memory API exists.
- `--runs N` makes exact carried-count expectations fail (D5). Iteration 5's README must say so; the sample should prefer a `min` range or accept the caveat.

### Iteration 5 carry-forward

README: `runs[n].memory` (`start` / `carry` / `expect` forms, names without `memory.`, `--runs` caveat), the read-never-written warning; plus the items already listed in the sprint nav (memory, intel `when`, `hidden_when` AND/OR idiom, `game.day` = 0 outside a save, clears exclude tonight's run).

### Open Questions

- [ ] (none)

### Split Decision

`No split — single scope, within size budget` (>500 lines from before/after code, but 4 steps, so the size heuristic does not trip; Core warning and ef-sim model are both small and gated by one shared suite).
