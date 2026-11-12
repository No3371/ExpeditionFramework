# ef-sim Skeleton: lint + sim.json v1 + kit bridge + random bot

> **Status:** Complete
> **Author:** Claude Opus 5.5 (plan)
> **Source:** Direct request — sprint iteration 1 objective (derive)
> **Sprint:** 2611121010-pack-defined-sims-sprint.md — iteration 1
> **Parent:** 2611121010-pack-defined-sims-sprint.md
> **Related Projex:** 2611121010-pack-defined-sims-sprint.md | 2611120231-authoring-tooling-sim-testing-expansion-explore.md | 2611120250-framework-expansion-synthesis-eval.md
> **Partial Execution:** [PATCHED] all steps 1–6 completed via 2611121035-ef-sim-skeleton-patch.md (code commit 84cced3)
> **Worktree:** Yes — sprint worktree `.projexwt/2611121010-pack-defined-sims`, branch `projex/sprint/2611121010-pack-defined-sims`; all commits land there, never on `main`

---

## Summary

New net8.0 console `tools/EfSim` (assembly `ef-sim`) with two commands: `lint` (today's lint, source moved, shared with `tests/Sim`) and `run` (reads pack's `sim.json` v1, plays a run matrix with a framework-owned kit bridge + built-in random bot, runs built-in checks, prints balance table, exits 0/1). Proven on `samples/SampleArea` with a `sim.json` whose expectations equal the framework sim's never-offered list.

**Scope:** framework repo only — `tools/EfSim/**` (new), `tests/Sim/Lint.cs` → `tools/EfSim/Lint.cs` (move + small refactor), `tests/Sim/Sim.csproj`, `samples/SampleArea/sim.json` (new), `README.md`.
**Estimated Changes:** 4 new C# files + 1 moved/refactored, 2 csproj (1 new), 1 JSON, README section.

---

## Objective

### Problem / Gap / Need
Sim checks today live in per-pack C# (`tests/Sim/Program.cs` here, NER's `tests/Sim/Program.cs`), each with its own `FakeBridge`. Sprint goal: one framework executable that lints + simulates any pack from data. This iteration = skeleton proven on the sample pack.

### Success Criteria
- [ ] C1 `tools/EfSim/EfSim.csproj` builds (net8.0, AssemblyName `ef-sim`), no warnings from `tools/EfSim/*.cs`
- [ ] C2 `ef-sim lint <folder> [--mod id] [--strict]` output + exit code byte-identical to `tests/Sim -- lint` for valid args (sample pack, a broken fixture, NER folder); lint source exists once (`tools/EfSim/Lint.cs`), `tests/Sim/Lint.cs` gone
- [ ] C3 `ef-sim lint samples/SampleArea --strict` → `lint sample_area: 0 error(s), 0 warning(s)`, exit 0
- [ ] C4 `samples/SampleArea/sim.json` exists; its `expect.offered` set == the `expected` array in `tests/Sim/Program.cs` `Play` (16 ids)
- [ ] C5 `ef-sim run samples/SampleArea` exits 0, prints one table row per matrix row with cleared / knocked out / extracted %, last line `ef-sim sample_area: all checks passed`
- [ ] C6 Checks are live: bogus expected id → exit 1 + `FAIL never offered: no_such_option`; removed text key → exit 1 + `FAIL missing text`; unknown kit in a row → exit 2; missing `sim.json` → exit 2
- [ ] C7 `ef-sim run` deterministic: two runs → identical stdout
- [ ] C8 `--runs N` overrides every row's run count (table shows N)
- [ ] C9 `dotnet run -c Release --project tests/Sim` → exit 0, `All checks passed`
- [ ] C10 Game assembly builds 0 Warning(s) 0 Error(s) (worktree-safe props)
- [ ] C11 README documents `ef-sim lint`, `ef-sim run`, `sim.json` v1

### Out of Scope
- Rule-based bots / expression-language policies (iteration 2) — `bot` accepts only `random`
- C# hook, Gunsmith loot check, packing checks (`MapContext.Missing`), balance-range expectations
- Any file under `NightExpeditionsReborn` (read-only use as lint-identity input only)
- NER parity tuning of bridge behaviour (luck `GameValue`, trades, wounds table) — iteration 5
- Changing `tests/Sim/Program.cs` (its `Play`/`FakeBridge` stay as-is)
- Self-contained publish

---

## Context

### Current State
- `tests/Sim/Sim.csproj`: net8.0 exe, `EnableDefaultCompileItems=false`, compiles `Program.cs`, `Lint.cs`, `..\..\src\Core\**\*.cs`, `..\..\api\*.cs`.
- `tests/Sim/Program.cs` `Main`: `root` = 5 × `..` from `AppContext.BaseDirectory` (bin/<cfg>/net8.0 → repo root); `lint` arg → `Lint.Run(root, rest)`. `Play` (l.777): one `Random(42)`, 5000 runs (`SIM_RUNS`) of `sample_area:flooded_cellar`, bridge alternates knife-only / everything (`n % 2`), 2000-step guard, no-options guard, random pick with `head_home` 3%, never-offered list of 16 ids (offered **and picked**).
- `tests/Sim/Lint.cs` (118 lines): `Lint.Run(frameworkRoot, args)` — parses args, reads `<ID>` from `manifest.xml`, `PackSet` (HasMod = framework | id | `--mod`) compiles framework + pack, errors = problems + `missing text pack:key` (en.csv of pack + framework), notes = warnings + locale parity; prints `ERROR`/`WARN` lines + `lint <id>: N error(s), M warning(s)`; exit 1 on errors (or warnings w/ `--strict`), 2 bad args. `ReadCsv`, `ParityProblems` internal.
- `ExpeditionFramework.csproj` (game, netstandard2.1) compiles only `src\**\*.cs` → a `tools/` folder cannot leak into the game assembly.
- Core API used: `PackSet`, `Content` (`Problems`, `Areas` keyed `pack:area`, `TextKeys`), `ExpeditionRun(content, area, bridge, roll, localize, playerName)` with `Begin/Over/Outcome/Options/Perform/Trading/FinishTrade/Hud/Get/Scene/Depth`, `RunOption.Interaction.Id`, `RunOption.Label`, `RunItem` (`Tags`, `Props`, `ApplyStats(content)`), `IBridge` (`src/Core/Model.cs:394`), `Localizer(pack, key, object[] args)`, `Json.Parse` → `Dictionary<string,object>` / `List<object>` / `string` / `double` / `bool` / `null`.
- Outcomes: engine `Finish("cleared")` when past last room, `"knocked_out"`, `leave` effect default `"left"`; NER instead sets run var `cleared`.
- NER sim (parity baseline, informs bridge design): Localizer records `missing text at run time` / `too few arguments`; labels containing `{` → `unfilled label`; `NightLog` with `{` → unfilled night log; 800-step guard; `Equip` adds `in_hand`; `GiveItem` puts item on a ground list; `RollTable` returns table id; random policy `head_home` 5%; `if (run.Trading) run.FinishTrade();` and `run.Hud()` per step.
- Baseline measured at plan time (worktree HEAD `e70b013`): `tests/Sim` → `All checks passed`; `tests/Sim -- lint samples/SampleArea --strict` → `lint sample_area: 0 error(s), 0 warning(s)`.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `tools/EfSim/EfSim.csproj` | new exe project | create (AssemblyName `ef-sim`) |
| `tools/EfSim/Program.cs` | command dispatch | create |
| `tools/EfSim/Lint.cs` | lint (moved from `tests/Sim/Lint.cs`) | `git mv` + extract `ReadId`/`Load` helpers |
| `tools/EfSim/SimConfig.cs` | `sim.json` v1 reader | create |
| `tools/EfSim/KitBridge.cs` | framework-owned data-driven `IBridge` | create |
| `tools/EfSim/PackRun.cs` | `run` command: matrix, random bot, checks, table | create |
| `tests/Sim/Sim.csproj` | framework sim | include `..\..\tools\EfSim\Lint.cs` |
| `samples/SampleArea/sim.json` | sample pack sim data | create |
| `README.md` | docs § Building and testing | document `ef-sim` + `sim.json` v1 |

### Dependencies
- **Requires:** nothing (first iteration)
- **Blocks:** iteration 2 (rule-based bots plug into `bot`), 3 (NER `sim.json`), 4 (C# hook), 5 (parity)

### Constraints
- Lint logic must exist once; both entry points keep working.
- `tests/Sim/Program.cs` unchanged; it still calls `Lint.Run(root, …)`.
- New code compiles Core by source include, like `tests/Sim` — no project reference to the game csproj.
- Determinism: every random draw comes from the row's seeded `Random`.
- Game-assembly build from worktree must use explicit `-p:GameManaged=… -p:InstallDir=<scratch>` (never deploys into live `Mods/`).
- `bin/`/`obj/` gitignored; don't commit build output or fixtures.

### Assumptions (verify early)
- A1 `tools/EfSim/bin/<cfg>/net8.0` is 5 levels below repo root, same as `tests/Sim` → same root formula works. Step 1 guards with a `ExpeditionFramework/main.json` existence check.
- A2 With the kit bridge (`ApplyStats`, `Equip` → true, ground list) the 16 expected ids are still all offered in 2 × 2500 random runs. "Offered" (appears in `Options()`) is weaker than the old "offered and picked", so this should hold.
- A3 `run.Hud()` per step + runtime-text checks raise nothing on the sample pack (tests/Sim never exercised these). If they do, it's a real finding — see Step 4 "If this fails".
- A4 `dotnet run --no-build` emits only program output (no build chatter) → stdout diffable.

### Impact Analysis
- **Direct:** files in Key Files.
- **Adjacent:** `tests/Sim` build (Lint include path); game build (unaffected — compiles `src\**` only).
- **Downstream:** README users invoking `tests/Sim -- lint` (still works); later iterations extend `SimConfig`/`KitBridge`/`PackRun`.

---

## Implementation

### Overview
Move lint into the new tool and split out reusable load helpers → add `sim.json` reader → kit bridge → `run` command → sample data → docs. All shell commands below: Git Bash, cwd = work-root (`.projexwt/2611121010-pack-defined-sims`).

`sim.json` v1 schema (decided here):

```json
{
  "version": 1,
  "kits": {
    "<kit name>": {
      "credits": 0,
      "items": [ { "id": "item_id", "count": 1, "tags": ["melee", "in_hand"], "props": { "sides": 4 } } ]
    }
  },
  "runs": [
    { "area": "flooded_cellar", "kit": "<kit name>", "bot": "random", "runs": 1000, "seed": 42 }
  ],
  "expect": { "offered": ["option_id"] }
}
```

Rules: `version` must be `1`. Required: `version`, `kits` (non-empty), `runs` (non-empty), each item `id`, each row `area`/`kit`/`runs` (>0). Optional with defaults: `credits` 0, `items` [], `count` 1, `tags` [], `props` {}, `bot` `"random"`, `seed` `42 + row index`, `expect` {}, `offered` []. Unknown keys anywhere → error. Tags may carry a leading `#` (stripped). `area` without `:` is prefixed `<pack id>:`. `bot` must be `random` in v1. Integers must be whole JSON numbers.

---

### Step 1: Create `tools/EfSim`, move Lint, extract shared helpers

**Objective:** `ef-sim lint` works with byte-identical behaviour; `tests/Sim -- lint` still works from the moved source.
**Confidence:** High
**Depends on:** None
**Do-Projex:** Encouraged
**Verify-Projex:** Encouraged

**Files:**
- `tests/Sim/Lint.cs` → `tools/EfSim/Lint.cs` (`git mv`)
- `tools/EfSim/EfSim.csproj` (new)
- `tools/EfSim/Program.cs` (new)
- `tests/Sim/Sim.csproj`

**Changes:**

`git mv tests/Sim/Lint.cs tools/EfSim/Lint.cs` (create `tools/EfSim` first).

`tools/EfSim/EfSim.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>ef-sim</AssemblyName>
    <RootNamespace>EfSim</RootNamespace>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="*.cs" />
    <Compile Include="..\..\src\Core\**\*.cs" />
    <Compile Include="..\..\api\*.cs" />
  </ItemGroup>
</Project>
```

`tests/Sim/Sim.csproj`:
```xml
// Before:
    <Compile Include="Lint.cs" />
// After:
    <Compile Include="..\..\tools\EfSim\Lint.cs" />
```

`tools/EfSim/Lint.cs` — refactor `Run` into helpers; keep `ReadCsv`, `Placeholder`, `ParityProblems`, `Listed` unchanged. Output order must stay: errors = problems (pack dict order) then missing text (sorted); notes = warnings then parity.
```csharp
// Before (top of class):
    private const string Framework = "expedition_framework";
    private const string MainFile = "ExpeditionFramework/main.json";
    private const string Usage = "usage: dotnet run --project tests/Sim -- lint <pack folder> [--mod <id>]... [--strict]";
// After:
    internal const string Framework = "expedition_framework";
    internal const string MainFile = "ExpeditionFramework/main.json";
    private const string Usage = "usage: ef-sim lint <pack folder> [--mod <id>]... [--strict]";
```
```csharp
// After — Run body from the manifest read onward (arg parsing above it unchanged):
        string id = ReadId(folder);
        if (id == null)
        {
            Console.WriteLine("ERROR no <ID> in manifest.xml");
            return 2;
        }
        Load(frameworkRoot, folder, id, mods, out Dictionary<string, Dictionary<string, string>> text, out List<string> errors, out List<string> notes);
        Dictionary<string, string> en = text[id];

        string localization = Path.Combine(folder, "Localization");
        // ... locale-parity loop unchanged (adds to notes) ...
        // ... printing + return unchanged ...
    }

    // The pack's <ID> from manifest.xml, or null.
    internal static string ReadId(string folder)
    {
        string manifest = Path.Combine(folder, "manifest.xml");
        Match idMatch = File.Exists(manifest) ? Regex.Match(File.ReadAllText(manifest), @"<ID>\s*([^<\s]+)\s*</ID>") : Match.Empty;
        return idMatch.Success ? idMatch.Groups[1].Value : null;
    }

    // Compiles the framework and the pack as the game would. errors: load problems, then text keys missing from en.csv;
    // notes: warnings. text: en.csv rows by pack (the pack and the framework).
    internal static Content Load(string frameworkRoot, string folder, string id, ISet<string> mods,
        out Dictionary<string, Dictionary<string, string>> text, out List<string> errors, out List<string> notes)
    {
        // In the game the framework and the pack itself are always installed, so mod() answers yes for them too.
        var set = new PackSet(new Registry()) { HasMod = mod => mod == Framework || mod == id || mods.Contains(mod) };
        if (id != Framework) set.Add(Framework, MainFile, file => File.ReadAllText(Path.Combine(frameworkRoot, file)));
        set.Add(id, MainFile, file => File.ReadAllText(Path.Combine(folder, file)));
        Content content = set.Compile(out Dictionary<string, List<string>> problems, out Dictionary<string, List<string>> warnings);

        errors = new List<string>();
        notes = new List<string>();
        foreach (List<string> list in problems.Values) errors.AddRange(list);
        foreach (List<string> list in warnings.Values) notes.AddRange(list);

        text = new Dictionary<string, Dictionary<string, string>> { [id] = ReadCsv(Path.Combine(folder, "Localization", "en.csv")) };
        if (id != Framework) text[Framework] = ReadCsv(Path.Combine(frameworkRoot, "Localization", "en.csv"));
        foreach ((string pack, string key) in content.TextKeys.OrderBy(k => k.Pack).ThenBy(k => k.Key))
        {
            if (text.TryGetValue(pack, out Dictionary<string, string> rows) && !rows.ContainsKey(key)) errors.Add("missing text " + pack + ":" + key);
        }
        return content;
    }
```
`Run` passes its `HashSet<string> mods` (an `ISet<string>`). Also update the file's header comment to mention `ef-sim lint` if it names `tests/Sim`.

`tools/EfSim/Program.cs`:
```csharp
using System;
using System.IO;
using System.Linq;

// ef-sim: lints and simulates a pack folder from data the pack carries.
internal static class Program
{
    private static int Main(string[] args)
    {
        // bin/<config>/net8.0 → tools/EfSim → repo root, the same way tests/Sim finds it.
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        if (!File.Exists(Path.Combine(root, Lint.MainFile)))
        {
            Console.WriteLine("ERROR framework not found at " + root);
            return 2;
        }
        string[] rest = args.Skip(1).ToArray();
        switch (args.Length > 0 ? args[0] : "")
        {
            case "lint": return Lint.Run(root, rest);
            case "run": return PackRun.Run(root, rest);
            default:
                Console.WriteLine("usage: ef-sim lint <pack folder> [--mod <id>]... [--strict]");
                Console.WriteLine("       ef-sim run <pack folder> [--runs <n>]");
                return 2;
        }
    }
}
```
(`PackRun` lands in Step 4; to build Step 1 in isolation, temporarily stub `case "run"` or do Steps 1–4 before the first `ef-sim` build. Final state must match the above.)

**Rationale:** Move (not copy) keeps one lint source; `ef-sim` is its natural owner, `tests/Sim` includes it by path. Extracting `ReadId`/`Load` lets `run` reuse manifest, compile and missing-text logic instead of duplicating it. Usage string changes to `ef-sim` (bad-args path only; outputs for valid args unchanged).

**Verification:** V1, V2, V3, V4, V5

**If this fails:** `git mv tools/EfSim/Lint.cs tests/Sim/Lint.cs`, restore `Sim.csproj` include, delete `tools/EfSim`.

---

### Step 2: `sim.json` v1 reader

**Objective:** Strict parse of the schema in Overview into typed objects; any problem → `FormatException` with a path-qualified message.
**Confidence:** High
**Depends on:** Step 1
**Do-Projex:** Encouraged

**Files:**
- `tools/EfSim/SimConfig.cs` (new)

**Changes:**
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExpeditionFramework.Core;

internal sealed class SimItem { public string Id; public int Count = 1; public readonly List<string> Tags = new List<string>(); public readonly Dictionary<string, int> Props = new Dictionary<string, int>(); }
internal sealed class SimKit { public int Credits; public readonly List<SimItem> Items = new List<SimItem>(); }
internal sealed class SimRow { public string Area, Kit, Bot; public int Runs, Seed; }

// sim.json v1. Read strictly: an unknown key or a wrong type is an error, so a typo can't silently change what runs.
internal sealed class SimConfig
{
    public static readonly string[] Bots = { "random" };
    public readonly Dictionary<string, SimKit> Kits = new Dictionary<string, SimKit>();
    public readonly List<SimRow> Rows = new List<SimRow>();
    public readonly List<string> Offered = new List<string>();

    public static SimConfig Read(string file)
    {
        if (!File.Exists(file)) throw new FormatException("no sim.json in the pack folder");
        Dictionary<string, object> top = Obj(Json.Parse(File.ReadAllText(file)), "sim.json");
        Only(top, "sim.json", "version", "kits", "runs", "expect");
        if (Int(Need(top, "version", "sim.json"), "version") != 1) throw new FormatException("version must be 1");
        var config = new SimConfig();
        foreach (KeyValuePair<string, object> k in Obj(Need(top, "kits", "sim.json"), "kits"))
        {
            string at = "kits." + k.Key;
            Dictionary<string, object> kit = Obj(k.Value, at);
            Only(kit, at, "credits", "items");
            var parsed = new SimKit { Credits = kit.TryGetValue("credits", out object c) ? Int(c, at + ".credits") : 0 };
            List<object> items = kit.TryGetValue("items", out object i) ? List(i, at + ".items") : new List<object>();
            for (int n = 0; n < items.Count; n++)
            {
                string iat = at + ".items[" + n + "]";
                Dictionary<string, object> item = Obj(items[n], iat);
                Only(item, iat, "id", "count", "tags", "props");
                var si = new SimItem { Id = Str(Need(item, "id", iat), iat + ".id") };
                if (item.TryGetValue("count", out object cnt)) si.Count = Int(cnt, iat + ".count");
                if (item.TryGetValue("tags", out object tags)) si.Tags.AddRange(List(tags, iat + ".tags").Select(t => Str(t, iat + ".tags").TrimStart('#')));
                if (item.TryGetValue("props", out object props))
                {
                    foreach (KeyValuePair<string, object> p in Obj(props, iat + ".props")) si.Props[p.Key] = Int(p.Value, iat + ".props." + p.Key);
                }
                parsed.Items.Add(si);
            }
            config.Kits[k.Key] = parsed;
        }
        if (config.Kits.Count == 0) throw new FormatException("kits is empty");
        List<object> rows = List(Need(top, "runs", "sim.json"), "runs");
        if (rows.Count == 0) throw new FormatException("runs is empty");
        for (int n = 0; n < rows.Count; n++)
        {
            string at = "runs[" + n + "]";
            Dictionary<string, object> row = Obj(rows[n], at);
            Only(row, at, "area", "kit", "bot", "runs", "seed");
            var r = new SimRow
            {
                Area = Str(Need(row, "area", at), at + ".area"),
                Kit = Str(Need(row, "kit", at), at + ".kit"),
                Bot = row.TryGetValue("bot", out object b) ? Str(b, at + ".bot") : "random",
                Runs = Int(Need(row, "runs", at), at + ".runs"),
                Seed = row.TryGetValue("seed", out object s) ? Int(s, at + ".seed") : 42 + n,
            };
            if (!config.Kits.ContainsKey(r.Kit)) throw new FormatException(at + ".kit: no kit named " + r.Kit);
            if (!Bots.Contains(r.Bot)) throw new FormatException(at + ".bot: unknown bot " + r.Bot + " (known: " + string.Join(", ", Bots) + ")");
            if (r.Runs <= 0) throw new FormatException(at + ".runs must be above 0");
            config.Rows.Add(r);
        }
        if (top.TryGetValue("expect", out object e))
        {
            Dictionary<string, object> expect = Obj(e, "expect");
            Only(expect, "expect", "offered");
            if (expect.TryGetValue("offered", out object o)) config.Offered.AddRange(List(o, "expect.offered").Select(x => Str(x, "expect.offered")));
        }
        return config;
    }

    private static object Need(Dictionary<string, object> obj, string key, string at) =>
        obj.TryGetValue(key, out object v) ? v : throw new FormatException(at + ": missing " + key);

    private static void Only(Dictionary<string, object> obj, string at, params string[] keys)
    {
        foreach (string key in obj.Keys.Where(k => !keys.Contains(k))) throw new FormatException(at + ": unknown key " + key);
    }

    private static Dictionary<string, object> Obj(object v, string at) => v as Dictionary<string, object> ?? throw new FormatException(at + " must be an object");
    private static List<object> List(object v, string at) => v as List<object> ?? throw new FormatException(at + " must be a list");
    private static string Str(object v, string at) => v as string ?? throw new FormatException(at + " must be text");
    private static int Int(object v, string at) => v is double d && d == Math.Floor(d) && Math.Abs(d) <= int.MaxValue ? (int)d : throw new FormatException(at + " must be a whole number");
}
```
`Json.Parse` throws `FormatException` on malformed JSON — same exception type, so caller handles one type.

**Rationale:** Strict schema = authors see typos immediately; exit 2 (config error) stays distinct from exit 1 (check failure). No cartesian expansion — one row = one area×kit×bot×runs combination (explicit, matches NER's row list).

**Verification:** V1, V9 (unknown-kit + missing-file cases)

**If this fails:** delete `SimConfig.cs`.

---

### Step 3: Framework-owned kit bridge

**Objective:** One `IBridge` built from a `SimKit`; fresh per run; records night-log problems.
**Confidence:** Medium (behaviour choices may need tuning for NER parity later — not this iteration)
**Depends on:** Step 2
**Do-Projex:** Encouraged

**Files:**
- `tools/EfSim/KitBridge.cs` (new)

**Changes:**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Core;

// The game's side of a run, faked from a kit: carried items, credits, and a ground pile for what the run gives.
internal sealed class KitBridge : IBridge
{
    private readonly Random random;
    private readonly HashSet<string> problems;
    private readonly List<RunItem> items = new List<RunItem>();
    private readonly List<RunItem> ground = new List<RunItem>();

    public KitBridge(SimKit kit, Content content, Random random, HashSet<string> problems)
    {
        this.random = random;
        this.problems = problems;
        Credits = kit.Credits;
        foreach (SimItem s in kit.Items)
        {
            var item = new RunItem { Handle = s.Id, Id = s.Id, Name = s.Id, Count = s.Count };
            item.Tags.UnionWith(s.Tags);
            item.ApplyStats(content);                                        // pack data first…
            foreach (KeyValuePair<string, int> p in s.Props) item.Props[p.Key] = p.Value;  // …kit props win
            items.Add(item);
        }
    }

    public IReadOnlyList<RunItem> Items() => items;
    public void Consume(RunItem item, int count) { item.Count -= count; if (item.Count <= 0) items.Remove(item); }

    public string GiveItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        ground.Add(new RunItem { Handle = itemId + "#" + ground.Count, Id = itemId, Name = itemId });
        return itemId;
    }

    public string RollTable(string table) => table;
    public bool Fits(RunItem gun, RunItem round) => true;
    public bool Equip(RunItem item) { item.Tags.Add("in_hand"); return true; }
    public int Credits { get; private set; }
    public void AddCredits(int amount) => Credits += amount;
    public bool SpendCredits(int amount) { if (Credits < amount) return false; Credits -= amount; return true; }
    public int GroundCount => ground.Count;
    public int ClearGround() { int n = ground.Count; ground.Clear(); return n; }

    public string TakeBack()
    {
        if (ground.Count == 0) return null;
        RunItem newest = ground[ground.Count - 1];
        ground.RemoveAt(ground.Count - 1);
        return newest.Name;
    }

    public void LoseFound() => ground.Clear();

    public List<string> LoseCarried(Func<int, int> chanceForSize)
    {
        var lost = new List<string>();
        foreach (RunItem item in items.ToList())
        {
            if (random.Next(100) >= chanceForSize(Math.Max(1, item.Get("size")))) continue;
            items.Remove(item);
            lost.Add(item.Name);
        }
        return lost;
    }

    public int GameValue(string name) => 0;
    public void Wound(string kind) { }
    public void NightLog(string text, string color) { if (text != null && text.Contains("{")) problems.Add("unfilled night log: " + text); }
    public void PlaySound(string id) { }
    public void PlayItemSound(RunItem item, string fallback) { }
    public bool OpenTrade(TradeRequest request) => false;
    public TradeResult CloseTrade() => new TradeResult();
    public List<string> TakeCart(TradeRequest cart) => new List<string>();
}
```
**Decisions (defaults taken; revisit in parity iteration):** `ApplyStats` then kit props (NER order) | `Equip` → adds `in_hand`, true (NER) | ground pile (NER) | `RollTable` → table id (NER) | `LoseCarried` rolls per item with `size` prop (default 1) — real roll, not a stub | `GameValue` → 0 and trades declined (framework sim behaviour; NER luck/trade semantics deferred) | night-log problems recorded, not thrown (a throw would abort the run mid-way).

**Rationale:** One bridge owned by the framework; packs supply only kit data.

**Verification:** V1, V6 (behaviour exercised end-to-end via `run`)

**If this fails:** delete `KitBridge.cs`.

---

### Step 4: `run` command — matrix, random bot, checks, table

**Objective:** `ef-sim run <folder> [--runs N]` per Overview; exit 0 all checks pass | 1 any check failed | 2 usage/config error.
**Confidence:** Medium (A2/A3)
**Depends on:** Steps 1–3
**Verify-Projex:** Encouraged

**Files:**
- `tools/EfSim/PackRun.cs` (new)

**Changes:**
```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ExpeditionFramework.Core;

// ef-sim run: plays every row of the pack's sim.json and runs the checks every pack gets.
internal static class PackRun
{
    private const string Usage = "usage: ef-sim run <pack folder> [--runs <n>]";
    private const int MaxSteps = 2000;

    public static int Run(string frameworkRoot, string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("--")) { Console.WriteLine(Usage); return 2; }
        string folder = Path.GetFullPath(args[0]);
        int runsOverride = 0;
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--runs" && i + 1 < args.Length && int.TryParse(args[i + 1], out int n) && n > 0) { runsOverride = n; i++; }
            else { Console.WriteLine("unknown argument " + args[i]); Console.WriteLine(Usage); return 2; }
        }
        string id = Lint.ReadId(folder);
        if (id == null) { Console.WriteLine("ERROR no <ID> in manifest.xml"); return 2; }
        SimConfig config;
        try { config = SimConfig.Read(Path.Combine(folder, "sim.json")); }
        catch (FormatException e) { Console.WriteLine("ERROR sim.json: " + e.Message); return 2; }

        Content content = Lint.Load(frameworkRoot, folder, id, new HashSet<string>(), out Dictionary<string, Dictionary<string, string>> text,
            out List<string> errors, out List<string> notes);
        foreach (string note in notes) Console.WriteLine("WARN " + note);
        var failures = new List<string>(errors);          // load problems + text keys missing from en.csv
        if (content.Problems.Count == 0)
        {
            var areas = new List<AreaDef>();
            foreach (SimRow row in config.Rows)
            {
                string areaId = row.Area.Contains(':') ? row.Area : id + ":" + row.Area;
                if (!content.Areas.TryGetValue(areaId, out AreaDef area)) { Console.WriteLine("ERROR sim.json: no area " + areaId); return 2; }
                areas.Add(area);
            }
            var problems = new HashSet<string>();
            var offered = new HashSet<string>();
            Localizer localize = (pack, key, a) =>
            {
                if (!text.TryGetValue(pack, out Dictionary<string, string> rows) || !rows.TryGetValue(key, out string t))
                {
                    problems.Add("missing text at run time: " + pack + ":" + key);
                    return key;
                }
                try { return string.Format(t, a); }
                catch (FormatException) { problems.Add("too few arguments (" + a.Length + ") for " + key + ": " + t); return t; }
            };
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,-18} {1,-12} {2,-8} {3,6} {4,8} {5,12} {6,10}",
                "area", "kit", "bot", "runs", "cleared", "knocked out", "extracted"));
            for (int r = 0; r < config.Rows.Count; r++)
            {
                SimRow row = config.Rows[r];
                Play(content, areas[r], row, config.Kits[row.Kit], runsOverride > 0 ? runsOverride : row.Runs, localize, offered, problems);
            }
            failures.AddRange(problems.OrderBy(p => p, StringComparer.Ordinal));
            failures.AddRange(config.Offered.Where(o => !offered.Contains(o)).Select(o => "never offered: " + o));
        }
        foreach (string failure in failures) Console.WriteLine("FAIL " + failure);
        Console.WriteLine("ef-sim " + id + ": " + (failures.Count == 0 ? "all checks passed" : failures.Count + " check(s) failed"));
        return failures.Count == 0 ? 0 : 1;
    }

    private static void Play(Content content, AreaDef area, SimRow row, SimKit kit, int runs, Localizer localize,
        HashSet<string> offered, HashSet<string> problems)
    {
        var random = new Random(row.Seed);
        string where = row.Area + " " + row.Kit + " " + row.Bot;
        int cleared = 0, knocked = 0, extracted = 0;
        for (int n = 0; n < runs; n++)
        {
            var run = new ExpeditionRun(content, area, new KitBridge(kit, content, random, problems), s => random.Next(1, s + 1), localize, "you");
            try
            {
                run.Begin();
                int steps = 0;
                while (!run.Over)
                {
                    if (run.Trading) run.FinishTrade();
                    if (++steps > MaxSteps) { problems.Add(where + ": a run never ended; scene " + run.Scene + " room " + run.Depth); break; }
                    List<RunOption> options = run.Options();
                    if (options.Count == 0) { problems.Add(where + ": no options in scene " + run.Scene + " at room " + run.Depth); break; }
                    foreach (RunOption option in options)
                    {
                        if (option.Label != null && option.Label.Contains("{")) problems.Add("unfilled label: " + option.Label);
                        offered.Add(option.Interaction.Id);
                    }
                    run.Perform(Wander(options, random));
                    run.Hud();
                }
            }
            catch (Exception e)
            {
                problems.Add(where + ": a run threw " + e.GetType().Name + ": " + e.Message);
            }
            if (run.Outcome == "cleared" || run.Get("cleared") > 0) cleared++;
            if (run.Outcome == "knocked_out") knocked++;
            else if (run.Outcome != null) extracted++;
        }
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,-18} {1,-12} {2,-8} {3,6} {4,7:0.0}% {5,11:0.0}% {6,9:0.0}%",
            row.Area, row.Kit, row.Bot, runs, 100.0 * cleared / runs, 100.0 * knocked / runs, 100.0 * extracted / runs));
    }

    // The built-in "random" bot: any option, but heads home only rarely so runs reach the end.
    private static RunOption Wander(List<RunOption> options, Random random)
    {
        List<RunOption> pool = options.Where(o => o.Interaction.Id != "head_home" || random.Next(100) < 5).ToList();
        if (pool.Count == 0) pool = options;
        return pool[random.Next(pool.Count)];
    }
}
```
Definitions (document in README): **cleared** = outcome `cleared` or run var `cleared` > 0; **knocked out** = outcome `knocked_out`; **extracted** = run ended any other way (includes cleared). A run aborted by a guard counts in none.

Check inventory delivered: load problems (`errors` from `Lint.Load`) | missing text keys (same) | runtime missing text + too-few format args (localizer) | unfilled labels (options) + unfilled night-log text (bridge) | run never ended (>2000 steps) | no options offered | run threw | never offered (`expect.offered`). Load warnings print as `WARN`, never fail (lint `--strict` covers them).

**Rationale:** Problem set dedupes repeated per-run messages; sorted output keeps stdout deterministic. Each row owns its seeded `Random` → rows independent and reproducible. `run.Hud()` per step renders HUD text so runtime-text checks cover it (as NER does).

**Verification:** V6, V7, V8, V9, V10

**If this fails:** If a sample check fails on a real content/engine defect surfaced by the new checks (A3) — e.g. `missing text at run time` for a sample key — fix the sample data (`samples/SampleArea/**`) only when the fix is obvious and local; otherwise stop and report the exact FAIL lines (do not weaken checks, do not drop ids from `expect.offered`). If an expected id is never offered (A2), find which bridge choice in Step 3 hides it and report it; adjust that bridge behaviour only if it contradicts the game's `IBridge` contract doc-comments. Rollback: delete `PackRun.cs`.

---

### Step 5: Sample pack `sim.json`

**Objective:** Sample data mirroring the framework sim's two bridges, 2 × 2500 runs, expectations = framework never-offered list.
**Confidence:** High
**Depends on:** Step 4
**Do-Projex:** Encouraged

**Files:**
- `samples/SampleArea/sim.json` (new)

**Changes:**
```json
{
  "version": 1,
  "kits": {
    "knife": {
      "credits": 50,
      "items": [
        { "id": "kitchen_knife", "count": 2, "tags": ["melee", "in_hand"], "props": { "bonus": 1, "sides": 4 } }
      ]
    },
    "everything": {
      "credits": 50,
      "items": [
        { "id": "kitchen_knife", "count": 2, "tags": ["melee", "in_hand"], "props": { "bonus": 1, "sides": 4 } },
        { "id": "pistol", "count": 2, "tags": ["firearm", "in_hand"], "props": { "capacity": 6, "firepower": 4, "accuracy": 2 } },
        { "id": "rounds", "count": 2, "tags": ["ammo"] },
        { "id": "bandage_item", "count": 2, "tags": ["medicine"], "props": { "heal_count": 1, "heal_sides": 3, "heal_flat": 2, "stops_bleeding": 1 } },
        { "id": "flashlight", "count": 2, "tags": ["bright"] },
        { "id": "flashbang_grenade", "count": 2, "tags": ["bright"] },
        { "id": "c4", "count": 2, "tags": ["explosive"] },
        { "id": "c4_set", "count": 2, "tags": ["explosive"] },
        { "id": "crowbar", "count": 2 },
        { "id": "cup_noodle", "count": 2 }
      ]
    }
  },
  "runs": [
    { "area": "flooded_cellar", "kit": "knife", "bot": "random", "runs": 2500, "seed": 42 },
    { "area": "flooded_cellar", "kit": "everything", "bot": "random", "runs": 2500, "seed": 43 }
  ],
  "expect": {
    "offered": [
      "press_on", "head_home", "shoot", "reload", "strike", "heal", "feed_rat", "blind_foreman", "blast_foreman",
      "interrupt_foreman", "give", "rob", "ignore", "pry", "search_pump", "claim"
    ]
  }
}
```
Items/props copied from `tests/Sim/Program.cs` `FakeBridge` (l.817–831: `Count = 2` each, credits 50).

**Rationale:** Same two loadouts and run total as the framework sim → expectations transfer directly.

**Verification:** V5, V6, V7, V11

**If this fails:** see Step 4 "If this fails"; never trim `offered`.

---

### Step 6: README

**Objective:** Document `ef-sim` and `sim.json` v1 briefly.
**Confidence:** High
**Depends on:** Step 5
**Do-Projex:** Encouraged

**Files:**
- `README.md` (§ Building and testing, ~l.290–312)

**Changes:**
- Replace the lint command block `dotnet run -c Release --project tests/Sim -- lint ../MyPack [--mod gunsmith_expansion] [--strict]` with `dotnet run -c Release --project tools/EfSim -- lint ../MyPack [--mod gunsmith_expansion] [--strict]`; keep the existing explanatory paragraph; add one sentence: `tests/Sim -- lint` runs the same code.
- Append subsection `### ef-sim run` after it, containing:
  - command `dotnet run -c Release --project tools/EfSim -- run ../MyPack [--runs 200]`
  - what it does: reads `<pack>/sim.json`, plays each row with the built-in `random` bot, prints `area kit bot runs cleared knocked out extracted` table (definitions from Step 4), then checks; `--runs` overrides every row's count (small counts can miss rarely-offered options).
  - checks list (Step 4 inventory), exit codes 0 / 1 / 2.
  - `sim.json` v1 example (the schema block from Overview) + rules paragraph (required/optional/defaults, unknown keys rejected, area prefixing, `bot` = `random` only for now, kit item tags/props layered over the pack's `item_stats`).
  - pointer: `samples/SampleArea/sim.json` is a working example.

**Rationale:** C11.

**Verification:** V12

**If this fails:** revert README hunk.

---

## Verification Plan

All commands: Git Bash, one shell session, cwd = work-root. Build once, then `--no-build` so stdout has no build chatter; `tr -d '\r'` strips Windows CRLF so `$` anchors and diffs work.

```bash
dotnet build -c Release tools/EfSim/EfSim.csproj && dotnet build -c Release tests/Sim/Sim.csproj
ef() { dotnet run -c Release --no-build --project tools/EfSim -- "$@" | tr -d '\r'; return ${PIPESTATUS[0]}; }
ts() { dotnet run -c Release --no-build --project tests/Sim -- "$@" | tr -d '\r'; return ${PIPESTATUS[0]}; }
T=$(mktemp -d); cp -r samples/SampleArea "$T/Bad"
sed -i '/^sa_area_title,/d' "$T/Bad/Localization/en.csv"     # broken fixture for lint identity + missing-text check
```

### Deterministic Suite

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V1 | C1 / S1–4 | `dotnet build -c Release --no-incremental tools/EfSim/EfSim.csproj 2>&1 \| grep -Ei 'EfSim[\\/][A-Za-z]+\.cs\([0-9,]+\): warning' \| wc -l` and build exit code | build exit 0; count `0`; `tools/EfSim/bin/Release/net8.0/ef-sim.dll` exists | compile errors, new warnings, wrong assembly name |
| V2 | C2 | `test ! -e tests/Sim/Lint.cs && test -f tools/EfSim/Lint.cs && grep -c 'tools\\EfSim\\Lint.cs' tests/Sim/Sim.csproj` | exit 0, prints `1` | duplicated lint source / stale include |
| V3 | C2 | `for p in samples/SampleArea "$T/Bad" ../../../NightExpeditionsReborn; do a=$(ef lint "$p" --strict; echo "exit=$?"); b=$(ts lint "$p" --strict; echo "exit=$?"); [ "$a" = "$b" ] && echo SAME \|\| echo "DIFF $p"; done` | three `SAME` lines | lint behaviour drift after refactor (ordering, missing-text, exit code) |
| V4 | C3 | `ef lint samples/SampleArea --strict; echo "exit=$?"` | `lint sample_area: 0 error(s), 0 warning(s)` then `exit=0` | sample lint regression |
| V5 | C4 | `fw=$(sed -n '/string\[\] expected =/,/};/p' tests/Sim/Program.cs \| grep -oE '"[a-z_]+"' \| sort \| tr '\n' ,); sj=$(sed -n '/"offered"/,/\]/p' samples/SampleArea/sim.json \| grep -oE '"[a-z_]+"' \| grep -v '"offered"' \| sort \| tr '\n' ,); n=$(echo "$fw" \| tr ',' '\n' \| grep -c .); [ "$fw" = "$sj" ] && [ "$n" = 16 ] && echo MATCH \|\| printf 'DIFF\n%s\n%s\n' "$fw" "$sj"` | `MATCH` | expectation list drift from framework sim |
| V6 | C5 | `ef run samples/SampleArea > "$T/run1.txt"; echo "exit=$?"; grep -cE '^flooded_cellar +(knife\|everything) +random +2500 +[0-9.]+% +[0-9.]+% +[0-9.]+%$' "$T/run1.txt"; tail -1 "$T/run1.txt"` | `exit=0`; `2`; `ef-sim sample_area: all checks passed` | run loop / table / checks broken, A2/A3 failures |
| V7 | C7 | `ef run samples/SampleArea > "$T/run2.txt"; cmp "$T/run1.txt" "$T/run2.txt" && echo IDENTICAL` | `IDENTICAL` | nondeterminism (unseeded random, unordered set output) |
| V8 | C8 | `ef run samples/SampleArea --runs 7 \| grep -cE '^flooded_cellar .* random +7 '` | `2` | `--runs` ignored |
| V9 | C6 | `cp -r samples/SampleArea "$T/Cfg"; sed -i 's/"kit": "knife"/"kit": "nope"/' "$T/Cfg/sim.json"; ef run "$T/Cfg"; echo "exit=$?"; rm "$T/Cfg/sim.json"; ef run "$T/Cfg"; echo "exit=$?"` | first: line `ERROR sim.json: runs[0].kit: no kit named nope` + `exit=2`; second: `ERROR sim.json: no sim.json in the pack folder` + `exit=2` | config errors misreported as pass/fail |
| V10 | C6 | `cp -r samples/SampleArea "$T/Never"; sed -i 's/"claim"/"claim", "no_such_option"/' "$T/Never/sim.json"; ef run "$T/Never" --runs 300 > "$T/never.txt"; echo "exit=$?"; grep -E '^FAIL never offered: no_such_option$' "$T/never.txt"; ef run "$T/Bad" --runs 50 > "$T/bad.txt"; echo "exit=$?"; grep -c '^FAIL missing text' "$T/bad.txt"` | `exit=1`; `FAIL never offered: no_such_option`; `exit=1`; count ≥ `1` | never-offered / missing-text checks not wired |
| V11 | C9 | `dotnet run -c Release --project tests/Sim \| tr -d '\r' \| tail -1; echo "exit=${PIPESTATUS[0]}"` | `All checks passed`, `exit=0` | framework sim regression (Lint include move) |
| V12 | C11 | `grep -c 'tools/EfSim -- run' README.md; grep -c 'tools/EfSim -- lint' README.md; grep -c '"version": 1' README.md; grep -c 'expect' README.md` | each ≥ `1` | docs missing |
| V13 | C10 | `S=$(cygpath -m "$(mktemp -d)"); dotnet build -c Release --no-incremental ExpeditionFramework.csproj "-p:GameManaged=E:/SteamLibrary/steamapps/common/Probably Stolen/Probably Stolen_Data/Managed/" "-p:InstallDir=$S/" 2>&1 \| grep -E '[0-9]+ (Warning\|Error)\(s\)'` | `0 Warning(s)` and `0 Error(s)` (never deploys to live `Mods/`) | tool leaked into game assembly / Core broken |
| V14 | hygiene | after commit: `git status --porcelain` | empty (bin/obj ignored; fixtures in `$T` outside repo) | stray files, uncommitted work |

### Judgment Checks

| Check | Covers | Why not deterministic |
|-------|--------|-----------------------|
| `sim.json` schema + README text is brief and clear for a pack author | C11 | readability |
| Bridge decisions in Step 3 match `IBridge` doc-comments (`src/Core/Model.cs:394-426`) | S3 | semantic reading of contract text |

### Acceptance Criteria Validation
| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| C1 | V1 | builds, 0 EfSim warnings, `ef-sim.dll` |
| C2 | V2, V3 | single source; 3 × SAME |
| C3 | V4 | 0/0, exit 0 |
| C4 | V5 | MATCH |
| C5 | V6 | exit 0, 2 rows, all checks passed |
| C6 | V9, V10 | exits 2/2/1/1 with stated lines |
| C7 | V7 | IDENTICAL |
| C8 | V8 | 2 rows at 7 runs |
| C9 | V11 | All checks passed |
| C10 | V13 | 0 W / 0 E |
| C11 | V12 + judgment | present, readable |

---

## Rollback Plan

1. `git restore --staged --worktree` uncommitted changes, or `git revert <iteration commits>` on the sprint branch.
2. Confirm `tests/Sim/Lint.cs` restored and `dotnet run -c Release --project tests/Sim` green.

---

## Notes

### Split Decision
No split — heuristic borderline (6 steps, ~45 KB with inline code) but steps tightly coupled: Steps 2–4 form one command and Step 5 only proves it; Step 1 is the shared lint base `run` reuses.

### Risks
- A2 bridge differs from old `FakeBridge` (ApplyStats, Equip true, ground pile) → an expected id may stop being offered. Mitigation: Step 4 "If this fails"; 2 × 2500 runs give wide margin; "offered" is weaker than old "offered and picked".
- A3 new runtime-text / HUD checks may surface pre-existing sample defects. Mitigation: fix sample data if local + obvious, else report; never weaken checks.
- `--runs` small → never-offered by chance (expected, documented; same caveat as NER at SIM_RUNS 50).
- V3 on NER reads NER files only; NER lint may itself report errors (missing `--mod`) — fine, only identity is checked.

### Decisions taken by default (for orchestrator)
- Lint **moved** to `tools/EfSim/Lint.cs`; `tests/Sim` source-includes it. Usage line now says `ef-sim lint …` (bad-args path only).
- Exit codes: 0 pass | 1 check failure | 2 usage/config error (mirrors lint).
- Row default seed `42 + index`; no cartesian expansion; `bot` defaults `random`.
- Random bot `head_home` 5% (NER `Wander`), not 3% (framework sim).
- Step guard 2000 (framework sim), not configurable yet.
- Expectation semantics = **offered** (listed by `Options()`), per sprint vision.
- Load warnings never fail `run`.
- Framework root = 5 levels above the exe (dev-tree layout); packaging for authors without the repo is out of scope.

### Open Questions
- [ ] (none)
