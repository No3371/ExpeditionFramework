# Loader Loud Failures — Warnings Channel, Schema Shapes, Spec Read Tracking, Lint Command

> **Status:** Ready
> **Author:** Opus 5.5 (plan-projex subagent, dispatched by orchestrate-projex)
> **Source:** Direct request ("Plan hardening first") — Phase 1 of 2611120250-framework-expansion-synthesis-eval.md § Recommended Sequence
> **Parent:** 2611120250-framework-expansion-synthesis-eval.md
> **Related Projex:** 2611120258-runtime-safety-fixes-plan.md (sibling, Phase 1 runtime half) | 2611120231-pack-schema-loader-expansion-explore.md (E1, E2, E9, E10) | 2611120231-authoring-tooling-sim-testing-expansion-explore.md (A) | 2611120229-ui-presentation-expansion-explore.md (P-3) | 2611120227-runtime-hooks-expr-expansion-explore.md
> **Worktree:** No — NightExpeditionsReborn's sim compiles `../../../ExpeditionFramework/src/Core` by relative path, so it only tests the main checkout. Worktree mode would make the acceptance consumer test the wrong tree.

---

## Summary

Makes silently-ignored authoring mistakes loud at load time. Adds a second severity (warning) that is logged but never voids a pack; then reports unknown keys in every definition and in every effect/condition argument (built-in and C#), wrong-typed scalars, unknown tones, and `#tag` item filters nothing defines. Fixes two built-ins whose object form is silently ignored today (`not`, `carrying` — found during planning, verified by probe). Adds a `lint` mode to the framework sim so any pack folder (NER first) can be checked offline, including locale parity.

**Scope:** `src/Core` loader/Spec/Builtins, `api/ExpeditionApi.cs` (`ISpec.Warn`), `src/Core/PackSet.cs`, `src/Game/PackRegistry.cs`, `tests/Sim`, `README.md`. NER files read-only.
**Estimated Changes:** 11 files (new: `src/Core/Schema.cs`, `tests/Sim/Lint.cs`), ~25 functions.

---

## Objective

### Problem / Gap / Need

Strict at section level, lax inside definitions (verified this session in `PackLoader.cs`):
- Only 4 places reject unknown keys (sections, include objects, patch entries, shared-effect defs). A typo anywhere else (`"interuptible"`, `"wieght"`, a misspelled effect arg) is dropped silently.
- `Str/Int/Bool` (`PackLoader.Str/Int/Bool`) return the fallback on wrong type: `"unlocked_by": ["a"]` → null → area always unlocked; `"weight": "1+1"` → 1; `"turns": "2"` → -1.
- Tones are read with no allow-list (`CompileIntent`, `CompileIntel`, `LineFrom`, `CasesLine`, `say`) → unknown tone renders plain.
- `#tag` item filters (`PackLoader.Items`) are never checked.
- `Content.Problems` is the only channel; any entry voids the pack (`PackRegistry.LoadAll`), so softer checks can't be added without breaking packs.
- **New finding (probe-verified):** `Spec` wraps non-object args as `{value: x}` but keeps object args as-is, so `{"not": {"scene": "combat"}}` makes `not` read a missing `value` → `c.Condition("value")` returns `s => true` → `not` is **always false**. Same for `{"carrying": {"all": [...]}}` → filter "any item". Live instances: framework `main.json` kit interaction `punch` (`where: {not: {carrying: {all: [...]}}}` → never offered); sample `main.json` morale `for_each.where` (`{not: {has_tag: ...}}` → never true); NER `rules.json` free-search `if` (`{not: {scene: combat}}` → never fires).

### Success Criteria
- [ ] SC1 `Content.Warnings` exists; warnings are logged per pack and never void a pack or reject a patch.
- [ ] SC2 Unknown key in any definition or definition sub-object → one warning at `<path>.<key>` listing known keys.
- [ ] SC3 Wrong-typed scalar field (number/flag/string/string-list/number-list) → problem (`must be …`). Covers `unlocked_by` list and `weight` expression.
- [ ] SC4 `weight`/`when`/`tags` on kinds that never use them (statuses, intents, loot, carts, kits, areas) → warning.
- [ ] SC5 Effect/condition argument key never read by its compiler (built-in or C# via `ISpec`) → warning.
- [ ] SC6 Unknown tone on lines, intents, `say`, intel slots → warning.
- [ ] SC7 `#tag` item filter whose tag no engine tag, `item_groups` group, or `item_stats` tag/`#key` defines → warning.
- [ ] SC8 `{"not": {<condition>}}` and `{"carrying": {<filter>}}` work as written.
- [ ] SC9 Framework + sample content: 0 problems, 0 warnings; framework sim exits 0.
- [ ] SC10 NER sim (unchanged NER files) exits 0; `lint ../NightExpeditionsReborn` reports 0 errors; every NER warning triaged (framework false positive → fixed here; NER bug → follow-up list).
- [ ] SC11 `dotnet run --project tests/Sim -- lint <folder>` prints errors/warnings, missing text, locale key + placeholder parity; exit 1 on errors (or warnings with `--strict`).
- [ ] SC12 Game assembly (`src/Game`) still builds.
- [ ] SC13 README documents severities, unknown-key rule, tone list, `ISpec` read rule, `not`/`carrying` object form, lint command.

### Out of Scope
- Runtime fixes (`LeaveShared` finally, reserved `run.*` writes, `spawn` outside a fight, unknown item give, render-time RNG) → 2611120258-runtime-safety-fixes-plan.md.
- Unused-definition / unreachable / read-never-written-var lint, orphan text keys (dynamic `{…}` keys make them unreliable), dynamic text-key expansion (E10 rest), `ItemSpec` `#a+b` in `Expr` (best_item/carried), game item-id validation (needs game catalog).
- Per-area failure isolation (E16), pack metadata (E13), deep `extends` (E3), any schema expansion.
- Shape checks inside `RichArg` special forms (`if/then/else`, `bar`, `scheduled`, `list`, `items`).
- Editing NightExpeditionsReborn (its fixes are follow-ups).
- `//` comment support in other JSON tooling.

---

## Context

### Current State
- Load: `PackSet.Compile` → `Attempt` per patcher → `PackLoader.Add/CompileAll/Finish`. Patch acceptance compares `trial.Content.Problems.Except(best.Content.Problems)`. `PackRegistry.LoadAll` logs each pack's problems and skips its areas.
- `PackLoader.Problem(at, msg)` appends to `content.Problems` and `problemsByPack[current]`. `Defer` charges a Finish-time check to the current pack.
- `Section<T>` (L652): `Extend` → `Weight = Int(node,"weight",1)`, `Tags`, `When` → `fill`. Weight/When/Tags are only consumed for rooms (`BuildLayout`), events/encounters/enemies (`DefPool.Pick`, tag pools), and enemy entity tags.
- Effects/conditions: `Effect()`/`Condition()` build `new Spec(this, pack, at + "." + name, value)`; `Spec.Args = args as Node ?? {value: args}`. `Spec` accessors read `Args` without recording anything. `CompileSharedCall` already errors on unknown shared-effect params.
- Sims: framework `tests/Sim/Program.cs` fails on `content.Problems` + missing text; NER `tests/Sim/Program.cs` compiles framework `src/Core/**` + `api/*.cs` and fails only on `Problems`, missing text, never-offered options, stuck runs.
- Planning survey (script over JSONC, all three packs): 0 wrong-typed scalars, 0 unknown tones, 0 unknown `#tags`, definition key sets all valid → expected NER impact of SC2–SC4/SC6/SC7 is zero; SC5 impact unknown until run.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `src/Core/Model.cs` | `Content` | add `Warnings` |
| `src/Core/Schema.cs` (new) | — | field types, per-kind shapes, tone/tag vocabularies |
| `src/Core/PackLoader.cs` | loader + `Spec` | `Warn`, `WarningsByPack`, `CheckShape` at ~16 sites, `LineFrom`/`UseOf` shape param, `Spec` read tracking, `#tag` deferred check |
| `src/Core/Builtins.cs` | built-in vocabulary | `not`/`carrying` object form; `say` tone check |
| `api/ExpeditionApi.cs` | public API | `ISpec.Warn`; doc of read-tracking rule |
| `src/Core/PackSet.cs` | multi-pack compile | `Compile(out problems, out warnings)` overload |
| `src/Game/PackRegistry.cs` | in-game load | log warnings, keep areas |
| `tests/Sim/Program.cs` | framework sim | fail on own warnings; tests per check; `lint` dispatch |
| `tests/Sim/Lint.cs` (new) + `tests/Sim/Sim.csproj` | lint command | pack lint + locale parity |
| `README.md` | docs | severities, rules, lint |

### Dependencies
- **Requires:** framework repo's first commit (Phase 0; repo has zero commits — `execute-projex` needs this plan committed on `main`; human has not decided yet). Both sims passing at start (true at planning time).
- **Blocks:** none hard. Shares `PackLoader.CompileSharedCall`, `tests/Sim/Program.cs`, `README.md` with 2611120258-runtime-safety-fixes-plan.md → execute the two sequentially (either order), never in parallel.

### Constraints
- NER files must not be edited. NER must load with 0 problems; warnings allowed only if triaged.
- `src/Core` has no Unity refs (sims compile it on net8.0); `src/Game` targets netstandard2.1 with game DLLs.
- Building `ExpeditionFramework.csproj` runs `InstallMod` (copies into the game's `Mods/`); verification builds pass `-p:InstallDir=obj/verify-install/`.
- Messages follow existing style: `"<path>: <message>"`, plain English, no projex references in code/comments (SKILL.md § Source Hygiene).
- netstandard2.1 / C# latest: no APIs beyond what `src/Core` already uses (LINQ, collections).

### Assumptions
- A1: No built-in compiler reads `Spec` keys inside its runtime closure (planning heuristic scan of `Builtins.cs`/`PackLoader.cs` `s =>` bodies found 0). Verify in Step 3 by the SC9 zero-warning gate.
- A2: Item tags at run time = `Schema.EngineItemTags` (the set `ItemReader` adds) ∪ `item_groups` names ∪ `item_stats` tags ∪ `item_stats` `#group` keys (`RunItem.ApplyStats`). Verify by reading `ItemReader.ReadWeapon/ReadStack` in Step 4.
- A3: The only line tones the UI distinguishes are `ExpeditionLocation.ToneColors` keys + `plain` (default for `say`/spans); intel tones are `IntelCards.Tone`'s `good|bad|mixed`.
- A4: NER content has no wrong-typed scalars (planning survey). If Step 2 produces any NER problem, apply the Step 2 escalation rule.

### Impact Analysis
- **Direct:** loader compile paths, `Spec`, two built-ins, `PackSet`, `PackRegistry`, sim.
- **Adjacent:** C# mod effects (`ISpec` consumers) — new warnings if they read keys lazily; `ISpec` gains a member (source-breaking only for implementers; none exist outside the framework).
- **Downstream:** NER gameplay changes from SC8: NER free-search `if` in `rules.json` starts firing; framework kit `punch` becomes offerable (packs using framework kits); sample morale break starts working. These are bug fixes; NER balance numbers may move.

---

## Implementation

### Overview
1. Warning channel first (everything else reports through it). 2. Declarative shapes drive unknown-key warnings + scalar type problems + tone warnings for definitions. 3. `Spec` read tracking covers effect/condition args (incl. C#), plus the `not`/`carrying` fix. 4. `#tag` check. 5. `lint` command. 6. Docs. 7. NER acceptance + triage. Each code step adds its sim tests in the same step.

### Step 1: Warning channel (E9)

**Objective:** Second severity that is reported per pack but never voids a pack or rejects a patch.
**Confidence:** High
**Depends on:** None

**Files:** `src/Core/Model.cs`, `src/Core/PackLoader.cs`, `api/ExpeditionApi.cs`, `src/Core/PackSet.cs`, `src/Game/PackRegistry.cs`, `tests/Sim/Program.cs`

**Changes:**

```csharp
// Model.cs, Content — after Problems:
public readonly List<string> Problems = new List<string>();
/// <summary>Likely mistakes that don't stop the pack loading, such as a key nothing reads.</summary>
public readonly List<string> Warnings = new List<string>();

// PackLoader.cs — next to problemsByPack / ProblemsByPack / Problem():
private readonly Dictionary<string, List<string>> warningsByPack = new Dictionary<string, List<string>>();
public IReadOnlyDictionary<string, List<string>> WarningsByPack => warningsByPack;
internal void Warn(string at, string message)
{
    string warning = at + ": " + message;
    content.Warnings.Add(warning);
    if (!warningsByPack.TryGetValue(current, out List<string> list)) warningsByPack[current] = list = new List<string>();
    list.Add(warning);
}
// Spec: public void Warn(string message) => loader.Warn(Path, message);

// ExpeditionApi.cs, ISpec — after Error:
/// <summary>Reports a likely mistake at this spec's place without stopping the pack loading.</summary>
void Warn(string message);

// PackSet.cs — existing Compile(out problems) delegates to:
public Content Compile(out Dictionary<string, List<string>> problems, out Dictionary<string, List<string>> warnings)
// ...same body; at the end:
warnings = best.Loader.WarningsByPack.ToDictionary(p => p.Key, p => new List<string>(p.Value));
// Patch acceptance stays Problems-only (unchanged line), so warnings never reject a patch.

// PackRegistry.LoadAll — Compile(out problems, out warnings); per pack, before the problems check:
if (warnings.TryGetValue(pack, out List<string> notes)) foreach (string note in notes) ModEntry.Log.Warning("warning: " + note);
// and in the trailing loop over packs not in `packs`, log their warnings the same way.
```

Sim (`Program.cs`): in `Main` after the problems loop add `foreach (string w in content.Warnings) Fail("warning: " + w);` (framework + sample must stay warning-free). Add helper `WarningsIn(string json)` mirroring `ProblemsIn` (returns `content.Warnings`). Add `TestWarningsDontVoid()`: a `PackSet` with pack `a` (valid area) whose patcher `b` adds a key that only warns (use a shape-unknown key once Step 2 lands; in Step 1 use a C# effect calling `spec.Warn("x")`) → `problems` empty for both packs, `warnings["…"]` non-empty, patched content present.

**Rationale:** Separate list (not a severity enum on strings) keeps `PackSet` patch acceptance and every existing `Problems` consumer (both sims, `PackRegistry`) unchanged. `ISpec.Warn` lets built-ins (`say` tone) and C# effects use the same channel.

**Verification:** V1, V3, V4.

**If this fails:** revert the 6 files; nothing else depends on it yet.

---

### Step 2: Schema shapes — unknown keys, scalar types, tones (E1 defs, E2, P-3 warning)

**Objective:** One declarative table per definition kind and sub-object drives (a) unknown-key warnings, (b) wrong-type problems, (c) tone warnings.
**Confidence:** High
**Depends on:** Step 1
**Verify-Projex:** Encouraged — wide-reaching (every compile path), easy to miss a key and false-warn.

**Files:** `src/Core/Schema.cs` (new), `src/Core/PackLoader.cs`, `tests/Sim/Program.cs`

**Changes:**

```csharp
// src/Core/Schema.cs (new; compiled by both sims via src/Core/**)
namespace ExpeditionFramework.Core
{
    internal enum Field { Any, Number, Flag, Text, Texts, Numbers, Tone, IntelTone }

    internal sealed class Shape
    {
        public readonly Dictionary<string, Field> Fields;
        public Shape(params (string Key, Field Type)[] fields) => Fields = fields.ToDictionary(f => f.Key, f => f.Type);
        public Shape With(params (string Key, Field Type)[] more) => new Shape(Fields.Select(f => (f.Key, f.Value)).Concat(more).ToArray());
    }

    /// <summary>What each object in a pack may hold. Keys outside a shape are warned about; wrong-typed values are problems.</summary>
    internal static class Schema
    {
        // The tones ExpeditionLocation.ToneColors paints, plus "plain" (no colour). Intel cards know IntelCards.Tone's three.
        public static readonly string[] Tones = { "good", "bad", "dim", "heading", "enemy", "hint", "plain" };
        public static readonly string[] IntelTones = { "good", "bad", "mixed" };
        // Tags ItemReader puts on items from the game itself; every other item tag comes from item_groups or item_stats.
        public static readonly string[] EngineItemTags = { "weapon", "in_hand", "firearm", "melee", "has_rounds", "ammo", "armor_piercing", "non_lethal" };

        private static readonly Shape Named = new Shape(("extends", Field.Text));
        // Weight, when and tags only matter for what pools and tags pick from: rooms, events, encounters, enemies.
        private static readonly Shape Pooled = Named.With(("weight", Field.Number), ("tags", Field.Texts), ("when", Field.Any));
        public static readonly Dictionary<string, Shape> Sections = new Dictionary<string, Shape> { /* table below */ };
        public static readonly Shape LootEntry, CartLine, BehaviorEntry, EncounterEntry, Luck, Packing, PackingNeed, Intel,
            Hud, HudRow, HudEnemies, LayoutSlot, Interaction, Confirm, Use, Line, Case, HudPart; /* table below */

        /// <summary>Why a value doesn't fit its field, or null.</summary>
        public static string Mismatch(Field type, object value) { /* table below */ }
    }
}
```

Shape table (`A`=Any, `N`=Number, `F`=Flag, `T`=Text, `Ts`=Texts, `Ns`=Numbers; every key read by the matching `Compile*` today — verify against code while writing):

| Shape | Base | Keys |
|---|---|---|
| `statuses` | Named | name T, turns N, rooms N, intent T, stats A, interactions A, on A |
| `intents` | Named | label A, tell A, tone Tone, interruptible F, next T, do A |
| `loot` | Named | items A |
| `carts` | Named | name T, budget A, stock A |
| `enemies` | Pooled | name T, stats A, texts A, opening T, drops T, behavior A, interactions A, on A |
| `encounters` | Pooled | enemies A, on A |
| `events` | Pooled | text A, scene T, enter A, choices A |
| `rooms` | Pooled | text A, enter A, interactions A, on A |
| `kits` | Named | interactions A, on A |
| `areas` | Named | title T, unlocked_by T, ambience T, art T, hidden_when A, player A, vars A, pools A, entrance T, kits Ts, groups Ts, luck A, texts A, packing A, intel A, hud A, interactions A, on A, layout A |
| LootEntry | — | item T, loot T, table T, weight N, when A |
| CartLine | — | item T, loot Ts, index A, count A |
| BehaviorEntry | — | weight N, when A, intent T, behavior A |
| EncounterEntry | — | id T, count N |
| Luck | — | value A, low N, high N |
| Packing / PackingNeed | — | no_backpack T, card T, needs A / items A, missing T |
| Intel | — | id T, pool T, chance N, counts Ns, title A, description A, effect A, tone IntelTone, good Ts |
| Hud / HudRow / HudEnemies | — | title A, rows A, enemies A / when A, parts A, separator F / line A, pending A, none A, pending_scenes Ts |
| LayoutSlot | — | room T, rooms A, from A, tag T, pool T, count A, unique F |
| Interaction | — | use T, id T, label A, group T, in Ts, targets T, turn F, once F, stay F, sound T, sound_else T, when A, where A, uses A, confirm A, do A, check A, after A, preview A |
| Confirm | — | when A, text A, yes A, no A |
| Use (object with `select`) | — | select A, each F, consume N, limit N, order A, where A, with A |
| Line | — | text T, key T, args A, variants N, tone Tone, cases A |
| Case / HudPart | Line | + when A / + when A, each_item A |

`Mismatch` messages: Number `"must be a number"` (double) · Flag `"must be true or false"` (bool) · Text/Tone/IntelTone `"must be a string"` · Texts `"must be a string or a list of strings"` · Numbers `"must be a number or a list of numbers"`. `null` values are skipped (same as absent).

```csharp
// PackLoader.cs — new
private void CheckShape(Node n, Shape shape, string at)
{
    foreach (KeyValuePair<string, object> field in n)
    {
        string path = at + "." + field.Key;
        if (!shape.Fields.TryGetValue(field.Key, out Field type))
        {
            Warn(path, "unknown key, ignored (known: " + string.Join(", ", shape.Fields.Keys) + ")");
            continue;
        }
        if (field.Value == null) continue;
        string wrong = Schema.Mismatch(type, field.Value);
        if (wrong != null) Problem(path, wrong);
        else if (type == Field.Tone && Array.IndexOf(Schema.Tones, (string)field.Value) < 0) Warn(path, "unknown tone \"" + field.Value + "\" (known: " + string.Join(", ", Schema.Tones) + ")");
        else if (type == Field.IntelTone && Array.IndexOf(Schema.IntelTones, (string)field.Value) < 0) Warn(path, "unknown tone \"" + field.Value + "\" (known: " + string.Join(", ", Schema.IntelTones) + ")");
    }
}
```

Call sites (all in `PackLoader.cs`):

| Site | Call |
|---|---|
| `Section<T>`, after `Node node = Extend(...)` | `CheckShape(node, Schema.Sections[key], path);` |
| `AddLootEntry` | `CheckShape(entry, Schema.LootEntry, at);` |
| `CompileCart` stock loop, after the `is Node entry` guard | `CheckShape(entry, Schema.CartLine, path);` |
| `BehaviorList` loop, after `is Node entry` guard | `CheckShape(entry, Schema.BehaviorEntry, path);` |
| `CompileEncounter`, `e is Node entry` branch | `CheckShape(entry, Schema.EncounterEntry, at + ".enemies");` |
| `CompileArea` luck (`luck is Node l`) / layout slot | `Schema.Luck` at `at + ".luck"` / `Schema.LayoutSlot` at `path` |
| `CompilePacking` / its `need` loop | `Schema.Packing` at `at` / `Schema.PackingNeed` at `path` |
| `CompileIntel` | `CheckShape(n, Schema.Intel, at);` |
| `CompileHud` / row loop / `enemies` map | `Schema.Hud` / `Schema.HudRow` / `Schema.HudEnemies` (only if key present) |
| `CompileInteraction` (after merge, so `use` templates are covered) / confirm | `Schema.Interaction` at `at` / `Schema.Confirm` at `at + ".confirm"` |
| `UseOf(node, pack, at, bool checkShape)` when `u.ContainsKey("select")` and `checkShape` | `CheckShape(u, Schema.Use, at);` |
| `LineFrom(object v, string pack, string at, Shape shape)` when `v is Node n` and `shape != null` — before the `cases` branch | `CheckShape(n, shape, at);` |

Signature changes:
- `LineFrom(v, pack, at)` → `LineFrom(v, pack, at, Shape shape)`; callers pass `Schema.Line` (`LineOf`, `RichArg` tail, `Spec.Line`), `Schema.Case` (`CasesLine` entries), `Schema.HudPart` (`CompileHudPart`), `null` (`Spec.InlineLine` — its keys are the effect's own args, covered by Step 3).
- `UseOf(node, pack, at)` → `UseOf(node, pack, at, bool checkShape)`; `true` from `CompileInteraction` (uses, with) and `CompileHudPart` (`each_item`); `false` from `Spec.Use(null)`; `true` from `Spec.Use(key)`.

Existing `Str/Int/Bool` fallbacks stay: the shape check reports, the read keeps its old fallback, so a problem never also produces a crash.

Sim tests (`Program.cs`, new `TestShapes()` called from `Main` before `TestModEffectIsAPeer`):
- `WarningsIn(rooms a {text k, txet k2})` → 1 warning containing `rooms.a.txet` and `known:`; `ProblemsIn` same JSON → 0.
- `ProblemsIn(areas a {title k, player {hp 1}, layout [{room r}], unlocked_by ["b"]} + rooms r + areas b)` → 1 problem containing `unlocked_by` and `must be a string`.
- `ProblemsIn(encounters e {weight "1+1", enemies [x]} + enemies x {...})` → problem containing `weight` + `must be a number`.
- `ProblemsIn(intents i {label k, do [], interruptible "yes"})` → `must be true or false`.
- `WarningsIn(statuses s {name k, weight 2})` → warning at `statuses.s.weight` (SC4).
- `WarningsIn(intents i {label k, do [], tone "angry"})` → `unknown tone`; line `{text k, tone "loud"}` inside a room `text` → `unknown tone`; intel slot `tone "good"` → none, `tone "great"` → `unknown tone … good, bad, mixed`.
- Interaction typo: `rooms r {text k, interactions [{id x, label k, do [], gruop g}]}` → warning at `interactions[0].gruop`.

**Escalation rule (A4):** if the NER sim (V2) fails with a problem raised by `CheckShape`'s type branch, change that field's mismatch to a `Warn` (not a `Problem`), add the NER location to the Step 7 follow-up list, and record the downgrade in the execution log.

**Rationale:** One table answers "what may this object hold" for both checks (DRY, E1+E2 together) instead of retrofitting `at` into ~60 `Str/Int/Bool` call sites. `Any` keeps value-level validation where it already lives (`ValueOf`, `Condition`, `Effect`, `LineFrom`). Restricting `weight/when/tags` to the four pooled kinds turns the known "parsed, never used" debt into a warning.

**Verification:** V1, V2, V3, V5.

**If this fails:** revert `Schema.cs` and `PackLoader.cs` call sites/signatures; Step 1 stays.

---

### Step 3: Spec read tracking + `not`/`carrying` object form + `say` tone (E1 effects, SC5, SC8)

**Objective:** Any effect/condition arg key its compiler never reads is warned about — for built-ins and C# effects alike; `not`/`carrying` accept the object form authors already write.
**Confidence:** Medium — exact marking for `Pool`/`Use`/`Enemies`/`InlineLine` must match what they read from `Args`.
**Depends on:** Step 1 (Step 2 for `LineFrom` signature)
**Verify-Projex:** Encouraged — false positives here would hit every pack and every C# mod.

**Files:** `src/Core/PackLoader.cs` (`Spec`, `Effect`, `Condition`, `CompileSharedCall`), `src/Core/Builtins.cs`, `api/ExpeditionApi.cs` (doc only), `tests/Sim/Program.cs`

**Changes:**

```csharp
// PackLoader.Spec
private readonly HashSet<string> read = new HashSet<string>();
private readonly List<Spec> children = new List<Spec>();
private void Mark(params string[] keys) => read.UnionWith(keys);
internal void MarkAllRead() => read.UnionWith(Args.Keys);

// Every accessor marks the key(s) it names before reading:
public bool Has(string key) { read.Add(key); return Args.TryGetValue(key, out object v) && v != null; }
public string Text(string key, string fallback = null) { read.Add(key); return Str(Args, key, fallback); }
// likewise Flag, Number, IsText, ListAt (Strings/Conditions), Entries; Value/Effect/Condition/Items/Line go through Has.
// Entries: each child Spec is also added to `children`.
// PoolOf: Mark("value", "pool", "from", "ids", "tag") before loader.Pool(Args, ...).
// Use(null): Mark("select", "each", "consume", "limit", "order", "where"); if (!Args.ContainsKey("select")) MarkAllRead();  // Args is itself a filter
// InlineLine(): own ? Mark("text", "key", "cases", "args", "variants", "tone") : Mark("value"); pass shape null to LineFrom.
// Enemies(): already goes through Has/IsText/Strings/Text, then PoolOf.

internal void ReportUnread()
{
    foreach (KeyValuePair<string, object> arg in Args)
    {
        if (arg.Value != null && !read.Contains(arg.Key)) loader.Warn(Path + "." + arg.Key, "not used here (misspelled, or not a field of this effect or condition)");
    }
    foreach (Spec child in children) child.ReportUnread();
}

// Built-ins that take a whole condition / item filter as their argument:
internal Condition WholeCondition() { MarkAllRead(); return loader.Condition(Args, PackId, Path); }
internal ItemFilter WholeItems() { MarkAllRead(); return loader.Items(Args, PackId, Path); }

// PackLoader.Effect():
//   Before: return compiler(new Spec(this, pack, at + "." + only.Key, only.Value)) ?? (s => { });
//   After:
var spec = new Spec(this, pack, at + "." + only.Key, only.Value);
Effect compiled = compiler(spec) ?? (s => { });
spec.ReportUnread();
return compiled;
// PackLoader.Condition(): same pattern.
// PreviewOf's DieCheck.From(new Spec(...)) does NOT report (the same args are compiled and reported by Effect).

// CompileSharedCall: first line `call.MarkAllRead();` — unknown params are already problems there.

// Builtins.cs
// Before:
r.Conditions["not"] = c => { Condition inner = c.Condition("value"); return s => !inner(s); };
// After:
r.Conditions["not"] = c =>
{
    // {"not": {"scene": "combat"}} arrives as the condition's own fields, not under "value".
    Condition inner = c.Has("value") ? c.Condition("value") : ((PackLoader.Spec)c).WholeCondition();
    return s => !inner(s);
};
// carrying: ItemFilter filter = c.Has("value") ? c.Items("value") : ((PackLoader.Spec)c).WholeItems();
// say: after reading tone — if (Array.IndexOf(Schema.Tones, tone) < 0) c.Warn("unknown tone \"" + tone + "\" (known: " + string.Join(", ", Schema.Tones) + ")");

// ExpeditionApi.cs ISpec summary: add "Keys an effect never asks for while compiling are reported as warnings, so read every key before returning the effect."
```

Sim tests (new `TestUnreadArgs()`):
- `WarningsIn(rooms r {text k, enter [{set {var run.x, valu 1}}]})` → warning at `enter[0].set.valu`.
- C# effect reading `amount` only; data `{t:drain {amount 1, amout 2}}` → exactly 1 warning (`amout`).
- C# effect reading nothing given scalar `{t:ping: 3}` → warning at `.value`.
- Shared effect call with unknown param → still exactly 1 problem, 0 warnings (no double report).
- `not` object form: area `hidden_when {not {scene combat}}` → evaluated on `new MapContext(...)`.Root → `true` (scene is `map`); `{not {value {scene combat}}}` → `true`; `{not {}}` → 1 problem.
- `carrying` object form: `{carrying {all ["#melee", "#in_hand"]}}` with a `FakeBridge` holding a melee-in-hand item → true; holding only a non-melee item → false.
- `say {text k, tone "loud"}` → 1 warning.
- `TestPublicApi`: make `tour` read `n` eagerly (`int total = entries.Sum(e => e.Number("n", 0));` before `return s =>`, closure uses `total`) and add `Check(content.Warnings.Count == 0, ...)`.

**Rationale:** Tracking inside `Spec` covers every built-in and every C# `ISpec` consumer with no per-effect tables to maintain. Only `PackLoader.Pool`/`UseOf`/`LineFrom` read `Args` behind `Spec`'s back, so only those four accessors need explicit marks. The `not`/`carrying` fix is targeted (2 built-ins with a natural object form); a generic "object means value" rule would change `ValueOf`/`Require` behavior for every value-only effect.

**Verification:** V1, V2, V3, V5.

**If this fails:** revert `Spec` tracking and the 3 built-in edits; Steps 1–2 stand. If only false positives remain from one accessor, fix its `Mark` list.

---

### Step 4: `#tag` item-filter lint (E10, `#group` part)

**Objective:** Warn when an item filter names a tag no engine tag, group, or stat defines.
**Confidence:** High
**Depends on:** Step 1, Step 2 (`Schema.EngineItemTags`)
**Do-Projex:** Encouraged

**Files:** `src/Core/PackLoader.cs`, `tests/Sim/Program.cs`

**Changes:**

```csharp
// PackLoader.Items, "#" branch — Before:
string tag = text.Substring(1);
return (item, s) => item.HasTag(tag);
// After:
string tag = text.Substring(1);
Defer(() => { if (!KnownItemTags().Contains(tag)) Warn(at, "no item has the tag " + tag + " (not a game tag, item group or item_stats tag)"); });
return (item, s) => item.HasTag(tag);

// New: known tags built once per Finish (all packs' groups and stats exist by then).
private HashSet<string> knownItemTags;
private HashSet<string> KnownItemTags() => knownItemTags ??= new HashSet<string>(Schema.EngineItemTags
    .Concat(content.ItemGroups.Values.SelectMany(g => g))
    .Concat(content.ItemStats.Values.SelectMany(s => s.Tags))
    .Concat(content.ItemStats.Keys.Where(k => k.StartsWith("#")).Select(k => k.Substring(1))));
// Defer already charges the check to the pack being compiled; Finish() sets knownItemTags = null at its end.
```

Check A2 first: read `ItemReader.ReadWeapon/ReadStack` and confirm `Schema.EngineItemTags` equals the tags added there.

Sim tests: `WarningsIn(rooms r {text k, interactions [{id x, label k, uses "#explosve", do []}]})` → 1 warning containing `explosve`; same with `#melee` → 0; with `item_groups {bright [torch]}` + `#bright` → 0; with `item_stats {"#loud": {tags ["noisy"]}}` + `#noisy` → 0.

**Rationale:** Deterministic once all packs are compiled; mirrors the existing deferred `tag` pool check (`PackLoader.Pool`).

**Verification:** V1, V3.

**If this fails:** revert the `#` branch edit and `KnownItemTags`.

---

### Step 5: `lint` command in the framework sim (Tooling A, offline home)

**Objective:** `dotnet run --project tests/Sim -- lint <pack folder> [--mod <id>]... [--strict]` checks any pack against the framework.
**Confidence:** High
**Depends on:** Step 1
**Do-Projex:** Encouraged

**Files:** `tests/Sim/Lint.cs` (new), `tests/Sim/Sim.csproj`, `tests/Sim/Program.cs`

**Changes:**

```xml
<!-- Sim.csproj -->
<Compile Include="Program.cs" />
<Compile Include="Lint.cs" />
```

```csharp
// Program.cs — Before: private static int Main() { root = ...; ...
// After:
private static int Main(string[] args)
{
    root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    if (args.Length > 0 && args[0] == "lint") return Lint.Run(root, args.Skip(1).ToArray());
    ...existing body unchanged...

// Lint.cs
internal static class Lint
{
    private const string Framework = "expedition_framework";

    public static int Run(string frameworkRoot, string[] args)
    // 1. folder = Path.GetFullPath(args[0]); mods = values after each "--mod"; strict = args.Contains("--strict").
    //    Missing folder arg → print usage, return 2.
    // 2. pack id = <ID> in folder/manifest.xml (Regex @"<ID>\s*([^<\s]+)\s*</ID>"); none → "ERROR no <ID> in manifest.xml", return 2.
    // 3. PackSet (new Registry(), HasMod = mods.Contains); Add framework (frameworkRoot) unless id == Framework; Add pack (folder);
    //    Compile(out problems, out warnings). Print "ERROR " + p / "WARN " + w for every pack's lists.
    // 4. Text: ReadCsv(frameworkRoot/Localization/en.csv) and ReadCsv(folder/Localization/en.csv); each content.TextKeys entry of
    //    pack id or Framework with no row → "ERROR missing text pack:key".
    // 5. Parity: each other folder/Localization/*.csv → ParityProblems(en, other, fileName) → "WARN " + each.
    // 6. Print "lint <id>: N error(s), M warning(s)"; return errors > 0 || (strict && warnings > 0) ? 1 : 0.

    // Key → text. Skips blank lines and lines starting with '#'; splits on the first comma, like the sims.
    internal static Dictionary<string, string> ReadCsv(string file);

    // Keys missing from / extra in `other`, and keys whose {n} placeholder sets differ (Regex @"\{\d+\}").
    internal static List<string> ParityProblems(Dictionary<string, string> en, Dictionary<string, string> other, string file);
}
```

Sim test (`TestLocaleParity()`): `ParityProblems({a:"x {0}", b:"y"}, {a:"x", c:"z"}, "zh.csv")` → 3 entries (a placeholders, b missing, c extra); identical dicts → 0.

**Rationale:** The sim already compiles Core + API headlessly; adding a mode avoids a new project and gives NER (and future packs) a lint without touching their repos. `PackSet` (not `PackLoader`) so lint sees patches exactly as the game does. Orphan-key reporting omitted: dynamic `{…}` keys make it unreliable (out of scope).

**Verification:** V1, V6, V7, V8.

**If this fails:** remove `Lint.cs`, the csproj line, and the `Main` dispatch.

---

### Step 6: README

**Objective:** Document the new contract.
**Confidence:** High
**Depends on:** Steps 1–5
**Do-Projex:** Encouraged

**Files:** `README.md`

**Changes:**
- § Making a pack: replace "A pack with any problem adds no areas." with: problems (broken structure, wrong types, dangling references) stop a pack's areas loading; warnings (unknown keys, keys an effect doesn't use, unknown tones, item tags nothing defines, `weight`/`when`/`tags` on kinds that ignore them) are logged as `warning: …` and the pack still loads.
- § Vocabulary: line tones `good, bad, dim, heading, enemy, hint, plain`; intel tones `good, bad, mixed`. `not` and `carrying` take their condition/filter directly (`{"not": {"scene": "combat"}}`) or under `value`.
- § The C# API: `spec.Warn`; "read every key while compiling — keys not asked for by the time the compiler returns are reported as unused".
- § Building and testing: `dotnet run -c Release --project tests/Sim -- lint ../MyPack [--mod gunsmith_expansion] [--strict]` — what it checks, exit codes.

**Verification:** V9 (presence); judgment: wording matches behavior.

**If this fails:** revert README.

---

### Step 7: NER acceptance + triage

**Objective:** Prove the acceptance consumer still loads; classify every warning it now gets.
**Confidence:** Medium — SC5 count on NER unknown until run.
**Depends on:** Steps 1–5

**Files:** none edited in NER. Framework files only if a false positive is found (fix goes in the owning step's code).

**Changes:** Run V2 and V7. For each NER warning: (a) framework false positive (key is read at run time, shape table missing a key, mark list incomplete) → fix in framework, rerun; (b) genuine NER mistake → add to the execution log's follow-up list as `NER: <file path from warning> — <what to change>`. Also record the gameplay deltas from SC8 (free-search `if` now fires; framework `punch` now offerable).

**Rationale:** NER is the only large real consumer; triage separates framework bugs (fix now) from content bugs (outside scope).

**Verification:** V2, V7; judgment: triage table complete.

**If this fails:** if NER problems appear that can't be classified, set plan Blocked and escalate with the list.

---

## Verification Plan

Commands run from the framework repo root (bash/PowerShell identical).

### Deterministic Suite

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V1 | SC1–SC9, SC11 tests; Steps 1–5 | `dotnet run -c Release --project tests/Sim` | exit 0; last line `All checks passed` | any new check misfiring; own content warning/problem; unit test regressions |
| V2 | SC10, SC8 downstream | `dotnet run -c Release --project ../NightExpeditionsReborn/tests/Sim` | exit 0; last line `All checks passed` | NER voided by new problems; behavior break |
| V3 | SC1, SC12 | `dotnet build -c Release ExpeditionFramework.csproj -p:InstallDir=obj/verify-install/` | exit 0, `0 Error(s)` | `PackRegistry`/`ISpec` change breaks the game build |
| V4 | SC1 | `grep -c "out Dictionary<string, List<string>> warnings" src/Core/PackSet.cs` | ≥ 1 | overload missing |
| V5 | SC2, SC3, SC6 | `grep -c "CheckShape(" src/Core/PackLoader.cs` | ≥ 18 (1 definition + 17 call sites in the Step 2 table) | call sites skipped |
| V6 | SC11 | `dotnet run -c Release --project tests/Sim -- lint samples/SampleArea --strict` | exit 0; output contains `lint sample_area: 0 error(s), 0 warning(s)` | lint misreads a clean pack |
| V7 | SC10 | `dotnet run -c Release --project tests/Sim -- lint ../NightExpeditionsReborn` | exit 0; output line `lint night_expeditions_reborn: 0 error(s), …` | NER has errors under game-like load |
| V8 | SC11 | `dotnet run -c Release --project tests/Sim -- lint` | exit 2; usage printed | arg handling |
| V9 | SC13 | `for p in "warning:" "-- lint" "plain" "spec.Warn"; do grep -c -F -e "$p" README.md; done` | four counts, each ≥ 1 | docs step skipped |

### Judgment Checks

| Check | Covers | Why not deterministic |
|-------|--------|-----------------------|
| Shape table covers every key each `Compile*` reads (read code side by side) | SC2, SC3 | completeness vs code is a reading task; V1 only catches keys present in test content |
| NER warning triage table (false positive vs NER bug) | SC10 | classification needs authorial intent |
| README wording matches behavior | SC13 | prose |
| In-game log shows `warning:` lines and areas still added | SC1 | needs the game running; not available headlessly |

### Acceptance Criteria Validation

| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| SC1 | V1 (`TestWarningsDontVoid`), V3, V4, in-game judgment | warnings separate; areas kept |
| SC2 | V1 (`TestShapes`), V5, judgment | typo → 1 warning with known list |
| SC3 | V1 (`TestShapes`) | `unlocked_by` list, `weight` expr, flag string → problems |
| SC4 | V1 (`TestShapes`) | `statuses.s.weight` warned |
| SC5 | V1 (`TestUnreadArgs`) | built-in + C# unread keys warned; shared call not doubled |
| SC6 | V1 (`TestShapes`, `TestUnreadArgs`) | unknown tones warned |
| SC7 | V1 (Step 4 tests) | `#explosve` warned; known tags silent |
| SC8 | V1 (`TestUnreadArgs`) | object-form `not`/`carrying` evaluate correctly |
| SC9 | V1, V6 | 0 problems, 0 warnings for framework + sample |
| SC10 | V2, V7, triage judgment | NER 0 problems; warnings classified |
| SC11 | V1 (`TestLocaleParity`), V6, V7, V8 | lint output and exit codes |
| SC12 | V3 | game assembly builds |
| SC13 | V9, judgment | documented |

---

## Rollback Plan

1. Revert the modified files to the pre-execution commit (`git -C <repo-root> checkout <base> -- <files>` or abandon the ephemeral branch via `projex-abandon`).
2. Delete `src/Core/Schema.cs` and `tests/Sim/Lint.cs` if untracked on base.
3. Rerun V1 and V2 to confirm the baseline.

---

## Notes

### Risks
- C# mods that read `ISpec` keys lazily get spurious warnings: documented rule (Step 6); warnings never block loading.
- SC8 changes gameplay in NER (free searches now consumed out of combat) and anywhere framework kit `punch` is used: intended bug fixes; flag in walkthrough.
- Warnings caused by another pack's patch are charged to the patched pack (`current` = target during compile): acceptable; patch problems keep existing blame rules.
- `Schema.Tones`/`EngineItemTags` duplicate lists that live in `src/Game` (`ExpeditionLocation.ToneColors`, `ItemReader`): comments name the owning symbols; a future change there must update `Schema`.

### Split decision
Phase 1 was split into two plans: this one (loader/lint) and 2611120258-runtime-safety-fixes-plan.md (independent runtime fixes). For this plan: `No split — heuristic tripped (~600 lines, 7 steps) but steps tightly coupled (rationale: Steps 2–5 all report through Step 1's channel; Steps 2–3 share the `LineFrom`/`UseOf` signature change; Step 7 validates all of them together).`

### Open Questions
- (none — decisions with defaults are listed below for the user)

### Decisions taken by default (user may override before execution)
- `not`/`carrying` object form is fixed (gameplay change in NER), not just warned about.
- Wrong-typed scalars are **problems** (E2), not warnings, with the A4 escalation rule as safety valve.
- `weight`/`when`/`tags` restricted to rooms/events/encounters/enemies (warning elsewhere).
