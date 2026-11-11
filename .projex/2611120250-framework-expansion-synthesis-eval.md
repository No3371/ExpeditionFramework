# Framework Expansion — 5-Slice Exploration Synthesis

> **Status:** Draft
> **Author:** Opus 5.5 (Orchestrator, aggregating 5 Sonnet explore-projex runs)
> **Parent:** Orchestrator
> **Related Projex:** 2611120231-pack-schema-loader-expansion-explore.md | 2611120227-runtime-hooks-expr-expansion-explore.md | 2611120227-persistence-game-integration-expansion-explore.md | 2611120229-ui-presentation-expansion-explore.md | 2611120231-authoring-tooling-sim-testing-expansion-explore.md | 2611120240-content-expansion-synthesis-eval.md (NightExpeditionsReborn)
> **Spawned Plans:** 2611120258-loader-loud-failures-plan.md | 2611120258-runtime-safety-fixes-plan.md (Phase 1)
> **Walkthroughs:** 2611120258-loader-loud-failures-walkthrough.md | 2611120258-runtime-safety-fixes-walkthrough.md (Phase 1 complete)

---

## TL;DR

Framework is strict at the edges, silent in the middle. Three walls block the NER content ideas: **(1) no persistent state from data**, **(2) decisions (damage/heal/chance/loot) are C#-only**, **(3) layout frozen at run start**. Base game already ships a per-mod KV store (`ModHelper.Get/Set…`) the framework doesn't use — cheapest wall to remove. Before any feature, a **hardening + lint pass** pays for itself: many content mistakes today fail silently.

All findings: static reading only (sim runs excepted). Nothing played in-game.

---

## Per-Slice Index

| Slice | Doc | Headline | Top candidates |
|---|---|---|---|
| Schema/loader | 2611120231-pack-schema-loader-expansion-explore.md | Unknown keys + wrong-typed scalars silently ignored inside defs | E2 strict scalars · E9 problem severity · E1 unknown-key warnings · E14 group composition · E7 slot `when`/`chance` · E5 per-pool weight/`when` · E8 pick-aware intel · E12 general `unlocked_by` |
| Runtime/hooks/expr | 2611120227-runtime-hooks-expr-expansion-explore.md | Decisions C#-only; hooks subject-scoped; no status timers | R2 status stacks + `on_expire` · R1 data-defined interceptors · R3/R4 observer + missing hooks · R5 dynamic rooms · R6 timers · R13 hardening |
| Persistence/game | 2611120227-persistence-game-integration-expansion-explore.md | Only 4 `game.*` reads, zero persistent writes; ModData unused | E1 pack `memory.*` store on ModData (~120 LOC) · E3 safe commit on leave-early · E2 more `game.*` reads · E4 save schema version + id migration |
| UI/presentation | 2611120229-ui-presentation-expansion-explore.md | Risk = button explosion in non-scrolling actions window, not tells | P-1 scrollable actions · P-4 threshold tone on `bar` · P-5 HUD intent-row control · P-3 pack tones + loader warning · P-7 sim text preview · P-8 locked-with-reason options |
| Tooling/sim/tests | 2611120231-authoring-tooling-sim-testing-expansion-explore.md | Sims pass; bots hard-coded; coverage by option id only | A lint · B shared Sim.Core · C coverage report · D data-driven bots · E seed replay · F higher-fidelity FakeBridge |

---

## Convergences

- **Silent failure is the #1 theme** (4/5 slices). Loader drops unknown keys/wrong types; runtime drops reserved `run.*` writes; `GiveItem` returns null on unknown id; unknown tones render plain; `remove` on last enemy = win. → One "loud failures" initiative: Schema E1/E2/E9/E10 + Runtime R13 + Tooling A + UI P-3 warning.
- **Lint needs one home.** Schema (`#group` lint, text-key gaps incl. dynamic `{…}` keys), Tooling (orphan keys, en/zh parity, unused defs), UI (tone validation). Put it in the loader with severities (E9) so in-game + sim share it.
- **Sim is the integration point for everything new.** Persistence: any new `IBridge` member needs FakeBridge + MapContext no-op. UI: sim never calls `Hud()`. Tooling: bots can't exercise new content. → Shared Sim.Core (B) first, else every feature pays the duplication tax twice.
- **Timers via statuses.** Runtime R2 (`on_expire`) + R6 + Schema slot `when` together cover NER's night clock, Sapper fuse, tide, acts — without a new scheduler.

## Content Ideas → Framework Verdicts

Resolves 2611120240-content-expansion-synthesis-eval.md § Unverified Capabilities.

| Assumed capability | Verdict | Source slice |
|---|---|---|
| Status hooks on `room_enter`/`damaged` | ✓ works | Runtime |
| Enemy `damaged` hook, self = enemy (two-phase bosses) | ✓ works | Runtime |
| Spawn by id | ✓ combat only; enemy-phase spawn loses Opening intent | Runtime |
| Stitcher heals allies | ✓ `pick_enemy` + `heal` | Runtime |
| `item_stats` props via `best_item` | ✓ | Runtime |
| Lurker via `remove` + `schedule` | ✗ remove irreversible; last-enemy remove = win | Runtime |
| `unlocked_by` list/condition | ✗ single id only; list silently ignored → area always unlocked (verified `PackLoader.cs:840`) | Schema |
| Third intel card | ✓ as extra slot; no cap; but `intel(slot)` returns count, not which pick; map card layout unverified | Schema, UI |
| Per-area `room_table` override | partial — layout pre-rolled at depth 0; no slot `when` | Schema |
| Pure-buy trade (Fence) | ✓ at TradeCounter level; negotiation UI unverified | Persistence |
| Cross-run memory (Ledger/rival/Hollow/companions persist) | ✗ today; E1 memory store unblocks | Persistence |
| New item ids / crafting salvage | unknown — needs game-side check (Tooling H: item-id catalog export) | — |

## Recommended Sequence

| Phase | Work | Unblocks |
|---|---|---|
| 0 | First commit of framework repo (repo has none) | CI, execute-projex cycle |
| 1 Harden | Schema E1/E2/E9 · Runtime R13 · Tooling A lint · UI P-3 tone warning | Safe authoring for everything after |
| 2 Sim base | Tooling B shared Sim.Core · C coverage · F FakeBridge fidelity | Every later feature testable once |
| 3 Memory | Probe ModData scope (per-save vs global) → Persistence E1 `memory.*` + E3 safe commit | Ledger, rivals, Hollow, companions, Tithe |
| 4 Expressiveness | Runtime R2 status stacks/`on_expire` · R3 observer hooks · `has_status()` · Schema E7 slot `when`/`chance` · E8 pick-aware intel | Tonight Cards, night clock, Sapper, acts, Commissions |
| 5 Presentation | UI P-1 scroll · P-4 · P-5 · P-8 | Readable richer fights |
| later | Runtime R1 data interceptors · R5 dynamic rooms · Tooling D data bots · Schema E5/E12/E14 | Shields/crit, Long Night/branching, sim of non-combat |

## Open Probes (need game/runtime, not reading)

- ModData: per-save or global? survives abrupt exit? (blocks Phase 3)
- Actions window overflow behaviour; builder support for disabled buttons/tooltips/scroll
- Mid-run ambience swap; third intel card on map layout
- `smg-nomed` kit likely still offers heal (bandage Count 0; `Select()` no count check) — sim results ≈ SMG kit
- HealthData API for lowering wound state; store trust/level exposure

## Incidental Bugs/Debts Found

- `LeaveShared` no `finally` → recursion counter leak on exception (Runtime)
- 1-turn enemy-applied status expires before player acts (Runtime)
- `variants`/dice in labels/`when` re-roll every 0.5 s UI refresh — latent (Runtime)
- `ef_best_depth` write-only (Persistence)
- Clear commit only in `Leave()` → crash loses clear (Persistence)
- `area.weight`/`area.when` parsed, never used (Schema)
- NER orphan keys: peddler buy/haggle, `ne_bribe_poor`, `ne_status_line` (Tooling)
- `//` comments accepted by parser → breaks standard JSON tooling (Tooling)
