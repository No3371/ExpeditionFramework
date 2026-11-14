# Audit: Damage Hit Context (`damage.hit` → `hit.NAME`)

> **Status:** Complete
> **Author:** Claude Opus 5.5 (audit step)
> **Parent:** 2611141406-damage-hit-context-log.md
> **Related Projex:** 2611141406-damage-hit-context-plan.md | 2611141406-damage-hit-context-log.md | 2611140500-authoring-language-text-params-hit-context-proposal.md
> **Audit Date:** 2026-11-14 | **Work Period:** reflog 16:06–16:17 (branch `projex/2611141406-damage-hit-context`, `479e92b..bf1dbcc`, base `main` @ `eaaeb81`)
> **Subject:** EF engine change: per-damage hit frame stack, `hit.NAME` path, `damage.hit` field, read-never-set lint, docs, `TestHitContext`

---

## Audit Summary

**Claim:** SC1–SC8 met; Starter/Showcase/NER lint, plain run, digest identical to baselines; tests green; V5g redirected game compile 0/0; public API and `src/Game` untouched.

**Verdict:** Verified

**Assessment:** Completeness: High | Correctness: High | Quality: High | Value: High

**Top Issues (none blocking):**
1. Read-never-set warning cannot be silenced for a cross-pack reader: `lint --strict` on a pack whose kit reads a `hit` name set only by another pack fails even with `--mod OTHER` (memory's analog is suppressed by `OtherPacks`; hit is not). Contradicts the plan's own reason for global names ("reader is often another pack's kit"). `src/Core/PackLoader.cs:725-732`.
2. Docs gaps: `expressions.md:66` read-only list omits `hit.`; hit lint warning undocumented (memory's is, `memory.md:50-52`); "names shared by every loaded pack" not stated anywhere shipped.
3. Execution log timestamps (Started 14:40, steps 14:45–16:18) disagree with reflog/evidence mtimes (16:06–16:17).

---

## Claims vs Evidence

| Claim | Evidence | Status | Notes |
|-------|----------|--------|-------|
| SC1 `hit` map, names `a-z0-9_`, evaluated after `amount` in written order, problems at `<path>.hit[.NAME]` | `Builtins.cs:26-47,635-647`; probe P9; `TestHitContext` | ✓ | Non-object `hit` reports at `<path>` (`.damage`), not `<path>.hit` — message names `"hit"`. Cosmetic |
| SC2 innermost frame, 0 outside, every damage pushes, pop on exception | `ExpeditionRun.cs:381-426`; probes P1–P6; tests | ✓ | `try/finally` covers whole body after the early return |
| SC3 read-only, `hit.$x`/`hit.a.b` unknown, one warning per (pack, name) | `Expr.cs:154-157`; probe P9 (`hit.`, `add hit.soft`, duplicate reads → 1 warning) | ✓ | |
| SC4 tests green, `TestHitContext` wired | my run: `All checks passed`; `grep -c 'TestHitContext()'` = 2 | ✓ | |
| SC5 four docs | `git diff` docs | ⚠ | Present and accurate; gaps (Finding S2, M1, M2) |
| SC6 baselines at start | `<S>/c2-proof/s0/*` mtimes 16:06–16:09 | ✓ | Not rerun pre-change by me; post-change rerun identical to the same baselines |
| SC7 lint/run/digest identical ×3 | my rerun `<W>/bar/` 12/12 ok, also byte-identical to executor's `c2-proof/after/*` | ✓ | NER 39 digest lines |
| SC8 no public API / `src/Game` / csproj change | `git diff --stat main..bf1dbcc -- api src/Game '*.csproj'` empty; no `-public/protected` line; no `+public/protected` line | ✓ | |
| V5g redirected game compile | `<S>/c2-proof/game-install/ExpeditionFramework.dll` 262144 B, 16:16, byte-equal `bin/Release/netstandard2.1/`, contains `IsPlainName` + UTF-16 warning text | ✓ | Live `Mods/ExpeditionFramework/ExpeditionFramework.dll` is 16:05:09 (before execution start 16:06:19), pre-C2 (no `IsPlainName`) → not written by V5g |

`<S>` = executor scratchpad; `<W>` = auditor scratchpad (`…/70184e206a1ee857/…/scratchpad`).

---

## Objective Verification

### 1. Frame semantics

**Evidence:** `ExpeditionRun.cs:381-426`, `Emit`/`Decide`/`Guarded` `:776-813`, `Defeat` `:483-494`, `Finish` `:370-379`; probe `<W>/efprobe` (`AuditProbe`, output `<W>/probe-out.txt`).

- Single damage path: every hp loss runs through `ExpeditionRun.Damage` (only `Set("hp", …)` with a loss is `:415`; heal is separate). Data `damage` (player and enemy turns — enemy intents are data effects), C# `IRun.Damage`, `ICombat.Defeat` all route here or to `Defeat`.
- Push after `!victim.Active || Over` early return (no hooks fire there), before `Decide` → interceptor-dealt damage nests. Pop in `finally`.
- Probes (all as designed):
  - P1 data hook throws (registry effect) inside a hit damage → exception escapes `Begin`, `Hit("soft")` = 0 after (frame popped).
  - P2 `defeat {who: self}` inside `damaged` → `knocked_out` reads `hit.soft` = 1, `run_end` = 1.
  - P3 `leave` inside `damaged` → `run_end` reads 1. Matches plan § Decisions "window = whole damage call".
  - P4 mod effect calling `IRun.Damage` inside a soft hit's hook → inner hook reads 0, outer restored (31).
  - P5 C# interceptor dealing damage during the decision → inner `damaged` reads 0, outer reads 1; hp 8.
  - P6 nested `"hit": {"soft": "hit.soft + 5"}` → inner reads 6: values evaluated before the push, so explicit propagation works.
  - Outside any damage → 0 (test case 1 `h_after`).
- C# listeners (`Guarded`) swallow exceptions, so only unguarded data/mod-effect throws reach the `finally` — P1 is the only route, and it holds.

**Verification:** ✓ Verified

### 2. Numeric path unchanged

**Evidence:** `Builtins.cs:632-648` diff; `<W>/bar/summary.txt`.

- Hit-less closure: `to(s)` → null return → `amount(s)` → public `Damage`. Old: `to(s)`, then `amount(s)` only when non-null. Same order, same RNG draws. `HitMap` returns null without touching RNG or loader state when `hit` absent.
- Rerun sequentially (Starter, Showcase, NER; lint `--strict`, plain run, `--digest`): 9/9 compares equal to `c-proof/base/` and `c-proof/digest2/*-digest-base.txt`; digest-minus-lines == plain run 3/3; tests/Sim `All checks passed`. NER run 158 s. My outputs byte-identical to executor's `c2-proof/after/*`.

**Verification:** ✓ Verified

### 3. Lint and loader

**Evidence:** `PackLoader.cs:147-161,725-734,1667`; `Expr.cs:394-401`; probes P7–P9.

- Problem paths: bad name → `….damage.hit.NAME` (empty name → `….damage.hit.`, cosmetic); bad expr → `….damage.hit.soft`; non-object → `….damage`.
- Memory notes still routed (`NoteRead` → `NoteMemory` for non-`hit.`); NER/Showcase lint unchanged; tests' memory-warning cases green.
- Duplicate reads → one warning (keyed first site).
- Mod-gated include holding the only setter (P8): `HasMod` false → warns; true → silent. Same model as memory's own-pack check; true in that configuration.
- **Cross-pack (P7):** setter pack a + reader pack b loaded together → 0 warnings. Reader b alone with `OtherPacks = {a}` (what `ef-sim lint --mod a` sets) → 1 warning; memory analog (`memory.a.flag`) → 0. Lint loads only framework + pack under test (`tools/EfSim/Lint.cs:77-88`) and `--strict` exits 1 on any warning (`Lint.cs:63`) → a pack reading another pack's hit name can never pass `lint --strict`. In-game (all installed packs in one `PackSet`) the warning is correct. Comment at `PackLoader.cs:729-730` acknowledges it; no escape hatch. NER unaffected (sets and reads `soft` in-pack).

**Verification:** ⚠ Partial — works as designed; cross-pack false positive under `--strict` (S1).

### 4. NER follow-up feasibility

**Evidence:** scratch copies `<W>/ner-hit`, `<W>/ner-mut` (pack data only: `ExpeditionFramework/`, `Localization/`, `Textures/`, `manifest.xml`, `sim.json`); edit script `<W>/ner_hit_edit.py`; outputs `<W>/nerprobe/`, summary `<W>/nerprobe-summary.txt`. NER repo untouched (`git status` shows only its pre-existing untracked `.projex` files).

- Edit exactly per plan § Notes: `land_hit` drops both `set run.soft` lines, `damage` gains `"hit": { "soft": "arg.soft" }`, comment rewritten; kit `defeated` `run.soft` → `hit.soft` ×2 + comment; Kade `defeated` `memory.rival_soft = hit.soft` + comment. `grep -c run.soft` → 0 in every file.
- Result: lint `--strict` 0/0, identical to `base/NER-lint.txt`; plain run byte-identical to `base/NER-run.txt`; `^digest ` lines identical to `digest2/NER-digest-base.txt`.
- Mutation (`ner-mut`: kit soft branch `!hit.soft`): lint still 0/0; all 39/39 `^digest ` lines differ from `digest2/NER-digest-base.txt`; ef-sim exits 1 (`FAIL runs[26]: memory.rival_won should be at least 25, got 23`). The `hit.soft` read is load-bearing and the digest sees it.
- Follow-up proof recipe for the NER plan: reuse `<W>/ner_hit_edit.py` (asserts each replacement matches once, CRLF preserved).

**Verification:** ✓ Follow-up de-risked

### 5. Public API and game safety

- No change under `api/`, `src/Game/`, any `*.csproj`; no public/protected line added or removed in `src/Core`. New members `internal` (`Hit`, `Damage(…, hit)`, `IsPlainName`, `NoteHitSet`).
- Behavioral surface: public `Expr.Reader("hit.soft")` now returns a reader (P10) — additive, documented as a path.
- `netstandard2.1`: `List`, `Dictionary.TryGetValue`, LINQ `All` on `string`, ValueTuple keys (already used). Proven by V5g artifact.
- I built only `tools/EfSim` and `tests/Sim` (plus a scratch copy of `tests/Sim` outside the repo for probes); never the game csproj.

**Verification:** ✓ Verified

### 6. Source hygiene and docs accuracy — see § Source Hygiene, § Documentation Audit.

---

## Code/Implementation Inspection

### `src/Core/ExpeditionRun.cs`
**Actual:** `hits` list, `Hit`, public `Damage` delegates, internal overload with `try/finally`. Public signature line byte-identical. — Quality: High
**Issues:** none.

### `src/Core/Builtins.cs`
**Actual:** `HitMap` (after `All`, deviation 2 — harmless), `damage` closure. — Quality: High
**Issues:**
- Hit values evaluated before `Damage`'s inactive/`Over` check (`:643-645` vs `ExpeditionRun.cs:402`): a hit value with dice rolls even when the damage does nothing. No current user; doc "worked out once when it starts" is consistent. — Low
- `else s.Run.Damage(...)` drops `hit` silently for a non-`ExpeditionRun` `IRun` (`:646`); only `MapContext` (no-op) qualifies. No comment. — Low

### `src/Core/Expr.cs`
**Actual:** `hit` head, `IsPlainName`, `memoryRead` → `pathRead`, message lists `hit.`. — Quality: High

### `src/Core/PackLoader.cs`
**Actual:** `hitSets`/`hitReads`, `NoteHitSet`, `NoteRead`, warning in `Finish`. — Quality: High
**Issues:** S1 (cross-pack lint). `NoteRead` method sits between field declarations (`:153` between `hitReads` `:148` and `packs` `:163`) — layout nit.

**Undocumented:** none found; diff = exactly the 4 source files + test + 4 docs + `.projex`.

---

## Testing Validation

**Execution:** tests/Sim `All checks passed` (my run, `<W>/bar/tests.txt`). Not flaky (deterministic seeds).
**Coverage:** cases 1–7 as planned; nested-hitless damage in case 1 goes through the public `Damage`, so "public Damage pushes a frame" is genuinely tested there.
**Missing / weak:**
- Case 4 (C# `IRun.Damage` empty frame) runs outside any damage, so it reads 0 with or without a frame — tests nothing beyond case 1. Probe P4 shows the real property holds. — Low
- No exception-through-frame test (plan deferred to code review). Probe P1 shows a mod effect throwing from a data hook reaches the `finally` and it pops — a data-only test is possible via `registry.AddEffect` throwing (pattern exists in `TestSharedEffectSurvivesThrow`). — Low
- No interceptor-nesting or `defeat`/`leave`-in-hook test (probes P2, P3, P5 pass). — Low

---

## Documentation Audit

**Accuracy:** effects/expressions/run-flow/csharp-api describe implemented semantics (after `amount`, nested own frame, outer restored, 0 outside, knockout/run_end window, `defeat` effect sees it, C# note). Examples parse (test packs use same shape).
**Gaps:**
- `docs/expressions.md:66` "`set` and `add` write to the same paths, except `memory.PACK.NAME`, `game.`, `arg.` and `val.`…" — `hit.` missing (now read-only). — Minor
- Read-never-set warning text undocumented; memory's equivalent is (`docs/memory.md:50-52`). — Minor
- Names shared across packs (plan § Decisions) and the lint consequence not in any shipped doc. — part of S1/M2
- `run-flow.md` new paragraph omits "in written order"; plan J4 / log claim it is covered. Matters only for dice in hit values. — Minor
- "C# interceptors' follow-ups" is vague (means: damage/effects an interceptor sets off). — Nit

---

## Source Hygiene

**Scope:** Diff only (`eaaeb81..bf1dbcc`, excluding `.projex/`)

| Rule | Location (symbol) | Quote | Severity |
|------|-------------------|-------|----------|
| 3 | `ExpeditionRun` → `hits` field comment | "A single run var (the old run.soft pattern) would leak" | Low — "old … pattern" is history narration; the rejected alternative ("a single run var") is named, so rule 6 is met. Drop "the old run.soft pattern" |

No projex IDs, labels, `file:ln`, step banners, or bare reassurance in added comments.

**Rule 6 gaps:**
- `Builtins` → `damage` closure `else s.Run.Damage(...)` — silently drops `hit` for a foreign `IRun`; reader needs to know why that is safe (only `MapContext`, a no-op).
- `Expr.Reader` `hit` case — `(s.Run as ExpeditionRun)?.Hit(name) ?? 0` chosen over a host interface (plan rationale); no comment. Low.

**Rule 6 vacuity:** none. `Damage(…, hit)` ("adding a member there would break every IRun outside this assembly"), `finally` ("an exception escaping a hook would leave its frame on top"), `Expr` hit case, `damage` order ("so a hit value that rolls dice always rolls in the same place"), `Finish` warning comment all name a constraint or rejected alternative.

**Rationale promotion:** No walkthrough yet (pre-close). Unpromoted: global-vs-pack-qualified names; push placement after the early return / before `Decide`. Close should name a target (run-flow.md or packs.md).

**Commit composition** (informational — fix forward): 7 commits (`eaaeb81` on main + 6 ephemeral). Step subjects `projex: step N …` allowed. `Projex:` trailer on every commit changing files outside `.projex/` (046c285, ec4bb22, 8cae3c8, f68faa1) 4/4; `eaaeb81`, `479e92b`, `bf1dbcc` are `.projex`-only (exempt). `eaaeb81` lacks Co-Authored-By (executor deviation 1; cannot amend a base-branch commit — accept).
**Violations:** None.

**Trailer survival on `main`:** 5/30 recent commits carry `Projex:` — every code-landing commit since the convention (2bf64dc, 6b99edf, c90a611, 6d56e0b, 68d0280); the rest are `.projex`-only or predate it. OK.

---

## Gap Analysis

### Promised But Not Delivered
| Promise | Status | Impact |
|---------|--------|--------|
| SC1 "non-object `hit` → problem at `<path>.hit`" | Partial (reported at `<path>`) | Low |
| Log J4 "docs cover written order, global names" | Partial | Low |

### Undocumented Issues
| Issue | Severity | Affects |
|-------|----------|---------|
| Cross-pack hit reader fails `lint --strict` regardless of `--mod` | Medium | Future cross-pack kits; not NER |
| Log timestamps 14:40–16:18 vs actual 16:06–16:17 | Low | Provenance record |
| Live `Mods/ExpeditionFramework` + `Mods/NightExpeditionsReborn` rewritten 16:05:09 (pre-C2 DLL), one minute before execution started; not by this execution and not recorded in its log | Info | Orchestrator should confirm that deploy was the human-approved one ("You may deploy the mod") |

### Unhandled Edge Cases
- Hit value with dice on an inactive target / after `Over` still rolls — Impact: RNG drift only once someone puts dice in `hit`.

---

## Quality Assessment

### Completeness: High
**Strengths:** every SC with evidence; proof bar reproduced independently; V5g artifact real.
**Gaps:** docs nits; cross-pack lint.

### Correctness: High
**Works:** nesting, restore, 0 outside, exceptions, knockout/run_end, defeat/leave in hook, interceptor nesting, C# damage: Yes.
**Bugs:** none found.

### Code Quality: High
**Positive:** small, symmetric with memory lint; no public surface; comments carry rationale.
**Tech Debt:** cross-pack warning model — Low/Medium.

### Value Delivered: High
**Intended:** replace NER's leaky `run.soft` with a scoped per-damage value.
**Actual:** engine support complete; follow-up proven drop-in (§ 4).

---

## Open Findings

### Impact Analysis
- Downstream NER: follow-up is a pure data edit with identical lint/run/digest (§ 4).
- Future: enables per-hit context for any kit; cross-pack kits blocked under `lint --strict` until S1 fixed.

### Improvements
- Suppress the hit warning when `OtherPacks` is non-empty (mirror memory: unknown packs may set it), or downgrade to "set by no pack loaded here" only when `OtherPacks` is null.
- Add the three missing test shapes (throw, interceptor, defeat-in-hook) cheaply from the probe.

---

## Findings

### Critical (Must Address)
- None.

### Significant (Should Address)
- **S1 Cross-pack `hit` read always warns in lint** — `src/Core/PackLoader.cs:725-732`; `tools/EfSim/Lint.cs:63,77-88`. Probe P7: reader pack with `OtherPacks={a}` → 1 warning; memory analog → 0. `lint --strict` fails for exactly the case the plan cites as the reason for global names. → Skip the warning (or soften wording) when `OtherPacks` names any pack; document. Not blocking: no pack reads a foreign hit name today.

### Minor (Nice to Fix)
- **M1** `docs/expressions.md:66` read-only list omits `hit.` → add.
- **M2** Hit read-never-set warning and cross-pack name sharing undocumented → add to `run-flow.md` § Damage and defeat (or `packs.md`), mirroring `memory.md:50-52`; promote the "names are global" rationale there.
- **M3** `run-flow.md` paragraph omits "in written order" → add three words.
- **M4** Non-object `hit` problem path is `<path>` not `<path>.hit` (`Builtins.cs:32`, via `c.Error`) → use `spec.Loader.Problem(c.Path + ".hit", …)` if SC1 wording matters.
- **M5** `TestHitContext` case 4 cannot fail; no throw/interceptor/defeat-in-hook tests → port probes P1, P2, P5 (`<W>/audit-probe.cs`).
- **M6** Comment "the old run.soft pattern" (`ExpeditionRun.cs:382-383`) is history narration → drop the parenthetical.
- **M7** Rule 6 gaps: `Builtins` `damage` `else` branch (`:646`) and `Expr` `hit` cast (`:157`) → one-line rationale each.
- **M8** Execution log timestamps don't match reflog (Started 14:40 vs `eaaeb81` 16:06:19; step 5 "16:18" vs complete commit 16:17:36) → correct at close.
- **Nit** Hit values evaluated before the inactive/`Over` early return (`Builtins.cs:643-645`); `NoteRead` placed among field declarations (`PackLoader.cs:153`); empty hit name path renders `….hit.`.

### Positive
- Bar reproduced 12/12, byte-identical to executor's outputs.
- `try/finally` and push placement correct under every nesting route probed, including interceptors and exceptions.
- Plan's NER follow-up is a verified drop-in.

---

## Recommendations

**Immediate:** Accept; fold M1–M3, M6–M8 into close (docs/comments/log only, no behavior change, bar unaffected).
**Future:** S1 before any cross-pack kit reads `hit`; M4–M5 opportunistically. NER `run.soft` → `hit.soft` follow-up after merge — edit in § 4 is ready to reuse.
**Process:** Execution log timestamps should come from the clock at write time.

---

## Final Verdict

**Status:** Accept with Conditions

**Overall Assessment:** Completeness High | Correctness High | Quality High | Value High

**Conditions:**
- [ ] M1, M2, M3 doc fixes (or explicitly deferred) before/at close
- [ ] M8 log timestamps corrected at close
- [ ] S1 recorded as a follow-up (memo or the NER follow-up's plan) if not fixed now

**Sign-off:** Yes — engine semantics, numeric invariance and API safety independently verified; remaining items are docs, test depth and a lint false positive with no current trigger.
