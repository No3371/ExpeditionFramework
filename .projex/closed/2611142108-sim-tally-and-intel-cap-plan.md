# Sim Tally + Area Intel Cap (`tally` / `--tally`, `intel_max`)

> **Status:** Complete
> **Completed:** 2026-11-14
> **Walkthrough:** 2611142158-sim-tally-and-intel-cap-walkthrough.md
> **Log:** 2611142108-sim-tally-and-intel-cap-log.md
> **Author:** Claude Opus 5.5 (plan step)
> **Parent:** 2611142022-night-set-pieces-proposal.md
> **Source:** 2611142022-night-set-pieces-proposal.md (NER `.projex/`, Complete (Accepted), NER `0aa7964`): § Balance Target "How it's measured" item 1 (tally); § Open Questions "Sprint scope" ask (2) (intel cap); § Next Steps 1
> **Related Projex:** 2611141406-damage-hit-context-plan.md (EF; proof method + command conventions reused) | 2611140627-text-params-and-sim-digest-walkthrough.md (EF; `--digest`, `digest2` baseline) | 2611140152-ef-ner-next-steps-proposal.md (NER; § D origin)
> **Worktree:** No — repo clean at plan time (`main` @ `77d5504`), no `projex/*` branches, ~12 files. NER path, proof baselines and the csproj's `GameManaged` path are relative to the main checkout; a worktree would break them.

---

## Summary

Two EF prerequisites for the NER night-set-pieces sprint.
1. **Tally:** sim.json top-level `"tally": ["fights", ...]`; `ef-sim run --tally` prints, after everything `run` / `run --digest` prints today, a second table: per row, the mean of each named `run.*` var at run end. Reads `run.Get` only — no RNG draw. Without `--tally`, output is byte-identical (a `tally` key alone changes nothing).
2. **Intel cap:** NER data alone **cannot** express "at most N cards per area per day across all slots, with priority" — slots roll independently and a slot's `when` can't see other slots' picks (on the map `intel()` reads an empty dict). New optional area key `intel_max` (expression, checked on the map like a slot's `when`): all slots roll exactly as today, then picks past the limit are dropped, keeping earlier slots first (list order = priority). Lint warns on two traps; tests, docs, ef-sim follow automatically through `IntelRoller.Roll`.

No pack uses either key, so Starter / Showcase / NER lint, plain `run` and `--digest` stay identical.

**Scope:** EF only — `src/Core` (Model, Schema, PackLoader, ExpeditionRun's `IntelRoller`), `tools/EfSim` (SimConfig, PackRun, Program), `tests/Sim`, `docs/` (intel, areas, testing, csharp-api). NER `run.fights`, `intel_max` values and slot order are the NER plan's.
**Estimated Changes:** 4 Core files (~35 lines), 3 EfSim files (~75 lines), 1 test file (~70 lines), 4 docs (~40 lines).

---

## Objective

### Problem / Gap / Need
- **Tally:** the proposal's balance bar needs fights / finds / credits per night. `ef-sim run` prints only cleared / KO / extracted per row (`tools/EfSim/PackRun.cs` `Play`); run vars are invisible to the table and to `--digest` (`docs/testing.md` § Run).
- **Intel cap:** NER's `night_area` template has 5 slots (`boss` chance 50, `rooms` counts `[35,45,20]`, `word_security|word_black|word_lower` chance 50, rep-gated). Per-day max today: Sump 5, Annex 5, Foundry 4 (`word_security` pool is empty in Sump/Foundry, `word_lower` empty in Annex/Foundry). Human wants ≤ floor(rooms/2) total, priority when more roll. `IntelRoller.Roll` (`src/Core/ExpeditionRun.cs`) rolls each slot alone; nothing caps the total.

### What "rooms" can mean on the intel side (finding)
- Cards roll once per day on the map (`IntelCards.Rolled` → `IntelRoller.Roll(area, rng, map.Root)`), before any run. There `run.*` reads 0 (`MapContext.Get` → 0; `docs/intel.md` § When it is rolled).
- `run.rooms` = `rooms.Count`, built by `ExpeditionRun.BuildLayout` at `Begin`: layout slot `count` expressions evaluated at run start; rooms with `when`, or `unique` pools running dry, can give fewer. Entrance is not in `rooms`. So the general room count is **not known** when cards roll.
- Known per area per day: only what the pack states. NER: layout = `floor` × `run.floors` (vars 4 | 5 | 5, constants; `floor` has no `when`, `unique: false`) + `final` → `run.rooms` = 5 | 6 | 6 every night → floor(rooms/2) = **2 | 3 | 3** (Sump | Annex | Foundry).
- Therefore `intel_max` is an expression evaluated in the map scope (same as `when`: `game.` and `memory.` work, `run.` reads 0). NER writes the numbers (template `3`, Sump override `2`; `extends` keys replace the template's, `PackLoader` template merge). Lint warns if `intel_max` reads `run.` (the obvious trap: `"run.rooms / 2"` would silently mean 0).

### Success Criteria
- [ ] **SC1** sim.json accepts optional top-level `tally`: non-empty list of distinct names matching `^[A-Za-z_][A-Za-z0-9_]*$`; anything else → `ERROR sim.json: ...`, exit 2.
- [ ] **SC2** `run --tally` appends, after the summary line and any `digest` lines, a header line, a column header and one line per row with the mean (`0.00`, invariant) of each tally var at run end; then `WARN tally: run.NAME was 0 in every run` per all-zero name. `--tally` without a `tally` key → `ERROR sim.json: --tally needs a "tally" list of run variable names`, exit 2. Exit code otherwise unchanged by the tally.
- [ ] **SC3** Tally draws no RNG and changes no output before it: `run P` output is an exact prefix of `run P --tally`; `run P --digest` an exact prefix of `run P --digest --tally`; a `tally` key without the flag leaves output byte-identical.
- [ ] **SC4** Areas accept optional `intel_max` (number or expression). When set, `IntelRoller.Roll` rolls every slot exactly as without it (same dice, same order), then evaluates `intel_max` at the same scope, treats < 0 as 0, and keeps picks in slot order (earlier slot first; within a slot its first picks) up to the limit; the rest are dropped. Every slot stays a key. Unset → today's behaviour, no extra evaluation.
- [ ] **SC5** Lint: bad `intel_max` expression → load problem (via `ValueOf`); `intel_max` on an area with no `intel` slots → warning; `intel_max` text reading `run.` → warning.
- [ ] **SC6** `tests/Sim` covers SC4–SC5 and ends `All checks passed`.
- [ ] **SC7** Docs: `docs/intel.md` (new § Limiting the cards, incl. the room-count finding), `docs/areas.md` key row, `docs/testing.md` (`--tally`, `tally` key, `intel.SLOT` counts after the limit), `docs/csharp-api.md` (`IntelRoller.Roll` applies `intel_max`).
- [ ] **SC8** Proof bar vs `c-proof`: Starter / Showcase / NER `lint --strict` identical to `base/*-lint.txt`; plain `run` byte-identical to `base/*-run.txt`; `--digest` lines identical to `digest2/*-digest-base.txt`.
- [ ] **SC9** Game assembly compiles with redirected `InstallDir` (`$Q/game-install/`): exit 0, `0 Error(s)`; live `Mods/ExpeditionFramework/` untouched. No change to `api/`, `src/Game/`, `*.csproj`; no removed/changed public signature in `src/Core`.

### Out of Scope
- NER data (`run.fights` counter, `intel_max` values, slot reorder, sim rows) — NER plan.
- Computing a run's room count on the map (evaluating layout counts with area vars): bigger, changes `MapContext` semantics; rejected (§ Notes).
- A per-slot `priority` field separate from list order (§ Notes).
- Tally of text vars, `game.` / `memory.` reads, or per-night distributions (means only).
- Building into the live game `Mods/` folder.

---

## Context

### Current State
- `IntelRoller.Roll(AreaDef, Func<int,int> roll, IScope scope)` → `Dictionary<string, List<int>>` (slot id → picked pool positions). Every slot is a key (game `IntelCards.Rolled` indexes each). Gated slot rolls no dice. Callers: `src/Game/IntelCards.cs` `Rolled` (writes pick counters; a missing index → counter 0 → card hidden), `tools/EfSim/PackRun.cs` `RollIntel` (intel rows; own `Random`), `tests/Sim` `TestMapMemory`. All consume the dict as-is → trimming inside `Roll` needs no caller change.
- `CompileArea` (`src/Core/PackLoader.cs`) compiles `intel` via `CompileIntel`; area shape in `Schema.Shapes["areas"]`; `ValueOf(node, pack, at)` compiles number-or-expression, reporting errors as problems; `Warn(at, msg)` exists.
- `PackRun.Run`: parses args (`--runs`, `--digest`), reads `SimConfig`, loads, prints table header, `Play` per row prints its table line, then WARN rule lines, FAILs, summary `ef-sim ID: ...`, then `digest` lines ("After the summary, so the plain output stays an exact prefix").
- `SimConfig.Read`: strict `Only(top, ..., "version","kits","bots","runs","expect","bridge")`.
- `ExpeditionRun.Get(name)`: engine vars (`depth`, `rooms`, `credits`, ...) or `vars[name]`, else 0. Pure read; `credits`/`ground` read bridge properties without side effects.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `src/Core/Model.cs` | `AreaDef` | + `public Value IntelMax;` |
| `src/Core/Schema.cs` | area shape | + `("intel_max", Field.Any)` |
| `src/Core/PackLoader.cs` | `CompileArea` | compile `intel_max`; 2 warnings |
| `src/Core/ExpeditionRun.cs` | `IntelRoller` | apply limit after rolling |
| `tests/Sim/Program.cs` | engine tests | + `TestIntelMax()` |
| `tools/EfSim/SimConfig.cs` | sim.json reader | + `Tally` list |
| `tools/EfSim/PackRun.cs` | `run` | `--tally`, sums in `Play`, table after digest |
| `tools/EfSim/Program.cs` | usage | + `[--tally]` |
| `docs/intel.md`, `docs/areas.md`, `docs/testing.md`, `docs/csharp-api.md` | docs | as SC7 |

### Dependencies
- **Requires:** EF `main` @ `77d5504` (or later with only `.projex/` changes); NER `master` @ `0aa7964` (data identical to `46d7d4c`; diff is `.projex/` only); baselines `$P/base/`, `$P/digest2/`.
- **Blocks:** NER sprint plan (`run.fights` + baseline tally; Sump set pieces; Foundry KO; intel cap data).

### Constraints
- Plain `run`, `--digest` lines, `lint --strict` identical for all three packs (SC8). No pack data in EF changes.
- Never build `ExpeditionFramework.csproj` without `-p:InstallDir=$Q/game-install/` (its `InstallMod` target copies into the live `Mods/`). Building `tools/EfSim` and `tests/Sim` is safe (they compile Core sources directly).
- Source hygiene (SKILL.md § Source Hygiene): no projex ids / labels in comments; non-obvious choices carry rationale naming the rejected alternative.
- NER read-only.

### Assumptions
- A1: `extends` replaces keys (`PackLoader` "Keys it sets replace the template's") → NER can override `intel_max` per area. Verified by reading; NER plan re-verifies with lint.
- A2: No Starter / Showcase / NER / framework `main.json` / SampleArea uses `intel_max` or `tally` (new keys; Step 0 greps).
- A3: Bash is Git Bash; `cmp`, `grep -aU`, `head -n` available (used by the prior plan's proof).
- A4: dotnet may print localized messages; every command sets `DOTNET_CLI_UI_LANGUAGE=en`.

### Impact Analysis
- **Direct:** files above.
- **Adjacent:** game `IntelCards` (fewer picks → cards hidden, tone override per index unchanged); `ExpeditionRun.Intel` in runs (sees trimmed picks: `intel(id)`, `schedule` with `intel`, `spawn`/`cast` with `intel`); ef-sim `intel.SLOT` expects count trimmed nights.
- **Downstream:** NER sprint adopts both keys; C# mods calling `IntelRoller.Roll` get the limit automatically (documented).

---

## Implementation

### Overview
**0** reconfirm baselines → **1** `intel_max` engine + loader → **2** tests → **3** ef-sim tally → **4** docs → **5** proof bar, probes, game compile. Executor's commit convention applies (one commit per step if executing via execute/patch with commits; do not push).

Command conventions (Git Bash, EF root as cwd):
```bash
export DOTNET_CLI_UI_LANGUAGE=en
S=<orchestrator session scratchpad>   # holds c-proof/; orchestrator supplies the path
P=$S/c-proof                          # $P/base/{ST,SC,NER}-{lint,run}.txt, $P/digest2/{ST,SC,NER}-digest-base.txt
Q=$S/d-proof                          # this plan's outputs
NER=../NightExpeditionsReborn; SC=docs/examples/Showcase; ST=docs/examples/Starter
SIM="dotnet run -c Release --no-build --project tools/EfSim --"
LIVE="../../Mods/ExpeditionFramework/ExpeditionFramework.dll"
```

---

### Step 0: Preflight + Baseline Reconfirm

**Objective:** Prove baselines hold before any change (SC8 precondition).
**Confidence:** High
**Depends on:** None
**Do-Projex:** Encouraged

**Files:** none (writes `$Q/s0/*`)

**Changes:**
```bash
git rev-parse --short HEAD                                     # 77d5504, or later with only .projex/ changes
git diff --stat 77d5504 HEAD -- src api tools tests docs ExpeditionFramework Localization samples '*.csproj'   # empty
git status --porcelain -- src api tools tests docs             # empty
git -C $NER rev-parse --short HEAD                             # 0aa7964
git -C $NER status --porcelain -- ExpeditionFramework Localization sim.json   # empty
git grep -n -E '"intel_max"|"tally"' -- docs/examples samples ExpeditionFramework; git -C $NER grep -n -E '"intel_max"|"tally"' -- ExpeditionFramework sim.json   # no output (A2)
mkdir -p $Q/s0
{ stat -c '%Y %s' "$LIVE" 2>/dev/null || echo "no live dll"; } > $Q/live-before.txt
dotnet build -c Release tools/EfSim
for n in ST SC NER; do d=$(eval echo \$$n)
  $SIM lint "$d" --strict > $Q/s0/$n-lint.txt
  $SIM run  "$d"          > $Q/s0/$n-run.txt
  $SIM run  "$d" --digest > $Q/s0/$n-d.txt
  cmp $Q/s0/$n-lint.txt $P/base/$n-lint.txt && echo "$n lint ok"
  cmp $Q/s0/$n-run.txt  $P/base/$n-run.txt  && echo "$n run ok"
  grep -aU '^digest ' $Q/s0/$n-d.txt | cmp - $P/digest2/$n-digest-base.txt && echo "$n digest ok"
done
dotnet run -c Release --project tests/Sim | tail -n 1          # All checks passed
```
(NER run ≈ 2m40s; run packs sequentially.)

**Rationale:** `c-proof/base` predates NER `0aa7964`; facts record it identical at `46d7d4c` and the diff since is `.projex/` only. Reconfirm anyway — a failing compare makes Step 5 meaningless.

**Verification:** V0a, V0b, V0c, V0d.

**If this fails:** drift or dirty → stop, report. Any `cmp` differs → stop the plan, report pack + first differing line; do not re-baseline. tests/Sim red → stop, report.

---

### Step 1: `intel_max` — Model, Schema, Loader, Roller

**Objective:** SC4, SC5.
**Confidence:** High
**Depends on:** Step 0
**Verify-Projex:** Encouraged

**Files:**
- `src/Core/Model.cs`
- `src/Core/Schema.cs`
- `src/Core/PackLoader.cs`
- `src/Core/ExpeditionRun.cs`

**Changes:**

```csharp
// Model.cs — AreaDef, after `public List<IntelSlot> Intel = ...;`
        /// <summary>
        /// The most intel cards the area shows a day, across all its slots, checked on the map when the day is rolled. Null
        /// means no limit.
        /// </summary>
        public Value IntelMax;
```

```csharp
// Schema.cs — Shapes["areas"]: add ("intel_max", Field.Any) after ("intel", Field.Any)
                ("intel", Field.Any), ("intel_max", Field.Any), ("hud", Field.Any), ...
```

```csharp
// PackLoader.cs — CompileArea, right after the `foreach (object e in List(n, "intel"))` loop
            if (n.TryGetValue("intel_max", out object intelMax))
            {
                d.IntelMax = ValueOf(intelMax, d.Pack, at + ".intel_max");
                if (d.Intel.Count == 0) Warn(at + ".intel_max", "limits intel cards, but the area has no intel");
                // The day's cards are rolled on the map before any run, so run.* (run.rooms included) reads 0 there.
                if (intelMax is string text && RunRead.IsMatch(text)) Warn(at + ".intel_max", "is checked on the map, where run.* reads 0; write the number");
            }
// PackLoader.cs — next to the other static Regex fields (TextValue, ...)
        private static readonly Regex RunRead = new Regex("(?<![A-Za-z0-9_.$])run\\.");
```

```csharp
// ExpeditionRun.cs — IntelRoller.Roll: before `return result;`
            if (area.IntelMax != null) Limit(area, result, Math.Max(0, area.IntelMax(scope)));
            return result;
        }

        // Every slot rolls first, as it would without a limit, so setting intel_max changes which cards show but not the
        // dice the slots draw. Trimming slot by slot instead would make a later slot's picks depend on the limit. Earlier
        // slots keep their cards first; within a slot, its first picks.
        private static void Limit(AreaDef area, Dictionary<string, List<int>> result, int left)
        {
            foreach (IntelSlot slot in area.Intel)
            {
                List<int> picks = result[slot.Id];
                if (picks.Count > left) picks.RemoveRange(left, picks.Count - left);
                left -= picks.Count;
            }
        }
```
Also extend `Roll`'s `<summary>`: "Then, if the area has `intel_max`, keeps that many picks in slot order and drops the rest."

**Rationale:** Trim after the full roll keeps RNG stream identical (cap changes only which cards show; ef-sim intel `Random` and the game RNG advance the same). List order as priority needs no new field. Evaluate only when set → zero behaviour change for existing packs. `Math.Max(0, …)`: negative reads as "no cards", not an exception from `RemoveRange`. Two warnings target the real traps; `ValueOf` already reports bad expressions.

**Verification:** V1a (EfSim build), V1b (ST/SC smoke), V2a–V2b after Step 2.

**If this fails:** `git checkout -- src/Core` (pre-commit) or revert the step commit.

---

### Step 2: Tests — `TestIntelMax`

**Objective:** SC4, SC5, SC6.
**Confidence:** High
**Depends on:** Step 1
**Do-Projex:** Encouraged

**Files:** `tests/Sim/Program.cs`

**Changes:** add `TestIntelMax();` in `Main` right after `TestMapMemory();`, and the method after `TestMapMemory`. Build the pack like `TestMapMemory` (`PackSet` + `set.Add("a", ...)`, `set.Compile(out problems, out warnings)`). Pack:
- room `r` whose `enter` sets `memory.cap` (so the read isn't warned), pool `leads: ["r","r2","r3"]` with rooms `r2`, `r3`;
- area `a`: `"intel_max": "memory.cap"`, slots in order `first` (`chance` 100), `many` (`counts` `[0,0,100]` → 2 picks), `last` (`chance` 100), each with `title`/`description` `"k"`.

Checks (roll `n => 1` unless stated; `MapContext(area.Id, (p,k,a)=>k, name=>0, store)`):
1. pack loads with 0 problems, 0 warnings.
2. `memory.cap` 9 → counts `first 1, many 2, last 1` (limit above total: unchanged).
3. cap 2 → `1, 1, 0`; `many`'s kept pick equals its first pick at cap 9 (first picks kept).
4. cap 0 → `0, 0, 0`, and all three ids are keys.
5. cap −3 → `0, 0, 0` (negative reads as 0, no throw).
6. dice log (`logged` as in `TestMapMemory`) identical at cap 9 and cap 1 (limit draws no dice and doesn't change the slots' draws).
7. the same area without `intel_max` (second pack `"b"`) gives `1, 2, 1` and `IntelMax == null`.
8. lint: `intel_max` on an area without `intel` → exactly one warning containing `intel_max`; `"intel_max": "run.rooms / 2"` (with intel slots) → exactly one warning containing `run.*`; `"intel_max": "1 +"` → one problem at `.intel_max`. Use the existing `ProblemsIn` helper for problems; for warnings compile via `PackSet` and read `warnings` (pattern of `TestMapMemory`).

**Rationale:** Engine behaviour lives in `IntelRoller`, which tests/Sim compiles directly; ef-sim (Step 5 probe) proves the wiring end-to-end.

**Verification:** V2a, V2b.

**If this fails:** fix the test or Step 1; never weaken a check to pass.

---

### Step 3: ef-sim `tally` + `--tally`

**Objective:** SC1, SC2, SC3.
**Confidence:** High
**Depends on:** Step 0 (independent of Steps 1–2)
**Verify-Projex:** Encouraged

**Files:**
- `tools/EfSim/SimConfig.cs`
- `tools/EfSim/PackRun.cs`
- `tools/EfSim/Program.cs`

**Changes:**

```csharp
// SimConfig.cs — field
    public readonly List<string> Tally = new List<string>();          // run.* vars `run --tally` averages per row
// Read: Only(top, "sim.json", "version", "kits", "bots", "runs", "expect", "bridge", "tally");
// after the bridge line:
        if (top.TryGetValue("tally", out object t))
        {
            List<object> names = List(t, "tally");
            if (names.Count == 0) throw new FormatException("tally is empty");
            for (int n = 0; n < names.Count; n++)
            {
                string name = Str(names[n], "tally[" + n + "]");
                if (!VarName.IsMatch(name)) throw new FormatException("tally[" + n + "] must be a run variable name, without run.: " + name);
                if (config.Tally.Contains(name)) throw new FormatException("tally: " + name + " is listed twice");
                config.Tally.Add(name);
            }
        }
    private static readonly Regex VarName = new Regex("^[A-Za-z_][A-Za-z0-9_]*$");   // + using System.Text.RegularExpressions
```

```csharp
// PackRun.cs
    private const string Usage = "usage: ef-sim run <pack folder> [--runs <n>] [--digest] [--tally]";
// args loop: else if (args[i] == "--tally") tally = true;   (bool tally = false;)
// after config is read (before Lint.Load):
        if (tally && config.Tally.Count == 0) { Console.WriteLine("ERROR sim.json: --tally needs a \"tally\" list of run variable names"); return 2; }
// Play gets a parameter `IReadOnlyList<string> tally` (null when !tally); RowResult gets `public double[] Tally;`
// In Play: `double[] sums = tally != null ? new double[tally.Count] : null;` and, right after `rowDigest?.Add(...)`:
            if (sums != null) for (int i = 0; i < sums.Length; i++) sums[i] += run.Get(tally[i]);
// before `return result;`: result.Tally = sums?.Select(s => s / runs).ToArray();
// In Run: collect `var tallyRows = new List<(SimRow Row, int Runs, double[] Means)>();` per row when tally is on.
// Assign to an outer `tallyLines` list (like rowDigests) only inside `if (content.Problems.Count == 0)`.
// After `foreach (string line in rowDigests) Console.WriteLine(line);`:
        foreach (string line in tallyLines) Console.WriteLine(line);
```
Tally block format (built in a helper `TallyLines(IReadOnlyList<string> names, rows)`; invariant culture; `w_i = Math.Max(7, names[i].Length)`):
```
tally: mean of run.NAME at run end
{0,-18} {1,-12} {2,-8} {3,6}  then per name: " " + name.PadLeft(w_i)          ← header with "area","kit","bot","runs"
{0,-18} {1,-12} {2,-8} {3,6}  then per name: " " + mean.ToString("0.00").PadLeft(w_i)   ← one per row, sim.json order
WARN tally: run.NAME was 0 in every run                                        ← per name whose sums are 0 on every row
```
`runs` column = the row's actual run count (honours `--runs`).

```csharp
// Program.cs usage line
                Console.WriteLine("       ef-sim run <pack folder> [--runs <n>] [--digest] [--tally]");
```
Comment at the `Console.WriteLine(line)` for tally: after the digest lines, so `run` and `run --digest` output each stay an exact prefix.

**Rationale:** `run.Get` is a pure read (no `Roll`, no bridge mutation), so RNG streams and every earlier line are untouched; printing last keeps both prefix properties. Sums are taken even when a run threw (its vars are what it reached), matching how outcome counting still runs after a throw. All-zero WARN catches tally typos (`fight` vs `fights`) without affecting exit code; printed only under `--tally`, so plain output never changes. Strict parsing matches sim.json's "unknown key or wrong type is an error".

**Verification:** V1a, V3a–V3f.

**If this fails:** `git checkout -- tools/EfSim` (pre-commit) or revert the step commit.

---

### Step 4: Docs

**Objective:** SC7.
**Confidence:** High
**Depends on:** Steps 1, 3
**Do-Projex:** Encouraged

**Files:** `docs/intel.md`, `docs/areas.md`, `docs/testing.md`, `docs/csharp-api.md`

**Changes:**
- `docs/intel.md`: new `## Limiting the cards` before `## Using intel in a run`:
  - `"intel_max": 2` on the area (number or expression): the most cards the area shows a day, across all its slots. Slots roll as usual; when more than that come up, earlier slots in the `intel` list keep their cards first, and within a slot its first picks. Below 0 reads as 0. Unset: no limit.
  - Checked when the day is rolled, like `when`: `memory.` and `game.` work, `run.` reads 0. The day's cards come before any run, and a run's rooms are only laid out when it begins (`run.rooms` belongs to that run), so write the number: e.g. a layout of 4 floors plus a final room makes 5 rooms → `"intel_max": 2` for half. Lint warns when `intel_max` reads `run.`. A template's `intel_max` can be overridden per area.
  - Note: the limit drops picks, so `intel(id)`, `schedule` with `intel`, and ef-sim's `intel.SLOT` see only the cards that show.
- `docs/areas.md` key table: row after `intel`: `` | `intel_max` | expression | The most intel cards a day across all slots. [Intel cards](intel.md#limiting-the-cards) | ``.
- `docs/testing.md`:
  - § Run: usage line `[--runs 200] [--digest] [--tally]`; paragraph after the `--digest` paragraph: `--tally` adds, after everything else (digest lines included), a table of the per-row mean of each `run.NAME` listed in sim.json `tally`, read at run end; draws no random number, so `run` and `run --digest` stay exact prefixes; a name that is 0 in every run prints `WARN tally: ...`; `--tally` without `tally` is a bad argument (exit 2). Short example block.
  - § sim.json key table: row `` | `tally` | Run variable names (without `run.`) that `--tally` averages | ``.
  - § Nights in a row, `expect` row: append "counted after the area's `intel_max`".
- `docs/csharp-api.md`: extend the `IntelRoller.Roll` sentence: "…and applies the area's `intel_max` after every slot has rolled."

**Rationale:** Each new key documented where readers look for it; the room-count limitation is stated where the trap is.

**Verification:** V4a.

**If this fails:** fix the text; docs don't affect gates.

---

### Step 5: Proof Bar, Probes, Game Compile

**Objective:** SC2, SC3, SC4 end-to-end, SC8, SC9.
**Confidence:** High
**Depends on:** Steps 1–4

**Files:** none in repo (writes `$Q/after/*`, `$Q/probe/*`, `$Q/game-install/`)

**Changes:**
```bash
dotnet build -c Release tools/EfSim
mkdir -p $Q/after
for n in ST SC NER; do d=$(eval echo \$$n)
  $SIM lint "$d" --strict > $Q/after/$n-lint.txt
  $SIM run  "$d"          > $Q/after/$n-run.txt
  $SIM run  "$d" --digest > $Q/after/$n-d.txt
  cmp $Q/after/$n-lint.txt $P/base/$n-lint.txt && echo "$n lint ok"
  cmp $Q/after/$n-run.txt  $P/base/$n-run.txt  && echo "$n run ok"
  grep -aU '^digest ' $Q/after/$n-d.txt | cmp - $P/digest2/$n-digest-base.txt && echo "$n digest ok"
done
dotnet run -c Release --project tests/Sim | tail -n 1

# Probe pack: a Showcase copy (outside the repo). Tally on it.
rm -rf $Q/probe && mkdir -p $Q/probe && cp -r $SC $Q/probe/sc
python - "$Q/probe/sc/sim.json" <<'EOF'
import json,sys; p=sys.argv[1]; d=json.load(open(p,encoding='utf-8')); d["tally"]=["rooms","depth","deals","nosuch"]; json.dump(d,open(p,'w',encoding='utf-8'),indent=1)
EOF
$SIM run $Q/probe/sc            > $Q/probe/plain.txt      # tally key, no flag
$SIM run $Q/probe/sc --tally    > $Q/probe/t1.txt
$SIM run $Q/probe/sc --tally    > $Q/probe/t2.txt
$SIM run $Q/probe/sc --digest   > $Q/probe/d.txt
$SIM run $Q/probe/sc --digest --tally > $Q/probe/dt.txt
cmp $Q/probe/plain.txt $P/base/SC-run.txt && echo "tally key inert"
head -c $(stat -c %s $Q/probe/plain.txt) $Q/probe/t1.txt | cmp - $Q/probe/plain.txt && echo "plain prefix"
head -c $(stat -c %s $Q/probe/d.txt) $Q/probe/dt.txt | cmp - $Q/probe/d.txt && echo "digest prefix"
grep -aU '^digest ' $Q/probe/dt.txt | cmp - $P/digest2/SC-digest-base.txt && echo "digest unchanged"
cmp $Q/probe/t1.txt $Q/probe/t2.txt && echo "tally deterministic"
tail -n +$(( $(wc -l < $Q/probe/plain.txt) + 1 )) $Q/probe/t1.txt > $Q/probe/tally.txt; cat $Q/probe/tally.txt
grep -c '^night_market ' $Q/probe/tally.txt                     # 3
grep -c '^WARN tally: run.nosuch was 0 in every run$' $Q/probe/tally.txt   # 1
grep -c 'WARN tally: run.\(rooms\|depth\)' $Q/probe/tally.txt             # 0 (engine vars, never 0 at run end)
$SIM run $SC --tally; echo "exit $?"                             # ERROR ... --tally needs ..., exit 2
python - "$Q/probe/sc/sim.json" <<'EOF'
import json,sys; p=sys.argv[1]; d=json.load(open(p,encoding='utf-8')); d["tally"]=["run.heat"]; json.dump(d,open(p,'w',encoding='utf-8'),indent=1)
EOF
$SIM run $Q/probe/sc; echo "exit $?"                             # ERROR sim.json: tally[0] must be a run variable name..., exit 2

# Intel cap end-to-end through ef-sim: Showcase copy with intel_max 0 starves its intel row.
rm -rf $Q/probe/cap && cp -r $SC $Q/probe/cap
sed -i '0,/"night_market": {/s//"night_market": { "intel_max": 0,/' $Q/probe/cap/ExpeditionFramework/main.json
$SIM lint $Q/probe/cap --strict; echo "exit $?"                  # clean, exit 0
$SIM run  $Q/probe/cap > $Q/probe/cap.txt; echo "exit $?"        # exit 1
grep -c '^FAIL runs\[2\]: intel.fence should be at least 1, got 0$' $Q/probe/cap.txt   # 1
sed -i 's/"intel_max": 0,/"intel_max": "run.rooms",/' $Q/probe/cap/ExpeditionFramework/main.json
$SIM lint $Q/probe/cap --strict | grep -c 'intel_max.*run\.\*'   # 1

# Game assembly, redirected install only (human-approved). NEVER without -p:InstallDir.
dotnet build -c Release ExpeditionFramework.csproj -p:InstallDir=$Q/game-install/ 2>&1 | tee $Q/game-build.txt | tail -n 5
ls $Q/game-install/ExpeditionFramework.dll
{ stat -c '%Y %s' "$LIVE" 2>/dev/null || echo "no live dll"; } > $Q/live-after.txt
cmp $Q/live-before.txt $Q/live-after.txt && echo "live untouched"
git diff --stat 77d5504 -- api src/Game '*.csproj'               # empty
git diff 77d5504 -- src/Core | grep -E '^-[[:space:]]*(public|protected) '   # no output
```
Clean up: `rm -rf $Q/probe` optional; `$Q/game-install/` may stay (scratch). Remove repo `obj/`/`bin/` only if the build created untracked non-ignored files (`git status --porcelain` must show only intended changes).

**Rationale:** Identity on three packs proves no behaviour change for packs without the keys; probes prove the new paths end-to-end (prefix, determinism, inert key, cap through `RollIntel`, lint trap warning); redirected build proves the game assembly compiles without deploying.

**Verification:** V5a–V5k.

**If this fails:** identity break → the change leaks into key-less packs: find the line (likely an unconditional evaluation or output) and fix; never re-baseline. Probe mismatch → fix Step 1/3. Game build error → fix (Core is shared with the game; `netstandard2.1`, no newer APIs). Live dll changed → stop and report to the human immediately.

---

## Verification Plan

### Deterministic Suite

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V0a | Step 0 | `git diff --stat 77d5504 HEAD -- src api tools tests docs ExpeditionFramework Localization samples '*.csproj'`; `git status --porcelain -- src api tools tests docs` | both empty | EF drift |
| V0b | Step 0 | `git -C $NER rev-parse --short HEAD`; `git -C $NER status --porcelain -- ExpeditionFramework Localization sim.json`; A2 greps | `0aa7964`; empty; no output | Wrong/dirty NER; key already used |
| V0c | Step 0 | Step 0 loop (9 compares) | 9 × `ok` | Baseline no longer holds → stop |
| V0d | Step 0 | `dotnet run -c Release --project tests/Sim \| tail -n 1` | `All checks passed` | Pre-existing red |
| V1a | Steps 1, 3 | `dotnet build -c Release tools/EfSim` | exit 0, `0 Error(s)` | Compile errors |
| V1b | Step 1 smoke | `for n in ST SC; do d=$(eval echo \$$n); $SIM lint "$d" --strict \| cmp - $P/base/$n-lint.txt; $SIM run "$d" \| cmp - $P/base/$n-run.txt; done` | all exit 0 | Early regression |
| V2a | SC4–SC6 | `dotnet run -c Release --project tests/Sim \| tail -n 1` | `All checks passed` | Cap / lint regressions |
| V2b | SC6 | `grep -c 'TestIntelMax()' tests/Sim/Program.cs` | `2` | Test not wired |
| V3a | SC3 | `cmp $Q/probe/plain.txt $P/base/SC-run.txt` | exit 0 | `tally` key leaks into plain output |
| V3b | SC3 | the two `head -c … \| cmp` lines | `plain prefix`, `digest prefix` | Tally printed early / changes earlier lines |
| V3c | SC3 | `grep -aU '^digest ' $Q/probe/dt.txt \| cmp - $P/digest2/SC-digest-base.txt`; `cmp $Q/probe/t1.txt $Q/probe/t2.txt` | both exit 0 | Tally draws RNG / nondeterminism |
| V3d | SC2 | `grep -c '^night_market ' $Q/probe/tally.txt`; `head -n 1 $Q/probe/tally.txt` | `3`; `tally: mean of run.NAME at run end` | Rows missing / header wrong |
| V3e | SC2 | the two `grep -c 'WARN tally…'` lines | `1`; `0` | All-zero warning wrong |
| V3f | SC1, SC2 | `$SIM run $SC --tally`; `$SIM run` on `tally: ["run.heat"]` | both print `ERROR sim.json: …`, exit 2 | Missing key / bad name accepted |
| V4a | SC7 | `grep -c 'intel_max' docs/intel.md docs/areas.md docs/testing.md docs/csharp-api.md`; `grep -c -- '--tally' docs/testing.md`; `grep -c '## Limiting the cards' docs/intel.md` | each ≥ `1` | Missing doc update |
| V5a | SC8 | `cmp $Q/after/$n-lint.txt $P/base/$n-lint.txt` (×3) | exit 0 | Lint drift |
| V5b | SC8 | `cmp $Q/after/$n-run.txt $P/base/$n-run.txt` (×3) | exit 0 | Behaviour change for key-less packs |
| V5c | SC8 | `grep -aU '^digest ' $Q/after/$n-d.txt \| cmp - $P/digest2/$n-digest-base.txt` (×3) | exit 0 | Hidden log/memory/rep/item change |
| V5d | SC4 | `grep -c '^FAIL runs\[2\]: intel.fence should be at least 1, got 0$' $Q/probe/cap.txt`; cap lint exit | `1`; lint exit 0 | Cap not applied via `RollIntel` / false lint |
| V5e | SC5 | `$SIM lint $Q/probe/cap --strict \| grep -c 'intel_max.*run\.\*'` | `1` | `run.` trap not warned |
| V5f | SC9 | `dotnet build -c Release ExpeditionFramework.csproj -p:InstallDir=$Q/game-install/` | exit 0, `0 Error(s)`; `$Q/game-install/ExpeditionFramework.dll` exists | Game assembly break |
| V5g | SC9 | `cmp $Q/live-before.txt $Q/live-after.txt` | exit 0 (`live untouched`) | Deployed into live game |
| V5h | SC9 | `git diff --stat 77d5504 -- api src/Game '*.csproj'` | empty | API / game / csproj touched |
| V5i | SC9 | `git diff 77d5504 -- src/Core \| grep -E '^-[[:space:]]*(public\|protected) '` | no output | Public signature changed |
| V5j | SC6 | `dotnet run -c Release --project tests/Sim \| tail -n 1` | `All checks passed` | Integration |
| V5k | all | `git status --porcelain` | only the 12 planned files (+ `.projex/` docs) | Stray build output / scratch in repo |

### Judgment Checks

| Check | Covers | Why not deterministic |
|-------|--------|-----------------------|
| Tally table readable; columns align with long names (`credits_found`) | SC2 | Layout quality is visual |
| `docs/intel.md` § Limiting the cards explains the room-count limit clearly enough that an author doesn't write `run.rooms` | SC7 | Clarity |
| New comments meet Source Hygiene (rationale names rejected alternative; no projex refs) | Steps 1, 3 | Prose judgment; audit pass |

### Acceptance Criteria Validation
| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| SC1 | V3f | bad/missing tally → exit 2 |
| SC2 | V3d, V3e, V3f | table + warnings as specified |
| SC3 | V3a, V3b, V3c | inert key, exact prefixes, no RNG |
| SC4 | V2a, V5d | cap trims in slot order; same dice |
| SC5 | V2a, V5e | two warnings + problem |
| SC6 | V2a, V2b, V5j | green, wired |
| SC7 | V4a + judgment | docs present |
| SC8 | V5a, V5b, V5c | 9 × identical |
| SC9 | V5f–V5i | compiles, nothing deployed, no API change |

---

## Rollback Plan

1. Before any commit: `git checkout -- src/Core tools/EfSim tests/Sim docs` (named paths only).
2. After step commits: `git revert` each step commit, newest first (no `--amend`, no branch deletion — hook-blocked).
3. Scratch `$Q/` needs no rollback. If the live dll changed (V5g), tell the human; don't try to restore it yourself.

---

## Notes

### Decisions
- **Intel cap needs EF.** Data alone can't: slot `when` sees no other slot's picks; reducing `counts`/`chance` lowers frequency without a cap or priority.
- **`intel_max` evaluated on the map, literal room count.** Rejected: giving the map a run-like scope (area vars + layout counts) so `run.rooms / 2` works — larger change to `MapContext` semantics, still wrong for layouts with dice, `when` rooms or dry `unique` pools. NER's room count is constant per area, so literals (2 | 3 | 3) are exact; lint guards the `run.` trap.
- **Priority = `intel` list order, applied after a full roll.** Rejected: a per-slot `priority` field (YAGNI); trimming during the roll (would change later slots' dice when the cap binds). Consequence for NER: reordering slots to change priority also reorders intel dice (intel only; the run's own dice are separate).
- **Tally prints after digest lines** so both `run` and `run --digest` stay exact prefixes; all-zero WARN only under `--tally`.
- **Worktree: No** (see header).

### Risks
- Tally WARN false positive for a var legitimately 0 on every row: informational only, exit code unchanged.
- NER literal drifts if `floors` changes later: NER README / plan should state "intel_max = floor((floors + 1) / 2)" next to `floors`.
- Game-side: fewer picks than `MaxPicks` is today's normal path (chance misses), so no new UI state.

### Open Questions
- (none blocking) For the NER plan, human may want to confirm priority order. Today's list: `boss`, `rooms`, `word_security`, `word_black`, `word_lower`. Under a cap of 2, a boss rumor + 1 room tip would push out rep-earned `word_*` cards.

### Split Decision
`No split — single scope (EF), within size budget`; tally (tools) and cap (Core + tools via `IntelRoller`) share the proof bar and the same prerequisite slot.
