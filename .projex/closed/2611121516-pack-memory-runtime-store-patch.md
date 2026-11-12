# Patch: Pack Memory Run-Time Store

> **Status:** Complete
> **Author:** Claude Sonnet 5.5 (patch)
> **Parent:** 2611121506-pack-memory-runtime-store-plan.md
> **Sprint:** 2611121459-pack-memory-sprint.md — iteration 1
> **Related Projex:** 2611121506-pack-memory-runtime-store-plan.md | 2611121459-pack-memory-sprint.md
> **Directive:** Execute 2611121506-pack-memory-runtime-store-plan.md
> **Source Plan:** 2611121506-pack-memory-runtime-store-plan.md (all 4 steps)
> **Result:** Success

---

## Summary

Added `memory.NAME` (int) and `memory.$NAME` (text) as a pack-scoped variable head in run-time expressions, `set`/`add`/`set_text`, and `{…}` templates/args. Store seam `IMemory` via `IBridge.Memory`; `MemoryStore` (Core) for sim/tests, `SaveMemory` (game mod data, mod id `expedition_framework`, keys `memory:<pack>:<name>` / `memory:<pack>:$<name>`). Plan implemented verbatim; no deviations. Commit 1d7404e.

---

## Changes

### Store seam
**Files:** `src/Core/Model.cs` (Modified), `src/Core/ExpeditionRun.cs` (Modified), `src/Game/GameBridge.cs` (Modified), `tools/EfSim/KitBridge.cs` (Modified), `tests/Sim/Program.cs` (Modified)
- `IBridge.Memory`; new `IMemory`, `MemoryStore`; internal `IMemoryHost` implemented by `ExpeditionRun` (off public `IRun`; map has none → reads 0/"" , writes dropped)
- `GameBridge.Memory = new SaveMemory()`; `KitBridge.Memory` fresh `MemoryStore`; `FakeBridge` takes optional store

### `memory` head
**File:** `src/Core/Expr.cs` (Modified)
- Optional trailing `pack` on `Compile`/`Reader`/`Writer`/`TextWriter`/`TextReader`; `memory` case in each; no pack → path unknown; numeric `memory.$x` rejected; error text lists `memory.`

### Pack threading
**Files:** `src/Core/Builtins.cs`, `src/Core/PackLoader.cs` (Modified)
- `set`/`add`/`set_text` pass `c.PackId`; `Template`, both `TextReader` calls, `ValueOf` pass the authoring `pack`

### Test
**File:** `tests/Sim/Program.cs` — `TestMemory()` covers SC1–SC4.

---

## Verification

| ID | Result |
|----|--------|
| V1 `tests/Sim` | exit 0, `All checks passed`, 0 `FAIL` lines |
| V2 game build `-warnaserror` | exit 0 |
| V3 pack-less call-site grep | `0` |
| V4 lint `--strict` sample + NER | both `0 error(s), 0 warning(s)`, exit 0 |
| V5 `ef-sim run samples/SampleArea` | `ef-sim sample_area: all checks passed` |
| V6 NER 4000-run rows vs baseline | `diff` empty (23 `ne_` rows) |
| V7 added-line hygiene grep | 0 hits |
| V8 existing `memory.` use in samples/NER | 0 files |
| Judgment: `SaveMemory` | uses `ExpeditionRun.FrameworkPack`, `ModHelper` mod data (not folder), keys per SC5; comments name rejected alternatives (public `IRun`, mod data folder, `$` key collision) |

**Status:** PASS (not verifiable in-game)

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611121506-pack-memory-runtime-store-plan.md | Source plan | Marked `[PATCHED]`, Status Complete, moved to `closed/` |
| 2611121459-pack-memory-sprint.md | Sprint nav | Untouched (orchestrator-owned) |

---

## Notes

- Python/shell here ran through a hook that indexes large output; edits done via a scripted replace, then confirmed by `git diff --stat` and V1/V2.
- Persistence to disk depends on the game's own save points (plan Risk); unverified in-game.
