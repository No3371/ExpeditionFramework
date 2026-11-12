# ef-sim loot expectations + NER Gunsmith case

> **Status:** Complete
> **Author:** Claude Opus 5.5 (plan, sprint iteration 5)
> **Source:** Direct request (sprint derive objective, iteration 5)
> **Parent:** 2611121010-pack-defined-sims-sprint.md
> **Sprint:** 2611121010-pack-defined-sims-sprint.md — iteration 5
> **Related Projex:** 2611121010-pack-defined-sims-sprint.md · 2611121110-ef-sim-ner-pack-plan.md · 2611121143-ef-sim-bridge-knobs-plan.md · 2611121154-ef-sim-bridge-knobs-patch.md · 2611121201-ef-sim-bridge-knobs-audit.md · 2611121240-ef-sim-lose-cash-overflow-patch.md · 2611121226-ef-sim-loot-expect-patch.md
> **Worktree:** Yes — work-root is the sprint worktree `ExpeditionFramework/.projexwt/2611121010-pack-defined-sims` (branch `projex/sprint/2611121010-pack-defined-sims`); every commit lands there, never on `main`.

---

## Summary

[PATCHED] Executed via 2611121226-ef-sim-loot-expect-patch.md (code commit 13ed7b2). Nothing remains open.

New `expect.loot` cases in `sim.json`: each rolls named loot tables N times in one run of an area, optionally with the pack reloaded with extra mods counted as installed, and checks that no rolled id starts with a forbidden prefix and/or that listed ids each roll at least once. NER `sim.json` gets two cases that together reproduce `CheckGunsmithLoot` (`NightExpeditionsReborn/tests/Sim/Program.cs:107-136`) — the last NER C# check without a data form (sprint criterion 2).

**Prototyped by the planner** against HEAD `9743aa6` (prototype reverted; worktree clean at planning time). The code below is the prototype's, verbatim. Observed: build 0W/0E; NER (copy with the new `sim.json`) exit 0, `loot: 2 case(s), 0 failed`; the only output difference vs today is that one added line; the `ne_` table lines hash identical (`b9cdd053…`); sample output byte-identical; 4 mutation probes fail (exit 1) and 7 malformed-config probes exit 2 with the messages pinned in V5/V6.

**Scope:** `tools/EfSim/{SimConfig,PackRun}.cs` + README (committed) · NER `sim.json` `expect.loot` (on disk, unversioned — never `git add` in NER) · criterion-4 sweep.
**Estimated Changes:** 3 committed files (1 new class + 1 parse method, 1 check method, 1 README section) + 1 unversioned data file.

---

## Objective

### Problem / Gap / Need

Sprint criterion 2 needs every NER C# sim check expressible through `ef-sim`. All are covered except `CheckGunsmithLoot`: reload the pack with `gunsmith_expansion` present (fail on load problems), roll 6 tables in an `ne_foundry` run 2000× each with seed 7, fail if any `gsx_` id rolls without the mod, fail if any of 7 named `gsx_` ids never rolls with it. `ef-sim run` has no way to roll loot or to load with extra mods.

### Success Criteria

- [ ] C1 — `sim.json` accepts `expect.loot`: a list of cases `{ area, tables, rolls, seed?, mods?, never_prefix?, must_roll? }`, strictly parsed (unknown keys, wrong types, empty `tables`, `rolls` ≤ 0, empty `never_prefix`, neither `never_prefix` nor `must_roll`, unknown area → exit 2 with a located message). EfSim builds 0 warnings / 0 errors with `-warnaserror`.
- [ ] C2 — `ef-sim run` rolls each case and prints `loot: N case(s), M failed` after the packing line (only when the pack has loot cases); each failure is a `FAIL expect.loot[n]: …` line; a case with `mods` loads through `Lint.Load` with those mods and fails on its load errors.
- [ ] C3 — NER `sim.json` carries the two Gunsmith cases (on disk); its sha256 is `65ddd3c41150b0cec6f5a9c0261dfbd42d1e951e4b9d437c370d94d114d39078`.
- [ ] C4 — `ef-sim run` on NER exits 0 with `loot: 2 case(s), 0 failed`; full output sha256 `933fe01596867350b0549036885908c749c5c86f7aeae241eed545cf8b69a17e`; default-run table unchanged (`ne_` lines sha256 `b9cdd05320977bf54c2efbc49860d2a99a4b2d887182c93f89b7f52a543ef646`, same as before the change).
- [ ] C5 — The case bites: a missing required id, dropping `mods`, forbidding `gsx_` with the mod present, and a mistyped table each make `ef-sim run` exit 1 with the pinned `FAIL` line.
- [ ] C6 — Sample pack `ef-sim run` output byte-identical to before (sha256 `b1565178f39d91aeed27111567bba72b20a40bfee5cecead9141144c2951a033`, exit 0).
- [ ] C7 — Criterion-4 sweep green: framework sim tests `All checks passed`; `lint --strict` NER and sample 0/0; game-assembly build 0W/0E; `ef-sim run` NER + sample exit 0.
- [ ] C8 — README documents `expect.loot` (`#### Loot` section + the checks paragraph).

### Out of Scope

- The optional C# hook from the sprint Vision — no NER check needs it.
- Loading other packs' data (Gunsmith's own files): `mods` only answers `mod(...)`/`requires`, exactly as the C# check's `HasMod` does.
- Editing NER's C# sim (`tests/Sim`) or any NER file other than `sim.json`; any `git` operation inside the NER folder.
- Engine (`src/Core`), `tests/Sim`, `samples/` changes. Re-running the 4000-run C# parity (unnecessary: the `ne_` table lines are pinned identical, C4).

---

## Context

### Current State

- `tools/EfSim/SimConfig.cs` reads `sim.json` strictly. `expect` allows `offered` and `packing` (`Only(expect, "expect", "offered", "packing")`, ~line 89). Classes `SimItem`, `SimKit`, `SimRow`, `SimPacking`, `SimBridge` at the top; helpers `Need/Only/Obj/List/Str/Int` at the bottom.
- `tools/EfSim/PackRun.cs` `Run`: loads via `Lint.Load(frameworkRoot, folder, id, new HashSet<string>(), …)` (line 30), validates areas of rows + packing cases (line 37, exit 2 on unknown), plays rows, then `CheckPacking` (line 68), then appends `problems`, `packing`, never-offered to `failures`. `AreaId(id, name)` prefixes the pack id when `name` has no `:`.
- `Lint.Load(frameworkRoot, folder, id, ISet<string> mods, out text, out errors, out notes)` (`tools/EfSim/Lint.cs:74`): `HasMod = framework || pack || mods.Contains`; `errors` = load problems + text keys missing from `en.csv`. `ef-sim lint NER --strict --mod gunsmith_expansion` is 0/0 today (planner-checked).
- NER loot gating: 14 entries in `NightExpeditionsReborn/ExpeditionFramework/loot.json` carry `"when": "mod(gunsmith_expansion)"`; `Expr.Compile` binds `mod(...)` to `HasMod` at load time (`src/Core/PackLoader.cs:1419`), so a reload is required — same as the C# check.
- Rolling: `run.Inventory.RollLoot(tableId)` (`src/Core/ExpeditionRun.Api.cs:145`) → `DecideLoot` → weighted pick via the run's `roll` func; nested tables recurse; game tables go to `IBridge.RollTable` (`KitBridge.RollTable` returns the table name). Unknown table id → `null` (so the plan pre-checks `content.Loot.ContainsKey`). `Content.Loot` is `Dictionary<string, LootDef>` keyed `pack:id` (`src/Core/Model.cs:321`).
- C# reference does not call `run.Begin()`; it constructs the run and rolls. The plan does the same, with an empty `SimKit` in a `KitBridge` (its constructor draws nothing from `random`).
- `tests/Sim/Sim.csproj` compiles only `tools/EfSim/Lint.cs` from EfSim — unaffected.
- NER `sim.json`: LF line endings, sha256 `7133cc992841c70d5ce619cd604137ac1bfb1aaa6a04ea74feb009360cc96fcf` (iteration-4 state); ends with the packing list's last case `{ "area": "ne_sump", "missing": ["weapon"] }`. NER is an empty `git init` — never add/commit there.
- Today's outputs (planner-observed with HEAD `9743aa6`): NER `ef run` sha256 `0c40c20ec5aa1c15c6d885ecb03b51e02cda19e5b79faf8cf40d1272da940bae` (equals iteration 4's V6), sample `b1565178…`.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `tools/EfSim/SimConfig.cs` | strict `sim.json` reader | `SimLoot` class, `Loot` list, `expect.loot` parsing (`LootCase`) |
| `tools/EfSim/PackRun.cs` | `ef-sim run` | loot areas validated; `CheckLoot` rolls cases, reloads with `mods` |
| `README.md` | docs (`### ef-sim run`) | checks paragraph mentions `expect.loot`; new `#### Loot` before `#### Bridge` |
| `../../../NightExpeditionsReborn/sim.json` | NER sim data (unversioned) | `expect.loot` with the two Gunsmith cases |

### Dependencies

- **Requires:** iteration 4 + `e92e015` on the sprint branch (HEAD `9743aa6` or later).
- **Blocks:** sprint goal check at iteration-6 derive.

### Constraints

- Default behaviour unchanged: a `sim.json` without `expect.loot` prints nothing new and draws no extra randomness from row streams (loot cases use their own `Random(seed)`, after all rows have played).
- Strict parsing like the rest of `SimConfig`: every error names its place (`expect.loot[n]…`).
- The git safety hook blocks `git branch -d`, `git commit --amend`, and any single command line containing `branch` plus a delete pattern like `rm -rf`. Keep cleanup commands free of the word `branch`.
- Never deploy to the live game: game-assembly build passes `-p:InstallDir=<scratch>/`.

### Assumptions

- `ExpeditionRun` can be constructed and `Inventory.RollLoot` called without `Begin()` (C# reference does it; prototype confirmed).
- Output text contains no absolute paths, so running on a scratch copy of NER and on NER itself yields the same hash (prototype: copy-run hash equals NER-run hash for today's data).

### Impact Analysis

- **Direct:** `SimConfig.cs`, `PackRun.cs`, README, NER `sim.json`.
- **Adjacent:** `Lint.Load` (called a second time per `mods` case; no change), `KitBridge` (constructed with an empty kit; no change).
- **Downstream:** any pack `sim.json` — only gains an optional key; sample pack has none.

---

## Implementation

### Overview

Parse → check → document → commit → NER data → verify → sweep. Steps 1–3 are pure code/docs from the prototype; Step 5 edits NER on disk via a script with an exact anchor.

All commands: **Git Bash**, cwd = work-root. Step 0 defines `$T`, `NER`, `ef`, used throughout.

### Step 0: Baseline

**Objective:** pin pre-change state.
**Confidence:** High
**Depends on:** None

```bash
cd "/e/SteamLibrary/steamapps/common/Probably Stolen/Modding/ExpeditionFramework/.projexwt/2611121010-pack-defined-sims"
export T=$(mktemp -d); echo "T=$T"
NER=../../../NightExpeditionsReborn
ef() { dotnet run -c Release --no-build --project tools/EfSim -- "$@" | tr -d '\r'; return ${PIPESTATUS[0]}; }
git status --porcelain; git log --oneline -1
dotnet build -c Release tools/EfSim/EfSim.csproj > /dev/null
ef run samples/SampleArea > "$T/sample-before.txt"; echo "exit=$?"; sha256sum "$T/sample-before.txt"
ef run "$NER" > "$T/ner-before.txt"; echo "exit=$?"; sha256sum "$T/ner-before.txt"; grep '^ne_' "$T/ner-before.txt" | sha256sum
sha256sum "$NER/sim.json" "$NER/tests/Sim/Program.cs" "$NER/tests/Sim/Sim.csproj"
cp "$NER/sim.json" "$T/ner-sim-before.json"
```

Expected: porcelain empty; HEAD `9743aa6` or a later sprint commit; sample `exit=0`, `b1565178…a033`; NER `exit=0`, `0c40c20e…0bae`, `ne_` lines `b9cdd053…f646`; NER `sim.json` `7133cc99…6fcf`. Record the two `tests/Sim` hashes for V12. If NER `sim.json` hash differs, **stop** — someone changed NER data; report before editing.

---

### Step 1: `SimConfig` — `SimLoot` + `expect.loot` parsing

**Objective:** C1 parsing.
**Confidence:** High
**Depends on:** Step 0
**Do-Projex:** Encouraged

**Files:** `tools/EfSim/SimConfig.cs`

**Changes:**

1a. After the `SimPacking` line:

```csharp
// Before:
internal sealed class SimPacking { public string At, Area; public List<SimItem> Bag; public List<string> Missing; }  // Bag null = no backpack

// After:
internal sealed class SimPacking { public string At, Area; public List<SimItem> Bag; public List<string> Missing; }  // Bag null = no backpack

// expect.loot[n]: roll each table Rolls times in one run of Area, with Mods also counted as installed.
internal sealed class SimLoot
{
    public string At, Area, NeverPrefix; public int Rolls, Seed;   // NeverPrefix null = no prefix check
    public readonly List<string> Tables = new List<string>();
    public readonly List<string> Mods = new List<string>();
    public readonly List<string> MustRoll = new List<string>();
}
```

1b. In `SimConfig` fields:

```csharp
// Before:
    public readonly List<SimPacking> Packing = new List<SimPacking>();
    public SimBridge Bridge = new SimBridge();

// After:
    public readonly List<SimPacking> Packing = new List<SimPacking>();
    public readonly List<SimLoot> Loot = new List<SimLoot>();
    public SimBridge Bridge = new SimBridge();
```

1c. Allowed `expect` keys:

```csharp
// Before:
            Only(expect, "expect", "offered", "packing");
// After:
            Only(expect, "expect", "offered", "packing", "loot");
```

1d. Inside the `expect` block, right after the closing `}` of `if (expect.TryGetValue("packing", out object pk)) { … }` and before the `expect` block's own closing `}`:

```csharp
// Before (end of the expect block):
                }
            }
        }
        if (top.TryGetValue("bridge", out object br)) config.Bridge = ReadBridge(br);

// After:
                }
            }
            if (expect.TryGetValue("loot", out object lt))
            {
                List<object> cases = List(lt, "expect.loot");
                for (int n = 0; n < cases.Count; n++) config.Loot.Add(LootCase(cases[n], "expect.loot[" + n + "]"));
            }
        }
        if (top.TryGetValue("bridge", out object br)) config.Bridge = ReadBridge(br);
```

1e. New method, placed directly above the `// bots.NAME: a non-empty list of rules …` comment. Name it `LootCase` — **not** `Loot` (a method named `Loot` clashes with the `Loot` field: CS0102).

```csharp
    // expect.loot[n]: { area, tables, rolls, seed?, mods?, never_prefix?, must_roll? } with at least one of never_prefix and must_roll.
    private static SimLoot LootCase(object value, string at)
    {
        Dictionary<string, object> c = Obj(value, at);
        Only(c, at, "area", "tables", "rolls", "seed", "mods", "never_prefix", "must_roll");
        var loot = new SimLoot
        {
            At = at,
            Area = Str(Need(c, "area", at), at + ".area"),
            Rolls = Int(Need(c, "rolls", at), at + ".rolls"),
            Seed = c.TryGetValue("seed", out object s) ? Int(s, at + ".seed") : 42,
            NeverPrefix = c.TryGetValue("never_prefix", out object p) ? Str(p, at + ".never_prefix") : null,
        };
        loot.Tables.AddRange(List(Need(c, "tables", at), at + ".tables").Select(x => Str(x, at + ".tables")));
        if (c.TryGetValue("mods", out object m)) loot.Mods.AddRange(List(m, at + ".mods").Select(x => Str(x, at + ".mods")));
        if (c.TryGetValue("must_roll", out object r)) loot.MustRoll.AddRange(List(r, at + ".must_roll").Select(x => Str(x, at + ".must_roll")));
        if (loot.Tables.Count == 0) throw new FormatException(at + ".tables is empty");
        if (loot.Rolls <= 0) throw new FormatException(at + ".rolls must be above 0");
        if (loot.NeverPrefix == "") throw new FormatException(at + ".never_prefix is empty");
        if (loot.NeverPrefix == null && loot.MustRoll.Count == 0) throw new FormatException(at + ": needs never_prefix or must_roll");
        return loot;
    }
```

**Rationale:** helper method keeps `Read` flat (like `Bot`, `ReadBridge`); empty prefix would match everything, and a case with no check is a silent no-op — both are config errors.

**Verification:** V1 (after Step 2), V6.

**If this fails:** `git checkout -- tools/EfSim/SimConfig.cs`.

---

### Step 2: `PackRun` — validate loot areas, `CheckLoot`

**Objective:** C2.
**Confidence:** High
**Depends on:** Step 1
**Verify-Projex:** Encouraged

**Files:** `tools/EfSim/PackRun.cs`

2a. Area validation (line 37):

```csharp
// Before:
            foreach (string name in config.Rows.Select(row => row.Area).Concat(config.Packing.Select(p => p.Area)))
// After:
            foreach (string name in config.Rows.Select(row => row.Area).Concat(config.Packing.Select(p => p.Area)).Concat(config.Loot.Select(l => l.Area)))
```

2b. Call site (lines 68–70) — `CheckLoot` runs **before** `problems` is copied into `failures`, so run-time problems raised while rolling are reported:

```csharp
// Before:
            List<string> packing = CheckPacking(id, config, areas, localize);
            failures.AddRange(problems.OrderBy(p => p, StringComparer.Ordinal));
            failures.AddRange(packing);

// After:
            List<string> packing = CheckPacking(id, config, areas, localize);
            List<string> loot = CheckLoot(frameworkRoot, folder, id, content, config, localize, problems);
            failures.AddRange(problems.OrderBy(p => p, StringComparer.Ordinal));
            failures.AddRange(packing);
            failures.AddRange(loot);
```

2c. New method directly above `private static string AreaId(string id, string name) =>`:

```csharp
    // expect.loot: rolls loot tables as a run of the case's area does, the pack reloaded with the case's mods counted as installed.
    private static List<string> CheckLoot(string frameworkRoot, string folder, string id, Content plain, SimConfig config, Localizer localize,
        HashSet<string> problems)
    {
        var failures = new List<string>();
        if (config.Loot.Count == 0) return failures;
        int failed = 0;
        foreach (SimLoot c in config.Loot)
        {
            int before = failures.Count;
            Content content = plain;
            if (c.Mods.Count > 0)
            {
                content = Lint.Load(frameworkRoot, folder, id, new HashSet<string>(c.Mods), out _, out List<string> errors, out _);
                failures.AddRange(errors.Select(e => c.At + " with " + string.Join(", ", c.Mods) + ": " + e));
            }
            string areaId = AreaId(id, c.Area);
            if (failures.Count == before && !content.Areas.ContainsKey(areaId)) failures.Add(c.At + ": no area " + areaId);
            if (failures.Count == before)
            {
                var random = new Random(c.Seed);
                var run = new ExpeditionRun(content, content.Areas[areaId], new KitBridge(new SimKit(), config.Bridge, content, random, problems),
                    s => random.Next(1, s + 1), localize, "you");
                var rolled = new HashSet<string>();
                foreach (string table in c.Tables)
                {
                    string tableId = AreaId(id, table);
                    if (!content.Loot.ContainsKey(tableId)) { failures.Add(c.At + ": no loot table " + tableId); continue; }
                    for (int n = 0; n < c.Rolls; n++)
                    {
                        string item = run.Inventory.RollLoot(tableId);
                        if (item != null) rolled.Add(item);
                    }
                }
                List<string> banned = c.NeverPrefix == null ? new List<string>()
                    : rolled.Where(item => item.StartsWith(c.NeverPrefix, StringComparison.Ordinal)).OrderBy(item => item, StringComparer.Ordinal).ToList();
                if (banned.Count > 0) failures.Add(c.At + ": rolled " + string.Join(", ", banned) + " (never_prefix " + c.NeverPrefix + ")");
                foreach (string must in c.MustRoll.Where(m => !rolled.Contains(m))) failures.Add(c.At + ": " + must + " never rolled");
            }
            if (failures.Count > before) failed++;
        }
        Console.WriteLine("loot: " + config.Loot.Count + " case(s), " + failed + " failed");
        return failures;
    }
```

**Rationale:** mirrors the C# check — one run per case, tables rolled in order from one `Random(seed)`, no `Begin()`. A `mods` case reloads through `Lint.Load` (the same seam `lint --mod` uses) and stops at load errors, as C# does (`if (with.Problems.Count > 0) return`). `Lint.Load`'s errors also include missing text keys — stricter than the C# check, deliberately (a mod-gated branch whose text is missing is a real defect); today it adds nothing (`lint --mod gunsmith_expansion` 0/0). AreaId reused for table names (same "no `:` → pack id prefix" rule).

**Verification:** V1, V2, V5, V6.

**If this fails:** `git checkout -- tools/EfSim/PackRun.cs tools/EfSim/SimConfig.cs`.

---

### Step 3: README

**Objective:** C8.
**Confidence:** High
**Depends on:** Step 2
**Do-Projex:** Encouraged

**Files:** `README.md` (section `### ef-sim run`)

3a. Checks paragraph (currently line 329):

```text
Before:
options, a run that throws, a decision no rule of the row's bot picks an option for, and every `expect.offered` option id that no run was offered, and every `expect.packing` case that comes out different (see Packing below). Load warnings print as `WARN` and

After:
options, a run that throws, a decision no rule of the row's bot picks an option for, and every `expect.offered` option id that no run was offered, every `expect.packing` case that comes out different (see Packing below), and every `expect.loot` case whose rolls break its rule (see Loot below). Load warnings print as `WARN` and
```

3b. Insert a new section between the end of `#### Packing` (paragraph ending `an unknown area are errors (exit 2).`) and `#### Bridge`, with one blank line on each side:

````markdown
#### Loot

`expect.loot` rolls loot tables the way a run does and checks what came out, e.g. entries that only exist when another mod
is installed:

```json
"expect": {
  "loot": [
    { "area": "ne_foundry", "tables": ["ne_supplies", "ne_arms"], "rolls": 2000, "seed": 7, "never_prefix": "gsx_" },
    { "area": "ne_foundry", "tables": ["ne_supplies", "ne_arms"], "rolls": 2000, "seed": 7, "mods": ["gunsmith_expansion"],
      "must_roll": ["gsx_crate_ammo", "gsx_crate_parts"] }
  ]
}
```

Each case needs `area`, `tables` (not empty) and `rolls` (above 0), and at least one of `never_prefix` (no rolled id may
start with it) and `must_roll` (each listed id must roll at least once). A table or area without `:` gets the pack's id as
prefix. Every table is rolled `rolls` times, in order, in one fresh run of `area` that is never begun (empty kit, `bridge`
settings applied), drawing from its own random seeded with `seed` (default 42), so cases never shift the table above.
`mods` (default none) reloads the framework and the pack with those mod ids also counted as installed for `mod(...)` and
include `requires`, as `lint --mod` does; no other pack is loaded, and load errors or missing text keys of that reload fail
the case. After the packing line a `loot: N case(s), M failed` line prints, and each failure is a `FAIL` such as
`expect.loot[1]: gsx_crate_ammo never rolled`, `expect.loot[0]: rolled gsx_crate_ammo (never_prefix gsx_)` or
`expect.loot[0]: no loot table night_expeditions_reborn:ne_armz`. Unknown keys, wrong types, an empty `tables` or
`never_prefix`, a case with neither check, and an unknown area are errors (exit 2).
````

**Verification:** V11.

**If this fails:** `git checkout -- README.md`.

---

### Step 4: Build + commit (sprint branch)

**Objective:** land C1, C2, C8 code on the sprint branch.
**Confidence:** High
**Depends on:** Steps 1–3

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build -c Release --no-incremental tools/EfSim/EfSim.csproj -warnaserror 2>&1 | grep -E '[0-9]+ (Warning|Error)\(s\)'
git status --porcelain
git add tools/EfSim/SimConfig.cs tools/EfSim/PackRun.cs README.md
git commit -m "feat(ef-sim): expect.loot cases in sim.json" -m "Roll loot tables in one run of an area, optionally with the pack reloaded with extra mods counted as installed; fail on a forbidden id prefix or a required id that never rolls." -m "Co-Authored-By: <executor model> <noreply@anthropic.com>"
```

Expected: `0 Warning(s)`, `0 Error(s)`; porcelain shows exactly the three files (` M`). Use the executor's own model name in the trailer.

**Verification:** V1, V13.

**If this fails:** fix the build before committing; never `--amend` (hook blocks it) — make a follow-up commit instead.

---

### Step 5: NER `sim.json` — Gunsmith cases (on disk only)

**Objective:** C3.
**Confidence:** High
**Depends on:** Step 4
**Do-Projex:** Encouraged

**Files:** `../../../NightExpeditionsReborn/sim.json` (unversioned; **no git commands in NER**)

Write this script to `$T/addloot.py` (scratch, not the repo):

```python
import sys
src, dst = sys.argv[1], sys.argv[2]
old = '      { "area": "ne_sump", "missing": ["weapon"] }\n    ]\n  }\n}\n'
tables = '["ne_supplies", "ne_drop_scav", "ne_drop_enforcer_kit", "ne_arms", "ne_drop_welder", "ne_drop_foreman"]'
new = ('      { "area": "ne_sump", "missing": ["weapon"] }\n'
       '    ],\n'
       '    "loot": [\n'
       '      { "area": "ne_foundry", "tables": ' + tables + ', "rolls": 2000, "seed": 7, "never_prefix": "gsx_" },\n'
       '      { "area": "ne_foundry", "tables": ' + tables + ', "rolls": 2000, "seed": 7, "mods": ["gunsmith_expansion"],\n'
       '        "must_roll": ["gsx_ammo_10_reload", "gsx_ammo_22_reload", "gsx_ammo_10_steel", "gsx_crate_ammo", "gsx_crate_parts", "gsx_crate_weapon", "gsx_crate_collector"] }\n'
       '    ]\n'
       '  }\n'
       '}\n')
text = open(src, encoding='utf-8', newline='').read()
if text.count(old) != 1 or text.count('"loot"') != 0:
    sys.exit('anchor not found exactly once, or loot already present')
open(dst, 'w', encoding='utf-8', newline='').write(text.replace(old, new))
print('ok')
```

```bash
python -I "$T/addloot.py" "$T/ner-sim-before.json" "$NER/sim.json"
sha256sum "$NER/sim.json"
```

Expected: `ok`; `65ddd3c41150b0cec6f5a9c0261dfbd42d1e951e4b9d437c370d94d114d39078`.

Mapping to `CheckGunsmithLoot` (judgment check J1): case 0 = `plain` (no mods) with `gsx_` forbidden; case 1 = `with` (`gunsmith_expansion`) with the 7 ids of Program.cs:132 required; both `ne_foundry`, the 6 tables of Program.cs:123 in that order, 2000 rolls, seed 7.

**Verification:** V3.

**If this fails:** `cp "$T/ner-sim-before.json" "$NER/sim.json"` (restores `7133cc99…`).

---

### Step 6: Verify NER, probes, sample

**Objective:** C4, C5, C6, C1 errors.
**Confidence:** High
**Depends on:** Step 5
**Verify-Projex:** Encouraged

Run V2, V4, V5, V6 exactly as written in the suite. Probes edit only a scratch copy `$T/P`, never `$NER`.

**If this fails:** compare the failing output with the expected lines; a hash mismatch with otherwise-correct lines usually means message text drifted from Step 2 — re-copy the code verbatim.

---

### Step 7: Criterion-4 sweep + hygiene

**Objective:** C7.
**Confidence:** High
**Depends on:** Step 6

Run V7–V10, V12, V13. Remove `$T` afterwards (`rm -rf "$T"` on its own line). Build output under `bin/`/`obj/` is gitignored; `git status --porcelain` must be empty after the patch doc is committed.

---

## Verification Plan

### Setup (Git Bash, cwd = work-root; reuse `$T`, `NER`, `ef` from Step 0)

```bash
NER=../../../NightExpeditionsReborn
ef() { dotnet run -c Release --no-build --project tools/EfSim -- "$@" | tr -d '\r'; return ${PIPESTATUS[0]}; }
```

V5 helper (scratch copy of NER with the new `sim.json`; `probe OLD NEW` swaps one exact substring, runs, restores):

```bash
mkdir -p "$T/P"; cp -r "$NER/ExpeditionFramework" "$NER/Localization" "$NER/manifest.xml" "$NER/sim.json" "$T/P/"; cp "$T/P/sim.json" "$T/good.json"
probe() { python -I -c "import sys;p=sys.argv[1];t=open(p,encoding='utf-8',newline='').read();a,b=sys.argv[2],sys.argv[3];assert t.count(a)==1,a;open(p,'w',encoding='utf-8',newline='').write(t.replace(a,b))" "$T/P/sim.json" "$1" "$2"; ef run "$T/P" | grep -E '^(FAIL|ERROR)' | head -1; echo "exit=${PIPESTATUS[0]}"; cp "$T/good.json" "$T/P/sim.json"; }
```

### Deterministic Suite

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V1 | C1 / S1–2 | `DOTNET_CLI_UI_LANGUAGE=en dotnet build -c Release --no-incremental tools/EfSim/EfSim.csproj -warnaserror 2>&1 \| grep -E '[0-9]+ (Warning\|Error)\(s\)'` | `0 Warning(s)`, `0 Error(s)` | compile errors (e.g. CS0102 `Loot` clash), warnings |
| V2 | C4 / S2, S5 | `ef run "$NER" > "$T/ner-after.txt"; echo "exit=$?"; grep -c '^loot: 2 case(s), 0 failed$' "$T/ner-after.txt"; grep -cE '^(FAIL\|ERROR)' "$T/ner-after.txt"; tail -1 "$T/ner-after.txt"; sha256sum "$T/ner-after.txt"; grep '^ne_' "$T/ner-after.txt" \| sha256sum; diff "$T/ner-before.txt" "$T/ner-after.txt"` | `exit=0`; `1`; `0`; `ef-sim night_expeditions_reborn: all checks passed`; `933fe01596867350b0549036885908c749c5c86f7aeae241eed545cf8b69a17e`; `b9cdd05320977bf54c2efbc49860d2a99a4b2d887182c93f89b7f52a543ef646`; diff is exactly one added line `> loot: 2 case(s), 0 failed` | check not wired, NER case failing, default table shifted |
| V3 | C3 / S5 | `sha256sum "$NER/sim.json"` | `65ddd3c41150b0cec6f5a9c0261dfbd42d1e951e4b9d437c370d94d114d39078` | wrong/extra NER edit |
| V4 | C6 | `ef run samples/SampleArea > "$T/sample-after.txt"; echo "exit=$?"; cmp "$T/sample-before.txt" "$T/sample-after.txt" && echo SAME` | `exit=0`; `SAME` | default path changed |
| V5 | C5 | the 4 probes below (after the V5 helper) | each prints its pinned line and `exit=1` | check that never fails |
| V6 | C1 | the 7 probes below | each prints its pinned `ERROR sim.json: …` line and `exit=2` | lax parsing, unlocated errors |
| V7 | C7 | `dotnet run -c Release --project tests/Sim \| tr -d '\r' \| tail -1; echo "exit=${PIPESTATUS[0]}"` | `All checks passed`; `exit=0` | framework sim regression |
| V8 | C7 | `ef lint samples/SampleArea --strict \| tail -1; ef lint "$NER" --strict \| tail -1; ef lint "$NER" --strict --mod gunsmith_expansion \| tail -1` | `lint sample_area: 0 error(s), 0 warning(s)`; `lint night_expeditions_reborn: 0 error(s), 0 warning(s)` (twice) | lint regression |
| V9 | C7 | `S=$(cygpath -m "$(mktemp -d)"); DOTNET_CLI_UI_LANGUAGE=en dotnet build -c Release --no-incremental ExpeditionFramework.csproj "-p:GameManaged=E:/SteamLibrary/steamapps/common/Probably Stolen/Probably Stolen_Data/Managed/" "-p:InstallDir=$S/" 2>&1 \| grep -E '[0-9]+ (Warning\|Error)\(s\)'` | `0 Warning(s)`, `0 Error(s)` (never deploys to live `Mods/`) | tool code leaking into game assembly |
| V10 | C7 | `ef run "$NER" > /dev/null; echo "ner=$?"; ef run samples/SampleArea > /dev/null; echo "sample=$?"` | `ner=0`; `sample=0` | sweep: both packs green |
| V11 | C8 | `for k in '#### Loot' '"never_prefix": "gsx_"' '"must_roll"' 'every `expect.loot` case' 'loot: N case(s), M failed' 'no loot table'; do printf '%s ' "$(grep -cF -- "$k" README.md)"; done; echo` | six numbers each ≥ `1` | docs missing |
| V12 | baseline intact | `sha256sum "$NER/tests/Sim/Program.cs" "$NER/tests/Sim/Sim.csproj"` | the Step 0 hashes | NER C# sim edited |
| V13 | scope + hygiene | `P=$(git log --diff-filter=A --format=%H -- .projex/2611121215-ef-sim-loot-expect-plan.md); git diff --name-only "$P"..HEAD \| sort; git status --porcelain` | exactly `README.md`, `tools/EfSim/PackRun.cs`, `tools/EfSim/SimConfig.cs` (plus the patch doc under `.projex/` once committed); porcelain empty | engine/sample/test files touched; stray files |

**V5 probes** (`probe OLD NEW` → expected first `FAIL` line, then `exit=1`):

```bash
probe '"gsx_crate_collector"]' '"gsx_crate_collector", "gsx_nope"]'
# FAIL expect.loot[1]: gsx_nope never rolled
probe '"mods": ["gunsmith_expansion"],' ''
# FAIL expect.loot[1]: gsx_ammo_10_reload never rolled
probe '"seed": 7, "never_prefix": "gsx_" }' '"seed": 7, "never_prefix": "gsx_", "mods": ["gunsmith_expansion"] }'
# FAIL expect.loot[0]: rolled gsx_ammo_10_reload, gsx_ammo_10_steel, gsx_ammo_22_reload, gsx_crate_ammo, gsx_crate_collector, gsx_crate_parts, gsx_crate_weapon (never_prefix gsx_)
probe '"ne_drop_foreman"], "rolls": 2000, "seed": 7, "never_prefix"' '"ne_drop_foremen"], "rolls": 2000, "seed": 7, "never_prefix"'
# FAIL expect.loot[0]: no loot table night_expeditions_reborn:ne_drop_foremen
```

(Probe 2 also prints six more `never rolled` lines — `head -1` shows the first. Probe 3 proves the 7 ids really roll with the mod and the prefix check bites.)

**V6 probes** (expected line, then `exit=2`):

```bash
probe '"never_prefix": "gsx_" }' '"never_prefix": "gsx_", "x": 1 }'
# ERROR sim.json: expect.loot[0]: unknown key x
probe ', "never_prefix": "gsx_" }' ' }'
# ERROR sim.json: expect.loot[0]: needs never_prefix or must_roll
probe '"tables": ["ne_supplies", "ne_drop_scav", "ne_drop_enforcer_kit", "ne_arms", "ne_drop_welder", "ne_drop_foreman"], "rolls": 2000, "seed": 7, "never_prefix"' '"tables": [], "rolls": 2000, "seed": 7, "never_prefix"'
# ERROR sim.json: expect.loot[0].tables is empty
probe '"rolls": 2000, "seed": 7, "never_prefix"' '"rolls": 0, "seed": 7, "never_prefix"'
# ERROR sim.json: expect.loot[0].rolls must be above 0
probe '"never_prefix": "gsx_"' '"never_prefix": ""'
# ERROR sim.json: expect.loot[0].never_prefix is empty
probe '"area": "ne_foundry", "tables": ["ne_supplies", "ne_drop_scav", "ne_drop_enforcer_kit", "ne_arms", "ne_drop_welder", "ne_drop_foreman"], "rolls": 2000, "seed": 7, "never' '"area": "ne_nowhere", "tables": ["ne_supplies", "ne_drop_scav", "ne_drop_enforcer_kit", "ne_arms", "ne_drop_welder", "ne_drop_foreman"], "rolls": 2000, "seed": 7, "never'
# ERROR sim.json: no area night_expeditions_reborn:ne_nowhere
probe '"mods": ["gunsmith_expansion"]' '"mods": "gunsmith_expansion"'
# ERROR sim.json: expect.loot[1].mods must be a list
cmp "$T/good.json" "$T/P/sim.json" && echo RESTORED
```

Last line: `RESTORED`.

### Judgment Checks

| Check | Covers | Why not deterministic |
|-------|--------|-----------------------|
| J1 — NER cases faithfully mirror `CheckGunsmithLoot` (Program.cs:107-136): area, 6 tables in order, 2000 rolls, seed 7, prefix `gsx_` without mod, 7 required ids with `gunsmith_expansion` | C3 | faithfulness to C# source is a reading comparison; V3 pins the bytes, V5 proves both directions bite |
| J2 — `mods` load errors fail the case (code path `failures.AddRange(errors…)`, case skipped) | C2 | no NER/sample data produces a load error under a mod; a synthetic broken pack is out of scope — reviewed by reading Step 2 |

### Acceptance Criteria Validation

| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| C1 | V1, V6 | build clean; 7 probes exit 2 with pinned messages |
| C2 | V2, V5, J2 | `loot:` line; FAIL lines; mods reload |
| C3 | V3, J1 | `65ddd3c4…`; faithful mapping |
| C4 | V2 | exit 0, `933fe015…`, `ne_` lines `b9cdd053…`, one-line diff |
| C5 | V5 | 4 probes exit 1 |
| C6 | V4 | `SAME` |
| C7 | V7, V8, V9, V10 | all green |
| C8 | V11 | six counts ≥ 1 |

---

## Rollback Plan

1. Code: `git revert <Step 4 commit>` on the sprint branch (no `--amend`, no history rewrite).
2. NER: `cp "$T/ner-sim-before.json" "$NER/sim.json"`; confirm `sha256sum` = `7133cc992841c70d5ce619cd604137ac1bfb1aaa6a04ea74feb009360cc96fcf`. (If `$T` is gone: delete the `,\n    "loot": [ … ]` block so the file again ends with the packing list.)

---

## Notes

### Risks

- **NER parity regression** — mitigated: loot cases run after all rows with their own `Random`, and V2 pins the `ne_` lines to today's hash (`b9cdd053…`), which is the iteration-4 table (46/46 Δ 0.0). No C# re-run needed.
- **Hash drift from cosmetic code differences** (message wording, `OrderBy`) — mitigated by verbatim code; if V2's full hash differs but the diff is exactly the one `loot:` line and V5/V6 lines match, report the new hash rather than chasing it.
- **`Lint.Load` stricter than C#** (missing text keys fail a `mods` case) — intended; documented in README.

### Decisions taken by default (orchestrator may override)

- Field names: `tables`, `rolls`, `seed` (default 42, same base as rows), `mods`, `never_prefix`, `must_roll`; `area` required (no default area).
- Unknown table id is a run-time `FAIL` (exit 1), not a config error: table sets can differ under `mods`, so it is checked against the content actually loaded.
- Empty kit for the loot run (C# used `Smg(2,2)`; loot rolling never reads the kit) — no `kit` field added (YAGNI).
- One output line `loot: N case(s), M failed` (M = failing cases), printed only when cases exist — mirrors `packing:`.

### Split decision

`No split — single scope, within size budget` (one repo scope + one unversioned data file the objective names; 7 small steps tightly sequential).

### Open Questions

- [ ] none
