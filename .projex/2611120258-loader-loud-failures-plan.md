# Loader Loud Failures — Warnings Channel, Schema Shapes, Spec Read Tracking, Lint Command

> **Status:** Ready
> **Author:** Opus 5.5 (plan-projex subagent, dispatched by orchestrate-projex)
> **Source:** Direct request ("Plan hardening first") — Phase 1 of 2611120250-framework-expansion-synthesis-eval.md § Recommended Sequence
> **Parent:** 2611120250-framework-expansion-synthesis-eval.md
> **Related Projex:** 2611120258-runtime-safety-fixes-plan.md (sibling, Phase 1 runtime half — executes **before** this plan) | 2611120448-phase1-hardening-plans-redteam.md | 2611120507-phase1-hardening-plans-blueteam.md (dispositions applied 2026-11-12) | 2611120231-pack-schema-loader-expansion-explore.md (E1, E2, E9, E10) | 2611120231-authoring-tooling-sim-testing-expansion-explore.md (A) | 2611120229-ui-presentation-expansion-explore.md (P-3) | 2611120227-runtime-hooks-expr-expansion-explore.md
> **Worktree:** No — NightExpeditionsReborn's sim compiles `../../../ExpeditionFramework/src/Core` by relative path, so it only tests the main checkout. Worktree mode would make the acceptance consumer test the wrong tree.

---

## Summary

Makes silently-ignored authoring mistakes loud at load time. Adds a second severity (warning) that is logged but never voids a pack; then reports unknown keys in every definition and in every effect/condition argument (built-in and C#), wrong-typed scalars, unknown tones, and `#tag` item filters nothing defines. Fixes two built-ins whose object form is silently ignored today (`not`, `carrying` — found during planning, verified by probe; human decided **fix**, accepting the gameplay changes). Adds a `lint` mode to the framework sim so a pack folder that depends only on the framework (NER first) can be checked offline, including locale parity.

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
- [ ] SC5 Effect/condition argument key never read by its compiler (built-in or C# via `ISpec`) → warning. Exempt: the `value` a `Spec` synthesized from scalar `true` (`{"next_room": true}` → 0 warnings; every other scalar still warns), and every key of a spec that reported an error (one mistake → one problem, no unread-key warnings).
- [ ] SC6 Unknown tone on lines, intents, `say`, intel slots → warning.
- [ ] SC7 `#tag` item filter whose tag no engine tag, `item_groups` group, or `item_stats` `tags` entry defines → warning (an `item_stats` `#key` consumes a tag, never defines one).
- [ ] SC8 `{"not": {<condition>}}` and `{"carrying": {<filter>}}` work as written; `null`/empty argument → problem `needs a condition` / `needs an item filter`.
- [ ] SC9 Framework + sample content: 0 problems, 0 warnings; framework sim exits 0.
- [ ] SC10 NER sim (unchanged NER files) exits 0; `lint ../NightExpeditionsReborn` reports 0 errors; every NER warning (as printed by that lint run — the NER sim never prints warnings) triaged (framework false positive → fixed here; NER bug → follow-up list).
- [ ] SC11 `dotnet run --project tests/Sim -- lint <folder>` (pack depending only on the framework) prints errors/warnings, missing text, locale key + placeholder parity; exit 1 on errors (or warnings with `--strict`).
- [ ] SC12 Game assembly (`src/Game`) still builds.
- [ ] SC13 README documents severities, unknown-key rule, tone list, `ISpec` read rule (incl. `true` exemption), `ISpec` only grows / framework Api.dll must be the loaded copy, `not`/`carrying` object form, lint command and its framework-only-dependency limit.

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
- **Requires:** ~~framework repo's first commit (Phase 0; human has not decided yet)~~ — superseded: done, `8152b9b` on `main`, full tree incl. `.projex/` (human decision, blue team F2). Precondition before `execute-projex`: `git -C <repo-root> ls-files src/Core/PackLoader.cs tests/Sim/Program.cs README.md` lists all three (code is tracked, so Rollback Plan works).
- **Requires:** 2611120258-runtime-safety-fixes-plan.md executed and closed (merged to `main`) first — **order fixed: runtime plan, then this plan** (blue team F20). Step 3's `set run.turn` test exercises the engine-var `c.Error` path that plan's Step 2 adds. Both sims passing at start (rerun after the runtime plan lands).
- **Blocks:** none. Shares `PackLoader.CompileSharedCall`, `tests/Sim/Program.cs`, `README.md` with the runtime plan → ~~either order~~ superseded by the fixed order above; never in parallel.

### Constraints
- NER files must not be edited. NER must load with 0 problems; warnings allowed only if triaged.
- `src/Core` has no Unity refs (sims compile it on net8.0); `src/Game` targets netstandard2.1 with game DLLs.
- Building `ExpeditionFramework.csproj` runs `InstallMod` (copies into the game's `Mods/`); verification builds pass `-p:InstallDir=obj/verify-install/`.
- Messages follow existing style: `"<path>: <message>"`, plain English, no projex references in code/comments (SKILL.md § Source Hygiene).
- netstandard2.1 / C# latest: no APIs beyond what `src/Core` already uses (LINQ, collections).

### Assumptions
- A1: An unread key at compile return is a real author mistake. Holds given three conditions: (a) no built-in compiler reads `Spec` keys inside its runtime closure (planning heuristic scan of `Builtins.cs`/`PackLoader.cs` `s =>` bodies found 0); (b) the `value` a `Spec` synthesized from scalar `true` is exempt — no-arg built-ins (`next_room`, `fits`, `reload`, `intent_interruptible`, …) never read it, and the framework's own content uses `{"x": true}`; (c) a spec that reported an error skips its unread report — compilers like `Builtins.Assign`/`set_text` error and return before reading remaining keys. Verify in Step 3 by the SC9 zero-warning gate and the F1/F4 tests. (Widened from (a) alone — blue team F14.)
- A2: Item tags at run time = `Schema.EngineItemTags` (the set `ItemReader` adds) ∪ `item_groups` names ∪ `item_stats` `tags`. ~~∪ `item_stats` `#group` keys~~ — superseded (blue team F11): `RunItem.ApplyStats` applies a `#tag` stat only to items already carrying that tag, so a `#key` consumes a tag, never defines one. Verify by reading `ItemReader.ReadWeapon/ReadStack` in Step 4.
- A3: The only line tones the UI distinguishes are `ExpeditionLocation.ToneColors` keys + `plain` (default for `say`/spans); intel tones are `IntelCards.Tone`'s `good|bad|mixed`.
- A4: NER content has no wrong-typed scalars (planning survey). If Step 2 produces any NER problem, apply the Step 2 escalation rule.

### Impact Analysis
- **Direct:** loader compile paths, `Spec`, two built-ins, `PackSet`, `PackRegistry`, sim.
- **Adjacent:** C# mod effects (`ISpec` consumers) — new warnings if they read keys lazily; `ISpec` gains a member (source-breaking only for implementers; none exist outside the framework).
- **Downstream:** gameplay changes from SC8, **accepted by the human** (blue team F5): NER `rules.json` free-search `if` (`{not: {scene: combat}}`) starts firing out of combat (`run.searches += run.free_searches` + `ne_free_search` line); framework kit `punch` (`main.json` `where: {not: {carrying: …}}`) becomes offerable to kits including `expedition_framework:melee` (the sample does; NER's kits don't); sample `cellar_nerves` morale `where` (`{not: {has_tag: {tag: boss}}}`) starts matching → non-boss humans flee at 25% when an ally falls. NER has no VCS → only rollback is reverting the framework commit.

---

## Implementation

### Overview
1. Warning channel first (everything else reports through it). 2. Declarative shapes drive unknown-key warnings + scalar type problems + tone warnings for definitions. 3. `Spec` read tracking covers effect/condition args (incl. C#). 3b. `not`/`carrying` object-form fix (split out of 3 so the gameplay-changing part is its own unit). 4. `#tag` check. 5. `lint` command. 6. Docs. 7. NER acceptance + triage. Each code step adds its sim tests in the same step.

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

### Step 3: Spec read tracking + `say` tone (E1 effects, SC5)

**Objective:** Any effect/condition arg key its compiler never reads is warned about — for built-ins and C# effects alike — without flagging the framework's `{"x": true}` no-arg idiom or piling warnings onto a reported error. (`not`/`carrying` object form moved to Step 3b.)
**Confidence:** Medium — exact marking for `Pool`/`Use`/`Enemies`/`InlineLine` must match what they read from `Args`.
**Depends on:** Step 1 (Step 2 for `LineFrom` signature)
**Verify-Projex:** Encouraged — false positives here would hit every pack and every C# mod.

**Files:** `src/Core/PackLoader.cs` (`Spec`, `Effect`, `Condition`, `CompileSharedCall`), `src/Core/Builtins.cs`, `api/ExpeditionApi.cs` (doc only), `tests/Sim/Program.cs`

**Changes:**

```csharp
// PackLoader.Spec
private readonly HashSet<string> read = new HashSet<string>();
private readonly List<Spec> children = new List<Spec>();
// {"next_room": true} is how content says "no arguments"; the "value" this wraps it in is never meant to be read.
// Only a literal true is exempt: {"next_room": 3}, false or a string still warn, and an explicit {"value": true} was typed by the author.
private readonly bool bareTrue;
// Compilers that reject an argument return before reading the rest; reporting those as unused would read as a second mistake.
private bool errored;
private void Mark(params string[] keys) => read.UnionWith(keys);
internal void MarkAllRead() => read.UnionWith(Args.Keys);

// Spec ctor — after `Args = args as Node ?? new Node { { "value", args } };`:
bareTrue = args is bool b && b;

// Spec.Error — Before: public void Error(string message) => loader.Problem(Path, message);
// After:
public void Error(string message)
{
    errored = true;
    loader.Problem(Path, message);
}

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
    if (!errored)
    {
        foreach (KeyValuePair<string, object> arg in Args)
        {
            if (arg.Value == null || read.Contains(arg.Key)) continue;
            if (bareTrue && arg.Key == "value") continue;
            loader.Warn(Path + "." + arg.Key, "not used here (misspelled, or not a field of this effect or condition)");
        }
    }
    // Entries children report for themselves (each skips itself only if it errored).
    foreach (Spec child in children) child.ReportUnread();
}

// Built-ins that take a whole condition / item filter as their argument (used by Step 3b):
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

// CompileSharedCall: first line `call.MarkAllRead();` — unknown params are already problems there. (Redundant once the errored-spec skip
// covers its own Error path, but harmless; keep.)

// Builtins.cs — say: after reading tone. Core calls Warn through the concrete Spec, not ISpec, so Core never depends on the new
// interface member being present in whichever ExpeditionFramework.Api.dll copy got loaded:
if (Array.IndexOf(Schema.Tones, tone) < 0) ((PackLoader.Spec)c).Warn("unknown tone \"" + tone + "\" (known: " + string.Join(", ", Schema.Tones) + ")");

// ExpeditionApi.cs ISpec summary: add "Keys an effect never asks for while compiling are reported as warnings, so read every key before returning the effect. A bare true ({\"my:effect\": true}) counts as no arguments."
```

~~`say` calling `c.Warn(...)` through `ISpec`~~ — superseded (blue team F12): a pack's older Api.dll copy loading first would throw `MissingMethodException` at compile.
~~`MarkAllRead()` inside no-arg built-ins~~ — rejected alternative to `bareTrue` (blue team F1): hides `{"next_room": {"typo": 1}}` and does nothing for C# no-arg effects.

Sim tests (new `TestUnreadArgs()`):
- `WarningsIn(rooms r {text k, enter [{set {var run.x, valu 1}}]})` → warning at `enter[0].set.valu`.
- C# effect reading `amount` only; data `{t:drain {amount 1, amout 2}}` → exactly 1 warning (`amout`).
- C# effect reading nothing given scalar `{t:ping: 3}` → warning at `.value` (non-`true` scalar still warns).
- Bare-`true` exemption: `WarningsIn` → 0 warnings (and `ProblemsIn` → 0) for room `enter [{next_room: true}]`, an interaction `where`/`when` using `{fits: true}`, and an intent/condition site using `{intent_interruptible: true}` — mirror the live sites (framework `ExpeditionFramework/main.json` `next_room`/`fits`/`reload: true`; sample `main.json` `intent_interruptible: true`) so each compiles without problems. C# no-arg effect given `{t:ping: true}` → 0 warnings.
- Errored spec: `{set {var "bogus", value 1}}` → exactly 1 problem, 0 warnings; `{set {var "run.turn", value 1}}` → exactly 1 problem, 0 warnings (exercises the runtime plan's engine-var `c.Error` path — that plan lands first).
- Shared effect call with unknown param → still exactly 1 problem, 0 warnings (no double report).
- `say {text k, tone "loud"}` → 1 warning.
- `TestPublicApi`: make `tour` read `n` eagerly (`int total = entries.Sum(e => e.Number("n", 0));` before `return s =>`, closure uses `total`) and add `Check(content.Warnings.Count == 0, ...)`.

**Rationale:** Tracking inside `Spec` covers every built-in and every C# `ISpec` consumer with no per-effect tables to maintain. Only `PackLoader.Pool`/`UseOf`/`LineFrom` read `Args` behind `Spec`'s back, so only those four accessors need explicit marks. `bareTrue` is the narrowest exemption that keeps the framework's own `{"x": true}` idiom clean (V1/SC9 gate) while every other unread scalar still warns. The errored skip is general (built-ins and C# compilers alike), so no per-compiler "read before error" edits are needed.

**Verification:** V1, V2, V3, V5.

**If this fails:** revert `Spec` tracking and the `say` edit; Steps 1–2 stand. If only false positives remain from one accessor, fix its `Mark` list. If a false positive comes from a no-arg built-in given a non-`true` scalar in shipped content, triage it in Step 7 (content mistake) — do not widen the exemption without recording it.

---

### Step 3b: `not`/`carrying` object form (SC8) — decided: fix

**Objective:** `not`/`carrying` accept the object form authors already write; `null`/empty argument → clear problem.
**Confidence:** High
**Depends on:** Step 3 (`WholeCondition`/`WholeItems`, read tracking)
**Decision:** human chose **fix** over warn-only (blue team F5), accepting the gameplay changes in § Impact Analysis. ~~Warn-only variant (`not`/`carrying` with no `value` key → warning, behavior unchanged)~~ — rejected; not to be implemented.

**Files:** `src/Core/Builtins.cs`, `tests/Sim/Program.cs`

**Changes:**

```csharp
// Builtins.cs — not. Before:
r.Conditions["not"] = c => { Condition inner = c.Condition("value"); return s => !inner(s); };
// After:
r.Conditions["not"] = c =>
{
    var spec = (PackLoader.Spec)c;
    if (c.Has("value"))
    {
        Condition inner = c.Condition("value");
        return s => !inner(s);
    }
    // {"not": null} and {"not": {}} name no condition; without this they'd surface as a confusing "unknown condition value".
    if (spec.Args.Count == 0 || spec.Args.ContainsKey("value"))
    {
        c.Error("needs a condition");
        return s => false;
    }
    // {"not": {"scene": "combat"}} arrives as the condition's own fields, not under "value".
    Condition whole = spec.WholeCondition();
    return s => !whole(s);
};

// carrying — same three-way shape:
//   c.Has("value") → c.Items("value");
//   spec.Args.Count == 0 || spec.Args.ContainsKey("value") → c.Error("needs an item filter"); return s => false;
//   else → spec.WholeItems();
// then the existing `return s => s.Run.Inventory.Items.Any(i => filter(i, s));`.
```

Sim tests (new `TestNotCarryingObjectForm()`, called from `Main` after `TestUnreadArgs`):
- `not` object form: area `hidden_when {not {scene combat}}` → evaluated on `new MapContext(...)`.Root → `true` (scene is `map`); `{not {value {scene combat}}}` → `true`.
- `{not {}}` → exactly 1 problem containing `needs a condition`, 0 warnings; `{not: null}` → same.
- `carrying` object form: `{carrying {all ["#melee", "#in_hand"]}}` with a `FakeBridge` holding a melee-in-hand item → true; holding only a non-melee item → false. `{carrying: null}` → 1 problem containing `needs an item filter`.

**Rationale:** Targeted (2 built-ins with a natural object form); a generic "object means value" rule would change `ValueOf`/`Require` behavior for every value-only effect. Split from Step 3 so the gameplay-changing edit is one revertible unit.

**Verification:** V1, V2 (NER behavior delta expected — free search out of combat), V3.

**If this fails:** revert the two built-in edits; Step 3 stands. If V1 fails only because the sample's `cellar_nerves` morale now fires and breaks a sim assertion, adjust that assertion (accepted gameplay change), record it in the execution log.

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
// item_stats "#tag" keys are left out on purpose: they only apply to items that already have the tag, so counting them would let
// the same misspelling in a filter and an item_stats key hide each other.
private HashSet<string> KnownItemTags() => knownItemTags ??= new HashSet<string>(Schema.EngineItemTags
    .Concat(content.ItemGroups.Values.SelectMany(g => g))
    .Concat(content.ItemStats.Values.SelectMany(s => s.Tags)));
// Defer already charges the check to the pack being compiled; Finish() sets knownItemTags = null at its end.
```

~~`.Concat(content.ItemStats.Keys.Where(k => k.StartsWith("#")).Select(k => k.Substring(1)))`~~ — superseded (blue team F11): `#key` consumes a tag (`RunItem.ApplyStats`), never defines one. Extra warning for an `item_stats` `#key` naming an unknown tag: not added (new scope).

Check A2 first: read `ItemReader.ReadWeapon/ReadStack` and confirm `Schema.EngineItemTags` equals the tags added there.

Sim tests: `WarningsIn(rooms r {text k, interactions [{id x, label k, uses "#explosve", do []}]})` → 1 warning containing `explosve`; same with `#melee` → 0; with `item_groups {bright [torch]}` + `#bright` → 0; with `item_stats {"#loud": {tags ["noisy"]}}` + `#noisy` → 0.

**Rationale:** Deterministic once all packs are compiled; mirrors the existing deferred `tag` pool check (`PackLoader.Pool`).

**Verification:** V1, V3.

**If this fails:** revert the `#` branch edit and `KnownItemTags`.

---

### Step 5: `lint` command in the framework sim (Tooling A, offline home)

**Objective:** `dotnet run --project tests/Sim -- lint <pack folder> [--mod <id>]... [--strict]` checks a pack that depends only on the framework. ~~checks any pack against the framework~~ — narrowed (blue team F6): lint loads only framework + target, and `--mod` toggles `HasMod` without loading anything, so a pack building on another non-framework pack would get false "no … named" errors. `--with <folder>` deferred until the first such add-on pack exists (no such pack today).
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
- § Vocabulary: line tones `good, bad, dim, heading, enemy, hint, plain`; intel tones `good, bad, mixed`. `not` and `carrying` take their condition/filter directly (`{"not": {"scene": "combat"}}`) or under `value`; with nothing (`null`/`{}`) it's a problem.
- § The C# API: `spec.Warn`; "read every key while compiling — keys not asked for by the time the compiler returns are reported as unused; a bare `true` counts as no arguments, and a spec you called `Error` on isn't reported". Add: "`ISpec` only ever gains members; the framework's own `ExpeditionFramework.Api.dll` must be the copy that loads — a pack shipping an older copy that wins the load breaks calls to newer members such as `Warn`."
- § Building and testing: `dotnet run -c Release --project tests/Sim -- lint ../MyPack [--mod gunsmith_expansion] [--strict]` — what it checks, exit codes, and that it checks a pack that depends only on the framework (`--mod` only answers `mod(...)` checks; it loads no other pack).
- Each V9 probe phrase must sit on one README line (README is hard-wrapped; `grep -F` misses a split phrase).

**Verification:** V9 (presence); judgment: wording matches behavior.

**If this fails:** revert README.

---

### Step 7: NER acceptance + triage

**Objective:** Prove the acceptance consumer still loads; classify every warning it now gets.
**Confidence:** Medium — SC5 count on NER unknown until run.
**Depends on:** Steps 1–5 (incl. 3b)

**Files:** none edited in NER. Framework files only if a false positive is found (fix goes in the owning step's code).

**Changes:** Run V2 and V7. V7 is the warning source — the NER sim (V2) fails only on problems and never prints warnings, so SC10's triage input is V7's `WARN` lines. For each NER warning: (a) framework false positive (key is read at run time, shape table missing a key, mark list incomplete) → fix in framework, rerun; (b) genuine NER mistake → add to the execution log's follow-up list as `NER: <file path from warning> — <what to change>`. Also record the accepted gameplay deltas from SC8 (NER free-search `if` now fires out of combat; framework `punch` now offerable to melee kits; sample `cellar_nerves` morale now fires).

**Rationale:** NER is the only large real consumer; triage separates framework bugs (fix now) from content bugs (outside scope).

**Verification:** V2, V7; judgment: triage table complete.

**If this fails:** if NER problems appear that can't be classified, set plan Blocked and escalate with the list.

---

## Verification Plan

Commands run from the framework repo root (bash/PowerShell identical).

### Deterministic Suite

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V1 | SC1–SC9, SC11 tests; Steps 1–5 (incl. 3b) | `dotnet run -c Release --project tests/Sim` | exit 0; last line `All checks passed` | any new check misfiring (incl. `{"x": true}` false positives on framework/sample content); own content warning/problem; unit test regressions |
| V2 | SC10 (problems only), SC8 downstream | `dotnet run -c Release --project ../NightExpeditionsReborn/tests/Sim` | exit 0; last line `All checks passed` | NER voided by new problems; behavior break. Prints no warnings — warning evidence is V7 |
| V3 | SC1, SC12 | `dotnet build -c Release ExpeditionFramework.csproj -p:InstallDir=obj/verify-install/` | exit 0, `0 Error(s)` | `PackRegistry`/`ISpec` change breaks the game build |
| V4 | SC1 | `grep -c "out Dictionary<string, List<string>> warnings" src/Core/PackSet.cs` | ≥ 1 | overload missing |
| V5 | SC2, SC3, SC6 | `grep -c "CheckShape(" src/Core/PackLoader.cs` | ≥ 18 (1 definition + 17 call sites in the Step 2 table) | call sites skipped |
| V6 | SC11 | `dotnet run -c Release --project tests/Sim -- lint samples/SampleArea --strict` | exit 0; output contains `lint sample_area: 0 error(s), 0 warning(s)` | lint misreads a clean pack |
| V7 | SC10 (primary evidence: errors + the `WARN` lines triaged in Step 7) | `dotnet run -c Release --project tests/Sim -- lint ../NightExpeditionsReborn` | exit 0; output line `lint night_expeditions_reborn: 0 error(s), …`; every `WARN` line appears in the Step 7 triage table | NER has errors under game-like load; untriaged warnings |
| V8 | SC11 | `dotnet run -c Release --project tests/Sim -- lint` | exit 2; usage printed | arg handling |
| V9 | SC13 | `for p in "warning:" "-- lint" "plain" "spec.Warn" "only ever gains" "depends only on the framework"; do grep -c -F -e "$p" README.md; done` | six counts, each ≥ 1 (last two are 0 at `8152b9b`) | docs step skipped |

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
| SC5 | V1 (`TestUnreadArgs`) | built-in + C# unread keys warned; bare `true` → 0; `{t:ping: 3}` → 1; errored `set bogus`/`set run.turn` → 1 problem, 0 warnings; shared call not doubled |
| SC6 | V1 (`TestShapes`, `TestUnreadArgs`) | unknown tones warned |
| SC7 | V1 (Step 4 tests) | `#explosve` warned; known tags silent |
| SC8 | V1 (`TestNotCarryingObjectForm`), V2 | object-form `not`/`carrying` evaluate correctly; `null`/`{}` → `needs a condition`/`needs an item filter` |
| SC9 | V1, V6 | 0 problems, 0 warnings for framework + sample |
| SC10 | V7 (warnings + errors), V2 (problems, behavior), triage judgment | NER 0 problems; every V7 warning classified |
| SC11 | V1 (`TestLocaleParity`), V6, V7, V8 | lint output and exit codes |
| SC12 | V3 | game assembly builds |
| SC13 | V9, judgment | documented |

---

## Rollback Plan

1. Revert the modified files to the pre-execution commit (`git -C <repo-root> checkout <base> -- <files>` or abandon the ephemeral branch via `projex-abandon`).
2. Delete `src/Core/Schema.cs` and `tests/Sim/Lint.cs` if untracked on base.
3. Rerun V1 and V2 to confirm the baseline.

---

## Revision Log

- **2026-11-12:** Applied blue team Must Fix items owned by this plan — trigger: 2611120507-phase1-hardening-plans-blueteam.md § Defense Plan (dispositions settled by human + orchestrator). F2: Dependencies record first commit `8152b9b` (full tree) + `ls-files` precondition. F5: decisions recorded (SC8 fix + accepted gameplay deltas; wrong-typed scalars = problems; misplaced `weight`/`when`/`tags` = warnings); SC8 split out of Step 3 into Step 3b (warn-only variant recorded as rejected); Impact/Risks/Step 7 list the sample `cellar_nerves` delta. F20: order fixed — runtime plan first (Requires), this plan second. F1: `Spec.bareTrue` exempts the `value` synthesized from scalar `true` in `ReportUnread`; `MarkAllRead`-in-no-arg-built-ins rejected; tests `next_room`/`fits`/`intent_interruptible: true` → 0, `{t:ping: 3}` → 1. F4: `Spec.Error` sets `errored`; `ReportUnread` skips an errored spec's own keys; tests `set bogus` / `set run.turn` → 1 problem, 0 warnings. F14: A1 widened to (a) eager reads, (b) `true` exemption, (c) errored skip. F19: `not`/`carrying` with `null`/`{}` → `needs a condition` / `needs an item filter`; tests added. F12: Core `say` warns via `((PackLoader.Spec)c).Warn`; README: `ISpec` only grows, framework Api.dll must win. F11: `item_stats` `#key` line dropped from `KnownItemTags`; SC7/A2 corrected. F10: V2 marked problems-only, V7 named SC10's warning evidence (SC10, Step 7, acceptance table). F6: lint claim narrowed to packs depending only on the framework (Summary, SC11, Step 5, Step 6); `--with` deferred to Follow-ups. V9 gains two probes; one-line probe rule added. Status stays `Ready` — all decisions recorded.

---

## Notes

### Risks
- C# mods that read `ISpec` keys lazily get spurious warnings: documented rule (Step 6); warnings never block loading.
- SC8 changes gameplay in NER (free searches now consumed out of combat), wherever framework kit `punch` is used, and in the sample (`cellar_nerves` morale fires): human-accepted bug fixes; flag in walkthrough. NER has no VCS — only rollback is reverting the framework commit.
- Warning duplication: one typo inside an `extends` template / `use` shared interaction warns once per consumer (Accept Risk, blue team F9); reverse with per-pack message de-dup if Step 7 triage or author logs show noisy duplicates.
- C#-pack side of `ISpec.Warn`: a C# pack calling `Warn` while an older Api.dll copy is loaded throws (Accept Risk; Core itself no longer exposed — Step 3 cast). Documented in Step 6.
- Warnings caused by another pack's patch are charged to the patched pack (`current` = target during compile): acceptable; patch problems keep existing blame rules.
- `Schema.Tones`/`EngineItemTags` duplicate lists that live in `src/Game` (`ExpeditionLocation.ToneColors`, `ItemReader`): comments name the owning symbols; a future change there must update `Schema`.

### Split decision
Phase 1 was split into two plans: this one (loader/lint) and 2611120258-runtime-safety-fixes-plan.md (independent runtime fixes). For this plan: `No split — heuristic tripped (~600 lines, 7 steps; 8 with Step 3b after revision) but steps tightly coupled (rationale: Steps 2–5 all report through Step 1's channel; Steps 2–3 share the `LineFrom`/`UseOf` signature change; Step 7 validates all of them together).`

### Open Questions
- (none)

### Decisions Recorded (human, 2026-11-12 — blue team F2/F5/F20)
~~Decisions taken by default (user may override before execution)~~ — superseded: all three confirmed by the human; no longer overridable defaults.
- Phase 0: full-tree first commit `8152b9b` on `main` (incl. `.projex/`).
- SC8: `not`/`carrying` object form is **fixed** (Step 3b), not warn-only. Gameplay changes accepted: NER free searches out of combat, framework `punch` offerable to melee kits, sample `cellar_nerves` morale firing.
- Wrong-typed scalars are **problems** (E2), not warnings, with the A4 escalation rule as safety valve.
- `weight`/`when`/`tags` on kinds other than rooms/events/encounters/enemies → **warning** only.
- Execution order: 2611120258-runtime-safety-fixes-plan.md first, then this plan. Worktree: No (unchanged).

### Follow-ups (deferred, not in this plan)
- Lint `--with <folder>` to load a non-framework dependency — build when the first add-on pack exists (blue team F6).
