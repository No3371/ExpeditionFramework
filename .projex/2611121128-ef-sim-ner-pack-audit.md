# Audit: ef-sim-ner-pack

> **Status:** Complete (Verified)
> **Author:** Claude Sonnet 5.5 (auditor)
> **Parent:** 2611121121-ef-sim-ner-pack-patch.md
> **Sprint:** 2611121010-pack-defined-sims-sprint.md — iteration 3
> **Related Projex:** 2611121110-ef-sim-ner-pack-plan.md | 2611121121-ef-sim-ner-pack-patch.md | 2611121010-pack-defined-sims-sprint.md

---

## Audit Summary

**Audit Date:** 2026-11-12 | **Subject:** commits `853846b`, `fdbf139` + NER `sim.json` (outside repo, unversioned)

**Claim:** `sim.json` rows take a kit list cycled by run index; `expect.packing` checks `MapContext.Missing`; NER gets a `sim.json` faithfully expressing its C# sim; `ef-sim run` on NER exits 0.

**Verdict:** Verified

**Assessment:** Completeness: High | Correctness: High | Quality: High | Value: High

**Top Issues (all non-blocking):**
1. `KitBridge` lacks NER `FakeBridge` knobs (luck 50 / roll_luck coin, trade, `LoseCarried`, `TakeBack` order) → EfSim balance numbers drift 1–10 pts from NER's C# table. Known, plan-declared out of scope (iteration 4). Drift measured below and shown to come only from `luck`.
2. Non-obvious packing choice (bag items skip `ApplyStats`, unlike kit items) has no code comment — Low.
3. Executor's judgment check was "not independently re-derived"; this audit does re-derive it (below) — no defect found.

---

## Claims vs Evidence

| Claim | Evidence | Status | Notes |
|-------|----------|--------|-------|
| V1 build 0/0 | rebuilt `tools/EfSim` `-warnaserror`: `0 Warning(s)`, `0 Error(s)` | ✓ | |
| V2 sample output unchanged | `exit=0`, sha256 `b1565178…` = baseline | ✓ | |
| V3 NER data pinned | CR-stripped sha256 `36c3c160…` | ✓ | |
| V4 NER run | `exit=0`; 23 rows; `packing: 6 case(s), 0 failed`; 0 FAIL/ERROR; last line `all checks passed`; sha256 `ba138a66…` | ✓ | 12 `never picked` WARNs (careful 6+, greedy rest); plausible — kits without tools never get feed/smokes etc. |
| V5 deterministic | second run `IDENTICAL` | ✓ | |
| V6 8 bad-form probes | all 8 expected `ERROR` lines, `exit=2` | ✓ | |
| V7 packing mismatch | `FAIL expect.packing[2]`/`[5]`, `exit=1`; empty bag ≠ no backpack | ✓ | |
| V8 framework sim | `All checks passed`, exit 0 | ✓ | |
| V9 lint strict | sample and NER `0 error(s), 0 warning(s)` | ✓ | |
| V10 game assembly | `0 Warning(s)`, `0 Error(s)`; InstallDir scratch only | ✓ | |
| V11 README keywords | `1 3 1 2 1 1` | ✓ | |
| V12 clean tree | `git status` clean after commits (apart from this audit) | ✓ | |
| V13 scope | changed vs plan-add commit: `README.md`, `PackRun.cs`, `SimConfig.cs` + the two `.projex` docs | ✓ | |
| V14 NER C# sim untouched | `tests/Sim` hashes `d4732e8e…`, `26ca5458…` unchanged | ✓ | |
| Trailer on code commit | `853846b` carries `Projex: 2611121121-ef-sim-ner-pack` | ✓ | |

---

## Fidelity: NER `sim.json` vs NER C# sim (`tests/Sim/Program.cs`)

Checked by reading both side by side and by behavioural comparison.

**Kits (vs `Handmade`/`Smg`/`Knife`/`Shotgun`, the `random` kit expression, `FakeBridge` ctor)** — ✓ all match: gun props (20/30/65/30/cap 4; 65/50/25/65/cap 8; 85/50/30/75/cap 4; random 40/40/40/40/cap 7, ergonomics 50), tags `firearm weapon in_hand`, `small_pistol_ammo` kind 0, `bandage_item` count = meds, blade `combat_knife` sides 4, knife kit has no gun/ammo, credits 60 (500 with tools). `smg` (4 meds, ne_sump) vs `smg-5med` (ne_annex/foundry) correct; `smg-nomed` keeps a count-0 bandage like C#. Random kits by `n % 3`: 0 → 2 mags/2 meds/no blade, 1 → 3/3/blade, 2 → 4/4/blade + 13 tools ×2 + `small_pistol_ammo_p` (ammo, armor_piercing, kind 1) + 500 credits. Item order matches. `n % kits.Count` with 0-based `n` matches `n % 3`.

**Bots (vs `Plan`)** — ✓ rule-by-rule. `heal` trigger; combat retreat (careful: `hp<=5 || dry && hp*2<=max`; greedy: `hp<=5` only); `read_boss`/`weak_point`; boss_read+weak_flash flashbang; threatened (heavy/aimed_shot) flashbang then coin cover (RNG drawn only when reached, as in C#); weakest switch_target; fallback chain; standoff (reload, feed/smokes, bribe at credits>=200, smoke_slip/sneak when weak, else ambush); event chain including `fit` gating, the `wait` rule on boss room, and `first_except counter`; explore (`reload`, `search`, head_home vs press_on with careful 55/70/shots<=2/boss-next rule and greedy 30/40/shots<=0). The four careful-vs-greedy differences are exactly those in `Plan`. `val.*`/`run.*` semantics verified in `RuleBot.Named` and `ExpeditionRun.Get` (`run.credits` = bridge credits, `run.rooms` = rooms.Count).

**Matrix and seeds (vs `Main`)** — ✓ 23 rows in C# order (3 random, 10 careful, 10 greedy). Seeds equal `areaId.Length*31 + policy.Length` with `areaId` = `night_expeditions_reborn:<area>`: random 998/1029/1091, careful 999/1030/1092, greedy 998/1029/1091. Runs 2000 = C# default.

**`expect.offered`** — ✓ parsed both lists programmatically: 60 vs 60, identical order.

**Packing (vs `CheckPacking`)** — ✓ all six cases, same tags/ids, same expected strings; `bag` omitted = null as in C# `Missing(area, null)`; ordered joined-string comparison as in C#.

**Behavioural confirmation** — ran NER's C# sim (`SIM_RUNS=2000`, All checks passed) and compared with `ef-sim run` NER. Raw EfSim runs sit 1–10 pts lower on cleared and higher on knocked_out, uniformly. A temporary experiment (reverted; not committed) setting `GameValue` luck=50 / roll_luck coin in `KitBridge` brought the table into noise of C#'s (e.g. ne_sump smg careful cleared 92.0 vs 92.0; annex smg-5med greedy 52.7 vs 53.4; foundry smg greedy 44.7 vs 45.0; sump knife greedy 26.7 vs 26.1). So the whole gap is the declared luck knob, not a data transcription error.

**Not carried by `sim.json` (declared out of scope):** `CheckGunsmithLoot`; the `Wounds` table; 800-step guard (EfSim uses 2000); `KitBridge` knobs listed above.

---

## Code/Implementation Inspection

### `tools/EfSim/SimConfig.cs`
Kit string-or-list parsing with located errors; shared `Items` parser for kits and bags; strict unknown-key checks; `expect.packing`. Quality High. Single-string kits behave as before (sample output byte-identical).

### `tools/EfSim/PackRun.cs`
`Play` takes `List<SimKit>`, `kits[n % kits.Count]`; areas keyed by name across rows and packing; `CheckPacking` builds `MapContext` with the run localizer and `name => 0` as C# does. `bag == null` vs empty distinguished (V7). Quality High.

### README
Checks list, kit-list paragraph, `#### Packing` section; V11 keywords present.

**Minor observations:** run table `Kit` column (12 wide) overflows for `random0+random1+random2` (cosmetic, plan-acknowledged). `extracted` counts only non-null outcomes where C# counts every non-knocked run (no effect on NER output).

---

## Source Hygiene

**Scope:** Diff only (`7fcbe10..853846b`, tools + README)

| Rule | Location (symbol) | Quote | Severity |
|------|-------------------|-------|----------|
| — | — | no violations in added comments (no projex IDs, no `file:ln`, no plan shape, no changelog) | — |

**Rule 6 gaps:**
- `PackRun` → `CheckPacking` — bag items are built without `ApplyStats` while `KitBridge` applies stats to kit items; this is a deliberate match with NER `CheckPacking` and a reader would wonder — Low.

**Rule 6 vacuity:** none.

**Rationale promotion:** README covers kit lists and packing; the no-`ApplyStats` choice is in the plan only (`Decisions Taken by Default`) — Low, unpromoted.

**Commit composition** (informational): 2 landed commits — `853846b` typed `feat(ef-sim)` with `Projex:` trailer; `fdbf139` doc-only (exempt). **Violations:** None.

**Trailer survival on `main`:** 2/30 recent commits carry a `Projex:` trailer (5/30 on the sprint branch) — not zero, channel alive; low rate reflects pre-convention history.

---

## Gap Analysis

### Promised But Not Delivered
None against the plan's C1–C7.

### Undocumented Issues
| Issue | Severity | Affects |
|-------|----------|---------|
| `KitBridge.GameValue` returns 0: luck rerolls never fire, so NER balance numbers read low vs C# table | Medium (known; iteration 4) | sprint criterion 2 parity |
| Bag items skip `ApplyStats`, no comment | Low | maintainers |

### Unhandled Edge Cases
- Small `--runs` (<~600) fails `never offered: antitoxin` on NER by chance — documented, committed file uses 2000.

---

## Findings

### Critical
None.

### Significant
None.

### Minor
- **Bridge knob drift** (luck/roll_luck, trade, `LoseCarried`, `TakeBack`) — only `luck` measurably moves outcomes (confirmed experimentally) → iteration 4 per plan; no patch needed now.
- **Missing rationale comment** on bag items skipping `ApplyStats` → one-line comment in `CheckPacking` (small patch, optional).

### Positive
- Plan's pinned data and output reproduced byte-exact; strict reader catches typos with located errors.
- NER `sim.json` is a faithful transcription; independent behavioural comparison against the C# sim corroborates it.

---

## Final Verdict

**Status:** Accept

**Overall Assessment:** Completeness High | Correctness High | Quality High | Value High

**Conditions:** none.

**Sign-off:** Yes — all 14 verifications reproduced; fidelity of NER `sim.json` to the C# sim verified by reading and by measurement. Temporary `KitBridge` experiment reverted (`git checkout`), no leftovers; NER `sim.json` and `tests/Sim` sources unmodified (building NER `tests/Sim` for comparison only touched its gitignored-style `bin/obj`).
