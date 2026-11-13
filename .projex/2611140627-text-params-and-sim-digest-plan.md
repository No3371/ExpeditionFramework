# Text Params for Shared Effects + ef-sim Run Digest

> **Status:** Ready
> **Author:** Claude Opus 5.5 (plan step)
> **Parent:** 2611140500-authoring-language-text-params-hit-context-proposal.md
> **Source:** 2611140500-authoring-language-text-params-hit-context-proposal.md (Accepted 2026-11-14; § Recommended Approach step 1, § Balance-Proof Bar)
> **Related Projex:** 2611140152-ef-ner-next-steps-proposal.md (NER; § C origin) | 2611140220-ef-sim-reach-and-item-guard-plan.md (row keys the proof leans on) | 2611120227-runtime-hooks-expr-expansion-explore.md | 2611120231-pack-schema-loader-expansion-explore.md
> **Worktree:** No — only dirt at plan time is the untracked parent proposal, committed together with this plan before execution; no `projex/*` branches; ~11 files. NER and baseline paths are relative to the main checkout, which worktree mode would break.

---

## Summary

First slice of Track C, EF only: (P) `ef-sim run --digest`, a per-row hash of observable run results, landed and baselined **before** any language change; then (C1) **text params** for shared effects — `$`-named params substituted as `{arg.$NAME}` into the body's JSON and compiled per call site. No pack data changes. Proof = Showcase, Starter, NER give identical lint, byte-identical `run` stdout and identical digests before vs. after C1.

**Scope:** `src/Core` (PackLoader shared effects, Expr `arg` reader, two observe-only hooks), `tools/EfSim` (digest), `tests/Sim`, `docs/`. Nothing in NER.
**Estimated Changes:** 11 files (1 new): ~3 loader fns + 1 new loader helper set, 1 Expr line, 2 small engine hooks, 1 new ef-sim class, ~4 ef-sim edits, 1 test fn, 4 docs.

---

## Objective

### Problem / Gap / Need
- Shared-effect params are numbers only (`docs/packs.md:140`). Literal fields (`reputation.faction`, `set/add.var`, text keys) can't be parameterized → NER's settle is copied 5× with a sign trap (parent § Current State).
- "Identical `run` table" can't prove a refactor: rows print only 3 percentages; wrong faction / wrong night-log key can leave them unchanged (parent § Balance-Proof Bar). A digest over observable results is needed first, and must itself be shown stable and RNG-neutral.

### Success Criteria
- [ ] SC1 — `ef-sim run <pack> --digest` prints one `digest runs[N] …` line per row after the normal output; without `--digest` stdout is byte-identical to baseline (all 3 packs).
- [ ] SC2 — Digest is stable: two `--digest` runs give identical digest lines; `--digest` stdout minus `digest ` lines == plain stdout (all 3 packs). Digest baseline captured **before** Step 3.
- [ ] SC3 — Text params work: `$` params declared with string defaults, `{arg.$NAME}` filled in body string values at compile, body compiled per call site; numeric-only shared effects keep the compile-once path.
- [ ] SC4 — Guards: value `^[a-z0-9_]+$` | number / missing required → problem | unknown `{arg.$x}` → problem | `arg.$x` outside `{}` → problem | bad `$` param name → problem | self/mutual recursion → one problem, no hang | `{arg.$x}` in a numeric-only shared effect → problem | uncalled text-param effect → warning. Problem paths name the call site and the body path.
- [ ] SC5 — After Step 3: Showcase, Starter, NER `lint --strict` byte-identical to baseline; `run` stdout byte-identical to baseline; `run --digest` digest lines identical to the Step 2 digest baseline.
- [ ] SC6 — `dotnet run -c Release --project tests/Sim` → `All checks passed`, exit 0, incl. new `TestTextParams`.
- [ ] SC7 — Docs updated: `packs.md`, `expressions.md`, `text.md`, `testing.md`.
- [ ] SC8 — No build of `ExpeditionFramework.csproj` (or NER's csproj) at any point; only `tools/EfSim` and `tests/Sim` projects are built/run.

### Out of Scope
- NER `rep_book` refactor + 2 loc keys + week-reset line → separate NER plan (follow-up 1).
- C2 hit context (`damage` `hit` map, `hit.NAME`) → later EF plan (follow-up 2). C3 `event.*` → parked.
- Text substitution in object **keys** (values only; a key containing `{arg.$` is a problem).
- Text params for anything other than shared effects (`interactions`, `templates`, rooms).
- Showcase/Starter data changes (any edit would void the identical-output proof).
- In-game verification (needs a csproj build).

---

## Context

### Current State
- `DeclareShared` (`src/Core/PackLoader.cs:460-477`): splits `params`/`do`, queues body compile into `sharedBodies` (run in `CompileFile`, `:535`), registers `registry.Effects[pack:name] = call => CompileSharedCall(...)`.
- `CompileSharedCall` (`:479-513`): `call.MarkAllRead()`; unknown arg → `call.Error`; bare form (`{"raise_heat": 2}`) fills param 0; each param → `Value` (`call.Value(key,0)` or `ValueOf(default)`); runtime: `EnterShared` guard (32-deep), args `Dictionary<string,int>`, `body[0](Scope.From(s).WithArgs(args))`, `LeaveShared` in `finally`.
- Compile order (`CompileAll`, `:210-236`): patches → `Declare` all files of all packs → `CompileFile` each. So every shared effect is registered before any caller compiles.
- `Effect(node, pack, at)` (`:1542-1562`) builds `Spec(this, pack, at + "." + key, value)`; `Spec.Path` is the call site path. Problems attribute to field `current` (`Problem`, `:640-646`).
- `Expr.Reader` `case "arg": return s => s.Arg(name);` (`src/Core/Expr.cs:148`) — accepts `arg.$x` today and reads 0. `TextReader` (`:211+`) has no `arg` head, so `{arg.…}` in runtime key templates never fills.
- `$` param names: legal today but unused — grep of every `"params"` in EF `docs/examples` and NER `ExpeditionFramework/*.json`: 0 `$` names; `arg.$` / `{arg.`: 0 hits.
- ef-sim `run` (`tools/EfSim/PackRun.cs`): args `--runs` only (`:19-23`); `Play` (`:187-247`) builds `KitBridge` + `ExpeditionRun` per run, prints a row line; final line `ef-sim <id>: …`. `RowResult` (`:169`).
- Observables today: run log lines are kept in `ExpeditionRun.log`, capped at `LogLimit = 200` (`ExpeditionRun.cs:194,358-363`; single writer `Log`) — so reading `Log_` at run end loses lines. `KitBridge.NightLog` (`KitBridge.cs:113`) drops text. `KitBridge.reputation` (`:99`) private. `MemoryStore` (`Model.cs:554-563`) has no enumeration; `SimMemory.Start` (`SimConfig.cs:43`) always returns a `MemoryStore`.
- Build isolation: `tools/EfSim/EfSim.csproj` and `tests/Sim/Sim.csproj` compile `src/Core/**` + `api/*.cs` by file include; no ProjectReference to `ExpeditionFramework.csproj`. Root csproj compiles `src\**\*.cs` only (`EnableDefaultCompileItems=false`), so a new `tools/EfSim/*.cs` never enters the game build.
- Baseline (proposal): EF `6b99edf`, NER `1277ab0`; `<baseline-dir>` (orchestrator's `c-baseline` scratch folder) holds `ner-run.txt`, `ner-run-2.txt` (identical), `Showcase-run.txt`, `Starter-run.txt`, `ner-lint.txt`; CRLF, as .NET writes on Windows. No Showcase/Starter lint captured yet.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `src/Core/ExpeditionRun.cs` | Run; `Log` writer | Add observe-only `Logged` callback invoked in `Log` |
| `src/Core/Model.cs` | `MemoryStore` | Add read-only `Numbers` / `Texts` enumeration |
| `tools/EfSim/RunDigest.cs` | **new** | SHA-256 accumulator over per-run canonical records |
| `tools/EfSim/KitBridge.cs` | Sim bridge | Keep night-log lines; expose reputation map read-only |
| `tools/EfSim/PackRun.cs` | `ef-sim run` | `--digest` flag; feed digest per run; print digest lines last |
| `tools/EfSim/Program.cs` | CLI usage | Usage line shows `[--digest]` |
| `src/Core/PackLoader.cs` | Loader | Text params: declare checks, per-site compile, substitution, guards, cycle check, uncalled warning |
| `src/Core/Expr.cs` | Paths | `arg.$NAME` has no number reader (→ compile error) |
| `tests/Sim/Program.cs` | Framework tests | New `TestTextParams()` |
| `docs/packs.md`, `docs/expressions.md`, `docs/text.md`, `docs/testing.md` | Docs | Text params; `--digest` + "proving a refactor" recipe |

### Dependencies
- **Requires:** EF `main` @ `6b99edf` + this plan and parent proposal committed; NER `master` @ `1277ab0` checked out at `../NightExpeditionsReborn` with clean `ExpeditionFramework/`, `Localization/`, `sim.json`.
- **Blocks:** NER `rep_book` refactor plan (needs C1 + digest); C2 hit-context plan (needs digest).

### Constraints
- Never build `ExpeditionFramework.csproj` or NER's csproj (installs into the live game). Always pass an explicit project: `dotnet build -c Release tools/EfSim`, `dotnet run -c Release --project tests/Sim`. Never a bare `dotnet build` at EF root.
- Digest observes only: must draw no random number and change no state the run reads.
- Numeric-only shared effects: compile path and runtime lambda unchanged.
- Git hook blocks `--amend`, branch deletion, `stash drop`.
- Source Hygiene (SKILL.md): no projex IDs / step labels in code comments; rationale comments name the rejected alternative.

### Assumptions
- A1 — `dotnet run --no-build` stdout of the three packs is deterministic run-to-run (proposal reproduced NER twice; Step 0 re-checks).
- A2 — Step 0 capture at branch start equals `<baseline-dir>` (code = `6b99edf` + docs only). If `<baseline-dir>` is missing, Step 0's own capture is the baseline.
- A3 — Adding members to `ExpeditionRun` / `MemoryStore` is source-compatible with `src/Game` and `api/` (no interface changes). Not compile-checked (csproj build forbidden); judgment check J2.
- A4 — Parsed JSON trees are `Dictionary<string, object>` (`Node`) / `List<object>` / `string` / `double` / `bool` / `null` (`src/Core/Json.cs`); verify before writing the substitution copy.
- A5 — `Finish()` may run more than once per loader (tests do); the uncalled-effect warning must fire at most once per effect.

### Impact Analysis
- **Direct:** files in Key Files.
- **Adjacent:** every shared-effect call site (all packs) passes through `CompileSharedCall`; `Expr.Reader` used by every expression; `ExpeditionRun.Log` hot path (one null-check added).
- **Downstream:** NER refactor plan consumes C1 + digest; `src/Game` compiles `src/Core` (additive members only); C# mods compiling effects via `PackLoader.Effect` after `Finish` may see a false "never called" warning for text-param effects (documented).

---

## Implementation

### Overview
Order is the proof: **0** baseline → **1** digest instrument (no language change) → **2** prove digest + capture digest baseline → **3** C1 loader → **4** tests → **5** docs → **6** full bar. Commit after each code step (execute-projex convention).

Conventions for commands: EF root as cwd, Git Bash. `<proof-dir>` = a scratch folder outside both repos (e.g. the session scratchpad). `NER=../NightExpeditionsReborn`, `SC=docs/examples/Showcase`, `ST=docs/examples/Starter`. `SIM="dotnet run -c Release --no-build --project tools/EfSim --"`. Rebuild ef-sim (`dotnet build -c Release tools/EfSim`) after every source change before any `--no-build` run.

---

### Step 0: Preflight + Baseline Capture

**Objective:** Fix the before-state at branch start; confirm it equals the proposal baseline.
**Confidence:** High
**Depends on:** None

**Files:** none (writes `<proof-dir>/base/*` only)

**Changes:**
```bash
git -C ../NightExpeditionsReborn rev-parse --short HEAD          # expect 1277ab0
git -C ../NightExpeditionsReborn status --porcelain -- ExpeditionFramework Localization sim.json   # expect empty
dotnet build -c Release tools/EfSim
mkdir -p <proof-dir>/base
for p in NER:$NER SC:$SC ST:$ST; do n=${p%%:*}; d=${p#*:};
  $SIM lint "$d" --strict > <proof-dir>/base/$n-lint.txt
  $SIM run  "$d"          > <proof-dir>/base/$n-run.txt; done
cmp <proof-dir>/base/NER-run.txt <baseline-dir>/ner-run.txt
cmp <proof-dir>/base/SC-run.txt  <baseline-dir>/Showcase-run.txt
cmp <proof-dir>/base/ST-run.txt  <baseline-dir>/Starter-run.txt
cmp <proof-dir>/base/NER-lint.txt <baseline-dir>/ner-lint.txt
dotnet run -c Release --project tests/Sim                         # expect "All checks passed"
```

**Rationale:** Showcase/Starter lint was never captured; branch-start capture covers it and guards against drift since the proposal.

**Verification:** V0a, V0b, V0c.

**If this fails:** NER not at `1277ab0` or dirty → stop, report (do not touch NER). `cmp` differs → stop, report; nondeterminism or drift voids the bar. tests/Sim red at base → record failing checks in the execution log as pre-existing; SC6 then means "no new failures".

---

### Step 1: Digest Instrument

**Objective:** `ef-sim run --digest` with no effect on plain output or RNG.
**Confidence:** High
**Depends on:** Step 0
**Verify-Projex:** Encouraged

**Files:**
- `src/Core/ExpeditionRun.cs`
- `src/Core/Model.cs`
- `tools/EfSim/RunDigest.cs` (new)
- `tools/EfSim/KitBridge.cs`
- `tools/EfSim/PackRun.cs`
- `tools/EfSim/Program.cs`

**Changes:**

```csharp
// src/Core/ExpeditionRun.cs — beside Log_ (:249) and in Log (:358)
// Before:
public void Log(string tone, string text)
{
    log.Add(new LogLine(tone, text, pendingRoll));
    pendingRoll = null;
    ...
// After:
/// <summary>Called with every line as it is logged. The log itself keeps only the last 200 lines; tools that need all of them listen here.</summary>
public Action<LogLine> Logged { get; set; }

public void Log(string tone, string text)
{
    var line = new LogLine(tone, text, pendingRoll);
    log.Add(line);
    pendingRoll = null;
    if (log.Count > LogLimit) log.RemoveRange(0, log.Count - LogLimit);
    Logged?.Invoke(line);
}
```

```csharp
// src/Core/Model.cs — MemoryStore (:554)
// After (added):
public IEnumerable<KeyValuePair<(string Pack, string Name), int>> Numbers => numbers;
public IEnumerable<KeyValuePair<(string Pack, string Name), string>> Texts => texts;
```

```csharp
// tools/EfSim/KitBridge.cs
// Before:
public void NightLog(string text, string color) { if (text != null && text.Contains("{")) problems.Add("unfilled night log: " + text); }
// After:
public readonly List<string> NightLines = new List<string>();
public void NightLog(string text, string color)
{
    if (text != null && text.Contains("{")) problems.Add("unfilled night log: " + text);
    NightLines.Add(color + "\t" + text);
}
public IReadOnlyDictionary<string, double> ReputationNow => reputation;
```

`tools/EfSim/RunDigest.cs` (new) — `internal sealed class RunDigest`:
- Field `IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256)`; `int runs`.
- `void Add(ExpeditionRun run, List<LogLine> lines, KitBridge bridge, IMemory memory)` writes one UTF-8 record, each line `\n`-terminated:
  - `run <index> <run.Outcome ?? "-">`
  - per log line, in order: `L <tone>\t<roll ?? "">\t<text>`
  - per night line, in order: `N <color>\t<text>`
  - memory (`(MemoryStore)memory`): numbers `!= 0` → `M <pack>\t<name>\t<value>`; texts `!= ""` → `T <pack>\t<name>\t<text>`; each sorted by pack then name, `StringComparer.Ordinal`
  - reputation: `bridge.ReputationNow` sorted by faction (ordinal) → `R <faction>\t<value.ToString("R", CultureInfo.InvariantCulture)>`
- `string Hex()` → lowercase hex of `GetHashAndReset()`.
- Class comment states what is hashed and why run vars are excluded (C2 removes `run.soft` on purpose; hashing run vars would flag an intended refactor) and why SHA-256 over `string.GetHashCode` (per-process randomized → not comparable across runs).

```csharp
// tools/EfSim/PackRun.cs
// Usage: "usage: ef-sim run <pack folder> [--runs <n>] [--digest]"
// Arg loop: else if (args[i] == "--digest") digest = true;
// RowResult gains: public string Digest;
// Play(..., bool digest): per run, before run.Begin():
//     var lines = digest ? new List<LogLine>() : null;
//     if (digest) run.Logged = lines.Add;
//   after the try/catch, before the tally:
//     rowDigest?.Add(run, lines, bridge, memory);
//   end: result.Digest = rowDigest?.Hex();
// Run(): collect $"digest runs[{r}] {row.Area} {row.Kit} {row.Bot} {last.Digest}" per row;
//   print them after the final "ef-sim <id>: ..." line (so the plain output stays a byte-identical prefix).
```

```csharp
// tools/EfSim/Program.cs
// Before: Console.WriteLine("       ef-sim run <pack folder> [--runs <n>]");
// After:  Console.WriteLine("       ef-sim run <pack folder> [--runs <n>] [--digest]");
```

**Rationale:** Run log is capped at 200 → only a per-line callback sees every line; one null-check on the engine's log path is the smallest hook. Memory enumeration on `MemoryStore` beats a recording decorator (decorator misses seeded keys). Digest lines last → plain output is an exact prefix, so "digest didn't perturb the run" is one `cmp`. Night-log capture changes nothing observable (problems set unchanged).

**Verification:** V1a (build), then Step 2.

**If this fails:** `git -C . checkout -- <files>`; delete `tools/EfSim/RunDigest.cs`.

---

### Step 2: Prove Digest + Capture Digest Baseline

**Objective:** Show the instrument is neutral and stable; freeze digests before any language change.
**Confidence:** High
**Depends on:** Step 1

**Files:** none (writes `<proof-dir>/digest/*`)

**Changes:**
```bash
dotnet build -c Release tools/EfSim
mkdir -p <proof-dir>/digest
for n in NER SC ST; do d=$(eval echo \$$n);
  $SIM run "$d"          > <proof-dir>/digest/$n-plain.txt
  $SIM run "$d" --digest > <proof-dir>/digest/$n-d1.txt
  $SIM run "$d" --digest > <proof-dir>/digest/$n-d2.txt
  cmp <proof-dir>/digest/$n-plain.txt <proof-dir>/base/$n-run.txt
  grep -v '^digest ' <proof-dir>/digest/$n-d1.txt | cmp - <proof-dir>/digest/$n-plain.txt
  cmp <proof-dir>/digest/$n-d1.txt <proof-dir>/digest/$n-d2.txt
  grep -c '^digest runs\[' <proof-dir>/digest/$n-d1.txt
  grep '^digest ' <proof-dir>/digest/$n-d1.txt > <proof-dir>/digest/$n-digest-base.txt; done
```
Expected digest line counts: NER 39, Showcase 3, Starter 1.

**Rationale:** Settles the proposal's "digest itself perturbs RNG" risk and proves digest determinism; `*-digest-base.txt` is the reference for Step 6. Commit Step 1 only after this passes.

**Verification:** V2a–V2d.

**If this fails:** plain ≠ base → Step 1 changed behavior: find the RNG/state touch, fix, repeat. d1 ≠ d2 → digest reads something nondeterministic (dictionary order, culture formatting): fix ordering/formatting, repeat.

---

### Step 3: C1 Text Params in the Loader

**Objective:** `$` params, `{arg.$NAME}` substitution, per-site compile, guards.
**Confidence:** Medium (new compile path; numeric path must stay untouched)
**Depends on:** Step 2
**Verify-Projex:** Encouraged

**Files:**
- `src/Core/PackLoader.cs`
- `src/Core/Expr.cs`

**Changes:**

```csharp
// src/Core/Expr.cs Reader (:148)
// Before:
case "arg": return s => s.Arg(name);
// After:
// A "$" arg is text, filled into the shared effect's JSON at load as {arg.$NAME}; as a number it would read 0.
case "arg": return name.StartsWith("$", StringComparison.Ordinal) ? null : s => s.Arg(name);
```

`src/Core/PackLoader.cs`:

1. **Fields:** `private readonly HashSet<string> expanding` (qualified names mid per-site compile); `private readonly Dictionary<string, (string Pack, string At)> uncalledText` (text-param effects not yet called); `static readonly Regex TextValue = new Regex("^[a-z0-9_]+$")`, `TextParamName = new Regex("^\\$[a-z0-9_]+$")`, `Placeholder = new Regex("\\{arg\\.\\$([^}]*)\\}")`.

2. **`DeclareShared`** — after reading `parameters`:
   - `bool text = parameters.Any(p => p.Key.StartsWith("$"))`.
   - For each `$` param: name must match `TextParamName` else `Problem(at + ".params." + key, "a text parameter is $ then a-z, 0-9 or _")`; default must be a JSON string else `Problem(..., "a text parameter's default is text (\"\" for none)")`; a non-empty default must match `TextValue` else problem.
   - Scan `body` (helper `ScanTextUse`, below) with the declared `$` names: any key containing `{arg.$` → problem; any `{arg.$x}` whose `x` isn't declared → `Problem(bodyPath, "no text parameter $x")`; any `arg.$` not inside a placeholder → `Problem(bodyPath, "write {arg.$x}: a text parameter is filled in as text, not read as a number")`. (Numeric-only effects: every `{arg.$` is a problem — same helper with an empty set. Read-only scan: compile of numeric bodies unchanged.)
   - `text == false` → today's code exactly (queue body, register `CompileSharedCall` with `compiled`).
   - `text == true` → don't queue body; `uncalledText[pack + ":" + name] = (pack, at)`; register `call => CompileSharedCall(call, name, parameters, null, pack, at, body)`.

3. **`CompileSharedCall`** gains `object rawBody = null`. First line when `rawBody != null`: `uncalledText.Remove(pack + ":" + name)` (before any error return, so a call with bad args doesn't also warn "never called"). Parameter loop: `$` params are skipped when building `values` (no `Value` compile), and instead resolved into `Dictionary<string, string> textArgs`:
   - source: `call.Args[key]` if present, else `call.Args["value"]` when bare and `i == 0`, else the declared default.
   - not a `string` → `call.Error("\"" + key + "\" is text: give a name like lower_level, not a number")`; empty → `call.Error(name + " needs " + key)`; fails `TextValue` → `call.Error("\"" + key + "\" must be a-z, 0-9 or _ (got \"" + v + "\")")`. On any error return `s => { }`.
   - If `rawBody != null`: `string qualified = pack + ":" + name;` if `!expanding.Add(qualified)` → `call.Error(name + " calls itself through its text parameters")`, return `s => { }`; else `try { body = new[] { Effect(Fill(rawBody, textArgs), pack, call.Path + ".do") }; } finally { expanding.Remove(qualified); }`.
   - Runtime lambda unchanged (`EnterShared` / args / `body[0]` / `LeaveShared`).

4. **`Fill(object node, Dictionary<string,string> args)`** — returns a deep copy: `Node` → new `Node` same key order, values filled; `List<object>` → new list; `string` → `Placeholder.Replace(s, m => args.TryGetValue(m.Groups[1].Value, out string v) ? v : m.Value)` (an unknown name was already reported by the declare scan; leaving it unfilled avoids a second, confusing error); other values as-is. Copy (not in-place): the raw body is reused by every call site.

5. **`ScanTextUse(object node, ISet<string> declared, string at)`** — walks the raw body with JSON-ish paths (`[i]`, `.key`) and reports as in 2.

6. **`Finish()`** — after existing checks: for each `uncalledText` entry: `current = entry.Pack; Warn(entry.At, "never called, so its body is not checked (text-parameter effects compile at each call)")`; then `uncalledText.Clear()` (fires once; A5).

Comments required (Source Hygiene rule 6): why per-site compile only for `$` effects (rejected: per-site for all → would change the numeric path the proof depends on and multiply compile cost); why values only, not keys (rejected: key substitution can collide two keys silently); why the value rule (rejected: free text → `{arg.$x}` could inject expression syntax into a `value` field).

**Rationale:** Substitution before compile makes every filled field a literal, so existing compile checks (faction list, var writer, text-key registration → lint) apply unchanged. `call.Path + ".do"` as body path names the call site and the body position in one string (proposal risk "paths point into body, not caller"). Cycle set instead of depth cap: compile recursion through text params is always a cycle, and the set reports it exactly once. Problems keep `current` = the caller's pack (the caller passed the values).

**Verification:** V1a (build), V3a (Showcase/Starter quick lint), then Steps 4 and 6.

**If this fails:** `git -C . checkout -- src/Core/PackLoader.cs src/Core/Expr.cs`.

---

### Step 4: Tests

**Objective:** Cover SC3/SC4 in `tests/Sim`.
**Confidence:** High
**Depends on:** Step 3
**Do-Projex:** Encouraged

**Files:** `tests/Sim/Program.cs`

**Changes:** add `TestTextParams()`; call it in `Main` after `TestSharedEffectSurvivesThrow()`. Uses `ProblemsIn` / `WarningsIn` / `Compiled` and `FakeBridge` (`Rep`), like `TestReputation`. Cases:
1. **Works:** effect `book` with `"params": { "$f": "", "n": 0 }`, body: `reputation {faction "{arg.$f}", value "arg.n", quiet}`, `set {var "run.{arg.$f}_n", value "arg.n"}`, `say "k_{arg.$f}"`; room `enter` calls `{book {"$f":"security","n":2}}` and `{book {"$f":"upper_level","n":3}}`. Assert 0 problems; after `Begin`: `bridge.Rep["security"]` +2, `["upper_level"]` +3, `run.Get("security_n")==2`, `run.Get("upper_level_n")==3`; `content.TextKeys` has `("t","k_security")` and `("t","k_upper_level")`.
2. **Bare form:** `{ "book1": "security" }` with `$f` as first param → same as named.
3. **Default:** `"$f": "security"` default, call `{ "book": {} }` → uses it.
4. **Nested:** outer text effect passes `{"$f": "{arg.$g}"}` to inner text effect → works.
5. **Numeric unchanged:** `raise`-style numeric effect called twice still compiles once (existing behavior) — assert run result only.
6. **Bad value** `"$f": "run.x"` → 1 problem; contains call-site path (`rooms.r.enter[0].book`) and `$f`.
7. **Number** `"$f": 3` → 1 problem containing `is text`.
8. **Missing required** (default `""`, not passed) → 1 problem containing `needs $f`.
9. **Unknown placeholder** `{arg.$g}` in body → 1 problem at `effects.book.do[…]` containing `no text parameter $g`.
10. **Outside braces** `"value": "arg.$f"` → problem containing `{arg.$f}`.
11. **Bad name** `"$F"` → problem at `.params.$F`.
12. **Bad faction via param** `"$f": "church"` → problem path starts with the call site and contains `.do[0].reputation`.
13. **Self recursion** `a` calls `{a {"$x":"{arg.$x}"}}` → exactly 1 `calls itself` problem, returns (no stack overflow). **Mutual** `a`→`b`→`a` → ≥1, no hang.
14. **Numeric effect with `{arg.$x}`** → problem.
15. **Expression `arg.$x`** in a room `set` → problem (Expr reader).
16. **Uncalled** text effect → exactly 1 warning containing `never called`; calling `loader.Finish()` twice still yields 1.

**Rationale:** Loader error text/paths and recursion aren't covered by any pack row (parent § What existing rows catch).

**Verification:** V4a.

**If this fails:** fix code (Step 3) or the test expectation; never weaken an assertion to pass.

---

### Step 5: Docs

**Objective:** Document text params and the digest.
**Confidence:** High
**Depends on:** Steps 2–3
**Do-Projex:** Encouraged

**Files:** `docs/packs.md`, `docs/expressions.md`, `docs/text.md`, `docs/testing.md`

**Changes:**
- `packs.md` § shared effects (`:140`): replace "Parameters are numbers." with a "Text parameters" paragraph + example: `$` name with string default (`""` = required), `{arg.$NAME}` anywhere in a string value of the body (field values, var paths, text keys, expressions), filled at load and compiled per call site; value rule `a-z 0-9 _`; values only, not keys; errors point at the call site + body place; a text-param effect that is never called gets a warning (C# callers after load: expected). Keep "32 levels deep" sentence. Example: a small two-faction settle using `reputation.faction "{arg.$faction}"`, `memory.gain_{arg.$short}`, `night_log.text "rep_up_{arg.$short}"`.
- `expressions.md` `arg.NAME` row (`:56`): add "`arg.$NAME` is not a path: text parameters are written `{arg.$NAME}` and filled at load (see packs.md)."
- `text.md` § Key templates (`:67-78`): one paragraph: `{arg.$NAME}` inside a shared effect is filled at load, so lint checks the resulting key; `{path}` templates fill at run time and lint can't.
- `testing.md` § Run: `--digest` in the command synopsis + option sentence: what is hashed (outcome, every run-log line, night-log lines, end memory with 0/"" ≡ unset, end reputation; not run vars), printed after the summary as `digest runs[N] area kit bot HEX`. New subsection "Proving a refactor": before/after `lint --strict` identical, `run` stdout byte-identical, `--digest` lines identical, at committed seeds and run counts, no `--runs`; any difference is a bug, not noise.

**Rationale:** Parent § Docs (C1 + P items only; C2 doc items deferred).

**Verification:** V5a, V5b.

**If this fails:** revert the doc files.

---

### Step 6: Full Proof Bar

**Objective:** SC5, SC6, SC8.
**Confidence:** High
**Depends on:** Steps 3–5

**Files:** none (writes `<proof-dir>/after/*`)

**Changes:**
```bash
dotnet build -c Release tools/EfSim
mkdir -p <proof-dir>/after
for n in NER SC ST; do d=$(eval echo \$$n);
  $SIM lint "$d" --strict > <proof-dir>/after/$n-lint.txt
  $SIM run  "$d"          > <proof-dir>/after/$n-run.txt
  $SIM run  "$d" --digest > <proof-dir>/after/$n-d.txt
  cmp <proof-dir>/after/$n-lint.txt <proof-dir>/base/$n-lint.txt
  cmp <proof-dir>/after/$n-run.txt  <proof-dir>/base/$n-run.txt
  grep '^digest ' <proof-dir>/after/$n-d.txt | cmp - <proof-dir>/digest/$n-digest-base.txt; done
dotnet run -c Release --project tests/Sim
```

**Rationale:** Parent § Balance-Proof Bar items 1–4 applied to the engine change alone.

**Verification:** V6a–V6d.

**If this fails:** any `cmp` difference → bug in Step 3 (or Step 1 hooks); bisect by reverting Step 3 locally and re-running the failing pack. No noise-band acceptance.

---

## Verification Plan

### Deterministic Suite

Shell: Git Bash from EF root; `$SIM`, `$NER`, `$SC`, `$ST`, `<proof-dir>`, `<baseline-dir>` as in Overview. PowerShell alternative for `cmp a b`: `fc.exe /b a b`.

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V0a | Step 0 / deps | `git -C ../NightExpeditionsReborn rev-parse --short HEAD` ; `git -C ../NightExpeditionsReborn status --porcelain -- ExpeditionFramework Localization sim.json` | `1277ab0`; empty | Wrong NER state |
| V0b | Step 0 / A2 | Step 0 four `cmp` lines | all exit 0 | Drift vs. proposal baseline / nondeterminism |
| V0c | Step 0 | `dotnet run -c Release --project tests/Sim` | last line `All checks passed`, exit 0 | Pre-existing red |
| V1a | Steps 1, 3 | `dotnet build -c Release tools/EfSim` | exit 0, `0 Error(s)` | Compile errors |
| V2a | SC1 | `cmp <proof-dir>/digest/$n-plain.txt <proof-dir>/base/$n-run.txt` (×3) | exit 0 | Step 1 changed plain output / RNG |
| V2b | SC2 | `grep -v '^digest ' $n-d1.txt \| cmp - $n-plain.txt` (×3) | exit 0 | `--digest` perturbs the run |
| V2c | SC2 | `cmp $n-d1.txt $n-d2.txt` (×3) | exit 0 | Nondeterministic digest |
| V2d | SC1 | `grep -c '^digest runs\[' $n-d1.txt` | NER `39`, SC `3`, ST `1` | Missing/extra rows |
| V3a | Step 3 smoke | `$SIM lint $SC --strict` ; `$SIM lint $ST --strict` | `0 error(s), 0 warning(s)` each | Quick regression before the long NER run |
| V4a | SC3, SC4, SC6 | `dotnet run -c Release --project tests/Sim` | `All checks passed`, exit 0 | Feature / guard regressions |
| V4b | SC4 | `grep -c 'TestTextParams()' tests/Sim/Program.cs` | `2` (definition + call) | Test not wired into `Main` |
| V5a | SC7 | `grep -c 'Parameters are numbers' docs/packs.md` | `0` | Stale doc line |
| V5b | SC7 | `grep -l '{arg\.\$' docs/packs.md docs/expressions.md docs/text.md` ; `grep -c -- '--digest' docs/testing.md` | 3 files listed; ≥ `2` | Missing doc updates |
| V6a | SC5 | `cmp after/$n-lint.txt base/$n-lint.txt` (×3) | exit 0 | Lint drift (new warnings/errors) |
| V6b | SC5 | `cmp after/$n-run.txt base/$n-run.txt` (×3) | exit 0 | Behavior change in numeric path |
| V6c | SC5 | `grep '^digest ' after/$n-d.txt \| cmp - digest/$n-digest-base.txt` (×3) | exit 0 | Hidden change (log/night-log/memory/rep) |
| V6d | SC6 | `dotnet run -c Release --project tests/Sim` | `All checks passed`, exit 0 | Integration |
| V6e | SC8 | `git -C . diff --stat 6b99edf -- '*.csproj'` and execution log shows no `dotnet build` without `tools/EfSim` / `tests/Sim` | empty diff | Accidental game build / csproj edit |

### Judgment Checks

| Check | Covers | Why not deterministic |
|-------|--------|-----------------------|
| J1 — Every `dotnet` command in the execution log names `tools/EfSim` or `tests/Sim` | SC8 | A forbidden build leaves no repo trace; only the log shows it |
| J2 — `src/Game` / `api/` don't implement or reflect over `ExpeditionRun` / `MemoryStore` in a way the new members break (grep `MemoryStore\|: ExpeditionRun\|Log(` in `src/Game`) | A3 | Game assembly can't be compiled here (csproj build forbidden) |
| J3 — New comments follow Source Hygiene (no projex refs, rationale names rejected alternative) | Steps 1, 3 | Wording judgment |
| J4 — Docs example reads correctly and matches implemented error texts | SC7 | Prose |

### Acceptance Criteria Validation
| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| SC1 | V2a, V2d | Plain output unchanged; one digest line per row |
| SC2 | V2b, V2c | Digest neutral and deterministic; baseline frozen before Step 3 |
| SC3 | V4a | Text-param cases 1–5 pass |
| SC4 | V4a, V4b | Guard cases 6–16 pass |
| SC5 | V6a, V6b, V6c | All three packs identical vs. base and digest base |
| SC6 | V4a, V6d | `All checks passed` |
| SC7 | V5a, V5b, J4 | Docs updated |
| SC8 | V6e, J1 | No game csproj build or edit |

---

## Rollback Plan

1. Before close: abandon the `projex/…` branch via `projex-abandon` — `main` untouched.
2. After merge: `git revert` the squash commit; no data/pack migration exists (no pack uses `$` params; NER refactor not yet landed).

---

## Notes

### Follow-ups (not planned here)
1. **NER plan** — `rep_book` text-param shared effect replaces the 5 settle blocks (`kit.json:242-280`), adds `ne_rep_down_upper` / `ne_rep_up_revolution` (en + zh, drafts in the parent's Open Questions), adds `rep_gain_revolution` to the week-reset block; acceptance = parent § Balance-Proof Bar using this plan's `--digest`. Requires this plan closed.
2. **EF C2 plan** — `damage` `hit` map + `hit.NAME`; NER 2-line `run.soft` patch after.
3. C3 parked (parent).

### Split Decision
`No split — single scope, within size budget.` Digest and C1 share one proof sequence (digest must be baselined between them); the parent recommends them as one EF plan. 7 steps but well under 500 lines.

### Risks
- Per-site compile touches numeric path → guarded by branch-on-`$` + V6b/V6c on 43 rows.
- `Logged` callback cost on the engine log path → one null-check; game never sets it.
- Uncalled-effect warning false positive for effects called only from mod-gated files (lint without `--mod`) or from C# after load → documented; lint with `--mod`.
- `current` attribution: problems in a cross-pack text-param call attribute to the caller pack → intended (caller supplied the values).
- NER run ~2m40s × 6 runs in Steps 0/2/6 (~16 min) → expected cost; run packs sequentially, not in parallel (shared `bin/`).

### Open Questions
- [ ] None blocking. Decisions taken here beyond the proposal (reviewable): values-only substitution; `""` default = required; uncalled text-param effect → warning; `arg.$x` becomes a compile error everywhere; digest = SHA-256, printed after the summary line; `Logged` hook + `MemoryStore` enumeration as the two `src/Core` additions for the digest.
