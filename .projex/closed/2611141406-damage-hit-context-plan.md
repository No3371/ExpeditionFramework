# Damage Hit Context (`damage.hit` → `hit.NAME`)

> **Status:** Complete
> **Author:** Claude Opus 5.5 (plan step)
> **Parent:** 2611140500-authoring-language-text-params-hit-context-proposal.md
> **Source:** 2611140500-authoring-language-text-params-hit-context-proposal.md (Accepted 2026-11-14; § C2 design sketch, § Balance-Proof Bar, Next Steps 3)
> **Related Projex:** 2611140627-text-params-and-sim-digest-walkthrough.md (EF; digest + C1, proof method, `digest2` baseline) | 2611141353-ner-rep-book-settle-walkthrough.md (NER; last change proven identical) | 2611140152-ef-ner-next-steps-proposal.md (NER; § C origin) | 2611120227-runtime-hooks-expr-expansion-explore.md
> **Log:** 2611141406-damage-hit-context-log.md
> **Completed:** 2026-11-14
> **Walkthrough:** 2611141641-damage-hit-context-walkthrough.md
> **Worktree:** No — repo clean at plan time (`main` @ `9cd3750`), no `projex/*` branches, ~8 files. NER path and proof baselines are relative to the main checkout; worktree mode would break them.

---

## Summary

`damage` gains an optional `hit` map (name → expression). Its values are evaluated once when the damage starts and pushed as a frame on a per-run stack; `hit.NAME` (new `Expr` head) reads the innermost frame, 0 outside any damage. Every damage pushes a frame (empty when no `hit`), so a damage dealt inside a hook gets its own context and the outer one comes back after. Loader validates the map; lint warns on a `hit.NAME` read that no `damage` sets. No pack data changes, so Starter/Showcase/NER must give identical lint, byte-identical `run`, identical `--digest`.

**Scope:** EF only — `src/Core` (ExpeditionRun, Expr, Builtins, PackLoader), `tests/Sim`, `docs/` (effects, expressions, run-flow, csharp-api). NER `run.soft` removal is a follow-up (§ Notes), not planned here.
**Estimated Changes:** 4 source files (~60 lines), 1 test file (~70 lines), 4 docs (~15 lines).

---

## Objective

### Problem / Gap / Need
NER passes per-hit softness through a global `run.soft` set before and cleared after `damage` (NER `combat.json` `land_hit`). Any damage dealt inside a `defeated` hook during a soft hit would inherit `soft = 1`; the next author has only a comment as warning. EF has no per-damage context: `Emit` builds a fresh `Scope(run, subject, other, null)` with no args (`src/Core/ExpeditionRun.cs` `Emit`), so hooks see only victim + source.

### Success Criteria
- [ ] **SC1** `damage` accepts optional `"hit": { NAME: expr, ... }`. Names `a-z0-9_`; values are expressions; evaluated once per damage, after `amount`, in written order. Non-object `hit`, bad name, bad expression → load problem at `<path>.hit[.NAME]`.
- [ ] **SC2** `hit.NAME` reads the innermost damage in progress (from its decision through its last hook, incl. `defeated`, `knocked_out` and the `run_end` a knockout fires); 0 outside any damage and for names the frame lacks. Every damage pushes a frame — with `hit`, without `hit`, and through C# `IRun.Damage` — and pops it on exit, also on exception.
- [ ] **SC3** `hit.NAME` is read-only (`set`/`add` on it = existing "must be a path like" problem); `hit.$x` and `hit.a.b` are unknown paths; a `hit.NAME` read with no `damage` in any loaded pack setting NAME → one warning per (pack, name).
- [ ] **SC4** `tests/Sim` green, including new `TestHitContext()` covering SC1–SC3.
- [ ] **SC5** Docs: `effects.md` damage row, `expressions.md` paths, `run-flow.md` § Damage and defeat, `csharp-api.md` one line.
- [ ] **SC6** Step 0 re-confirms baselines at current HEADs (EF source = `9cd3750`, NER `0e0d46f`): lint/run equal `c-proof/base/`, digest lines equal `c-proof/digest2/`. Mismatch → plan stops.
- [ ] **SC7** After the change, Starter/Showcase/NER: `lint --strict` identical, plain `run` byte-identical to `c-proof/base/`, `^digest ` lines identical to `c-proof/digest2/*-digest-base.txt`.
- [ ] **SC8** No public API change: `api/`, `src/Game/`, `*.csproj` untouched; no removed/changed `public`/`protected` line in `src/Core`. Only `tools/EfSim` and `tests/Sim` built (plus V5g only if the human approves it).

### Out of Scope
- NER `run.soft` → `hit.soft` (follow-up, § Notes).
- List sugar `"hit": ["soft"]` (proposal "optional"; not built — see Notes § Decisions).
- Copying `hit` into the `damage` `Decision` or any new C# API member.
- Text-valued hit names (`hit.$x`), key templates `{hit.x}`.
- C3 `event.*` vars.

---

## Context

### Current State (verified at `9cd3750`)
- `ExpeditionRun.Damage(IEntity target, int amount, IEntity source, bool pierce)` (`src/Core/ExpeditionRun.cs:381-399`): returns 0 if victim inactive or run over; builds `Decision("damage", new Scope(this, source, victim, null), amount)`, `Decide`; on cancel sets `run.last_damage` 0; else lowers hp, sets `last_damage`, `Emit("damaged", victim, source)`, then `Defeat` at hp ≤ 0. `Defeat` (`:456-467`) emits `knocked_out` + `Finish("knocked_out")` (→ `run_end`, `:370-379`) for the player, else `defeated`.
- `Damage` is a public member of `IRun` (`api/ExpeditionApi.cs:291`); `MapContext` implements it as `=> 0` (`ExpeditionRun.cs:1203`). Callers: `damage` builtin (`src/Core/Builtins.cs:609-615`) and C# mods / `tests/Sim` (`Program.cs:1183,1223`).
- `damage` builtin: `to`, `amount`, `armor`; calls `s.Run.Damage(t, amount(s), s.Self, pierce)`.
- `Expr.Reader` (`src/Core/Expr.cs:134-157`): heads `self target player item with run memory arg game val role`. Unknown head → `null` → `Atom` fails with `unknown path '…' (paths start with …)` (`:391`). `Atom` calls the internal `memoryRead` callback for `memory.` words (`:394`); `PackLoader.ValueOf` (`src/Core/PackLoader.cs:1636-1651`) passes `path => NoteMemory(pack, path, at, write: false)`. Conditions given as strings also go through `ValueOf` (`Condition`, same file).
- `Assign` (`Builtins.cs:351-372`) rejects a `var` without both `Expr.Reader` and `Expr.Writer` → `hit.*` has no writer → already a problem once `hit` is a readable head.
- `PackLoader.Finish` (`:674-716`): memory read-never-written warnings (`memoryReads`/`memoryWrites`), then uncalled text-param warnings. Text-param name/value regexes `TextValue = ^[a-z0-9_]+$` (`:154`).
- Internal `PackLoader.Spec` (`:1765`) exposes `Args` (ordered `Dictionary<string, object>`), `Loader`; `ValueOf` is internal. Builtins already cast `(PackLoader.Spec)c` (`Builtins.cs:76,107,838`).
- `src/Game` constructs `ExpeditionRun` via its constructor (`src/Game/ExpeditionLocation.cs:124`) and uses no `Damage`/`Decision`/`Emit`/`Scope`. Nothing on the game side builds damage decisions or hook scopes → nothing there must carry `hit`.
- Game assembly is `netstandard2.1` (`ExpeditionFramework.csproj`); ef-sim and tests compile the same `src/Core` + `api` under `net8.0`.
- No current use of a `hit` head anywhere (EF src/docs/tests, Starter, Showcase, NER).

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `src/Core/ExpeditionRun.cs` | Run, damage, hooks | Hit frame stack; internal `Damage(..., hit)` overload; public `Damage` delegates; `Hit(name)` |
| `src/Core/Expr.cs` | Expression paths | `hit` head; unknown-path message lists `hit.`; read callback also gets `hit.` words |
| `src/Core/Builtins.cs` | `damage` effect | Read/validate `hit` map; evaluate after `amount`; call overload |
| `src/Core/PackLoader.cs` | Loader/lint | `NoteHitSet`/`NoteRead`; read-never-set warning in `Finish` |
| `tests/Sim/Program.cs` | Engine tests | `TestHitContext()` |
| `docs/effects.md`, `docs/expressions.md`, `docs/run-flow.md`, `docs/csharp-api.md` | Docs | `hit` field, `hit.NAME` path, semantics, C# note |

### Dependencies
- **Requires:** C1 + digest merged (`2bf64dc`, done); proof baselines `c-proof/base/`, `c-proof/digest2/` in the session scratchpad.
- **Blocks:** NER `run.soft` follow-up (§ Notes).

### Constraints
- Never build `ExpeditionFramework.csproj` or NER's csproj without the human's approval (installs into the live game). `dotnet` only on `tools/EfSim` and `tests/Sim`; V5g is the single exception and only after the human says yes.
- `IRun`, `IScope`, `Decision`, `ISpec` and every public member stay unchanged (mods compile against them).
- New `src/Core` code uses only APIs present in `netstandard2.1` (`List<T>`, `Dictionary<,>`, LINQ already in use). No `Stack<T>.TryPeek`-style additions needed.
- No new `dice`/`Roll` call on any existing path: a `damage` without `hit` evaluates exactly what it does today, in the same order.
- Source Hygiene (SKILL.md): no projex refs in comments; non-obvious choices carry rationale naming the rejected alternative.
- Do not touch NER.

### Assumptions
- A1: `Dictionary<string, object>` from the JSON reader keeps written key order (relied on by shared-effect `params` today). Only matters if a `hit` value rolls dice.
- A2: Baselines still reproduce at current HEADs (NER changed since `base/`, but the rep_book walkthrough proved its output identical). Step 0 checks; mismatch = stop.
- A3: `ExpeditionRun.Get("NAME")` returns a plain run var after `Finish` (tests read run vars after `Begin` today).

### Impact Analysis
- **Direct:** the four `src/Core` files above.
- **Adjacent:** every `damage` (now pushes/pops a frame — no observable effect without `hit` reads); `Expr` unknown-path message text (no current pack hits it: all three lint 0/0); `PackLoader.Finish` warning list (new warning only fires on `hit.` reads).
- **Downstream:** C# mods: unchanged API; a mod effect compiled from data (`spec.Value`) can read `hit.NAME`. `src/Game`: no source change; compiles against unchanged public surface (V5e/V5f; V5g if approved). NER: unaffected until the follow-up.

---

## Implementation

### Overview
**0** reconfirm baselines (stop on mismatch) → **1** engine: hit stack + `hit` head → **2** `damage.hit` + lint → **3** tests → **4** docs → **5** full proof bar + game-assembly checks. Commit after each step (execute-projex convention).

Command conventions (Git Bash, EF root as cwd):
```bash
S=<session scratchpad>        # holds c-proof/ from the C1 and rep_book proofs; orchestrator supplies the path
P=$S/c-proof                  # baselines: $P/base/{ST,SC,NER}-{lint,run}.txt, $P/digest2/{ST,SC,NER}-digest-base.txt
Q=$S/c2-proof                 # this plan's outputs
NER=../NightExpeditionsReborn; SC=docs/examples/Showcase; ST=docs/examples/Starter
SIM="dotnet run -c Release --no-build --project tools/EfSim --"
```
Rebuild (`dotnet build -c Release tools/EfSim`) after every source change before a `--no-build` run. Digest compare uses `grep -aU` (Git Bash `grep` strips CR otherwise). PowerShell alternative for `cmp a b`: `fc.exe /b a b`.

---

### Step 0: Preflight + Baseline Reconfirm

**Objective:** SC6. Prove the accepted baselines still hold at current HEADs before any change.
**Confidence:** High
**Depends on:** None

**Files:** none (writes `$Q/s0/*`)

**Changes:**
```bash
git rev-parse --short HEAD                                   # 9cd3750, or later with only .projex/ changes:
git diff --stat 9cd3750 HEAD -- src api tools tests docs ExpeditionFramework Localization samples '*.csproj'   # expect empty
git status --porcelain -- src api tools tests docs           # expect empty
git -C $NER rev-parse --short HEAD                           # 0e0d46f
git -C $NER status --porcelain -- ExpeditionFramework Localization sim.json   # expect empty
dotnet build -c Release tools/EfSim
mkdir -p $Q/s0
for n in ST SC NER; do d=$(eval echo \$$n)
  $SIM lint "$d" --strict > $Q/s0/$n-lint.txt
  $SIM run  "$d"          > $Q/s0/$n-run.txt
  $SIM run  "$d" --digest > $Q/s0/$n-d.txt
  cmp $Q/s0/$n-lint.txt $P/base/$n-lint.txt && echo "$n lint ok"
  cmp $Q/s0/$n-run.txt  $P/base/$n-run.txt  && echo "$n run ok"
  grep -aU '^digest ' $Q/s0/$n-d.txt | cmp - $P/digest2/$n-digest-base.txt && echo "$n digest ok"
done
dotnet run -c Release --project tests/Sim                    # last line "All checks passed"
```

**Rationale:** NER moved (`1277ab0` → `0e0d46f`) since `base/` was taken; the rep_book proof showed identical output, so all nine compares must pass. A failing compare here would make Step 5 meaningless.

**Verification:** V0a, V0b, V0c, V0d.

**If this fails:** EF source drift or NER not at `0e0d46f`/dirty → stop, report. Any `cmp` differs → **stop the plan**, report which pack/file and the first differing line; do not re-baseline. tests/Sim red → stop, report the failing checks.

---

### Step 1: Hit Frame Stack + `hit.NAME` Head

**Objective:** SC2 engine side, SC3 path side.
**Confidence:** High
**Depends on:** Step 0
**Verify-Projex:** Encouraged

**Files:**
- `src/Core/ExpeditionRun.cs`
- `src/Core/Expr.cs`

**Changes:**

`ExpeditionRun.cs` — field near the other run state, `Hit`, and the split `Damage`. Keep the public signature line byte-identical (V5f greps for removed `public` lines):
```csharp
// Before:
        public int Damage(IEntity target, int amount, IEntity source, bool pierce)
        {
            var victim = (Entity)target;
            if (!victim.Active || Over) return 0;
            var decision = new Decision("damage", new Scope(this, source, victim, null), amount);
            ...
            if (victim.Get("hp") <= 0) Defeat(victim, source as Entity);
            return dealt;
        }

// After:
        // One frame per damage in progress, innermost last; null for a damage without a hit map. A damage dealt inside a
        // hook pushes its own frame, so it never sees the outer damage's names. A single run var (the old run.soft
        // pattern) would leak into that nested damage and be cleared under the outer one.
        private readonly List<Dictionary<string, int>> hits = new List<Dictionary<string, int>>();

        /// <summary>hit.NAME: the innermost damage in progress's value for name, or 0.</summary>
        internal int Hit(string name)
        {
            Dictionary<string, int> top = hits.Count > 0 ? hits[hits.Count - 1] : null;
            return top != null && top.TryGetValue(name, out int v) ? v : 0;
        }

        public int Damage(IEntity target, int amount, IEntity source, bool pierce)
        {
            return Damage(target, amount, source, pierce, null);
        }

        /// <summary>As the public Damage, with the hit map the damage's hooks read as hit.NAME. Not on IRun: adding a member there would break every IRun outside this assembly.</summary>
        internal int Damage(IEntity target, int amount, IEntity source, bool pierce, Dictionary<string, int> hit)
        {
            var victim = (Entity)target;
            if (!victim.Active || Over) return 0;
            hits.Add(hit);
            try
            {
                var decision = new Decision("damage", new Scope(this, source, victim, null), amount);
                ... (existing body unchanged, through `return dealt;`)
            }
            finally
            {
                // Without this, an exception escaping a hook would leave its frame on top and every later hook would read it.
                hits.RemoveAt(hits.Count - 1);
            }
        }
```

`Expr.cs` — `Reader` switch, before `default`:
```csharp
// Before:
                case "game": return s => s.Run.Game(name);
// After:
                case "game": return s => s.Run.Game(name);
                case "hit":
                    // hit values are numbers set by one damage; a "$" or dotted name could never be set, so it is no path at all.
                    if (!IsPlainName(name)) return null;
                    return s => (s.Run as ExpeditionRun)?.Hit(name) ?? 0;
```
with an `internal` helper (Step 2's `damage` validates names with the same rule; `Expr.cs` already has `using System.Linq;`):
```csharp
        /// <summary>A hit name: a-z, 0-9 or _, not empty.</summary>
        internal static bool IsPlainName(string name) => name.Length > 0 && name.All(ch => (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '_');
```

`Atom` unknown-path message (`:391`): add `hit.` to the list:
```csharp
// Before: "... (paths start with self., target., player., item., with., run., memory., role., arg., game. or val.)"
// After:  "... (paths start with self., target., player., item., with., run., memory., role., arg., game., hit. or val.)"
```
`Atom` callback (`:394`) and its overload doc comment (`:42-45`): also hand `hit.` words to the callback; rename the internal field/parameter `memoryRead` → `pathRead` and say in the comment that it receives `memory.` and `hit.` paths.
```csharp
// Before:
            if (memoryRead != null && word.StartsWith("memory.", StringComparison.Ordinal)) memoryRead(word);
// After:
            if (pathRead != null && (word.StartsWith("memory.", StringComparison.Ordinal) || word.StartsWith("hit.", StringComparison.Ordinal))) pathRead(word);
```

**Rationale:** Stack on the run, not `Scope`: `Emit` builds fresh scopes, and hooks fired from inside damage (statuses, kits, `Finish` → `run_end`) don't inherit the caller's scope. `(s.Run as ExpeditionRun)` instead of a new internal host interface: only `ExpeditionRun` runs damage (`MapContext.Damage` is a no-op), so `MapContext` reading 0 is correct. Frame pushed after the inactive/over early return (no hooks fire there) and before `Decide` (a C# interceptor that deals damage nests correctly). Public `Damage` pushes an empty frame → C# damage inside a soft hit also reads 0.

**Verification:** V1a; V1b (Starter/Showcase lint + run unchanged).

**If this fails:** compile error → fix in place. V1b differs → the stack or the message change altered behavior; `git checkout -- src/Core/ExpeditionRun.cs src/Core/Expr.cs` and redo.

---

### Step 2: `damage.hit` Field + Lint

**Objective:** SC1, SC3 lint.
**Confidence:** High
**Depends on:** Step 1
**Verify-Projex:** Encouraged

**Files:**
- `src/Core/Builtins.cs`
- `src/Core/PackLoader.cs`

**Changes:**

`Builtins.cs` `damage`:
```csharp
// Before:
            r.Effects["damage"] = c =>
            {
                Func<IScope, IEntity> to = Who(c, "to", "target");
                Value amount = c.Value("amount", 0);
                bool pierce = c.Text("armor", "") == "ignore";
                return s => { IEntity t = to(s); if (t != null) s.Run.Damage(t, amount(s), s.Self, pierce); };
            };

// After:
            r.Effects["damage"] = c =>
            {
                Func<IScope, IEntity> to = Who(c, "to", "target");
                Value amount = c.Value("amount", 0);
                bool pierce = c.Text("armor", "") == "ignore";
                List<KeyValuePair<string, Value>> hit = HitMap(c);
                return s =>
                {
                    IEntity t = to(s);
                    if (t == null) return;
                    int n = amount(s);
                    if (hit == null) { s.Run.Damage(t, n, s.Self, pierce); return; }
                    // After amount and in written order, so a hit value that rolls dice always rolls in the same place.
                    var values = new Dictionary<string, int>();
                    foreach (KeyValuePair<string, Value> h in hit) values[h.Key] = h.Value(s);
                    if (s.Run is ExpeditionRun run) run.Damage(t, n, s.Self, pierce, values);
                    else s.Run.Damage(t, n, s.Self, pierce);
                };
            };
```
Helper in `Builtins`:
```csharp
        /// <summary>damage's "hit": name → expression, or null when absent. Names are a-z, 0-9 or _ so hit.NAME can read them.</summary>
        private static List<KeyValuePair<string, Value>> HitMap(ISpec c)
        {
            if (!c.Has("hit")) return null;
            var spec = (PackLoader.Spec)c;
            if (!(spec.Args["hit"] is Dictionary<string, object> map))
            {
                c.Error("\"hit\" must be an object of names and expressions, like { \"soft\": \"arg.soft\" }");
                return null;
            }
            var result = new List<KeyValuePair<string, Value>>();
            foreach (KeyValuePair<string, object> e in map)
            {
                string at = c.Path + ".hit." + e.Key;
                if (!Expr.IsPlainName(e.Key)) { spec.Loader.Problem(at, "a hit name is a-z, 0-9 or _"); continue; }
                spec.Loader.NoteHitSet(e.Key);
                result.Add(new KeyValuePair<string, Value>(e.Key, spec.Loader.ValueOf(e.Value, c.PackId, at)));
            }
            return result;
        }
```
One name rule, defined once in `Expr.IsPlainName` (Step 1). Returning `null` on a bad `hit` keeps the damage dealing damage without context (load already failed with a problem, lint shows it).

`PackLoader.cs`:
```csharp
// New fields beside memoryReads/memoryWrites:
        private readonly HashSet<string> hitSets = new HashSet<string>();
        private readonly Dictionary<(string Reader, string Name), (string At, string Path)> hitReads = new Dictionary<(string, string), (string, string)>();

        internal void NoteHitSet(string name) => hitSets.Add(name);

        /// <summary>Notes a path an expression reads: memory paths for the read-never-written check, hit paths for the read-never-set one.</summary>
        private void NoteRead(string pack, string path, string at)
        {
            if (path.StartsWith("hit.", StringComparison.Ordinal))
            {
                string name = path.Substring(4);
                if (!hitReads.ContainsKey((pack, name))) hitReads[(pack, name)] = (at, path);
                return;
            }
            NoteMemory(pack, path, at, write: false);
        }

// ValueOf, before:
                Value value = Expr.Compile(text, out string error, name => NamedValue(pack, name, at), HasMod, pack, path => NoteMemory(pack, path, at, write: false));
// after:
                Value value = Expr.Compile(text, out string error, name => NamedValue(pack, name, at), HasMod, pack, path => NoteRead(pack, path, at));

// Finish, after memoryReads/memoryWrites are cleared, before the uncalledText loop:
            foreach (KeyValuePair<(string Reader, string Name), (string At, string Path)> read in hitReads)
            {
                if (hitSets.Contains(read.Key.Name)) continue;
                current = read.Key.Reader;
                // A warning, as with memory: the pack still runs; the name just always reads 0. Names are shared by every
                // loaded pack, so a hit set by another pack that is not loaded here warns too.
                Warn(read.Value.At, read.Value.Path + " is read but no damage in a loaded pack sets it in its \"hit\", so it always reads 0");
            }
            hitReads.Clear();
            hitSets.Clear();
```
`Problem` is `internal` already; `Loader` on `Spec` is `internal`.

**Rationale:** Map, not list: NER softness is computed (`item.round_soft`, `item.#non_lethal || item.stuns`). `hit` names are global, not pack-qualified: the reader of a hit is whoever hooks the damage (often another pack's kit), so qualification would force every reader to know the dealer's pack. Read-never-set warning mirrors memory's read-never-written warning; it catches `hit.sfot`.

**Verification:** V1a; V1b; V3a (tests in Step 3 cover behavior).

**If this fails:** V1b differs → `NoteRead` changed memory warnings (check it still calls `NoteMemory` for every non-`hit.` path) or the `damage` closure changed evaluation order for hit-less damage; revert the two files and redo.

---

### Step 3: Tests

**Objective:** SC4 (behavior proof for SC1–SC3).
**Confidence:** Medium (pack JSON may need syntax adjustment; assertions are fixed)
**Depends on:** Step 2
**Verify-Projex:** Encouraged

**Files:**
- `tests/Sim/Program.cs`

**Changes:** add `TestHitContext();` to `Main` after `TestTextParams();`, and the method below. Reuse the file's `Compiled`, `ProblemsIn`, `WarningsIn`, `FakeBridge`, `Check` helpers and the run construction used in `TestTextParams`. Run var names are prefixed `h_` to stay clear of `ExpeditionRun.EngineVars`.

Cases and exact assertions:
1. **Nesting + restore + outside** (player side, data only). Area `player.hp` 10. Room `enter`: `damage {to: player, amount: 1, armor: ignore, hit: {soft: 1, loud: "1 + 1"}}`, then `set run.h_after = "hit.soft + 100"`. Area `on.damaged`: `add run.h_calls 1`; `if {when: "run.h_calls == 1", then: [set run.h_outer_before = "hit.soft * 10 + hit.loud", damage {to: player, amount: 1, armor: ignore}, set run.h_outer_after = "hit.soft * 10 + hit.loud"], else: [set run.h_inner = "hit.soft * 10 + hit.loud + 1"]}`.
   Expect: `h_calls == 2`, `h_outer_before == 12`, `h_inner == 1` (nested damage without `hit` reads 0), `h_outer_after == 12` (outer restored), `h_after == 100` (0 outside), player hp 8.
2. **Knockout + run_end.** Area `player.hp` 1; `enter`: `damage {to: player, amount: 5, armor: ignore, hit: {soft: 1}}`; `on.knocked_out`: `set run.h_ko = "hit.soft"`; `on.run_end`: `set run.h_end = "hit.soft + 10"`. Expect `run.Outcome == "knocked_out"`, `h_ko == 1`, `h_end == 11`.
3. **Enemy defeated.** Enemy `rat` (`hp` 5, `behavior` idle intent) with `on.turn_start`: `damage {to: self, amount: 9, armor: ignore, hit: {soft: 1}}`; area `on.defeated`: `set run.h_def = "hit.soft"`. In C#: `Make("t:rat")`, `Muster`, `Engage(null, run.RootScope, false, null)`, `Combat.EnemyTurn()` (pattern from `TestPublicApi`). Expect `h_def == 1`.
4. **C# damage pushes an empty frame.** Separate pack: area `player.hp` 10, room with no `enter`, area `on.damaged: set run.h_cs = "hit.soft + 50"`. After `Begin`: `IRun api = run; api.Damage(api.Player, 1, null, true)` → `h_cs == 50`.
5. **Frames popped.** After cases 1, 2 and 3 finish, `run.Hit("soft") == 0` on each run (tests compile `src/Core`, so the internal `Hit` is reachable). The `finally` itself is a code-review item (J3): `Emit` already catches listener exceptions, so no data-only test can throw through a frame.
6. **Loader problems** (`ProblemsIn`, each exactly 1 problem):
   - `"hit": ["soft"]` → contains `hit` and `must be an object`.
   - `"hit": { "Soft": 1 }` → contains `.hit.Soft` and `a-z, 0-9 or _`.
   - `"hit": { "soft": "nope.x" }` → contains `.hit.soft` and `unknown path`.
   - `set { var: "hit.soft", value: 1 }` → contains `"var" must be a path`.
   - value `"hit.$x"` and value `"hit.a.b"` → each contains `unknown path`.
7. **Lint warnings** (`WarningsIn`): pack with `damage … hit: {soft: 1}` and a read of `hit.sfot` → exactly 1 warning containing `hit.sfot is read but no damage`; same pack reading `hit.soft` → 0 warnings (also proves `hit` is marked read: no "not used here").

**Rationale:** Digest can't see `hit` (no NER content uses it yet; run vars excluded), so behavior proof lives here. Case 1 is the hazard the proposal names (`run.soft` leaking into nested damage).

**Verification:** V3a, V3b.

**If this fails:** a pack-syntax problem → fix the test pack, never the expected values. An expected value wrong → engine bug in Step 1/2; fix there.

---

### Step 4: Docs

**Objective:** SC5.
**Confidence:** High
**Depends on:** Steps 1–2
**Do-Projex:** Encouraged

**Files:**
- `docs/effects.md`
- `docs/expressions.md`
- `docs/run-flow.md`
- `docs/csharp-api.md`

**Changes:**
- `effects.md` damage row (`:124`): example gains `"hit": { "soft": 1 }`; description adds: "`hit` names numbers the hooks this damage sets off can read as `hit.NAME`. [Damage and defeat](run-flow.md#damage-and-defeat)".
- `expressions.md` § Paths table, after `arg.NAME` (`:56`): `| \`hit.NAME\` | The \`hit\` value NAME of the damage in progress, 0 outside one. [Damage and defeat](run-flow.md#damage-and-defeat) |`. Also the `## Paths` intro if it lists heads.
- `run-flow.md` § Damage and defeat (`:70-76`), new paragraph after the first:
  > A `damage` can carry a `hit` map of names and expressions, worked out once when it starts (after `amount`). Everything that runs while that damage is in progress reads them as `hit.NAME`: C# interceptors' follow-ups, `damaged`, `defeated`, `knocked_out`, the `run_end` a knockout fires, and whatever those hooks set off, such as a `defeat` effect. A `damage` dealt inside one of those hooks has its own `hit` (0 for names it doesn't give), and the outer one comes back when it ends. Outside any damage `hit.NAME` is 0. Use it instead of a run variable set around the damage: that variable would leak into any damage dealt inside the hooks.
- `csharp-api.md` § Decisions, after the table (`:113` row): "`damage` decisions don't carry the data's `hit` map; an effect a mod compiles from data (`spec.Value`) can read `hit.NAME` like any expression."

**Rationale:** Proposal § Docs lists effects/expressions/run-flow/csharp-api for C2.

**Verification:** V4a; J4.

**If this fails:** prose only; fix in place.

---

### Step 5: Full Proof Bar + Game-Assembly Checks

**Objective:** SC7, SC8, SC4 final.
**Confidence:** High
**Depends on:** Steps 1–4

**Files:** none (writes `$Q/after/*`)

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
  grep -aUv '^digest ' $Q/after/$n-d.txt | cmp - $Q/after/$n-run.txt && echo "$n digest-minus-lines == plain"
done
dotnet run -c Release --project tests/Sim
git diff --stat main -- api src/Game '*.csproj'                         # expect empty
git diff main -- src/Core | grep -E '^-[[:space:]]*(public|protected) '   # expect no output (exit 1)
```
V5g — **only if the human approves it** (orchestrator asks; approved once for an earlier plan): a game-assembly compile with the install redirected away from the game:
```bash
dotnet build -c Release ExpeditionFramework.csproj -p:InstallDir=$Q/game-install/
ls $Q/game-install/ExpeditionFramework.dll                               # exists
git -C . status --porcelain                                              # only intended changes (bin/ obj/ are ignored)
```
Without approval, SC8's game-compile proof rests on V5e + V5f (public surface and `src/Game` untouched; same `src/Core` compiles under ef-sim and tests) + J2 (`netstandard2.1` API use).

**Rationale:** Proposal § Balance-Proof Bar items 1–4, applied to an engine-only change; same compares as the C1 proof.

**Verification:** V5a–V5f (+V5g if approved); J1–J3.

**If this fails:** any `cmp` differs → bug in Step 1 or 2 (no NER/pack change exists); bisect by reverting Step 2 locally, re-run the failing pack. No noise-band acceptance. V5f prints a line → a public signature changed; restore it.

---

## Verification Plan

### Deterministic Suite

Shell: Git Bash, EF root; variables as in Overview. `(×3)` = for `n` in `ST SC NER`.

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V0a | SC6 / Step 0 | `git diff --stat 9cd3750 HEAD -- src api tools tests docs ExpeditionFramework Localization samples '*.csproj'` ; `git status --porcelain -- src api tools tests docs` | both empty | EF source drift |
| V0b | SC6 | `git -C $NER rev-parse --short HEAD` ; `git -C $NER status --porcelain -- ExpeditionFramework Localization sim.json` | `0e0d46f`; empty | Wrong/dirty NER |
| V0c | SC6 | Step 0 loop: 3 `cmp` per pack (×3) | 9 × exit 0 (`lint ok`, `run ok`, `digest ok`) | Baseline no longer holds → stop |
| V0d | SC6 | `dotnet run -c Release --project tests/Sim` | last line `All checks passed`, exit 0 | Pre-existing red |
| V1a | Steps 1–3 | `dotnet build -c Release tools/EfSim` | exit 0, `0 Error(s)` | Compile errors |
| V1b | Steps 1–2 smoke | `for n in ST SC; do d=$(eval echo \$$n); $SIM lint "$d" --strict \| cmp - $P/base/$n-lint.txt; $SIM run "$d" \| cmp - $P/base/$n-run.txt; done` | all exit 0 | Early regression before the long NER run |
| V3a | SC1–SC4 | `dotnet run -c Release --project tests/Sim` | `All checks passed`, exit 0 | Feature / guard regressions |
| V3b | SC4 | `grep -c 'TestHitContext()' tests/Sim/Program.cs` | `2` | Test not wired into `Main` |
| V4a | SC5 | `grep -c 'hit\.NAME' docs/expressions.md docs/run-flow.md docs/effects.md` ; `grep -c '"hit"' docs/effects.md` ; `grep -c 'hit\.NAME' docs/csharp-api.md` | each file ≥ `1` | Missing doc update |
| V5a | SC7 | `cmp $Q/after/$n-lint.txt $P/base/$n-lint.txt` (×3) | exit 0 | Lint drift (new warning, message change) |
| V5b | SC7 | `cmp $Q/after/$n-run.txt $P/base/$n-run.txt` (×3) | exit 0 | Behavior change on hit-less damage |
| V5c | SC7 | `grep -aU '^digest ' $Q/after/$n-d.txt \| cmp - $P/digest2/$n-digest-base.txt` (×3) | exit 0 | Hidden change in log/night-log/memory/rep/credits/items |
| V5d | SC4 | `dotnet run -c Release --project tests/Sim` | `All checks passed`, exit 0 | Integration |
| V5e | SC8 | `git diff --stat main -- api src/Game '*.csproj'` | empty | API / game source / csproj touched |
| V5f | SC8 | `git diff main -- src/Core \| grep -E '^-[[:space:]]*(public\|protected) '` | no output | Public signature changed (breaks game/mods) |
| V5g | SC8 (conditional) | `dotnet build -c Release ExpeditionFramework.csproj -p:InstallDir=$Q/game-install/` | exit 0, `0 Error(s)`; DLL in `$Q/game-install/` | Game assembly compile break (`netstandard2.1`) — **run only with the human's approval** |

### Judgment Checks

| Check | Covers | Why not deterministic |
|-------|--------|-----------------------|
| J1 — Every `dotnet` command in the execution log names `tools/EfSim` or `tests/Sim` (or is V5g after recorded approval) | SC8 | A forbidden build leaves no repo trace |
| J2 — New `src/Core` code uses only `netstandard2.1` APIs (no `TryPeek`, `Index`/`Range` on lists, `GetValueOrDefault` on `Dictionary` without the extension, etc.) | SC8 | Without V5g, ef-sim/tests compile under `net8.0` only |
| J3 — New comments follow Source Hygiene (no projex refs; rationale names the rejected alternative) | Steps 1–2 | Wording |
| J4 — Docs describe exactly the implemented semantics (order, nesting, 0 outside, global names, C# note) | SC5 | Prose |

### Acceptance Criteria Validation
| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| SC1 | V3a (cases 1, 6) | `hit` evaluated, bad shapes are problems |
| SC2 | V3a (cases 1–5) | Nesting, restore, knockout/run_end, defeated, C# frame |
| SC3 | V3a (cases 6–7) | Read-only, unknown paths, read-never-set warning |
| SC4 | V3a, V3b, V5d | Green, wired |
| SC5 | V4a, J4 | Four docs updated, accurate |
| SC6 | V0a–V0d | Baselines hold or plan stopped |
| SC7 | V5a–V5c | Identical lint/run/digest ×3 |
| SC8 | V5e, V5f, J1, J2 (+V5g if approved) | No public/API/game-source change; only allowed builds |

---

## Rollback Plan

1. Execution branch: `projex-abandon` (base `main`); nothing lands.
2. After merge: `git revert` the squash commit; no data/migration (no pack uses `hit`).

---

## Notes

### Decisions (not settled by the proposal)
- **No list sugar** (`"hit": ["soft"]`): `{ "soft": 1 }` is as short; one shape to document and validate.
- **Every damage pushes a frame**, incl. hit-less and C# `IRun.Damage`: that is what makes a nested damage read 0 instead of the outer hit.
- **Window = whole damage call**, from decision to last hook. A `defeat` effect or `leave` run inside a damage hook sees that damage's hit (documented). Rejected: an empty frame for `defeat`/`ICombat.Defeat` — extra rule for no current need.
- **Names global across packs**, not `pack:`-qualified; read-never-set is a warning, not a problem.
- **No C# surface**: no `Decision` copy, no `IRun`/`IScope` member. C# reaches `hit.NAME` through data-compiled expressions. `IRun.Damage` unchanged; the hit overload is `internal`.
- **Evaluation order**: `to`, `amount`, then `hit` values in written order; hit-less damage evaluates exactly as today.
- **Names `a-z0-9_`**, numbers only (`hit.$x` unknown path).

### Follow-up (not planned here): NER `run.soft` → `hit.soft`
Own NER `.projex/` patch or plan after this plan is merged to EF `main`.
- `ExpeditionFramework/combat.json` `land_hit` (`:20-28`): delete `set run.soft = arg.soft` (`:23`) and `set run.soft = 0` (`:25`); `damage` (`:24`) gains `"hit": { "soft": "arg.soft" }`; rewrite comment `:18-19`. Kit `defeated` (`:360-372`): `run.soft` → `hit.soft` at `:364`, `:372`, comment `:360`.
- `ExpeditionFramework/enemies.json` Kade `defeated` (`:363-365`): `memory.rival_soft = hit.soft`; comment `:363`.
- Proof against this plan's result (EF at merged C2): NER `lint --strict` identical to `c-proof/base/NER-lint.txt`; plain `run` byte-identical to `base/NER-run.txt`; `^digest ` lines identical to `digest2/NER-digest-base.txt` (digest excludes run vars, so deleting `run.soft` cannot move it; any diff is a behavior change). Plus a mutation probe on a scratch copy: kit `defeated` `hit.soft` → `!hit.soft` must change the digest (rows 2003/2006/2007 depend on soft takedowns). `grep -c 'run\.soft' ExpeditionFramework/*.json` → 0.

### Split Decision
`No split — single scope, within size budget` (6 steps but ~350 lines; one `.projex` scope; NER work deferred).

### Risks
- **Message change in unknown-path error** alters lint text for packs with a bad path: none of the three has one (lint 0/0); V5a catches otherwise.
- **`NoteRead` drops or misroutes memory notes**: `TestMemoryWarnings` / `TestPackMemory` (V3a) assert the memory read-never-written warnings; V5a catches any new false warning in the three packs.
- **Per-damage allocation** (`List.Add`, dictionary only with `hit`): negligible; ef-sim run time unchanged within noise (not a gate).
- **`netstandard2.1` API use** without V5g: J2.

### Pending Human Decision (non-blocking)
- V5g: run the redirected game-assembly compile (`-p:InstallDir=` into the scratchpad)? The orchestrator asks before execution and records the answer in the execution log. The plan is complete without it (V5e/V5f/J2 stand in).

### Open Questions
None.
