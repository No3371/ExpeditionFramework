# Pack Memory: Sample Carried Counter + README [PATCHED]

> **Status:** Complete
> **Author:** Claude Opus 5.5 (plan)
> **Parent:** 2611121459-pack-memory-sprint.md
> **Sprint:** 2611121459-pack-memory-sprint.md — iteration 5
> **Source:** Sprint iteration objective (`2611121459-pack-memory-sprint.md` § Current Position, item 5)
> **Related Projex:** 2611121459-pack-memory-sprint.md | 2611121506-pack-memory-runtime-store-plan.md | 2611121531-pack-memory-map-reads-plan.md | 2611121550-pack-memory-game-reads-plan.md | 2611121613-pack-memory-sim-memory-plan.md | 2611121626-pack-memory-sim-memory-audit.md | 2611121641-pack-memory-sample-readme-patch.md
> **Partial Execution:** whole plan completed via 2611121641-pack-memory-sample-readme-patch.md (commit 2dad985)
> **Worktree:** No — execute in the sprint work-root on `projex/sprint/2611121459-pack-memory`

---

## Summary

Last goal criteria of the sprint. (A) `samples/SampleArea` gains a carried counter: the Foreman's defeat adds 1 to `memory.foreman_beaten`; entering the office reads it and, when above 0, prints a line naming the count; `sim.json` gains a 4th row that seeds the counter, carries it across 500 runs and checks it with `expect`. (B) `README.md` documents memory, map-time reads (`hidden_when`, intel `when`), `game.day` / `game.clears` / `game.clears.AREA`, the read-never-written warning, `sim.json` `runs[n].memory`, and short C#/host notes. No code changes.

**Scope:** `samples/SampleArea/ExpeditionFramework/main.json` | `samples/SampleArea/Localization/en.csv` | `samples/SampleArea/sim.json` | `README.md`. One projex scope (repo root `.projex/`). NER untouched.
**Estimated Changes:** 4 files. Sample: 2 JSON edits + 1 CSV line + 1 sim row. README: 11 edits (1 new `##` section, 1 new `####` subsection, 9 line-level edits).

---

## Objective

### Problem / Gap / Need

- Sample has no memory (`grep memory. samples/` empty) → no in-repo `ef-sim` coverage of `runs[n].memory`; sprint goal requires "sample pack demonstrates a carried counter that `ef-sim` checks".
- README has no memory, `game.day`/`game.clears`, intel `when`, `runs[n].memory` or warning docs. Worse, two README claims are now incomplete/false: path heads list (README:168-169) omits `memory`; bots `when` "every other path and function works as in pack data" (README:381-382) — false for `memory.` (bot `when` compiles without a pack → `unknown path`; probed).

### Success Criteria

- [ ] SC1: Sample writes `memory.foreman_beaten` (Foreman `on.defeated`, `add` 1) and reads it (office `enter`, `if` + `say` with the count as arg); new text key `sa_foreman_grudge` in `en.csv`.
- [ ] SC2: `samples/SampleArea/sim.json` row 4 (`runs[3]`) has `memory: { start: {foreman_beaten: 1}, carry: true, expect: {foreman_beaten: {min: 2}} }`; `ef-sim run samples/SampleArea` passes; a mutated exact expect shows the counter really carried (`got 230`).
- [ ] SC3: The 3 existing sample rows and `tests/Sim`'s `Outcomes:` line are byte-identical to before (the new effects draw no random numbers).
- [ ] SC4: README documents every carry-forward item (list in Context) in plain English for pack authors; each claim matches code (claims table below); the two stale claims are fixed.
- [ ] SC5: Gates green: `tests/Sim` `All checks passed`; game-assembly build 0 warnings / 0 errors; `lint --strict` 0/0 on sample and NER; `ef-sim run` sample and NER pass; NER 4000-run rows identical to baseline.

### Out of Scope

- Any change under `src/`, `api/`, `tools/`, `tests/`. If a README claim can't be made true as written, change the README wording, not code.
- NER (`../NightExpeditionsReborn`) — read-only gate target.
- Optional audit minor (reject unreadable `start`/`expect` names like `a b`, `$$x`) — sprint left it out.
- Map-time memory in the sample (no intel / `hidden_when` in the sample) — README covers it with an example; sample keeps one demo.

---

## Context

### Current State (verified 2026-11-12 on sprint branch at `e5b1734`)

- Sample `main.json`: Foreman `on.defeated` = `[credits 2d20+10, set run.boss_down 1]` (line 116); office `enter` = one `fight` (line 201). `en.csv` 37 lines, ends with newline.
- `sim.json`: 3 rows (seeds 42/43/44), no `memory`. Current table (captured now):
  ```
  flooded_cellar     knife        random     2500     2.8%        91.6%       8.4%
  flooded_cellar     everything   random     2500    37.8%        54.6%      45.4%
  flooded_cellar     everything   cautious   2500     1.8%        54.5%      45.5%
  ```
- `tests/Sim` currently prints `Outcomes: knocked_out=3742, cleared=1049, left=209; longest run 28 steps` then `All checks passed`. Its `FakeBridge` gets a fresh `MemoryStore` per run (`tests/Sim/Program.cs` `FakeBridge` ctor), so the grudge line never fires there, but its `Loc` throws on a missing key — the CSV line is required.
- **Probe (scratch copy, discarded):** exactly the Step 1–3 sample edits, with row 4 `expect` set to `1`: rows 1–3 identical to above; row 4 printed `flooded_cellar     everything   random      500    38.2%        51.6%      48.4%`; `FAIL runs[3]: memory.foreman_beaten should be 1, got 230`; `lint --strict` 0/0. Bot `when` reading memory → `ERROR sim.json: bots.cautious[10].when: unknown path 'memory.foreman_beaten' (paths start with self., target., player., item., with., run., memory., role., arg., game. or val.)`, exit 2.

### README claims → code (auditor checklist)

| README claim | Code |
|---|---|
| `memory.NAME` int, `memory.$NAME` text, separate values | `Expr.Reader`/`Writer` (`IsMemoryNumber` rejects `$`), `Expr.TextWriter`/`TextReader`; `SaveMemory.Key` prefixes `$` for text |
| Used in expressions/conditions, `set`/`add`/`set_text` `var`, text `args` | `Expr.Reader` `case "memory"`; `Builtins` `set`/`add` (`Assign`), `set_text` (`TextWriter(target, c.PackId)`); `PackLoader.TextArg` (TextReader then `ValueOf`) |
| Unset reads 0 / "" | `IMemory` doc (`Model.cs`), `MemoryStore`, `SaveMemory` fallbacks |
| Per pack: same name in two packs = separate | `IMemory.Get(pack, name)`; `SaveMemory.Key` = `memory:{pack}:{name}` |
| In the save, framework's mod id, per save; write is immediate | `SaveMemory` → `ModHelper.SetModDataInt(ExpeditionRun.FrameworkPack, …)`; `ModHelper.ModDataStore` = `PlayerStore.Instance?.ModData()` (decompiled) |
| No save loaded: reads 0/"", writes do nothing | decompiled `ModHelper.SetModData` (null store → skip + warning log), `GetModData` → fallback, `GetModDataInt` fallback 0 |
| Map reads memory and `game.`; `run.` reads 0 there | `MapContext` (`IMemoryHost`, `Get` → 0, `Game` → delegate); `ExpeditionLocation` ctor passes `new SaveMemory()` and `GameValues.Get` |
| `hidden_when` + `unlocked_by` both required; OR via `hidden_when` | `ExpeditionLocation.IsDiscovered` |
| Intel `when` checked once per day at roll; gated slot picks nothing | `IntelRoller.Roll` (skips slot when `When` fails); `IntelCards` daily roll on `dayCounter` |
| `game.day` = day counter, 0 with no save | `GameValues.Get` `"day"` → `StoreStation.GetDayCounter()` (0 when `Instance == null`, iteration 3 decompile) |
| `game.clears` this area, `game.clears.AREA` same pack | `GameReads.ClearsArea` (prefixes reading area's pack) |
| Clear counted on leaving after `cleared` outcome or `run.cleared` > 0 → during a run counts earlier nights | `ExpeditionLocation.Leave` |
| Warning text | `PackLoader.Finish`: `memory.NAME is read but never written by this pack, so it always reads 0` (`""` for `$`) |
| sim.json `memory` keys/forms/errors/FAIL text | `SimConfig.MemoryOf`, `IsTextName`, `SimMemory.Check`; `PackRun.Play` (store per run unless carry) |
| Non-carry `expect` checks the last run | `PackRun.Play` returns last run's store |
| Bots can't read `memory.` | `SimConfig` bot `when` → `Expr.Compile(…, bot.Named)` (no pack); probe above |
| `bridge.game` names incl. `day`, `clears.AREA` | `KitBridge.GameValue` (raw name lookup); `ExpeditionRun.Game` passes name through |
| C#: no memory API; reachable via `spec.Value`/`Effect`/`Condition` | `api/ExpeditionApi.cs` (no memory member); `PackLoader.Spec.Value/Effect/Condition` compile with `PackId` |
| Hosts: `IBridge.Memory`, `MemoryStore`, `MapContext` optional store, `IntelRoller.Roll` scope | `Model.cs` `IBridge.Memory`, `MemoryStore` (public); `MapContext(areaId, localize, game, IMemory memory = null)`; `IntelRoller.Roll(AreaDef, Func<int,int>, IScope)` |

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `samples/SampleArea/ExpeditionFramework/main.json` | Sample pack data | Foreman `on.defeated` adds to `memory.foreman_beaten`; office `enter` prints grudge line when > 0 |
| `samples/SampleArea/Localization/en.csv` | Sample text | + `sa_foreman_grudge` |
| `samples/SampleArea/sim.json` | Sample ef-sim config | + row 4 with `memory` |
| `README.md` | Pack-author docs | Memory section, ef-sim Memory subsection, 9 line edits |

### Dependencies
- **Requires:** iterations 1–4 (all on branch).
- **Blocks:** sprint close.

### Constraints
- README audience: pack authors. Plain English, short sentences, match surrounding style (no marketing, no "simply"). Text in Steps is the intended wording — executor may fix grammar but not add claims outside the claims table.
- New sample effects must draw no random numbers (no `chance`, dice, `variants`) so rows 1–3 and `tests/Sim` stay identical.
- Sample JSON comment obeys Source Hygiene (no projex ids, present tense).
- No `git commit --amend`, no branch deletion (user hook).

### Assumptions
- `$TEMP` is set in Git Bash (iteration 4 suite used it).
- `ef-sim run` on a copied pack folder outside the repo finds the framework (iteration 4 fixture under `$TEMP` did).

### Impact Analysis
- **Direct:** 4 files above.
- **Adjacent:** `tests/Sim` loads the sample (`LoadAll`, `LoadText`, `Play`, `TestWeakPointOptions`) — new key + effects must load clean; outcomes unchanged.
- **Downstream:** none (sample is copied to `Mods/SampleArea` by hand).

---

## Implementation

### Overview

Steps 1–3 edit the sample (mechanical, exact text). Step 4 edits README (exact text). Step 5 runs the suite and commits once.

### Step 1: Sample data — write and read the counter

**Objective:** Foreman defeat → `memory.foreman_beaten += 1`; office entry reads it.
**Confidence:** High (probed)
**Depends on:** None
Do-Projex: Encouraged

**Files:** `samples/SampleArea/ExpeditionFramework/main.json`

**Changes:**

```jsonc
// Before (Foreman, line 116):
      "on": { "defeated": [ { "credits": "2d20 + 10" }, { "set": { "var": "run.boss_down", "value": 1 } } ] }

// After:
      // memory.foreman_beaten outlasts the run: it is kept in the player's save, so the count grows night after night.
      "on": {
        "defeated": [
          { "credits": "2d20 + 10" },
          { "set": { "var": "run.boss_down", "value": 1 } },
          { "add": { "var": "memory.foreman_beaten", "value": 1 } }
        ]
      }
```

```jsonc
// Before (office room, line 201):
      "enter": [ { "fight": { "from": ["foreman"], "won": [ { "say": { "text": "sa_foreman_down", "tone": "good" } } ] } } ],

// After:
      "enter": [
        { "if": {
          "when": "memory.foreman_beaten > 0",
          "then": [ { "say": { "text": "sa_foreman_grudge", "args": ["memory.foreman_beaten"], "tone": "bad" } } ]
        } },
        { "fight": { "from": ["foreman"], "won": [ { "say": { "text": "sa_foreman_down", "tone": "good" } } ] } }
      ],
```

**Rationale:** `add` and an expression `if` draw nothing → existing rows unchanged. Reading the count as a line arg exercises the read path and the text key every run of the carried row (it starts at 1). A gameplay-changing read (e.g. gating the Foreman's `call_help`) would shift rows 1–3 and `tests/Sim` outcomes.

**Verification:** V1, V3, V4.

**If this fails:** `git -C <work-root> checkout -- samples/SampleArea/ExpeditionFramework/main.json`.

---

### Step 2: Sample text key

**Objective:** Text for the grudge line.
**Confidence:** High
**Depends on:** Step 1
Do-Projex: Encouraged

**Files:** `samples/SampleArea/Localization/en.csv`

**Changes:** insert one line directly after `sa_foreman_down,The office goes quiet.` (no commas in the text — the loader splits on the first comma, but keep it plain):

```
sa_foreman_grudge,The Foreman knows your face. Times you have beaten him: {0}.
```

**Verification:** V1 (tests/Sim `Loc` throws on a missing key), V3, V4.

**If this fails:** `git -C <work-root> checkout -- samples/SampleArea/Localization/en.csv`.

---

### Step 3: Sample sim row with carried memory

**Objective:** `ef-sim` checks the carried counter.
**Confidence:** High (probed)
**Depends on:** Steps 1–2
Do-Projex: Encouraged

**Files:** `samples/SampleArea/sim.json`

**Changes:**

```jsonc
// Before:
    { "area": "flooded_cellar", "kit": "everything", "bot": "cautious", "runs": 2500, "seed": 44 }
  ],

// After:
    { "area": "flooded_cellar", "kit": "everything", "bot": "cautious", "runs": 2500, "seed": 44 },
    { "area": "flooded_cellar", "kit": "everything", "bot": "random", "runs": 500, "seed": 45,
      "memory": { "start": { "foreman_beaten": 1 }, "carry": true, "expect": { "foreman_beaten": { "min": 2 } } } }
  ],
```

**Rationale:** `start` 1 → grudge line from the first run; `carry` → one store across 500 runs (probe: final 230); `min: 2` proves ≥ 1 win carried while staying robust under `--runs` overrides (an exact count would fail whenever `--runs` is passed — the README caveat). Random bot, not cautious: cautious clears 1.8%, random 37.8%.

**Verification:** V4, V6.

**If this fails:** `git -C <work-root> checkout -- samples/SampleArea/sim.json`.

---

### Step 4: README

**Objective:** Document memory and related reads for pack authors; fix two stale claims.
**Confidence:** High (every claim mapped in the claims table)
**Depends on:** Steps 1–3 (README names the sample counter and its row)
Verify-Projex: Encouraged

**Files:** `README.md`

Apply these 11 edits. Line numbers are at `e5b1734`; match on text.

**R1 — warnings list (lines 24-27).** Before:
```
Warnings are likely mistakes that don't: keys a definition doesn't have (the message lists the ones it does), arguments an
effect or condition never reads, unknown tones, `#tags` no item can have, and `weight`, `when` or `tags` on kinds that
```
After:
```
Warnings are likely mistakes that don't: keys a definition doesn't have (the message lists the ones it does), arguments an
effect or condition never reads, unknown tones, `#tags` no item can have, `memory.` names a pack reads but never writes,
and `weight`, `when` or `tags` on kinds that
```
(The rest of the sentence, `ignore them (only rooms, …`, is unchanged; re-wrap only if a line gets far over ~120 chars.)

**R2 — `hidden_when` row (line 107).** Before:
```
| `hidden_when` | Condition that keeps the area off the map |
```
After:
```
| `hidden_when` | Condition that keeps the area off the map. It can read `memory.` and `game.` (see [Memory across nights](#memory-across-nights)) |
```

**R3 — `intel` row (line 111).** Before:
```
| `intel` | Daily map cards: `id`, `pool`, `chance`, `counts`, `title`, `description`, `effect`, `tone`, `good`. Read during a run with `intel(slot)` |
```
After:
```
| `intel` | Daily map cards: `id`, `pool`, `chance`, `counts`, `title`, `description`, `effect`, `tone`, `good`, `when`. Read during a run with `intel(slot)`. A slot whose `when` doesn't hold when the day's cards are rolled shows nothing that day |
```

**R4 — path heads (lines 168-169).** Before:
```
`self.hp <= 5 && run.depth >= 2`. Paths start with `self`, `target`, `player`, `item`, `with` (the second item of a pair),
`arg`, `val` (a named value), `game` or `run`. `run` also has `depth`, `rooms`, `rooms_left`, `turn`, `enemies`,
```
After:
```
`self.hp <= 5 && run.depth >= 2`. Paths start with `self`, `target`, `player`, `item`, `with` (the second item of a pair),
`arg`, `val` (a named value), `game`, `run` or `memory` (see [Memory across nights](#memory-across-nights)). `run` also has `depth`, `rooms`, `rooms_left`, `turn`, `enemies`,
```

**R5 — `game` reads (line 180).** Before:
```
`game` reads the player outside the run: `game.luck`, `game.roll_luck`, `game.wounds`, `game.maimed`.
```
After:
```
`game` reads the player outside the run: `game.luck`, `game.roll_luck`, `game.wounds`, `game.maimed`, `game.day` (the
game's day counter, 0 with no save loaded), `game.clears` (how many times the player has cleared this area) and
`game.clears.AREA` (the same for another area of this pack, such as `game.clears.flooded_cellar`). A clear counts when the
player leaves an area after a run that ended `cleared` or with `run.cleared` above 0, so during a run these count earlier
nights, not tonight. They work on the map too.
```

**R6 — new section.** Insert before `## The C# API` (after the `item_stats` / `loaded` paragraph ending `last until the run ends.`), with one blank line on each side:

````markdown
## Memory across nights

`run.` variables last one run. `memory.` values stay in the player's save from night to night, so a pack can keep a
count, remember a name, or open something up after an earlier night.

```json
{ "add": { "var": "memory.foreman_beaten", "value": 1 } }
{ "set_text": { "var": "memory.$rival", "value": "target.name" } }
{ "if": { "when": "memory.foreman_beaten >= 3", "then": [ ... ] } }
```

- `memory.NAME` holds a whole number and `memory.$NAME` holds text. They are separate values, even with the same NAME.
  Use them as you would `run.NAME` and `run.$NAME`: in expressions and conditions, as the `var` of `set`, `add` and
  `set_text`, and in text `args`. A name never written reads 0, or "" as text.
- Names are fixed: write each one out in full in the data.
- Each pack has its own memory. Two packs that both write `memory.visits` keep separate counts.
- A write goes into the save straight away, so it stays however the run ends. The game keeps memory in the save's mod
  data under the framework's own mod id, so every save has its own and a new game starts with none. With no save loaded,
  memory reads 0 and "" and writes do nothing.
- The map reads memory too. An area's `hidden_when` and an intel slot's `when` can test `memory.` and `game.` (`run.`
  reads 0 there). Intel is rolled once a day, so what a run writes tonight changes the street's cards from the next day.
- `unlocked_by` puts an area on the map once the area it names has been cleared, and `hidden_when` keeps it off while its
  condition holds. With both, the area needs both. To open an area on either, leave out `unlocked_by` and test both in
  `hidden_when`:

  ```json
  "hidden_when": "game.clears.flooded_cellar == 0 && memory.met_smuggler == 0"
  ```

- Loading warns about a `memory.` name a pack reads but never writes, such as `memory.ghost is read but never written by
  this pack, so it always reads 0`. It is usually a typo. `ef-sim lint --strict` fails on it.
- In `ef-sim`, memory lasts one run unless a `sim.json` row asks for more (see [Memory](#memory) under ef-sim run).

`samples/SampleArea` counts the Foreman's defeats in `memory.foreman_beaten`, and the Foreman mentions the count when you
reach his office again. Its `sim.json` carries the count across 500 runs and checks it.
````

**R7 — C# notes.** Insert after the paragraph ending `To use another mod's hook under \`on\`, load\nafter that mod.` and before `## Building and testing`:

```markdown
The API has no memory calls of its own. A C# effect or condition reaches memory through what it compiles from data:
expressions read with `spec.Value`, and effects and conditions read with `spec.Effect` and `spec.Condition`, read and
write `memory.` for the pack whose data uses them, as data does.

Code that runs the engine itself, as `tests/Sim` and `ef-sim` do, provides memory through `IBridge.Memory`
(`MemoryStore` keeps it in memory). `MapContext` takes a store as its optional last argument for map-time reads; without
one, memory reads 0 and "". `IntelRoller.Roll` takes the scope that intel slots' `when` is checked against, such as a
`MapContext`'s `Root`.
```

**R8 — ef-sim checks paragraph (lines 327-329).** Replace the fragment
```
every `expect.packing` case that comes out different (see Packing below), and every `expect.loot` case
```
with
```
every `runs[n].memory.expect` value that comes out different (see Memory below), every `expect.packing` case that comes out different (see Packing below), and every `expect.loot` case
```

**R9 — sim.json defaults (lines 351-352).** Replace
```
`credits` 0, `items` empty, `count` 1, `tags` and `props` empty, `bot` `random`, `seed` 42 plus the row's index, `expect`
empty. Unknown keys are errors,
```
with
```
`credits` 0, `items` empty, `count` 1, `tags` and `props` empty, `bot` `random`, `seed` 42 plus the row's index, `expect`
empty, row `memory` none (see Memory below). Unknown keys are errors,
```

**R10 — bots `when` (lines 381-382).** Before:
```
In `when`, `self` and `player` are the player, `target` is the focused foe, and every other path and function works as in pack
data (`run.depth`, `run.rooms`, `run.credits`, `run.<var>`, `any_enemy(target.hp < 3)`, …). Bots also read:
```
After:
```
In `when`, `self` and `player` are the player, `target` is the focused foe, and every other path and function works as in pack
data (`run.depth`, `run.rooms`, `run.credits`, `run.<var>`, `any_enemy(target.hp < 3)`, …) except `memory.`, which bots
can't read. Bots also read:
```

**R11 — new `#### Memory` subsection + bridge `game` names.**
(a) Insert before `#### Packing` (after the Bots errors paragraph ending `holds).`), blank line each side:

````markdown
#### Memory

A run's `memory.` starts empty and is dropped when the run ends, so every run plays a first night. A row's `memory`
changes that:

```json
{ "area": "flooded_cellar", "kit": "everything", "runs": 500, "seed": 45,
  "memory": { "start": { "foreman_beaten": 1 }, "carry": true, "expect": { "foreman_beaten": { "min": 2 } } } }
```

| Key | What it does |
|---|---|
| `start` | What memory holds before the row's runs, by name without `memory.`: `"visits": 3` for a number, `"$rival": "Dex"` for text. Without `carry` every run starts from these; with it, only the first |
| `carry` | `true` makes the row's runs share one memory, each run seeing what the runs before it wrote, like nights in a row. Default `false` |
| `expect` | What memory must hold after the row's last run: a whole number (exactly), `{ "min": 1, "max": 5 }` (either may be left out), or text for a `$` name. Without `carry` it checks the last run alone |

Each value that comes out different is a `FAIL` such as `runs[3]: memory.foreman_beaten should be at least 2, got 1`.
Writing `memory.` in a name, an empty name, unknown keys, wrong types and `min` above `max` are errors (exit 2).
`--runs` changes how many runs a carried row plays, so an exact `expect` on a carried count fails under it; use `min`
and `max` where that matters.
````

(b) Bridge paragraph, line 464. Before:
```
`game` maps a name to a whole number, or to `coin`: 0 or 1 drawn from the run's random each time it is read. Unlisted
names read 0.
```
After:
```
`game` maps a name to a whole number, or to `coin`: 0 or 1 drawn from the run's random each time it is read. Names are
written as after `game.`, such as `"day": 30` or `"clears.ne_sump": 2`. Unlisted names read 0.
```

**Rationale:** One `##` section collects the concept (memory, map reads, unlock idiom, warning) so the scattered rows (R2–R5) can link to it; ef-sim specifics live with ef-sim (R8, R9, R11). R10/R4 fix claims that became wrong when `memory` landed. No claim outside the claims table.

**Verification:** V8; judgment checks J1–J2.

**If this fails:** `git -C <work-root> checkout -- README.md`.

---

### Step 5: Gates + commit

**Objective:** Run the suite; commit the 4 files once all pass.
**Confidence:** High
**Depends on:** Steps 1–4

Run V1–V10 (below). All must match. Then:

```powershell
C:\Users\BA\.claude\skills\projex\stage-n-commit.ps1 "<work-root>" "docs+sample: pack memory in README; sample carries a Foreman counter checked by ef-sim" "--trailer Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>" README.md samples/SampleArea/ExpeditionFramework/main.json samples/SampleArea/Localization/en.csv samples/SampleArea/sim.json
```

(Use the executing model's name in the trailer.) Then remove `$TEMP/ef-sample-mut`, `$TEMP/ef-sample-bot`, `$TEMP/ef-pack-memory-install`. `git -C <work-root> status --porcelain` must be empty (bin/obj are gitignored).

**If this fails:** fix the failing step; do not commit until V1–V10 pass. A V4/V1 row diff means a new effect draws randomness — remove it, don't re-baseline.

---

## Verification Plan

### Deterministic Suite

Run from `<work-root>`. V2 in PowerShell; everything else bash (Git Bash). Build once with V1/V3; later rows may use `--no-build`.

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V1 | SC1, SC3, SC5 | `dotnet run -c Release --project tests/Sim` | exit 0; contains line `Outcomes: knocked_out=3742, cleared=1049, left=209; longest run 28 steps`; last line `All checks passed`; no `FAIL` | missing text key; sample load problem; new effect drawing randomness |
| V2 | SC5 | PowerShell: `dotnet build ExpeditionFramework.csproj -c Release --no-incremental -warnaserror -p:GameManaged="E:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed\\" -p:InstallDir="$env:TEMP\ef-pack-memory-install\\"` | exit 0 | (regression guard; no code changes) |
| V3 | SC1, SC5 | `dotnet run -c Release --project tools/EfSim -- lint samples/SampleArea --strict` then `dotnet run -c Release --no-build --project tools/EfSim -- lint "$NER" --strict` | both exit 0; `lint sample_area: 0 error(s), 0 warning(s)`; `lint night_expeditions_reborn: 0 error(s), 0 warning(s)` | key missing from CSV; read-never-written warning (typo in name) |
| V4 | SC2, SC3, SC5 | bash V4 | `diff` prints nothing; last line `ef-sim sample_area: all checks passed`; exit 0 | rows 1–3 shifted; row 4 missing/different; expect failing |
| V5 | SC5 | bash V5 | `diff` prints nothing; NER run last line `ef-sim night_expeditions_reborn: all checks passed` | NER balance change |
| V6 | SC2 | bash V6 | exit 1; output contains exactly `FAIL runs[3]: memory.foreman_beaten should be 1, got 230`; last line `ef-sim sample_area: 1 check(s) failed` | counter not written / not carried / expect not checked |
| V7 | SC4 (R10 claim) | bash V7 | exit 2; output exactly `ERROR sim.json: bots.cautious[10].when: unknown path 'memory.foreman_beaten' (paths start with self., target., player., item., with., run., memory., role., arg., game. or val.)` | README bot claim no longer true |
| V8 | SC4 | bash V8 | every line `ok`; no `MISSING` / `STALE` | README edit skipped or stale claim left |
| V9 | Scope | bash V9 | no output | edits outside the 4 files (incl. code) |
| V10 | Source Hygiene | bash V10 | no output | projex refs / plan shape / changelog wording in added lines |

Bash blocks (run from `<work-root>`):

```bash
NER="E:/SteamLibrary/steamapps/common/Probably Stolen/Modding/NightExpeditionsReborn"
BASE="C:/Users/BA/AppData/Local/Temp/claude/a48a32985dda25c5/057518b9-283c-4d89-bc27-9ae607bcf871/scratchpad/ner-baseline-4000.txt"
EFSIM="dotnet run -c Release --no-build --project tools/EfSim --"

# V4 — sample passes; rows 1-3 unchanged, row 4 as probed
$EFSIM run samples/SampleArea > "$TEMP/sample-after.txt"; echo "exit $?"
diff <(grep -E '^flooded_cellar' "$TEMP/sample-after.txt") - <<'EOF'
flooded_cellar     knife        random     2500     2.8%        91.6%       8.4%
flooded_cellar     everything   random     2500    37.8%        54.6%      45.4%
flooded_cellar     everything   cautious   2500     1.8%        54.5%      45.5%
flooded_cellar     everything   random      500    38.2%        51.6%      48.4%
EOF
tail -1 "$TEMP/sample-after.txt"

# V5 — NER 4000-run rows identical to the pre-sprint baseline; default NER run passes
$EFSIM run "$NER" --runs 4000 > "$TEMP/ner-after.txt"
diff <(grep -E '^ne_' "$BASE") <(grep -E '^ne_' "$TEMP/ner-after.txt")
$EFSIM run "$NER" | tail -1

# V6 — exact expect reveals the carried count
rm -rf "$TEMP/ef-sample-mut"; cp -r samples/SampleArea "$TEMP/ef-sample-mut"
sed -i 's/"expect": { "foreman_beaten": { "min": 2 } }/"expect": { "foreman_beaten": 1 }/' "$TEMP/ef-sample-mut/sim.json"
$EFSIM run "$TEMP/ef-sample-mut" | grep -E '^(FAIL|ef-sim)'; echo "exit ${PIPESTATUS[0]}"

# V7 — bots can't read memory
rm -rf "$TEMP/ef-sample-bot"; cp -r samples/SampleArea "$TEMP/ef-sample-bot"
sed -i 's/{ "pick": \["press_on"\] }/{ "when": "memory.foreman_beaten > 0", "pick": ["press_on"] }/' "$TEMP/ef-sample-bot/sim.json"
$EFSIM run "$TEMP/ef-sample-bot"; echo "exit $?"

# V8 — README content
R=README.md
has() { grep -qF -- "$1" "$R" && echo "ok   $1" || echo "MISSING $1"; }
hasnt() { grep -qF -- "$1" "$R" && echo "STALE $1" || echo "ok   (gone) $1"; }
[ "$(grep -c '^## Memory across nights$' $R)" = 1 ] && echo "ok   ## section" || echo "MISSING ## section"
[ "$(grep -c '^#### Memory$' $R)" = 1 ] && echo "ok   #### section" || echo "MISSING #### section"
[ "$(grep -oF '(#memory-across-nights)' $R | wc -l)" -ge 2 ] && echo "ok   links" || echo "MISSING links"
has '(#memory)'
has '`game`, `run` or `memory`'
has '`game.clears.AREA`'
has '`game.day`'
has "intel slot's \`when\`"
has 'is read but never written by'
has '`runs[n].memory.expect`'
has 'row `memory` none'
has "except \`memory.\`, which bots"
has '`IBridge.Memory`'
has '`IntelRoller.Roll`'
has '"clears.ne_sump": 2'
has 'memory.foreman_beaten'
hasnt '`arg`, `val` (a named value), `game` or `run`.'
hasnt '`game.maimed`.'

# V9 — only the 4 planned files changed since the iteration started
git diff --name-only e5b1734 -- . ':!.projex' | grep -vxE 'README\.md|samples/SampleArea/(ExpeditionFramework/main\.json|Localization/en\.csv|sim\.json)'

# V10 — hygiene in added lines
git diff e5b1734 -- samples README.md | grep -E '^\+' | grep -nE 'projex|2611[0-9]{6}|Step [0-9]|SC[0-9]|iteration|no longer|used to'
```

V9/V10 run after the commit too (diff from `e5b1734` covers committed + uncommitted).

### Judgment Checks

| Check | Covers | Why not deterministic |
|-------|--------|-----------------------|
| J1: Each README sentence added/changed matches a row of the claims table, and nothing claims more (e.g. no cross-pack memory reads, no promise about when the game writes the save file to disk) | SC4 | Semantic match of prose to code |
| J2: README prose is plain English for pack authors: short sentences, no jargon beyond names the README already uses, consistent with surrounding style; R1–R11 read naturally in place | SC4 | Style judgment |
| J3: Sample comment and grudge line read naturally; comment has no changelog wording | SC1, Source Hygiene | Wording judgment |

### Acceptance Criteria Validation

| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| SC1 sample writes/reads, key present | V1, V3, V4, J3 | tests pass; lint 0/0; sample passes |
| SC2 carried row checked | V4, V6 | row 4 line exact; exact-expect mutation → `got 230` |
| SC3 old rows unchanged | V4, V1 | rows 1–3 identical; `Outcomes:` line identical |
| SC4 README | V7, V8, J1, J2 | bot claim true; all `ok`; claims match code |
| SC5 gates | V1–V5, V9 | all green; only 4 files changed |

---

## Rollback Plan

Nothing is committed until Step 5. To abandon: `git -C <work-root> checkout -- README.md samples/SampleArea/ExpeditionFramework/main.json samples/SampleArea/Localization/en.csv samples/SampleArea/sim.json`, then `rm -rf "$TEMP/ef-sample-mut" "$TEMP/ef-sample-bot"`. If already committed: `git -C <work-root> revert <commit>` (no amend — the user's hook blocks it).

---

## Notes

### Risks
- Row 4 numbers differ from the probe if Step 1 differs semantically (e.g. extra effect, `variants`). Mitigation: V4 exact row; Steps give exact JSON.
- `min: 2` is weak as a balance check — intended: it shows carry works and survives `--runs`; V6 proves the real count.
- README line wrap: R4/R8 produce lines over 120 chars; acceptable (README already has long lines, e.g. 177, 329) — re-wrap optional, wording fixed.

### Split decision
No split — single scope, 5 steps, ~25 KB.

### Open Questions
- [ ] (none)
