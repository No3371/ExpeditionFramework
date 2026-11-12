# Patch: Optional Cross-Pack Memory

> **Status:** Complete
> **Author:** Claude Opus 5.5
> **Parent:** 2611121850-cross-pack-memory-reads-patch.md
> **Directive:** Reading `memory.PACK.NAME` when PACK isn't loaded reads 0 (`""` for text) and warns instead of failing the load. Cross-pack reads become optional, like Gunsmith-gated loot. Writes to another pack and malformed paths stay errors. Update tests and README ("Memory across nights", lint paragraph). Document `include` + `"requires": ["PACK"]` as the way to skip the warning, after confirming the pack id equals the id `requires`/`HasMod` checks.
> **Source Plan:** Direct (follow-up to 2611121850-cross-pack-memory-reads-patch.md)
> **Result:** Success
> **Related Projex:** 2611121850-cross-pack-memory-reads-patch.md | 2611121459-pack-memory-sprint.md

---

## Summary

`PackLoader.Finish`: a read of a pack that is neither loaded nor a stand-in (`OtherPacks`) now gives a warning instead of a problem: `memory.P.N reads pack P, which isn't loaded, so it reads 0` (or `""`). It is still reported once per (reader, owner, name). Runtime needs no change: every store returns its default for a key never written. Code commit: `c90a611`.

---

## Changes

### Loader

**File:** `src/Core/PackLoader.cs`
**Change Type:** Modified
**What Changed:**
- `Finish` memory loop: `Problem` → `Warn` for an unloaded owner. The message gains `, so it reads 0|""`, and the empty-value text is shared with the never-written warning. The comment gives the reason: failing the load would make the reading pack unusable without the other one installed.
- Field comment: "fails reads" → "about reads" of an unloaded pack.

### Tests

**File:** `tests/Sim/Program.cs`
**Change Type:** Modified
**What Changed:**
- `TestPackMemory`: the two "unloaded pack fails the load" checks are replaced. Pack `t` reads `memory.nobody.x + 5` and the line `hi_{memory.nobody.$x}`. Checks: no problems; exactly 2 warnings (number says `reads 0`, text says `reads ""`); the run gives `run.x == 5` and `run.$g == "hi_"`. The header comment is updated. Write and `a.b.c` checks are unchanged.

### Docs

**File:** `README.md`
**Change Type:** Modified
**What Changed:**
- Cross-pack bullet: writing is a load error. Reading is optional: an unloaded pack reads 0/"" and warns (example text). Putting the reads in an `include` with `"requires": ["PACK"]` loads them only when PACK is installed and enabled, so the warning never fires.
- Lint paragraph: `--mod` counts the mod as an installed pack for `memory.MOD.NAME`. Without it each such read warns, which fails `--strict`.
- ef-sim Memory paragraph: dropped "don't fail it". Stand-in reads are never warned about.

**Why requires works:** the game's `PackRegistry.LoadAll` adds each pack under `mod.Manifest.ID`, and its `HasMod` checks the enabled mods' `Manifest.ID`. Same id. An enabled mod with `main.json` is always added to the loader (even if its own load later reports problems), so `requires: [PACK]` passing means PACK is in the loader's `packs`. In lint, `--mod X` makes `requires` pass and X a stand-in, so it is consistent there too.

---

## Verification

**Method:** gates from the directive, plus a scratch probe (copy of SampleArea with `memory.copied = memory.other.visits + 3`, carried row expecting `copied: 3`, no seeding of `other`). Runtime check: `MemoryStore.Get/GetText` return 0/"" for missing keys. The game's `SaveMemory` uses `ModHelper.GetModDataInt(..., fallback = 0)` (decompiled: returns fallback when the key is missing or doesn't parse) and `GetModData(..., "")`, keyed under the framework's mod id, so the other mod's presence doesn't matter. Nothing in Expr, the runtime or ef-sim checks the owner pack.

**Result:**
```
tests/Sim: All checks passed
lint sample_area --strict: 0 error(s), 0 warning(s)
lint night_expeditions_reborn --strict: 0 error(s), 0 warning(s)
ef-sim run sample_area / night_expeditions_reborn: all checks passed
ef-sim run NER --runs 4000: all checks passed; 23 ^ne_ rows identical to baseline
game assembly (--no-incremental, scratch InstallDir): 0 warnings, 0 errors
probe lint: 0 errors, 1 warning "memory.other.visits reads pack other, which isn't loaded, so it reads 0" (exit 0)
probe lint --strict: exit 1;  --mod other --strict: 0/0, exit 0
probe ef-sim run --runs 50: same WARN, all checks passed (copied == 3, so the read gave 0)
```

**Status:** PASS

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611121850-cross-pack-memory-reads-patch.md | Parent; its unloaded-pack-is-a-problem behavior is superseded | Note added under Notes pointing here |

---

## Notes

- In game, a pack that was installed earlier and wrote memory leaves its values in the save. If it is then removed, reads of it return those stored values, not 0, while the warning still says "reads 0". The warning describes the usual case; nothing clears an uninstalled pack's keys.
- The patch id stamp is 2611122012, matching the code-commit trailer. The scaffold was created as 2611122010 and renamed before commit.
