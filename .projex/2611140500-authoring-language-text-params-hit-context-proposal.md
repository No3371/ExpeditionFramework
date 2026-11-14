# Authoring Language: Text Params, Hit Context, Event Vars (Track C)

> **Status:** In Progress (Accepted 2026-11-14)
> **Progress:** C1 + digest done
> **Author:** Claude Opus 5.5 (propose step)
> **Parent:** 2611140152-ef-ner-next-steps-proposal.md
> **Related Projex:** 2611140627-text-params-and-sim-digest-plan.md (EF; Next Steps 1 — digest + C1) | 2611140627-text-params-and-sim-digest-walkthrough.md (EF; closes Next Steps 1) | 2611140743-text-params-and-sim-digest-audit.md | 2611140800-text-params-audit-fixes-patch.md | 2611141308-ner-rep-book-settle-plan.md (NER; Next Steps 2 — rep_book refactor) | 2611140152-ef-ner-next-steps-proposal.md (NER; source, § C + open question "Balance proof for refactors") | 2611140132-ef-ner-capability-boundaries-explore.md (NER) | 2611140220-ef-sim-reach-and-item-guard-plan.md (EF; tracks A/B1 — the row keys this proof leans on) | 2611140220-ner-rep-week-intel-sim-rows-walkthrough.md (NER; rows 2009–2016) | 2611120227-runtime-hooks-expr-expansion-explore.md | 2611120231-pack-schema-loader-expansion-explore.md
> **Baseline:** EF `main` @ `6b99edf`, NER `master` @ `1277ab0`. `ef-sim run` NER (39 rows × 2000), Showcase (3 rows), Starter (1 row) and `lint --strict` NER (0 errors, 0 warnings) captured as proposer baseline. NER run output reproduced byte-for-byte on a second run (deterministic).

---

## Summary

**Recommendation:** first slice = **C1 text params** (EF loader) **+ a determinism digest in ef-sim** (the proof tool), then an NER plan that folds the five reputation blocks into one `rep_book` shared effect, accepted only on identical lint/run/digest output. **C2 hit context** follows as its own small EF plan + 2-line NER change, same bar. **C3 `event.*` vars: not now** — payoff is 8 `set` lines, risk is event/cart lifetime.

Balance proof (settles the parent's open question, for the human to accept): a C refactor is a pure refactor, so the bar is **identical output, no noise-band fallback**. Today's table alone is too coarse to prove that (§ Balance-Proof Bar), hence the digest.

---

## Problem Statement

### Current State

**EF engine (re-verified at `6b99edf`):**
- Shared effect = `params` + `do`; body compiled **once** (`src/Core/PackLoader.cs:470-475`, deferred via `sharedBodies`, run at `:535`). Call args are `Value`s (`:487-496`) evaluated into `Dictionary<string, int>` per call (`:503-505`), read as `arg.NAME` (`src/Core/Expr.cs:148`). Docs: "Parameters are numbers." (`docs/packs.md:140`).
- Effect fields such as `reputation.faction` (`src/Core/Builtins.cs:431-432`, checked against `GameReads.Factions`, `src/Core/Model.cs:504`), `set/add.var` (literal path → `Expr.Writer`) and `say/night_log.text` are **compile-time literals**. Text keys may carry runtime `{path}` templates (`docs/text.md:69-78`), but `faction` and `var` cannot.
- `damage` builtin → `ExpeditionRun.Damage(target, amount, source, pierce)` (`src/Core/Builtins.cs:609-615`, `src/Core/ExpeditionRun.cs:376-394`). Only extra state: `decision.pierce` and `run.last_damage`. `Emit` builds a fresh `Scope(run, subject, other, null)` with no args (`ExpeditionRun.cs:744-757`); `damaged` (`:391`), `defeated` (`:461`) and `knocked_out` (`:456`, then `Finish` → `run_end` at `:369`) see only victim + source.
- `Damage` has exactly one caller: the `damage` builtin (`Builtins.cs:614`).

**NER workarounds (re-verified at `1277ab0`; parent claims checked):**

| Parent claim | Verdict | Evidence |
|---|---|---|
| Settle block repeated 5× | ✓, with nuance | `ExpeditionFramework/kit.json:242-280`: 5 × (`rep_settle` call + per-faction tail). Lower/black/security tails = 7 lines (gain + loss branch); upper (`:272-276`) gain-only; revolution (`:277-280`) loss-only. 39 lines total. |
| Opposite-sign source "needs its own block, or it is dropped" | ✓ | Comment `kit.json:269-271`. Upper has no `ne_rep_down_upper`, revolution no `ne_rep_up_revolution` (NER `Localization/en.csv:882-889` has 8 keys, not 10). |
| Literals that force the copy | ✓ | Per block: `faction` literal, `memory.rep_gain_<f>` var name literal (`:244,254,263,274`), night-log key literal (`ne_rep_up_<f>` / `ne_rep_down_<f>`), `cur: game.rep.<faction>` path. Only `n`/`home` differ in substance. |
| `run.soft` wraps `damage` | ✓ | `combat.json:18-28` (`land_hit`: set → `damage` → clear). Readers: kit `defeated` hook `combat.json:364,372`; Kade's own hook `enemies.json:363-365` (`memory.rival_soft`). Writers feeding it: `combat.json:46` (`item.round_soft`), `:69`, `:212`, `:236`. |
| Peddler's 7-var reset | ✗ partly | `events.json:888-894` is 7 `set`s, but `run.rep_peddler = 1` (`:888`) is **not** a reset: it must outlive the event, read by the black-market settle `kit.json:252`. Real reset: 6 vars. Plus single resets in `door` (`events.json:615`) and `slots` (`:852`). `run.searches` (`:843`) is read outside the event — not a candidate. |

Other enum-int plumbing C1 could touch (not motivating, list only): `rival_outcome` 4-branch `cond` mapping `arg.outcome` → `memory.rival_<x>` (`rules.json:227-238`; numeric `rival_last` still needed); `claimant` `kind` → text key (`rules.json:281-288`).

### Gap / Need / Opportunity
- The settle is NER's largest copy-paste and the place a future rep source silently vanishes (sign trap). Root cause = EF can't parameterize literal fields.
- `run.soft` is a global flag standing in for per-hit context. Latent hazard: any damage dealt *inside* a `defeated` hook during a soft hit would inherit `soft = 1`. No NER content does that today; the next author has no warning but a comment.
- Event resets are verbose but correct.

### Why Now?
Tracks A/B1/F landed: NER now has rows for rep bands, week rollover, rep budgets, map cards (2009–2016). Without them the settle refactor would land where tests were blind (parent § Option B cons). That blocker is gone.

---

## Proposed Change

### Overview

| Item | EF change | NER payoff | Effort | Balance |
|---|---|---|---|---|
| **P** ef-sim `--digest` | per-row hash of observable run results | the proof instrument | S | none (observes only) |
| **C1** text params | `$`-named params, `{arg.$NAME}` substituted at compile, body compiled per call site | 39-line settle → 5 one-line calls + one ~16-line `rep_book`; sign trap gone | M | none if digest identical |
| **C2** hit context | `damage` `hit` map → `hit.NAME` in hooks during that damage | `run.soft` set/clear gone; nested-damage hazard gone | S | none if digest identical |
| **C3** `event.*` | event-scoped vars cleared on event start | 8 `set` lines | M | none, but lifetime risk |

### C1 design sketch (directional; Plan fixes details)

- **Declare** a text param by a `$` name, mirroring EF's text-var convention (`run.$name`): `"params": { "$faction": "lower_level", "$short": "lower", "n": 0 }`. A plain string default stays an expression (`"tier": "run.loot_tier"`, `rules.json:72`) — so a marker is required; `$` avoids a new object shape.
- **Use** as `{arg.$NAME}` inside any string in the body: field values, var paths, text keys, expressions. Substituted on the raw JSON tree before compiling.
- **Compile** per call site, only for effects with ≥1 text param. Numeric-only effects keep today's compile-once path untouched.
- **Guards:** value must match `[a-z0-9_]+` (no expression injection) | unknown `{arg.$x}` = problem | problem paths name the call site and the body path | recursion through text-param effects capped at compile time (the runtime 32-deep guard doesn't cover compile).
- Bonus: substituted keys are literal at compile, so `lint` checks them — unlike runtime key templates (`docs/text.md:78`).

NER result (sketch, not final):

```json
"rep_book": {
  "params": { "$faction": "", "$short": "", "n": 0, "home": 0 },
  "do": [
    { "rep_settle": { "n": "arg.n", "home": "arg.home", "spent": "memory.rep_gain_{arg.$short}", "cur": "game.rep.{arg.$faction}" } },
    { "if": { "when": "run.rep_out > 0", "then": [
      { "add": { "var": "memory.rep_gain_{arg.$short}", "value": "run.rep_out" } },
      { "reputation": { "faction": "{arg.$faction}", "value_x100": "run.rep_out", "night_log": { "text": "ne_rep_up_{arg.$short}", "args": ["run.$rep"] } } }
    ] } },
    { "if": { "when": "run.rep_out < 0", "then": [
      { "reputation": { "faction": "{arg.$faction}", "value_x100": "run.rep_out", "night_log": { "text": "ne_rep_down_{arg.$short}", "args": ["run.$rep"] } } }
    ] } }
  ]
}
```
```json
{ "rep_book": { "$faction": "lower_level", "$short": "lower", "n": "25 * run.ledger_mercy - 25 * run.rep_robbed", "home": "run.turf_lower ? 15 : 0" } }
```

Why this is balance-neutral by construction: same effect order, same `dice` calls (all inside unchanged `rep_settle`, `rules.json:370-388`); the added upper-loss / revolution-gain branches can't fire because `rep_settle` keeps `rep_out`'s sign equal to `n`'s, and those `n` expressions are one-signed. Revolution's `spent` goes from literal `0` to `memory.rep_gain_revolution`, which is never written non-zero (gain branch unreachable) → reads 0. Needs two new loc keys (`ne_rep_down_upper`, `ne_rep_up_revolution`) or lint `--strict` fails (Q2). Week-reset block (`kit.json:235-241`) stays as is; add `rep_gain_revolution` to it so the trap doesn't reappear there.

### C2 design sketch
- `damage` takes optional `"hit": { "soft": "arg.soft" }` — **a map of name → expression**, evaluated once when the damage starts. The parent's `tags` list can't express NER's case: softness is computed (`item.round_soft`, `item.#non_lethal || item.stuns`), so a static list would force an `if` around two `damage` calls. Optional sugar: `"hit": ["soft"]` = each 1.
- `hit.NAME` (new `Expr` head) reads the innermost damage in progress; 0 outside any. Stack push/pop in `ExpeditionRun.Damage`, so nested damage in a hook gets its own context and the outer one is restored. Visible in `damaged`, `defeated`, `knocked_out` and the `run_end` that `knocked_out` triggers. Optionally copied into the `damage` decision for C# interceptors.
- NER: delete `combat.json:23,25` and comments; `run.soft` → `hit.soft` at `combat.json:364,372`, `enemies.json:365`.

### C3 (not now)
6 + 1 + 1 resets. Open lifetime question: `peddler_cart.budget` (`events.json:10`) and the counter's `after_counter` (`events.json:91-110`) read peddler vars — `event.*` must outlive the event while its cart/counter is open, which needs an EF definition of "event end" that doesn't exist yet. Revisit when a new event needs ≥3 resets.

### Approach Options

#### Option A: C1 only (+ digest)
- **Pros:** biggest NER payoff; one design (macro semantics) to review; proof surface = settle rows only.
- **Cons:** `run.soft` hazard stays.
- **Effort:** M (EF) + S (NER).

#### Option B: C1 + C2 in one EF plan
- **Pros:** one plan cycle; both are identical-output refactors, so one proof run covers both.
- **Cons:** two unrelated engine areas (loader vs. run/expr) in one review; a digest diff then needs bisecting; C2's NER payoff is 2 lines.
- **Effort:** M+ (EF) + S (NER).

#### Option C: C1 + C2 + C3
- **Pros:** closes the parent's whole track.
- **Cons:** C3 needs a lifetime model (event vs. cart vs. counter) for 8 lines of payoff; widest risk.
- **Effort:** L.

### Recommended Approach

**Option A, then C2 as the next small plan.** First slice:
1. EF plan: `ef-sim run --digest` + C1 + tests (`tests/Sim`) + docs. Acceptance: NER/Showcase/Starter `run` output and digests identical to baseline (no NER change yet), lint `--strict` identical.
2. NER plan: `rep_book` refactor + two loc keys. Acceptance: § Balance-Proof Bar.
3. Then C2 the same way. C3 stays parked.

Reason: C1 carries the design weight and the payoff; keeping C2 apart keeps each review to one engine area. The digest goes first because without it "identical" proves little (next section).

---

## Balance-Proof Bar (recommendation for the human)

**Pure refactors (all of C) must produce identical output. No noise-band fallback.** A C refactor changes no `dice` call and no effect order, so any difference is a bug to find, not noise. The parent's fallback ("table diff within noise + sign-off") stays for D/E-style changes that are *meant* to move numbers.

Why the parent's "byte-identical `--runs 2000` table" alone is too weak: each row prints only cleared / knocked-out / extracted at 0.1% (baseline file). Rep settles at `run_end`, after play; in non-carry rows it can't move those numbers at all. In carry rows it moves them only through rep/memory-gated content. A bug that books the right amount to the wrong faction, or a wrong night-log key, can leave the table identical and every `expect` range satisfied.

**Bar, all four must hold, before vs. after, at committed seeds and committed run counts:**
1. `ef-sim lint "<NER>" --strict` → output identical (0/0).
2. `ef-sim run "<NER>"` (sim.json's own 2000 per row; no `--runs` override, so carried rows keep their length) → stdout byte-identical: table, WARN lines, check lines.
3. `ef-sim run --digest` → per-row digest identical. Digest = hash over each run, in order, of: outcome | run log lines (rendered) | night-log lines (rendered; sim drops them today, `tools/EfSim/KitBridge.cs:113`) | end memory with 0 ≡ absent | end reputation per faction. Run vars excluded (C2 deletes `run.soft` on purpose).
4. Same 1–3 for Showcase and Starter (engine additive-ness), plus `dotnet run -c Release --project tests/Sim` green.

Cost: NER `run` takes ~2m40s locally; the whole bar ~6 min.

---

## Impact Analysis

### Affected Areas
- EF `src/Core/PackLoader.cs` (`DeclareShared`, `CompileSharedCall`; per-site compile path) — C1.
- EF `src/Core/ExpeditionRun.cs` (`Damage` hit stack), `src/Core/Expr.cs` (`hit` head), `src/Core/Builtins.cs` (`damage` `hit` field) — C2.
- EF `tools/EfSim/PackRun.cs`, `KitBridge.cs` (`--digest`, night-log capture) — P.
- EF `tests/Sim/Program.cs` — loader error cases (bad `$` value, unknown placeholder, recursion), nested-damage `hit` restore.
- NER `ExpeditionFramework/kit.json` (settle), `rules.json` (`rep_book`), `Localization/en.csv` (2 keys) — C1; `combat.json`, `enemies.json` — C2.

### What existing rows catch / miss

| Regression | NER 39 rows | Showcase/Starter | Caught elsewhere |
|---|---|---|---|
| Numeric shared-effect path broken (C1 touches it) | ✓ every row (`alarm`, `find_loot`, `hurt`…) | ✓ Showcase `raise_heat` | — |
| RNG order shifted in settle | ✓ carry rows (2005, 2007, 2008, 2011, 2013–2016) — later nights shift | ✗ | digest |
| Weekly budget / cap broken | ✓ 2005, 2007, 2008, 2011, 2015, 2016 (`rep_gain_*` ranges) | ✗ | digest |
| Week rollover | ✓ 2013, 2015 (`rep_week`) | ✗ | — |
| Upper/revolution amounts | ✓ 2013, 2014 (`rep_exact`) | ✗ | — |
| Lower/black/security booked to wrong faction or amount within range | ✗ (ranges wide; no `rep_exact` for them) | ✗ | digest |
| Wrong night-log key / args | ✗ (sim drops night-log text) | ✗ | lint (missing key) + digest |
| Soft vs. lethal takedown (C2) | ✓ 2003 (`rival_soft: 0`), 2006 (`rival_soft: 1`), 2007 (security subdued gain) | ✗ | digest |
| `hit` leaking into nested damage | ✗ (no NER content nests damage) | ✗ | `tests/Sim` unit case |
| Loader error text/paths for text params | ✗ | ✗ | `tests/Sim` |
| In-game reputation application | ✗ (sim's `KitBridge` stands in) | ✗ | unchanged code path; one in-game night recommended |
| Packs beside NER (e.g. Gunsmith Expansion) | partial (loot `mods` cases only) | ✗ | — |

### Dependencies
- Needs A/B1 rows (done). Digest (P) before the NER refactor.
- C2 independent of C1; C3 waits on an event-lifetime design.

### Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Per-site compile changes the numeric path | Low | High | Only `$`-param effects take the new path; Showcase/Starter/NER identical before any NER edit |
| Placeholder injects expression syntax | Low | Med | `[a-z0-9_]+` value guard |
| Compile-time recursion through text-param effects | Low | Med | Compile-depth cap + test |
| Problem paths point into the shared body, not the caller | Med | Low | Path = call site + body path; tested |
| Digest itself perturbs RNG | Low | High | Observe only; digest run's table must equal the plain run's |
| `{arg.$x}` confused with runtime key templates `{path}` | Med | Low | Substitution runs first; any `{arg.` left is a problem; docs say which is which |
| Revolution budget not reset weekly after unification | Low | Low | Add `rep_gain_revolution` to the week-reset block |

### Breaking Changes
None. `$` param names and `hit`/`hit.NAME` are new; a `$` name was not usable as a param before (verify in Plan). `--digest` is opt-in.

### Docs (EF `docs/`)
- `packs.md` § effects: replace "Parameters are numbers." (`:140`) with text params — `$` names, `{arg.$NAME}`, per-call-site compile, value rule, errors at call site.
- `expressions.md` paths: `arg.NAME` row (`:56`) notes `{arg.$NAME}` is compile-time; add `hit.NAME` (C2).
- `text.md` § Key templates (`:69-78`): `{arg.$NAME}` is filled at load, so lint checks it — unlike `{path}`.
- `effects.md` `damage` row (`:124`), `run-flow.md` § Damage and defeat (`:73-76`) + hooks table (`:110-112`), `csharp-api.md` if the decision carries `hit` — C2.
- `testing.md`: `--digest` and a short "proving a refactor" recipe.

---

## Open Questions

- [x] **Balance-proof bar:** → accepted by the human (2026-11-14, "Accept it"). accept "identical lint + identical `run` output + identical `--digest`, no noise-band fallback" as the bar for every C refactor in NER (the noise-band + sign-off fallback kept only for changes meant to move balance, D/E)?
- [x] **Missing-sign lines:** → agent drafts them (human, 2026-11-14, "Agent drafts them"). Drafts for the NER refactor plan to add (en.csv / zh.csv, beside lines 888–889):
  - `ne_rep_down_upper,The plant's owners heard whose side you took on their floor. ({0})` / `铸造厂的老板们听说了你在他们车间里站在哪一边。（{0}）`
  - `ne_rep_up_revolution,The crews who took the plant heard you did right by theirs. ({0})` / `占领铸造厂的工人们听说你对他们的人够意思。（{0}）`
  Original question: unifying the settle needs two lines no night can show today, `ne_rep_down_upper` and `ne_rep_up_revolution`. Agent drafts them in NER's voice, or you write them?

---

## Next Steps

If accepted:
1. `/plan-projex.md` in EF `.projex/`: ef-sim `--digest` + C1 text params + `tests/Sim` cases + docs (`packs.md`, `expressions.md`, `text.md`, `testing.md`). Acceptance: Showcase/Starter/NER outputs and digests identical to baseline.
2. `/plan-projex.md` in NER: `rep_book` + loc keys + week-reset line; acceptance = § Balance-Proof Bar.
3. `/plan-projex.md` in EF: C2; NER follow-up patch for `run.soft`.
4. C3: reopen only with a second event needing ≥3 resets.

---

## Appendix

### Research / References
- EF: `src/Core/PackLoader.cs:440-513,535` | `src/Core/Expr.cs:134-156` | `src/Core/ExpeditionRun.cs:365-394,451-462,744-763` | `src/Core/Builtins.cs:431-447,609-615` | `src/Core/Model.cs:504` | `tools/EfSim/SimConfig.cs:153,159` (row keys, default seed `42 + n`) | `tools/EfSim/PackRun.cs:198-245` | `tools/EfSim/KitBridge.cs:113` | `docs/packs.md:116-140` | `docs/text.md:69-78`.
- NER: `kit.json:197-205,231-281` | `rules.json:56-76,220-298,360-388` | `combat.json:18-28,46,69,212,236,359-376` | `enemies.json:363-366` | `events.json:8-15,91-110,612-616,840-853,885-930` | `Localization/en.csv:882-889` | `sim.json` (39 rows; memory/game/days/intel/expect keys per row).
- Baseline outputs (proposer scratch, not committed): NER run sha256 `59695b34…`, Showcase `f3936dc0…`, Starter `9e6b68ba…`, NER lint `fb9bcec1…` (0/0).

### Alternatives Considered
- **Runtime text args** (`arg.$NAME` as a text path; make `faction`/`var` accept text paths): rejected — every literal field would turn runtime-resolved, losing load-time faction/var/key checks and the memory read/write warnings (`PackLoader.cs:590-600`).
- **NER-only: runtime key templates** (`ne_rep_up_{run.$fac}`): covers the night-log key only; `faction` and the budget var stay literal → the block can't fold.
- **C2 as a static `tags` list** (parent wording): can't carry a computed softness without duplicating `damage`.
- **C2 via `run.last_*`-style run vars set by EF:** same leak on nested damage as `run.soft`.
- **Byte-identical table as the only proof:** too coarse (see bar).
