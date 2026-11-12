# Audit: pack-memory-game-reads

> **Status:** Complete (Verified)
> **Author:** Claude Sonnet 5.5 (audit)
> **Parent:** 2611121556-pack-memory-game-reads-patch.md
> **Sprint:** 2611121459-pack-memory-sprint.md — iteration 3
> **Related Projex:** 2611121550-pack-memory-game-reads-plan.md | 2611121556-pack-memory-game-reads-patch.md | 2611121459-pack-memory-sprint.md
> **Audit Date:** 2026-11-12 | **Work Period:** 2026-11-12 15:50-15:56
> **Subject:** commit `4cb0e4d` (`feat(game): game.day and game.clears reads for packs`), 4 files

---

## Audit Summary

**Claim:** Packs can read `game.day`, `game.clears`, `game.clears.AREA` at run time and on the map; all plan gates green.

**Verdict:** Verified

**Assessment:** Completeness: High | Correctness: High | Quality: High | Value: High

**Top Issues:**
1. Game-layer reads (`day`, `Clears`) compile-checked and decompile-checked only; never run in-game (known, unfixable here).
2. `ClearsArea` with an unqualified area id (no `:`) is untested (Low; ids are always qualified).
3. `game.clears` does not count the in-progress run (documented limit, README deferred to iteration 5).

---

## Claims vs Evidence

| Claim | Evidence | Status | Notes |
|-------|----------|--------|-------|
| `tests/Sim` passes incl. `TestGameReads` | Re-ran: `All checks passed` | ✓ | |
| Game-assembly build 0 warn/0 err `-warnaserror` | Re-ran with scratch InstallDir: 0 warnings, 0 errors | ✓ | Nothing deployed to live game |
| lint --strict sample 0/0 | Re-ran: `lint sample_area: 0 error(s), 0 warning(s)` | ✓ | NER not re-run: no change under tools/ or NER; executor logged 0/0 |
| ef-sim run sample passes | Re-ran: `ef-sim sample_area: all checks passed` | ✓ | |
| NER 4000-run table unchanged | Not re-run; `git diff --stat 09f25ed..4cb0e4d -- tools samples README.md` empty and NER uses no `game.clears`/`game.day` | ⚠ | Inferred, structurally safe |
| Nothing under tools/samples/README changed | Diff stat: only 4 files (Model.cs, ExpeditionLocation.cs, GameBridge.cs, tests/Sim/Program.cs) | ✓ | |
| `day` = `StoreStation.GetDayCounter()` null-safe | Decompile: static, returns 0 when `Instance == null` | ✓ | |
| `Clears` reads `afterhourState` without creating entries | Decompile: `afterhourState` public `Dictionary<string, AfterhourLocationState>`; `GetAfterhourState` inserts a new entry when missing | ✓ | `TryGetValue` path creates none |
| `GetCounter(key, fallback=0)` | Decompile of `AfterhourLocationState` | ✓ | |

---

## Objective Verification

### SC1 `GameReads.ClearsArea`
**Evidence:** `GameReads` in `src/Core/Model.cs`; `TestGameReads`.
- `clears` → areaId; `clears.vault` + `a:cellar` → `a:vault`; `clears.`, `clearsvault`, `day`, null → null. Length guard `name.Length <= "clears".Length + 1` rejects `clears.`; null areaId returns null.
**Verification:** ✓ Verified

### SC2 OR unlock via `hidden_when`
**Evidence:** `TestGameReads` compiles a pack with `hidden_when: "game.clears.vault < 1 && memory.key < 1"`; asserts clean load, host sees `clears.vault`, hidden/unhidden by clear, by memory.
**Verification:** ✓ Verified (test is meaningful: three states, host-name capture, both arms)

### SC3 Game wiring
**Evidence:** `GameValues.Get(name, areaId)` checks `GameReads.ClearsArea` first, then `day`; `GameValues.Clears` uses `HasInstance`, null dict, `TryGetValue`, null state guard; `LocationId(areaId)` keys match `Leave`'s write (`GetAfterhourState(Id)` where `Id => LocationId(area.Id)`) and `ClearsCounter`. `GameBridge` takes `AreaDef` (`pack = area.Pack`, `areaId = area.Id`); the only `new GameBridge` site updated; map lambda passes `area.Id`; `IsDiscovered` uses `GameValues.Clears(area.UnlockedBy)`. No other callers of `GameValues.Get` remain (grep).
**Verification:** ✓ Verified

### SC4 `IBridge.GameValue` doc
Names luck, roll_luck, wounds, maimed, day, clears / clears.AREA. ✓ Verified

### SC5 Gates
See Claims table. ✓ (NER table inferred, see above)

---

## Code/Implementation Inspection

**Claimed:** 4 files, minimal. **Actual:** matches; no undocumented changes. Quality: High.
- `IsDiscovered` behaviour change: previously created an empty afterhour state on read; now none. Documented in plan; benign.
- `day` returns the saved counter, 0 outside a loaded save (README note carried to iteration 5).

**Metrics:** Readability High | Tech debt None.

---

## Testing Validation

**Coverage:** name grammar, loader acceptance, host-name plumbing, OR unlock. **Missing:** `GameValues.*` (game types, untestable here); unqualified area id in `ClearsArea`. **Quality issues:** none.

---

## Documentation Audit

README deferred by plan to iteration 5 (in scope there). `IBridge.GameValue` doc comment present and accurate.

---

## Source Hygiene

**Scope:** `git show 4cb0e4d -- . ':(exclude,glob)**/.projex/**'` (diff only)

| Rule | Location (symbol) | Quote | Severity |
|------|-------------------|-------|----------|
| — | none found | — | — |

All added comments (`IBridge.GameValue`, `GameReads`, `GameValues`, `GameValues.Clears`, `TestGameReads`) are present tense with no projex ids, plan labels, `file:ln`, or plan shape.

**Rule 6 gaps:** none. The non-obvious choice (read `afterhourState` directly) carries a rationale naming the rejected `GetAfterhourState` and its consequence (junk save entries). The `unlocked_by` ANDed with `hidden_when` constraint is stated in `GameReads`.

**Rule 6 vacuity:** none.

**Rationale promotion:** none yet needed for code; the `hidden_when` OR idiom and "clears counts earlier nights, not tonight" are README items, explicitly carried to iteration 5 in the patch Notes — acceptable, track there.

**Commit composition** (informational): 1 landed code commit checked — typed subject (`feat(game):`) 1/1 | type matches diff 1/1 | `Projex:` trailer 1/1 (`2611121556-pack-memory-game-reads`). Doc commits exempt.
**Violations:** None.

**Trailer survival on `main`/branch history:** 6/30 of recent commits carry a `Projex:` trailer. The remainder are doc-only/projex commits and pre-convention commits; every code commit in this sprint carries it — OK.

---

## Gap Analysis

### Promised But Not Delivered
None.

### Undocumented Issues
| Issue | Severity | Affects |
|-------|----------|---------|
| `ClearsArea` unqualified-areaId branch untested | Low | tests |
| In-game behaviour unverified | Medium (inherent) | game layer |

### Unhandled Edge Cases
- `game.clears.AREA` naming a nonexistent area silently reads 0 (by design; iteration 4 may lint it).
- Cross-pack clears unsupported (out of scope, `:` not a word char).

---

## Quality Assessment

**Completeness:** High. **Correctness:** High (reads consistent with write key; null-safe; no save mutation on read). **Code Quality:** High. **Value:** High — delivers the goal's "`game.day` and `game.clears` are readable" and unlocks the OR-unlock idiom the iteration 2 audit flagged.

---

## Open Findings

- Downstream: iteration 4 sim/lint needs no change for reads (`bridge.game` keys `day`, `clears`, `clears.AREA`); optional lint of `clears.AREA` ids. Iteration 5 README must cover: `day` = 0 outside a save, clears excludes tonight's run, `hidden_when` OR idiom, plus iteration 2 carry-forward list.
- Improvement: none needed.

---

## Findings

### Critical
None.

### Significant
None.

### Minor
- **No test for unqualified area id in `ClearsArea`** → add one assertion if the helper is touched again.
- **NER 4000-run table not re-run in this audit** → structurally unaffected; iteration 5 runs the full gate set.

### Positive
- Read-without-write design for clears avoids save pollution; keys verified against the write path.
- Test pins the OR-unlock contract end to end.

---

## Recommendations

**Immediate:** none. **Future:** README items above in iteration 5; optional lint of `game.clears.AREA` ids in iteration 4.

---

## Final Verdict

**Status:** Accept

**Overall Assessment:** Completeness High | Correctness High | Quality High | Value High

**Sign-off:** Yes — all success criteria verified by reading and re-running gates; only in-game behaviour is unverifiable here.
