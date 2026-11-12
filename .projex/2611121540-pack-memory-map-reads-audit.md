# Audit: Pack Memory Map-Time Reads

> **Status:** Complete (Verified)
> **Author:** Claude Sonnet 5.5
> **Parent:** 2611121537-pack-memory-map-reads-patch.md
> **Sprint:** 2611121459-pack-memory-sprint.md — iteration 2
> **Related Projex:** 2611121531-pack-memory-map-reads-plan.md | 2611121537-pack-memory-map-reads-patch.md | 2611121459-pack-memory-sprint.md | 2611121522-pack-memory-runtime-store-audit.md

---

> **Audit Date:** 2026-11-12 | **Auditor:** Claude Sonnet 5.5 | **Work Period:** 2026-11-12 (iteration 2)
> **Subject:** map-time memory reads — code commit `684b090` (+ patch doc `5bdd5fc`) on `projex/sprint/2611121459-pack-memory`

---

## Audit Summary

**Claim:** `MapContext` reads pack memory (optional `IMemory`, `IMemoryHost`); intel slots gain `when`; `IntelRoller.Roll` takes a scope and rolls nothing for a gated slot; game passes `SaveMemory` / `map.Root`; area unlock by memory = memory-aware `hidden_when` (D1, no new field); all gates green.

**Verdict:** Verified

**Assessment:** Completeness: High | Correctness: High | Quality: High | Value: High

**Top Issues:**
1. (Minor) `hidden_when` ANDs with `unlocked_by` — "chain unlock OR memory" is not expressible; README (iter 5) must state it.
2. (Minor) Test never proves the "gated slot rolls no dice, so other slots' picks don't shift" claim made in the `Roll` doc (test roll is constant `n => 1`).
3. (Informational) Commit trailer `Projex: 2611121545-pack-memory-map-reads` matches no document name (patch doc is `2611121537-…`); fix forward, history not rewritten.

---

## Claims vs Evidence

| Claim | Evidence | Status | Notes |
|-------|----------|--------|-------|
| 7 files changed as planned | `git show 684b090 --stat`; diff read in full | ✓ | `ExpeditionRun.cs`, `Model.cs`, `PackLoader.cs`, `Schema.cs`, `ExpeditionLocation.cs`, `IntelCards.cs`, `tests/Sim/Program.cs`; nothing else |
| `MapContext` takes optional `IMemory`, is `IMemoryHost` | diff: ctor `IMemory memory = null`, `IMemoryHost.Memory => memory` | ✓ | Null default keeps 3 other callers (`tools/EfSim/PackRun.cs`, 2 in `tests/Sim`) unchanged |
| Intel `when` loads, compiles with area pack | `Schema.Intel` + `CompileIntel` `Condition(n["when"], pack, …)` | ✓ | Same call path as `hidden_when`/line `cases` |
| `Roll` skips false-`when` slot, slot stays a key | diff: `result[slot.Id] = picks;` precedes the `continue` | ✓ | `IntelCards.Rolled` reads `picks[slot.Id]` for every slot — still safe |
| Game passes store/scope | diff; V3 grep = 1 + 1 | ✓ | Game diff is exactly 2 call-site lines |
| `TestMapMemory` proves SC1/SC2 | test read; `tests/Sim` re-run | ✓ | `All checks passed`, 0 FAIL |
| Game build 0 warn / 0 err | re-ran `dotnet build -c Release -warnaserror` with the facts-file GameManaged/InstallDir flags | ✓ | 0 warnings, 0 errors (incremental, 0.49 s) |
| lint `--strict` 0/0 both packs | re-ran | ✓ | sample_area and night_expeditions_reborn: 0 error(s), 0 warning(s) |
| `ef-sim run` sample passes | re-ran | ✓ | `ef-sim sample_area: all checks passed` |
| NER 4000-run table identical | re-ran `--runs 4000`, `diff` of `^ne_` rows vs baseline | ✓ | IDENTICAL, 23 rows; plausible a priori — NER has no intel `when`, no `memory.`, ef-sim never rolls intel |
| No new area field | `grep unlocked_when\|UnlockedWhen` over `*.cs` | ✓ | no match |
| Misplaced ammo comment moved | diff | ✓ | Now sits above `TestReloadUsesWholeItems`; `TestMemory` comment alone above it |
| Worktree clean | `git status --short` empty | ✓ | |

---

## Objective Verification

### Objective: map-time memory reads — `MapContext` backed by the host store, an intel-slot condition, an area unlock condition reading `memory.*`, proven by framework sim tests

**Evidence:** diff `684b090`; `TestMapMemory`; gate re-runs above.

**Findings:**
- Actual: `memory.*` resolves on `MapContext` via the existing `Expr.MemoryOf` → `IMemoryHost` route (no `Expr` change needed); `hidden_when`, intel `when`, intel line `cases` all read it; cross-pack isolation tested (`other` store).
- Missing: nothing against the iteration objective. Area unlock = `hidden_when` (D1).
- Quality: High

**Verification:** ✓ Verified

**Issues:** see Decision D1 review and Gap Analysis.

### Decision D1 review — judged against iteration objective and goal wording "read on the map (intel and area-unlock conditions)"

- **Capability test:** "unlocked once `memory.debt_paid >= 1`" = `"hidden_when": "memory.debt_paid < 1"`. Test proves it: area hidden until `unlocked` set, unhidden after, other pack's memory doesn't unlock. So an area-unlock condition that reads memory exists and works.
- **Wording:** the goal says memory is *read* at area-unlock conditions; it does not say a new field. `IsDiscovered` is the unlock decision point and `hidden_when` is a condition evaluated there on the memory-backed `map.Root`. Met.
- **Rejecting a second field:** `unlocked_when` would be `!hidden_when` — more schema/lint/README surface, no new capability. Agree.
- **Real limit (minor):** `IsDiscovered` ANDs `hidden_when` with `unlocked_by`. A pack can gate by memory alone, by clears-chain alone, or by both required; it cannot say "unlocked by clearing X OR by memory". If a content idea needs OR, the pack must drop `unlocked_by` and fold the clears test into the expression (no `clears` read exists until iteration 3's `game.clears`). Not an objective failure; iteration 3 may make it expressible, README should state the AND.
- **Idiom is inverted** (condition says when it is *hidden*) — a documentation burden, already carried to iteration 5.
- **Verdict on D1:** acceptable; objective and goal wording met.

---

## Code/Implementation Inspection

### `src/Core/ExpeditionRun.cs` — `MapContext`, `IMemoryHost`, `IntelRoller.Roll`

**Claimed:** optional store, host interface, scope-gated roll. **Actual:** as claimed — Quality: High

**Issues Found:**
- `Roll` gains a required param: source-breaking for outside C# callers (none known; only `IntelCards.Rolled` in repo) — Severity: Low; plan Risks notes it for README.
- `result[slot.Id] = picks;` ordering before the `continue` is load-bearing (callers index every slot) and uncommented — Severity: Low (see Source Hygiene).

**Undocumented:** none. The `at` → `scope` rename (CS0136 local collision) is documented in the patch doc.

### `src/Core/Model.cs`, `PackLoader.cs`, `Schema.cs`

One field, one loader line, one schema entry (`("when", Field.Any)`); unknown-key warning avoided (lint `--strict` 0/0 on a pack exercised by the test). Quality: High. Patch-pack intel overlay: `grep` shows slots are only built in `CompileIntel`, so no copy path drops `When`.

### `src/Game/ExpeditionLocation.cs`, `IntelCards.cs`

Two call-site edits. `new SaveMemory()` is stateless (goes to `ModHelper` per call), so one per `MapContext` is fine. `IsDiscovered` now makes `ModHelper` mod-data reads when `hidden_when` mentions memory; cost small. Intel `when` evaluates once per game day at roll (`Rolled`), so memory set tonight affects tomorrow's street — intended, stated in plan Risks, README item carried.

**Metrics:** Readability High | Test coverage: `TestMapMemory` covers bare map, hidden/unhidden, gated vs ungated slot, line cases, pack isolation; game layer not coverable | Technical Debt: Low

---

## Testing Validation

**Coverage:** area `hidden_when`, intel `when` false/true, ungated slot unchanged, `cases` quiet/loud, other-pack isolation, clean load (0 problems/warnings).
**Execution:** All pass: Yes (re-run, `All checks passed`) | Flaky: none seen
**Missing:**
- Dice isolation: `Picks` uses `n => 1`, so nothing shows a gated slot consumes no `roll` calls (the property the `Roll` doc credits for "doesn't shift what the others pick"). Regress it silently (roll before the `when` check) and tests stay green.
- `when` with `counts`/`chance` slots not exercised (slot defaults used).
**Quality Issues:** none; assertions carry messages.

---

## Documentation Audit

**Completeness:** User docs deferred to iteration 5 by plan (README) — carry list in plan Notes and patch Notes is complete (intel `when` once per day, `hidden_when` idiom + AND with `unlocked_by`, `MapContext`/`Roll` signatures). Partial, by design.
**Accuracy:** `IMemoryHost` doc updated (old "the map has no store" sentence was made false and was rewritten) — Yes. Patch doc matches diff — Yes.
**Quality:** High

---

## Source Hygiene

**Scope:** Diff only (`git show 684b090 -- . ':(exclude,glob)**/.projex/**'`)

| Rule | Location (symbol) | Quote | Severity |
|------|-------------------|-------|----------|
| — | — | no rule 1–5 violation found | — |

Swept added comments: `IMemoryHost` doc, `MapContext` doc, `IntelRoller.Roll` doc, `IntelSlot.When` doc, `TestMapMemory` comment. No projex ID/filename/label, no `file:ln`, no changelog wording ("no longer"/"used to"; V7 grep empty and re-run), no `Step N`/banners, no unwarned "deliberate". Present tense throughout. Mechanical V7/V8 greps re-run: no matches.

**Rule 6 gaps** (non-obvious change, no rationale comment):
- `IntelRoller` → `Roll`, line `result[slot.Id] = picks;` before the `when` `continue` — the key must exist even for gated/poolless slots because `IntelCards.Rolled` indexes `picks[slot.Id]` for every slot; a tidy-up moving it below the `continue` breaks the game with a `KeyNotFoundException` that no framework test would catch. Pre-existing ordering, but the new early-continue makes it newly fragile. Low.

**Rule 6 vacuity:** none. `Roll` doc names the rejected alternative (`canStart`-time check: runs every check, card could vanish mid-day) and the dice-isolation rationale; `MapContext` doc states the without-store behavior; `IMemoryHost` doc keeps the "rejected: put it on `IRun`" rationale.

**Rationale promotion:** patch doc carries a "Carry-forward for iteration 5 README" note instead of a `Rationale promoted:` line; no shipped doc yet names intel `when` timing or the `hidden_when` unlock idiom. The rationale that matters for code readers (roll-time gating) is in the code comment; the pack-author-facing idiom is unpromoted until iteration 5 — acceptable because the plan schedules README there. Re-check at iteration 5.

**Commit composition** (informational — fix forward, never rewrite history): 1 landed non-doc commit checked (`684b090`) — typed subject `feat(map): …` [1/1] | type matches diff (new capability) [1/1] | `Projex:` trailer present [1/1] but names `2611121545-pack-memory-map-reads`, which matches no document (patch is `2611121537-pack-memory-map-reads-patch.md`; timestamp guessed ahead of `new-projex`, patch Notes admit it, hook forbids amend). Resolvable by name stem `pack-memory-map-reads`.
**Violations:** `684b090` — trailer timestamp does not resolve to a filename; stem does.

**Trailer survival on `main`:** `git log main -n 30` returns only 8 commits (short history); 2/8 carry a `Projex:` trailer (`68d0280`, `3186508`) — channel alive; low rate reflects pre-convention history, not a dead channel. Sprint branch code commits: 2/2 carry it (`1d7404e`, `684b090`).

---

## Gap Analysis

### Promised But Not Delivered
| Promise | Status | Impact |
|---------|--------|--------|
| (none against plan SC1–SC4) | — | — |

### Undocumented Issues
| Issue | Severity | Affects |
|-------|----------|---------|
| Dice-isolation property untested | Low | `IntelRoller.Roll` regressions |
| `result[slot.Id]` ordering uncommented | Low | future edits to `Roll` |
| Trailer id mismatch | Informational | commit→doc lookup by exact id |

### Unhandled Edge Cases
- "Unlocked by clears OR memory" not expressible (AND semantics) — Impact: content-design limit, README note; may be revisited with `game.clears`.
- Memory-gated area hidden during an in-progress state (already discovered, then `hidden_when` turns true) simply returns `IsDiscovered` false; existing behavior of `hidden_when`, not introduced here.
- `tools/EfSim/PackRun.cs` `MapContext` is store-less: a sim pack whose `hidden_when`/intel reads memory evaluates it as 0; plan assigns ef-sim memory to iteration 4.

---

## Quality Assessment

### Completeness: High
**Strengths:** every planned step and SC done; misplaced comment fixed in passing.
**Gaps:** README (scheduled), dice-isolation test.

### Correctness: High
**Works:** map memory reads: Yes | gated slot empty, ungated unchanged: Yes | per-pack isolation on map: Yes | existing callers unchanged: Yes
**Bugs:** none found.

### Code Quality: High
**Positive:** minimal footprint (one ctor param, one field, one guard); reuses `Expr.MemoryOf` route with zero `Expr` change; rationale comments name rejected alternatives.
**Concerns:** Low items above.
**Tech Debt:** Low — required `Roll` param; none else.

### Value Delivered: High
**Intended:** packs can condition areas and street talk on remembered events.
**Actual:** exactly that, from JSON only. **Impact:** User: Positive.

---

## Open Findings

### Undocumented Discoveries
- Changes: none beyond the plan (comment relocation in `tests/Sim/Program.cs`, documented).
- Workarounds: `scope` rename — acceptable, documented.

### Impact Analysis
- Downstream: iteration 4 lint "memory read but never written" must scan `hidden_when` and intel `when` too (new read sites). Iteration 4 `PackRun.cs` store-less map. Iteration 3 `game.*` heads will be readable on the map through `GameValues.Get`.
- Future: Enabled: memory-gated areas and rumors | Blocked: nothing.
- Risks: none new; in-game persistence and timing unverifiable (sprint-wide caveat).

### Improvements
- Add a `TestMapMemory` assertion counting `roll` calls (gated slot → 0 calls).
- One-line comment on the `result[slot.Id]` ordering.

---

## Stakeholder Impact

| Stakeholder | Promised | Reality | Impact |
|-------------|----------|---------|--------|
| Pack author | memory-conditioned areas and intel from JSON | delivered (`hidden_when`, intel `when`, line `cases`) | Positive |
| C# pack author | stable API | `Roll` signature gains a param; `MapContext` ctor additive | Neutral |
| Sprint | iteration 2 objective | met | Positive |

---

## Findings

### Critical (Must Address)
- none

### Significant (Should Address)
- none

### Minor (Nice to Fix)
- **Dice-isolation untested** → in iteration 4/5 or a later patch, assert a gated slot makes zero `roll` calls.
- **`result[slot.Id]` ordering uncommented** → one-line rationale comment (keys must exist for every slot).
- **`hidden_when` AND `unlocked_by`** → README states it; revisit OR-need after `game.clears`.
- **Iteration 4 lint scope** → include `hidden_when` and intel `when` as memory read sites.
- **Trailer id mismatch (informational)** → fix forward; none.

### Positive
- Smallest change that satisfies the objective; D1 avoided a redundant schema field with the capability proven by test.
- All gates independently reproduced green, NER 4000-run table identical.

---

## Recommendations

**Immediate:** none blocking.
**Future:** carry the four Minor items into iterations 4–5 derive.
**Process:** run `new-projex` before the code commit so the `Projex:` trailer can name the real document.

---

## Final Verdict

**Status:** Accept

**Overall Assessment:**
- Completeness: High
- Correctness: High
- Quality: High
- Value: High

**Conditions:**
- [ ] none

**Sign-off:** Yes — iteration 2 objective and plan SC1–SC4 met; D1 (no `unlocked_when`) satisfies the area-unlock wording through memory-aware `hidden_when`, with the AND-with-`unlocked_by` limit to document.
