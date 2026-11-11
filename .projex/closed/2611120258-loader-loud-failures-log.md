# Execution Log: Loader Loud Failures

> **Status:** Complete
> **Started:** 20261112 06:18
> **Repo Root:** ExpeditionFramework (repo root)
> **Plan File:** .projex/2611120258-loader-loud-failures-plan.md
> **Parent:** 2611120258-loader-loud-failures-plan.md
> **Base Branch:** projex/2611120258-runtime-safety-fixes

## Pre-Check Results
```
BRANCH=projex/2611120258-runtime-safety-fixes
PLAN_REL=.projex/2611120258-loader-loud-failures-plan.md

PASS  Plan is committed (8721cfc projex(revise): apply blue team Must Fix items to Phase 1 hardening plans)
PASS  Working tree is clean

PRE-CHECK PASSED
```

Stacked execution (orchestrator): plan's "runtime plan executed and closed (merged to main) first" dependency satisfied by stacking — 2611120258-runtime-safety-fixes-plan.md executed, all criteria passed, its branch left open; this branch is cut from it (HEAD `fd2699a` + status commit `8bb29f8`). Precondition `git ls-files src/Core/PackLoader.cs tests/Sim/Program.cs README.md` → all three listed ✓. Baseline: framework sim `All checks passed` (exit 0); NER sim `All checks passed` (exit 0).

## Steps

### 20261112 06:22 - Step 1: Warning channel (E9)
**Action:** `Model.cs` `Content.Warnings`. `PackLoader.cs`: `warningsByPack` + `WarningsByPack` + `Warn(at, msg)` (charged to `current`, like `Problem`); `Spec.Warn(msg)`. `api/ExpeditionApi.cs` `ISpec.Warn`. `PackSet.cs`: `Compile(out problems, out warnings)` overload, old `Compile(out problems)` delegates; patch acceptance untouched (Problems-only). `PackRegistry.LoadAll`: logs `warning: …` per pack before the problems check (areas still added) + warnings of packs outside `packs` (helper `LogWarnings`). Sim: `Main` fails on `content.Warnings`; `WarningsIn(json)` helper; `TestWarningsDontVoid` (pack `a` + patcher `b` whose C# effect `b:warn_me` calls `spec.Warn("x")` → no problems, 1 warning under `a`, patch kept, area kept).
**Result:** V1 `All checks passed` exit 0 | V2 `All checks passed` exit 0 | V3 build success, 0 errors, 0 warnings, exit 0 | V4 `grep -c` → 1.
**Status:** Success

### 20261112 06:40 - Step 2: Schema shapes — unknown keys, scalar types, tones
**Action:** New `src/Core/Schema.cs`: `Field` enum, `Shape` (+`With`), `Schema` (`Tones`, `IntelTones`, `EngineItemTags`, `Sections` for the 10 definition kinds — `Pooled` base with weight/tags/when only for enemies/encounters/events/rooms — and the 17 sub-shapes of the plan table; `Mismatch`). `PackLoader.cs`: `CheckShape(n, shape, at)` (unknown key → warning listing known keys; wrong type → problem; unknown tone → warning) + `UnknownTone` helper; called at all 17 sites of the plan table (Section after Extend, AddLootEntry, cart stock, BehaviorList, encounter `{id,count}`, luck, layout slot, packing, packing need, intel, hud, hud row, hud enemies (only if present), interaction (after `use` merge), confirm, `UseOf` (select form, when `checkShape`), `LineFrom` (when `shape != null`, before the `cases` branch)). Signatures: `LineFrom(v, pack, at, Shape)` — `Schema.Line` from LineOf/RichArg tail/`Spec.Line`, `Schema.Case` from CasesLine, `Schema.HudPart` from CompileHudPart, `null` from `Spec.InlineLine`; `UseOf(node, pack, at, bool checkShape)` — true from interaction uses/with, hud `each_item`, `Spec.Use(key)`; false from `Spec.Use(null)`. Str/Int/Bool fallbacks unchanged. Sim `TestShapes` (all 7 plan cases: typo warn+0 problems, `unlocked_by` list, weight expr, flag string, status weight, intent/line/intel tones, interaction `gruop`).
**Result:** V1 `All checks passed` exit 0 (framework + sample: 0 problems, 0 warnings) | V2 `All checks passed` exit 0 (no NER problem from the type branch → A4 escalation not triggered) | V3 exit 0, 0 errors | V5 `grep -c "CheckShape(" src/Core/PackLoader.cs` → 18. Judgment (self-verified, side by side): each shape lists exactly the keys its `Compile*` reads (CompileStatus/Intent/Loot/Cart/Enemy/Encounter/Event/Room/Kit/Area, AddLootEntry, BehaviorList, CompileEncounter, luck, RoomPool/Pool `room|rooms|from|tag|pool`+count/unique, CompilePacking, CompileIntel, CompileHud/HudPart, CompileInteraction, confirm, UseOf, LineFrom/CasesLine). A2/A3 checked: `ItemReader` adds exactly weapon, in_hand, firearm|melee, has_rounds, ammo, armor_piercing, non_lethal; `ExpeditionLocation.ToneColors` keys good/bad/dim/heading/enemy/hint; `IntelCards.Tone` good/bad/else mixed. NER warning count deferred to Step 7 (needs Step 5 lint).
**Status:** Success

### 20261112 06:55 - Step 3: Spec read tracking + `say` tone
**Action:** `PackLoader.Spec`: `read` set, `children`, `bareTrue` (ctor: `args is bool b && b`), `errored` (set in `Error`), `Mark`, `MarkAllRead`, `ReportUnread` (skips null values, read keys, the bare-`true` `value`, and an errored spec's own keys; always recurses into Entries children), `WholeCondition`/`WholeItems` (for Step 3b). Accessors mark: `Has`/`Text`/`Flag`/`Number`/`ListAt` (→ Strings/Conditions)/`Entries` (+ children); Value/Effect/Condition/Items/IsText/Line via `Has`; `InlineLine` marks text,key,cases,args,variants,tone (own form) or value; `Use(null)` marks select,each,consume,limit,order,where (+ all when no `select`); `PoolOf` marks value,pool,from,ids,tag before `loader.Pool`. `PackLoader.Effect`/`Condition` call `spec.ReportUnread()` after the compiler returns; `PreviewOf`'s DieCheck spec does not (comment says why). `CompileSharedCall` first line `call.MarkAllRead()`. `Builtins.say`: unknown tone → warning via `((PackLoader.Spec)c).Warn(PackLoader.UnknownTone(...))` (comment names the rejected `ISpec.Warn` route + MissingMethodException). `ISpec` summary documents the read rule + bare-`true`. Sim: `Compiled(json, registry)` helper (`WarningsIn` now uses it); `TestUnreadArgs` (set `valu`; C# `t:drain` `amout`; `t:ping: 3` → `.value`; `t:ping: true` → 0; bare-true sites `next_room`/`end_fight`/`reload`/`fits`/`intent_interruptible` → 0 warnings 0 problems; `set bogus` / `set run.turn` → 1 problem 0 warnings; shared call unknown param → 1 problem 0 warnings; `say` tone `loud` → 1 warning). `TestPublicApi`: `tour` reads `n` eagerly (`total`), + `content.Warnings.Count == 0` check.
**Result:** V1 exit 1 with exactly 2 failures, both the `not` object-form bug Step 3b fixes (true positives, as the plan's Problem section predicts): `expedition_framework/…main.json.kits.melee.interactions[1].where.not.carrying: not used here …` and `sample_area/…main.json.kits.cellar_nerves.on.defeated[0].for_each.where.all[1].not.has_tag: not used here …`. Every other check (incl. all new `TestUnreadArgs` cases and `TestPublicApi` warnings check) passed. V2 `All checks passed` exit 0. V3 exit 0. SC9 gate deferred to Step 3b (see Deviations).
**Status:** Success (V1 zero-warning gate closes in Step 3b)

### 20261112 07:05 - Step 3b: `not`/`carrying` object form (decided: fix)
**Action:** `Builtins.cs`: `not` → three-way (`Has("value")` → old path; `Args.Count == 0 || ContainsKey("value")` → `c.Error("needs a condition")`, `s => false`; else `spec.WholeCondition()`). `carrying` same shape (`needs an item filter`; else `spec.WholeItems()`), then the existing `Inventory.Items.Any(...)`. Sim `TestNotCarryingObjectForm`: `hidden_when {not {scene combat}}` and `{not {value {scene combat}}}` → 0 problems/warnings, true on `MapContext.Root`; `{not {}}` / `{not null}` → exactly 1 problem `needs a condition`, 0 warnings; `{carrying {all [#melee, #in_hand]}}` → clean, true with `FakeBridge` knife-in-hand, false after removing the knife (gun in hand only); `{carrying null}` → 1 problem `needs an item filter`.
**Result:** V1 `All checks passed` exit 0 — the 2 Step 3 warnings are gone (framework `kits.melee` punch `where`, sample `cellar_nerves` morale `where` now compile as written). Sample play outcomes shift baseline `knocked_out=3762, cleared=1062, left=176` → `knocked_out=3742, cleared=1049, left=209` (accepted gameplay delta: non-boss humans now flee at 25% when an ally falls); no sim assertion needed adjusting. V2 `All checks passed` exit 0. V3 exit 0.
**Status:** Success

### 20261112 07:12 - Step 4: `#tag` item-filter lint
**Action:** A2 confirmed first (`ItemReader.ReadWeapon`: weapon, in_hand, firearm|melee, has_rounds; `ReadStack`: ammo, armor_piercing, non_lethal = `Schema.EngineItemTags`; `RunItem.ApplyStats` applies `#tag` stats only to items already tagged). `PackLoader.Items` `#` branch: `Defer` warning `no item has the tag X (not a game tag, item group or item_stats tag)` when the tag is not in `KnownItemTags()`. New `knownItemTags` cache + `KnownItemTags()` = `EngineItemTags` ∪ `ItemGroups` group names ∪ `ItemStats` `tags` (item_stats `#key` deliberately excluded, comment says why); `Finish()` resets the cache at its end. Sim `TestItemTags`: `#explosve` → 1 warning; `#melee` → 0; `item_groups {bright [torch]}` + `#bright` → 0; `item_stats {"#loud": {tags [noisy]}}` + `#noisy` → 0.
**Result:** V1 `All checks passed` exit 0 (framework + sample `#tags` all known) | V2 `All checks passed` exit 0 | V3 exit 0.
**Status:** Success

### 20261112 07:25 - Step 5: `lint` command in the framework sim
**Action:** New `tests/Sim/Lint.cs` (`Lint.Run(frameworkRoot, args)`: folder = `args[0]`, `--mod <id>` repeatable, `--strict`; missing folder / unknown arg → usage, exit 2; `<ID>` from `manifest.xml` (none → `ERROR no <ID> in manifest.xml`, exit 2); `PackSet` framework (unless id is the framework) + pack, `Compile(out problems, out warnings)`; `ERROR`/`WARN` lines for every pack; missing English text for the pack's and the framework's `TextKeys`; parity of each other `Localization/*.csv` vs `en.csv` → `WARN`; summary `lint <id>: N error(s), M warning(s)`; exit 1 on errors or `--strict` + warnings. `ReadCsv` (skips blank/`#` lines, first-comma split; missing file → empty), `ParityProblems` (missing / extra keys, `{n}` placeholder set mismatch). `Sim.csproj` compiles `Lint.cs`. `Program.Main(string[] args)` dispatches `lint`. Sim `TestLocaleParity` (3 entries for the plan's dicts; identical → 0).
**Result:** V1 `All checks passed` exit 0 | V6 `lint sample_area: 0 error(s), 0 warning(s)` exit 0 | V8 usage printed, exit 2 | V7 `lint night_expeditions_reborn: 0 error(s), 0 warning(s)` exit 0 (also 0/0 with `--mod gunsmith_expansion --strict`; `lint . --strict` on the framework itself → 0/0). Non-vacuity probe (scratch pack outside the repo with one mistake of each kind): `1 error(s), 6 warning(s)`, exit 1 — `unlocked_by` list (ERROR), `txet` unknown key, `set.valu` unread, `say` tone `loud`, `#explosve` tag, zh.csv extra key + placeholder mismatch.
**Status:** Success

### 20261112 07:35 - Step 6: README
**Action:** `README.md` § Making a pack: "A pack with any problem adds no areas." → problems (broken structure, wrong types, dangling references) stop areas; warnings (unknown keys with known list, unread effect/condition args, unknown tones, `#tags` no item can have, `weight`/`when`/`tags` on kinds that ignore them) logged as `warning: …`, pack still loads. § Vocabulary: `not`/`carrying` take condition/filter directly or under `value`, `null`/`{}` → problem; `{"x": true}` = no arguments; line tones `good, bad, dim, heading, enemy, hint, plain`, intel `good, bad, mixed`. § The C# API: `spec.Error`/`spec.Warn`; read-every-key rule (bare `true` exempt, errored spec not reported); `ISpec` only ever gains members, framework's own Api.dll must be the loaded copy. § Building and testing: `lint` command line, what it checks, framework-only-dependency limit (`--mod` loads nothing), exit codes 0/1/2.
**Result:** V9 counts: `warning:` 1 | `-- lint` 1 | `plain` 1 | `spec.Warn` 1 | `only ever gains` 1 | `depends only on the framework` 1 (each phrase on one line). Judgment: wording matches implemented behavior (checked against Steps 1–5 code and the lint probe output).
**Status:** Success

### 20261112 07:42 - Step 7: NER acceptance + triage
**Action:** Ran V2 (`dotnet run -c Release --project ../NightExpeditionsReborn/tests/Sim`) and V7 (`dotnet run -c Release --project tests/Sim -- lint ../NightExpeditionsReborn`) on unchanged NER files. Located the object-form `not` sites in all three packs.
**Result:** V2 `All checks passed` exit 0 (0 problems, no never-offered options, no stuck runs). V7 `lint night_expeditions_reborn: 0 error(s), 0 warning(s)` exit 0 (also 0/0 with `--mod gunsmith_expansion --strict`, Step 5). Triage table: **no NER warnings to triage** — 0 framework false positives, 0 NER content bugs (matches the planning survey's zero prediction for SC2–SC4/SC6/SC7; SC5 also zero on NER). `kit.json:165` `{ "not": "#in_hand" }` is the item-filter `not` (`PackLoader.Items`), unaffected. Accepted gameplay deltas from SC8 (now live): NER `rules.json:352` free-search `if` `when: [run.free_searches > 0, {not: {scene: combat}}]` now holds out of combat → `run.searches += run.free_searches`, `free_searches = 0`, `ne_free_search` line (was never true); framework `ExpeditionFramework/main.json:188` kit `melee` interaction `where: {not: {carrying: {all: [#melee, #in_hand]}}}` now offered when no melee weapon is in hand (was never offered) — NER's kits don't include it; sample `main.json:215` `cellar_nerves` morale `{not: {has_tag: {tag: boss}}}` now matches non-boss humans (25% flee when an ally falls; sample outcome mix shifted, Step 3b).
**Status:** Success

### 20261112 07:50 - Completion: full verification, criteria, review
**Action:** Fresh full suite V1–V9; criteria checked one by one; plan re-read for spec compliance; quality review of the branch diff (`projex/2611120258-runtime-safety-fixes..HEAD`), incl. a source-hygiene grep of added lines for projex ids / step labels / line numbers / banners (0 hits). Cleanup: scratch lint probe pack and temp sim outputs removed (outside the repo); `obj/verify-install/` is gitignored build output, left in place.
**Result:** V1 `All checks passed` exit 0 (outcomes `knocked_out=3742, cleared=1049, left=209`) | V2 `All checks passed` exit 0 | V3 build exit 0, 0 errors | V4 1 | V5 18 | V6 `lint sample_area: 0 error(s), 0 warning(s)` exit 0 | V7 `lint night_expeditions_reborn: 0 error(s), 0 warning(s)` exit 0 | V8 usage, exit 2 | V9 six probes each 1. Working tree clean.
**Status:** Success

Success criteria:
- SC1 ✓ `Content.Warnings`; logged per pack (`PackRegistry.LogWarnings`, before the problems check); `TestWarningsDontVoid` (warning kept, patch kept, area kept); V3, V4. In-game log check not run (needs the game).
- SC2 ✓ `TestShapes` (`rooms.a.txet` + `known:`; interaction `gruop`); V5 = 18; shape table read against every `Compile*`.
- SC3 ✓ `TestShapes`: `unlocked_by` list, `weight "1+1"`, `interruptible "yes"` → problems.
- SC4 ✓ `TestShapes`: `statuses.s.weight` warned.
- SC5 ✓ `TestUnreadArgs`: built-in `valu`, C# `amout`, `t:ping: 3`; bare `true` → 0 (built-in + C#); `set bogus`/`set run.turn` → 1 problem 0 warnings; shared call not doubled; `TestPublicApi` 0 warnings.
- SC6 ✓ intent tone, line tone, intel tone (`good` ok / `great` warned with `good, bad, mixed`), `say` tone.
- SC7 ✓ `TestItemTags`: `#explosve` warned; `#melee`, group, item_stats tag silent.
- SC8 ✓ `TestNotCarryingObjectForm`; framework/sample/NER object-form sites now compile as written; V2 passes.
- SC9 ✓ V1 (main content: 0 problems, 0 warnings), V6.
- SC10 ✓ V2 exit 0; V7 0 errors, 0 warnings → triage table empty (Step 7).
- SC11 ✓ `TestLocaleParity`, V6, V7, V8, non-vacuity probe (Step 5).
- SC12 ✓ V3.
- SC13 ✓ V9 + wording review (Step 6).

## Deviations

- **Step 3 V1 gate:** the plan lists V1 for Step 3, but with read tracking alone framework + sample content carries 2 true-positive warnings — the `not` object-form instances Step 3b fixes. Committed Step 3 with those 2 known failures recorded; Step 3b must bring V1 back to `All checks passed`. No code adjusted to hide them.
- **Step 5 `HasMod`:** plan sketch says `HasMod = mods.Contains`; implemented as `mod == expedition_framework || mod == <pack id> || mods.Contains(mod)`, because in the game both are always installed and enabled (`PackRegistry` asks `ModLoader.Mods`), so `mod(...)` and include `requires` answer as in-game. No effect on the three packs (all lint 0/0 either way). Unknown lint arguments also print usage + exit 2 (plan only specified the missing-folder case).
- **Commit trailer:** Step 1 commit `a5b21e1` lacks the `Co-Authored-By` trailer (amend blocked by the user's git safety hook); later commits carry it.

- **Status commit on base:** the `In Progress` status commit `8bb29f8` landed on the base branch `projex/2611120258-runtime-safety-fixes` (spec: status change is committed on base before branching), so that still-open runtime branch now carries one extra doc-only commit.

## Issues Encountered

- `stage-n-commit.ps1` called from Git Bash via `pwsh -File` split the `--trailer Projex: …` value into a stray file argument ("Files not found or unchanged"); called from PowerShell directly it works. No partial commit resulted.
- Context-mode output filtering hid some long diffs/file dumps; worked around with targeted reads.

## Follow-ups

- NER content bugs found: **none** (0 warnings, 0 errors under lint).
- Lint `--with <folder>` for packs depending on another non-framework pack (carried from plan).
- In-game check that `warning:` lines appear in the log and areas still load (SC1 judgment; needs the game).

## User Interventions

None.
