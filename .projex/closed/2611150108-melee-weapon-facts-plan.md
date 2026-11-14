# Weapon Facts From the Game (`cells`, `reach`, `breadth`, `price`, `kinds`, `purposes`) + Melee Measurement Command

> **Status:** Complete
> **Author:** Claude Opus 5.5 (plan step)
> **Parent:** User
> **Source:** Direct request (human goal 2026-11-15: NER unified melee model, option "Opt1": EF reports raw melee facts, NER writes formulas in data)
> **Completed:** 2026-11-15
> **Walkthrough:** 2611150108-melee-weapon-facts-walkthrough.md
> **Log:** 2611150108-melee-weapon-facts-log.md
> **Related Projex:** 2611142108-sim-tally-and-intel-cap-plan.md (EF; parity-proof conventions reused) | a later NER plan (not yet written) consumes these facts: formula, curated overrides, fit
> **Worktree:** Yes — repo clean (`main` @ `a985df6`), no `projex/*` branches, but the NER sprint is running `ef-sim` from the EF main checkout right now (`tools/EfSim/bin/Release/net8.0/ef-sim.exe` was locked at plan time, so `dotnet run` there fails with MSB3027). Editing `src/Core` in the main checkout would also change the `ef-sim` the sprint is using. A worktree has its own `bin/`, so neither problem arises. The csproj's relative `GameManaged` path breaks in a worktree, so the compile check passes it explicitly (Step 5).

---

## Summary

EF reports six whole-number facts on every **weapon** item (firearm or melee, i.e. whatever `ItemReader.ReadWeapon` reads), read from the game the way `ReadGun` reads `firepower`:

| Prop | Definition | Source in game |
|---|---|---|
| `cells` | filled cells of the item's own grid shape | `GameItem.GetItemSize()` (base `shape`; attachments excluded) |
| `reach` | longer side of the smallest box around the filled cells | `shape` + `GridShape.GetLocal`, via new `Core.Footprint.Measure` |
| `breadth` | shorter side of that box; 0 with no filled cell | same |
| `price` | the game's base price, clamped to 0..int.MaxValue | `GameItem.unitValue` (not `GetValue()`) |
| `kinds` | number of game item types other than `WEAPON`, `MELEE_WEAPON`, `FIREARM` | `GameItem.GetGameItemType()` |
| `purposes` | number of the game's **major** types other than `WEAPON` (what else the item is *for*) | `TypeHelper.MAJOR_TYPES_LIST` ∩ `GetGameItemType()` |

Plus a debug-panel entry **"Expeditions: log melee weapon facts"**. It logs one line per melee weapon the game can make, so the NER fit has real data. EF's `main.json` melee table (`sides`, `bonus`, `stuns`, `#melee` `sides` 4) is unchanged. No pack reads the new props, so NER lint, `run` and `--digest` at `--runs 2000` stay byte-identical.

**Scope:** EF only. `src/Core` (new `Footprint.cs`), `src/Game` (`ItemReader.cs`, `DevTools.cs`, `PackRegistry.cs`), `tests/Sim/Program.cs`, `docs/` (`items.md`, `testing.md`, `packs.md`). Out of scope: NER formulas, overrides, sim kit props, the fit, and deploying.
**Estimated Changes:** 1 new Core file (~30 lines), 3 Game files (~70 lines), 1 test (~20 lines), 3 docs (~20 lines).

---

## Objective

### Problem / Gap / Need
NER's melee strength comes from EF's hand-written `item_stats` table (`sides` / `bonus` / `stuns` per id). The human wants a unified model: bigger is better, longer reach is better, price is a small nudge, and a pure weapon beats a multi-use item, with optional curated overrides. NER can write that as `values` formulas only if EF hands it the raw facts. Today EF reports nothing about shape, price or kinds, and nobody has measured the real shapes. They come from sprite alpha at runtime, so the decompile can't tell them.

### Success Criteria
- [ ] SC1 — `ItemReader.ReadWeapon` sets `cells`, `reach`, `breadth`, `price`, `kinds`, `purposes` on every weapon, **after** `ApplyStats` (game wins, as for gun stats).
- [ ] SC2 — `Core.Footprint.Measure` is game-free and covered by `TestFootprint` in `tests/Sim` (straight, vertical, diagonal, padded grid, empty); `tests/Sim` ends `All checks passed`.
- [ ] SC3 — The debug panel gets one framework-wide entry, "Expeditions: log melee weapon facts". It logs `Melee facts: id=… cells=… reach=… breadth=… price=… kinds=… purposes=… width=… height=… shape=… types=… name=…` per melee weapon, using the same `ReadFacts` code a run uses.
- [ ] SC4 — The EF csproj compiles with output redirected to scratch (exit 0); nothing is written into the game folder.
- [ ] SC5 — NER (`<ner>`) lint `--strict`, `run --runs 2000` and `run --runs 2000 --digest` are byte-identical between the base `ef-sim` and the new one, run back to back on the same NER state.
- [ ] SC6 — `ExpeditionFramework/main.json` is unchanged.
- [ ] SC7 — The docs are updated: `items.md` § What the game reports names all six facts and their definitions; `testing.md` § Kits says to give kit weapons these as props; `packs.md` § Debug panel names the new entry.
- [ ] SC8 — Source hygiene (SKILL.md): no projex ids, plan labels, step numbers or line numbers in the new comments, and every non-obvious choice names the alternative it rejects.

### Out of Scope
- The NER formula, overrides and fitting, and NER sim kits with the new props (a later NER plan).
- Facts on non-weapon stacks (YAGNI: a stack's shape and price are per unit; add them when something needs it).
- Expression support inside `item_stats` (NER computes its own values from the facts).
- Lint warnings for `item_stats` keys that shadow fact names (none exist in any pack today; see Risks).
- Deploying to the game and running the measurement. That is the human's step, after close (§ Post-Close Handoff).

---

## Context

### Current State
- `src/Game/ItemReader.cs`: `ReadWeapon` builds a `RunItem` with tags `weapon`, `in_hand`, `firearm|melee`, `has_rounds`. It then runs `item.ApplyStats(content)` and, for guns, `ReadGun`, which does `item.Set("firepower", …)` and so on **after** stats, so the game's numbers win. `IsMelee` = game type `MELEE_WEAPON` **or** the `item_stats` entry for that id has tag `melee` (that is how `stun_gun`, which is not `MELEE_WEAPON` in the game, counts as melee).
- `RunItem.Props` is `Dictionary<string,int>`; `item.NAME` in expressions → `Expr.ReadItem` → `IItem.Get(name)` → 0 when absent. **Lint does not check item prop names**: only `#tag` filters are checked, against `KnownItemTags` = `Schema.EngineItemTags` + groups + `item_stats` tags (`PackLoader.Items`). So new props need no Schema or lint change.
- `ef-sim`: `KitBridge` builds kit items as `ApplyStats(content)` then `kit props win` (`SimConfig` accepts any `props` key with whole numbers). Sim parity for the new facts already exists: a kit gives them as props. EfSim and KitBridge need no change.
- Name clash check (plan time): `cells|reach|breadth|price|kinds|purposes` appear in no `item_stats` key and no `item.`/`with.` read in EF `ExpeditionFramework/`, `samples/`, `docs/examples/`, or the NER worktree's `ExpeditionFramework/` and `sim.json` (one hit is an English comment in NER `events.json`).
- `main.json` `item_stats` lines 6 and 21–33: `#melee` `sides` 4; per-id `sides`/`bonus`/`stuns` for 13 items. Stays as is.
- Debug panel: EF's `Game.DevTools.Add(content, pack)` adds per-pack entries through `ModHelper.AddDebugFunction(ModEntry.ModId, ModHelper.DEBUG_CATEGORY_MODDING, name, action)`. `PackRegistry.Stop` → `DevTools.RemoveAll()` removes every EF entry. The game panel (F9) needs `dev.txt` next to the exe. It exists (0 bytes), and the last `Player.log` shows `[DevTools] Developer tools enabled: F9 debug panel and F8 console.`
- Logging: `ModEntry.Log.Log(x)` → `UnityEngine.Debug.Log("[Expedition Framework]: " + x)` → `Player.log` (confirmed: `[Expedition Framework]: Expedition packs: …` is in the current log).

**Game facts (decompile, verified):**
- `GameItem.shape : GridShape` (`width`, `height`, `GetLocal(x,y)`, plus `orientation`/`flipped`, which are separate from local width/height). `BuildShapeFromSprite`: grid = `sprite.rect / gridSize` (each at least 1); a cell is filled if the alpha at that cell's sample > 0; row `i = 0` is the sprite's **top**. Width/height are the **slice** size, not trimmed, so empty edge rows or columns are possible. Hence the bounding box.
- `GetItemSize()` counts `shape.GetLocal == 1`; `GetModifiedItemSize()` uses `modifiedShape` (attachments). We use the base `shape`.
- `unitValue` is set in each factory (melee: shiv 8, skinning knife 100, combat knife 75, box cutter 28, cleaver 65, kitchen knife 45, machete 185, crowbar 75, hatchet 55, pipe 15, stun baton 165; tazer 100, stun gun 250 (`SetValue`); wrench 30). `GetValue()` = `(unitValue + power/amenity/armor/liquid/organ mods) × not-purchased 1.1 / vendor 0.9 × vendor discount × accepts-payment`, then `× unitCount`. That is context-dependent, so it is rejected. At runtime `unitValue` changes only through aging (`AgableHelper.OnAge`, `AGE_INT` items), machine/container upgrades, and module tiers. None of these applies to melee weapons. Condition does not touch it.
- Game types (`SetGameItemType`): shiv WEAPON MELEE_WEAPON MAKESHIFT JUNK; skinning knife, combat knife, machete WEAPON MELEE_WEAPON; box cutter + TOOL HOUSEHOLD_GOOD; cleaver, kitchen knife + HOUSEHOLD_GOOD; crowbar, hatchet + TOOL; pipe + MAKESHIFT JUNK; stun baton MELEE_WEAPON WEAPON; tazer (`GunsItemDirectory`) WEAPON MELEE_WEAPON; stun gun (`GunsItemDirectory`) WEAPON MIDLINE (**not** MELEE_WEAPON); wrench_orange (`UnusedDirectory`) TOOL WEAPON MELEE_WEAPON. `TypeHelper.MAJOR_TYPES_LIST` = FOOD MEDICAL MATERIAL HOUSEHOLD_GOOD SUBSTANCE WEAPON WATER ARMOR TOOL MODULE AMMUNITION. MAKESHIFT, JUNK and MIDLINE are grades, not uses. That is why `purposes` exists beside `kinds`.
- Predicted `kinds/purposes`: shiv 2/0, pipe 2/0, box cutter 2/2, cleaver 1/1, kitchen knife 1/1, crowbar 1/1, hatchet 1/1, wrench 1/1, stun gun 1/0, the rest 0/0. The measurement confirms or corrects these.
- Directories are `MonoBehaviour`s registered in `Awake`. `DirectoryMaster.GetDictionaryClassNameList<GameItem>()` lists the loaded ones, `GetIdentifierList<GameItem>(className)` lists one directory's ids, and `Create<GameItem>(id)` runs the raw factory (no unique id, no ownership, no permit hook, unlike `DirectoryMaster.Item`). Whether `UnusedDirectory` is in the scene is **unknown**. The command logs it if it is not.

### Key Files

| File | Role | Change Summary |
|---|---|---|
| `src/Core/Footprint.cs` (new) | Game-free box measurement | `Footprint.Measure(width, height, filled)` → `(Reach, Breadth)` |
| `src/Game/ItemReader.cs` | Game item → `RunItem` | `ReadFacts` (+ `FactNames`, `Describe`), called from `ReadWeapon` after `ApplyStats` |
| `src/Game/DevTools.cs` | EF debug-panel entries | `AddFramework(content)` + `LogMeleeFacts` |
| `src/Game/PackRegistry.cs` | Loads packs, adds entries | Calls `DevTools.AddFramework(content)` once after the pack loop |
| `tests/Sim/Program.cs` | Core tests | `TestFootprint()` + call in `Main` |
| `docs/items.md` | § What the game reports | Weapon facts paragraph |
| `docs/testing.md` | § Kits | Kit weapons take the facts as props |
| `docs/packs.md` | § Debug panel | New entry |

### Dependencies
- **Requires:** this plan committed to EF `main`; a worktree off it.
- **Blocks:** the NER melee-model plan (formula, overrides, fit) and the human's one measurement launch.

### Constraints
- Never build the EF csproj into the live game. Only `-p:InstallDir=<scratch>/game-install/ -o <scratch>/ef-build` (plus `-p:GameManaged=…`, a read-only reference path). `ef-sim` and `tests/Sim` builds are fine.
- NER `<ner>` is read-only and mid-sprint. Never edit it. Run sims one at a time, and compare base and new back to back.
- Source hygiene per SKILL.md. Commits: message with body, ending `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; commit lint rejects bare labels such as "SC1" or "V6" in messages, so spell them out. The hook blocks `--amend`, branch deletion and `stash drop`; never bypass it.
- The local `dotnet` prints build messages in Chinese, so judge builds by **exit code**, not by English text.

### Placeholders (Git Bash)

| Name | Value |
|---|---|
| `<repo-root>` | EF main checkout (`Modding/ExpeditionFramework`) |
| `<work-root>` | EF worktree created by execute-projex |
| `<ner>` | `Modding/NightExpeditionsReborn/.projexwt/2611142200-night-set-pieces` (absolute) |
| `<game>` | game root (contains `Probably Stolen.exe`, `Modding/`) |
| `<scratch>` | orchestrator-supplied scratch dir for this work (`…/scratchpad/melee`) |
| `<base>` | the commit `<work-root>` was cut from (`git -C <work-root> rev-parse HEAD` before any edit) |

Shell setup used by the suite: `W=<work-root>; S=<scratch>; NER=<ner>; GAME=<game>; SIMB="dotnet run -c Release --no-build --project $S/ef-base/tools/EfSim --"; SIMN="dotnet run -c Release --no-build --project $W/tools/EfSim --"`.

### Assumptions
- A1: `TypeHelper.MAJOR_TYPES_LIST`, `GameItem.shape`, `unitValue`, `GetItemSize`, `GetGameItemType`, `DirectoryMaster.GetDictionaryClassNameList/GetIdentifierList/Create` are public in the shipped `Assembly-CSharp` (they are in the decompile). Step 5 compile proves it.
- A2: `DirectoryMaster.Create<GameItem>(id)` on a weapon id has no lasting side effect beyond an unparented object that gets garbage-collected. Not provable offline. The command is dev-only and human-triggered, and each id is in its own try/catch.
- A3: `GetLocal` row 0 is the sprite's top (from `BuildShapeFromSprite`). This only affects how the logged `shape=` reads; the facts are orientation-free.
- A4: `ef-sim` output has no timestamps or paths that differ between two binaries (held in the sim-tally plan's proofs).

### Impact Analysis
- **Direct:** the files above.
- **Adjacent:** every pack's weapon items gain six props in game. Reads of other prop names don't change. A pack `item_stats` key named like a fact is overwritten in game (none exist).
- **Downstream:** NER can read `item.cells` etc. In `ef-sim` a kit weapon reads 0 unless the kit (or `item_stats`) gives the prop. The NER plan must give its kit weapons these props.

---

## Implementation

### Overview
A pure Core helper does the measurement, so it can be tested. `ItemReader.ReadFacts` is the one place that reads facts, used both by runs and by the debug command, so the measured data is exactly what formulas will see. The debug entry is framework-wide and lives with the existing per-pack entries. Then docs, the compile check, and the parity proof.

### Step 0: Preflight + Baselines

**Objective:** Prove the start state and capture the base `ef-sim` outputs before any edit.
**Confidence:** High
**Depends on:** None

**Files:** none (scratch only)

**Changes:**
```bash
git -C "$W" rev-parse HEAD                     # record as <base>
git -C "$W" diff --stat a985df6 HEAD -- src api tools tests docs ExpeditionFramework   # expect empty (V0a)
git -C "$NER" rev-parse HEAD > "$S/ner-head-start.txt"
git -C "$NER" status --porcelain | sha1sum > "$S/ner-status-start.txt"
stat -c %Y "$GAME/Mods/ExpeditionFramework/ExpeditionFramework.dll" > "$S/game-dll-mtime.txt"   # V2a reference
mkdir -p "$S/ef-base" "$S/base" "$S/new"
git -C "$W" archive <base> | tar -x -C "$S/ef-base"
dotnet build -c Release "$S/ef-base/tools/EfSim"; echo "exit $?"
dotnet build -c Release "$W/tools/EfSim"; echo "exit $?"
(cd "$W" && dotnet run -c Release --project tests/Sim | tail -n 1)   # V0c
```
`ef-base` is a plain copy of the base tree, with its own `bin/`, so the base `ef-sim` can be re-run against any NER state later. `ef-sim` finds the framework 5 levels above its `bin/Release/net8.0`, which is the copy's root.

**Rationale:** The NER sprint may commit between the baseline and the final runs. Keeping a base binary lets Step 5 run base and new back to back, so drift can't cause a false diff.
**Verification:** V0a, V0c.
**If this fails:** EF drift → stop and report. Base build fails → check `dotnet --info` and report; don't continue without a base.

---

### Step 1: `Core.Footprint` + Test

**Objective:** A game-free measurement of the filled-cell box.
**Confidence:** High
**Depends on:** Step 0
**Do-Projex:** Encouraged

**Files:**
- `src/Core/Footprint.cs` (new)
- `tests/Sim/Program.cs`

**Changes:**
```csharp
// src/Core/Footprint.cs (new)
using System;

namespace ExpeditionFramework.Core
{
    /// <summary>Measures the filled part of an item's grid shape.</summary>
    internal static class Footprint
    {
        /// <summary>
        /// The longer and shorter side of the smallest box around the filled cells, or (0, 0) when none is filled. It measures
        /// the box, not the grid: a sprite's grid can carry empty rows or columns, and counting them would make an item longer
        /// than it looks. A diagonal item fills a square box, so it reads its length in grid steps as reach and its breadth
        /// shows it isn't straight. Packs that want the diagonal longer can combine the two; a single rounded length would hide which it was.
        /// </summary>
        public static (int Reach, int Breadth) Measure(int width, int height, Func<int, int, bool> filled)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!filled(x, y)) continue;
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }
            if (maxX < 0) return (0, 0);
            int across = maxX - minX + 1, down = maxY - minY + 1;
            return (Math.Max(across, down), Math.Min(across, down));
        }
    }
}
```
```csharp
// tests/Sim/Program.cs: in Main, after "        TestItemTags();" add:
        TestFootprint();

// and add the method next to TestItemTags:
    // An item's reach and breadth come from the box around its filled cells, whatever empty grid surrounds them.
    private static void TestFootprint()
    {
        (int, int) Of(int width, int height, params (int X, int Y)[] cells) =>
            Footprint.Measure(width, height, (x, y) => cells.Contains((x, y)));
        Check(Of(4, 1, (0, 0), (1, 0), (2, 0), (3, 0)) == (4, 1), "a straight bar: " + Of(4, 1, (0, 0), (1, 0), (2, 0), (3, 0)));
        Check(Of(1, 3, (0, 0), (0, 1), (0, 2)) == (3, 1), "an upright bar: " + Of(1, 3, (0, 0), (0, 1), (0, 2)));
        Check(Of(3, 3, (0, 0), (1, 1), (2, 2)) == (3, 3), "a diagonal: " + Of(3, 3, (0, 0), (1, 1), (2, 2)));
        Check(Of(5, 4, (1, 1), (2, 1), (3, 1)) == (3, 1), "empty grid around the cells: " + Of(5, 4, (1, 1), (2, 1), (3, 1)));
        Check(Of(2, 2) == (0, 0), "nothing filled: " + Of(2, 2));
    }
```
(`Footprint` is `internal`. `tests/Sim` compiles `src/Core/**` into its own assembly, so it can see it. `System.Linq` is already imported there, which `cells.Contains` needs.)

**Rationale:** The bounding box matters only because the game's slice can be padded. Keeping it in Core makes it testable; `GridShape` can't be loaded outside the game.
**Verification:** V1a, V1b.
**If this fails:** Delete `Footprint.cs`, revert `Program.cs`.

---

### Step 2: `ItemReader.ReadFacts`

**Objective:** Six facts on every weapon, after `item_stats`.
**Confidence:** High
**Depends on:** Step 1
**Verify-Projex:** Encouraged

**Files:** `src/Game/ItemReader.cs`

**Changes:**
```csharp
// Before (usings):
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Core;
// After:
using System;
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Core;
```
```csharp
// Before (constants):
        private const string MeleeType = "MELEE_WEAPON";
// After:
        private const string MeleeType = "MELEE_WEAPON";
        private const string WeaponType = "WEAPON";

        /// <summary>The facts <see cref="ReadFacts"/> sets on every weapon, in the order the debug log prints them.</summary>
        public static readonly string[] FactNames = { "cells", "reach", "breadth", "price", "kinds", "purposes" };

        // The types EF already turns into tags; counting them would make every weapon read as more than a weapon.
        private static readonly HashSet<string> TaggedTypes = new HashSet<string> { WeaponType, MeleeType, FirearmType };
```
```csharp
// Before (ReadWeapon tail):
            item.ApplyStats(content);
            if (IsFirearm(weapon)) ReadGun(weapon, item);
            return item;
// After:
            item.ApplyStats(content);
            ReadFacts(weapon, item);
            if (IsFirearm(weapon)) ReadGun(weapon, item);
            return item;
```
```csharp
// New, after ReadWeapon:
        /// <summary>
        /// What the weapon's shape, price and game types say. ReadWeapon calls this after item_stats, as it does ReadGun, so a
        /// pack can't mask the game's numbers. Shape is the item's own, without attachments. Price is the base price (unitValue): GetValue() adds what
        /// the party, a trader and the stack size do, so the same knife would read differently at a counter. Purposes counts only
        /// the game's major types, because the other types include grades like JUNK or MIDLINE that say nothing about other uses.
        /// </summary>
        public static void ReadFacts(GameItem weapon, RunItem item)
        {
            GridShape shape = weapon.shape;
            (int reach, int breadth) = Footprint.Measure(shape.width, shape.height, (x, y) => shape.GetLocal(x, y) == 1);
            List<string> types = weapon.GetGameItemType();
            item.Set("cells", weapon.GetItemSize());
            item.Set("reach", reach);
            item.Set("breadth", breadth);
            item.Set("price", (int)Math.Min(int.MaxValue, Math.Max(0L, weapon.unitValue)));
            item.Set("kinds", types.Count(t => !TaggedTypes.Contains(t)));
            item.Set("purposes", types.Count(t => t != WeaponType && TypeHelper.MAJOR_TYPES_LIST.Contains(t)));
        }

        /// <summary>One log line: the id, the facts, the shape grid (rows top first, 1 filled), the game types and the name.</summary>
        public static string Describe(GameItem weapon, RunItem item)
        {
            GridShape shape = weapon.shape;
            IEnumerable<string> rows = Enumerable.Range(0, shape.height)
                .Select(y => new string(Enumerable.Range(0, shape.width).Select(x => shape.GetLocal(x, y) == 1 ? '1' : '0').ToArray()));
            return "id=" + item.Id + " " + string.Join(" ", FactNames.Select(n => n + "=" + item.Get(n)))
                + " width=" + shape.width + " height=" + shape.height + " shape=" + string.Join("/", rows)
                + " types=" + string.Join(",", weapon.GetGameItemType()) + " name=" + weapon.GetDisplayName();
        }
```
`IsMelee` stays an instance method; the debug command creates an `ItemReader`.

**Rationale:**
- Order: write the facts after `ApplyStats`, which matches `ReadGun` and how the docs already describe game facts. Curated changes belong in pack values or in a separate `item_stats` key the formula adds, not in rewriting a fact.
- Weapons only: these are the single items EF reads one per object, while stacks are per unit.
- Setting the facts before `ReadGun` keeps the gun stats last, so `capacity` handling is unchanged.

**Verification:** V2a (compile, Step 5), V2b.
**If this fails:** Revert `ItemReader.cs`. If a game member is not accessible (A1), report which one. Don't use reflection without asking.

---

### Step 3: Debug Entry "Expeditions: log melee weapon facts"

**Objective:** A reusable, human-triggered measurement of every melee weapon the game can make.
**Confidence:** Medium (A2: factory side effects are not provable offline)
**Depends on:** Step 2
**Verify-Projex:** Encouraged

**Files:** `src/Game/DevTools.cs`, `src/Game/PackRegistry.cs`

**Changes:**
```csharp
// DevTools.cs usings, before:
using System.Collections.Generic;
// after:
using System;
using System.Collections.Generic;
```
```csharp
// DevTools.cs, new members (after Add):
        // Where the game keeps its melee weapons: most in their own directory, the tazer with the guns, a wrench in an unused one.
        // Only these are built, not every item the game has: building thousands of items to find a dozen would be slow and would run
        // factories nothing here needs.
        private static readonly string[] WeaponDirectories = { "MeleeWeaponItemDirectory", "GunsItemDirectory", "UnusedDirectory" };

        /// <summary>Entries for the framework itself, whatever packs are installed.</summary>
        public static void AddFramework(Content content)
        {
            ModHelper.AddDebugFunction(ModEntry.ModId, ModHelper.DEBUG_CATEGORY_MODDING, "Expeditions: log melee weapon facts", () => LogMeleeFacts(content));
        }

        // One log line per melee weapon the game can make, read by the same code a run uses, for fitting formulas over the facts.
        // Each item is built fresh from its directory, so the line shows it as the game first makes it, not as one carried and aged.
        private static void LogMeleeFacts(Content content)
        {
            var reader = new ItemReader(content);
            var ids = new SortedSet<string>(content.ItemStats.Where(e => !e.Key.StartsWith("#") && e.Value.Tags.Contains("melee")).Select(e => e.Key));
            List<string> loaded = DirectoryMaster.GetDictionaryClassNameList<GameItem>();
            foreach (string directory in WeaponDirectories)
            {
                if (loaded.Contains(directory)) ids.UnionWith(DirectoryMaster.GetIdentifierList<GameItem>(directory));
                else ModEntry.Log.Log("Melee facts: directory " + directory + " is not loaded");
            }
            int count = 0;
            foreach (string id in ids)
            {
                try
                {
                    GameItem weapon = DirectoryMaster.Create<GameItem>(id);
                    if (weapon == null) { ModEntry.Log.Log("Melee facts: " + id + " is not a game item"); continue; }
                    if (!reader.IsMelee(weapon)) continue;
                    var item = new RunItem { Id = id };
                    ItemReader.ReadFacts(weapon, item);
                    ModEntry.Log.Log("Melee facts: " + ItemReader.Describe(weapon, item));
                    count++;
                }
                catch (Exception ex)
                {
                    ModEntry.Log.Warning("Melee facts: " + id + " failed: " + ex.Message);
                }
            }
            StoreUIManager.Instance.Notify(count + " melee weapons written to the game log.");
        }
```
```csharp
// PackRegistry.LoadAll, before:
            foreach (KeyValuePair<string, List<string>> other in problems.Where(p => !packs.Contains(p.Key) && p.Value.Count > 0))
// after (insert above that line, i.e. right after the per-pack foreach closes):
            DevTools.AddFramework(content);
            foreach (KeyValuePair<string, List<string>> other in problems.Where(p => !packs.Contains(p.Key) && p.Value.Count > 0))
```
The call goes after the loop because the loop `continue`s past a pack that has problems, and the framework's entry must not depend on EF's own pack loading cleanly. `PackRegistry.Stop` already calls `DevTools.RemoveAll()`, which removes it.

**Rationale:**
- A debug command was chosen over an automatic log at load. At `OnModAssetsLoaded` time it isn't known whether every item directory has run `Awake`. A load-time log would also repeat on every launch for as long as `dev.txt` exists. The command is reusable when the game adds weapons, and the panel is already enabled here (`dev.txt` present).
- The command builds items with `DirectoryMaster.Create` rather than `DirectoryMaster.Item`/`ItemSpawner.Spawn`. Those also assign a unique id from `PlayerStore` and run the permit hook, which are side effects on the save for a throwaway object.

**Verification:** V2a (compile), V4.
**If this fails:** Revert both files. The run facts (Step 2) stand alone.

---

### Step 4: Docs

**Objective:** Document the facts, the sim side and the new entry.
**Confidence:** High
**Depends on:** Steps 2–3
**Do-Projex:** Encouraged

**Files:** `docs/items.md`, `docs/testing.md`, `docs/packs.md`

**Changes:**
- `docs/items.md` § What the game reports. After the paragraph ending "`capacity_bonus` of every attached part." insert:

```markdown
Every weapon also reports what the game says about its size, price and uses, all whole numbers:

| Prop | What it is |
|---|---|
| `cells` | Grid cells the item fills in the bag (attachments not counted) |
| `reach` | The longer side of the box around those cells: a 4-long crowbar has 4, whichever way it lies |
| `breadth` | The shorter side of that box. A straight item has 1; a diagonal one has about its reach |
| `price` | The game's base price, before traders, party and condition change it |
| `kinds` | How many game item types it has besides weapon, melee weapon and firearm (including grades like junk) |
| `purposes` | How many of the game's main uses it has besides weapon: tool, household good, material and so on |

The framework's own data doesn't use them; a pack can, for example `"melee_sides": "2 + item.cells / 2"` in its `values`.
They are set after `item_stats`, so an `item_stats` number with one of these names is replaced in the game. To see them
for every melee weapon, use the debug panel's "Expeditions: log melee weapon facts" ([Debug panel](packs.md#debug-panel)).
```
- `docs/testing.md` § Kits, after "Give each weapon the tags the game would (`weapon`, `melee` or `firearm`, `in_hand`)." append: ` If the pack reads a weapon's [game facts](items.md#what-the-game-reports) (`cells`, `reach`, `breadth`, `price`, `kinds`, `purposes`), give them as props too: a kit item has no shape or price, so they read 0.`
- `docs/packs.md` § Debug panel, after the paragraph ending "`Expeditions: unlock AREA` entry." append: ` The framework adds "Expeditions: log melee weapon facts", which writes one line per melee weapon the game can make to the game's log (`Player.log`), starting `Melee facts:`, with the facts from [What the game reports](items.md#what-the-game-reports), its shape and its game types.`

The `values` example is valid as written: EF division drops the remainder (`docs/expressions.md` § Numbers), and a value evaluated in an action that `uses` the weapon sees it as `item`.

**Verification:** V6.
**If this fails:** Revert the docs.

---

### Step 5: Compile + Parity Proof + Cleanup

**Objective:** Prove it compiles without touching the game, `tests/Sim` passes, NER output is identical and `main.json` is untouched. Then clean the worktree.
**Confidence:** High
**Depends on:** Steps 1–4

**Changes:** none to source. Commands:
```bash
cd "$W"
dotnet build ExpeditionFramework.csproj -c Release -p:InstallDir="$S/game-install/" -p:GameManaged="$GAME/Probably Stolen_Data/Managed/" -o "$S/ef-build"; echo "exit $?"   # V2a; use forward slashes in both paths
dotnet run -c Release --project tests/Sim | tail -n 1                                                  # V1a
dotnet build -c Release tools/EfSim; echo "exit $?"
# NER parity, one sim at a time, base then new for each, nothing else in between:
$SIMB lint "$NER" --strict            > "$S/base/lint.txt"   2>&1; echo "exit $?" >> "$S/base/lint.txt"
$SIMN lint "$NER" --strict            > "$S/new/lint.txt"    2>&1; echo "exit $?" >> "$S/new/lint.txt"
$SIMB run "$NER" --runs 2000          > "$S/base/run.txt"    2>&1; echo "exit $?" >> "$S/base/run.txt"
$SIMN run "$NER" --runs 2000          > "$S/new/run.txt"     2>&1; echo "exit $?" >> "$S/new/run.txt"
$SIMB run "$NER" --runs 2000 --digest > "$S/base/digest.txt" 2>&1; echo "exit $?" >> "$S/base/digest.txt"
$SIMN run "$NER" --runs 2000 --digest > "$S/new/digest.txt"  2>&1; echo "exit $?" >> "$S/new/digest.txt"
for f in lint run digest; do cmp "$S/base/$f.txt" "$S/new/$f.txt" && echo "$f ok"; done                # V5a
git -C "$NER" rev-parse HEAD | cmp - "$S/ner-head-start.txt" && git -C "$NER" status --porcelain | sha1sum | cmp - "$S/ner-status-start.txt" && echo "ner unchanged"   # V5b (informational)
# Cleanup (Cleanup contract): build output in the worktree
rm -rf "$W/bin" "$W/obj" "$W/api/bin" "$W/api/obj" "$W/tools/EfSim/bin" "$W/tools/EfSim/obj" "$W/tests/Sim/bin" "$W/tests/Sim/obj"
git -C "$W" status --porcelain   # only intended tracked changes / nothing untracked
```
If V5b reports NER changed, that's fine: base and new ran on the same state, back to back. Record it in the log. If the lint exit is non-zero on **both**, it is NER's own state, not a regression; it still has to be identical.

**Verification:** V1a, V2a, V5a, V5b, V7, V8.
**If this fails:** A compile error → fix it in the step that caused it. A parity diff → `diff` the files. Any diff means a fact leaked into sim behavior (it shouldn't: no Core path reads the new props); stop and report.

---

## Verification Plan

### Deterministic Suite

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V0a | Step 0 | `git -C "$W" diff --stat a985df6 HEAD -- src api tools tests docs ExpeditionFramework` | empty | EF drift since plan |
| V0c | Step 0 | `(cd "$W" && dotnet run -c Release --project tests/Sim \| tail -n 1)` | `All checks passed` | Pre-existing red |
| V1a | SC2 | same as V0c, after Step 1 and again in Step 5 | `All checks passed` | Footprint wrong / regressions |
| V1b | SC2 | `grep -c 'TestFootprint()' "$W/tests/Sim/Program.cs"` | `2` | Test not wired |
| V2a | SC4, SC1, SC3 | Step 5 redirected `dotnet build ExpeditionFramework.csproj …`; `echo $?`; `stat -c %Y "$GAME/Mods/ExpeditionFramework/ExpeditionFramework.dll"` taken in Step 0 and again after the build | exit `0`; both mtimes equal (`1794657202` at plan time) | Compile errors; accidental live install |
| V2b | SC1 | `grep -oE '"(cells\|reach\|breadth\|price\|kinds\|purposes)"' "$W/src/Game/ItemReader.cs" \| sort -u \| wc -l`; `grep -n 'ApplyStats(content);' -A1 "$W/src/Game/ItemReader.cs" \| grep -c 'ReadFacts(weapon, item)'` | `6`; `1` | Missing fact; facts set before stats |
| V4 | SC3 | `grep -c 'Expeditions: log melee weapon facts' "$W/src/Game/DevTools.cs"`; `grep -c 'DevTools.AddFramework(content);' "$W/src/Game/PackRegistry.cs"`; `grep -c 'ItemReader.ReadFacts' "$W/src/Game/DevTools.cs"` | `1`; `1`; `1` | Entry missing / not registered / separate fact code |
| V5a | SC5 | Step 5 `for f in lint run digest; do cmp …` | `lint ok`, `run ok`, `digest ok` | NER output drift |
| V5b | SC5 | Step 5 NER head/status compare | `ner unchanged` or logged drift (informational) | Explains a surprise |
| V6 | SC7 | `for p in cells reach breadth price kinds purposes; do grep -c "$p" "$W/docs/items.md"; done`; `grep -c 'log melee weapon facts' "$W/docs/packs.md" "$W/docs/items.md"`; `grep -c 'breadth' "$W/docs/testing.md"` | each ≥ `1`; each ≥ `1`; `1` | Docs incomplete |
| V7 | SC8 | `git -C "$W" diff <base> -- src tests \| grep -E '^\+.*//.*([0-9]{10}-\|projex\|Step [0-9]\|\bSC[0-9]\|\bV[0-9]\|\.cs:[0-9])'` | no output | Unshipped references in comments |
| V8 | SC6 | `git -C "$W" diff --quiet <base> -- ExpeditionFramework/main.json; echo $?` | `0` | Melee table touched |

### Judgment Checks

| Check | Covers | Why not deterministic |
|---|---|---|
| Every new comment states what the code does now, and each non-obvious choice names its rejected alternative (box vs grid; `unitValue` vs `GetValue`; major types vs all types; after vs before `item_stats`; `Create` vs `Item`; the listed directories vs all items) | SC8 | Rationale quality needs reading |
| In-game: the entry appears under Modding (F9); the log has one `Melee facts:` line per melee weapon (expect 13, plus `wrench_orange` if `UnusedDirectory` is loaded); `kinds`/`purposes`/`price` match § Context predictions | SC3 | Needs the game; done post-close by the human (§ Post-Close Handoff) |

### Acceptance Criteria Validation

| Criterion | Check IDs | Expected Result |
|---|---|---|
| SC1 | V2a, V2b | compiles; 6 names; set after stats |
| SC2 | V1a, V1b | `All checks passed`; wired |
| SC3 | V2a, V4 + in-game judgment | compiles; registered; shares `ReadFacts` |
| SC4 | V2a | exit 0, game dll untouched |
| SC5 | V5a (V5b context) | 3 × ok |
| SC6 | V8 | 0 |
| SC7 | V6 | all present |
| SC8 | V7 + judgment | no output |

---

## Post-Close Handoff (human + orchestrator, not the executor)

1. The human deploys EF from `main`, **with the game closed** (normal build into `Mods/ExpeditionFramework`).
2. Launch the game, load a save, press **F9** → category **Modding** → **"Expeditions: log melee weapon facts"**. A notice says how many weapons were written. Quit the game.
3. Orchestrator, **before the next launch** (the game rotates `Player.log` → `Player-prev.log` on each start):
   `grep -a 'Melee facts:' "$USERPROFILE/AppData/LocalLow/Questing Goose Studio/Probably Stolen/Player.log" > <scratch>/melee-facts.txt`
   Each line reads `[Expedition Framework]: Melee facts: id=… cells=… reach=… breadth=… price=… kinds=… purposes=… width=… height=… shape=0110/1111 types=WEAPON,MELEE_WEAPON,… name=…`. "directory … is not loaded" / "… failed:" lines also start `Melee facts:`, so read them too.
4. Feed `melee-facts.txt` to the NER melee-model plan.

---

## Rollback Plan

1. `projex-abandon` the execution branch (worktree mode: `--worktree`). Nothing outside the worktree changed except scratch.
2. If already merged: `git revert` the squash commit. No data migration; no pack reads the props.

---

## Notes

### Risks
- **A pack `item_stats` key named like a fact is silently replaced in game but applies in `ef-sim`.** None exist today. Docs say so. Lint could warn later if it bites.
- **`Create<GameItem>` side effects (A2).** The command is dev-only and manual, with a try/catch per id. Worst case is a stray object or log noise in a dev session.
- **Sprite-derived shapes may surprise** (e.g. anti-aliased edges filling extra cells). That is why the log prints the full `shape=` grid: the NER fit can judge.
- **Concurrent sprint commits to EF `main`** before close: the squash-close may need a rebase. Files overlap only if the sprint touches `ItemReader`/`DevTools`/`PackRegistry`/these docs.

### Split Decision
`No split — heuristic tripped (~545 lines, 6 steps incl. preflight) but steps tightly coupled (rationale: the command must reuse ReadFacts; Footprint exists only for ReadFacts; Step 0/5 are one parity proof around the code). ~half the length is quoted code and suite.`

### Open Questions (for the human; none block execution)
- [ ] Does `purposes` count what you meant by "category purity"? It counts other **major** uses (tool, household good…) and ignores grades (junk, makeshift, midline). `kinds` counts everything, so NER can pick either. Is "makeshift/junk" a separate negative for NER to apply per id?
- [ ] Diagonal sprites: EF reports `reach` = longer box side plus `breadth`. NER can treat a square-ish box with few cells as diagonal and lengthen it, e.g. via `reach * reach + breadth * breadth`, since expressions have no square root. Decide after seeing the measured shapes.
- [ ] `ef-sim` kits read 0 for facts unless given. The NER plan will either give each kit weapon props, or record the measured facts in NER `item_stats` (applied in the sim; in the game EF replaces them with the same real values).
