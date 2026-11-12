# Patch: Cross-Pack Memory Reads

> **Status:** Complete
> **Author:** Claude Opus 5.5
> **Parent:** 2611121459-pack-memory-sprint.md
> **Directive:** Keep pack memory separated but allow explicit cross-pack reads: `memory.NAME` = own pack; `memory.PACK.NAME` / `memory.PACK.$NAME` = read PACK's value; cross-pack writes are load errors; unknown PACK is a load error; never-written warning extends across packs; `ef-sim` `memory.start`/`expect` accept `PACK.NAME`; README documents it. Out of scope: `game.clears.PACK.AREA`.
> **Source Plan:** Direct (follow-up to 2611121459-pack-memory-sprint.md)
> **Result:** Success
> **Related Projex:** 2611121459-pack-memory-sprint.md | 2611121622-pack-memory-sim-memory-patch.md | 2611121516-pack-memory-runtime-store-patch.md

---

## Summary

`memory.` paths now take one or two segments. One segment (`memory.NAME`) = authoring pack, unchanged. Two (`memory.PACK.NAME`, `memory.PACK.$NAME`) = read pack PACK's value from the same store (`IMemory.Get/GetText(PACK, NAME)`). Two-segment paths have no writer; `set`/`add`/`set_text` on one → load problem with dedicated message. Dots inside own names no longer valid (three segments → unknown path). `PackLoader.Finish` now tracks reads per (reader, owner, name): unknown owner → problem; owner never writes NAME → warning naming that pack. `ef-sim` loads only framework + one pack, so a new `OtherPacks` stand-in set (PackLoader/PackSet) lets packs named in `sim.json` memory keys (and lint `--mod`) count as installed; reads of stand-ins never warn.

Code commit: `6d56e0b`.

---

## Changes

### Path grammar

**File:** `src/Core/Expr.cs`
**Change Type:** Modified
**What Changed:**
- `MemoryKey(rest, ownPack, out pack, out name)` (L115): single parser for what follows `memory.`; rejects empty parts, `$`-prefixed pack, third segment, null own pack.
- `IsPackMemory(path)` (L128): true for two-segment memory paths; Builtins uses it for the write error.
- `Reader` (L146) / `Writer` (L172) / `TextWriter` (dot-free name) / `TextReader` (L215, memory handled up front since generic split rejects `PACK.$NAME` field): all route through `MemoryKey`. Removed `IsMemoryNumber`.

**Why:** One grammar shared by runtime, loader bookkeeping and ef-sim key parsing.

### Load checks

**File:** `src/Core/PackLoader.cs`, `src/Core/PackSet.cs`, `src/Core/Builtins.cs`, `src/Core/Model.cs`
**Change Type:** Modified
**What Changed:**
- PackLoader: `packs` set filled in `Add` (L202); `OtherPacks` property (L169); `memoryReads` keyed (Reader, Owner, Name) → (At, Path); `Finish` (≈L583-597) emits `"<path> reads pack <P>, which isn't loaded"` problem or `"<path> is read but never written by pack <P>, so it always reads 0|\"\""` warning (own pack keeps `by this pack`); `NoteMemory` (L662) parses via `Expr.MemoryKey`.
- PackSet: `OtherPacks` passthrough to each attempt's loader.
- Builtins: `set`/`add` (`Assign`) and `set_text` fail early on `IsPackMemory` with `ReadOnlyMemory` message (`"... can't be changed; a pack changes its own memory as memory.NAME"`).
- Model: `IMemory` doc states cross-pack read-only.

**Why:** Directive's load-time rules; unknown-pack check deferred to `Finish` so load order doesn't matter.

### ef-sim

**File:** `tools/EfSim/SimConfig.cs`, `tools/EfSim/PackRun.cs`, `tools/EfSim/Lint.cs`
**Change Type:** Modified
**What Changed:**
- SimConfig: `SimMemory.Numbers/Texts` keyed (Pack?, Name); `SimMemoryCheck` gains `Pack`, `Key`; `Packs` (L30) lists other packs named; `MemoryName` (L269) replaces `IsTextName`, parses via `Expr.MemoryKey`, rejects >2 segments (`a memory name is NAME or PACK.NAME ...`).
- PackRun: collects row memory packs → `Lint.Load(..., memoryPacks)` for main load and loot reloads.
- Lint: `Load` takes optional `memoryPacks`; `OtherPacks = mods ∪ memoryPacks`, so `lint --mod X` also accepts `memory.X.NAME`.

**Why:** ef-sim loads one pack; without stand-ins any cross-pack read would fail its load, making `PACK.NAME` seeding useless.

### Tests + docs

**File:** `tests/Sim/Program.cs`, `README.md`
**Change Type:** Modified
**What Changed:**
- `TestPackMemory` (L687): cross-pack number/text/line reads return pack a's values (71 = 7*10+1, `hi_you`); store keeps a=7, b=1 (own untouched); `set`/`add`/`set_text` on `memory.a.*` and `set` on `memory.t.x` (own pack, two segments) → one problem each; `memory.nobody.x` (expr and line piece) → unloaded-pack problem; `memory.a.ghost` → one cross-pack warning, `memory.b.visits` none; `OtherPacks` stand-in → no problem/warning; `memory.a.b.c` and pack-less `memory.a.x` don't compile.
- README "Memory across nights": no dots in names; cross-pack read bullet + example; cross-pack warning text. Lint paragraph: `--mod` also allows `memory.MOD.NAME`. ef-sim Memory: `start` example `other_pack.visits`, stand-in paragraph, >1-dot names are errors.

---

## Verification

**Method:** gates from directive, plus throwaway scratch probe (copy of SampleArea reading `memory.other.visits` / `memory.other.$name`, seeded via `sim.json`).

**Result:**
```
tests/Sim: All checks passed
lint sample_area --strict: 0 error(s), 0 warning(s)
lint night_expeditions_reborn --strict: 0 error(s), 0 warning(s)
ef-sim run sample_area: all checks passed
ef-sim run night_expeditions_reborn: all checks passed
ef-sim run NER --runs 4000: all checks passed; 23 ^ne_ rows identical to pre-sprint baseline
game assembly (GameManaged=<game Managed>, InstallDir=<scratch>, --no-incremental): 0 warnings, 0 errors
probe run --runs 50: all checks passed (other.visits 5, other.$name "Dex", copied >= 10)
probe expect other.visits 6 / other.$name "Rex": 2 FAILs as expected
probe expect key a.b.c: ERROR sim.json ... a memory name is NAME or PACK.NAME
probe lint (no --mod): 2 errors "memory.other.* reads pack other, which isn't loaded"; with --mod other --strict: 0/0
```

**Status:** PASS

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611121459-pack-memory-sprint.md | Parent (closed sprint) | None — closed record; this patch extends its result |

---

## Notes

- Decisions beyond directive: (1) `OtherPacks` stand-in set for ef-sim — packs named in `sim.json` memory keys and lint `--mod` ids count as installed; their reads never warn (writes unknown). (2) `memory.OWNPACK.NAME` is a legal read of own pack but, being two-segment, not writable. (3) Unknown-pack problem and cross warnings reported once per (reader, owner, name), at first read, like existing warning.
- In game a pack reading `memory.X.*` fails to load when X isn't installed (directive: load error). **Superseded** by 2611122012-optional-cross-pack-memory-patch.md: an unloaded pack now reads 0/"" and warns. Pack ids containing `.` can't be addressed; none seen (all `<ID>`s are `a-z_`).
- Not verified in-game.
