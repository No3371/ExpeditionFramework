# ef-sim NER Pack: kit lists, `expect.packing`, and NightExpeditionsReborn's `sim.json`

> **Status:** Complete
> **Author:** Claude Opus 5.5 (plan)
> **Source:** Direct request — sprint iteration 3 objective (derive)
> **Sprint:** 2611121010-pack-defined-sims-sprint.md — iteration 3
> **Parent:** 2611121010-pack-defined-sims-sprint.md
> **Related Projex:** 2611121010-pack-defined-sims-sprint.md | 2611121039-ef-sim-rule-bots-plan.md (`careful` fixture, `Plan` mapping table) | 2611121053-ef-sim-rule-bots-audit.md | 2611121012-ef-sim-skeleton-plan.md | 2611121121-ef-sim-ner-pack-patch.md
> Execution: completed via 2611121121-ef-sim-ner-pack-patch.md (code commit 853846b)
> **Worktree:** Yes — sprint worktree `.projexwt/2611121010-pack-defined-sims`, branch `projex/sprint/2611121010-pack-defined-sims`; all framework commits land there, never on `main`. One file (`NightExpeditionsReborn/sim.json`) is written outside the repo (see Constraints).

---

## Summary

Adds the two `sim.json` features NightExpeditionsReborn (NER) needs — a row `kit` that is a list cycled by run index, and `expect.packing` cases checked through `MapContext.Missing` — then writes NER's `sim.json` (kits matching NER's `FakeBridge`, `careful`/`greedy` rule bots transcribed from `Plan`, the 23-row matrix with NER seeds, the 60-id offered list, the 6 packing cases). `ef-sim run` on NER then exits 0 and executes every data-expressible check NER's C# sim makes. The planner prototyped all of it in a scratch copy: outputs below are the prototype's, byte for byte.

**Scope:** `tools/EfSim/SimConfig.cs`, `tools/EfSim/PackRun.cs`, `README.md` (framework, sprint branch) + one new file `NightExpeditionsReborn/sim.json` (on disk, outside any commit). No `KitBridge` change, no engine change.
**Estimated Changes:** 3 framework files (2 C# methods changed, 2 helpers added, 2 small classes), 1 new data file (192 lines).

---

## Objective

### Problem / Gap / Need

`sim.json` v1 can express NER's careful/greedy rows (iteration 2) but not (a) NER's 3 `random` rows, whose kit varies with run index `n` (period 3), nor (b) NER `CheckPacking` (6 bags vs `MapContext.Missing` on `ne_sump`). No NER `sim.json` exists, so `ef-sim` has never run NER.

### Success Criteria

- [ ] C1 — A `runs[]` row's `kit` may be a non-empty list of kit names; run `n` (0-based) of the row uses `kit[n % length]`. A single string works as before. Bad forms are exit-2 errors naming the place (unknown name, empty list, non-text element, neither text nor list).
- [ ] C2 — `expect.packing` is a list of cases `{ area, bag?, missing }`; each is checked through `MapContext.Missing`; a mismatch is a `FAIL expect.packing[N]: …` line (exit 1). `bag` omitted = no backpack (null bag). Unknown keys / missing `area` or `missing` / unknown area are exit-2 errors.
- [ ] C3 — `NightExpeditionsReborn/sim.json` exists with exactly the content in Step 4 (sha256 below); NER's `tests/Sim` is untouched.
- [ ] C4 — `ef-sim run <NER>` (default 2000 runs per row) exits 0, prints 23 table rows and `packing: 6 case(s), 0 failed`, no `FAIL`/`ERROR`, last line `ef-sim night_expeditions_reborn: all checks passed`; output is exactly the prototype's (sha256 below) and deterministic.
- [ ] C5 — Sample pack `ef-sim run samples/SampleArea` output is byte-identical to before the change.
- [ ] C6 — Framework `tests/Sim` passes; `lint --strict` on sample and NER both `0 error(s), 0 warning(s)`; game assembly builds 0 warnings 0 errors.
- [ ] C7 — README documents kit lists and `expect.packing`.

### Out of Scope

- `KitBridge` behaviour knobs (luck, trade, `LoseCarried`, `TakeBack` order) — the prototype shows every check passes without them (objective's exception not triggered).
- NER `CheckGunsmithLoot`; 4000-run parity with NER's C# table (iteration 4+). The balance numbers printed here are **not** expected to match NER's yet.
- Editing or deleting NER `tests/Sim`, NER's other files, or `samples/SampleArea/sim.json`.
- Changing the 2000-step guard, the table format, or bot semantics.

---

## Context

### Current State

- `SimConfig.Read` (`tools/EfSim/SimConfig.cs`) parses `runs[n].kit` with `Str(...)` into `SimRow.Kit` and checks `config.Kits.ContainsKey`. Kit items are parsed inline in the `kits` loop. `expect` allows only `offered`.
- `PackRun.Run` (`tools/EfSim/PackRun.cs`) resolves one `AreaDef` per row into a list `areas`, calls `Play(content, areas[r], row, config.Kits[row.Kit], …)`; `Play` builds `new KitBridge(kit, …)` per run. Failures order: load errors, sorted `problems`, never-offered.
- `MapContext(string areaId, Localizer, Func<string,int> game).Missing(AreaDef area, IReadOnlyList<RunItem> bag)` (`src/Core/ExpeditionRun.cs:1088-1158`) returns localized texts; `bag == null` → the area's `no_backpack` text only; otherwise one text per `needs` entry no item satisfies. NER `ne_sump` packing (`NightExpeditionsReborn/ExpeditionFramework/areas.json:60-66`): `no_backpack: ne_need_weapon`, needs `#weapon` → `ne_need_weapon` ("weapon"), `["#melee", "#has_rounds"]` → `ne_need_ammo` ("ammo for the gun").
- NER C# sim (`NightExpeditionsReborn/tests/Sim/Program.cs`): `CheckPacking` (84-105) builds bare `RunItem { Id, Name, Tags }` (no `ApplyStats`) and compares `string.Join(", ", Missing(...))`. Kits (171-176, 184, `FakeBridge` ctor 297-314): gun `gun` tags `firearm weapon in_hand` props firepower/accuracy/recoil/ergonomics 50/reliability/capacity; `small_pistol_ammo` ×Mags `ammo` kind 0; `bandage_item` ×Meds no tags (added even when Meds = 0); `combat_knife` ×1 `melee weapon in_hand` sides 4 unless NoBlade; tools ×2 each + `small_pistol_ammo_p` ×1 `ammo armor_piercing` kind 1 when Tools; credits 500 with tools else 60. Random kit: Mags = Meds = `2+n%3`, capacity 7, stats 40, Tools on `n%3==2`, NoBlade on `n%3==0`. Seeds `areaId.Length*31+policy.Length` with `areaId = "night_expeditions_reborn:<area>"`.
- NER is not versioned: its folder has an initialized git repo with **no commits** and everything untracked. Nothing in NER is committed by this plan.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `tools/EfSim/SimConfig.cs` | strict `sim.json` reader | `SimRow.Kits` list (+ `Kit` label), `SimPacking` class, `Items(...)` helper shared by kits and bags, `expect.packing` parsing |
| `tools/EfSim/PackRun.cs` | `ef-sim run` | areas keyed by name (rows + packing cases), `AreaId` helper, kit cycling in `Play`, `CheckPacking` |
| `README.md` | docs (`### ef-sim run`) | checks list, kit-list paragraph, new `#### Packing` section |
| `NightExpeditionsReborn/sim.json` (outside repo) | NER pack data | new file |

### Dependencies

- **Requires:** iterations 1–2 (`ef-sim run`, rule bots) — on the sprint branch at `de7ca9d`.
- **Blocks:** iteration 4 (bridge knobs + 4000-run parity) uses this `sim.json`.

### Constraints

- All framework commits on `projex/sprint/2611121010-pack-defined-sims` in the work-root; never in the base checkout.
- `NightExpeditionsReborn/sim.json` is created on disk with the Write tool at `E:\SteamLibrary\steamapps\common\Probably Stolen\Modding\NightExpeditionsReborn\sim.json`. Do **not** run `git add`/`git commit` in the NER folder. Its exact content lives in this plan (Step 4) and is pinned by sha256; the patch doc must record the path and the V3 hash so the audit can check it.
- Commit trailer: `Co-Authored-By: Claude Sonnet <noreply@anthropic.com>` (the executing model's name).
- Files are LF (`core.autocrlf=true`); keep the existing style (no new comments beyond those shown).
- Game assembly build from the worktree needs the `GameManaged`/`InstallDir` overrides in V10 so nothing deploys into the live game.

### Assumptions

- Prototype built and run on a copy of the work-root at `de7ca9d` and a copy of NER's `ExpeditionFramework/`, `Localization/`, `manifest.xml` — outputs identical expected on the real folders (V4 verifies).
- NER's data on disk is unchanged since 2026-11-12 11:10 (lint 0/0 then). If V4's hash differs but V4's other expectations hold, see Step 4 "If this fails".

### Impact Analysis

- **Direct:** `SimConfig`, `PackRun`, README; new NER data file.
- **Adjacent:** `RuleBot`/`KitBridge` untouched; `Lint.cs` (shared with framework `tests/Sim`) untouched, so `tests/Sim` compiles the same.
- **Downstream:** sample `sim.json` keeps working (single-string kits, no packing → no `packing:` line → output unchanged).

---

## Implementation

### Overview

Two reader features, one runner change, docs, then the NER data file. Code below is the prototype's final code; copy it exactly.

### Step 0: Baseline (Git Bash, cwd = work-root)

**Objective:** capture the pre-change sample output and confirm NER has no `sim.json`.
**Confidence:** High
**Depends on:** None

```bash
export T=$(mktemp -d); echo "T=$T"
NER=../../../NightExpeditionsReborn
dotnet build -c Release tools/EfSim/EfSim.csproj -warnaserror
ef() { dotnet run -c Release --no-build --project tools/EfSim -- "$@" | tr -d '\r'; return ${PIPESTATUS[0]}; }
ef run samples/SampleArea > "$T/before.txt"; echo "exit=$?"
sha256sum "$T/before.txt"
test ! -e "$NER/sim.json" && echo ABSENT
sha256sum "$NER/tests/Sim/Program.cs" "$NER/tests/Sim/Sim.csproj"
```

Expected: `exit=0`; `b1565178f39d91aeed27111567bba72b20a40bfee5cecead9141144c2951a033`; `ABSENT`; `d4732e8e8916e11a2f7f0d35fc093fed36126a0c0f0df9aea18c182ad7d8624d` (Program.cs), `26ca5458c92409d81efd40943599049374dd55c428d7dc42287cbe90fdb0cb7a` (Sim.csproj). Keep `$T` for every later step.

**If this fails:** `ABSENT` missing → stop and report (someone else wrote NER `sim.json`; do not overwrite). Hash differs → the sprint branch moved; report before continuing.

---

### Step 1: `SimConfig` — kit lists, shared item parser, `expect.packing`

**Objective:** C1, C2 parsing.
**Confidence:** High
**Depends on:** Step 0
**Do-Projex: Encouraged**

**Files:** `tools/EfSim/SimConfig.cs`

**Changes:**

(a) Row and packing classes — line 9:

```csharp
// Before:
internal sealed class SimRow { public string Area, Kit, Bot; public int Runs, Seed; }

// After:
internal sealed class SimRow
{
    public string Area, Bot; public int Runs, Seed;
    public readonly List<string> Kits = new List<string>();     // run n uses Kits[n % Kits.Count]
    public string Kit => string.Join("+", Kits);
}
internal sealed class SimPacking { public string At, Area; public List<SimItem> Bag; public List<string> Missing; }  // Bag null = no backpack
```

(b) Field — after `public readonly List<string> Offered = new List<string>();` add:

```csharp
    public readonly List<SimPacking> Packing = new List<SimPacking>();
```

(c) Kits loop — replace the inline item parsing:

```csharp
// Before:
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

// After:
            if (kit.TryGetValue("items", out object i)) parsed.Items.AddRange(Items(i, at + ".items"));
            config.Kits[k.Key] = parsed;
```

(d) Row parsing — remove `Kit = …` from the initializer and replace the kit check:

```csharp
// Before:
            var r = new SimRow
            {
                Area = Str(Need(row, "area", at), at + ".area"),
                Kit = Str(Need(row, "kit", at), at + ".kit"),
                Bot = row.TryGetValue("bot", out object b) ? Str(b, at + ".bot") : RandomBot,
                Runs = Int(Need(row, "runs", at), at + ".runs"),
                Seed = row.TryGetValue("seed", out object s) ? Int(s, at + ".seed") : 42 + n,
            };
            if (!config.Kits.ContainsKey(r.Kit)) throw new FormatException(at + ".kit: no kit named " + r.Kit);

// After:
            var r = new SimRow
            {
                Area = Str(Need(row, "area", at), at + ".area"),
                Bot = row.TryGetValue("bot", out object b) ? Str(b, at + ".bot") : RandomBot,
                Runs = Int(Need(row, "runs", at), at + ".runs"),
                Seed = row.TryGetValue("seed", out object s) ? Int(s, at + ".seed") : 42 + n,
            };
            object kitValue = Need(row, "kit", at);
            if (kitValue is string one) r.Kits.Add(one);
            else if (kitValue is List<object> many) r.Kits.AddRange(many.Select(x => Str(x, at + ".kit")));
            else throw new FormatException(at + ".kit must be a kit name or a list of kit names");
            if (r.Kits.Count == 0) throw new FormatException(at + ".kit is empty");
            foreach (string kit in r.Kits.Where(kit => !config.Kits.ContainsKey(kit))) throw new FormatException(at + ".kit: no kit named " + kit);
```

(e) `expect`:

```csharp
// Before:
            Only(expect, "expect", "offered");
            if (expect.TryGetValue("offered", out object o)) config.Offered.AddRange(List(o, "expect.offered").Select(x => Str(x, "expect.offered")));

// After:
            Only(expect, "expect", "offered", "packing");
            if (expect.TryGetValue("offered", out object o)) config.Offered.AddRange(List(o, "expect.offered").Select(x => Str(x, "expect.offered")));
            if (expect.TryGetValue("packing", out object pk))
            {
                List<object> cases = List(pk, "expect.packing");
                for (int n = 0; n < cases.Count; n++)
                {
                    string at = "expect.packing[" + n + "]";
                    Dictionary<string, object> c = Obj(cases[n], at);
                    Only(c, at, "area", "bag", "missing");
                    config.Packing.Add(new SimPacking
                    {
                        At = at,
                        Area = Str(Need(c, "area", at), at + ".area"),
                        Bag = c.TryGetValue("bag", out object bag) ? Items(bag, at + ".bag") : null,
                        Missing = List(Need(c, "missing", at), at + ".missing").Select(x => Str(x, at + ".missing")).ToList(),
                    });
                }
            }
```

(f) New helper — insert immediately before `private static object Need(`:

```csharp
    // A list of items { id, count?, tags?, props? }, as in kits.NAME.items and expect.packing[n].bag.
    private static List<SimItem> Items(object value, string at)
    {
        List<object> items = List(value, at);
        var parsed = new List<SimItem>();
        for (int n = 0; n < items.Count; n++)
        {
            string iat = at + "[" + n + "]";
            Dictionary<string, object> item = Obj(items[n], iat);
            Only(item, iat, "id", "count", "tags", "props");
            var si = new SimItem { Id = Str(Need(item, "id", iat), iat + ".id") };
            if (item.TryGetValue("count", out object cnt)) si.Count = Int(cnt, iat + ".count");
            if (item.TryGetValue("tags", out object tags)) si.Tags.AddRange(List(tags, iat + ".tags").Select(t => Str(t, iat + ".tags").TrimStart('#')));
            if (item.TryGetValue("props", out object props))
            {
                foreach (KeyValuePair<string, object> p in Obj(props, iat + ".props")) si.Props[p.Key] = Int(p.Value, iat + ".props." + p.Key);
            }
            parsed.Add(si);
        }
        return parsed;
    }

```

**Rationale:** one item parser keeps kit and bag items identical in shape and error locations (`kits.X.items[n]…` unchanged). `SimRow.Kit` survives as a computed label so `PackRun`'s table and `where` strings need no change and single-kit rows print exactly as before. Missing-as-list (not one joined string) lets `[]` mean "ready" and is compared joined with `", "` like NER.

**Verification:** V1 (builds after Step 2), V6.

**If this fails:** `git checkout -- tools/EfSim/SimConfig.cs`.

---

### Step 2: `PackRun` — kit cycling, packing check

**Objective:** C1, C2 behaviour.
**Confidence:** High
**Depends on:** Step 1
**Verify-Projex: Encouraged** (C5 byte-identity of sample output is easy to break)

**Files:** `tools/EfSim/PackRun.cs`

**Changes:**

(a) Area resolution:

```csharp
// Before:
            var areas = new List<AreaDef>();
            foreach (SimRow row in config.Rows)
            {
                string areaId = row.Area.Contains(':') ? row.Area : id + ":" + row.Area;
                if (!content.Areas.TryGetValue(areaId, out AreaDef area)) { Console.WriteLine("ERROR sim.json: no area " + areaId); return 2; }
                areas.Add(area);
            }

// After:
            var areas = new Dictionary<string, AreaDef>();
            foreach (string name in config.Rows.Select(row => row.Area).Concat(config.Packing.Select(p => p.Area)))
            {
                string areaId = AreaId(id, name);
                if (!content.Areas.TryGetValue(areaId, out AreaDef area)) { Console.WriteLine("ERROR sim.json: no area " + areaId); return 2; }
                areas[name] = area;
            }
```

(b) `Play` call in the row loop:

```csharp
// Before:
                Play(content, areas[r], row, config.Kits[row.Kit], bot, runsOverride > 0 ? runsOverride : row.Runs, localize, offered, problems);

// After:
                Play(content, areas[row.Area], row, row.Kits.Select(k => config.Kits[k]).ToList(), bot, runsOverride > 0 ? runsOverride : row.Runs, localize, offered, problems);
```

(c) Failures — packing runs after the never-picked WARNs and before `problems` is copied (its localizer can add missing-text problems):

```csharp
// Before:
            failures.AddRange(problems.OrderBy(p => p, StringComparer.Ordinal));
            failures.AddRange(config.Offered.Where(o => !offered.Contains(o)).Select(o => "never offered: " + o));

// After:
            List<string> packing = CheckPacking(id, config, areas, localize);
            failures.AddRange(problems.OrderBy(p => p, StringComparer.Ordinal));
            failures.AddRange(packing);
            failures.AddRange(config.Offered.Where(o => !offered.Contains(o)).Select(o => "never offered: " + o));
```

(d) New methods — insert immediately before `private static void Play(`:

```csharp
    // expect.packing: what MapContext.Missing says a bag lacks for an area, compared as the joined text the player sees.
    private static List<string> CheckPacking(string id, SimConfig config, Dictionary<string, AreaDef> areas, Localizer localize)
    {
        var failures = new List<string>();
        if (config.Packing.Count == 0) return failures;
        foreach (SimPacking c in config.Packing)
        {
            var map = new MapContext(AreaId(id, c.Area), localize, name => 0);
            List<RunItem> bag = c.Bag?.Select(s =>
            {
                var item = new RunItem { Handle = s.Id, Id = s.Id, Name = s.Id, Count = s.Count };
                item.Tags.UnionWith(s.Tags);
                foreach (KeyValuePair<string, int> p in s.Props) item.Props[p.Key] = p.Value;
                return item;
            }).ToList();
            string got = string.Join(", ", map.Missing(areas[c.Area], bag)), want = string.Join(", ", c.Missing);
            if (got != want) failures.Add(c.At + ": " + c.Area + " should need [" + want + "], got [" + got + "]");
        }
        Console.WriteLine("packing: " + config.Packing.Count + " case(s), " + failures.Count + " failed");
        return failures;
    }

    private static string AreaId(string id, string name) => name.Contains(':') ? name : id + ":" + name;

```

(e) `Play` signature and bridge:

```csharp
// Before:
    private static void Play(Content content, AreaDef area, SimRow row, SimKit kit, RuleBot bot, int runs, Localizer localize,
…
            var bridge = new KitBridge(kit, content, random, problems);

// After:
    private static void Play(Content content, AreaDef area, SimRow row, List<SimKit> kits, RuleBot bot, int runs, Localizer localize,
…
            var bridge = new KitBridge(kits[n % kits.Count], content, random, problems);
```

**Rationale:** bag items are deliberately **not** layered over `item_stats` (`ApplyStats`): NER's `CheckPacking` builds bare items, and a packing case should state exactly what the item carries. `MapContext` gets the qualified area id and `game` → 0, as NER does. No `packing:` line when there are no cases keeps the sample output byte-identical.

**Verification:** V1, V2, V7.

**If this fails:** `git checkout -- tools/EfSim/PackRun.cs`.

---

### Step 3: README

**Objective:** C7.
**Confidence:** High
**Depends on:** Step 2
**Do-Projex: Encouraged**

**Files:** `README.md` (section `### ef-sim run`, end of file)

**Changes:**

(a) Checks paragraph:

```markdown
// Before:
options, a run that throws, a decision no rule of the row's bot picks an option for, and every `expect.offered` option id that no run was offered. Load warnings print as `WARN` and

// After:
options, a run that throws, a decision no rule of the row's bot picks an option for, every `expect.offered` option id that no run was offered, and every `expect.packing` case that comes out different (see Packing below). Load warnings print as `WARN` and
```

(b) Insert a new paragraph right after the paragraph ending `` the kit's props winning. `samples/SampleArea/sim.json` is a working example. `` (blank line before and after):

```markdown
A row's `kit` may also be a list of kit names: run `n` of the row (counting from 0) uses `kit[n % length]`, so
`"kit": ["light", "heavy"]` alternates. The table names such a row `light+heavy`.
```

(c) Append at the end of the file (after the Bots errors paragraph, one blank line between):

````markdown
#### Packing

`expect.packing` checks what an area's `packing` says a bag still needs, the way the map does before an expedition:

```json
"expect": {
  "packing": [
    { "area": "ne_sump", "bag": [ { "id": "combat_knife", "tags": ["weapon", "melee"] } ], "missing": [] },
    { "area": "ne_sump", "bag": [ { "id": "handmade_pistol", "tags": ["weapon", "firearm"] } ], "missing": ["ammo for the gun"] },
    { "area": "ne_sump", "missing": ["weapon"] }
  ]
}
```

Each case needs `area` and `missing` (the expected texts in order, as the player reads them; `[]` means ready to go). `bag`
items take `id`, `count`, `tags` and `props` like kit items but are not layered over `item_stats`: the case states everything
the item carries. Leaving `bag` out means no backpack at all (the area's `no_backpack` text); `"bag": []` is an empty
backpack. After the table a `packing: N case(s), M failed` line prints, and each case that comes out different is a `FAIL`
such as `expect.packing[2]: ne_sump should need [ammo for the gun], got []`. Unknown keys, a missing `area` or `missing`, and
an unknown area are errors (exit 2).
````

**Verification:** V11.

**If this fails:** `git checkout -- README.md`.

---

### Step 4: Write `NightExpeditionsReborn/sim.json`

**Objective:** C3, C4.
**Confidence:** High (prototype passed)
**Depends on:** Steps 1–2 built
**Do-Projex: Encouraged**

**Files:** `E:\SteamLibrary\steamapps\common\Probably Stolen\Modding\NightExpeditionsReborn\sim.json` (new; outside the repo; do not touch anything else in NER)

**Changes:** create the file with the Write tool, content exactly as below (LF, trailing newline after the final `}`).

How the data maps to NER `Program.cs`:

| `sim.json` | NER source |
|---|---|
| kits `handmade`, `smg`, `smg-5med`, `shotgun`, `knife`, `smg-nomed` | `Handmade(3,3)`, `Smg(4,4)` (ne_sump rows), `Smg(4,5)` (ne_annex / ne_foundry rows), `Shotgun(4,5)`, `Knife(3)`, `Smg(4,0)` renamed `smg-nomed`; items in `FakeBridge` ctor order; credits 60; `bandage_item` count 0 kept for `smg-nomed` (FakeBridge adds it with Meds = 0) |
| kits `random0`/`random1`/`random2` + rows `"kit": ["random0","random1","random2"]` | random kit at `n%3 == 0/1/2`: mags = meds = 2/3/4, capacity 7, stats 40; no knife on 0; tools ×2 + `small_pistol_ammo_p` and credits 500 on 2 |
| `careful` | iteration-2 plan's `careful` fixture, verbatim |
| `greedy` | `careful` with: rule 1 `when` → `val.scene.combat && player.hp <= 5`; head-home rule `55`→`30`, `70`→`40`, `shots_left <= 2`→`<= 0`, boss-next clause dropped (iteration-2 mapping table, last rows) |
| `runs` order and seeds | `Main` lines 42-62: random ×3, then careful ×10, greedy ×10; seed = `(24 + 1 + len(area)) * 31 + len(policy)` → ne_sump 998/999/998, ne_annex 1029/1030/1029, ne_foundry 1091/1092/1091 (random/careful/greedy); `runs` 2000 = NER's default `SIM_RUNS` |
| `expect.offered` | `expected` array, lines 63-69 (60 ids, same order) |
| `expect.packing` | `CheckPacking` lines 95-104, same 6 bags/order; NER's `Needs(...) != ""` → `"missing": []`; `Missing(area, null)` → no `bag` |

```json
{
  "version": 1,
  "kits": {
    "handmade": {
      "credits": 60,
      "items": [
        { "id": "gun", "tags": ["firearm", "weapon", "in_hand"], "props": { "firepower": 20, "accuracy": 30, "recoil": 65, "ergonomics": 50, "reliability": 30, "capacity": 4 } },
        { "id": "small_pistol_ammo", "count": 3, "tags": ["ammo"], "props": { "kind": 0 } },
        { "id": "bandage_item", "count": 3 },
        { "id": "combat_knife", "tags": ["melee", "weapon", "in_hand"], "props": { "sides": 4 } }
      ]
    },
    "smg": {
      "credits": 60,
      "items": [
        { "id": "gun", "tags": ["firearm", "weapon", "in_hand"], "props": { "firepower": 65, "accuracy": 50, "recoil": 25, "ergonomics": 50, "reliability": 65, "capacity": 8 } },
        { "id": "small_pistol_ammo", "count": 4, "tags": ["ammo"], "props": { "kind": 0 } },
        { "id": "bandage_item", "count": 4 },
        { "id": "combat_knife", "tags": ["melee", "weapon", "in_hand"], "props": { "sides": 4 } }
      ]
    },
    "smg-5med": {
      "credits": 60,
      "items": [
        { "id": "gun", "tags": ["firearm", "weapon", "in_hand"], "props": { "firepower": 65, "accuracy": 50, "recoil": 25, "ergonomics": 50, "reliability": 65, "capacity": 8 } },
        { "id": "small_pistol_ammo", "count": 4, "tags": ["ammo"], "props": { "kind": 0 } },
        { "id": "bandage_item", "count": 5 },
        { "id": "combat_knife", "tags": ["melee", "weapon", "in_hand"], "props": { "sides": 4 } }
      ]
    },
    "smg-nomed": {
      "credits": 60,
      "items": [
        { "id": "gun", "tags": ["firearm", "weapon", "in_hand"], "props": { "firepower": 65, "accuracy": 50, "recoil": 25, "ergonomics": 50, "reliability": 65, "capacity": 8 } },
        { "id": "small_pistol_ammo", "count": 4, "tags": ["ammo"], "props": { "kind": 0 } },
        { "id": "bandage_item", "count": 0 },
        { "id": "combat_knife", "tags": ["melee", "weapon", "in_hand"], "props": { "sides": 4 } }
      ]
    },
    "shotgun": {
      "credits": 60,
      "items": [
        { "id": "gun", "tags": ["firearm", "weapon", "in_hand"], "props": { "firepower": 85, "accuracy": 50, "recoil": 30, "ergonomics": 50, "reliability": 75, "capacity": 4 } },
        { "id": "small_pistol_ammo", "count": 4, "tags": ["ammo"], "props": { "kind": 0 } },
        { "id": "bandage_item", "count": 5 },
        { "id": "combat_knife", "tags": ["melee", "weapon", "in_hand"], "props": { "sides": 4 } }
      ]
    },
    "knife": {
      "credits": 60,
      "items": [
        { "id": "bandage_item", "count": 3 },
        { "id": "combat_knife", "tags": ["melee", "weapon", "in_hand"], "props": { "sides": 4 } }
      ]
    },
    "random0": {
      "credits": 60,
      "items": [
        { "id": "gun", "tags": ["firearm", "weapon", "in_hand"], "props": { "firepower": 40, "accuracy": 40, "recoil": 40, "ergonomics": 50, "reliability": 40, "capacity": 7 } },
        { "id": "small_pistol_ammo", "count": 2, "tags": ["ammo"], "props": { "kind": 0 } },
        { "id": "bandage_item", "count": 2 }
      ]
    },
    "random1": {
      "credits": 60,
      "items": [
        { "id": "gun", "tags": ["firearm", "weapon", "in_hand"], "props": { "firepower": 40, "accuracy": 40, "recoil": 40, "ergonomics": 50, "reliability": 40, "capacity": 7 } },
        { "id": "small_pistol_ammo", "count": 3, "tags": ["ammo"], "props": { "kind": 0 } },
        { "id": "bandage_item", "count": 3 },
        { "id": "combat_knife", "tags": ["melee", "weapon", "in_hand"], "props": { "sides": 4 } }
      ]
    },
    "random2": {
      "credits": 500,
      "items": [
        { "id": "gun", "tags": ["firearm", "weapon", "in_hand"], "props": { "firepower": 40, "accuracy": 40, "recoil": 40, "ergonomics": 50, "reliability": 40, "capacity": 7 } },
        { "id": "small_pistol_ammo", "count": 4, "tags": ["ammo"], "props": { "kind": 0 } },
        { "id": "bandage_item", "count": 4 },
        { "id": "combat_knife", "tags": ["melee", "weapon", "in_hand"], "props": { "sides": 4 } },
        { "id": "flashlight", "count": 2 },
        { "id": "crowbar", "count": 2 },
        { "id": "wire_cutter", "count": 2 },
        { "id": "metal_scanner", "count": 2 },
        { "id": "sec_keycard", "count": 2 },
        { "id": "cup_noodle", "count": 2 },
        { "id": "packet_red_cigarette", "count": 2 },
        { "id": "flashbang_grenade", "count": 2 },
        { "id": "smoke_grenade", "count": 2 },
        { "id": "c4", "count": 2 },
        { "id": "bottled_water", "count": 2 },
        { "id": "green_injector", "count": 2 },
        { "id": "lottery_ticket", "count": 2 },
        { "id": "small_pistol_ammo_p", "tags": ["ammo", "armor_piercing"], "props": { "kind": 1 } }
      ]
    }
  },
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
    ],
    "greedy": [
      { "when": "player.hp * 100 <= player.max_hp * 45 || (player.bleeding > 0 && player.hp * 10 <= player.max_hp * 7)", "pick": ["heal"] },
      { "when": "val.scene.combat && player.hp <= 5", "pick": ["smoke_run", "flee"] },
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
      { "when": "!val.offered.press_on || player.hp * 100 <= player.max_hp * 30 || (!val.offered.heal && player.hp * 100 < player.max_hp * 40) || (val.has_gun && val.shots_left <= 0)", "pick": ["head_home"] },
      { "pick": ["press_on"] }
    ]
  },
  "runs": [
    { "area": "ne_sump", "kit": ["random0", "random1", "random2"], "bot": "random", "runs": 2000, "seed": 998 },
    { "area": "ne_annex", "kit": ["random0", "random1", "random2"], "bot": "random", "runs": 2000, "seed": 1029 },
    { "area": "ne_foundry", "kit": ["random0", "random1", "random2"], "bot": "random", "runs": 2000, "seed": 1091 },
    { "area": "ne_sump", "kit": "handmade", "bot": "careful", "runs": 2000, "seed": 999 },
    { "area": "ne_sump", "kit": "smg", "bot": "careful", "runs": 2000, "seed": 999 },
    { "area": "ne_annex", "kit": "handmade", "bot": "careful", "runs": 2000, "seed": 1030 },
    { "area": "ne_annex", "kit": "smg-5med", "bot": "careful", "runs": 2000, "seed": 1030 },
    { "area": "ne_annex", "kit": "shotgun", "bot": "careful", "runs": 2000, "seed": 1030 },
    { "area": "ne_foundry", "kit": "smg-5med", "bot": "careful", "runs": 2000, "seed": 1092 },
    { "area": "ne_foundry", "kit": "shotgun", "bot": "careful", "runs": 2000, "seed": 1092 },
    { "area": "ne_sump", "kit": "knife", "bot": "careful", "runs": 2000, "seed": 999 },
    { "area": "ne_annex", "kit": "knife", "bot": "careful", "runs": 2000, "seed": 1030 },
    { "area": "ne_sump", "kit": "smg-nomed", "bot": "careful", "runs": 2000, "seed": 999 },
    { "area": "ne_sump", "kit": "handmade", "bot": "greedy", "runs": 2000, "seed": 998 },
    { "area": "ne_sump", "kit": "smg", "bot": "greedy", "runs": 2000, "seed": 998 },
    { "area": "ne_annex", "kit": "handmade", "bot": "greedy", "runs": 2000, "seed": 1029 },
    { "area": "ne_annex", "kit": "smg-5med", "bot": "greedy", "runs": 2000, "seed": 1029 },
    { "area": "ne_annex", "kit": "shotgun", "bot": "greedy", "runs": 2000, "seed": 1029 },
    { "area": "ne_foundry", "kit": "smg-5med", "bot": "greedy", "runs": 2000, "seed": 1091 },
    { "area": "ne_foundry", "kit": "shotgun", "bot": "greedy", "runs": 2000, "seed": 1091 },
    { "area": "ne_sump", "kit": "knife", "bot": "greedy", "runs": 2000, "seed": 998 },
    { "area": "ne_annex", "kit": "knife", "bot": "greedy", "runs": 2000, "seed": 1029 },
    { "area": "ne_sump", "kit": "smg-nomed", "bot": "greedy", "runs": 2000, "seed": 998 }
  ],
  "expect": {
    "offered": [
      "press_on", "search", "head_home", "reload", "heal", "shoot", "melee", "gun_butt", "aim", "cover", "fall_back", "flash", "flashbang", "switch_target",
      "smoke_run", "flee", "read_boss", "weak_point", "ambush", "sneak", "bribe", "feed", "smokes", "bait", "smoke_slip", "hand_back", "spare", "finish", "take",
      "give", "food", "rob", "pry", "cut", "pick", "force", "wait", "smoke", "light", "feel", "scan", "dive", "card", "blow", "wire", "shove", "dash", "valve",
      "stim", "test", "wade", "clean", "back", "pull10", "pull25", "pull50", "ticket", "counter", "move_on", "antitoxin"
    ],
    "packing": [
      { "area": "ne_sump", "bag": [ { "id": "combat_knife", "tags": ["weapon", "melee"] } ], "missing": [] },
      { "area": "ne_sump", "bag": [ { "id": "handmade_pistol", "tags": ["weapon", "firearm", "has_rounds"] } ], "missing": [] },
      { "area": "ne_sump", "bag": [ { "id": "handmade_pistol", "tags": ["weapon", "firearm"] } ], "missing": ["ammo for the gun"] },
      { "area": "ne_sump", "bag": [ { "id": "handmade_pistol", "tags": ["weapon", "firearm"] }, { "id": "combat_knife", "tags": ["weapon", "melee"] } ], "missing": [] },
      { "area": "ne_sump", "bag": [ { "id": "bandage_item", "tags": ["medicine"] } ], "missing": ["weapon", "ammo for the gun"] },
      { "area": "ne_sump", "missing": ["weapon"] }
    ]
  }
}
```

**Rationale:** `runs` 2000 per row: the rarest expected id (`antitoxin`) first appears between 300 and 600 runs per row in the prototype, so 2000 leaves >3× margin, and seeded rows make the pass deterministic, not a chance outcome. Kit names differ from NER's printed names only where NER reuses one name for two different kits (`smg` 4 vs 5 meds).

**Verification:** V3, V4, V5, V9, V14.

**If this fails:** delete only `NightExpeditionsReborn/sim.json` (it is new). If V4 fails only on the output hash while exit/rows/FAIL counts hold, diff `$T/ner.txt` against "Expected NER output" below and report the differing lines in the patch doc instead of changing data to match.

---

### Step 5: Commit (framework only)

**Objective:** land Steps 1–3 on the sprint branch.
**Confidence:** High
**Depends on:** V1–V11 green

```bash
git add tools/EfSim/SimConfig.cs tools/EfSim/PackRun.cs README.md
git commit -m "feat(ef-sim): kit lists per row and expect.packing cases" -m "Rows may cycle a list of kits by run index; expect.packing checks MapContext.Missing per case. NightExpeditionsReborn/sim.json (outside this repo) uses both." -m "Co-Authored-By: Claude Sonnet <noreply@anthropic.com>"
git status --porcelain
```

`git status --porcelain` must be empty (bin/obj are ignored). Never `--amend`, never commit in NER.

**If this fails:** fix the cause and make a new commit; do not amend.

---

## Expected NER output

`ef run ../../../NightExpeditionsReborn` (prototype, default runs; `\r` stripped). sha256 `ba138a669a45404b88ac9680d636feb1033b88f50aac93d45c1ecbbbdfdae996`.

```text
area               kit          bot        runs  cleared  knocked out  extracted
ne_sump            random0+random1+random2 random     2000    18.3%        67.9%      32.2%
ne_annex           random0+random1+random2 random     2000     8.7%        79.1%      20.9%
ne_foundry         random0+random1+random2 random     2000     7.0%        80.0%      20.0%
ne_sump            handmade     careful    2000    36.4%         7.9%      92.1%
ne_sump            smg          careful    2000    88.0%         2.8%      97.2%
ne_annex           handmade     careful    2000     3.1%        19.7%      80.4%
ne_annex           smg-5med     careful    2000    33.7%        16.2%      83.8%
ne_annex           shotgun      careful    2000    38.0%        15.7%      84.3%
ne_foundry         smg-5med     careful    2000    27.6%        22.8%      77.3%
ne_foundry         shotgun      careful    2000    33.7%        18.5%      81.6%
ne_sump            knife        careful    2000    11.1%        13.5%      86.5%
ne_annex           knife        careful    2000     4.2%        12.0%      88.1%
ne_sump            smg-nomed    careful    2000    84.6%         4.1%      96.0%
ne_sump            handmade     greedy     2000    53.1%        27.2%      72.9%
ne_sump            smg          greedy     2000    94.3%         4.6%      95.5%
ne_annex           handmade     greedy     2000     4.1%        49.7%      50.4%
ne_annex           smg-5med     greedy     2000    42.6%        39.9%      60.2%
ne_annex           shotgun      greedy     2000    52.7%        32.6%      67.5%
ne_foundry         smg-5med     greedy     2000    35.5%        44.5%      55.5%
ne_foundry         shotgun      greedy     2000    48.8%        36.1%      64.0%
ne_sump            knife        greedy     2000    20.7%        46.4%      53.7%
ne_annex           knife        greedy     2000     7.6%        52.4%      47.6%
ne_sump            smg-nomed    greedy     2000    89.8%         6.0%      94.0%
WARN sim.json: bots.careful[3] never picked an option
WARN sim.json: bots.careful[4] never picked an option
WARN sim.json: bots.careful[8] never picked an option
WARN sim.json: bots.careful[9] never picked an option
WARN sim.json: bots.careful[15] never picked an option
WARN sim.json: bots.careful[19] never picked an option
WARN sim.json: bots.greedy[3] never picked an option
WARN sim.json: bots.greedy[4] never picked an option
WARN sim.json: bots.greedy[8] never picked an option
WARN sim.json: bots.greedy[9] never picked an option
WARN sim.json: bots.greedy[15] never picked an option
WARN sim.json: bots.greedy[19] never picked an option
packing: 6 case(s), 0 failed
ef-sim night_expeditions_reborn: all checks passed
```

The 12 `WARN … never picked` lines are expected (NER's matrix kits carry no flashbang, food or cigarettes, and some event branches never arise for these bots); they are not failures. Balance numbers are not compared with NER's C# sim in this iteration.

---

## Verification Plan

### Setup (Git Bash, cwd = work-root; reuse `$T` and `ef` from Step 0)

```bash
NER=../../../NightExpeditionsReborn
DOTNET_CLI_UI_LANGUAGE=en dotnet build -c Release --no-incremental tools/EfSim/EfSim.csproj -warnaserror 2>&1 | grep -E '[0-9]+ (Warning|Error)\(s\)'
ef() { dotnet run -c Release --no-build --project tools/EfSim -- "$@" | tr -d '\r'; return ${PIPESTATUS[0]}; }
mkdir -p "$T/N0"; cp -r "$NER/ExpeditionFramework" "$NER/Localization" "$NER/manifest.xml" "$NER/sim.json" "$T/N0/"
bad() { rm -rf "$T/N"; cp -r "$T/N0" "$T/N"; sed -i "$1" "$T/N/sim.json"; ef run "$T/N" --runs 5 | grep -E '^ERROR|^FAIL expect|^packing:|^ef-sim'; echo "exit=${PIPESTATUS[0]}"; }
```

V6 probes (run each line; `R0` is the first random row's tail, `C3` the first careful row's):

```bash
R0='"random0", "random1", "random2"\], "bot": "random", "runs": 2000, "seed": 998'
C3='"kit": "handmade", "bot": "careful", "runs": 2000, "seed": 999'
P5='{ "area": "ne_sump", "missing": \["weapon"\] }'
bad "s/$R0/\"random0\", \"random9\"], \"bot\": \"random\", \"runs\": 2000, \"seed\": 998/"
bad "s/$R0/], \"bot\": \"random\", \"runs\": 2000, \"seed\": 998/"
bad "s/$R0/\"random0\", 3], \"bot\": \"random\", \"runs\": 2000, \"seed\": 998/"
bad "s/$C3/\"kit\": 5, \"bot\": \"careful\", \"runs\": 2000, \"seed\": 999/"
bad "s/$P5/{ \"area\": \"ne_sump\", \"missing\": [\"weapon\"], \"note\": 1 }/"
bad "s/$P5/{ \"area\": \"ne_sump\" }/"
bad "s/$P5/{ \"area\": \"ne_attic\", \"missing\": [\"weapon\"] }/"
bad "s/$P5/{ \"area\": \"ne_sump\", \"bag\": [ { \"name\": \"x\" } ], \"missing\": [\"weapon\"] }/"
```

Expected, in order (each followed by `exit=2`):
1. `ERROR sim.json: runs[0].kit: no kit named random9`
2. `ERROR sim.json: runs[0].kit is empty`
3. `ERROR sim.json: runs[0].kit must be text`
4. `ERROR sim.json: runs[3].kit must be a kit name or a list of kit names`
5. `ERROR sim.json: expect.packing[5]: unknown key note`
6. `ERROR sim.json: expect.packing[5]: missing missing`
7. `ERROR sim.json: no area night_expeditions_reborn:ne_attic`
8. `ERROR sim.json: expect.packing[5].bag[0]: unknown key name`

V7 probes:

```bash
bad 's/"missing": \["ammo for the gun"\] }/"missing": ["weapon"] }/'
bad "s/$P5/{ \"area\": \"ne_sump\", \"bag\": [], \"missing\": [\"weapon\"] }/"
```

Expected: (1) `packing: 6 case(s), 1 failed`, `FAIL expect.packing[2]: ne_sump should need [weapon], got [ammo for the gun]`, a line matching `ef-sim night_expeditions_reborn: [0-9]+ check\(s\) failed`, `exit=1`; (2) same shape with `FAIL expect.packing[5]: ne_sump should need [weapon], got [weapon, ammo for the gun]`, `exit=1` (proves empty bag ≠ no backpack). Other FAIL lines at `--runs 5` are never-offered noise and are filtered out.

### Deterministic Suite

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V1 | C1, C2 / S1–2 | Setup's build line | `0 Warning(s)` and `0 Error(s)` | compile errors, warnings |
| V2 | C5 / S2 | `ef run samples/SampleArea > "$T/after.txt"; echo "exit=$?"; cmp "$T/before.txt" "$T/after.txt" && echo SAME` | `exit=0`; `SAME` | single-kit rows or packing line changing sample output |
| V3 | C3 / S4 | `tr -d '\r' < "$NER/sim.json" \| sha256sum` | `36c3c1603d7542d020ea2b21d4f2448421ed2a904d016bb9de23cf48e3360d82` | NER data differs from plan |
| V4 | C4 / S4 | `ef run "$NER" > "$T/ner.txt"; echo "exit=$?"; grep -cE '^ne_(sump\|annex\|foundry) ' "$T/ner.txt"; grep -c '^packing: 6 case(s), 0 failed$' "$T/ner.txt"; grep -cE '^(FAIL\|ERROR)' "$T/ner.txt"; tail -1 "$T/ner.txt"; sha256sum "$T/ner.txt"` | `exit=0`; `23`; `1`; `0`; `ef-sim night_expeditions_reborn: all checks passed`; `ba138a669a45404b88ac9680d636feb1033b88f50aac93d45c1ecbbbdfdae996` | kit cycling, packing, bots or matrix wrong; a check failing on NER |
| V5 | C4 | `ef run "$NER" > "$T/ner2.txt"; cmp "$T/ner.txt" "$T/ner2.txt" && echo IDENTICAL` | `IDENTICAL` | nondeterminism |
| V6 | C1, C2 | V6 probes above | the 8 listed lines, each `exit=2` | lax parsing, unlocated errors |
| V7 | C2 | V7 probes above | as listed, `exit=1` | packing mismatch passing; null vs empty bag conflated |
| V8 | C6 | `dotnet run -c Release --project tests/Sim \| tr -d '\r' \| tail -1; echo "exit=${PIPESTATUS[0]}"` | `All checks passed`; `exit=0` | framework sim regression |
| V9 | C6 | `ef lint samples/SampleArea --strict \| tail -1; ef lint "$NER" --strict \| tail -1` | `lint sample_area: 0 error(s), 0 warning(s)`; `lint night_expeditions_reborn: 0 error(s), 0 warning(s)` | lint regression |
| V10 | C6 | `S=$(cygpath -m "$(mktemp -d)"); DOTNET_CLI_UI_LANGUAGE=en dotnet build -c Release --no-incremental ExpeditionFramework.csproj "-p:GameManaged=E:/SteamLibrary/steamapps/common/Probably Stolen/Probably Stolen_Data/Managed/" "-p:InstallDir=$S/" 2>&1 \| grep -E '[0-9]+ (Warning\|Error)\(s\)'` | `0 Warning(s)` and `0 Error(s)` (never deploys to live `Mods/`) | tool code leaking into game assembly |
| V11 | C7 | `for k in '#### Packing' 'expect.packing' 'kit[n % length]' 'no_backpack' 'not layered over' 'packing: N case(s)'; do printf '%s ' "$(grep -cF "$k" README.md)"; done; echo` | six numbers each ≥ `1` | docs missing |
| V12 | hygiene | after Step 5: `git status --porcelain` | empty | stray files |
| V13 | scope | `P=$(git log --diff-filter=A --format=%H -- .projex/2611121110-ef-sim-ner-pack-plan.md); git diff --name-only "$P"..HEAD \| sort` | exactly `README.md`, `tools/EfSim/PackRun.cs`, `tools/EfSim/SimConfig.cs` (plus the patch doc under `.projex/` if already committed) | engine, bridge, sample or test files touched |
| V14 | C3 | `sha256sum "$NER/tests/Sim/Program.cs" "$NER/tests/Sim/Sim.csproj"` | Step 0 hashes (`d4732e8e…`, `26ca5458…`) | NER C# sim edited |

Table commands: `\|` is a markdown escape — type a plain `|`.

### Judgment Checks

| Check | Covers | Why not deterministic |
|-------|--------|-----------------------|
| `greedy` differs from `careful` in exactly the four places the mapping table lists, and kits match `FakeBridge` item-for-item | C3 | semantic equivalence to C# `Plan`/`FakeBridge`; V3 pins the bytes the planner checked, not their meaning |

### Acceptance Criteria Validation

| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| C1 kit lists | V1, V4, V6 | builds; random rows run; bad forms exit 2 with located errors |
| C2 packing | V4, V6, V7 | 6 cases pass on NER; mismatch fails; bad cases exit 2 |
| C3 NER file | V3, V14, judgment | hash matches; NER C# sim untouched |
| C4 NER run | V4, V5 | exit 0, exact output, deterministic |
| C5 sample unchanged | V2 | `SAME` |
| C6 green | V8, V9, V10 | pass / 0-0 / 0-0 |
| C7 docs | V11 | all ≥ 1 |

---

## Rollback Plan

1. `git revert <code commit>` on the sprint branch (or `git checkout -- tools/EfSim README.md` before committing).
2. Delete `NightExpeditionsReborn/sim.json` (new file; nothing else in NER was touched).

---

## Notes

### Risks

- NER data edited by someone between planning and execution → V4 hash differs. Mitigation: Step 4 "If this fails" — report, don't tune data.
- `--runs` below ~600 makes `antitoxin` a never-offered miss on NER (prototype: fails at 100 and 300, passes at 600). Documented behaviour (README already warns small counts can miss rare options); the committed `sim.json` uses 2000.

### Decisions Taken by Default

- `missing` is a list of texts (compared joined with `", "`), not one pre-joined string; `[]` = ready.
- `bag` omitted = no backpack (null bag); `"bag": []` = empty backpack (different result — V7 proves it).
- Bag items skip `item_stats` layering (matches NER `CheckPacking`).
- Row label for kit lists is `a+b+c`; the 12-wide column overflows for NER's random rows (cosmetic only).
- `packing:` summary line prints only when cases exist (keeps sample output identical).
- NER kit names: `smg` (4 mags, 4 meds; ne_sump), `smg-5med` (4 mags, 5 meds; ne_annex/ne_foundry), `random0/1/2` (by `n%3`).
- `smg-nomed` keeps a `bandage_item` with count 0, as `FakeBridge` does.
- No `KitBridge` knob added: every check passes with the current bridge (objective's exception not needed).

### Open Questions

- [ ] (none)

### Split Decision

`No split — heuristic tripped (>500 lines, 6 steps) but steps tightly coupled (rationale: the NER file is the proof for both reader features, and most length is the embedded data and expected output, not steps).`
