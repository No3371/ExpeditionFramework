# ef-sim bridge knobs + NER 4000-run parity [PATCHED]

> **Status:** Complete
> **Author:** Claude Opus 5.5 (plan, sprint iteration 4)
> **Source:** Direct request (sprint derive objective, iteration 4)
> **Parent:** 2611121010-pack-defined-sims-sprint.md
> **Sprint:** 2611121010-pack-defined-sims-sprint.md — iteration 4
> **Related Projex:** 2611121010-pack-defined-sims-sprint.md · 2611121110-ef-sim-ner-pack-plan.md · 2611121121-ef-sim-ner-pack-patch.md · 2611121128-ef-sim-ner-pack-audit.md
> Partial Execution: all steps completed via 2611121154-ef-sim-bridge-knobs-patch.md (parity: 46/46 cells identical)
> **Worktree:** Yes — work-root is the sprint worktree `ExpeditionFramework/.projexwt/2611121010-pack-defined-sims` (branch `projex/sprint/2611121010-pack-defined-sims`); every commit lands there, never on `main`.

---

## Summary

Optional top-level `bridge` object in `sim.json` sets how `KitBridge` (ef-sim's stand-in game) answers `GameValue`, trade (`OpenTrade` / `CloseTrade` / `TakeCart`), `TakeBack` order and `LoseCarried`. Left out → today's behaviour, byte-identical. NER `sim.json` gets a `bridge` block equal to NER `FakeBridge` (`NightExpeditionsReborn/tests/Sim/Program.cs:346-362`). Then both 4000-run tables are captured and compared cell by cell (sprint criterion 3, ±3 pts).

**Prototyped by the planner** in a scratch copy of the worktree (HEAD `205e276`): code below is the prototype's, verbatim. Observed: sample output byte-identical; new binary on unchanged NER data byte-identical to iteration 3 (`ba138a66…`); with the `bridge` block, **all 46 cells equal the C# table exactly (worst |Δ| 0.0)** — the run RNG streams now line up draw for draw.

**Scope:** `tools/EfSim/{SimConfig,KitBridge,PackRun}.cs` + README `#### Bridge` (committed) · NER `sim.json` `bridge` block (on disk, unversioned) · 4000-run capture + comparison (evidence in patch doc).
**Estimated Changes:** 4 repo files (~70 lines), 1 NER data file (+7 lines).

---

## Objective

### Problem / Gap / Need
`KitBridge` hard-codes `GameValue` 0, trade never opens, `TakeCart` empty, `TakeBack` newest, `LoseCarried` rolls per item. NER `FakeBridge` returns `luck` 50 / `roll_luck` coin from run RNG, opens trades with `CloseTrade` cash `-rand(0,30)`, `TakeCart` → `cart_goods`, `TakeBack` oldest, `LoseCarried` always `["something"]` with no roll. Iteration 3's audit measured ef-sim 1–10 pts low on cleared at 2000 runs. A pack has no way to state these.

### Success Criteria
- [ ] C1 — `sim.json` accepts optional `bridge` { `game`, `trade` {`opens`,`lose_cash_up_to`,`cart`}, `take_back`, `lose_carried` }; strict parse, bad input → `ERROR sim.json: …` exit 2 (V1, V5)
- [ ] C2 — defaults preserve behaviour: sample-pack `ef-sim run` output byte-identical to baseline `b1565178…`; new binary on pre-edit NER data still `ba138a66…` (V2, V3)
- [ ] C3 — NER `sim.json` = pre-edit content + the exact `bridge` block below; CR-stripped sha256 `7133cc99…` (V4)
- [ ] C4 — `ef-sim run` NER (rows' default 2000) exit 0, 23 rows, packing 6/6, `all checks passed` (V6)
- [ ] C5 — **sprint criterion 3**: NER C# sim `SIM_RUNS=4000` table vs `ef-sim run NER --runs 4000` table — every cleared and knocked-out cell of all 23 rows within ±3 pts (V7–V9)
- [ ] C6 — framework sim passes; lint sample + NER `--strict` 0/0; game assembly 0 warnings 0 errors (V10–V12)
- [ ] C7 — README documents `bridge` (V13)

### Out of Scope
- Gunsmith loot check (iteration 5) · 800- vs 2000-step guard · NER `Wounds` table / `LuckRolls` counter · changing NER `sim.json` `runs` counts (stay 2000; 4000 comes from `--runs 4000`, mirroring C#'s `SIM_RUNS` override of its 2000 default)
- Editing NER `tests/Sim` (parity baseline) · tuning NER data or the ±3 tolerance to make cells pass
- Per-row/per-kit bridge settings (NER uses one global `FakeBridge`; YAGNI)

---

## Context

### Current State
- `tools/EfSim/KitBridge.cs` — `IBridge` impl; ctor `(SimKit kit, Content content, Random random, HashSet<string> problems)`; `random` is the row's shared run RNG (dice, bot coin, bridge).
- `tools/EfSim/SimConfig.cs` — strict `sim.json` v1 reader; top keys `version, kits, bots, runs, expect`; helpers `Need/Only/Obj/List/Str/Int`; `Json.Parse` yields `bool` for `true/false`, `double` for numbers.
- `tools/EfSim/PackRun.cs` — `Play(...)` builds `new KitBridge(kits[n % kits.Count], content, random, problems)` per run.
- Engine callers (`src/Core`, unchanged): `ExpeditionRun.Game` → `GameValue`; `OpenTrade` sets `Trading` only if bridge returns true, PackRun's loop then calls `FinishTrade` → `CloseTrade` (sets `trade_cash`); `TakeCart` → `Found(name)` each; `IInventory.TakeBack` / `LoseCarried(size => …)`.
- `main` has not moved since sprint start and the sprint branch does not touch `src/`, `api/`, framework data → NER's C# sim (built from base checkout) and ef-sim run the same engine.
- NER `sim.json`: unversioned (NER `git init`, no commits — **never `git add`/commit there**), LF endings, CR-stripped sha256 `36c3c1603d7542d020ea2b21d4f2448421ed2a904d016bb9de23cf48e3360d82`, contains exactly one line `  "runs": [`.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `tools/EfSim/SimConfig.cs` | sim.json reader | `SimBridge` class; `bridge` key; `ReadBridge` parser |
| `tools/EfSim/KitBridge.cs` | stand-in game | ctor takes `SimBridge`; 6 members read it |
| `tools/EfSim/PackRun.cs` | runner | pass `config.Bridge` through `Play` to `KitBridge` |
| `README.md` | docs | `#### Bridge` subsection after `#### Packing` |
| `../NightExpeditionsReborn/sim.json` (outside repo) | NER sim data | insert `bridge` block before `"runs"` |

### Dependencies
- **Requires:** iteration 3 (NER `sim.json`, kit lists, packing) — landed.
- **Blocks:** sprint criterion 3; iteration 5 (Gunsmith) independent.

### Constraints
- Default path must draw nothing extra from the run RNG (sample output byte-identical).
- `coin` = `random.Next(2)` on the run RNG per read; `CloseTrade` = `-random.Next(0, lose_cash_up_to + 1)` (29 → `Next(0,30)`, same draw as `FakeBridge`); fixed `lose_carried` neither rolls nor removes items — each exactly as `FakeBridge`, so RNG consumption matches.
- Source Hygiene (SKILL.md): no projex ids/labels/line numbers in code comments.
- Git safety hook blocks `git branch -d` and `git commit --amend`; don't retry/bypass.

### Assumptions (verify early — Step 0)
- Worktree HEAD is `205e276` or a descendant touching only `.projex/` (plan commit). Sample baseline hash `b1565178…`.
- NER `sim.json` hash still `36c3c160…`; NER `tests/Sim/Program.cs` / `Sim.csproj` hashes `d4732e8e…` / `26ca5458…`.
- `python` on PATH in Git Bash (used by the comparison script; planner ran it).

### Impact Analysis
- **Direct:** 3 tool files, README, NER `sim.json`.
- **Adjacent:** framework `tests/Sim` compiles only `tools/EfSim/Lint.cs` (untouched). Game csproj compiles only `src\**` → tool code can't leak.
- **Downstream:** packs with no `bridge` unchanged. NER default-run output changes (new hash `0c40c20e…`) — expected.

---

## Implementation

### Overview
Shell: **Git Bash**, cwd = work-root, unless stated. Code below is the prototype's final code; copy it exactly (files are CRLF — the Edit tool keeps that).

### Step 0: Baseline

**Objective:** confirm assumptions; capture baseline.
**Confidence:** High
**Depends on:** None

```bash
export T=$(mktemp -d); echo "T=$T"
NER=../../../NightExpeditionsReborn
git log --oneline -1 205e276 && git diff --name-only 205e276 HEAD
DOTNET_CLI_UI_LANGUAGE=en dotnet build -c Release --no-incremental tools/EfSim/EfSim.csproj -warnaserror 2>&1 | grep -E '[0-9]+ (Warning|Error)\(s\)'
ef() { dotnet run -c Release --no-build --project tools/EfSim -- "$@" | tr -d '\r'; return ${PIPESTATUS[0]}; }
ef run samples/SampleArea > "$T/before.txt"; echo "exit=$?"; sha256sum "$T/before.txt"
tr -d '\r' < "$NER/sim.json" | sha256sum
sha256sum "$NER/tests/Sim/Program.cs" "$NER/tests/Sim/Sim.csproj"
cp "$NER/sim.json" "$T/ner-sim-before.json"
python --version
```

Expected: `git diff --name-only` empty or only `.projex/…` paths; `0 Warning(s)` `0 Error(s)`; `exit=0`; `b1565178f39d91aeed27111567bba72b20a40bfee5cecead9141144c2951a033`; `36c3c1603d7542d020ea2b21d4f2448421ed2a904d016bb9de23cf48e3360d82`; `d4732e8e8916e11a2f7f0d35fc093fed36126a0c0f0df9aea18c182ad7d8624d` / `26ca5458c92409d81efd40943599049374dd55c428d7dc42287cbe90fdb0cb7a`; a Python 3 version. Keep `$T`, `NER`, `ef` for all later steps (re-define in each new shell; `$T` path is printed).

**If this fails:** any hash differs → stop, report (sprint branch or NER moved). No python → use `py -3` in V9 instead.

---

### Step 1: `SimConfig` — `SimBridge` + `bridge` parsing

**Objective:** C1.
**Confidence:** High
**Depends on:** Step 0
**Do-Projex:** Encouraged

**Files:** `tools/EfSim/SimConfig.cs`

**Change 1a** — after the `SimPacking` line:

```csharp
// Before:
internal sealed class SimPacking { public string At, Area; public List<SimItem> Bag; public List<string> Missing; }  // Bag null = no backpack

// After:
internal sealed class SimPacking { public string At, Area; public List<SimItem> Bag; public List<string> Missing; }  // Bag null = no backpack

// sim.json "bridge": how the stand-in game answers the engine. Each field left out keeps the default noted beside it.
internal sealed class SimBridge
{
    public readonly Dictionary<string, int> Game = new Dictionary<string, int>();   // game.NAME values; unlisted names read 0
    public readonly HashSet<string> CoinGame = new HashSet<string>();             // game.NAME read as 0 or 1 from the run's random
    public bool TradeOpens;                                                        // false: the trade effect never opens a screen
    public int? LoseCashUpTo;                                                      // null: a closed trade changes no cash
    public readonly List<string> Cart = new List<string>();                        // what take_cart hands over; empty by default
    public bool TakeOldest;                                                        // false: take_back hands over the newest found item
    public List<string> LoseCarried;                                               // null: lose_items rolls each carried item
}
```

**Change 1b** — field + allowed top key:

```csharp
// Before:
    public readonly List<SimPacking> Packing = new List<SimPacking>();

    public static SimConfig Read(string file)
    {
        if (!File.Exists(file)) throw new FormatException("no sim.json in the pack folder");
        Dictionary<string, object> top = Obj(Json.Parse(File.ReadAllText(file)), "sim.json");
        Only(top, "sim.json", "version", "kits", "bots", "runs", "expect");

// After:
    public readonly List<SimPacking> Packing = new List<SimPacking>();
    public SimBridge Bridge = new SimBridge();

    public static SimConfig Read(string file)
    {
        if (!File.Exists(file)) throw new FormatException("no sim.json in the pack folder");
        Dictionary<string, object> top = Obj(Json.Parse(File.ReadAllText(file)), "sim.json");
        Only(top, "sim.json", "version", "kits", "bots", "runs", "expect", "bridge");
```

**Change 1c** — the only `return config;` in the file (end of `Read`), plus new `ReadBridge` right after `Read` (method must not be named `Bridge` — clashes with the field, CS0102):

```csharp
// Before:
        return config;
    }

// After:
        if (top.TryGetValue("bridge", out object br)) config.Bridge = ReadBridge(br);
        return config;
    }

    // bridge: { game?, trade?, take_back?, lose_carried? }.
    private static SimBridge ReadBridge(object value)
    {
        Dictionary<string, object> obj = Obj(value, "bridge");
        Only(obj, "bridge", "game", "trade", "take_back", "lose_carried");
        var bridge = new SimBridge();
        if (obj.TryGetValue("game", out object g))
        {
            foreach (KeyValuePair<string, object> v in Obj(g, "bridge.game"))
            {
                string at = "bridge.game." + v.Key;
                if (v.Value is string s)
                {
                    if (s != "coin") throw new FormatException(at + " must be a whole number or coin");
                    bridge.CoinGame.Add(v.Key);
                }
                else bridge.Game[v.Key] = Int(v.Value, at);
            }
        }
        if (obj.TryGetValue("trade", out object t))
        {
            Dictionary<string, object> trade = Obj(t, "bridge.trade");
            Only(trade, "bridge.trade", "opens", "lose_cash_up_to", "cart");
            if (trade.TryGetValue("opens", out object o)) bridge.TradeOpens = o is bool b ? b : throw new FormatException("bridge.trade.opens must be true or false");
            if (trade.TryGetValue("lose_cash_up_to", out object l))
            {
                bridge.LoseCashUpTo = Int(l, "bridge.trade.lose_cash_up_to");
                if (bridge.LoseCashUpTo < 0) throw new FormatException("bridge.trade.lose_cash_up_to must be 0 or above");
            }
            if (trade.TryGetValue("cart", out object c)) bridge.Cart.AddRange(List(c, "bridge.trade.cart").Select(x => Str(x, "bridge.trade.cart")));
        }
        if (obj.TryGetValue("take_back", out object tb))
        {
            string order = Str(tb, "bridge.take_back");
            if (order != "newest" && order != "oldest") throw new FormatException("bridge.take_back must be newest or oldest");
            bridge.TakeOldest = order == "oldest";
        }
        if (obj.TryGetValue("lose_carried", out object lc))
        {
            if (lc is string roll) { if (roll != "roll") throw new FormatException("bridge.lose_carried must be roll or a list of names"); }
            else if (lc is List<object> names) bridge.LoseCarried = names.Select(x => Str(x, "bridge.lose_carried")).ToList();
            else throw new FormatException("bridge.lose_carried must be roll or a list of names");
        }
        return bridge;
    }
```

**Rationale:** one global object mirrors NER's single `FakeBridge`; strict parsing matches the rest of `sim.json`. `coin` as a string value keeps `game` one flat map. `lose_carried` string-or-list follows the row `kit` string-or-list precedent. `trade.opens` separate from `cart` because `take_cart` (robbing a cart) is independent of an opened trade screen, as in `FakeBridge`.

**Verification:** builds only after Steps 2–3 (signature change); V1, V5.

**If this fails:** `git checkout -- tools/EfSim/SimConfig.cs`.

---

### Step 2: `KitBridge` reads `SimBridge`

**Objective:** C1, C2 behaviour.
**Confidence:** High
**Depends on:** Step 1
**Do-Projex:** Encouraged

**Files:** `tools/EfSim/KitBridge.cs`

```csharp
// Before:
// The game's side of a run, faked from a kit: carried items, credits, and a ground pile for what the run gives.
internal sealed class KitBridge : IBridge
{
    private readonly Random random;
    private readonly HashSet<string> problems;
    private readonly List<RunItem> items = new List<RunItem>();
    private readonly List<RunItem> ground = new List<RunItem>();

    public KitBridge(SimKit kit, Content content, Random random, HashSet<string> problems)
    {
        this.random = random;
        this.problems = problems;

// After:
// The game's side of a run, faked from a kit: carried items, credits, and a ground pile for what the run gives.
// Game values, trading, take-back order and carried losses follow the pack's sim.json "bridge" settings.
internal sealed class KitBridge : IBridge
{
    private readonly SimBridge settings;
    private readonly Random random;
    private readonly HashSet<string> problems;
    private readonly List<RunItem> items = new List<RunItem>();
    private readonly List<RunItem> ground = new List<RunItem>();

    public KitBridge(SimKit kit, SimBridge settings, Content content, Random random, HashSet<string> problems)
    {
        this.settings = settings;
        this.random = random;
        this.problems = problems;
```

```csharp
// Before:
    public string TakeBack()
    {
        if (ground.Count == 0) return null;
        RunItem newest = ground[ground.Count - 1];
        ground.RemoveAt(ground.Count - 1);
        return newest.Name;
    }

    public void LoseFound() => ground.Clear();

    public List<string> LoseCarried(Func<int, int> chanceForSize)
    {
        var lost = new List<string>();

// After:
    public string TakeBack()
    {
        if (ground.Count == 0) return null;
        int at = settings.TakeOldest ? 0 : ground.Count - 1;
        RunItem taken = ground[at];
        ground.RemoveAt(at);
        return taken.Name;
    }

    public void LoseFound() => ground.Clear();

    // A fixed list stands in for the loss without rolling or removing anything, so it draws nothing from the run's random.
    public List<string> LoseCarried(Func<int, int> chanceForSize)
    {
        if (settings.LoseCarried != null) return new List<string>(settings.LoseCarried);
        var lost = new List<string>();
```

```csharp
// Before:
    public int GameValue(string name) => 0;

// After:
    public int GameValue(string name) =>
        settings.CoinGame.Contains(name) ? random.Next(2) : settings.Game.TryGetValue(name, out int value) ? value : 0;
```

```csharp
// Before:
    public bool OpenTrade(TradeRequest request) => false;
    public TradeResult CloseTrade() => new TradeResult();
    public List<string> TakeCart(TradeRequest cart) => new List<string>();

// After:
    public bool OpenTrade(TradeRequest request) => settings.TradeOpens;
    public TradeResult CloseTrade() => new TradeResult { CashChange = settings.LoseCashUpTo is int most ? -random.Next(0, most + 1) : 0 };
    public List<string> TakeCart(TradeRequest cart) => new List<string>(settings.Cart);
```

**Rationale:** each default branch is the old expression → no new RNG draws by default. Returned lists are copies so the engine can't mutate settings.

**Verification:** V1–V3 (after Step 3).

**If this fails:** `git checkout -- tools/EfSim/KitBridge.cs`.

---

### Step 3: `PackRun` passes settings

**Objective:** wire `config.Bridge` to every `KitBridge`.
**Confidence:** High
**Depends on:** Step 2
**Do-Projex:** Encouraged

**Files:** `tools/EfSim/PackRun.cs` — three one-line edits:

```csharp
// Before:
                Play(content, areas[row.Area], row, row.Kits.Select(k => config.Kits[k]).ToList(), bot, runsOverride > 0 ? runsOverride : row.Runs, localize, offered, problems);
// After:
                Play(content, areas[row.Area], row, row.Kits.Select(k => config.Kits[k]).ToList(), bot, config.Bridge, runsOverride > 0 ? runsOverride : row.Runs, localize, offered, problems);

// Before:
    private static void Play(Content content, AreaDef area, SimRow row, List<SimKit> kits, RuleBot bot, int runs, Localizer localize,
// After:
    private static void Play(Content content, AreaDef area, SimRow row, List<SimKit> kits, RuleBot bot, SimBridge settings, int runs, Localizer localize,

// Before:
            var bridge = new KitBridge(kits[n % kits.Count], content, random, problems);
// After:
            var bridge = new KitBridge(kits[n % kits.Count], settings, content, random, problems);
```

**Verification:** V1, V2, V3, V5.

**If this fails:** `git checkout -- tools/EfSim/PackRun.cs`.

---

### Step 4: README `#### Bridge`

**Objective:** C7.
**Confidence:** High
**Depends on:** Step 1
**Do-Projex:** Encouraged

**Files:** `README.md` — append at end of file, after the `#### Packing` subsection's last line `an unknown area are errors (exit 2).` (currently line 421, the file's last line). Insert exactly, one blank line before, file ending with a newline:

````markdown
#### Bridge

The stand-in game reads every `game.NAME` as 0, never opens a trade screen, hands nothing over for `take_cart`, gives back
the newest found item for `take_back`, and rolls each carried item for `lose_items`. A top-level `bridge` object changes
that, for packs whose balance depends on it:

```json
"bridge": {
  "game": { "luck": 50, "roll_luck": "coin" },
  "trade": { "opens": true, "lose_cash_up_to": 29, "cart": ["cart_goods"] },
  "take_back": "oldest",
  "lose_carried": ["something"]
}
```

`game` maps a name to a whole number, or to `coin`: 0 or 1 drawn from the run's random each time it is read. Unlisted
names read 0. `trade.opens` (default `false`) lets the `trade` effect open; closing it then changes cash by minus a random
0 to `lose_cash_up_to` (left out: no change, nothing drawn) and buys nothing. `trade.cart` lists the names `take_cart`
hands over (default none). `take_back` is `newest` (default) or `oldest`. `lose_carried` is `roll` (default: each carried
item is lost with the effect's chance) or a list of names reported lost every time, with nothing removed or rolled. Every
field is optional; unknown keys and other values are errors (exit 2). Draws come from the same random as dice and bots,
so a setting that draws shifts every later roll of the run.
````

**Verification:** V13.

**If this fails:** `git checkout -- README.md`.

---

### Step 5: Commit (sprint branch)

**Depends on:** Steps 1–4, V1–V3, V5 green.

```powershell
C:\Users\BA\.claude\skills\projex\stage-n-commit.ps1 "<work-root>" "feat(ef-sim): bridge settings in sim.json" "--trailer Co-Authored-By: Claude Sonnet <noreply@anthropic.com>" README.md tools/EfSim/KitBridge.cs tools/EfSim/PackRun.cs tools/EfSim/SimConfig.cs
```

(Use the dispatched model's exact name in the trailer.) Read output; confirm one commit, 4 files.

---

### Step 6: NER `sim.json` `bridge` block (on disk only)

**Objective:** C3.
**Confidence:** High
**Depends on:** Step 3 (parser must accept it)

**Files:** `NightExpeditionsReborn/sim.json` (outside repo; **do not `git add`/commit in NER**). Insert this block immediately before the single line `  "runs": [` (after the `bots` object's closing `  },`). LF endings, 2-space indent:

```json
  "bridge": {
    "game": { "luck": 50, "roll_luck": "coin" },
    "trade": { "opens": true, "lose_cash_up_to": 29, "cart": ["cart_goods"] },
    "take_back": "oldest",
    "lose_carried": ["something"]
  },
```

Mapping to `FakeBridge` (Program.cs:346-362): `GameValue` luck 50 / roll_luck `random.Next(2)` → `game`; `OpenTrade` true → `opens`; `CloseTrade` `-random.Next(0, 30)` → `lose_cash_up_to: 29`; `TakeCart` `["cart_goods"]` → `cart`; `TakeBack` `ground[0]` → `oldest`; `LoseCarried` `["something"]` → `lose_carried`.

Mechanical way (Git Bash; asserts the anchor is unique, keeps LF):

```bash
python - "$NER/sim.json" <<'EOF'
import sys
p = sys.argv[1]
t = open(p, newline='').read()
block = '''  "bridge": {
    "game": { "luck": 50, "roll_luck": "coin" },
    "trade": { "opens": true, "lose_cash_up_to": 29, "cart": ["cart_goods"] },
    "take_back": "oldest",
    "lose_carried": ["something"]
  },
'''
assert t.count('\n  "runs": [\n') == 1 and '"bridge"' not in t
open(p, 'w', newline='').write(t.replace('\n  "runs": [\n', '\n' + block + '  "runs": [\n'))
EOF
tr -d '\r' < "$NER/sim.json" | sha256sum
```

Expected: `7133cc992841c70d5ce619cd604137ac1bfb1aaa6a04ea74feb009360cc96fcf`.

**Verification:** V4, V6.

**If this fails:** `cp "$T/ner-sim-before.json" "$NER/sim.json"` (restores `36c3c160…`).

---

### Step 7: Capture both 4000-run tables and compare

**Objective:** C5 (sprint criterion 3).
**Confidence:** High (prototype: 46/46 cells identical)
**Depends on:** Step 6

Run each with a ≥10-minute timeout or in the background (planner timings: C# ~1.6 min, ef-sim ~1 min). They may run in parallel.

```bash
# C# baseline (builds from the base checkout; writes only NER tests/Sim/bin,obj)
(cd "$NER" && SIM_RUNS=4000 dotnet run -c Release --project tests/Sim) | tr -d '\r' > "$T/cs4000.txt"; echo "cs exit=${PIPESTATUS[0]}"
# ef-sim, same matrix at 4000 runs per row
ef run "$NER" --runs 4000 > "$T/ef4000.txt"; echo "ef exit=$?"
```

Write the comparison script to `$T/compare.py` exactly:

```python
import re, sys
cs = [re.match(r'^(\S+)\s+(\S+)\s+cleared\s+([\d.]+)%\s+knocked out\s+([\d.]+)%', l) for l in open(sys.argv[1], encoding='utf-8', errors='replace')]
cs = [m for m in cs if m]
ef = [re.match(r'^(\S+)\s+(\S+)\s+(\S+)\s+(\d+)\s+([\d.]+)%\s+([\d.]+)%\s+([\d.]+)%', l) for l in open(sys.argv[2], encoding='utf-8', errors='replace')]
ef = [m for m in ef if m]
print('rows: cs', len(cs), 'ef', len(ef))
if len(cs) != 23 or len(ef) != 23: print('ROW COUNT MISMATCH'); sys.exit(1)
worst, bad = 0.0, []
for i, (c, e) in enumerate(zip(cs, ef)):
    if c[1] != e[1]: print('AREA MISMATCH row', i, c[1], e[1]); sys.exit(1)
    if e[4] != '4000': print('RUNS NOT 4000 row', i, e[4]); sys.exit(1)
    dc = float(e[5]) - float(c[3]); dk = float(e[6]) - float(c[4])
    worst = max(worst, abs(dc), abs(dk))
    flag = '' if abs(dc) <= 3 and abs(dk) <= 3 else '  OVER'
    if flag: bad.append(i)
    print(f'{i:2} {c[1]:<11} {e[2]:<24} {e[3]:<8} cleared {c[3]:>5} vs {e[5]:>5} ({dc:+.1f})  knocked {c[4]:>5} vs {e[6]:>5} ({dk:+.1f}){flag}')
print(f'worst |delta| {worst:.1f}')
print('PARITY OK' if not bad else 'PARITY FAIL rows ' + ','.join(map(str, bad)))
sys.exit(0 if not bad else 1)
```

```bash
python "$T/compare.py" "$T/cs4000.txt" "$T/ef4000.txt" | tee "$T/compare.txt"; echo "compare exit=${PIPESTATUS[0]}"
```

Rows pair by position: both sims emit the same 23 rows in the same order (3 random rows, careful ×10, greedy ×10); the script checks area per row because C#'s kit label (`smg` for both Smg(4,4) and Smg(4,5); `random` for the cycling row) differs from ef-sim's (`smg-5med`, `random0+random1+random2`). C#'s `--- wounds ---` lines don't match its regex (third token isn't `cleared`).

**Evidence for the audit:** paste `$T/compare.txt` verbatim (all 25 lines) plus the two table hashes from V7/V8 into the patch doc.

**If a cell exceeds ±3 (`PARITY FAIL`):** do **not** edit NER data, the knobs' semantics, or the tolerance. Record in the patch doc: the full compare output, the `OVER` row indices with area/kit/bot and both values, whether V7's C# hash matched the pin (if not, the base engine or NER pack moved — say so), and V8's hash. Mark the objective **Partial** (C5 unmet), still commit Steps 1–5 (they stand on their own) and return to the orchestrator. Same if `ROW COUNT MISMATCH` / `AREA MISMATCH` (report the raw lines instead).

**Verification:** V7–V9.

---

### Step 8: Green sweep + cleanup

**Objective:** C6; leave work-root clean.
**Depends on:** Step 5

Run V10–V12 (commands below), then V14–V16. Remove nothing tracked; `bin/`/`obj/` are gitignored (leave them — close cleans). Do not delete `$T` until the patch doc is written.

---

## Verification Plan

### Setup (Git Bash, cwd = work-root; reuse `$T`, `NER`, `ef` from Step 0)

```bash
NER=../../../NightExpeditionsReborn
ef() { dotnet run -c Release --no-build --project tools/EfSim -- "$@" | tr -d '\r'; return ${PIPESTATUS[0]}; }
mkdir -p "$T/N0"; cp -r "$NER/ExpeditionFramework" "$NER/Localization" "$NER/manifest.xml" "$NER/sim.json" "$T/N0/"
bad() { rm -rf "$T/N"; cp -r "$T/N0" "$T/N"; sed -i "$1" "$T/N/sim.json"; ef run "$T/N" --runs 5 | grep -E '^ERROR|^ef-sim'; echo "exit=${PIPESTATUS[0]}"; }
```

(`$T/N0` copy is made **after** Step 6, so it carries the `bridge` block the probes mutate.)

**V5 probes** — run each; expected output line then `exit=2`:

```bash
bad 's/"bridge": {/"bridge": { "x": 1,/'                                   # ERROR sim.json: bridge: unknown key x
bad 's/"roll_luck": "coin"/"roll_luck": "heads"/'                           # ERROR sim.json: bridge.game.roll_luck must be a whole number or coin
bad 's/"luck": 50/"luck": 50.5/'                                            # ERROR sim.json: bridge.game.luck must be a whole number
bad 's/"opens": true/"opens": 1/'                                           # ERROR sim.json: bridge.trade.opens must be true or false
bad 's/"lose_cash_up_to": 29/"lose_cash_up_to": -1/'                        # ERROR sim.json: bridge.trade.lose_cash_up_to must be 0 or above
bad 's/"take_back": "oldest"/"take_back": "first"/'                         # ERROR sim.json: bridge.take_back must be newest or oldest
bad 's/"lose_carried": \["something"\]/"lose_carried": "all"/'              # ERROR sim.json: bridge.lose_carried must be roll or a list of names
bad 's/"lose_carried": \["something"\]/"lose_carried": 3/'                  # ERROR sim.json: bridge.lose_carried must be roll or a list of names
bad 's/"cart": \["cart_goods"\]/"cart": "cart_goods"/'                      # ERROR sim.json: bridge.trade.cart must be a list
bad 's/"trade": {/"trade": { "y": 2,/'                                      # ERROR sim.json: bridge.trade: unknown key y
```

### Deterministic Suite

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V1 | C1 / S1–3 | `DOTNET_CLI_UI_LANGUAGE=en dotnet build -c Release --no-incremental tools/EfSim/EfSim.csproj -warnaserror 2>&1 \| grep -E '[0-9]+ (Warning\|Error)\(s\)'` | `0 Warning(s)`, `0 Error(s)` | compile errors (e.g. CS0102 name clash), warnings |
| V2 | C2 / S2–3 | `ef run samples/SampleArea > "$T/after.txt"; echo "exit=$?"; cmp "$T/before.txt" "$T/after.txt" && echo SAME` | `exit=0`; `SAME` | default path drawing RNG or changing behaviour |
| V3 | C2 | `mkdir -p "$T/P"; cp -r "$NER/ExpeditionFramework" "$NER/Localization" "$NER/manifest.xml" "$T/P/"; cp "$T/ner-sim-before.json" "$T/P/sim.json"; ef run "$T/P" \| sha256sum` | `ba138a669a45404b88ac9680d636feb1033b88f50aac93d45c1ecbbbdfdae996` (iteration 3's NER output) | defaults changing NER output |
| V4 | C3 / S6 | `tr -d '\r' < "$NER/sim.json" \| sha256sum` | `7133cc992841c70d5ce619cd604137ac1bfb1aaa6a04ea74feb009360cc96fcf` | wrong/extra NER edit |
| V5 | C1 | V5 probes above | the 10 listed lines, each `exit=2` | lax parsing, unlocated errors |
| V6 | C4 | `ef run "$NER" > "$T/ner.txt"; echo "exit=$?"; grep -cE '^ne_(sump\|annex\|foundry) ' "$T/ner.txt"; grep -c '^packing: 6 case(s), 0 failed$' "$T/ner.txt"; grep -cE '^(FAIL\|ERROR)' "$T/ner.txt"; tail -1 "$T/ner.txt"; sha256sum "$T/ner.txt"` | `exit=0`; `23`; `1`; `0`; `ef-sim night_expeditions_reborn: all checks passed`; `0c40c20ec5aa1c15c6d885ecb03b51e02cda19e5b79faf8cf40d1272da940bae` | knob breaking a check; nondeterminism |
| V7 | C5 / S7 | `grep -E '^ne_[a-z]+ +[a-z0-9-]+ +cleared ' "$T/cs4000.txt" \| sha256sum; grep -cE '^ne_[a-z]+ +[a-z0-9-]+ +cleared ' "$T/cs4000.txt"; tail -1 "$T/cs4000.txt"` | `8b401fcbfa0447d3ec52b570197b85df48d3b5d42a72154305329013c4b7ffdf`; `23`; `All checks passed`; and Step 7 printed `cs exit=0` | baseline moved / C# sim failing |
| V8 | C5 / S7 | `grep -E '^ne_' "$T/ef4000.txt" \| sha256sum; tail -1 "$T/ef4000.txt"` | `a8258ba4047222a79d80d4a0e93b1f2f111313156ab65843a2b765cad90cfecd`; `ef-sim night_expeditions_reborn: all checks passed`; and Step 7 printed `ef exit=0` | ef-sim table differs from prototype |
| V9 | C5 (criterion 3) | `python "$T/compare.py" "$T/cs4000.txt" "$T/ef4000.txt" \| tail -2; echo "exit=${PIPESTATUS[0]}"` | `worst \|delta\| 0.0` (any value ≤ 3.0 still passes C5 — report it); `PARITY OK`; `exit=0` | any cell outside ±3 |
| V10 | C6 | `dotnet run -c Release --project tests/Sim \| tr -d '\r' \| tail -1; echo "exit=${PIPESTATUS[0]}"` | `All checks passed`; `exit=0` | framework sim regression |
| V11 | C6 | `ef lint samples/SampleArea --strict \| tail -1; ef lint "$NER" --strict \| tail -1` | `lint sample_area: 0 error(s), 0 warning(s)`; `lint night_expeditions_reborn: 0 error(s), 0 warning(s)` | lint regression |
| V12 | C6 | `S=$(cygpath -m "$(mktemp -d)"); DOTNET_CLI_UI_LANGUAGE=en dotnet build -c Release --no-incremental ExpeditionFramework.csproj "-p:GameManaged=E:/SteamLibrary/steamapps/common/Probably Stolen/Probably Stolen_Data/Managed/" "-p:InstallDir=$S/" 2>&1 \| grep -E '[0-9]+ (Warning\|Error)\(s\)'` | `0 Warning(s)`, `0 Error(s)` (never deploys to live `Mods/`) | tool code leaking into game assembly |
| V13 | C7 | `for k in '#### Bridge' '"lose_cash_up_to": 29' 'take_back' 'lose_carried' '`coin`' 'nothing removed or rolled'; do printf '%s ' "$(grep -cF -- "$k" README.md)"; done; echo` | six numbers each ≥ `1` | docs missing |
| V14 | hygiene | `git status --porcelain` | empty (after Step 5 and the patch-doc commit) | stray files |
| V15 | scope | `P=$(git log --diff-filter=A --format=%H -- .projex/2611121143-ef-sim-bridge-knobs-plan.md); git diff --name-only "$P"..HEAD \| sort` | exactly `README.md`, `tools/EfSim/KitBridge.cs`, `tools/EfSim/PackRun.cs`, `tools/EfSim/SimConfig.cs` (plus the patch doc under `.projex/` if committed) | engine, sample, test files touched |
| V16 | baseline intact | `sha256sum "$NER/tests/Sim/Program.cs" "$NER/tests/Sim/Sim.csproj"` | Step 0 hashes (`d4732e8e…`, `26ca5458…`) | NER C# sim edited |

### Judgment Checks

| Check | Covers | Why not deterministic |
|-------|--------|-----------------------|
| `bridge` block in NER mirrors `FakeBridge` member by member (mapping table in Step 6) | C3 | semantic equivalence of data vs C#; V4 only pins bytes, V9 confirms the effect |
| Code comments obey Source Hygiene (no projex ids, line numbers, plan shape) | all | reading comments |

### Acceptance Criteria Validation

| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| C1 | V1, V5 | build clean; 10 probe errors, exit 2 |
| C2 | V2, V3 | `SAME`; `ba138a66…` |
| C3 | V4, judgment | `7133cc99…`; mapping faithful |
| C4 | V6 | exit 0, 23 rows, packing 6/6, `0c40c20e…` |
| C5 | V7, V8, V9 | pinned table hashes; `PARITY OK` |
| C6 | V10, V11, V12 | all green |
| C7 | V13 | six counts ≥ 1 |

---

## Rollback Plan

1. Repo: revert the Step 5 commit with a new commit (`git -C <work-root> revert <sha>`); never amend.
2. NER: `cp "$T/ner-sim-before.json" "$NER/sim.json"` → CR-stripped hash `36c3c160…`.

---

## Notes

### Risks
- C# sim reads the **base** checkout; if `main` or NER pack content changes before execution, V7's hash moves. Mitigation: V7 reports it; C5 is judged on V9 regardless.
- Long runs: both ≤ ~2 min here; use background or 10-min timeouts, never short ones.
- `bad` probes use `sed -i` on a copy under `$T`, never on NER's own file.

### Decisions taken by default (open to orchestrator override)
- Bridge settings are one global `bridge` object (not per row/kit).
- NER rows keep `runs: 2000`; the 4000-run table uses `--runs 4000` (mirrors C# `SIM_RUNS` override of default 2000).
- `lose_cash_up_to` absent → no draw; present (even 0) → draws `Next(0, n+1)`.
- Fixed `lose_carried` neither removes items nor rolls (as `FakeBridge`).

### Split decision
No split — single scope (EfSim tool + its README + the pack data it reads), ~350-line plan, steps tightly coupled.

### Open Questions
- [ ] none
