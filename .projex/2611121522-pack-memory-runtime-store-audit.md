# Audit: Pack Memory Run-Time Store

> **Status:** Complete (Accept with Conditions)
> **Author:** Claude Sonnet 5.5 (audit)
> **Parent:** 2611121516-pack-memory-runtime-store-patch.md
> **Sprint:** 2611121459-pack-memory-sprint.md — iteration 1
> **Related Projex:** 2611121506-pack-memory-runtime-store-plan.md | 2611121516-pack-memory-runtime-store-patch.md | 2611121459-pack-memory-sprint.md
> **Audit Date:** 2026-11-12 | **Work Period:** commit 1d7404e on `projex/sprint/2611121459-pack-memory`
> **Subject:** `memory.NAME` / `memory.$NAME` run-time store, plan SC1–SC6 + sprint iteration 1 objective

---

## Audit Summary

**Claim:** Pack-scoped `memory` variable head in run-time expressions, `set`/`add`/`set_text`, `{…}` templates/args; `IMemory` seam via `IBridge.Memory`; `MemoryStore` (sim/tests) and `SaveMemory` (game mod data); all gates green.

**Verdict:** Verified

**Assessment:** Completeness: High | Correctness: High | Quality: High | Value: High

**Top Issues:** (none blocking)
1. `{memory.$x}` in a text-key template (`PackLoader` → `Template`) has no direct test; only the line-`args` path is exercised.
2. `SaveMemory` is unverifiable here (no in-game test); persistence-to-disk timing depends on game save points (known plan risk, README iteration 4).
3. Trailer survival on `main` is 2/8 — pre-sprint commits predate the convention; informational.

---

## Claims vs Evidence

| Claim | Evidence | Status | Notes |
|-------|----------|--------|-------|
| `tests/Sim` passes | rerun: exit 0, 0 `FAIL`, `All checks passed` | ✓ | |
| Game build 0 warn/0 err `-warnaserror` | rerun with scratch `InstallDir`: exit 0, `0 個警告`, `0 個錯誤` | ✓ | nothing deployed to live game |
| Lint `--strict` sample + NER 0/0 | rerun: both `0 error(s), 0 warning(s)`, exit 0 | ✓ | |
| `ef-sim run` sample | `all checks passed` | ✓ | |
| NER 4000-run table identical | `diff` of 23 `ne_` rows vs `ner-baseline-4000.txt`: identical; run `all checks passed` | ✓ | |
| Commit has `Projex:` trailer | `1d7404e`: `Projex: 2611121516-pack-memory-runtime-store` | ✓ | |
| Worktree clean after work | `git status --short` empty before this doc | ✓ | bin/obj ignored |
| No deviations from plan | diff touches exactly the 8 files named in patch doc | ✓ | |

---

## Objective Verification

### SC1 — run-time read/write, int and text
**Evidence:** `Expr.Reader`/`Writer`/`TextWriter`/`TextReader` `memory` cases; `Builtins` `set_text` and `ApplyVar`-style set/add pass `c.PackId`; `TestMemory` (`add memory.visits`, `set run.seen ← memory.visits`, `if when memory.visits >= 2`, `set_text memory.$who`, `run.$who ← memory.$who`, `line hi_{memory.$who}`).
**Verification:** ✓ Verified. Gap: `Template` (text-key `{memory.$x}`) wired in `PackLoader` (`pack` threaded) but not directly tested — Minor.

### SC2 — per pack
**Evidence:** store keyed `(pack, name)`; `TestMemory` asserts pack `a` visits=2, pack `b` visits=100, text `a`=you / `b`="". Pack comes only from authoring context (`c.PackId`, `Compile(..., pack)`); no syntax names another pack.
**Verification:** ✓ Verified.

### SC3 — persistence across runs
**Evidence:** two `ExpeditionRun`s sharing one `MemoryStore` (seen 1 → 2); fresh store → 1.
**Verification:** ✓ Verified.

### SC4 — load-time problems
**Evidence:** `ProblemsIn` checks: `set`/`add` on `memory.$x` = 1 problem each; `set_text` on `memory.x` = 1; `Expr.Compile("memory.x")` packless errors; with pack compiles.
**Verification:** ✓ Verified.

### SC5 — game store
**Evidence:** `SaveMemory` uses `ModHelper.GetModDataInt/SetModDataInt` and `GetModData/SetModData` with `ExpeditionRun.FrameworkPack` (`expedition_framework`), key `memory:<pack>:<name>` / `memory:<pack>:$<name>`. Decompiled `ModHelper`: `SetModData`/`GetModData` write `PlayerStore.Instance.ModData()` keyed `modId:key`, no-op with a warning when no save is loaded; fallback honoured (`""`). Int helpers compile against the game assembly.
**Verification:** ✓ Verified statically; ⚠ not exercised in-game (cannot be).

### SC6 — gates
**Verification:** ✓ all rerun above.

### Sprint iteration 1 objective
Objective (sprint nav `## Current Position`, item 1): `memory.*` in run-time expressions and effects with a per-pack host store. **Met.** Out-of-scope items (map-time reads, `game.day`/`game.clears`, sim.json memory, lint warning, sample/README) correctly untouched; map `IRun` reads 0/"" and drops writes as the plan states.

---

## Code/Implementation Inspection

### `src/Core/Expr.cs`
**Claimed:** optional trailing `pack`, `memory` case in each entry point, numeric `memory.$x` rejected. **Actual:** as claimed — Quality: High.
**Issues Found:** none. `MemoryOf` degrades to 0/"" when `IRun` isn't an `IMemoryHost` (map, external `IRun`) rather than throwing — sound.
**Undocumented:** error text and doc comment list `memory.`; `Builtins` error hints mention `memory.` — benign.

### `src/Core/Model.cs` / `ExpeditionRun.cs`
`IBridge.Memory` is an interface-member addition: **breaking for any external `IBridge` implementer** (sibling mods). Repo-internal implementers (GameBridge, KitBridge, FakeBridge) all updated; build proves it. `IMemoryHost` internal, off public `IRun`. Quality: High.

### `src/Game/GameBridge.cs`
`SaveMemory` is stateless, key scheme as specified, `$` prefix for text keys prevents `memory.x`/`memory.$x` collision. Pack ids containing `:` could alias keys (`a:b`+`c` vs `a`+`b:c`) — theoretical; pack ids are folder-like names. Low.

### `tools/EfSim/KitBridge.cs`
Fresh store per bridge; comment states memory never carries between runs (iteration 3 changes this).

**Metrics:** Readability High | Test coverage: SC1–SC4 directly, SC5 not testable | Tech debt: None/Low

---

## Testing Validation

**Execution:** all pass; no flaky tests seen. **Missing:** text-key template `{memory.$x}`; `memory` read from `MapContext` (0/"" and dropped write) has no test; `add` on a `memory.x` through `Reader(raw:true)` implicitly covered by `add memory.visits`.
**Quality:** tests assert specific values with diagnostic messages; early-return on compile problems avoids cascade.

---

## Documentation Audit

README intentionally deferred to iteration 4 (plan Out of Scope). In-code `///` docs accurate and present-tense. Walkthrough/patch doc accurate vs diff.

---

## Source Hygiene

**Scope:** Diff only (`git diff 1d7404e~1 1d7404e -- . ':(exclude,glob)**/.projex/**'`).

| Rule | Location (symbol) | Quote | Severity |
|------|-------------------|-------|----------|
| — | none found | — | — |

Added-comment scan: no projex ID/filename/label (`PU`/`SC`/`step`/`finding`), no `file:ln`, no `====`/`----` banners, no changelog narration.

**Rule 6 gaps:** none. Non-obvious decisions all commented: `IMemoryHost` (rejected: putting store on public `IRun`), `Expr` `IsMemoryNumber` (`$` name would collide with the text key), `SaveMemory` (rejected: mod data folder — shared across saves).

**Rule 6 vacuity:** none. `TestMemory` header comment ("belongs to the pack that wrote the data and lives in the bridge's store, so it outlasts a run") states the property under test; acceptable. `KitBridge.Memory` comment names the consequence (no carry between runs).

**Rationale promotion:** patch doc records none-needed; supported — rationale is in shipped comments; README documentation scheduled iteration 4.

**Commit composition** (informational): 1 landed source commit checked — typed subject `feat(memory): …` [1/1] | type matches diff (new feature) [1/1] | `Projex:` trailer [1/1]. **Violations:** None. (`2b5289b`, `8d3ddf5` are doc-only, exempt.)

**Trailer survival on `main`:** 2/8 commits (only 8 exist; 30 requested) carry a `Projex:` trailer — history predates the convention; the sprint branch commit carries it. Not Critical: channel works where the convention is in force. OK.

---

## Gap Analysis

### Promised But Not Delivered
None for iteration 1.

### Undocumented Issues
| Issue | Severity | Affects |
|-------|----------|---------|
| `IBridge.Memory` is a breaking interface addition for external implementers | Low | sibling mods implementing `IBridge` (none known) |
| `{memory.$x}` template path untested | Low | `PackLoader` `Template` |

### Unhandled Edge Cases
- `:` in a pack id could alias save keys — Impact: negligible.
- `memory.*` on the map silently reads 0/"" — Impact: intended until iteration 2.

---

## Quality Assessment

**Completeness:** High — all SC met. **Correctness:** High — gates independently reproduced. **Code Quality:** High — small, seam-based, no shortcuts/TODOs. **Value:** High — unblocks sprint iterations 2–4.

---

## Open Findings

- Downstream: iteration 3 needs `KitBridge` to share a store across runs (currently per-bridge; check `KitBridge` lifetime). Iteration 2 needs map-time store access: `IMemoryHost` is only on `ExpeditionRun`, so `MapContext` needs it too.
- Risk: in-game persistence untestable — Likelihood Medium that a save-point nuance surprises; mitigated by README note.

---

## Findings

### Critical (Must Address)
None.

### Significant (Should Address)
None.

### Minor (Nice to Fix)
- **`{memory.$x}` text-key template has no test** → add one case to `TestMemory` (could fold into a later iteration).
- **`IBridge` breaking addition** → mention in README/changelog when iteration 4 documents memory.

### Positive
- Plan implemented verbatim; every gate reproduced independently; rationale comments name rejected alternatives; scoping disciplined (out-of-scope items untouched).

---

## Recommendations

**Immediate:** none. **Future:** carry the template test and the `IBridge` note into later iterations. **Process:** none.

---

## Final Verdict

**Status:** Accept with Conditions

**Conditions:**
- [ ] (non-blocking) template `{memory.$x}` test and `IBridge` note picked up by a later iteration

**Sign-off:** Yes — all SC1–SC6 and the iteration objective verified; conditions are minor and non-blocking.
