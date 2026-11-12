# ef-sim Rule Bots: sim.json `bots` in the expression language

> **Status:** Complete
> **Author:** Claude Opus 5.5 (plan)
> **Source:** Direct request — sprint iteration 2 objective (derive)
> **Sprint:** 2611121010-pack-defined-sims-sprint.md — iteration 2
> **Parent:** 2611121010-pack-defined-sims-sprint.md
> **Related Projex:** 2611121010-pack-defined-sims-sprint.md | 2611121012-ef-sim-skeleton-plan.md | 2611121035-ef-sim-skeleton-patch.md | 2611121025-ef-sim-skeleton-audit.md | 2611121100-ef-sim-rule-bots-patch.md
> Execution: completed via 2611121100-ef-sim-rule-bots-patch.md (code commit 592f847)
> **Worktree:** Yes — sprint worktree `.projexwt/2611121010-pack-defined-sims`, branch `projex/sprint/2611121010-pack-defined-sims`; all commits land there, never on `main`

---

## Summary

`sim.json` gains a `bots` section: named, ordered rule lists. Rule = `when` (framework expression) + `pick` (first offered of listed ids, optionally aimed at weakest active foe) or `first_except`. Bot-side inputs reach rules through `val.NAME` (the `named` resolver `Expr.Compile` already takes). A row's `bot` names `random` (built-in, unchanged) or a pack bot. Strict v1 parsing with located errors. Proven on `samples/SampleArea/sim.json` (new `cautious` row) and by a parse-check of a full NER `careful` bot transcription.

**Scope:** `tools/EfSim/*` (new `RuleBot.cs`; edits to `SimConfig.cs`, `PackRun.cs`, `KitBridge.cs`) | `samples/SampleArea/sim.json` | `README.md` § ef-sim run. No engine (`src/`, `api/`) or `tests/Sim` change.
**Estimated Changes:** 1 new file, 4 edited files; ~200 lines.

**Pre-validated:** the planner built every code block below in a scratch copy of the repo and ran V1–V8 against it; the expected outputs quoted in the suite are the observed ones.

---

## Objective

### Problem / Gap / Need

`ef-sim` has only the built-in `random` bot (`SimConfig.Bots = { "random" }`). NER's balance rows use `careful`/`greedy` (`Plan`, `NightExpeditionsReborn/tests/Sim/Program.cs:227-282`), which read inputs the expression language doesn't reach: scene, offered option ids, foe intent-id suffixes, focus-vs-weakest, bridge gear state, and a coin from the run RNG. NER `sim.json` (iteration 3) can't carry its balance rows without bots.

### Success Criteria

- [ ] C1 `ef-sim` builds with 0 warnings / 0 errors.
- [ ] C2 `sim.json` accepts optional top-level `bots`: `{ NAME: [ rule, … ] }`; rule keys `when` (optional expression), exactly one of `pick` (non-empty id list) | `first_except` (id list), optional `target: "weakest"` (only with `pick`).
- [ ] C3 A row's `bot` is `random` or a defined bot; anything else → `ERROR sim.json: runs[i].bot: unknown bot …`, exit 2.
- [ ] C4 Strict parse, located errors, exit 2: unknown rule key | uncompilable `when` | unknown `val.` name | empty pick | missing/both pick & first_except | bad `target` | `target` without `pick` | bot named `random` | empty rule list.
- [ ] C5 Rules read every NER `Plan` input (mapping table § Plan Input Mapping); the NER `careful` transcription fixture (V7) parses with no error.
- [ ] C6 `samples/SampleArea/sim.json` has a `cautious` rule-bot row; `ef run samples/SampleArea` exits 0, table has the row, every `cautious` rule picks at least once (no `never picked` WARN), output deterministic.
- [ ] C7 Built-in `random` rows unchanged byte-for-byte (RNG stream not disturbed).
- [ ] C8 A run where no rule picks fails with `FAIL <area> <kit> <bot>: no rule picked an option in scene <scene> at room <n>`, exit 1.
- [ ] C9 Framework sim tests pass; lint sample + NER `--strict` 0/0; game assembly builds 0 warnings 0 errors; `src/`, `api/`, `tests/`, `ExpeditionFramework.csproj` untouched.
- [ ] C10 README documents bots (rule shape, semantics, `val.` names, errors, WARN).

### Out of Scope

- Writing NER `sim.json` (iteration 3). The V7 fixture is a parse-check only, lives in a temp dir, never committed.
- Per-run kit variation, NER `FakeBridge` behaviour deltas (luck, `LoseCarried`, trade), NER seed formula, 800-step guard (iterations 3/5).
- Validating `pick` ids against pack interactions at load (the never-picked WARN covers typos instead).
- Any engine change: `val.` is resolved inside ef-sim only.

---

## Context

### Current State

- `tools/EfSim/SimConfig.cs`: strict `sim.json` v1 reader (`Only` rejects unknown keys; helpers `Need/Obj/List/Str/Int` throw `FormatException` with a location prefix). `public static readonly string[] Bots = { "random" };` validates row bots.
- `tools/EfSim/PackRun.cs`: `Run` prints `ERROR sim.json: <message>` + exit 2 on `FormatException`; `Play` loops runs with one `Random(row.Seed)` shared by dice, `KitBridge` and `Wander`; `run.Perform(Wander(options, random))`.
- `tools/EfSim/KitBridge.cs`: `IBridge` over kit items (`List<RunItem> items`), `Credits`.
- `src/Core/Expr.cs`: `Expr.Compile(text, out error, named)`; `val.NAME` → `named(NAME)`; `null` → compile error `unknown path 'val.NAME' (…)`. `NAME` is everything after the first `.` (word chars `[A-Za-z0-9_.#]`), so `val.offered.heal` → `named("offered.heal")`. `&&`/`||` short-circuit left to right; values evaluate lazily per call → a `coin` value only draws RNG when reached.
- `Scope` (internal, `src/Core/ExpeditionRun.cs:80`) is compiled into `ef-sim` (source include) → usable as `new Scope(run, run.Player, run.Focus, null)`. `any_enemy(...)` etc. bind each active foe as `target`.
- Run vars: `run.depth`, `run.rooms`, `run.credits` (= `Bridge.Credits`) are engine built-ins (`ExpeditionRun.Get`); other `run.X` read pack vars (`boss_read`, `boss_fight`).
- `ExpeditionRun.Perform(null)` throws `NullReferenceException` → NER's `Plan` returning `null` would crash its sim; so on every path NER survives, "a pick that finds nothing" never happens there. Fall-through semantics here is therefore equivalent to NER on all non-crashing paths.
- `Interaction.Label` is always non-null (loader: `required: true`, sole construction site `PackLoader.cs:1126`) → NER's `o.Interaction.Label != null` test is always true.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `tools/EfSim/RuleBot.cs` | new | `BotRule` + `RuleBot` (val resolver, `Choose`, weakest-targeted pick) |
| `tools/EfSim/SimConfig.cs` | sim.json reader | `bots` parsing/compiling; row bot validation against `random` + defined bots |
| `tools/EfSim/PackRun.cs` | run loop | dispatch to rule bot; no-pick failure; never-picked WARN |
| `tools/EfSim/KitBridge.cs` | fake bridge | `HasGun` / `Loaded` / `ShotsLeft` (NER `FakeBridge` formulas) |
| `samples/SampleArea/sim.json` | sample data | `bots.cautious` + 3rd row |
| `README.md` | docs | § ef-sim run: bots |

### Dependencies

- **Requires:** iteration 1 (`84cced3`) — present on the sprint branch.
- **Blocks:** iteration 3 (NER `sim.json` with `careful`/`greedy` rows).

### Constraints

- All commits on `projex/sprint/2611121010-pack-defined-sims` in the worktree. Never touch the base checkout or NER (NER is read-only here: only V9 lints it).
- Game csproj compiles only `src\**` → nothing in `tools/` can reach the game assembly.
- Built-in `random` must consume RNG exactly as before (C7).
- Build artifacts (bin/obj) are gitignored; fixtures go in `$T` (mktemp), outside the repo.

### Assumptions

- `Json.Parse` keeps object key order for `bots` (only affects error-message listing order; rule order comes from a JSON list). Observed true in prototype.
- `dotnet` SDK on PATH builds `net8.0` (observed: SDK 11.0.100 builds it).

### Impact Analysis

- **Direct:** `tools/EfSim/{RuleBot,SimConfig,PackRun,KitBridge}.cs`, `samples/SampleArea/sim.json`, `README.md`.
- **Adjacent:** `tests/Sim` compiles `tools/EfSim/Lint.cs` only → unaffected. `Lint.cs` unchanged.
- **Downstream:** iteration 3 authors NER `careful`/`greedy` from § Plan Input Mapping.

---

## Implementation

### Overview

Bot-side values live in ef-sim: `RuleBot.Named(name)` returns a `Value` closing over the bot's current-decision fields (`run`, `bridge`, `options`, `random`), set by `Choose` before evaluating rules. `SimConfig` compiles each `when` at read time with `bot.Named` → compile errors surface before any run. `PackRun.Play` calls `bot.Choose(...)` for rule-bot rows, `Wander` for `random`.

All commands below: Git Bash, cwd = work-root (the sprint worktree).

### Step 0: Capture baseline

**Objective:** record pre-change `random` rows for C7.
**Confidence:** High
**Depends on:** None
**Do-Projex:** Encouraged

```bash
dotnet build -c Release tools/EfSim/EfSim.csproj
T=$(mktemp -d); echo "$T"   # keep this dir for the whole execution
dotnet run -c Release --no-build --project tools/EfSim -- run samples/SampleArea | tr -d '\r' | grep ' random ' > "$T/before.txt"; cat "$T/before.txt"
```

Expected (observed by planner):
```
flooded_cellar     knife        random     2500     2.8%        91.6%       8.4%
flooded_cellar     everything   random     2500    37.8%        54.6%      45.4%
```

**Verification:** two lines, as above. **If this fails:** stop — the branch differs from iteration 1's audited state; report.

---

### Step 1: Add `tools/EfSim/RuleBot.cs`

**Objective:** rule model, `val.` resolver, rule evaluation, targeted pick.
**Confidence:** High
**Depends on:** None
**Do-Projex:** Encouraged

**Files:** `tools/EfSim/RuleBot.cs` (new; `EfSim.csproj` compiles `*.cs` → no csproj edit)

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Api;
using ExpeditionFramework.Core;

// One rule of a sim.json bot: when its condition holds, it picks the first offered option it names. A rule that finds
// nothing to pick falls through to the next rule.
internal sealed class BotRule
{
    public string At;                       // "bots.NAME[i]", for messages
    public Value When;                      // null: always
    public List<string> Pick;               // ids in priority order; null when Except is used
    public bool Weakest;                    // only options aimed at the weakest active foe
    public List<string> Except;             // pick the first offered option whose id is not listed
    public int Picked;                      // how often this rule chose, across every row that used the bot
}

// A bot from sim.json: an ordered list of rules, read in the framework expression language. Besides the usual paths
// (self = the player, target = the focused foe, run.*, any_enemy(...), …) a rule reads the decision at hand through val.NAME.
internal sealed class RuleBot
{
    public readonly string Name;
    public readonly List<BotRule> Rules = new List<BotRule>();

    private ExpeditionRun run;
    private KitBridge bridge;
    private List<RunOption> options;
    private Random random;

    public RuleBot(string name) { Name = name; }

    /// <summary>Resolves val.NAME for this bot's rules, or null for a name it doesn't know (which fails the compile).</summary>
    public Value Named(string name)
    {
        switch (name)
        {
            case "loaded": return s => bridge.Loaded;
            case "shots_left": return s => bridge.ShotsLeft;
            case "has_gun": return s => bridge.HasGun ? 1 : 0;
            case "focus_is_weakest": return s => Weakest() == run.Focus ? 1 : 0;
            case "coin": return s => random.Next(2) == 0 ? 1 : 0;   // draws from the run's RNG only when evaluated
        }
        int dot = name.IndexOf('.');
        if (dot <= 0 || dot == name.Length - 1) return null;
        string head = name.Substring(0, dot), arg = name.Substring(dot + 1);
        switch (head)
        {
            case "scene": return s => run.Scene == arg ? 1 : 0;
            case "offered": return s => options.Count(o => o.Interaction.Id == arg);
            case "intent_ends": return s => run.Foes.Count(e => e.Active && e.Intent != null && e.Intent.Id.EndsWith(arg, StringComparison.Ordinal));
            case "foes_with": return s => run.Foes.Count(e => e.Get(arg) > 0);   // every foe of the fight, defeated or gone included
            default: return null;
        }
    }

    /// <summary>The option the first matching rule picks, or null if none picks anything.</summary>
    public RunOption Choose(ExpeditionRun run, KitBridge bridge, List<RunOption> options, Random random)
    {
        this.run = run;
        this.bridge = bridge;
        this.options = options;
        this.random = random;
        var scope = new Scope(run, run.Player, run.Focus, null);
        foreach (BotRule rule in Rules)
        {
            if (rule.When != null && rule.When(scope) == 0) continue;
            RunOption picked = rule.Except != null
                ? options.FirstOrDefault(o => !rule.Except.Contains(o.Interaction.Id))
                : Find(rule);
            if (picked == null) continue;
            rule.Picked++;
            return picked;
        }
        return null;
    }

    // The first offered option per id, in the rule's id order; for a targeted pick, only options aimed at the weakest foe.
    private RunOption Find(BotRule rule)
    {
        Entity weakest = rule.Weakest ? Weakest() : null;
        if (rule.Weakest && weakest == null) return null;
        foreach (string id in rule.Pick)
        {
            RunOption o = options.FirstOrDefault(x => x.Interaction.Id == id && (!rule.Weakest || x.Target == weakest));
            if (o != null) return o;
        }
        return null;
    }

    // Lowest hp among active foes; ties go to the earlier foe.
    private Entity Weakest() => run.Foes.Where(e => e.Active).OrderBy(e => e.Get("hp")).FirstOrDefault();
}
```

**Rationale:** `Find` mirrors NER's `Find(params ids)` (id priority, first option per id); `Weakest` mirrors `Foes.Where(Active).OrderBy(hp).First()` (stable sort → earlier foe on ties); `coin` mirrors `random.Next(2) == 0` on the shared row RNG, lazily. `foes_with` counts all foes because NER's `weak_flash` test is `run.Foes.Any(...)` without `Active`; active-only is already `count_enemies(target.STAT > 0)`.

**Verification:** compiles with Step 2–4 (V1). **If this fails:** delete the file.

---

### Step 2: `KitBridge` gear state

**Objective:** `HasGun`, `Loaded`, `ShotsLeft` exactly as NER `FakeBridge` (Program.cs:316-318).
**Confidence:** High
**Depends on:** None
**Do-Projex:** Encouraged

**Files:** `tools/EfSim/KitBridge.cs`

```csharp
// Before:
    public IReadOnlyList<RunItem> Items() => items;

// After:
    // What a bot can see of its gear, worked out as NightExpeditionsReborn's sim does.
    public bool HasGun => items.Any(i => i.Tags.Contains("firearm"));
    public int Loaded => items.Where(i => i.Tags.Contains("firearm")).Sum(i => i.Get("loaded"));
    public int ShotsLeft => Loaded + items.Where(i => i.Tags.Contains("ammo")).Sum(i => i.Count * Math.Max(1, i.Get("rounds")) - i.Get("spent"));

    public IReadOnlyList<RunItem> Items() => items;
```

**Verification:** V1. **If this fails:** revert the file.

---

### Step 3: `SimConfig` — parse and compile `bots`

**Objective:** strict `bots` parsing; row bot validation.
**Confidence:** High
**Depends on:** Step 1
**Verify-Projex:** Encouraged

**Files:** `tools/EfSim/SimConfig.cs` — five edits.

(a) Bot registry:
```csharp
// Before:
    public static readonly string[] Bots = { "random" };
    public readonly Dictionary<string, SimKit> Kits = new Dictionary<string, SimKit>();

// After:
    public const string RandomBot = "random";
    public readonly Dictionary<string, SimKit> Kits = new Dictionary<string, SimKit>();
    public readonly Dictionary<string, RuleBot> Bots = new Dictionary<string, RuleBot>();
```

(b) Allowed top-level keys:
```csharp
// Before:
        Only(top, "sim.json", "version", "kits", "runs", "expect");
// After:
        Only(top, "sim.json", "version", "kits", "bots", "runs", "expect");
```

(c) Parse `bots` after kits, before runs (rows reference bots):
```csharp
// Before:
        if (config.Kits.Count == 0) throw new FormatException("kits is empty");
        List<object> rows = List(Need(top, "runs", "sim.json"), "runs");

// After:
        if (config.Kits.Count == 0) throw new FormatException("kits is empty");
        if (top.TryGetValue("bots", out object bots))
        {
            foreach (KeyValuePair<string, object> b in Obj(bots, "bots")) config.Bots[b.Key] = Bot(b.Key, b.Value);
        }
        List<object> rows = List(Need(top, "runs", "sim.json"), "runs");
```

(d) Row bot default + validation:
```csharp
// Before:
                Bot = row.TryGetValue("bot", out object b) ? Str(b, at + ".bot") : "random",
...
            if (!Bots.Contains(r.Bot)) throw new FormatException(at + ".bot: unknown bot " + r.Bot + " (known: " + string.Join(", ", Bots) + ")");

// After:
                Bot = row.TryGetValue("bot", out object b) ? Str(b, at + ".bot") : RandomBot,
...
            if (r.Bot != RandomBot && !config.Bots.ContainsKey(r.Bot))
                throw new FormatException(at + ".bot: unknown bot " + r.Bot + " (known: " + string.Join(", ", new[] { RandomBot }.Concat(config.Bots.Keys)) + ")");
```

(e) New method, inserted directly above `private static object Need(`:
```csharp
    // bots.NAME: a non-empty list of rules { when?, pick, target? } or { when?, first_except }.
    private static RuleBot Bot(string name, object value)
    {
        string at = "bots." + name;
        if (name == RandomBot) throw new FormatException(at + ": random is built in and can't be redefined");
        List<object> rules = List(value, at);
        if (rules.Count == 0) throw new FormatException(at + " has no rules");
        var bot = new RuleBot(name);
        for (int n = 0; n < rules.Count; n++)
        {
            string rat = at + "[" + n + "]";
            Dictionary<string, object> rule = Obj(rules[n], rat);
            Only(rule, rat, "when", "pick", "target", "first_except");
            var parsed = new BotRule { At = rat };
            if (rule.TryGetValue("when", out object w))
            {
                parsed.When = Expr.Compile(Str(w, rat + ".when"), out string error, bot.Named);
                if (error != null) throw new FormatException(rat + ".when: " + error);
            }
            bool pick = rule.TryGetValue("pick", out object p), except = rule.TryGetValue("first_except", out object x);
            if (pick == except) throw new FormatException(rat + ": needs exactly one of pick or first_except");
            if (pick)
            {
                parsed.Pick = List(p, rat + ".pick").Select(id => Str(id, rat + ".pick")).ToList();
                if (parsed.Pick.Count == 0) throw new FormatException(rat + ".pick is empty");
            }
            else parsed.Except = List(x, rat + ".first_except").Select(id => Str(id, rat + ".first_except")).ToList();
            if (rule.TryGetValue("target", out object t))
            {
                if (!pick) throw new FormatException(rat + ".target only goes with pick");
                if (Str(t, rat + ".target") != "weakest") throw new FormatException(rat + ".target must be weakest");
                parsed.Weakest = true;
            }
            bot.Rules.Add(parsed);
        }
        return bot;
    }
```

**Rationale:** compiling at read time puts expression errors in the same `ERROR sim.json: <location>: …` / exit 2 channel as other config errors, before any content loads. `ExpeditionFramework.Core` is already imported (`Expr`).

**Verification:** V1, V5. **If this fails:** `git checkout -- tools/EfSim/SimConfig.cs`.

---

### Step 4: `PackRun` — dispatch, no-pick failure, never-picked WARN

**Objective:** run rule bots; report a decision no rule covers; warn on dead rules.
**Confidence:** High
**Depends on:** Steps 1–3
**Verify-Projex:** Encouraged

**Files:** `tools/EfSim/PackRun.cs` — three edits.

(a) In `Run`, the row loop:
```csharp
// Before:
                Play(content, areas[r], row, config.Kits[row.Kit], runsOverride > 0 ? runsOverride : row.Runs, localize, offered, problems);
            }

// After:
                config.Bots.TryGetValue(row.Bot, out RuleBot bot);
                Play(content, areas[r], row, config.Kits[row.Kit], bot, runsOverride > 0 ? runsOverride : row.Runs, localize, offered, problems);
            }
            // A rule that never chose anything is usually a typo in its ids or a condition that can't hold; warn, don't fail.
            foreach (RuleBot used in config.Rows.Select(row => row.Bot).Distinct().Where(config.Bots.ContainsKey).Select(b => config.Bots[b]))
            {
                foreach (BotRule rule in used.Rules.Where(rule => rule.Picked == 0)) Console.WriteLine("WARN sim.json: " + rule.At + " never picked an option");
            }
```

(b) `Play` signature + keep the bridge:
```csharp
// Before:
    private static void Play(Content content, AreaDef area, SimRow row, SimKit kit, int runs, Localizer localize,
        HashSet<string> offered, HashSet<string> problems)
...
            var run = new ExpeditionRun(content, area, new KitBridge(kit, content, random, problems), s => random.Next(1, s + 1), localize, "you");

// After:
    private static void Play(Content content, AreaDef area, SimRow row, SimKit kit, RuleBot bot, int runs, Localizer localize,
        HashSet<string> offered, HashSet<string> problems)
...
            var bridge = new KitBridge(kit, content, random, problems);
            var run = new ExpeditionRun(content, area, bridge, s => random.Next(1, s + 1), localize, "you");
```

(c) Decision:
```csharp
// Before:
                    run.Perform(Wander(options, random));

// After:
                    RunOption choice = bot == null ? Wander(options, random) : bot.Choose(run, bridge, options, random);
                    if (choice == null) { problems.Add(where + ": no rule picked an option in scene " + run.Scene + " at room " + run.Depth); break; }
                    run.Perform(choice);
```

Leave `Wander`, the table format (`{2,-8}` bot column) and everything else unchanged — C7 needs `random` rows byte-identical.

**Verification:** V2, V3, V4, V6. **If this fails:** `git checkout -- tools/EfSim/PackRun.cs`.

---

### Step 5: Sample `cautious` bot

**Objective:** prove rule bots on the sample pack; every rule fires; every `val.` kind used.
**Confidence:** High (observed in prototype: exit 0, all 11 rules picked at 2500 and at 50 runs)
**Depends on:** Steps 1–4
**Do-Projex:** Encouraged

**Files:** `samples/SampleArea/sim.json` — replace whole file with:

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
  "bots": {
    "cautious": [
      { "when": "player.hp * 100 <= player.max_hp * 45 || (player.bleeding > 0 && player.hp * 10 <= player.max_hp * 7)", "pick": ["heal"] },
      { "when": "val.scene.combat && val.has_gun && val.loaded == 0 && val.shots_left > 0", "pick": ["reload"] },
      { "when": "val.scene.combat && val.intent_ends.wind_up > 0", "pick": ["interrupt_foreman"] },
      { "when": "val.scene.combat && val.foes_with.blinded_once == 0 && val.coin", "pick": ["blind_foreman"] },
      { "when": "val.scene.combat && !val.focus_is_weakest", "pick": ["shoot", "strike", "punch"], "target": "weakest" },
      { "when": "val.scene.combat", "pick": ["shoot", "strike", "punch"] },
      { "when": "val.scene.event && player.hp * 10 > player.max_hp * 6", "pick": ["rob", "pry"] },
      { "when": "val.scene.event", "first_except": ["rob", "pry"] },
      { "when": "val.offered.search_pump", "pick": ["search_pump"] },
      { "when": "!val.offered.press_on || player.hp * 100 <= player.max_hp * 40 || (run.depth + 1 == run.rooms && val.has_gun && val.shots_left < 4) || run.credits >= 200", "pick": ["claim", "head_home"] },
      { "pick": ["press_on"] }
    ]
  },
  "runs": [
    { "area": "flooded_cellar", "kit": "knife", "bot": "random", "runs": 2500, "seed": 42 },
    { "area": "flooded_cellar", "kit": "everything", "bot": "random", "runs": 2500, "seed": 43 },
    { "area": "flooded_cellar", "kit": "everything", "bot": "cautious", "runs": 2500, "seed": 44 }
  ],
  "expect": {
    "offered": [
      "press_on", "head_home", "shoot", "reload", "strike", "heal", "feed_rat", "blind_foreman", "blast_foreman",
      "interrupt_foreman", "give", "rob", "ignore", "pry", "search_pump", "claim"
    ]
  }
}
```

(Kits, the two `random` rows and `expect` are unchanged from the current file; only `bots` and the third row are new.)

**Rationale:** sample foes give multi-foe fights (2 rats; foreman + rat) → targeted pick fires; foreman's `expedition_framework:wind_up` intent → `intent_ends`; `blinded_once` foe stat → `foes_with`; coin gates the blind rule.

**Verification:** V2, V3, V4. **If this fails:** restore with `git checkout -- samples/SampleArea/sim.json`; if only the `never picked` count is non-zero, the implementation diverged from Steps 1–4 — diff against them before touching the data.

---

### Step 6: README

**Objective:** document bots.
**Confidence:** High
**Depends on:** Steps 1–5
**Do-Projex:** Encouraged

**Files:** `README.md` (§ `### ef-sim run`)

(a) Line starting `Reads \`<pack>/sim.json\``: replace `plays each row of its run matrix with the built-in \`random\` bot against` with `plays each row of its run matrix with the row's bot against`.

(b) Checks paragraph: replace `a run that throws, and every` with `a run that throws, a decision no rule of the row's bot picks an option for, and every`.

(c) In the defaults paragraph, delete the sentence `` `random` is the only bot for now. ``

(d) Insert directly after the paragraph ending `` `samples/SampleArea/sim.json` is a working example. ``:

````markdown
#### Bots

A row's `bot` is `random` (built in: a uniformly random offered option, with `head_home` left out 95% of the time) or a
name from `bots`. A bot is an ordered list of rules. Each decision takes the first rule whose `when` holds and that finds an
offered option; a rule that finds nothing falls through to the next. If no rule picks anything the run stops with a `FAIL`.

```json
"bots": {
  "cautious": [
    { "when": "player.hp * 100 <= player.max_hp * 45", "pick": ["heal"] },
    { "when": "val.scene.combat && !val.focus_is_weakest", "pick": ["shoot", "strike"], "target": "weakest" },
    { "when": "val.scene.event", "first_except": ["rob"] },
    { "pick": ["press_on"] }
  ]
}
```

A rule has an optional `when` (an expression; no `when` always holds) and exactly one of `pick` (option ids in priority
order: the first id that has an offered option wins, with its first such option) or `first_except` (the first offered option
whose id is not listed). `"target": "weakest"` limits `pick` to options aimed at the active foe with the lowest hp (the earlier
foe on ties).

In `when`, `self` and `player` are the player, `target` is the focused foe, and every other path and function works as in pack
data (`run.depth`, `run.rooms`, `run.credits`, `run.<var>`, `any_enemy(target.hp < 3)`, …). Bots also read:

| `val.` name | Value |
|-------------|-------|
| `scene.NAME` | 1 while the scene is `NAME` (`room`, `combat`, `event`, or an event's own scene name) |
| `offered.ID` | how many offered options have id `ID` |
| `intent_ends.SUFFIX` | active foes whose intent id ends with `SUFFIX` |
| `foes_with.STAT` | foes in the fight, defeated or gone included, with `STAT` above 0 |
| `focus_is_weakest` | 1 if the focused foe is the weakest active foe (or there is neither) |
| `loaded` | rounds loaded in carried `firearm` items |
| `shots_left` | `loaded` plus the rounds in carried `ammo` items |
| `has_gun` | 1 if carrying a `firearm` item |
| `coin` | 1 or 0 at even odds, drawn from the row's random stream each time it is evaluated; `&&` and `\|\|` stop early, so put it last |

Errors (exit 2), each naming the place, e.g. `bots.cautious[2].when: …`: unknown keys, a `when` that doesn't compile or uses an
unknown `val.` name, a rule with neither or both of `pick` and `first_except`, an empty `pick`, a `target` other than
`weakest` or without `pick`, a bot named `random`, a bot with no rules, and a row naming an unknown bot. After the table, each
rule of a bot that some row used and that never picked an option prints a `WARN` (often a mistyped id or a condition that never
holds).
````

**Verification:** V11. **If this fails:** `git checkout -- README.md`.

---

### Step 7: Commit

**Objective:** one code commit on the sprint branch.
**Depends on:** Steps 1–6 and V1–V11 passing.

```bash
git add tools/EfSim/RuleBot.cs tools/EfSim/SimConfig.cs tools/EfSim/PackRun.cs tools/EfSim/KitBridge.cs samples/SampleArea/sim.json README.md
git commit -m "feat(ef-sim): rule-based bots in sim.json" -m "Sprint 2611121010-pack-defined-sims#2, plan 2611121039-ef-sim-rule-bots-plan.md." -m "Co-Authored-By: <executing model> <noreply@anthropic.com>"
rm -rf "$T"
```

**Verification:** V12, V13.

---

## Plan Input Mapping

Every input NER `Plan` (`NightExpeditionsReborn/tests/Sim/Program.cs:227-282`) reads → how a rule reads it. Iteration 3 authors `careful`/`greedy` from this table; no engine or ef-sim change needed (V7 proves the full `careful` transcription parses).

| `Plan` input (line) | Rule form |
|---|---|
| `run.Player.Get("hp")` / `"max_hp"` (230) | `player.hp` / `player.max_hp` (`self.` same) |
| `run.Player.Get("bleeding") > 0` (231) | `player.bleeding > 0` |
| `Find("heal")`, `Find("reload")` non-null (232) | `val.offered.heal`, `val.offered.reload` |
| `gear.Loaded == 0` → `empty` (233) | `val.loaded == 0` |
| `run.Scene == "combat"` / `"standoff"` / `"event"` (235/253/263) | `val.scene.combat` / `val.scene.standoff` / `val.scene.event`; the trailing room logic (271-281) is reached only outside those scenes in NER, and only on NER-crash paths here (see fall-through) |
| `dry = Find("shoot") == null && reload == null` (237) | `!val.offered.shoot && !val.offered.reload` |
| `return Find(a, b, …)` | `"pick": ["a", "b", …]` (id priority, first option per id) |
| `Find("read_boss","weak_point")` (239) | `"pick": ["read_boss", "weak_point"]` |
| `run.Get("boss_read") > 0` (241) | `run.boss_read > 0` |
| `run.Foes.Any(e => e.Get("weak_flash") > 0)` (241, all foes) | `val.foes_with.weak_flash > 0` |
| active foe intent `EndsWith("heavy")` / `EndsWith("aimed_shot")` (242) | `val.intent_ends.heavy + val.intent_ends.aimed_shot > 0` |
| `run.Player.Get("in_cover") == 0` (244) | `player.in_cover == 0` |
| `random.Next(2) == 0` (244, shared run RNG, drawn only after `threatened && in_cover == 0`) | `val.coin`, placed last in the `&&` chain → same draw count and order |
| `weakest = Foes.Where(Active).OrderBy(hp).First()`; `weakest != run.Focus` (245-246) | `!val.focus_is_weakest` |
| `switch_target` option with `o.Target == weakest` (248) | `"pick": ["switch_target"], "target": "weakest"` |
| `weak = hp*2 <= max \|\| gear.ShotsLeft <= 3` (256) | `(player.hp * 2 <= player.max_hp \|\| val.shots_left <= 3)` |
| `gear.Credits >= 200` (259) | `run.credits >= 200` (engine reads `Bridge.Credits`) |
| `fit = hp*10 > max*6` (265) | `player.hp * 10 > player.max_hp * 6` |
| `wait` option with `Label != null` (266) | `"pick": ["wait"]` — `Label` is never null (loader requires it) |
| `run.Get("boss_fight") == 0`, `run.Depth == run.Get("rooms")` (266) | `run.boss_fight == 0 && run.depth == run.rooms` |
| `options.First(o => o.Interaction.Id != "counter")` (269) | `"first_except": ["counter"]` |
| `finished = Find("press_on") == null` (273) | `!val.offered.press_on` |
| `gear.ShotsLeft`, `gear.HasGun` (276-278) | `val.shots_left`, `val.has_gun` |
| `bossNext = run.Depth + 1 == run.Get("rooms")` (279) | `run.depth + 1 == run.rooms` |
| `greedy` flag | two bot defs; `greedy` differs from the V7 `careful` fixture in exactly: flee rule `when` → `val.scene.combat && player.hp <= 5`; head-home rule thresholds `55`→`30`, `70`→`40`, `val.shots_left <= 2`→`val.shots_left <= 0`, and the `run.depth + 1 == run.rooms && (…)` clause dropped |
| `Find` returns null → `Perform(null)` | rule falls through. NER's `Perform(null)` throws, so on every path NER's sim survives, the outcome is identical |

Arithmetic is C#/`Expr` int math in both → identical thresholds.

---

## Verification Plan

### Setup (Git Bash, cwd = work-root; after Step 6, reuse `$T` from Step 0)

```bash
dotnet build -c Release tools/EfSim/EfSim.csproj -warnaserror
ef() { dotnet run -c Release --no-build --project tools/EfSim -- "$@" | tr -d '\r'; return ${PIPESTATUS[0]}; }
bad() { rm -rf "$T/B"; cp -r samples/SampleArea "$T/B"; sed -i "$1" "$T/B/sim.json"; ef run "$T/B" --runs 5 | grep -E '^ERROR|all checks|failed'; echo "exit=${PIPESTATUS[0]}"; }
mkdir -p "$T/Ner"; cp -r samples/SampleArea/. "$T/Ner/"
cat > "$T/Ner/sim.json" <<'EOF'
{
  "version": 1,
  "kits": { "knife": { "items": [ { "id": "kitchen_knife", "tags": ["melee", "in_hand"] } ] } },
  "bots": {
    "careful": [
      { "when": "player.hp * 100 <= player.max_hp * 45 || (player.bleeding > 0 && player.hp * 10 <= player.max_hp * 7)", "pick": ["heal"] },
      { "when": "val.scene.combat && (player.hp <= 5 || (!val.offered.shoot && !val.offered.reload && player.hp * 2 <= player.max_hp))", "pick": ["smoke_run", "flee"] },
      { "when": "val.scene.combat", "pick": ["read_boss", "weak_point"] },
      { "when": "val.scene.combat && run.boss_read > 0 && val.foes_with.weak_flash > 0", "pick": ["flashbang"] },
      { "when": "val.scene.combat && val.intent_ends.heavy + val.intent_ends.aimed_shot > 0", "pick": ["flashbang"] },
      { "when": "val.scene.combat && val.intent_ends.heavy + val.intent_ends.aimed_shot > 0 && player.in_cover == 0 && val.coin", "pick": ["cover"] },
      { "when": "val.scene.combat && !val.focus_is_weakest", "pick": ["switch_target"], "target": "weakest" },
      { "when": "val.scene.combat", "pick": ["shoot", "reload", "melee", "gun_butt", "flee"] },
      { "when": "val.scene.standoff && val.loaded == 0", "pick": ["reload"] },
      { "when": "val.scene.standoff", "pick": ["feed", "smokes"] },
      { "when": "val.scene.standoff && (player.hp * 2 <= player.max_hp || val.shots_left <= 3) && run.credits >= 200", "pick": ["bribe"] },
      { "when": "val.scene.standoff && (player.hp * 2 <= player.max_hp || val.shots_left <= 3)", "pick": ["smoke_slip", "sneak"] },
      { "when": "val.scene.standoff", "pick": ["ambush"] },
      { "when": "val.scene.event", "pick": ["spare", "search"] },
      { "when": "val.scene.event && player.hp * 10 > player.max_hp * 6", "pick": ["take"] },
      { "when": "val.scene.event && player.hp * 10 > player.max_hp * 6 && run.boss_fight == 0 && run.depth == run.rooms", "pick": ["wait"] },
      { "when": "val.scene.event", "pick": ["card", "scan", "light", "cut", "pry", "food", "wire", "clean", "test", "smokes", "stim", "pull10", "ticket", "valve", "pick", "wait", "feel"] },
      { "when": "val.scene.event && player.hp * 10 > player.max_hp * 6", "pick": ["rob", "dive", "shove", "wade", "dash"] },
      { "when": "val.scene.event", "pick": ["wait", "move_on", "back"] },
      { "when": "val.scene.event", "first_except": ["counter"] },
      { "when": "val.loaded == 0", "pick": ["reload"] },
      { "pick": ["search"] },
      { "when": "!val.offered.press_on || player.hp * 100 <= player.max_hp * 55 || (!val.offered.heal && player.hp * 100 < player.max_hp * 70) || (val.has_gun && val.shots_left <= 2) || (run.depth + 1 == run.rooms && ((val.has_gun && val.shots_left < 8) || player.hp * 10 < player.max_hp * 7))", "pick": ["head_home"] },
      { "pick": ["press_on"] }
    ]
  },
  "runs": [ { "area": "flooded_cellar", "kit": "knife", "bot": "random", "runs": 5 } ]
}
EOF
```

The `careful` fixture is the full NER `Plan(greedy: false)` transcription (iteration 3 starting point); it is defined but unused by the row, so V7 checks that it parses, not how it plays.

### Deterministic Suite

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V1 | C1 / S1–4 | `dotnet build -c Release --no-incremental tools/EfSim/EfSim.csproj -warnaserror; echo "exit=$?"` | `exit=0` | compile errors, new warnings |
| V2 | C6 / S4–5 | `ef run samples/SampleArea > "$T/run1.txt"; echo "exit=$?"; grep -cE '^flooded_cellar +everything +cautious +2500 +[0-9.]+% +[0-9.]+% +[0-9.]+%$' "$T/run1.txt"; grep -c 'never picked' "$T/run1.txt"; tail -1 "$T/run1.txt"` | `exit=0`; `1`; `0`; `ef-sim sample_area: all checks passed` | rule bot not wired, dead rules (val/pick broken), new failures |
| V2b | C6 | `grep ' cautious ' "$T/run1.txt"` | exactly `flooded_cellar     everything   cautious   2500     1.8%        54.5%      45.5%` (planner's prototype) | implementation diverged from plan code (rule order, coin draw, weakest tie-break) |
| V3 | C6 | `ef run samples/SampleArea > "$T/run2.txt"; cmp "$T/run1.txt" "$T/run2.txt" && echo IDENTICAL` | `IDENTICAL` | nondeterminism |
| V4 | C7 | `grep ' random ' "$T/run1.txt" \| cmp - "$T/before.txt" && echo SAME` | `SAME` | `random` RNG stream disturbed |
| V5 | C3 / C4 | run each line: (1) `bad 's/val.intent_ends.wind_up > 0/(player.hp >/'` (2) `bad 's/val.intent_ends.wind_up/val.nope/'` (3) `bad 's/"pick": \["press_on"\] }/"pick": ["press_on"], "chance": 5 }/'` (4) `bad 's/"bot": "cautious"/"bot": "cautios"/'` (5) `bad 's/"cautious": \[/"random": [/'` (6) `bad 's/"target": "weakest"/"target": "strongest"/'` (7) `bad 's/{ "pick": \["press_on"\] }/{ "when": "true" }/'` (8) `bad 's/"target": "weakest"/"target": "weakest", "first_except": []/'` | each prints its line then `exit=2`: (1) `ERROR sim.json: bots.cautious[2].when: expected a number or path` (2) `ERROR sim.json: bots.cautious[2].when: unknown path 'val.nope' (paths start with self., target., player., item., with., run., role., arg., game. or val.)` (3) `ERROR sim.json: bots.cautious[10]: unknown key chance` (4) `ERROR sim.json: runs[2].bot: unknown bot cautios (known: random, cautious)` (5) `ERROR sim.json: bots.random: random is built in and can't be redefined` (6) `ERROR sim.json: bots.cautious[4].target must be weakest` (7) `ERROR sim.json: bots.cautious[10]: needs exactly one of pick or first_except` (8) `ERROR sim.json: bots.cautious[4]: needs exactly one of pick or first_except` | lax parsing, unlocated errors, wrong exit code |
| V6 | C8 | `rm -rf "$T/B"; cp -r samples/SampleArea "$T/B"; sed -i 's/"cautious": \[/"only_press": [ { "pick": ["press_on"] } ], "cautious": [/;s/"bot": "cautious"/"bot": "only_press"/' "$T/B/sim.json"; ef run "$T/B" --runs 50 \| grep -E '^FAIL\|^WARN\|^ef-sim'; echo "exit=${PIPESTATUS[0]}"` | `WARN sim.json: bots.only_press[0] never picked an option`; `FAIL flooded_cellar everything only_press: no rule picked an option in scene combat at room 1`; `FAIL flooded_cellar everything only_press: no rule picked an option in scene event at room 1`; `ef-sim sample_area: 2 check(s) failed`; `exit=1` | null pick crashing / passing silently |
| V7 | C5 | `ef run "$T/Ner" > "$T/ner.txt"; echo "exit=$?"; grep -c '^ERROR' "$T/ner.txt"; tail -1 "$T/ner.txt"` | `exit=0`; `0`; `ef-sim sample_area: all checks passed` | a mapped `Plan` input not expressible |
| V8 | C9 | `dotnet run -c Release --project tests/Sim \| tr -d '\r' \| tail -1; echo "exit=${PIPESTATUS[0]}"` | `All checks passed`; `exit=0` | framework sim regression |
| V9 | C9 | `ef lint samples/SampleArea --strict \| tail -1; echo "exit=$?"; ef lint ../../../NightExpeditionsReborn --strict \| tail -1; echo "exit=$?"` | `lint sample_area: 0 error(s), 0 warning(s)`, `exit=0`; `lint night_expeditions_reborn: 0 error(s), 0 warning(s)`, `exit=0` | lint regression |
| V10 | C9 | `S=$(cygpath -m "$(mktemp -d)"); DOTNET_CLI_UI_LANGUAGE=en dotnet build -c Release --no-incremental ExpeditionFramework.csproj "-p:GameManaged=E:/SteamLibrary/steamapps/common/Probably Stolen/Probably Stolen_Data/Managed/" "-p:InstallDir=$S/" 2>&1 \| grep -E '[0-9]+ (Warning\|Error)\(s\)'` | `0 Warning(s)` and `0 Error(s)` (never deploys to live `Mods/`) | tool leaking into game assembly |
| V11 | C10 | `for k in '#### Bots' first_except focus_is_weakest intent_ends foes_with shots_left 'no rule of the row' 'never picked'; do printf '%s ' "$(grep -cF "$k" README.md)"; done; echo; grep -cF 'is the only bot' README.md` | eight numbers each ≥ `1`; then `0` | docs missing / stale |
| V12 | hygiene | after commit: `git status --porcelain` | empty | stray files |
| V13 | C9 scope | `P=$(git log --diff-filter=A --format=%H -- .projex/2611121039-ef-sim-rule-bots-plan.md); git diff --name-only "$P"..HEAD -- src api tests ExpeditionFramework.csproj ExpeditionFramework Localization \| wc -l` | `0` | engine/test/game files touched |

Table commands: `\|` is a markdown escape — type a plain `|`. V11 note: `'no rule of the row'` matches step 6(b) text `a decision no rule of the row's bot picks an option for`.

### Judgment Checks

| Check | Covers | Why not deterministic |
|-------|--------|-----------------------|
| Mapping table rows each correspond to a `Plan` read at the cited line, and the V7 fixture follows `Plan`'s branch order | C5 | semantic equivalence of C# control flow vs rule list; V7 only proves it parses |

### Acceptance Criteria Validation

| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| C1 | V1 | exit 0 |
| C2 | V2, V5, V7 | sample + fixture parse; bad shapes rejected |
| C3 | V5(4) | unknown bot error, exit 2 |
| C4 | V5 | 8 located errors, exit 2 |
| C5 | V7 + judgment | fixture parses; mapping reviewed |
| C6 | V2, V2b, V3 | row present, 0 never-picked, deterministic |
| C7 | V4 | `SAME` |
| C8 | V6 | FAIL lines, exit 1 |
| C9 | V8, V9, V10, V13 | green; scope clean |
| C10 | V11 | docs present |

---

## Rollback Plan

1. Before commit: `git checkout -- tools/EfSim samples/SampleArea/sim.json README.md && rm -f tools/EfSim/RuleBot.cs`.
2. After commit: `git revert <code-commit>` on the sprint branch (never `--amend`, never on `main`).

---

## Notes

### Risks

- V2b exact line depends on exact code: any deviation in rule evaluation order, `coin`'s draw, or tie-breaking changes it. Intended — it pins the plan's semantics. If only V2b fails, compare code to Steps 1–4; do not edit the expected line.
- `foes_with` counts defeated/gone foes (NER parity). An author wanting active-only uses `count_enemies(target.STAT > 0)`; README states the difference.
- A pick id typo is not a load error; it surfaces as the `never picked` WARN. A rarely-firing legit rule may also WARN at small `--runs`; WARN never fails.
- Bot column stays `{2,-8}`: names longer than 8 chars shift that row's columns (cosmetic; no check parses columns after the bot name except V2/V2b regex, which uses `cautious`, 8 chars).

### Decisions Taken by Default

- Fall-through on empty pick (NER returns null → would crash) — equivalent on all NER-surviving paths; enables NER's `??` chains (event branch) as plain rule lists.
- `val.` namespacing with dotted parameters (`scene.X`, `offered.X`, `intent_ends.X`, `foes_with.X`) instead of new `Expr` functions — keeps engine untouched.
- Credits read via existing `run.credits`; depth/rooms via `run.depth`/`run.rooms` — no duplicate `val.` names.
- No-pick → per-row `FAIL` + run stops (like the no-options guard), not a fallback to `random`.
- Never-picked → `WARN`, not `FAIL`.
- Rule scope: `self` = player, `target` = focused foe.

### Open Questions

- [ ] (none)

### Split Decision

`No split — single scope, within size budget` (one projex scope, one repo, tightly coupled steps; >5 steps but well under 500 lines of change).
