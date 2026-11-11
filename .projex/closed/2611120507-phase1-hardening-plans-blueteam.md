# Blue Team: Phase 1 Hardening Plans (loader loud failures + runtime safety fixes)

> **Status:** Complete (Fix Then Proceed)
> **Author:** Opus 5.5 (blueteam-projex subagent, dispatched by orchestrate-projex)
> **Parent:** 2611120448-phase1-hardening-plans-redteam.md
> **Source:** 2611120448-phase1-hardening-plans-redteam.md | **Subject:** 2611120258-loader-loud-failures-plan.md + 2611120258-runtime-safety-fixes-plan.md (as one pair)
> **Related:** 2611120250-framework-expansion-synthesis-eval.md (upstream)

Evidence base: read-only re-inspection of both plans (mtimes 03:07 / 03:08, both before the red team at 05:04 → no plan revision since attack), `src/Core/{PackLoader,Builtins,ExpeditionRun,ExpeditionRun.Api,Model}.cs`, `src/Game/{GameBridge,PackRegistry}.cs`, `tests/Sim/Program.cs`, `README.md`, framework/sample/NER JSON, `git status`. No code or plan edited. Sims not run.

**Round state:** settled — 17 holding findings answered (F2, F5 by the human via orchestrator; rest by orchestrator as projex user).

---

## Bottom Line

> The pair survives its red team with no redesign. 17 of 21 records hold; 4 lapsed (red team's own validated challenges). Dispositions: 14 Must Fix (one split with an Accept part), 3 Accept Risk, 0 Refuted. Only F1 breaks a plan as written — loader Step 3's read tracking fails its own V1/V6 gate on the framework's `{"x": true}` idiom; a one-rule exemption fixes it without losing SC5. Every Must Fix is a plan-text amendment to unexecuted plans (revise-projex), preceded by the human-approved full-tree first commit (F2) and recorded decisions (F5: SC8 fixed, gameplay changes accepted; both other defaults accepted). Execution order fixed: runtime plan, then loader plan.

**Verdict:** Fix Then Proceed

**Top Residual Risks:**
1. SC8 fix ships accepted but unversioned gameplay changes in NER (no git): free searches fire out of combat; sample `cellar_nerves` morale and framework `punch` go live.
2. Warning noise: duplicated/misattributed warnings (F9); false warnings after a game update until `Schema` refreshed (F21).
3. C#-side latent: `IRun.Set` can shadow engine vars (F7); C# packs calling `ISpec.Warn` against an older loaded Api.dll (F12).

---

## Finding Ledger

| ID | Finding | Severity | Screen | Disposition |
| --- | --- | --- | --- | --- |
| F1 | SC5 flags the framework's own `{"x": true}` no-arg form | High | Holds | Must Fix |
| F2 | Phase 0 scope undefined; rollback depends on code being tracked | Medium | Holds | Must Fix |
| F3 | SC4 logs a false "could not give" line for empty item ids | Medium | Holds | Must Fix |
| F4 | An error plus unread-key warnings for one mistake | Medium | Holds | Must Fix |
| F5 | Plans `Ready` while blockers open; SC8 default ships NER gameplay change | Medium | Holds | Must Fix |
| F6 | Lint can't load a pack's non-framework dependencies | Low | Holds | Must Fix (narrow claim; `--with` deferred) |
| F7 | Engine-var guard leaves two writers uncovered | Low | Holds | Must Fix (`area.vars`) | Accept Risk (`IRun.Set`) |
| F8 | Render-time dice: `chance` gates always open, high-roll gates always closed | Low | Holds | Must Fix (README wording) |
| F9 | Warning duplication and blame | Low | Holds | Accept Risk |
| F10 | Verification gaps | Low | Holds | Must Fix |
| F11 | `KnownItemTags` counts `item_stats` `#key` as defining a tag | Low | Holds | Must Fix |
| F12 | `ISpec.Warn` vs packs shipping their own Api.dll | Low | Holds | Must Fix (cast in Core + README) |
| F13 | `spawn` "outside a fight" line during fight teardown | Low | Holds | Accept Risk |
| F14 | Assumption challenge: A1 "no runtime-closure Spec reads" is not the hazard that matters | — (unrated in source) | Holds | Must Fix (via F1 + F4) |
| F15 | Assumption challenge: planning survey (0 wrong-typed / tones / #tags; key sets valid) | — | Lapsed | — |
| F16 | Assumption challenge: NER RNG sequence unchanged | — | Lapsed | — |
| F17 | Assumption challenge: `spawn` guard matches engine semantics | — | Lapsed | — |
| F18 | Assumption challenge: `KnownItemTags` uses `ItemGroups.Values` | — | Lapsed | — |
| F19 | Edge case: `{"not": null}` / `{"not": {}}` | — (Recovery: Possible) | Holds | Must Fix |
| F20 | Edge case: sequential execution in either order | — (Recovery: Possible) | Holds | Must Fix (order: runtime → loader) |
| F21 | Monitor item: `Schema` vs `src/Game` drift (tones, engine item tags) | — | Holds | Accept Risk |

---

## Screened Out

Source graded these four challenges "Validate (holds)" — the subject's assumption survived the attack. No attack remains to defend; recorded `Lapsed` (attacked weakness does not exist). Spot-checked:

### F15: Planning survey — 0 wrong-typed, 0 tones, 0 #tags, key sets valid
**Screen:** Lapsed
**Evidence:** Source § Role-Based Assumption Challenges re-verified independently (interaction/choice keys ⊂ Interaction shape; tones ∈ {good, bad, dim, enemy, heading, hint}; no unknown `#tags`). Counter-evidence: none. Plans unchanged since.

### F16: NER RNG sequence and behavior unchanged
**Screen:** Lapsed
**Evidence:** Source: NER `variants` only in `say`; every NER `dice(`/`chance` inside effects, none in `when`/`where`/HUD. Consistent with runtime plan Risks ("No shipped content does this (planning grep)").

### F17: `spawn` guard matches engine semantics
**Screen:** Lapsed
**Evidence:** `ExpeditionRun.cs` L559 `Scene = "combat";` precedes `prepare?.Invoke(...)` L565. Spawn sites: NER `combat.json` L110 (in-combat `call_for_help`), sample `main.json` L32 (inside intent `call_help` `do` — in-combat). Neither hits the new `else` path.

### F18: `KnownItemTags` uses `ItemGroups.Values`
**Screen:** Lapsed
**Evidence:** `Model.cs` L362 `content.ItemGroups.TryGetValue(Id, ...)` — keyed by item id, values = group names → `.Values.SelectMany` yields group names as the plan intends.

---

## Defenses

Ordered by severity: High → Medium → Low → unrated.

### F1: SC5 flags the framework's own idiomatic `{"x": true}` no-argument form

**Source:** Finding 1 | **Severity:** High | **Screen:** Holds

**Attack restated:** Scalar args get wrapped as `{value: x}`; no-arg built-ins never read `value` → `ReportUnread` warns on `{"next_room": true}` etc. → loader V1 (`Fail("warning: …")`) and V6 (`--strict`) fail at Step 3, and the only prescribed fallback reverts SC5.

**Evidence checked:**
- `PackLoader.cs` L1471 `Args = args as Node ?? new Node { { "value", args } };` — confirmed.
- `Builtins.cs` L99 `fits`, L362–365 `end_event`/`stay`/`next_room`/`retreat`, L526 `enemy_turn`, L599 `reload`, L677 `clear_ground`, L695 `lose_found` — none read `value`. `intent_interruptible` L94 reads only `who`.
- Live sites (grep): framework `ExpeditionFramework/main.json` L119 `next_room: true`, L157 `fits: true`, L159 `reload: true`; sample `main.json` L109 `intent_interruptible: true`; NER `kit.json` L13, L174 `fits: true`. 6 sites, exact match with source.
- Loader plan Step 3 (L305–388): no mention of `true`/scalar exemption; test "C# effect reading nothing given scalar `{t:ping: 3}` → warning at `.value`" (L377). "If this fails" (L388) = fix a Mark list (n/a — these compilers call no accessor) or revert tracking.
- Compensating control? Step 7 triage routes NER false positives back to framework — but V1 is framework+sample and fails first. None effective.

**Recommended disposition:** Must Fix

**Defense:** None holds. Fix is small and keeps SC5 intact: `Spec` ctor records when it synthesized `value` from scalar `true`; `ReportUnread` skips that one key. Other scalars (`{"next_room": 3}`, `false`, strings) still warn, so plan's `{t:ping: 3}` test stays valid. Explicit `{"value": true}` object form is not exempted (author wrote the key). Add tests: `next_room: true`, `fits: true`, `intent_interruptible: true` → 0 warnings; `{t:ping: 3}` → 1. Rejected alternative: `MarkAllRead()` in no-arg built-ins — hides `{"next_room": {"typo": 1}}`, and does nothing for C# no-arg effects.

**Question:** F1 (High) — The loader plan's Step 3 makes every unread effect/condition key a warning. Scalars like `true` are wrapped as `{value: true}`, and no-arg built-ins (`next_room`, `fits`, `reload`, `intent_interruptible`, …) never read `value`, so the framework's own content (3 sites in `ExpeditionFramework/main.json`, 1 in the sample) and NER's `kit.json` (2 `fits: true`) would each warn. The plan's own gate V1 treats any framework/sample warning as a failure, so Step 3 fails as written and its only fallback is reverting the whole read-tracking feature. Recommendation: Must Fix — amend loader Step 3 (via revise-projex) so a `value` key the `Spec` synthesized from scalar `true` is exempt from `ReportUnread`; every other scalar still warns; add the three 0-warning tests plus keep the `{t:ping: 3}` → 1-warning test. Confirm Must Fix with this rule, or choose a different rule (e.g. exempt all synthesized scalars, or `MarkAllRead()` in no-arg built-ins)?

**User's answer:**
> Orchestrator (as projex user): "accept your recommended disposition and defense for every other item as written — F1 (exempt Spec-created `value` from scalar `true` only; reject MarkAllRead), F3, F4, F6, F7 (split), F8, F9 Accept Risk, F10, F11, F12, F13 Accept Risk, F14, F19, F20 (runtime plan first, then loader), F21 Accept Risk. Defense Plan order as you proposed."

**Disposition:** Must Fix

**Follow-up:** revise-projex on 2611120258-loader-loud-failures-plan.md Step 3 (rule + tests).

---

### F2: Phase 0 scope undefined; rollback depends on code being tracked

**Source:** Finding 2 | **Severity:** Medium | **Screen:** Holds | **Decision owner:** HUMAN (reserved)

**Attack restated:** Repo has zero commits; both plans say only "framework repo's first commit". A plans-only commit leaves `src/`, `tests/`, `README.md` untracked → rollback (`checkout <base> -- <files>` / `projex-abandon`) can't restore code, and `Worktree: No` means edits land in the only copy.

**Evidence checked:**
- `git status`: "your current branch 'main' does not have any commits yet"; untracked: `.gitignore`, `.projex/`, `ExpeditionFramework.csproj`, `ExpeditionFramework/`, `Localization/`, `README.md`, `api/`, `manifest.xml`, `samples/`, `src/`, `tests/`.
- `git status -uall`: 43 files, all text (`.cs`/`.json`/`.md`/`.csv`/`.xml`/`.csproj`/`.gitignore`); no binaries. `.gitignore` = `bin/`, `obj/`, `*.user`, `.vs/`.
- Loader plan L84 / runtime plan L69: "Requires: framework repo's first commit (Phase 0 …; human has not decided yet)". Loader Rollback L581–583, runtime Rollback L351–352 both assume code at `<base>`.
- Both plans `Worktree: No` (L8) — justified (NER sim compiles `../../../ExpeditionFramework/src/Core`).

**Recommended disposition:** Must Fix

**Defense:** None — the plans state the prerequisite but not its scope. The full tree is small (43 text files), already has a sane `.gitignore`, contains no secrets-looking or binary content by type, so a full-tree first commit costs nothing and is the only scope that makes the Rollback Plans true. Add to both plans' Dependencies: precondition `git -C <repo-root> ls-files src/Core/PackLoader.cs tests/Sim/Program.cs README.md` lists all three.

**Question:** F2 (Medium) — HUMAN DECISION. The framework repo is on `main` with zero commits; all 43 files (source, tests, API, README, packs, localization, the `.projex/` docs) are untracked. Both plans require "the first commit" but never say what goes in it, while their rollback steps (`git checkout <base> -- <files>`, `projex-abandon`) only work if the code itself is committed. If only the plans are committed, a failed execution can't be rolled back and edits land directly in your only copy (worktree mode is off for a valid reason: NER's sim compiles the framework source by relative path). Recommendation: Must Fix — make the first commit the full tree (`.gitignore` already excludes `bin/`, `obj/`, `*.user`, `.vs/`; nothing binary is untracked), plus both plans; and add an `ls-files` precondition to both plans. Do you approve a full-tree first commit (and who runs it — you, or the orchestrator on your go-ahead)? If not full-tree, what should it contain?

**User's answer:**
> Human (via orchestrator AskUserQuestion): "Full tree, you commit (Recommended)" — orchestrator note: full-tree first commit (all 43 text files + .projex/) on `main`, made by the orchestrator after this blue team doc is complete; Must Fix incl. the `ls-files` precondition in both plans.

**Disposition:** Must Fix

**Follow-up:** human decision → Phase 0 commit; revise-projex both plans' Dependencies (precondition line).

---

### F3: SC4 logs a false "could not give" line for empty item ids

**Source:** Finding 3 | **Severity:** Medium | **Screen:** Holds

**Attack restated:** Guard `itemId != null && name == null` misfires on `""`, which `GameBridge.GiveItem` already treats as "nothing to give" → player sees `could not give  (unknown item id, …)`.

**Evidence checked:**
- `GameBridge.cs` L62 `GiveItem(string itemId) => PutOnGround(string.IsNullOrEmpty(itemId) ? null : ItemSpawner.Spawn(itemId));` — `""` → null name.
- `ExpeditionRun.Api.cs` L138 current guard `itemId != null`; runtime plan Step 4 L239 keeps `itemId != null` in the new log condition.
- `""` sources: `ExpeditionRun.cs` L665 `Bridge.RollTable(entry.GameTable)` → `GameBridge.cs` L74 `LootRegistry.Roll*` — whether it can return `""` is unverifiable headless. Interceptor blanking also possible.
- Plan's own rationale (L246): "A null `itemId` (empty loot roll) stays silent" — intent already says empty rolls are silent; code just doesn't match it for `""`.

**Recommended disposition:** Must Fix

**Defense:** Likelihood is uncertain, but the fix is one token and aligns the guard with the plan's stated intent and `GiveItem`'s own contract: `!string.IsNullOrEmpty(itemId) && name == null`. Add sim check `Give("")` → no new log line.

**Question:** F3 (Medium) — Runtime plan Step 4 logs "could not give <id>" when the game returns no item. Its guard is `itemId != null`, but `GameBridge.GiveItem` already treats an empty string as "nothing to give", so an empty roll from a game loot table (or a `loot` interceptor that blanks the id) would print a misleading `could not give  (unknown item id, or no room on the ground)` to the player. The plan's own rationale says empty rolls should stay silent. Recommendation: Must Fix — change the guard to `!string.IsNullOrEmpty(itemId) && name == null` and add a `Give("")` → no log line check. Confirm?

**User's answer:**
> Orchestrator (as projex user): "accept your recommended disposition and defense for every other item as written — F1 (exempt Spec-created `value` from scalar `true` only; reject MarkAllRead), F3, F4, F6, F7 (split), F8, F9 Accept Risk, F10, F11, F12, F13 Accept Risk, F14, F19, F20 (runtime plan first, then loader), F21 Accept Risk. Defense Plan order as you proposed."

**Disposition:** Must Fix

**Follow-up:** revise-projex on 2611120258-runtime-safety-fixes-plan.md Step 4.

---

### F4: An error plus unread-key warnings for one mistake

**Source:** Finding 4 | **Severity:** Medium | **Screen:** Holds

**Attack restated:** Compilers that `Error` then `return s => { }` before reading remaining keys leave them unread → `ReportUnread` adds a false "not used here" warning beside the real problem.

**Evidence checked:**
- `Builtins.cs` L311–323 `Assign`: on bad `var` → `c.Error(...)`; `return s => { };` before `c.Value("value", 0)` → `value` unread.
- `Builtins.cs` L240–246 `set_text`: same shape (error before `line`/`value`).
- Runtime plan Step 2 L162–166: new engine-var guard errors + returns before `c.Value("value")`.
- Loader plan Step 3 L356: only `CompileSharedCall` gets `MarkAllRead()`; L378 test covers only the shared-call double report.

**Recommended disposition:** Must Fix

**Defense:** None in the plans. Fix is general and covers C# compilers too: `Spec.Error()` sets `errored`; `ReportUnread` returns early for an errored spec (children still report on their own). Test `{set: {var: "bogus", value: 1}}` → 1 problem, 0 warnings. With that rule, the `CompileSharedCall` special case could also go, but keeping it is harmless.

**Question:** F4 (Medium) — Several compilers (`set`/`add` via `Builtins.Assign`, `set_text`, and the runtime plan's new engine-var guard) report an error and return early, before reading their remaining keys. Once read tracking lands, each such mistake produces the real problem plus a false "`value` not used here" warning, which reads like a second mistake. The plan only de-duplicates shared-effect calls. Recommendation: Must Fix — amend loader Step 3 so `Spec.Error()` marks the spec errored and `ReportUnread` skips errored specs; add test `{set: {var: "bogus", value: 1}}` → 1 problem, 0 warnings. Confirm?

**User's answer:**
> Orchestrator (as projex user): "accept your recommended disposition and defense for every other item as written — F1 (exempt Spec-created `value` from scalar `true` only; reject MarkAllRead), F3, F4, F6, F7 (split), F8, F9 Accept Risk, F10, F11, F12, F13 Accept Risk, F14, F19, F20 (runtime plan first, then loader), F21 Accept Risk. Defense Plan order as you proposed."

**Disposition:** Must Fix

**Follow-up:** revise-projex on loader plan Step 3 (see F20 for cross-plan test placement).

---

### F5: Plans `Ready` while their blockers are open; SC8 default ships a NER gameplay change

**Source:** Finding 5 | **Severity:** Medium | **Screen:** Holds | **Decision owner:** HUMAN (reserved — SC8 fix vs warn-only)

**Attack restated:** Both plans say `Ready` though Phase 0 and default-taken decisions are unresolved; SC8 (`not`/`carrying` object form) defaults to "fix", which changes NER balance with no VCS/release in NER.

**Evidence checked:**
- Loader L3, runtime L3: `> **Status:** Ready`. Loader L601–604 "Decisions taken by default (user may override before execution)": SC8 fixed, wrong-typed scalars = problems, `weight`/`when`/`tags` restriction.
- `Builtins.cs` L74 `not` reads only `value`; `PackLoader.cs` L1484–1486 `Condition(key)` returns `s => true` when the key is absent → object-form `not` is always **false** today.
- Live object-form sites (grep — one more than source's 3): framework `main.json` L188 `{"not": {"carrying": {"all": [...]}}}` (both `not` and `carrying`, `punch`); sample `main.json` L215 `{"not": {"has_tag": {"tag": "boss"}}}` (kit `cellar_nerves` `defeated` morale `where` → today never matches, so `flee` never fires; after fix: non-boss humans flee at 25%); NER `rules.json` L352 `{"not": {"scene": "combat"}}` → after fix, `run.searches += run.free_searches` + `ne_free_search` say fires out of combat.
- NER: not a git repo → no rollback for its balance besides reverting the framework.
- Compensating control: orchestrator brief holds these as reserved decisions → nothing executes before the human answers. That control is procedural, not in the documents.

**Recommended disposition:** Must Fix

**Defense:** Partial — the orchestrator already gates execution on the human. What's missing is in the documents: decisions aren't recorded, and SC8 is fused into Step 3 so warn-only can't be chosen without editing the step. Fix: (1) after the human answers, revise both plans to record Phase 0 + each decision (Status stays `Ready` only once recorded; until then `Blocked`). (2) Split SC8 out of Step 3 into its own step with two variants: fix (current code) or warn-only (`not`/`carrying` with no `value` key → warning "takes its condition under value"; behavior unchanged). Recommendation on the gameplay choice itself is the human's; my lean is **fix** — the NER `if` was clearly written to run out of combat (it's inside a `when` that checks `free_searches > 0`), so today's behavior is the bug, but it does change NER balance and the sample's morale behavior (which V1 may observe), so it must be a deliberate call.

**Question:** F5 (Medium) — HUMAN DECISION. Both plans are marked `Ready` while Phase 0 (F2) and three "decisions taken by default" are still open. The biggest is SC8: `not` and `carrying` currently ignore the object form (`{"not": {"scene": "combat"}}`) and silently evaluate wrong. Fixing it changes live behavior: in NightExpeditionsReborn `rules.json` L352, free searches (`run.searches += run.free_searches` + the "free search" line) will start firing outside combat; the framework kit's `punch` (`main.json` L188) becomes offerable to kits that include `expedition_framework:melee` (the sample does; NER's kits don't); the sample's `cellar_nerves` morale rule (L215), which today never fires, starts making non-boss humans flee 25% of the time when an ally falls. NER has no git, so the balance change can't be versioned there. Options: (a) fix as planned (my lean — today's behavior is clearly unintended), (b) warn-only — loader warns when `not`/`carrying` gets an object without `value`, behavior unchanged, NER author fixes data later. Recommendation regardless of (a)/(b): Must Fix the documents — split SC8 into its own step so the choice is a switch, record all decisions in the plans, and hold Status at `Blocked` until recorded. Which do you choose for SC8 — fix or warn-only? And do you accept the other two defaults (wrong-typed scalars are load problems; `weight`/`when`/`tags` outside rooms/events/encounters/enemies only warn)?

**User's answer:**
> Human (via orchestrator AskUserQuestion): SC8 — "Fix (Recommended)"; other two defaults — "Accept both (Recommended)". Orchestrator note: gameplay changes (NER free searches out of combat, framework `punch` offerable, sample morale rules incl. `cellar_nerves` firing) accepted; still split SC8 into its own step and record decisions in both plans; Status gating to `Blocked` until recorded is fine.

**Disposition:** Must Fix

**Follow-up:** human decision → revise-projex both plans (decisions + Status; SC8 split).

---

### F6: Lint can't load a pack's non-framework dependencies

**Source:** Finding 6 | **Severity:** Low | **Screen:** Holds

**Attack restated:** Lint adds only framework + target; `--mod` toggles `HasMod` but loads nothing → a NER add-on gets "no … named" errors, contradicting "checks any pack against the framework".

**Evidence checked:** Loader plan Step 5 L435 objective "checks any pack against the framework"; L468 step 3 adds framework + folder only. No add-on packs exist today (only framework, sample, NER).

**Recommended disposition:** Must Fix (narrow the claim); defer `--with`

**Defense:** No consumer is harmed today — every existing pack depends only on the framework. The defect is the claim, not the tool. Narrow SC11/Step 5 objective/README to "checks a pack that depends only on the framework"; record `--with <folder>` as a follow-up when the first add-on appears. Rejected: building `--with` now — speculative scope.

**Question:** F6 (Low) — Loader Step 5's lint loads only the framework plus the target folder, so a pack that builds on NER (or any non-framework pack) would get false "no … named" errors. No such pack exists today. Recommendation: Must Fix the wording only — narrow the objective/SC11/README to "a pack that depends only on the framework" — and defer a `--with <folder>` option until the first add-on pack exists. Confirm, or do you want `--with` built now?

**User's answer:**
> Orchestrator (as projex user): "accept your recommended disposition and defense for every other item as written — F1 (exempt Spec-created `value` from scalar `true` only; reject MarkAllRead), F3, F4, F6, F7 (split), F8, F9 Accept Risk, F10, F11, F12, F13 Accept Risk, F14, F19, F20 (runtime plan first, then loader), F21 Accept Risk. Defense Plan order as you proposed."

**Disposition:** Must Fix (narrow claim; `--with` deferred)

**Follow-up:** revise-projex loader plan Step 5 + Step 6 README wording.

---

### F7: Engine-var guard leaves two writers uncovered

**Source:** Finding 7 | **Severity:** Low | **Screen:** Holds

**Attack restated:** `area.vars` writes straight into `vars` (`Begin`), and C# `IRun.Set` is unguarded → engine names can still be silently shadowed; README sentence omits `vars`.

**Evidence checked:** `ExpeditionRun.cs` `Begin()` L265 `foreach (... area.Vars) vars[variable.Key] = ...` — no check. `ExpeditionRun.cs` L333 `public void Set(string variable, int value) => vars[variable] = value;` — no check. Runtime plan Step 2 guards only `Builtins.Assign`; L169 test even builds an area with `vars` setting `alarm`.

**Recommended disposition:** Must Fix (`area.vars`) | Accept Risk (`IRun.Set`)

**Defense:** `area.vars` is pack data, same audience as `set`/`add` → same load problem, checked in `CompileArea` against `ExpeditionRun.EngineVars`; test `areas.a.vars {turn: 3}` → 1 problem. `IRun.Set` is the C# API: the engine itself calls `Set("won", …)` etc. on non-engine names, and guarding it means a throw or log at run time for C# authors — out of proportion for a no-op shadow; document instead ("names the engine keeps are ignored by `Set`").

**Question:** F7 (Low) — Runtime Step 2 makes `set`/`add` on engine-kept names (`run.turn`, `run.depth`, …) a load problem, but an area's `vars` block can still set them (silently ignored), and C# `IRun.Set` can too. Recommendation: Must Fix the data path — add the same check to `CompileArea` for `vars` keys plus a test, and mention `vars` in the README sentence; Accept Risk for C# `IRun.Set` (document only — a runtime guard there is heavier than the harm). Confirm?

**User's answer:**
> Orchestrator (as projex user): "accept your recommended disposition and defense for every other item as written — F1 (exempt Spec-created `value` from scalar `true` only; reject MarkAllRead), F3, F4, F6, F7 (split), F8, F9 Accept Risk, F10, F11, F12, F13 Accept Risk, F14, F19, F20 (runtime plan first, then loader), F21 Accept Risk. Defense Plan order as you proposed."

**Disposition:** Must Fix (`area.vars`) | Accept Risk (`IRun.Set`)

**Follow-up:** revise-projex runtime plan Step 2 + Step 6.

---

### F8: Render-time dice make `chance` gates always open and dice-threshold gates always closed

**Source:** Finding 8 | **Severity:** Low | **Screen:** Holds

**Attack restated:** Under rendering `Roll` = 1 → `chance N` (N ≥ 1) always true, `dice(1,6) >= 4` always false; `Perform` doesn't recheck `when` → offer-time value is the only gate.

**Evidence checked:** Runtime plan Step 5 L272 `rendering > 0 ? 1 : roll(sides)`. `ExpeditionRun.cs` L845 `if (i.When != null && !i.When(scope)) return false;` is in option building; `Perform` L858–879 never evaluates `i.When`. Shipped content: no dice in `when`/`where`/HUD (F16). Runtime plan Risks L359 acknowledges "as if all dice rolled 1" but not the direction per gate type.

**Recommended disposition:** Must Fix (README wording only)

**Defense:** No shipped content affected; deterministic render (a planned improvement over per-refresh flicker) is the right model. The only gap is authors not knowing the consequence. Step 6 already writes the "dice count as 1" sentence; extend it: "so a `when` using `chance` always shows and one needing a high roll never shows — roll into a `run.` var when entering the room instead." Rejected: load-time warning on dice in `when` — new code for a hazard no content has.

**Question:** F8 (Low) — Runtime Step 5 makes all dice read as 1 while buttons/HUD are drawn (good: no flicker, no wasted randomness). Side effect: a button gated by `chance` always shows, and one needing a high roll never shows, and `Perform` doesn't recheck the gate. No shipped content does this. Recommendation: Must Fix the docs only — extend the planned README sentence to spell out that asymmetry and the "roll into a `run.` var on room entry" pattern; no load-time warning. Confirm?

**User's answer:**
> Orchestrator (as projex user): "accept your recommended disposition and defense for every other item as written — F1 (exempt Spec-created `value` from scalar `true` only; reject MarkAllRead), F3, F4, F6, F7 (split), F8, F9 Accept Risk, F10, F11, F12, F13 Accept Risk, F14, F19, F20 (runtime plan first, then loader), F21 Accept Risk. Defense Plan order as you proposed."

**Disposition:** Must Fix (README wording)

**Follow-up:** revise-projex runtime plan Step 6.

---

### F9: Warning duplication and blame

**Source:** Finding 9 | **Severity:** Low | **Screen:** Holds

**Attack restated:** `extends`/`use` merge before `CheckShape` → one typo warns per consumer; patch-introduced keys are warned under the target pack.

**Evidence checked:** Loader plan Risks L592 acknowledges patch blame ("acceptable"). Source states warnings never void a pack or reject a patch — consistent with SC1 (L34).

**Recommended disposition:** Accept Risk

**Defense:** Cosmetic: warnings don't affect loading, patch acceptance, or gameplay. Plan already accepts the blame half. De-dup by message text in `WarningsByPack` is cheap but can be added when logs actually get noisy. Reversal signal: a pack author reports duplicate/misattributed warnings, or NER triage (Step 7) shows ≥ N duplicates.

**Question:** F9 (Low) — With templates (`extends`) and shared interactions (`use`) merged before checking, one typo warns once per consumer; and keys added by another pack's `patch` are warned under the patched pack's name. Warnings never block loading, and the plan already accepts the blame part. Recommendation: Accept Risk — revisit (add per-pack message de-dup) if Step 7's NER triage or players' logs show noisy duplicates. Confirm?

**User's answer:**
> Orchestrator (as projex user): "accept your recommended disposition and defense for every other item as written — F1 (exempt Spec-created `value` from scalar `true` only; reject MarkAllRead), F3, F4, F6, F7 (split), F8, F9 Accept Risk, F10, F11, F12, F13 Accept Risk, F14, F19, F20 (runtime plan first, then loader), F21 Accept Risk. Defense Plan order as you proposed."

**Disposition:** Accept Risk

**Follow-up:** none (monitor via Step 7 triage).

---

### F10: Verification gaps

**Source:** Finding 10 | **Severity:** Low | **Screen:** Holds

**Attack restated:** Runtime V4 probes `rooms_left`/`scheduled` already pass; V5 matches any `finally`; Step 1 test note cites `FightForeman` which uses `new Registry()`; NER sim never prints warnings.

**Evidence checked:**
- `grep -c rooms_left README.md` = 1, `grep -c scheduled README.md` = 1 → two V4 probes vacuous today.
- `grep -c finally src/Core/PackLoader.cs` = 0 today → V5 is discriminating now, but only weakly (any future `finally` passes).
- `Program.cs` L246 `var loader = new PackLoader(content, new Registry());` in `FightForeman`; runtime Step 1 L135 "(as `FightForeman` does)" — confirmed misleading (test needs the registry that has `t:boom`/`t:risky`).
- Loader V2 = NER sim exit 0 only; V7 (lint NER) prints warnings.

**Recommended disposition:** Must Fix

**Defense:** All are plan-text edits: V4 probes → new phrases (`kept by the engine`, `ground` and `scheduled`); V5 → rely on `TestSharedEffectSurvivesThrow` (drop or narrow the grep); drop the `FightForeman` parenthetical, say "with the same `registry`"; name V7 as SC10's evidence.

**Question:** F10 (Low) — Some verification steps can't catch what they claim: two of runtime V4's README probes (`rooms_left`, `scheduled`) already match today; V5 passes on any `finally`; runtime Step 1's test note says "as `FightForeman` does", but `FightForeman` builds a fresh empty registry, so copying it would fail with "unknown effect"; and the NER sim never shows warnings, so SC10's triage really depends on V7. Recommendation: Must Fix — reword the probes, drop/narrow the V5 grep, fix the test note, and name V7 as SC10 evidence. Confirm?

**User's answer:**
> Orchestrator (as projex user): "accept your recommended disposition and defense for every other item as written — F1 (exempt Spec-created `value` from scalar `true` only; reject MarkAllRead), F3, F4, F6, F7 (split), F8, F9 Accept Risk, F10, F11, F12, F13 Accept Risk, F14, F19, F20 (runtime plan first, then loader), F21 Accept Risk. Defense Plan order as you proposed."

**Disposition:** Must Fix

**Follow-up:** revise-projex both plans' Verification Plan + runtime Step 1.

---

### F11: `KnownItemTags` counts `item_stats` `#key` as defining a tag

**Source:** Finding 11 | **Severity:** Low | **Screen:** Holds

**Attack restated:** `item_stats["#tag"]` consumes a tag; counting it as a definer lets a typo repeated in filter + stats silence itself.

**Evidence checked:** `Model.cs` L360–368 `ApplyStats`: groups → `Tags.UnionWith`; `#tag` stats applied only `foreach (string tag in Tags)` already present → consumer. Loader plan Step 4 L417 includes `ItemStats.Keys.Where(k => k.StartsWith("#"))` in the known set. Plan's test L423 (`item_stats {"#loud": {tags ["noisy"]}}` + `#noisy`) relies on `s.Tags` (L416), not the `#key` line → unaffected by removal.

**Recommended disposition:** Must Fix

**Defense:** One line removed; test set unaffected. Optional extra (not recommended now): warn when an `item_stats` `#key` names an unknown tag — same `KnownItemTags` set, cheap, but new scope.

**Question:** F11 (Low) — Loader Step 4's set of "known" item tags includes `item_stats` keys starting with `#`. But `item_stats["#loud"]` only applies stats to items that already have tag `loud`; it never gives the tag. So the same typo in a filter and an `item_stats` key would hide itself. Recommendation: Must Fix — drop that line from `KnownItemTags` (the plan's tests still pass); don't add the extra `item_stats` `#key` warning now. Confirm, or include that extra warning?

**User's answer:**
> Orchestrator (as projex user): "accept your recommended disposition and defense for every other item as written — F1 (exempt Spec-created `value` from scalar `true` only; reject MarkAllRead), F3, F4, F6, F7 (split), F8, F9 Accept Risk, F10, F11, F12, F13 Accept Risk, F14, F19, F20 (runtime plan first, then loader), F21 Accept Risk. Defense Plan order as you proposed."

**Disposition:** Must Fix

**Follow-up:** revise-projex loader plan Step 4.

---

### F12: `ISpec.Warn` versus packs that ship their own Api.dll

**Source:** Finding 12 | **Severity:** Low | **Screen:** Holds

**Attack restated:** A pack's older `ExpeditionFramework.Api.dll` copy loading first → `say` calling `c.Warn(...)` through `ISpec` throws `MissingMethodException` at compile.

**Evidence checked:** `PackRegistry.cs` L72 comment "A pack's listed ExpeditionFramework.Api.dll has no IMod, so it creates no entry with an instance." → packs do list their own Api.dll. Loader plan L369 `say`: `c.Warn(...)` on `ISpec`; L365/368 already cast `((PackLoader.Spec)c)` for `WholeCondition`/`WholeItems`. No C# packs exist today.

**Recommended disposition:** Must Fix (cast in Core) + document append-only `ISpec`

**Defense:** Core calling through the concrete `PackLoader.Spec` removes Core's dependency on the new interface member at zero cost and matches the plan's existing cast pattern. The C#-pack side (their code calling `Warn` against an older loaded Api) is inherent to shipping copies; document that the framework's Api.dll must win and `ISpec` is append-only.

**Question:** F12 (Low) — The loader plan adds `Warn` to the public `ISpec` interface and has the built-in `say` call it through `ISpec`. C# packs ship their own copy of the Api DLL; if an older copy is the one loaded, that call throws at load. No C# packs exist yet. Recommendation: Must Fix — have Core call `((PackLoader.Spec)c).Warn(...)` (same cast the plan already uses for `not`/`carrying`), and add a README line that `ISpec` only grows and the framework's Api.dll must be the one loaded. Confirm?

**User's answer:**
> Orchestrator (as projex user): "accept your recommended disposition and defense for every other item as written — F1 (exempt Spec-created `value` from scalar `true` only; reject MarkAllRead), F3, F4, F6, F7 (split), F8, F9 Accept Risk, F10, F11, F12, F13 Accept Risk, F14, F19, F20 (runtime plan first, then loader), F21 Accept Risk. Defense Plan order as you proposed."

**Disposition:** Must Fix (cast in Core + README)

**Follow-up:** revise-projex loader plan Step 3 (`say`) + Step 6.

---

### F13: `spawn` "outside a fight" line during fight teardown

**Source:** Finding 13 | **Severity:** Low | **Screen:** Holds

**Attack restated:** An intent `do` that ends the fight then hits a later `spawn` → player sees new English "spawn does nothing outside a fight" line.

**Evidence checked:** `ExpeditionRun.cs` L602 `Scene = "room";` in `EndFight`. Runtime plan Step 3 L202 logs `bad` line then runs `else`. Live spawn sites (F17): NER `combat.json` L110, sample `main.json` L32 — neither ends the fight before `spawn` in the same list. Runtime Risks L361 accepts English-only `bad` lines.

**Recommended disposition:** Accept Risk

**Defense:** No content triggers it; when it does fire, the line is accurate (the spawn really did nothing) and points the author to a real ordering mistake. Reversal signal: player/author report of the line in normal play.

**Question:** F13 (Low) — Runtime Step 3 makes `spawn` outside a fight run its `else` and log "spawn does nothing outside a fight". If an enemy's action ends the fight and a later `spawn` in the same list runs, players see that line. No shipped content does this, and the line is truthful. Recommendation: Accept Risk — revisit on a report. Confirm?

**User's answer:**
> Orchestrator (as projex user): "accept your recommended disposition and defense for every other item as written — F1 (exempt Spec-created `value` from scalar `true` only; reject MarkAllRead), F3, F4, F6, F7 (split), F8, F9 Accept Risk, F10, F11, F12, F13 Accept Risk, F14, F19, F20 (runtime plan first, then loader), F21 Accept Risk. Defense Plan order as you proposed."

**Disposition:** Accept Risk

**Follow-up:** none.

---

### F14: A1 "no runtime-closure Spec reads" is not the hazard that matters

**Source:** Role-Based Assumption Challenge (Plan Executor) | **Severity:** — (unrated) | **Screen:** Holds

**Attack restated:** A1 checks only lazy reads; unread synthesized `value`s (F1) and reads skipped after errors (F4) are separate hazards that pass the planning survey. Action: "Relax — extend A1".

**Evidence checked:** Same as F1 and F4. Source itself confirms A1 holds for `Builtins.cs`.

**Recommended disposition:** Must Fix (resolved by F1 + F4)

**Defense:** No independent fix — extend A1's text to "unread keys are only real mistakes, given the `true`-scalar exemption (F1) and errored-spec skip (F4)". Falls out of the F1/F4 revisions.

**Question:** F14 — The red team notes the plan's assumption A1 ("compilers never read keys lazily") is true but isn't the only way read tracking can misfire; the other two are F1 (unread `true` scalars) and F4 (keys skipped after an error). Recommendation: Must Fix by widening A1's wording in the same revision that lands F1 and F4; no separate work. Confirm?

**User's answer:**
> Orchestrator (as projex user): "accept your recommended disposition and defense for every other item as written — F1 (exempt Spec-created `value` from scalar `true` only; reject MarkAllRead), F3, F4, F6, F7 (split), F8, F9 Accept Risk, F10, F11, F12, F13 Accept Risk, F14, F19, F20 (runtime plan first, then loader), F21 Accept Risk. Defense Plan order as you proposed."

**Disposition:** Must Fix (via F1 + F4)

**Follow-up:** folded into F1/F4 revise.

---

### F19: Edge case — `{"not": null}` / `{"not": {}}`

**Source:** Role-Specific Edge Case (Pack Author) | **Severity:** — (Recovery: Possible) | **Screen:** Holds

**Attack restated:** `null` → Args `{value: null}` → `Has("value")` false → `WholeCondition` → confusing problem `unknown condition "value"`.

**Evidence checked:** `PackLoader.cs` L1471 wraps null as `{value: null}`; L1474 `Has` false for null value; loader plan L365 falls to `WholeCondition()`. Plan test L379 covers `{not {}}` only.

**Recommended disposition:** Must Fix

**Defense:** Still a load problem (author is told), only the text is wrong. Fix inside code the plan is already writing: if `Args` is exactly `{value: null}` (or empty) → `c.Error("needs a condition")`. Same for `carrying` ("needs an item filter"). Add `{not: null}` to the existing test.

**Question:** F19 — With the planned `not`/`carrying` change, `{"not": null}` would be reported as `unknown condition "value"`, which confuses authors (it's still a load problem, just a misleading one). Recommendation: Must Fix — in the same Step 3 code, report "needs a condition" / "needs an item filter" when the argument is null or empty, and add `{not: null}` to the existing test. Confirm? (If you choose warn-only for SC8 in F5, this collapses into the warn path.)

**User's answer:**
> Orchestrator (as projex user): "accept your recommended disposition and defense for every other item as written — F1 (exempt Spec-created `value` from scalar `true` only; reject MarkAllRead), F3, F4, F6, F7 (split), F8, F9 Accept Risk, F10, F11, F12, F13 Accept Risk, F14, F19, F20 (runtime plan first, then loader), F21 Accept Risk. Defense Plan order as you proposed."

**Disposition:** Must Fix

**Follow-up:** revise-projex loader plan Step 3 (or the split SC8 step from F5).

---

### F20: Edge case — sequential execution in either order

**Source:** Role-Specific Edge Case (Plan Executor) | **Severity:** — (Recovery: Possible) | **Screen:** Holds

**Attack restated:** Edits don't conflict (`CompileSharedCall`, README adjacent sentences), but F4's double report only appears once both plans land → test belongs to whichever runs second.

**Evidence checked:** Runtime plan L92 "Order is arbitrary"; runtime Step 2 adds the engine-var error path; loader Step 3 adds tracking. Neither plan references the other's F4 interaction.

**Recommended disposition:** Must Fix

**Defense:** With F4's general rule (errored spec skips `ReportUnread`) placed in the loader plan, the only cross-plan piece is one test: `set run.turn` → 1 problem, 0 warnings. Fix the order instead of conditional placement: **runtime plan first, loader plan second**; loader Step 3 carries the F4 rule plus both tests (`set bogus` and `set run.turn`). Rationale: runtime plan is smaller, independent, and lands the `Assign` error path the loader test then exercises.

**Question:** F20 — The two plans can run in either order, but the error-plus-warning double report (F4) only appears once both have landed, so its test has no fixed home. Recommendation: Must Fix by fixing the order — execute the runtime plan first, then the loader plan, whose Step 3 carries F4's rule and the tests `{set: {var: "bogus"}}` and `{set: {var: "run.turn"}}` → 1 problem, 0 warnings. Confirm this order?

**User's answer:**
> Orchestrator (as projex user): "accept your recommended disposition and defense for every other item as written — F1 (exempt Spec-created `value` from scalar `true` only; reject MarkAllRead), F3, F4, F6, F7 (split), F8, F9 Accept Risk, F10, F11, F12, F13 Accept Risk, F14, F19, F20 (runtime plan first, then loader), F21 Accept Risk. Defense Plan order as you proposed."

**Disposition:** Must Fix (order: runtime → loader)

**Follow-up:** revise-projex both plans (ordering note; loader Step 3 test).

---

### F21: `Schema` vs `src/Game` drift (tones, engine item tags)

**Source:** Remediation § Monitor + Roles Not Attacked (Game updater) | **Severity:** — (Time) | **Screen:** Holds

**Attack restated:** `Schema.Tones`/`EngineItemTags` duplicate lists in `src/Game` (`ExpeditionLocation.ToneColors`, `ItemReader`); a game update adding a tag/tone leaves `Schema` stale → false warnings.

**Evidence checked:** Loader plan Risks L593 acknowledges; mitigation = comments naming owning symbols. Source "Solid" list: lists match today.

**Recommended disposition:** Accept Risk

**Defense:** Failure mode is a false *warning* (never a problem/void), and only after a game update. A cross-check test isn't possible headless (`src/Game` isn't compiled by the sim). Reversal signal: a new game version, or a warning naming a real game tag/tone.

**Question:** F21 — The loader plan copies the game's tone list and engine item tags into a new `Schema` file because the headless sim can't see `src/Game`. If a game update adds a tag or tone, `Schema` goes stale and authors get false warnings (never load failures). The plan already names the owning symbols in comments. Recommendation: Accept Risk — recheck `Schema` on each game update. Confirm?

**User's answer:**
> Orchestrator (as projex user): "accept your recommended disposition and defense for every other item as written — F1 (exempt Spec-created `value` from scalar `true` only; reject MarkAllRead), F3, F4, F6, F7 (split), F8, F9 Accept Risk, F10, F11, F12, F13 Accept Risk, F14, F19, F20 (runtime plan first, then loader), F21 Accept Risk. Defense Plan order as you proposed."

**Disposition:** Accept Risk

**Follow-up:** none (monitor on game update).

---

## Defense Plan

### Must Fix (order: severity + dependency)
1. F2 Full-tree first commit on `main` (43 text files + `.projex/`), by orchestrator after this doc; `ls-files` precondition added to both plans' Dependencies — orchestrator commit + revise-projex (both)
2. F5 Record decisions in both plans (SC8 = fix, gameplay deltas accepted; wrong-typed scalars = problems; misplaced `weight`/`when`/`tags` = warnings); split SC8 out of loader Step 3 into its own step; Status `Blocked` until recorded — revise-projex (both)
3. F20 Execution order: runtime plan first, then loader plan — revise-projex (both)
4. F1 `ReportUnread` exempts a `value` that `Spec` synthesized from scalar `true` only; tests `next_room`/`fits`/`intent_interruptible: true` → 0 warnings, `{t:ping: 3}` → 1; `MarkAllRead` alternative rejected — revise-projex (loader Step 3)
5. F4 `Spec.Error()` marks errored; `ReportUnread` skips errored specs; tests `set bogus` and `set run.turn` → 1 problem, 0 warnings — revise-projex (loader Step 3)
6. F14 Widen A1 wording to cover F1/F4 hazards — with 4–5
7. F19 null/empty `not`/`carrying` → "needs a condition" / "needs an item filter"; `{not: null}` test — revise-projex (SC8 step)
8. F12 Core `say` warns via `((PackLoader.Spec)c).Warn`; README: `ISpec` append-only, framework Api.dll must win — revise-projex (loader Steps 3, 6)
9. F11 Drop `item_stats` `#key` line from `KnownItemTags` — revise-projex (loader Step 4)
10. F3 Guard `!string.IsNullOrEmpty(itemId) && name == null`; `Give("")` silent check — revise-projex (runtime Step 4)
11. F7 `CompileArea` checks `vars` keys against `ExpeditionRun.EngineVars` (problem) + test; README mentions `vars` — revise-projex (runtime Steps 2, 6)
12. F10 V4 probes → new phrases; drop/narrow V5 grep; fix `FightForeman` note; V7 named as SC10 evidence — revise-projex (both)
13. F6 Narrow lint claim to "packs depending only on the framework"; `--with` deferred — revise-projex (loader Steps 5, 6)
14. F8 README: `chance` gates always show, high-roll gates never show; roll into a `run.` var on room entry — revise-projex (runtime Step 6)

### Mitigate
- F5 Plans held `Blocked` until decisions recorded; orchestrator gates execution meanwhile
- F7 `IRun.Set` documented as ignoring engine-kept names

### Accept & Monitor
- F9 Duplicate/misattributed warnings — reverse if Step 7 NER triage or author logs show noisy duplicates (→ per-pack message de-dup)
- F13 `spawn` teardown line — reverse on player/author report
- F21 `Schema` vs `src/Game` drift — recheck on each game update; reverse if a warning names a real game tag/tone
- F7 (C# part) `IRun.Set` shadowing — reverse when first C# pack writes engine names
- F6 (deferred part) — build `--with` when first add-on pack appears

---

## Residual Risk

After the defense plan lands:
- **Accepted gameplay change (F5):** NER free searches fire out of combat, framework `punch` offerable to melee kits, sample `cellar_nerves` morale fires. NER has no VCS, so the only rollback is reverting the framework commit. Bites if NER balance turns out wrong in play.
- **F9:** one typo warns once per `extends`/`use` consumer; patch-added keys charged to the target pack. Cosmetic; bites as log noise at scale.
- **F13:** English "spawn does nothing outside a fight" when an intent ends the fight before a later `spawn`. No content does this.
- **F21:** game update adding a tag/tone → false warnings until `Schema` updated. Never a load failure.
- **F7 (C#):** `IRun.Set` on engine names silently shadows. Bites only for C# packs (none exist).
- **F12 (C# side):** a C# pack calling `ISpec.Warn` while an older Api.dll is loaded throws. Core no longer exposed.
- **F6 (deferred):** lint can't check add-on packs depending on non-framework packs until `--with` exists.

---

## Artifacts

**Follow-up Projex:** (1) orchestrator full-tree first commit on `main`; (2) revise-projex on 2611120258-loader-loud-failures-plan.md and 2611120258-runtime-safety-fixes-plan.md carrying Must Fix items 1–14 (no new plans — all fixes amend unexecuted plans); then execute runtime plan, then loader plan.
