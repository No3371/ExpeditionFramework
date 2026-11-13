# ef-sim Reach + Item-Id Guard

> **Status:** Ready
> **Author:** Claude Opus 5.5 (plan step)
> **Parent:** 2611140152-ef-ner-next-steps-proposal.md
> **Source:** 2611140152-ef-ner-next-steps-proposal.md (NER `.projex/`; Tracks A1–A4, B1, F-EF). Order "verify first" and "bad id → give nothing + report" confirmed by the human 2026-11-14.
> **Related Projex:** 2611140220-ner-rep-week-intel-sim-rows-plan.md (NER `.projex/`, Requires this plan merged) | 2611140132-ef-ner-capability-boundaries-explore.md (NER) | 2611121201-ef-sim-bridge-knobs-audit.md | 2611121626-pack-memory-sim-memory-audit.md
> **Worktree:** Yes

---

## Summary

Gives `ef-sim run` the conditions NER's newest features run under, and stops a misspelled game item id from handing the player a blank "unknown" item.

- **A1** per-row `game` overlay on `bridge.game`.
- **A2** per-row `"days": true`: `game.day` +1 per night, reputation carries night to night.
- **A3** per-row `"intel": true`: each night's intel rolled with Core `IntelRoller` and passed to the run.
- **A4** per-row `expect`: end-of-row `rep.F` / `rep_exact.F`, and `intel.SLOT` (nights the slot named anything).
- **B1** `src/Game`: unknown item id → spawn nothing, log a warning once per id (the run log already says "could not give").
- **F (EF part)** docs: `testing.md`, `intel.md`, `game.md`, `items.md`, `getting-started.md`.

Every new key is opt-in; rows without them run exactly as today (no extra draws from the row's random).

**Scope:** EF `tools/EfSim/*`, `src/Game/{ItemIds.cs (new), GameBridge.cs, TradeCounter.cs, DevTools.cs}`, `docs/*`, `docs/examples/Showcase/sim.json` (one appended row).
**Estimated Changes:** 11 files (1 new), ~6 functions + 1 class.

---

## Objective

### Problem / Gap / Need

- Rows accept only `area|kit|bot|runs|seed|memory`; `bridge.game` is file-wide → one rep/day for every row.
- `PackRun.Play` builds a fresh `KitBridge` each night → reputation resets; `game.day` never moves → NER's week rollover never runs in a committed test.
- `PackRun.Play` passes no `intel` to `ExpeditionRun` → `intel()` reads 0; intel `when` gates never evaluated.
- No way to assert end reputation (requested in NER reputation walkthrough).
- `GameBridge.GiveItem` → `ItemSpawner.Spawn(id)` → for an unknown id the game returns `ItemDirectory.CreateEmptyItem` (blank "unknown" item); EF's `item == null` guard never fires. Same in `TradeCounter.EnsureCart` and `DevTools.GiveKit` (`ModHelper.SpawnItem` = `ItemSpawner.Spawn`).
- `docs/items.md` says a bad id "gives or matches nothing" — wrong for giving today.

### Success Criteria

- [ ] SC1 NER `ef-sim run --runs 4000` stdout byte-identical before/after (NER `sim.json` untouched by this plan).
- [ ] SC2 Showcase `ef-sim run --runs 4000`: the 2 existing table lines identical before/after; 1 new line appended; "all checks passed".
- [ ] SC3 Lint `--strict` 0 problems / 0 warnings for NER, `docs/examples/Starter`, `docs/examples/Showcase`.
- [ ] SC4 `tests/Sim` exits 0.
- [ ] SC5 New keys parse strictly: wrong types / unknown faction / `intel.X` without `"intel": true` → exit 2 with a message naming the key; unknown intel slot → exit 2.
- [ ] SC6 `days` carries reputation and advances the day: scratch NER row (week rollover) passes `rep_week >= 1`; Showcase negative control without `days` fails its rep expectation.
- [ ] SC7 `intel` rolls: Showcase new row passes `intel.fence >= 1`.
- [ ] SC8 B1: the only `ItemSpawner.Spawn` call in `src/Game` is inside `ItemIds.cs`; `ModHelper.SpawnItem` gone from `DevTools.cs`; EF main project compiles with install redirected (0 errors).
- [ ] SC9 Docs describe new keys and the new bad-id behaviour; no doc still says ef-sim has no intel or that a bad id passes silently in game.

### Out of Scope

- B2 (load-time id check), B3 (lint catalog), C, D, E tracks.
- Pinned intel picks per row; `game.clears` accumulating across `days` nights.
- NER `sim.json` / README (→ 2611140220-ner-rep-week-intel-sim-rows-plan.md).
- In-game play test of B1 (human, after install; listed in Notes).

---

## Context

### Current State

- `tools/EfSim/SimConfig.cs`: `SimRow { Area, Bot, Runs, Seed, Kits, Memory }`; row keys checked by `Only(row, at, "area","kit","bot","runs","seed","memory")`. `SimBridge.Game` (`Dictionary<string,int>`) + `CoinGame` (`HashSet<string>`) parsed in `ReadBridge`. Memory expect range parse inline in `MemoryOf` (`whole number` or `{min,max}`).
- `tools/EfSim/KitBridge.cs`: private `reputation` dict (field initializer), `Rep(f)` = changed ?? `rep_exact.f`/100 ?? `rep.f` ?? 0; `GameValue(name)`: rep reads via `GameReads.RepFaction`, else coin → `random.Next(2)`, else `settings.Game[name]` ?? 0.
- `tools/EfSim/PackRun.cs`: `Run` loops rows → `Play(content, id, area, row, kits, bot, config.Bridge, runs, localize, offered, problems)` returns last `IMemory`; `memory` failures list collects `row.Memory.Check`. `Play` per night: `new KitBridge(kit, settings, content, random, problems, memory)` and `new ExpeditionRun(content, area, bridge, roll, localize, "you")` (no intel).
- `src/Core/ExpeditionRun.cs`: ctor last param `IReadOnlyDictionary<string, List<string>> intel = null`; `IntelRoller.Roll(AreaDef, Func<int,int> roll, IScope scope)` → slot id → pool positions; `MapContext(areaId, localize, Func<string,int> game, IMemory memory)` with `.Root`. Game side (`IntelCards.Today`) maps positions → `area.Pools[slot.Pool][position]`.
- `intel(id)` in expressions = `Run.Intel[id].Count` (`Expr.cs`).
- `src/Game/GameBridge.cs` `GiveItem(itemId) => PutOnGround(string.IsNullOrEmpty(itemId) ? null : ItemSpawner.Spawn(itemId))`; Core `IInventory.Give` already logs `"could not give " + itemId + " (unknown item id, or no room on the ground)"` (tone `bad`) when `GiveItem` returns null.
- Game registry (decomp): `ItemSpawner.Spawn(id)` = prebuilt factory if `PreBuiltItemHelper.preBuiltItems.ContainsKey(id)` else `DirectoryMaster.Item(id)` (dummy on miss). The game's own check elsewhere: `!DirectoryMaster.Has<GameItem>(id) && !PreBuiltItemHelper.preBuiltItems.ContainsKey(id)`.
- `ModEntry.Log` is a `ModLog` with `Log/Warning/Error(object)`.
- EF `main` @ `712bb88` (trade backdrop fix committed; was `95004d0` when facts were taken). Working tree clean at planning time.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `tools/EfSim/SimConfig.cs` | sim.json reader | Row keys `game`, `days`, `intel`, `expect`; `SimBridge.With`; `SimRowCheck`; shared `ReadGame`/`ReadRange` |
| `tools/EfSim/KitBridge.cs` | stand-in game | Optional `SimWorld` (carried rep + day) |
| `tools/EfSim/PackRun.cs` | runner | Per-row settings, world, intel roll, row expect checks, slot validation |
| `src/Game/ItemIds.cs` (new) | id guard | `Known`, `Spawn(id, where)` |
| `src/Game/GameBridge.cs` | game bridge | `GiveItem` via `ItemIds.Spawn` |
| `src/Game/TradeCounter.cs` | trade cart | `EnsureCart` via `ItemIds.Spawn` |
| `src/Game/DevTools.cs` | debug kit | `GiveKit` via `ItemIds.Spawn` |
| `docs/examples/Showcase/sim.json` | checked example | +1 row using `days`, `intel`, `expect` |
| `docs/testing.md` | sim docs | Row keys, nights-in-a-row section, run checks list, "what lint can't see" |
| `docs/intel.md`, `docs/game.md`, `docs/items.md`, `docs/getting-started.md` | docs | Intel in sim; per-row game; bad-id behaviour |

### Dependencies

- **Requires:** EF `main` clean for the files listed (true at planning). Execution base = `main` HEAD at execution start (≥ `95004d0`).
- **Blocks:** 2611140220-ner-rep-week-intel-sim-rows-plan.md (NER rows use these keys; must run against EF `main` after this merge). Later Track C refactor acceptance.

### Constraints

- **Do not touch other sessions' work.** At execution start run `git -C <repo-root> status --porcelain`; any dirty path in this plan's file list → stop, report (do not stash/revert/commit it). Unrelated dirty paths: leave alone; never stage them.
- Worktree mode: all edits/commits in `<work-root>` (`.projexwt/2611140220-ef-sim-reach-and-item-guard`). Base checkout untouched.
- **Never build into the game.** `dotnet build -c Release` of `ExpeditionFramework.csproj` without overrides copies into `Mods/`. Only the redirected compile in V7 is allowed. Never build NER's csproj.
- Existing rows: no new random draws, same `settings` object, `intel: null` → identical output.
- Intel draws from its own `Random`, never the row's.
- Source Hygiene (SKILL.md): no projex ids / step labels in code comments; rationale comments name the rejected alternative.
- Git hook blocks `--amend`, branch deletion, `stash drop`: never retry or bypass.
- Commit trailer: `Co-Authored-By: Claude <executing model name> <noreply@anthropic.com>`.

### Assumptions

- A-1 `DirectoryMaster` and `PreBuiltItemHelper` are reachable from `src/Game` without a `using` (global namespace, as `ItemSpawner` in `TradeCounter.cs`). Verify at V7.
- A-2 `GameItem` satisfies `DirectoryEntry` for `Has<GameItem>` (the game calls it). Verify at V7.
- A-3 `AreaDef.Id` is the qualified area id (`Def.Id`), as `ExpeditionLocation` passes `area.Id` to `MapContext`.
- A-4 The relative `GameManaged` override in V7 resolves from the worktree (`.projexwt/<name>/` → four `..` → game folder). If it doesn't, pass the absolute game `Probably Stolen_Data/Managed/` path on the command line (not in docs).

### Impact Analysis

- **Direct:** files in Key Files.
- **Adjacent:** `tests/Sim` compiles `tools/EfSim/Lint.cs` only — unaffected but run (SC4). `Lint.cs` untouched.
- **Downstream:** packs with `sim.json` (NER, Showcase): unchanged unless they add keys. In game: a bad id now gives nothing + warning instead of a blank item; trade carts and the debug kit skip bad ids.

---

## Implementation

### Overview

Parse → stand-in game → runner → guard → docs. Baseline captured first so SC1/SC2 compare against the untouched worktree.

### Step 0: Worktree + Baseline

**Objective:** Isolated work root and pre-change outputs.
**Confidence:** High
**Depends on:** None

1. `git -C <repo-root> status --porcelain` → confirm none of this plan's files is dirty (Constraint 1).
2. `{projex-scripts}/projex-worktree.ps1 <repo-root> projex/2611140220-ef-sim-reach-and-item-guard main` → record `<work-root>`.
3. From `<work-root>`, save baselines outside the repo (`<scratch>` = session scratch dir):

```bash
dotnet run -c Release --project tools/EfSim -- run ../../../NightExpeditionsReborn --runs 4000 > <scratch>/ner-before.txt
dotnet run -c Release --project tools/EfSim -- run docs/examples/Showcase --runs 4000 > <scratch>/showcase-before.txt
```

Both must end with `all checks passed`. If not: stop, report (pre-existing failure).

**Verification:** both files exist, last line matches `all checks passed`.
**If this fails:** remove worktree via `projex-abandon --worktree`; report.

---

### Step 1: sim.json Row Keys (SimConfig.cs)

**Objective:** Parse `game`, `days`, `intel`, `expect` on rows; overlay helper on `SimBridge`.
**Confidence:** High
**Depends on:** Step 0

**Files:** `tools/EfSim/SimConfig.cs`

**Changes:**

```csharp
// SimRow — add:
public SimGame Game;                 // null: the file's bridge.game alone
public bool Days;                    // nights follow each other: game.day +1 a night, reputation carries
public bool Intel;                   // roll each night's intel
public readonly List<SimRowCheck> Expect = new List<SimRowCheck>();

// New, beside SimMemoryCheck:
internal sealed class SimGame { public readonly Dictionary<string, int> Numbers = new Dictionary<string, int>(); public readonly HashSet<string> Coin = new HashSet<string>(); }
// runs[n].expect entry: rep.F / rep_exact.F (end of the row's last night) or intel.SLOT (nights the slot named anything).
internal sealed class SimRowCheck { public string Name; public int? Min, Max; }
```

`SimBridge` — add:

```csharp
// A copy with a row's game values laid over this one's. A row's rep.F or rep_exact.F drops the file's other spelling of
// that faction: KitBridge reads rep_exact first, so a file-wide rep_exact would otherwise hide the row's rep.
public SimBridge With(SimGame row)
{
    var copy = new SimBridge { TradeOpens = TradeOpens, LoseCashUpTo = LoseCashUpTo, TakeOldest = TakeOldest, LoseCarried = LoseCarried };
    foreach (KeyValuePair<string, int> v in Game) copy.Game[v.Key] = v.Value;
    copy.CoinGame.UnionWith(CoinGame);
    copy.Cart.AddRange(Cart);
    foreach (string name in row.Numbers.Keys.Concat(row.Coin))
    {
        string faction = GameReads.RepFaction(name, out _);
        if (faction != null) { copy.Game.Remove("rep." + faction); copy.Game.Remove("rep_exact." + faction); copy.CoinGame.Remove("rep." + faction); copy.CoinGame.Remove("rep_exact." + faction); }
        copy.Game.Remove(name); copy.CoinGame.Remove(name);
    }
    foreach (KeyValuePair<string, int> v in row.Numbers) copy.Game[v.Key] = v.Value;
    copy.CoinGame.UnionWith(row.Coin);
    return copy;
}
```

`Read` — row parsing:

```csharp
// Before:
Only(row, at, "area", "kit", "bot", "runs", "seed", "memory");
// After:
Only(row, at, "area", "kit", "bot", "runs", "seed", "memory", "game", "days", "intel", "expect");
...
if (row.TryGetValue("memory", out object m)) r.Memory = MemoryOf(m, at + ".memory");
if (row.TryGetValue("game", out object g)) { r.Game = new SimGame(); ReadGame(g, at + ".game", r.Game.Numbers, r.Game.Coin); }
if (row.TryGetValue("days", out object d)) r.Days = d is bool db ? db : throw new FormatException(at + ".days must be true or false");
if (row.TryGetValue("intel", out object it)) r.Intel = it is bool ib ? ib : throw new FormatException(at + ".intel must be true or false");
if (row.TryGetValue("expect", out object x)) r.Expect.AddRange(RowExpect(x, at + ".expect", r.Intel));
```

`ReadBridge` — replace the inline `game` loop with `ReadGame(g, "bridge.game", bridge.Game, bridge.CoinGame);` where:

```csharp
// NAME: a whole number or "coin", as bridge.game and runs[n].game take.
private static void ReadGame(object value, string at, Dictionary<string, int> numbers, HashSet<string> coin)
{
    foreach (KeyValuePair<string, object> v in Obj(value, at))
    {
        string vat = at + "." + v.Key;
        if (v.Value is string s)
        {
            if (s != "coin") throw new FormatException(vat + " must be a whole number or coin");
            coin.Add(v.Key);
        }
        else numbers[v.Key] = Int(v.Value, vat);
    }
}
```

(Error text for `bridge.game.X` stays `bridge.game.X must be a whole number or coin`.)

Range helper (used by `MemoryOf`'s expect branch and `RowExpect`; `MemoryOf` messages unchanged):

```csharp
// A whole number (exactly) or { min?, max? } with at least one.
private static (int? Min, int? Max) ReadRange(object value, string at)
{
    if (value is Dictionary<string, object> range)
    {
        Only(range, at, "min", "max");
        if (range.Count == 0) throw new FormatException(at + " needs min or max");
        int? min = range.TryGetValue("min", out object lo) ? Int(lo, at + ".min") : (int?)null;
        int? max = range.TryGetValue("max", out object hi) ? Int(hi, at + ".max") : (int?)null;
        if (min > max) throw new FormatException(at + ".min is above max");
        return (min, max);
    }
    if (value is double) { int n = Int(value, at); return (n, n); }
    throw new FormatException(at + " must be a whole number or {min, max}");
}
```

In `MemoryOf`, the non-`$` branch becomes `(check.Min, check.Max) = ReadRange(v.Value, vat);`.

```csharp
// runs[n].expect: rep.F / rep_exact.F of a faction the game has, or intel.SLOT on a row with "intel": true.
private static List<SimRowCheck> RowExpect(object value, string at, bool intel)
{
    var checks = new List<SimRowCheck>();
    foreach (KeyValuePair<string, object> v in Obj(value, at))
    {
        string vat = at + "." + v.Key;
        string faction = GameReads.RepFaction(v.Key, out _);
        if (faction != null) { if (!GameReads.IsFaction(faction)) throw new FormatException(vat + ": " + GameReads.UnknownFaction(faction)); }
        else if (v.Key.StartsWith("intel.", StringComparison.Ordinal) && v.Key.Length > "intel.".Length)
        { if (!intel) throw new FormatException(vat + " needs \"intel\": true on the row"); }
        else throw new FormatException(vat + ": a row expects rep.FACTION, rep_exact.FACTION or intel.SLOT");
        (int? min, int? max) = ReadRange(v.Value, vat);
        checks.Add(new SimRowCheck { Name = v.Key, Min = min, Max = max });
    }
    return checks;
}
```

Note: `RowExpect` reads `r.Intel`, so parse `intel` before `expect` (order above).

**Rationale:** Strict, opt-in keys match sim.json v1 style ("unknown key is an error"). `ReadGame`/`ReadRange` remove duplication instead of copying loops. No version bump: additions only.

**Verification:** V1 (build), V5a–V5d.
**If this fails:** `git -C <work-root> checkout -- tools/EfSim/SimConfig.cs`.

---

### Step 2: Carried World (KitBridge.cs)

**Objective:** Optional world shared by a row's nights: reputation + day.
**Confidence:** High
**Depends on:** Step 1

**Files:** `tools/EfSim/KitBridge.cs`

**Changes:**

```csharp
// New, in KitBridge.cs above KitBridge:
// What a "days" row carries from night to night: reputation changes and the day counter.
internal sealed class SimWorld { public readonly Dictionary<string, double> Reputation = new Dictionary<string, double>(); public int Day; }

// Ctor — Before:
public KitBridge(SimKit kit, SimBridge settings, Content content, Random random, HashSet<string> problems, IMemory memory = null)
// After:
public KitBridge(SimKit kit, SimBridge settings, Content content, Random random, HashSet<string> problems, IMemory memory = null, SimWorld world = null)
{   ...existing body...
    this.world = world;
    reputation = world?.Reputation ?? new Dictionary<string, double>();
}

// Field — Before:
private readonly Dictionary<string, double> reputation = new Dictionary<string, double>();
// After:
private readonly SimWorld world;
private readonly Dictionary<string, double> reputation;   // the world's when the row carries one, else this night's own

// GameValue — Before:
return settings.CoinGame.Contains(name) ? random.Next(2) : settings.Game.TryGetValue(name, out int value) ? value : 0;
// After:
if (world != null && name == "day") return world.Day;
return settings.CoinGame.Contains(name) ? random.Next(2) : settings.Game.TryGetValue(name, out int value) ? value : 0;
```

Update the comment above `reputation` to say it carries across a `days` row's nights. Keep `Rep`'s fallback order (changed → rep_exact → rep → 0).

**Rationale:** World null → same dictionary-per-night and same reads → existing rows identical. Day lives in the world so `GameValue` stays the single read path for runs and the intel map scope.

**Verification:** V1, V2.
**If this fails:** `git -C <work-root> checkout -- tools/EfSim/KitBridge.cs`.

---

### Step 3: Runner (PackRun.cs)

**Objective:** Per-row settings, world/day, intel, row expectations.
**Confidence:** Medium (intel wiring)
**Depends on:** Steps 1–2
**Verify-Projex:** Encouraged (SC1 byte-identity is easy to break subtly)

**Files:** `tools/EfSim/PackRun.cs`

**Changes:**

1. In `Run`, after `areas` is filled, validate intel slots (config error → exit 2, like `no area`):

```csharp
for (int r = 0; r < config.Rows.Count; r++)
{
    foreach (SimRowCheck c in config.Rows[r].Expect.Where(c => c.Name.StartsWith("intel.", StringComparison.Ordinal)))
    {
        string slot = c.Name.Substring("intel.".Length);
        if (!areas[config.Rows[r].Area].Intel.Any(s => s.Id == slot)) { Console.WriteLine("ERROR sim.json: runs[" + r + "].expect." + c.Name + ": area " + config.Rows[r].Area + " has no intel " + slot); return 2; }
    }
}
```

2. Row loop — Before:

```csharp
IMemory last = Play(content, id, areas[row.Area], row, row.Kits.Select(k => config.Kits[k]).ToList(), bot, config.Bridge, runsOverride > 0 ? runsOverride : row.Runs, localize, offered, problems);
if (row.Memory != null) memory.AddRange(row.Memory.Check(id, last, "runs[" + r + "]"));
```

After:

```csharp
SimBridge settings = row.Game == null ? config.Bridge : config.Bridge.With(row.Game);
RowResult last = Play(content, id, areas[row.Area], row, row.Kits.Select(k => config.Kits[k]).ToList(), bot, settings, runsOverride > 0 ? runsOverride : row.Runs, localize, offered, problems);
if (row.Memory != null) memory.AddRange(row.Memory.Check(id, last.Memory, "runs[" + r + "]"));
memory.AddRange(CheckRow(row, last, "runs[" + r + "]"));
```

3. Result type + check (in `PackRun`):

```csharp
// What a row leaves behind: the last night's memory and bridge, and how many nights each intel slot named anything.
private sealed class RowResult { public IMemory Memory; public KitBridge Bridge; public readonly Dictionary<string, int> IntelNights = new Dictionary<string, int>(); }

private static IEnumerable<string> CheckRow(SimRow row, RowResult result, string at)
{
    foreach (SimRowCheck c in row.Expect)
    {
        int value = c.Name.StartsWith("intel.", StringComparison.Ordinal)
            ? result.IntelNights.TryGetValue(c.Name.Substring("intel.".Length), out int n) ? n : 0
            : result.Bridge?.GameValue(c.Name) ?? 0;
        string where = at + ": " + c.Name;
        if (c.Min != null && c.Min == c.Max) { if (value != c.Min) yield return where + " should be " + c.Min + ", got " + value; }
        else if (c.Min is int min && value < min) yield return where + " should be at least " + min + ", got " + value;
        else if (c.Max is int max && value > max) yield return where + " should be at most " + max + ", got " + value;
    }
}
```

(`rep.F`/`rep_exact.F` reads go through `KitBridge.GameValue`, which never draws for rep names.)

4. `Play` — signature returns `RowResult`; body changes:

```csharp
var random = new Random(row.Seed);
// Intel has its own random so a row that turns intel on doesn't shift the dice of its runs against a row that doesn't.
Random intelRandom = row.Intel ? new Random(unchecked(row.Seed * 7919 + 1)) : null;
SimWorld world = row.Days ? new SimWorld() : null;
int firstDay = settings.Game.TryGetValue("day", out int d0) ? d0 : 0;
var result = new RowResult();
...
for (int n = 0; n < runs; n++)
{
    memory = carried ?? row.Memory?.Start(pack) ?? new MemoryStore();
    if (world != null) world.Day = firstDay + n;
    var bridge = new KitBridge(kits[n % kits.Count], settings, content, random, problems, memory, world);
    Dictionary<string, List<string>> intel = row.Intel ? RollIntel(area, bridge, memory, localize, intelRandom, result.IntelNights) : null;
    var run = new ExpeditionRun(content, area, bridge, s => random.Next(1, s + 1), localize, "you", intel);
    result.Bridge = bridge;
    ...unchanged...
}
...
result.Memory = memory;
return result;
```

```csharp
// Today's intel as the map would show it: each slot's picks as pool entries, its when read against this night's game
// and memory. The game shows only cards whose afterhour event started; every rolled pick counts here.
private static Dictionary<string, List<string>> RollIntel(AreaDef area, KitBridge bridge, IMemory memory, Localizer localize, Random random,
    Dictionary<string, int> nights)
{
    var map = new MapContext(area.Id, localize, bridge.GameValue, memory);
    Dictionary<string, List<int>> picks = IntelRoller.Roll(area, s => random.Next(1, s + 1), map.Root);
    var today = new Dictionary<string, List<string>>();
    foreach (IntelSlot slot in area.Intel)
    {
        today[slot.Id] = picks[slot.Id].Select(p => area.Pools[slot.Pool][p]).ToList();
        if (today[slot.Id].Count > 0) nights[slot.Id] = (nights.TryGetValue(slot.Id, out int k) ? k : 0) + 1;
    }
    return today;
}
```

Notes for the executor:
- `picks[slot.Id]` is non-empty only when `area.Pools` has `slot.Pool` (Roll skips otherwise), so the indexer is safe.
- A `"coin"` game name read by an intel `when` draws from the row's random via `bridge.GameValue`; only intel rows can hit this. Leave as is (no pack reads a coin name in intel `when`); state it in the `RollIntel` comment's last sentence: "A coin game read in a when draws from the run's random."
- Update the `Play` comment: "Plays a row's runs and returns what its last night left and the intel tally."

**Rationale:** Rows without the keys: `settings` is `config.Bridge`, `world`/`intelRandom` null, `intel` null → no change in draws or reads (SC1). Intel built exactly as `IntelCards` maps positions → pool entries.

**Verification:** V1, V2, V3, V4, V5d, V6.
**If this fails:** `git -C <work-root> checkout -- tools/EfSim/PackRun.cs`; re-check SC1 diff to find the first differing row.

---

### Step 4: Showcase Row

**Objective:** Committed EF regression for `days`, `intel`, row `expect`.
**Confidence:** Medium (rep bound is data-derived, see rationale)
**Depends on:** Step 3

**Files:** `docs/examples/Showcase/sim.json`

**Changes:** append to `runs` (after the memory row; add the comma to the previous row):

```json
    { "area": "night_market", "kit": "blade", "bot": "careful", "runs": 200, "seed": 77, "days": true, "intel": true,
      "memory": { "start": { "visits": 3 }, "carry": true },
      "expect": { "intel.fence": { "min": 1 }, "rep.black_market": { "min": 26 }, "rep_exact.security": { "max": -4050 } } }
```

**Rationale:** Showcase data: fence intel `when: memory.visits > 0` (start 3 → gate open, chance 60); `run_end` adds `run.deals * 25` hundredths to black_market (never subtracts) and −0.5 security on heat ≥ 4 (never adds). File-wide start: `rep.black_market` 25, `rep_exact.security` −4050. So `rep_exact.security <= -4050` holds by design; `rep.black_market >= 26` needs ≥4 deals carried over the row — only plausible with `days` (negative control V6b proves carry matters). If V6a fails on the black_market bound only, do NOT lower it: stop and report the value (it means the careful bot rarely trades — evidence for the human/coordinator).

**Verification:** V6a, V6b, V2 (existing lines).
**If this fails:** `git -C <work-root> checkout -- docs/examples/Showcase/sim.json`.

---

### Step 5: Item-Id Guard (B1)

**Objective:** Unknown id → nothing spawned + one warning per id.
**Confidence:** High (mirrors the game's own check)
**Depends on:** None (independent of 1–4)

**Files:** `src/Game/ItemIds.cs` (new), `src/Game/GameBridge.cs`, `src/Game/TradeCounter.cs`, `src/Game/DevTools.cs`

**Changes:**

```csharp
// src/Game/ItemIds.cs
using System.Collections.Generic;

namespace ExpeditionFramework.Game
{
    /// <summary>
    /// Spawns game items by id, refusing ids the game doesn't know. ItemSpawner.Spawn makes a blank "unknown" item for
    /// those instead of returning null, so a misspelled id in pack data would hand the player junk with no sign of the
    /// typo. Here it gives nothing and warns once per id in the mod log.
    /// </summary>
    internal static class ItemIds
    {
        private static readonly HashSet<string> reported = new HashSet<string>();

        /// <summary>True when the game can make an item with this id: a prebuilt item or one in the item directory.</summary>
        public static bool Known(string id) =>
            id != null && (PreBuiltItemHelper.preBuiltItems.ContainsKey(id) || DirectoryMaster.Has<GameItem>(id));

        /// <summary>The spawned item, or null for an empty or unknown id. <paramref name="where"/> names the caller in the warning.</summary>
        public static GameItem Spawn(string id, string where)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (Known(id)) return ItemSpawner.Spawn(id);
            if (reported.Add(id)) ModEntry.Log.Warning("Unknown item id '" + id + "' (" + where + "): nothing given. Check the id's spelling.");
            return null;
        }
    }
}
```

```csharp
// GameBridge.cs — Before:
public string GiveItem(string itemId) => PutOnGround(string.IsNullOrEmpty(itemId) ? null : ItemSpawner.Spawn(itemId));
// After:
public string GiveItem(string itemId) => PutOnGround(ItemIds.Spawn(itemId, "give"));

// TradeCounter.cs EnsureCart — Before:
GameItem item = ItemSpawner.Spawn(id);
// After:
GameItem item = ItemIds.Spawn(id, "trade cart");

// DevTools.cs GiveKit — Before:
foreach (string id in kit) ModHelper.MoveItemToInventory(ModHelper.SpawnItem(id));
// After:
foreach (string id in kit)
{
    GameItem item = ItemIds.Spawn(id, "debug kit");
    if (item != null) ModHelper.MoveItemToInventory(item);
}
```

Keep the empty-id behaviour of `GiveItem` (Core's comment relies on "" → null without a "bad" log; `ItemIds.Spawn` returns null for "" without warning, and Core skips its log for empty ids).

**Rationale:** Same predicate the game uses; one helper so the three spawn sites can't drift. Rejected: a load-time catalog check (B2) — the registry fill order vs pack load is unverified; out of scope.

**Verification:** V7, V8.
**If this fails:** `git -C <work-root> checkout -- src/Game/GameBridge.cs src/Game/TradeCounter.cs src/Game/DevTools.cs` and delete `src/Game/ItemIds.cs`.

---

### Step 6: Docs (Track F, EF part)

**Objective:** Document new keys and the bad-id behaviour.
**Confidence:** High
**Do-Projex:** Encouraged
**Depends on:** Steps 1–5

**Files / changes:**

- `docs/testing.md`
  - **Rows** paragraph: after "and [`memory`](#memory)." add: "Rows also take `game`, `days`, `intel` and `expect`; see [Nights in a row](#nights-in-a-row)."
  - "Each check that fails" list: add "- every row `expect` (end reputation, intel nights) that comes out different."
  - New section `## Nights in a row` (before `## Packing`), table:

| Key | What it does |
|---|---|
| `game` | `game.NAME` values for this row only, laid over `bridge.game` (same forms: whole number or `"coin"`). A row's `rep.F` or `rep_exact.F` replaces both spellings of that faction from the bridge |
| `days` | `true`: the row's nights follow each other. `game.day` starts at the row's (or bridge's) `day`, default 0, and goes up 1 a night; reputation a night changes stays for the next. Pair with `memory.carry` for a save that lasts the same nights |
| `intel` | `true`: each night rolls the area's intel as the map does, from its own random, against that night's game and memory, and the run sees it in `intel(id)` and `schedule` with `intel` |
| `expect` | After the row's last night: `rep.FACTION` / `rep_exact.FACTION` (the reputation that night ended with) and `intel.SLOT` (how many nights the slot named anything; needs `"intel": true`). A whole number or `{ "min", "max" }` |

    Plus one example row (the Showcase row from Step 4) and: "Rows without these keys play as before: separate nights, the bridge's values, no intel. `--runs` changes how many nights a `days` row plays, so expect ranges, not end values that depend on the count."
  - `**What lint can't see.**` paragraph: replace "and in the game gives nothing." with "and in the game gives nothing; the framework logs `Unknown item id` once per id."
  - `## Bridge` reputation paragraph: append "With `days`, the copy lasts for the row."
- `docs/intel.md` line "`ef-sim run` has no map, so its runs have no intel…" → "`ef-sim run` rolls no intel unless a row sets `\"intel\": true`; then `intel(id)` and `schedule` with `intel` work as on the map. See [Testing: nights in a row](testing.md#nights-in-a-row)."
- `docs/game.md` line "In `ef-sim`, game reads come from `bridge.game`…" → "In `ef-sim`, game reads come from `bridge.game` in `sim.json`, or a row's own `game`; a `days` row moves `game.day` and keeps reputation from night to night. See [Testing: bridge](testing.md#bridge)."
- `docs/items.md` `## Item ids`: replace "…passes lint with no warning, and in the game gives or matches nothing; the framework doesn't report it." with "…passes lint with no warning. In the game a filter or `item_stats` entry with it matches nothing, silently; giving it (`give_item`, a loot roll, a trade cart, the `debug_kit`) gives nothing and the framework logs `Unknown item id '…'` once per id in the game's log, and the expedition log says it could not give the item." And "a wrong id is simply missing from what you get" → "a wrong id is missing from what you get and logged."
- `docs/getting-started.md` "a misspelled id passes lint and gives the player nothing." → "a misspelled id passes lint, gives the player nothing, and is logged as an unknown item id in the game."

**Verification:** V9.
**If this fails:** `git -C <work-root> checkout -- docs/`.

---

### Step 7: Gates + Commit

**Objective:** Run the full suite, commit by explicit path.
**Depends on:** Steps 1–6

Run V1–V9. Then (from PowerShell):

```powershell
{projex-scripts}\stage-n-commit.ps1 <work-root> "feat(ef-sim): per-row game, nights in a row and intel; guard unknown item ids" "--trailer Projex: 2611140220-ef-sim-reach-and-item-guard-plan.md" "--trailer Co-Authored-By: Claude <model> <noreply@anthropic.com>" tools/EfSim/SimConfig.cs tools/EfSim/KitBridge.cs tools/EfSim/PackRun.cs src/Game/ItemIds.cs src/Game/GameBridge.cs src/Game/TradeCounter.cs src/Game/DevTools.cs docs/examples/Showcase/sim.json docs/testing.md docs/intel.md docs/game.md docs/items.md docs/getting-started.md
```

Split into two commits (ef-sim+docs testing/intel/game+Showcase; B1+items/getting-started) if the executor prefers; never stage other paths. Cleanup contract: remove worktree `bin/`, `obj/` (incl. `tools/EfSim`, `tests/Sim`, `api/`) and scratch files before close.

**Close/merge (for close-projex):** before merging into `main`, `git -C <repo-root> status --porcelain`; if any path this branch touches is dirty in `main`, stop and report — never stash/revert others' work. The hook blocks branch deletion: if the close script fails at deleting `projex/2611140220-…`, leave the branch and report; don't bypass.

---

## Verification Plan

`<wr>` = `<work-root>`; commands run from `<wr>` in Git Bash. `EFSIM` = `dotnet run -c Release --project tools/EfSim --`.

### Deterministic Suite

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V1 | Steps 1–3 | `dotnet build -c Release tools/EfSim` | exit 0, `0 Error(s)` | compile errors |
| V2 | SC1, SC2 | `$EFSIM run ../../../NightExpeditionsReborn --runs 4000 > <scratch>/ner-after.txt; cmp <scratch>/ner-before.txt <scratch>/ner-after.txt` and `diff <(grep '^night_market' <scratch>/showcase-before.txt) <(grep '^night_market' <scratch>/showcase-after.txt \| head -n 2)` (after V6a writes showcase-after) | `cmp` exit 0; `diff` exit 0; showcase-after has 3 `^night_market` lines | RNG shift / changed reads in old rows |
| V3 | SC3 | `$EFSIM lint ../../../NightExpeditionsReborn --strict`; same for `docs/examples/Starter`, `docs/examples/Showcase` | each exit 0 with 0 problems, 0 warnings | lint regressions |
| V4 | SC4 | `dotnet run -c Release --project tests/Sim` | exit 0 | Core/Lint regressions |
| V5a | SC5 | scratch copy of Showcase (`cp -r docs/examples/Showcase <scratch>/bad`), first row gets `"days": 1`; `$EFSIM run <scratch>/bad` | exit 2; stdout contains `runs[0].days must be true or false` | loose bool parse |
| V5b | SC5 | same copy, first row `"expect": { "rep.nobody": 1 }` (days removed) | exit 2; contains `unknown faction 'nobody'` | faction validation |
| V5c | SC5 | first row `"expect": { "intel.fence": 1 }` without `intel` | exit 2; contains `needs "intel": true` | intel expect guard |
| V5d | SC5 | first row `"intel": true, "expect": { "intel.nope": 1 }` | exit 2; contains `has no intel nope` | slot validation |
| V6a | SC6, SC7 | `$EFSIM run docs/examples/Showcase --runs 4000 > <scratch>/showcase-after.txt; tail -n 1 <scratch>/showcase-after.txt` | exit 0; `ef-sim ef_showcase: all checks passed` | intel/rep/days not wired |
| V6b | SC6 | scratch Showcase copy with the new row's `"days": true` removed; `$EFSIM run <scratch>/nodays --runs 4000` | exit 1; a `FAIL runs[2]: rep.black_market should be at least 26` line | carry not actually doing anything |
| V6c | SC6 | scratch copy of NER (`cp -r ../../../NightExpeditionsReborn <scratch>/ner-days`, minus `bin/ obj/`), append row `{ "area": "ne_annex", "kit": ["random0","random1","random2"], "bot": "random", "runs": 200, "seed": 2099, "days": true, "game": { "day": 6 }, "memory": { "carry": true, "expect": { "rep_week": { "min": 1 } } } }`; `$EFSIM run <scratch>/ner-days` | exit 0, `all checks passed` | day not advancing |
| V7 | SC8 | `dotnet build ExpeditionFramework.csproj -c Release "-p:GameManaged=..\..\..\..\Probably Stolen_Data\Managed\\" "-p:InstallDir=<scratch>\ef-install\\"` | exit 0, `0 Error(s)`; nothing written under the game's `Mods/` (check `Mods/ExpeditionFramework/ExpeditionFramework.dll` timestamp unchanged) | compile errors; accidental install |
| V8 | SC8 | `grep -rn "ItemSpawner.Spawn" src/Game` ; `grep -c "ModHelper.SpawnItem" src/Game/DevTools.cs` | one hit, in `src/Game/ItemIds.cs`; `0` | missed spawn site |
| V9 | SC9 | `grep -rn "has no map, so its runs have no intel\|gives or matches nothing; the framework doesn't report it" docs` ; `grep -c "## Nights in a row" docs/testing.md` | no hits; `1` | stale docs |

### Judgment Checks

| Check | Covers | Why not deterministic |
|-------|--------|-----------------------|
| New/changed comments obey Source Hygiene (no projex ids, rationale names rejected alternative) | Steps 1–5 | prose quality |
| Docs wording accurate vs code (overlay rule, day start, `--runs` caveat) | SC9 | prose vs behaviour |
| In game: bad id in a `give_item` gives nothing and logs once | B1 | needs the game; human after install (Notes) |

### Acceptance Criteria Validation

| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| SC1 | V2 | NER stdout byte-identical |
| SC2 | V2, V6a | 2 old Showcase lines identical, 3rd line new, all passed |
| SC3 | V3 | 0/0 ×3 |
| SC4 | V4 | exit 0 |
| SC5 | V5a–V5d | exit 2 with messages |
| SC6 | V6a, V6b, V6c | pass / negative control fails / rollover passes |
| SC7 | V6a | `intel.fence` passes |
| SC8 | V7, V8 | compiles, single spawn site |
| SC9 | V9 + judgment | no stale text |

---

## Rollback Plan

1. Before close: `{projex-scripts}/projex-abandon.ps1 <repo-root> main projex/2611140220-ef-sim-reach-and-item-guard --worktree` (base untouched; hook may refuse branch deletion → leave branch, report).
2. After merge: `git -C <repo-root> revert <squash-sha>` (new commit; no amend).

---

## Notes

### Risks

- Showcase `rep.black_market >= 26` bound depends on the careful bot trading ≥4 times in 200 carried nights: if V6a fails there, report the observed value; don't loosen silently.
- V7 is a real compile of the game-facing project with install redirected. If the coordinator/human disallows any build of `ExpeditionFramework.csproj`, skip V7 and mark B1 compile as unverified (A-1, A-2 then rest on decomp reading).
- Concurrent EF sessions: Constraint 1 + close-time dirty check.
- Hook blocks branch deletion → close scripts may report failure at the delete step after the merge succeeded; read state before retrying anything.

### Split Verdict

No split — heuristic tripped (~660 lines, 8 steps) but steps tightly coupled: Steps 1–4 are one compile unit and one SC1 gate; B1 (Step 5) is ~30 lines and its doc lines share Step 6. Cross-repo work is already split (NER rows → 2611140220-ner-rep-week-intel-sim-rows-plan.md).

### Follow-ups (not this plan)

- Human in-game check: misspell one id in a scratch pack's `give_item`; expect nothing given, one `Unknown item id` warning, one "could not give" log line.
- B2 load-time id check; pinned intel per row; `game.clears` in `days` rows.

### Open Questions

- [x] TradeCounter collision → resolved: other session committed `712bb88`; file clean at planning time (Constraint 1 re-checks).
