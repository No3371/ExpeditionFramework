# Red Team: Phase 1 Hardening Plans (loader loud failures + runtime safety fixes)

> **Status:** Complete
> **Lead:** Opus 5.5 (redteam-projex subagent, dispatched by orchestrate-projex)
> **Parent:** 2611120258-loader-loud-failures-plan.md
> **Subject:** 2611120258-loader-loud-failures-plan.md + 2611120258-runtime-safety-fixes-plan.md, attacked as one pair (shared files, sequential execution) | **Related:** 2611120250-framework-expansion-synthesis-eval.md (upstream) | 2611120231-pack-schema-loader-expansion-explore.md | 2611120227-runtime-hooks-expr-expansion-explore.md
> **Walkthroughs:** 2611120258-loader-loud-failures-walkthrough.md | 2611120258-runtime-safety-fixes-walkthrough.md (both subject plans closed 2026-11-12 → report closed)

Evidence base: read-only inspection of `src/Core/{PackLoader,Builtins,ExpeditionRun,ExpeditionRun.Api,PackSet,Model,Expr}.cs`, `src/Game/{PackRegistry,ExpeditionLocation,GameBridge,IntelCards,ItemReader}.cs`, both sims, framework/sample/NER JSON (JSONC parsed by throwaway scripts). No code changed; sims not run.

---

## Bottom Line

**Verdict:** Fix Issues

**Top Vulnerabilities:**
1. **F1 (High)** — Spec read tracking (SC5) warns on the framework's own idiom `{"next_room": true}` / `{"fits": true}`: 4 sites in framework+sample content and 2 in NER. The SC9 zero-warning gate (V1, V6) fails at Step 3. The plan's fallback is to revert all of Step 3, and its own test expects an unread scalar to warn.
2. **F2 (Medium)** — Phase 0 ("first commit") has no defined scope. Execution, rollback and abandon all assume the whole code tree is tracked on `main`. If only the plans get committed, rollback cannot restore the code.
3. **F4 / F3 (Medium)** — When a compiler reports an error and returns early, its remaining keys also get "not used" warnings (both plans add such early-return paths). SC4 checks `itemId != null`, but `GameBridge.GiveItem` treats an empty id as "nothing to give", so an empty id would log a misleading "could not give" line.

---

## Stakeholder Roles

| Wave | Role | Cares About | Pain Points | Critical Assumptions |
|------|------|-------------|-------------|---------------------|
| 1 | Plan Executor (execute-projex agent) | Steps run as written; gates pass deterministically | Gate fails with no prescribed fix → improvises or reverts | Plan snippets match code; A1–A4 hold; gates are reachable |
| 1 | Pack Author (framework / sample / NER data) | Mistakes surfaced, idioms keep working, packs keep loading | False warnings on idiomatic data; new problems void areas | Existing documented forms stay valid |
| 1 | C# Mod Author (`ISpec` consumer) | Stable API; warnings mean real mistakes | Lazy reads → spurious warnings; new interface member | `ISpec` additive; read rule is discoverable |
| 1 | Human Maintainer (decision owner) | Controlled gameplay change, recoverable history | 5 pending decisions + Phase 0 hidden behind `Ready` status | Plans won't run before decisions |
| 2 | Player (in game) | Stable labels, no debug noise, unchanged balance unless intended | English debug lines in run log; NER free-search change | Fixes are invisible unless bugs |
| 2 | Downstream / Third-party Pack | Loads with the framework; lintable | Wrong-type now voids areas; lint can't load its dependencies | Lint handles "any pack folder" |
| 2 | Sim / Lint Gate owner (Integrator) | Gates catch real regressions, not vacuous | Vacuous greps; NER sim blind to warnings | Each V-check discriminates |
| 3 | Hostile / careless patcher pack (Attacker) | Change another pack's data; avoid blame | n/a — they succeed | Warnings follow `current` (target pack) |
| 3 | Exploiting player (Attacker) | Gate-free buttons | n/a | Render-time `when` = offer-time gate; `Perform` doesn't recheck |
| 3 | Projex governance / Source Hygiene auditor (Accountable) | Plans' readiness claims true; no drift-prone duplicates | `Ready` with open blockers; duplicated vocab lists | Status reflects executability |

### Wave Derivation

- **Wave 1 → 2:** F3, F8 and F13 put new or changed behavior in front of the **Player** (English `bad` lines, chance-gated buttons, the NER free-search change from SC8). F6, F7 and SC3 ("wrong type = problem, voids areas") affect packs outside the three surveyed ones → **Third-party Pack**. F1 shows the plan's own gates (V1/V6) failing, and F10 shows that V2 never sees warnings → **Sim/Lint Gate owner**.
- **Wave 2 → 3:** the Third-party Pack findings (blame goes to `current`, patch acceptance compares problem strings) → **Hostile patcher**. The Player finding F8 (render dice read as 1, `Perform` doesn't recheck `when`) → **Exploiting player**. F2/F5 (a `Ready` status with open blockers) and the risk that `Schema` drifts from `src/Game` → **Governance auditor**.

## Roles Not Attacked

| Role | Surfaced by | What would have been asked |
|------|-------------|---------------------------|
| Translator (NER `zh.csv`) | Lint step 5 parity WARN lines | Are placeholder-parity warnings actionable, and who triages them? |
| Game updater (new game version) | Time-drift of `Schema.EngineItemTags` / `Tones` vs `ItemReader` / `ToneColors` | Who notices when the game adds a tag or tone and `Schema` lags? |

---

## Attack Surface (Per Role)

**Plan Executor:**
- Claims: loader Step 3 "Verify A1 by SC9 zero-warning gate"; "If only false positives remain from one accessor, fix its Mark list". Runtime: "Order is arbitrary".
- Assumptions: scalar-wrapped `value` warnings only hit real mistakes; Phase 0 leaves code tracked; runtime Step 1 test may copy FightForeman.
- Dependencies: `git` base with code tracked; both sims green at start; NER path `../NightExpeditionsReborn`.

**Pack Author:**
- Claims: SC2/SC5 warn only on unused keys; SC9 framework content warning-free; SC10 NER warnings triaged.
- Assumptions: `{"<effect>": true}` stays valid (README lists `next_room`, `fits` as vocabulary; framework kits use the `true` form).
- Dependencies: Schema tables match `Compile*`; tones and item tags match `src/Game`.

**C# Mod Author:**
- Claims: `ISpec.Warn` added; "read every key while compiling".
- Assumptions: interface growth is harmless; packs ship their own `ExpeditionFramework.Api.dll` (the comment in `PackRegistry.InstanceOf` says so).
- Dependencies: one Api assembly version at runtime.

**Human Maintainer:**
- Claims: both plans `Ready`; decisions "taken by default".
- Assumptions: nothing executes before Phase 0 + decisions.
- Dependencies: orchestrator honoring the pending list.

**Player / Third-party Pack / Gate owner / Adversaries / Auditor:** see the findings below.

---

## Critical Findings

### Finding 1: SC5 flags the framework's own idiomatic `{"x": true}` no-argument form
**Severity:** High | **Likelihood:** High (certain — sites exist today)

**Affects Roles:** Plan Executor, Pack Author, Gate owner, Human Maintainer

**Attack Vector:** `Spec` ctor (`PackLoader.cs` L1471) wraps a scalar as `{value: x}`. Built-ins that read nothing (`next_room`, `stay`, `end_event`, `retreat`, `enemy_turn`, `reload`, `clear_ground`, `lose_found`, condition `fits`; `intent_interruptible` reads only `who`) leave a non-null `value` unread → `ReportUnread` warns. Live sites:
- framework `ExpeditionFramework/main.json` L119 `{ "next_room": true }`, L157 `"where": { "fits": true }`, L159 `{ "reload": true }`
- sample `main.json` L109 `"when": { "intent_interruptible": true }`
- NER `kit.json` L13, L174 `{ "fits": true }`

**Role-Specific Impact:**
- **Plan Executor:** V1 fails (`Fail("warning: …")` on framework+sample), and so does V6 (`--strict`). Step 3's "If this fails" offers either "fix its Mark list" (no Mark list applies, because these compilers call no accessor) or "revert Spec tracking", which loses SC5 completely. The plan's own test "C# effect reading nothing given scalar `{t:ping: 3}` → warning at `.value`" locks in the conflicting rule.
- **Pack Author:** a valid, documented form is reported as "misspelled, or not a field". NER's `fits` warnings get triaged as NER bugs, so the follow-up list will send the NER author to change correct data.
- **Gate owner:** A1 ("0 runtime-closure reads") was the only hazard the plan checked for. This different hazard gets past the planning survey.

**Blast Radius:** Every pack using `true` as the argument of a no-argument effect or condition, which is the framework's own house style.

**Remediation:** Choose the rule in the plan before executing. Recommended:
- (a) Exempt a synthesized `value` whose scalar is `true` from `ReportUnread`, because the author meant "do it".
- (b) Keep warning on other scalars (`{"next_room": 3}`).
- Add tests: `{"next_room": true}` and `{"fits": true}` → 0 warnings; `{"t:ping": 3}` → 1 warning.

Alternatively, the no-argument built-ins could call `MarkAllRead()`, but that also hides `{"next_room": {"typo": 1}}`.

---

### Finding 2: Phase 0 scope undefined; rollback depends on code being tracked
**Severity:** Medium | **Likelihood:** Medium

**Affects Roles:** Human Maintainer, Plan Executor, Auditor

**Attack Vector:** `git status`: zero commits; `src/`, `api/`, `tests/`, `README.md` and the packs are all untracked. Both plans say only "framework repo's first commit". Rollback relies on `git checkout <base> -- <files>` or `projex-abandon`. If the first commit holds only `.projex/` (a plausible "commit the plans" reading of execute-projex's prerequisite), edits to untracked files survive an abandon, and `checkout <base> -- src/Core/PackLoader.cs` errors. `Worktree: No` (correct, because the NER sim compiles `../../../ExpeditionFramework/src/Core`) means all edits land in the human's only copy.

**Role-Specific Impact:**
- **Human Maintainer:** an unrecoverable mix of plan edits and the pristine code.
- **Plan Executor:** the rollback step fails exactly when it is needed.

**Blast Radius:** the entire framework source.

**Remediation:**
1. Define Phase 0 as: commit the full tree (`.gitignore` already excludes `bin/`, `obj/`) plus both plans.
2. Add a precondition to both plans: `git -C <repo-root> ls-files src/Core/PackLoader.cs tests/Sim/Program.cs README.md` lists all three.

---

### Finding 3: SC4 logs a false "could not give" line for empty item ids
**Severity:** Medium | **Likelihood:** Low–Medium (depends on game `LootRegistry` / interceptor behavior; not verifiable headless)

**Affects Roles:** Player, Pack Author, C# Mod Author

**Attack Vector:** `GameBridge.GiveItem` already treats `string.IsNullOrEmpty(itemId)` as "nothing to give". The runtime plan's guard is `itemId != null && name == null`. An `""` produced by `Bridge.RollTable` (game table rolling nothing) or by a `loot` decision interceptor blanking `Text` → `"could not give  (unknown item id, or no room on the ground)"` in the player's run log. Both FakeBridges return `itemId`, so no sim can observe it.

**Remediation:** use `!string.IsNullOrEmpty(itemId) && name == null`, and add a sim case where `Give("")` produces no log line.

---

### Finding 4: An error plus unread-key warnings for one mistake
**Severity:** Medium | **Likelihood:** High (whenever an author error path fires)

**Affects Roles:** Pack Author, C# Mod Author, Plan Executor

**Attack Vector:** Compilers that `Error` and `return s => { }` before reading the rest leave keys unread, so `ReportUnread` adds noise. Examples:
- `Builtins.Assign` (bad `var` → `value` unread)
- `set_text` (bad `var`)
- the runtime plan's new engine-var guard (returns before `c.Value("value")`)
- any C# compiler that validates first

The plan de-duplicates only `CompileSharedCall` (`MarkAllRead`). Executed in either order, the pair produces `set run.turn` → 1 problem + 1 "`value` not used here" warning, which reads as a second, false mistake.

**Remediation:** `Spec` records `errored` when `Error()` is called, and `ReportUnread` skips that spec. Add a test: `{set: {var: "bogus", value: 1}}` → 1 problem, 0 warnings.

---

### Finding 5: Plans are `Ready` while their blockers are open; the SC8 default ships a NER gameplay change
**Severity:** Medium | **Likelihood:** Medium

**Affects Roles:** Human Maintainer, Player, Auditor

**Attack Vector:** Both are `> **Status:** Ready`, yet each depends on an undecided Phase 0, and the loader plan lists 3 defaults plus 2 orchestrator-noted decisions. SC8 is on by default. Confirmed live: NER `rules.json` L352 `{ "not": { "scene": "combat" } }` is always false today. After the fix, `run.searches += run.free_searches` fires out of combat, changing NER balance in a repo with no VCS and no versioned release. The framework `punch` (main.json L188) becomes offerable for the sample (its kits include `expedition_framework:melee`); NER's own kits do not use it.

**Remediation:**
- Set both plans to `Blocked` until Phase 0 is done and the decisions are recorded.
- Make SC8 its own step gated on the decision, with a warn-only fallback: warn when a `not`/`carrying` object has no `value` key.

---

### Finding 6: Lint can't load a pack's non-framework dependencies
**Severity:** Low | **Likelihood:** Medium (future add-on packs)

**Affects Roles:** Third-party Pack, Pack Author

**Attack Vector:** Lint step 3 adds only the framework and the target folder. `--mod` toggles `HasMod`; it does not load the other pack. A NER add-on (behaviors, templates, `patch` targeting `night_expeditions_reborn:…`) gets "no … named" errors and the exit code is 1. This contradicts the claim "any pack folder can be checked".

**Remediation:** add a repeatable `--with <folder>` that `PackSet.Add`s the folder before the target, or narrow the claim in SC11 and the README.

---

### Finding 7: Engine-var guard leaves two writers uncovered
**Severity:** Low | **Likelihood:** Low

**Affects Roles:** Pack Author, Third-party Pack

**Attack Vector:** `ExpeditionRun.Begin` writes `area.Vars` straight into `vars` (L265), so `areas.x.vars: {turn: 3}` still shadows silently. C# `IRun.Set("turn", …)` is likewise unguarded. The SC2 claim "set/add … becomes a load problem" is true; the README sentence "kept by the engine — set/add on them is a problem" leaves out `vars`.

**Remediation:** check `CompileArea` `vars` keys against `ExpeditionRun.EngineVars` (problem), and add a test.

---

### Finding 8: Render-time dice make `chance` gates always open and dice-threshold gates always closed
**Severity:** Low | **Likelihood:** Low (no shipped content; verified that NER/framework render paths contain no dice or `variants`)

**Affects Roles:** Player, Exploiting player, Pack Author

**Attack Vector:** Under `rendering`, `Roll` returns 1. So `chance` (`Roll(100) <= v`) is true for any v ≥ 1, and `"dice(1,6) >= 4"` is always false. `Perform` (L858) does not recheck `i.When`, so the offer-time value is the only gate. The planned README line "dice count as 1" is accurate but doesn't spell out the asymmetry.

**Remediation:** README: "a `when` using `chance` always shows; one needing a high roll never shows — roll into a `run.` var on room entry instead". Optionally `Warn` at load when a `when`, `where` or HUD condition contains dice or `chance`.

---

### Finding 9: Warning duplication and blame
**Severity:** Low | **Likelihood:** High

**Affects Roles:** Pack Author, Hostile patcher

**Attack Vector:**
- `extends` templates and `use` shared interactions are merged before `CheckShape`, so one typo warns once per consumer.
- Patch-introduced keys are warned under the target pack, because `current` = target during `Section` (acknowledged in Risks).

A careless or hostile patcher can flood another pack's log with warnings that aren't attributed to it. Damage is cosmetic: warnings never void a pack or reject a patch.

**Remediation:** accept, or de-duplicate warnings by message text in `WarningsByPack`.

---

### Finding 10: Verification gaps
**Severity:** Low | **Likelihood:** High

**Affects Roles:** Gate owner, Plan Executor

**Attack Vector:**
- Runtime V4: `rooms_left` and `scheduled` already appear in `README.md` (count 1 each today), so those two probes are vacuous.
- Runtime V5: any `finally` in `PackLoader.cs` passes.
- Runtime Step 1 test text says "(as `FightForeman` does)", but `FightForeman` uses `new Registry()` (Program.cs L246). Copying it would leave `t:risky` unregistered and the test would fail with a confusing "unknown effect".
- V2 (NER sim) never prints warnings, so SC10 triage relies only on V7.

**Remediation:**
- Replace the V4 probes with phrases that are new text ("`ground` and `scheduled`, kept by the engine").
- V5: grep `LeaveShared` inside a `finally` block, or rely on the test.
- Drop the FightForeman parenthetical.
- Make V7 the named SC10 evidence.

---

### Finding 11: `KnownItemTags` counts `item_stats` `#key` as defining a tag
**Severity:** Low | **Likelihood:** Low

**Affects Roles:** Pack Author

**Attack Vector:** `Model.cs` L365 shows that `item_stats["#tag"]` *consumes* a tag (stats applied to items already carrying it); it never adds one. Including `#key` in the known set means a typo made in both places (`"#explosve"` filter + `item_stats["#explosve"]`) silences both.

**Remediation:** leave `#key` keys out of the known set. Optionally warn when an `item_stats` `#key` names an unknown tag.

---

### Finding 12: `ISpec.Warn` versus packs that ship their own Api.dll
**Severity:** Low | **Likelihood:** Low (no C# packs exist today)

**Affects Roles:** C# Mod Author

**Attack Vector:** `PackRegistry.InstanceOf`'s comment ("A pack's listed ExpeditionFramework.Api.dll …") shows that C# packs ship their own Api.dll copy. If an older copy loads first, then `Builtins` `say` calling `c.Warn(...)` through `ISpec` throws `MissingMethodException` at compile time.

**Remediation:** inside Core, call `((PackLoader.Spec)c).Warn` (the plan already casts for `WholeCondition`). Document that `ISpec` members are append-only and require the framework's Api.dll to win.

---

### Finding 13: `spawn` "outside a fight" line during fight teardown
**Severity:** Low | **Likelihood:** Low

**Affects Roles:** Player

**Attack Vector:** If an enemy intent's `do` ends the fight midway (`EndFight` sets `Scene = "room"`, L602) and a later `spawn` in the same list runs, the player sees the English line "spawn does nothing outside a fight". Previously this was silent. The `else` branch is honest.

**Remediation:** accept, or log at most once per run.

---

## Role-Based Assumption Challenges

### Plan Executor: A1 "no runtime-closure Spec reads" is the hazard that matters
**Challenge:** A1 checks for lazy reads only. Unread synthesized `value`s (F1) and reads skipped after an error (F4) are separate hazards.
**Counter-Evidence:** A1 holds for `Builtins.cs`: every accessor call is made before `return s =>`. But 6 live sites trip F1.
**If Wrong:** Step 3 is reverted, and SC5 (the most valuable check) is lost.
**Action:** Relax — extend A1 to "unread keys are only real mistakes", with the F1/F4 rules.

### Pack Author: "planning survey: 0 wrong-typed, 0 tones, 0 #tags, key sets valid"
**Challenge:** Re-verified independently: interaction/choice keys across the 3 packs ⊂ the Interaction shape; all tones ∈ {good, bad, dim, enemy, heading, hint}; no unknown `#tags` in the framework+sample or framework+NER sets.
**Counter-Evidence:** none.
**If Wrong:** n/a.
**Action:** Validate (holds).

### Runtime plan: "NER RNG sequence and behavior expected unchanged"
**Challenge:** The NER sim calls `Options()` and `Hud()` every step with a shared seeded `Random`.
**Counter-Evidence:** NER `variants` occur only in `say` lines. Every NER `dice(`/`chance` is inside effects (`if`/`cond`/`for_each`), not in interaction `when`/`where` or HUD. Render paths consume no RNG today.
**If Wrong:** n/a.
**Action:** Validate (holds).

### Runtime plan: `spawn` guard matches engine semantics
**Challenge:** Does `prepare` or `combat_start` run before `Scene = "combat"`?
**Counter-Evidence:** `Engage` sets `Scene = "combat"` (L559) before `prepare` and `Emit("combat_start")`. NER's only `spawn` is inside the in-combat `call_for_help`.
**If Wrong:** n/a.
**Action:** Validate (holds).

### Loader plan: `KnownItemTags` uses `ItemGroups.Values`
**Challenge:** Is `ItemGroups` keyed by group name?
**Counter-Evidence:** No — it is item id → group names (`PackLoader.cs` L496–497), so `.Values.SelectMany` correctly yields group names. `Schema.EngineItemTags` equals the `ItemReader` tags (L56–59, L89–93).
**If Wrong:** n/a.
**Action:** Validate (holds; see F11 for the `#key` nuance).

---

## Role-Specific Edge Cases & Failures

### Pack Author: `{"not": null}` / `{"not": {}}`
**Trigger:** null or empty argument.
**Role Experience:** `null` → Args `{value: null}` → `Has("value")` false → `WholeCondition` → problem "unknown condition \"value\"" (confusing text). `{}` → the problem the plan expects.
**Recovery:** Possible
**Mitigation:** for an absent or null `value`, report `needs a condition`.

### Plan Executor: sequential execution in either order
**Trigger:** runtime plan first, loader plan second (or the reverse).
**Role Experience:** the `CompileSharedCall` edits don't conflict (one adds a top line, the other wraps the closure). `Program.cs` `Main()` → `Main(string[] args)` is a single-plan change. The README `§ Vocabulary` is edited by both, in adjacent sentences. F4's double reporting appears only once both plans have landed.
**Recovery:** Possible
**Mitigation:** add F4's test to whichever plan executes second.

---

## What's Hidden (Per Role)

**Omissions per role:**
- **Pack Author:** the `{"x": true}` idiom isn't addressed (F1). `area.vars` can still shadow engine vars (F7).
- **Human Maintainer:** Phase 0 scope (F2). `Ready` hides blockers (F5).
- **Player:** new English `bad` lines (spawn, give) appear in the run log, not only in the dev log.
- **Third-party Pack:** SC3 makes wrong-typed scalars into area-voiding problems for packs no survey covered. The A4 escalation rule watches NER only.

**Tradeoffs per role:**
- **C# Mod Author:** accepts an implicit "read eagerly" contract in exchange for free typo detection.
- **Pack Author:** accepts warnings blamed on the target pack in exchange for keeping the patch blame rules unchanged.

---

## Scale & Stress (Role Impact)

**At 10x (packs / content):**
- **Pack Author:** template and `use` duplication (F9) multiplies warnings per consumer, and the game log becomes noisy.
- **Third-party Pack:** more packs means more chance of a wrong-typed scalar voiding areas, with no escalation path outside NER.

**At 100x:**
- **Gate owner:** lint without `--with` (F6) can't check dependency chains, so a pack ecosystem becomes unlintable offline.
- **Load time:** `ReportUnread` is O(keys) per spec, and `KnownItemTags` is cached per Finish. Negligible.

---

## Remediation

### Must Fix (Before Proceeding)
- **F1 no-argument `true` idiom** (affects: Executor, Pack Author, Gate) → state the rule for synthesized `value` in Step 3 and add tests for `next_room: true`, `fits: true`, `intent_interruptible: true` → verify that V1/V6 are warning-free with the framework JSON unchanged.
- **F2 Phase 0 scope** (affects: Maintainer, Executor) → define the first commit as the full tree and add an `ls-files` precondition → the human confirms.
- **F5 status** (affects: Maintainer, Auditor) → set `Blocked` until Phase 0 and the decisions are done; gate SC8 behind the decision.

### Should Fix (Before Production)
- **F4 error + unread double report** (affects: Pack Author, C# Author) → `Spec.errored` suppresses `ReportUnread`.
- **F3 empty item id** (affects: Player) → `!string.IsNullOrEmpty(itemId)`.
- **F7 `area.vars` shadowing** (affects: Pack Author) → check `vars` keys against `EngineVars`.
- **F10 vacuous or wrong verification text** (affects: Gate, Executor) → reword V4 probes; fix the Step 1 test note.
- **F11 `#key` as tag definer** (affects: Pack Author) → drop it from the known set.
- **F12 `say` → `ISpec.Warn`** (affects: C# Author) → call through `PackLoader.Spec`.

### Monitor
- **F6 lint dependencies** (affects: Third-party Pack) → revisit when the first add-on pack appears.
- **F8 render-dice semantics** (affects: Player, Pack Author) → revisit if content puts dice in `when`.
- **F9 duplicated or misattributed warnings** (affects: Pack Author) → revisit if logs get noisy.
- **F13 spawn line during teardown** (affects: Player) → revisit on player reports.
- **`Schema` vs `src/Game` drift** (Time) → revisit on each game update.

---

## Final Assessment

**Soundness:** Fixable
**Risk:** Medium
**Readiness:** Ready with Fixes

**Per-Role Readiness:**
- **Plan Executor:** Not Ready — F1 makes the Step 3 gate fail with no prescribed resolution; F2 makes rollback unsafe.
- **Pack Author:** Ready with Fixes — the shape tables are verified accurate; F1 and F4 must be fixed first.
- **C# Mod Author:** Ready with Fixes — F12 is latent.
- **Human Maintainer:** Not Ready — Phase 0 and 5 decisions are pending (F2, F5).
- **Player:** Ready with Fixes — F3; the SC8 balance change is intended but undecided.
- **Third-party Pack:** Ready (no known packs); F6/F7 to monitor.

**Solid (verified):**
- The loader-plan shape table matches every `Compile*` read.
- The tone list matches `ToneColors` + `plain`; intel tones match `IntelCards`.
- `EngineItemTags` matches `ItemReader`.
- The `not`/`carrying` bug is confirmed at 3 live sites.
- A1 holds for built-ins.
- The `spawn` guard agrees with `Engage` ordering.
- NER render paths have no RNG.
- The runtime `try/finally` matches `Guarded`.
- `-p:InstallDir=` correctly overrides the csproj property (no copy into the game's `Mods/`).

**Conditions for Approval:**
- [ ] F1 rule written into loader Step 3, with tests (for Executor, Pack Author)
- [ ] Phase 0 defined as a full-tree commit and done (for Maintainer, Executor)
- [ ] Decisions recorded; plan Status reflects them (for Maintainer)
- [ ] F4 suppression added to whichever plan executes second (for Pack Author)

**No-Go If:**
- [ ] The first commit doesn't track `src/`, `tests/`, `api/`, `README.md` (impacts Maintainer, Executor)
- [ ] Step 3 is executed with the "revert on false positives" fallback as the only answer to F1 (impacts Pack Author, Gate)
