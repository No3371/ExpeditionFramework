# Walkthrough: Weapon Facts From the Game + Melee Measurement Command

> **Execution Date:** 2026-11-15
> **Completed By:** Claude Sonnet 5.5 (execute and close)
> **Source Plan:** 2611150108-melee-weapon-facts-plan.md
> **Parent:** 2611150108-melee-weapon-facts-plan.md
> **Duration:** ~20 min execution (log 01:15 - 01:36)
> **Result:** Partial Success: every offline criterion passes; the in-game behaviour is unverified (see Still Unverified In Game)

---

## Summary

EF now sets six whole-number facts (`cells`, `reach`, `breadth`, `price`, `kinds`, `purposes`) on every weapon `ItemReader.ReadWeapon` reads, after `ApplyStats` so the game wins. A game-free `Core.Footprint.Measure` does the box measurement and is tested. A framework-wide debug entry "Expeditions: log melee weapon facts" logs one `Melee facts:` line per melee weapon, using the same `ReadFacts` code a run uses. No pack reads the new props: NER lint, run and digest are byte-identical to the base build. Deploy was NOT done (human instruction: "Commit, deploy later"); nothing in the game has run this code.

---

## Still Unverified In Game

Everything below needs one game launch with the built dll deployed. None of it can be proved offline, and none was observed.

1. **The F9 entry appears and writes.** That "Expeditions: log melee weapon facts" shows in the F9 panel (Modding category) and, when clicked, writes its `Melee facts:` lines to `Player.log` and shows the notify line. The code compiles and `LogMeleeFacts` is in the built dll; nothing more is known.
2. **`DirectoryMaster.Create<GameItem>` has no side effects.** Plan assumption A2. The command builds each weapon with the raw factory (no unique id, no ownership). If it leaks registrations, spawns objects, or fires hooks, only a launch shows it. Mitigations in code: dev-only, human-triggered, per-id try/catch.
3. **Whether `UnusedDirectory` is loaded.** The command logs `Melee facts: directory UnusedDirectory is not loaded` if absent, and then `wrench_orange` (the only expected member) is missing from the data. Unknown until a launch.
4. **`kinds`, `purposes`, `price` match the plan's predictions.** The plan predicts `kinds/purposes`: shiv 2/0, pipe 2/0, box cutter 2/2, cleaver 1/1, kitchen knife 1/1, crowbar 1/1, hatchet 1/1, wrench 1/1, stun gun 1/0, the rest 0/0, and prices from the decompile (shiv 8, skinning knife 100, combat knife 75, box cutter 28, cleaver 65, kitchen knife 45, machete 185, crowbar 75, hatchet 55, pipe 15, stun baton 165, tazer 100, stun gun 250, wrench 30). These come from reading the decompile, not from running the game. Also unobserved: real `cells`/`reach`/`breadth` per weapon (shapes come from sprite alpha at runtime; the decompile cannot tell them), whether `stun_gun` (not `MELEE_WEAPON` in the game) is picked up through its `item_stats` melee tag, and assumption A3 (row 0 of `GetLocal` is the sprite's top; it affects only how `shape=` reads).

Also not exercised: `ReadFacts` on a real game item at all (it is game-bound; only `Footprint.Measure` has a test). That the `Assembly-CSharp` members used are public is proved only by the compile.

---

## Objectives Completion

| Objective | Status | Notes |
|---|---|---|
| EF reports six facts on every weapon (SC1) | Complete (offline) | `ReadFacts` after `ApplyStats`; values never observed in game |
| Game-free tested measurement (SC2) | Complete | `TestFootprint`, 5 cases; tests end `All checks passed` |
| Framework-wide debug entry (SC3) | Complete (offline) | Code present and compiled; behaviour unverified in game |
| Compile redirected to scratch (SC4) | Complete | exit 0; live dll untouched |
| NER byte-identical (SC5) | Complete | lint, run, digest identical, base vs new, back to back |
| `main.json` unchanged (SC6) | Complete | not in the diff |
| Docs (SC7) | Complete | items.md, testing.md, packs.md |
| Source hygiene (SC8) | Complete | comments name rejected alternatives; no projex ids, labels, step numbers or line numbers |

---

## Execution Detail

> Derived from git history and the execution log. Five step commits; no code deviation from the plan's text.

### Step 0: Preflight + Baselines
**Planned:** record base, NER state, live dll mtime; build base and new EfSim; run tests.
**Actual:** base `fde196b`; base tree archived to scratch `ef-base`; both EfSim built (exit 0); tests `All checks passed`. Live dll mtime recorded (1794657202).
**Deviation:** None. **Files:** none (scratch only).

### Step 1: Core.Footprint + Test (commit `8324776`)
**Actual:** `src/Core/Footprint.cs` (33 lines, new) with `Measure(width, height, filled)` returning `(Reach, Breadth)`, `(0, 0)` when empty. `tests/Sim/Program.cs`: `TestFootprint()` (straight, upright, diagonal, padded grid, empty) and its call in `Main`.
**Deviation:** None. **Verification:** tests `All checks passed`; `TestFootprint()` occurs twice (definition and call).
**Issue:** the commit has no body (see Deviations).

### Step 2: ItemReader.ReadFacts (commit `196166c`)
**Actual:** `src/Game/ItemReader.cs` +39: `WeaponType`, `FactNames`, `TaggedTypes`, `ReadFacts(weapon, item)` called right after `ApplyStats(content);` in `ReadWeapon`, and `Describe`. `price` is `unitValue` clamped to 0..int.MaxValue; `kinds` excludes WEAPON, MELEE_WEAPON and FIREARM; `purposes` counts `TypeHelper.MAJOR_TYPES_LIST` types except WEAPON.
**Deviation:** None. **Verification:** redirected build exit 0, 0 warnings, 0 errors; the dll contains `ReadFacts`, which proves the game members are accessible.

### Step 3: Debug entry (commit `297f1aa`)
**Actual:** `src/Game/DevTools.cs` +45 (`WeaponDirectories`, `AddFramework`, `LogMeleeFacts`); `src/Game/PackRegistry.cs` +1 (`DevTools.AddFramework(content);` after the per-pack loop).
**Deviation:** None. **Verification:** build exit 0; dll contains `LogMeleeFacts`. Not provable offline: A2 and the in-game behaviour.

### Step 4: Docs (commit `b5f7d46`)
**Actual:** `docs/items.md` +15 (facts table after the capacity_bonus paragraph), `docs/testing.md` (kit weapons take the facts as props), `docs/packs.md` (debug entry). Anchors `items.md#what-the-game-reports` and `packs.md#debug-panel` exist.
**Deviation:** None.

### Step 5: Compile + Parity Proof + Cleanup (commit `90f30ae`, log completion)
**Actual:** redirected compile exit 0; tests pass; base vs new `ef-sim` against the NER sprint worktree (read-only): `lint --strict`, `run --runs 2000`, `run --runs 2000 --digest` byte-identical (64 B, 6125 B, 12448 B). Worktree build output removed.
**Deviation:** see Deviations (first parity pass discarded).

---

## Complete Change Log

> From `git diff --stat main..projex/2611150108-melee-weapon-facts` before this close commit: 10 files, +211 -3.

### Files Created
| File | Purpose | Lines | In Plan? |
|---|---|---|---|
| `src/Core/Footprint.cs` | Game-free box measurement | 33 | Yes |
| `.projex/2611150108-melee-weapon-facts-log.md` | Execution log (moved to closed at close) | 61 | Yes (execute) |

### Files Modified
| File | Changes | Lines Affected | In Plan? |
|---|---|---|---|
| `src/Game/ItemReader.cs` | `ReadFacts`, `Describe`, constants, call in `ReadWeapon` | +39 | Yes |
| `src/Game/DevTools.cs` | `AddFramework`, `LogMeleeFacts`, `WeaponDirectories` | +45 | Yes |
| `src/Game/PackRegistry.cs` | call `DevTools.AddFramework(content)` | +1 | Yes |
| `tests/Sim/Program.cs` | `TestFootprint` and its call | +13 | Yes |
| `docs/items.md` | weapon facts table and notes | +15 | Yes |
| `docs/packs.md` | debug entry sentence | 1 line | Yes |
| `docs/testing.md` | kit weapons take the facts as props | 1 line | Yes |
| `.projex/2611150108-melee-weapon-facts-plan.md` | Status Complete, Log link; at close Completed and Walkthrough; moved to closed | few | Yes |

### Files Deleted
None.

### Planned But Not Changed
None. `ExpeditionFramework/main.json` was planned to stay unchanged and did.

---

## Success Criteria Verification

Evidence is from the execution log and scratch outputs; the close step re-ran tests and the redirected compile.

| Criterion | Method | Result | Evidence |
|---|---|---|---|
| SC1 six facts after `ApplyStats` | `git diff`: `ReadFacts` follows `ApplyStats(content);` in `ReadWeapon`; six names in `FactNames` | Pass (code); values unobserved in game | diff of `ItemReader.cs` |
| SC2 Core measure + `TestFootprint` | `dotnet run -c Release --project tests/Sim`, re-run at close | Pass | `All checks passed`, exit 0 |
| SC3 debug entry and log format | diff of `DevTools.cs`; dll contains `LogMeleeFacts` | Pass (code only); not exercised | format `Melee facts: id=... cells=... reach=... breadth=... price=... kinds=... purposes=... width=... height=... shape=... types=... name=...` via `Describe` |
| SC4 compile to scratch | `dotnet build ExpeditionFramework.csproj -c Release -p:InstallDir=<scratch>/game-install/ -p:GameManaged=<game>/Probably Stolen_Data/Managed/ -o <scratch>/ef-build`, re-run at close into `ef-build-close` | Pass | exit 0; live `Mods/ExpeditionFramework/ExpeditionFramework.dll` mtime 1794657202 before and after |
| SC5 NER parity | base (`fde196b` copy) vs new `ef-sim`, one at a time, back to back, against the NER sprint worktree | Pass | `lint ok`, `run ok`, `digest ok` in scratch `parity.out` |
| SC6 `main.json` unchanged | not in `git diff --stat`; log: exit 0 | Pass | diff stat above |
| SC7 docs | grep for the six names, entry name, anchors | Pass | Step 4 |
| SC8 hygiene | read the new comments in the diff | Pass | no ids, labels or line numbers; comments name rejected alternatives |

**Overall:** 8/8 offline criteria pass. In-game behaviour is a separate, unmet verification (above).

---

## Deviations from Plan

### Deviation 1: First parity pass discarded
- **Planned:** one back-to-back pass.
- **Actual:** the first pass wrote new-binary outputs from an unquoted path containing a space (`new/*.txt` held an 85 B path error). The whole pass was re-run with quoted argv arrays, base and new interleaved; the first pass's base outputs had identical sizes.
- **Impact:** none on the result. **Recommendation:** plans should show quoted argv arrays for paths under `Probably Stolen`.

### Deviation 2: Step 1 commit has no body
- Step commit lint accepted it; later steps have bodies. The branch forbids `--amend`, so it stays. It is squashed away at close.

### Deviation 3: Co-author line
- Plan Author says Opus 5.5; commits carry the Sonnet 5.5 co-author line as instructed by the caller.

---

## Issues Encountered

- **NER HEAD moved during execution** (454ed39 to be469ec, sprint commits at 01:27-01:28). Severity Low. The porcelain status hash was unchanged and base and new ran back to back on the same state, which the plan accepts.
- **Tool background limit** hit twice on a polling command (180 s). No effect.
- **In-game checks could not run offline.** Severity Medium, accepted by design; listed above.

---

## Key Insights

**Rationale promoted:** none needed. The rejected alternatives (`GetValue()` for price, grid size instead of box, building every game item, counting grade types) live in shipped source comments and `docs/items.md`.

### Lessons Learned
1. **Prove parity with the new props unread.**
   - Context: new props change nothing until a pack reads them.
   - Insight: byte-identical lint, run and digest is the cheap proof.
   - Application: do the same for the next additive prop.
2. **Quote every argv path here.**
   - Context: the game path has a space.
   - Insight: an unquoted `$W` produced a path-error file that looked like output.
   - Application: use arrays in scripts and check output sizes.

### Gotchas / Pitfalls
1. **Facts read 0 in `ef-sim`.** A kit item has no shape or price, so a pack reading `item.cells` sees 0 in sim unless the kit gives the prop (documented in `docs/testing.md`).
2. **An `item_stats` key named like a fact is overwritten in game.** None exist today; no lint covers it.

### Technical Insights
- `GridShape` width and height are slice sizes, not trimmed, so a bounding box of the filled cells is needed.
- `DirectoryMaster.Create<GameItem>` is the raw factory: no unique id, ownership or permit hook.

---

## Recommendations

### Immediate Follow-ups
- [ ] Deploy (the human's step, "deploy later"), launch once, press F9, run the entry, and read `Player.log` for `Melee facts:` lines.
- [ ] Check the four unverified items above against those lines; if `UnusedDirectory` is not loaded, find `wrench_orange` another way.
- [ ] Write the NER melee-model plan from the measured data; give its kit weapons these props.

### Future Considerations
- Lint warning for `item_stats` keys that shadow fact names.
- Facts on non-weapon stacks when something needs them.

### Plan Improvements
- Show quoted argv arrays for paths with spaces.

---

## Related Projex Updates

| Document | Update |
|---|---|
| 2611150108-melee-weapon-facts-plan.md | Complete; Walkthrough link; moved to closed |
| 2611150108-melee-weapon-facts-log.md | Complete; moved to closed |

| Type | New Projex Suggested |
|---|---|
| Plan (NER) | Unified melee model: formula, curated overrides, fit from the measured facts |

---

## Appendix

### References
- Branch `projex/2611150108-melee-weapon-facts` tip `90f30ae` (base `fde196b`); step commits `8324776`, `196166c`, `297f1aa`, `b5f7d46`, `90f30ae`.
- Scratch evidence: `scratchpad/melee/` (`parity.out`, `ef-build*.log`, `new/`, `base/`, `game-dll-mtime.txt`).
- Landing message style reference: `6f6b6b0`.
- Branch deletion is blocked by the git hook; the ephemeral branch is left in place after close.
