# Sprint: Pack-Defined Sims

> **Status:** Complete (Goal Reached)
> **Goal:** ExpeditionFramework ships `ef-sim`: one executable that lints and simulates any pack from data the pack carries (`sim.json`: kits, rule-based bots, run matrix, expectations). NightExpeditionsReborn's current C# sim is expressed as such data, and `ef-sim` on NER reproduces every check the C# sim makes plus its 4000-run balance table within ±3 points per cell. Framework sim tests, lint, and the game-assembly build stay green.
> **Base Branch:** main
> **Step:** standard
> **Strategy:** plan<opus>, patch<sonnet>!, audit<sonnet>, [patch<sonnet>] — derive on opus
> **Sprint Branch:** projex/sprint/2611121010-pack-defined-sims
> **Worktree Path:** ExpeditionFramework/.projexwt/2611121010-pack-defined-sims
> **Parent:** 2611120250-framework-expansion-synthesis-eval.md
> **Started:** 2026-11-12 10:10

## Vision

Replaces synthesis Phase 2 "shared Sim.Core" (2611120250-framework-expansion-synthesis-eval.md § Recommended Sequence) with a pack-agnostic sim tool. Human-sanctioned direction (conversation 2026-11-12):

- Framework ships the executable; packs ship data only. One `FakeBridge` in framework; bridge changes touch one place.
- Pack data covers: kits (item ids/tags + gun stats) | bot policies as ordered rules in the framework expression language | area × kit × bot × runs matrix | expectations (options that must be offered, packing cases, balance ranges as regression guards).
- Built-in checks for every pack: load problems | missing text keys | runs that never end | never-offered options.
- Optional C# hook kept for checks data can't express (e.g. NER `CheckGunsmithLoot`: load with/without another mod and compare loot rolls). No pack is required to write C#.
- Self-contained publish so authors without the .NET SDK can run it: desirable, not part of the verifiable goal.

**Done =** all of:
1. `ef-sim` (name may differ) builds from the framework repo; `lint <folder>` still works through it.
2. NER `sim.json` exists; `ef-sim` on NER exits 0 and runs every check the NER C# sim (`NightExpeditionsReborn/tests/Sim/Program.cs`) makes today — text keys, never-offered, packing, Gunsmith loot, run-never-ended guard — by data or via the C# hook.
3. 4000-run balance table (cleared / knocked out per area × kit × policy row) from `ef-sim` matches the NER C# sim's within ±3 percentage points per cell.
4. Framework sim tests pass; lint on NER + sample pack 0 errors/0 warnings; game assembly builds 0 warnings 0 errors.

Retiring NER's C# sim (deleting `tests/Sim`) is the human's call after the sprint, not part of the goal.

## Current Position

**As of 2026-11-12 (iteration 6):**

**Goal reached.** Iterations 1–5 all succeeded (audits Verified). The sprint branch touches no `src/` (`git diff main..HEAD` outside `.projex`: README, `samples/SampleArea/sim.json`, `tests/Sim/Sim.csproj`, `tools/EfSim/*`), so the NER C# sim (built from `main`) remains a valid oracle. Derive re-verified every criterion independently at HEAD `7b635ed`:

1. `tools/EfSim` builds Release `-warnaserror` 0W/0E; `lint` runs through it (framework `tests/Sim` references `tools/EfSim/Lint.cs`).
2. `ef-sim run NightExpeditionsReborn --runs 4000` exit 0: `packing: 6 case(s), 0 failed`, `loot: 2 case(s), 0 failed` (Gunsmith), `all checks passed` — text keys, never-offered, packing, Gunsmith loot, run-never-ended guard all covered by data; no C# hook needed.
3. Parity: NER C# `SIM_RUNS=4000 dotnet run -c Release --project tests/Sim` (exit 0, `All checks passed`) vs `ef-sim` 4000 runs: 23 rows each, same area order, cleared + knocked out = 46 cells, max |Δ| 0.0 pts (≤ ±3).
4. Framework `tests/Sim`: `All checks passed`; `lint --strict` NER and `samples/SampleArea`: 0 error(s), 0 warning(s) each; game assembly `ExpeditionFramework.csproj` Release `--no-incremental -warnaserror` (GameManaged pointed at the game, InstallDir = scratch): 0 warnings, 0 errors.

Not part of the goal and left open for the human: retiring NER `tests/Sim`; self-contained publish; NER `sim.json` lives outside git (NER is not a repo). Next: finalize.

<details><summary>Position as of iteration 5</summary>

**As of 2026-11-12 (iteration 5):**

Iterations 1–4 succeeded (all audits Verified). Criterion 1 met. Criterion 3 met: iteration 4 added `bridge` settings to `sim.json` (game values, trade, take-back order, carried loss), set NER's to mirror its `FakeBridge`, and its audit compared the 4000-run tables of both sims — 46/46 cells Δ 0.0; the follow-up patch (`e92e015`) fixed the one audit finding (`lose_cash_up_to` overflow).

Criterion 2: every NER C# check is covered except **`CheckGunsmithLoot`** (`NightExpeditionsReborn/tests/Sim/Program.cs:107-136`): reload the pack with `HasMod gunsmith_expansion`, fail on its load problems, roll 6 `ne_foundry`-run tables (`ne_supplies`, `ne_drop_scav`, `ne_drop_enforcer_kit`, `ne_arms`, `ne_drop_welder`, `ne_drop_foreman`) ×2000 with seed 7, fail if any `gsx_` id rolls without the mod, fail if any of 7 named `gsx_` ids never rolls with it. It is data-expressible — the needed seam already exists: `PackRun` loads through `Lint.Load(frameworkRoot, folder, id, mods, …)` with an empty mod set today (`tools/EfSim/PackRun.cs:30`), and `Inventory.RollLoot` is reachable from an `ExpeditionRun`. So a `sim.json` expectation (loot tables rolled N times, optionally with extra mods present, ids/prefix that must be absent / must appear) closes it without a C# hook. The C# hook in the Vision stays optional; no NER check needs it. Guard limit 2000 vs C# 800 remains a non-issue (no run hits either; parity shows Δ 0.0).

Criterion 4 was last re-checked by iteration 3's audit; iterations 4–5 touch `tools/EfSim` only, so the iteration-5 audit should re-run the full done-criteria sweep (framework sim tests, lint on NER + sample pack `--strict` 0/0, game-assembly build 0/0, `ef-sim run` on NER and sample exit 0) so the next derive can judge the goal from evidence.

Objective chosen for iteration 5: loot expectations in `sim.json` (tables × rolls, optional extra mods, forbidden prefix / required ids) with NER's `sim.json` carrying the Gunsmith case; `ef-sim run` on NER exits 0 and fails when the case is broken; full criterion-4 sweep in the audit. This is the last uncovered check; if it lands green, the next derive should find the goal reached.

</details>

<details><summary>Position as of iteration 4</summary>

**As of 2026-11-12 (iteration 4):**

Iterations 1–3 succeeded (all audits Verified). Criterion 1 met. NER `sim.json` exists on disk (`NightExpeditionsReborn/sim.json`, unversioned; audit-pinned faithful to `tests/Sim/Program.cs`: kits incl. per-run cycling, careful/greedy rule bots, 23-row matrix with C# seeds, 60 offered ids, 6 packing cases). `ef-sim run` on NER exits 0 with packing 6/6. Criterion 4 re-checked green by iteration 3's audit (V8–V10).

Criterion 2 status: text keys, never-offered, packing, run-never-ended guard, runtime missing-text — covered. **Gunsmith loot check not covered** (no data form, no C# hook). Guard limit 2000 vs C# 800 (only matters if a run hits it).

Criterion 3 status: not met. Iteration 3's audit compared at 2000 runs: `ef-sim` sits 1–10 pts below C# on cleared, uniformly; a reverted experiment giving `KitBridge.GameValue` `luck`=50 / `roll_luck` = `random.Next(2)` (exactly NER `FakeBridge`, Program.cs:351-354) brought cells to within noise. Remaining `KitBridge` vs `FakeBridge` deltas (`KitBridge.cs:53-82` vs Program.cs:346-362): `TakeBack` newest vs oldest; `LoseCarried` rolls per item vs always one `"something"`; `OpenTrade` false vs true with `CloseTrade` cash `-rand(0,30)` and `TakeCart` → `cart_goods`. Those change RNG consumption and outcome paths, so the ±3-pt claim must be measured with them, not assumed. No 4000-run baseline captured yet; NER `sim.json` rows use `runs: 2000`.

Objective chosen for iteration 4: bridge-behaviour settings in `sim.json` (game values such as luck/roll_luck, trade, take-back order, carried-loss) set in NER `sim.json` to match `FakeBridge`; capture the NER C# 4000-run table and the `ef-sim` 4000-run table; every cell within ±3 pts (criterion 3). Sample pack output stays unchanged by default. Gunsmith (criterion 2's last check) follows as iteration 5, then a final green sweep.

</details>

<details><summary>Position as of iteration 3</summary>

**As of 2026-11-12 (iteration 3):**

Iterations 1–2 succeeded (both audits Verified). Criterion 1 met. `sim.json` now carries `version`, `kits` (fixed: credits + items[id,count,tags,props]), `bots` (ordered rules: `when` expression, `pick` id list / `first_except`, `target: weakest`; `val.*` exposes hp, scene, foe intents, focus, run vars, loaded/shots left/credits/has gun, `coin` drawn from the run RNG only when evaluated), `runs` [area,kit,bot,runs,seed], `expect.offered`. `ef-sim` run loop already mirrors NER's (`FinishTrade` when trading, `Hud()` each step, `Wander` with 5% `head_home`, one shared `Random(seed)` for dice, bridge and bot). The iteration-2 plan's `careful` fixture (NER `Plan` as rules) parses; no rule bot has run on NER yet. No NER `sim.json` exists; criteria 2–3 unmet; criterion 4 re-checked green by iteration 2's audit (V8–V10).

Remaining gaps to express NER's C# sim as data (re-surveyed against `NightExpeditionsReborn/tests/Sim/Program.cs`):
- **Per-run kit variation** — the 3 `random` rows build the kit from run index `n` (mags/meds `2+n%3`, capacity 7, stats 40, tool set ×2 + AP ammo and credits 500 on `n%3==2` else 60, no blade on `n%3==0`). Everything has period 3, so a row whose `kit` is a list cycled by run index (`kits[n % len]`) expresses it exactly with three fixed kits. Not supported yet.
- **Packing cases** — `CheckPacking`: 6 hand-built bags against `MapContext.Missing` on `ne_sump`, each with an expected missing-string. No `expect` field for it yet.
- **NER matrix as data** — 3 random rows + careful/greedy × 10 kit rows (handmade/smg/shotgun/knife/smg-nomed; FakeBridge gun props `firepower/accuracy/recoil/ergonomics 50/reliability/capacity`, `small_pistol_ammo` `kind 0`, `combat_knife` `sides 4`), seeds `areaId.Length*31+policy.Length`, 60-id offered list. All expressible with today's schema except the random rows' variation.
- **Bridge behaviour deltas** (`KitBridge` vs NER `FakeBridge`): `luck`=50 and `roll_luck` coin, `OpenTrade` true with `CloseTrade` cash `-rand(0,30)`, `TakeCart` → `cart_goods`, `LoseCarried` returns one item without rolling, `TakeBack` oldest. These move the balance table and gate some runtime text paths (peddler trade `ne_trade_*`); they belong with parity work unless a check cannot pass without one.
- **Gunsmith check** (`CheckGunsmithLoot`: reload with `HasMod gunsmith_expansion`, roll 6 `ne_foundry` tables ×2000, `gsx_` ids absent without / 7 named ids present with) — possibly data-expressible (`expect.loot` with/without a mod) rather than a C# hook; decide when reached.
- **4000-run baseline** — NER C# sim table not captured yet; 800-step guard vs `ef-sim`'s 2000 (only matters if a run hits it).

Objective chosen for iteration 3: NER `sim.json` (decomposition step 3) — the first time `ef-sim` runs NER and the first observable progress on criterion 2. Bridge knobs, Gunsmith and parity follow as steps 4–5.

</details>

<details><summary>Position as of iteration 2</summary>

Iteration 1 landed the skeleton (`84cced3`, audit Verified). Criterion 1 met: `tools/EfSim` (assembly `ef-sim`, net8.0, source-includes `src/Core/**` + `api/*.cs`) builds; `ef-sim lint <folder>` is the moved `Lint.cs` (framework `tests/Sim` now references it); `ef-sim run <folder> [--runs n]` reads strict `sim.json` v1 (`version`, `kits` {credits, items[id,count,tags,props]}, `runs` [area,kit,bot,runs,seed], `expect.offered`), plays rows through a framework `KitBridge`, prints cleared/knocked/extracted per row, fails on load problems, missing text keys, runtime missing text / too-few-args / unfilled labels / unfilled night log, run-never-ended (>2000 steps), no-options, never-offered. `samples/SampleArea/sim.json` exists. Criteria 2–3 unmet; criterion 4 not re-checked since iteration 1's audit.

Gaps between `ef-sim` and the NER C# sim, in the order they block the goal:
- **Bots** — only the built-in `random` bot (`SimConfig.Bots = {"random"}`). NER's balance rows are `careful`/`greedy` (`Plan`, Program.cs:227-282). Those rules need inputs the expression language doesn't reach today: it is numeric only (paths `self/target/player/item/with/run/role/arg/game/val`, enemy aggregates `any_enemy`/`count_enemies`/…), with no reads for scene, offered option ids, foe intent ids (string suffix tests `heavy`/`aimed_shot`), focus vs weakest foe (`switch_target` with a target), or bridge state (loaded rounds, shots left, credits, has gun). `val.NAME` (the `named` resolver of `Expr.Compile`) is the obvious hook for bot-side values. Picks are "first offered of id list" plus a targeted pick and a 50% coin (`random.Next(2)` on the shared run RNG).
- **Kits** — NER's random rows vary the kit per run index (mags/meds `2+n%3`, tool set on `n%3==2`, no blade on `n%3==0`, credits 500 vs 60); `sim.json` v1 kits are fixed.
- **Bridge behaviour that moves the RNG or outcomes** — NER `FakeBridge` differs from `KitBridge`: `GameValue` (`luck`=50, `roll_luck` = coin), `LoseCarried` always returns one item without rolling, `TakeBack` takes the oldest ground item, `OpenTrade` true with `CloseTrade` cash change `-rand(0,30)`, `TakeCart` gives `cart_goods`, night log throws. Each of these can shift the 4000-run table; parity work (criterion 3) will need them as kit/bridge options or defaults.
- **Matrix details** — NER per-row seed `areaId.Length*31+policy.Length`, 800-step guard, rows keyed by policy name; Packing and Gunsmith checks; no NER 4000-run baseline captured yet.

Objective chosen for iteration 2: rule-based bots (decomposition step 2). It is the largest unknown — whether the expression language plus bot-side values can express `Plan` — and NER `sim.json` (step 3) can't carry its balance rows without it.

What existed at iteration 1:
- Framework `tests/Sim` (net8.0, source-includes `src/Core/**` + `api/*.cs`): `Program.cs` (unit-style tests + a random-bot play of `sample_area:flooded_cellar` with a hard-coded never-offered list, framework-specific `FakeBridge`) and `Lint.cs` (`lint <folder> [--mod id] [--strict]` via `PackSet`). Game csproj compiles only `src\**`, so a new tool folder outside `src/` cannot leak into the game assembly.
- NER `tests/Sim/Program.cs` (364 lines) — the parity baseline. Checks: load problems; missing text keys; runtime missing text / too-few-args / unfilled labels (`Missing`); unfilled night-log text (bridge throws); `CheckPacking` (`MapContext.Missing` on 6 hand-built bags); `CheckGunsmithLoot` (reload with `HasMod gunsmith_expansion`, roll 6 tables ×2000 with/without, `gsx_` ids); run-never-ended (>800 steps) and no-options guards; never-offered (60 ids). Balance: 3 random rows (kit varies per run index: mags/meds 2+n%3, tools on n%3==2, no blade on n%3==0) + 2 policies × 10 kit rows; per-row seed `areaId.Length*31+policy.Length`; prints cleared/knocked/extracted/credits/items/depth plus a wounds table. Bots `careful`/`greedy` (`Plan`) read hp/max_hp/bleeding, scene, foes' intent ids, `in_cover`, `boss_read`, `weak_flash`, focus, run vars (`rooms`, `boss_fight`), and bridge state (loaded, shots left, credits, has gun) — the rule language must reach all of these.
- No NER 4000-run baseline table captured yet (needed for criterion 3; NER C# sim builds against base `main`, so it can be captured any time).

Planned decomposition (one per iteration, revisable): (1) `ef-sim` skeleton — lint passthrough, `sim.json` v1 (kits, matrix, offered expectations), framework-owned data-driven bridge, built-in checks, built-in random bot, balance table; proven on the sample pack → (2) rule-based bots in the expression language → (3) NER `sim.json` (kits incl. per-run variation, matrix, expectations, packing cases) → (4) C# hook + NER Gunsmith check → (5) capture 4000-run baselines and close parity gaps.

</details>

Planned decomposition (revised iteration 3, one per iteration): (3) NER `sim.json` + per-run kit cycling + packing expectations; `ef-sim` on NER passes its checks → (4) bridge behaviour knobs matching NER `FakeBridge` + capture NER 4000-run baseline + compare (parity, criterion 3) → (5) Gunsmith loot check (data or C# hook) → (6) final green sweep (criterion 4) if not already shown.

### Closing Assessment (finalize, 2026-11-12)

Goal reached in 5 working iterations (5 Success, 0 Failure) + 1 goal-reached derive.

- `tools/EfSim` (`ef-sim`): `lint` (moved from `tests/Sim`, still shared) | `run` reads `sim.json` v1 — kits (incl. per-run kit lists), rule-based bots in the expression language, run matrix with seeds, `bridge` behaviour settings, expectations (`offered`, `packing`, `loot`) | built-in checks for every pack | balance table.
- NER `sim.json` (on disk, unversioned; sha256 `65ddd3c4…`) replaces every check of NER's C# sim; 4000-run table identical (46/46 cells Δ 0.0).
- Sprint-scope audit skipped: every iteration audited Verified; final derive independently re-ran all four criteria.
- Left to human: retire NER `tests/Sim` | self-contained publish of `ef-sim` | put NER under version control.

## Iterations

| # | Objective | Result | Artifacts |
|---|-----------|--------|-----------|
| 1 | `ef-sim` skeleton: lint moved in, `run` with `sim.json` v1, kit bridge, built-in checks, random bot, balance table; proven on sample pack | Success | 2611121012-ef-sim-skeleton-plan.md · 2611121035-ef-sim-skeleton-patch.md · 2611121025-ef-sim-skeleton-audit.md (Verified; 2 Minor cosmetic findings, fix skipped) |
| 2 | Rule-based bots in `sim.json` (`bots`, `val.*` inputs, strict parsing); NER `Plan` inputs mapped; proven on sample pack | Success | 2611121039-ef-sim-rule-bots-plan.md · 2611121100-ef-sim-rule-bots-patch.md · 2611121053-ef-sim-rule-bots-audit.md (Verified; no actionable findings) |
| 3 | NER `sim.json` (kits, careful/greedy bots, 23-row matrix, 60 offered ids) + kit lists per row + `expect.packing`; `ef-sim run` on NER exits 0 | Success | 2611121110-ef-sim-ner-pack-plan.md · 2611121121-ef-sim-ner-pack-patch.md · 2611121128-ef-sim-ner-pack-audit.md (Verified; fidelity faithful; luck knob drift measured, left to next iteration) |
| 4 | Bridge settings in `sim.json` (game values, trade, take-back, loss); NER set to mirror its FakeBridge; 4000-run parity vs NER C# sim | Success | 2611121143-ef-sim-bridge-knobs-plan.md · 2611121154-ef-sim-bridge-knobs-patch.md · 2611121201-ef-sim-bridge-knobs-audit.md (Verified; 46/46 cells Δ 0.0) · 2611121240-ef-sim-lose-cash-overflow-patch.md (audit finding 1 fixed) |
| 5 | `expect.loot` cases (tables, rolls, seed, mods reload, never_prefix, must_roll); NER Gunsmith check as data; full criterion-4 sweep green | Success | 2611121215-ef-sim-loot-expect-plan.md · 2611121226-ef-sim-loot-expect-patch.md · 2611121232-ef-sim-loot-expect-audit.md (Verified; Low findings only) |
| 6 | — (derive: goal reached, all criteria re-measured at `7b635ed`) | Goal reached | — |

## Open Questions

- [ ] (none yet)

## Revision Log

| Date | Iter | Summary |
|------|------|---------|
| 2026-11-12 | — | Sprint started |
| 2026-11-12 | 1 | Derive: surveyed both sims; recorded NER check inventory and bot inputs; objective = `ef-sim` skeleton (lint + sim.json v1 + data-driven bridge + built-in checks + random bot) proven on sample pack |
| 2026-11-12 | 2 | Derive: criterion 1 met by iteration 1; inventoried remaining gaps vs NER C# sim (bots, per-run kit variation, FakeBridge behaviour deltas, matrix/seed details); objective = rule-based bots in `sim.json` reaching every input NER `Plan` reads, proven on sample pack |
| 2026-11-12 | 3 | Derive: bots landed (iter 2); re-surveyed NER sim vs `sim.json` — remaining: per-run kit variation (period 3 → kit list cycled by run index), packing cases, NER data file, FakeBridge deltas, Gunsmith, baseline; decomposition revised (bridge knobs moved to parity step); objective = NER `sim.json` with kit cycling + `expect.packing`, `ef-sim` on NER exits 0 |
| 2026-11-12 | 4 | Derive: NER `sim.json` landed and passes (iter 3); criterion 2 lacks only Gunsmith; criterion 3 drift traced to `luck` knob, other FakeBridge deltas (trade, TakeBack order, LoseCarried) unmeasured; objective = bridge-behaviour settings in `sim.json`, NER set to FakeBridge, 4000-run tables of both sims compared within ±3 pts per cell |
| 2026-11-12 | 5 | Derive: criterion 3 met by iteration 4 (46/46 cells Δ 0.0); only Gunsmith loot check uncovered for criterion 2, data-expressible via existing `Lint.Load` mods seam; objective = `sim.json` loot expectations (tables × rolls, extra mods, forbidden prefix / required ids) with NER's Gunsmith case, plus full criterion-4 sweep in audit; decomposition step (6) folded into this audit |
| 2026-11-12 | 6 | Derive: goal reached — re-ran all criteria at `7b635ed`: ef-sim build 0/0; NER `ef-sim --runs 4000` exit 0 (packing 6/6, loot 2/2); 4000-run parity vs NER C# sim 46/46 cells Δ 0.0; framework sim tests pass; lint --strict NER + sample 0/0; game assembly 0W/0E. No objective emitted |
| 2026-11-12 | — | Finalize: closing assessment; Status → Complete (Goal Reached); moved to closed/; squash-close into main |
