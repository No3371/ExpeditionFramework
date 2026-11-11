# Walkthrough: Loader Loud Failures — Warnings Channel, Schema Shapes, Spec Read Tracking, Lint Command

> **Execution Date:** 2026-11-12
> **Completed By:** Opus 5.5 (execute-projex + close-projex subagents, dispatched by orchestrate-projex)
> **Source Plan:** 2611120258-loader-loud-failures-plan.md
> **Parent:** 2611120258-loader-loud-failures-plan.md
> **Log:** 2611120258-loader-loud-failures-log.md
> **Duration:** ~1.5 h (log 06:18 → 07:50)
> **Result:** Success
> **Base Branch:** projex/2611120258-runtime-safety-fixes (stacked; squash-closed into it, runtime close → main follows)

---

## Summary

Silent authoring mistakes now loud at load time. Second severity (warning) added — logged, never voids a pack. Warnings for unknown definition keys (listing known keys), unread effect/condition args (built-in + C# `ISpec`), unknown tones, `#tag` filters nothing defines, misplaced `weight`/`when`/`tags`; wrong-typed scalars → problems. `not`/`carrying` object form fixed (human-accepted gameplay deltas). `lint` mode in framework sim checks pack folders offline incl. locale parity. 13/13 criteria pass; NER lints 0 errors / 0 warnings.

---

## Objectives Completion

| Objective | Status | Notes |
|-----------|--------|-------|
| Warning channel separate from problems (E9) | Complete | `Content.Warnings`, `ISpec.Warn`, per-pack logging; patches/areas kept |
| Unknown keys + wrong types + tones in definitions (E1 defs, E2, P-3) | Complete | `Schema.cs` + 17 `CheckShape` sites |
| Unread effect/condition args (E1 effects) | Complete | `Spec` read tracking, bare-`true` + errored-spec exemptions |
| `#tag` item-filter lint (E10 `#group` part) | Complete | deferred warning vs `KnownItemTags()` |
| `not`/`carrying` object form (SC8) | Complete | fixed, not warn-only (human decision) |
| Offline `lint` command (Tooling A) | Complete | `tests/Sim/Lint.cs`, exit codes 0/1/2 |
| README docs | Complete | severities, rules, tones, `ISpec` rule, lint |

---

## Execution Detail

> Derived from git history (`projex/2611120258-runtime-safety-fixes..projex/2611120258-loader-loud-failures`) and execution log.

### Step 1: Warning channel (E9) — `a5b21e1`

**Planned:** `Content.Warnings`, loader `Warn`, `ISpec.Warn`, `PackSet.Compile(out problems, out warnings)`, `PackRegistry` logs warnings, sim fails on own warnings.
**Actual:** As planned. `PackLoader`: `warningsByPack` + `WarningsByPack` + `Warn(at,msg)` charged to `current`; `Spec.Warn`. Old `Compile(out problems)` delegates; patch acceptance Problems-only. `PackRegistry.LoadAll` logs `warning: …` before problems check (helper `LogWarnings`, also covers packs outside `packs`). Sim: `WarningsIn(json)`, `TestWarningsDontVoid`.
**Deviation:** None (code). Commit lacks `Co-Authored-By` trailer — amend blocked by user git hook; history not rewritten.

| File | Change | Planned? | Details |
|------|--------|----------|---------|
| `src/Core/Model.cs` | Modified | Yes | +2 @ ~338: `Content.Warnings` |
| `src/Core/PackLoader.cs` | Modified | Yes | warning dict, `Warn`, `Spec.Warn` |
| `api/ExpeditionApi.cs` | Modified | Yes | `ISpec.Warn` (+3 @ 126) |
| `src/Core/PackSet.cs` | Modified | Yes | overload @ 33–39, +1 @ 68 |
| `src/Game/PackRegistry.cs` | Modified | Yes | 90–120: log warnings, `LogWarnings` helper |
| `tests/Sim/Program.cs` | Modified | Yes | warning gate in `Main`, `TestWarningsDontVoid` |

**Verification:** V1/V2 `All checks passed`; V3 build 0 errors; V4 → 1.

### Step 2: Schema shapes — `1dc9bf1`

**Planned:** `Schema.cs` (field types, per-kind shapes, tone/tag vocab), `CheckShape` at 17 sites, `LineFrom`/`UseOf` shape param.
**Actual:** New `Schema.cs` (106 lines): `Field`, `Shape`(+`With`), `Schema.Tones`/`IntelTones`/`EngineItemTags`, `Sections` for 10 kinds (`Pooled` base only for enemies/encounters/events/rooms), 17 sub-shapes, `Mismatch`. `CheckShape(n, shape, at)`: unknown key → warning w/ known keys; wrong type → problem; unknown tone → warning. `LineFrom(v,pack,at,Shape)`, `UseOf(node,pack,at,bool checkShape)` signatures per plan. Sim `TestShapes` (7 cases).
**Deviation:** None.
**Files:** `src/Core/Schema.cs` (Created, Yes), `src/Core/PackLoader.cs` (17 call sites across ~615–1350), `tests/Sim/Program.cs`.
**Verification:** V1/V2 pass (no NER type problem → A4 escalation not triggered); V3 0 errors; V5 = 18. Shape table read side-by-side vs every `Compile*`.

### Step 3: Spec read tracking + `say` tone — `c52388e`

**Planned:** `Spec` marks reads; unread keys warned after compile; bare-`true` + errored exemptions; `say` tone check.
**Actual:** `Spec`: `read`, `children`, `bareTrue`, `errored`, `Mark`, `MarkAllRead`, `ReportUnread`, `WholeCondition`/`WholeItems`. Accessors mark; `InlineLine`, `Use(null)`, `PoolOf` mark their forms. `PackLoader.Effect`/`Condition` call `ReportUnread()`; `PreviewOf` DieCheck spec does not (commented). `CompileSharedCall` → `call.MarkAllRead()`. `Builtins.say` warns via `((PackLoader.Spec)c).Warn(...)`. `ISpec` summary documents read rule. Sim `Compiled(json, registry)`, `TestUnreadArgs`; `TestPublicApi` reads `n` eagerly + 0-warnings check.
**Deviation:** V1 gate exited 1 with exactly 2 true-positive warnings (object-form `not` in framework `kits.melee` and sample `cellar_nerves`) — committed with failures recorded; Step 3b closes the gate. No code adjusted to hide them.
**Files:** `src/Core/PackLoader.cs`, `src/Core/Builtins.cs` (+3 @ 363), `api/ExpeditionApi.cs` (109–113 doc), `tests/Sim/Program.cs`.
**Verification:** V1 exit 1 (2 expected), V2/V3 pass.

### Step 3b: `not`/`carrying` object form — `2027374`

**Planned:** Three-way arg handling; `null`/`{}` → problem; tests.
**Actual:** `Builtins.cs` 74–91 (`not`) and 107–117 (`carrying`): `Has("value")` → old path; empty/`value` null → `needs a condition` / `needs an item filter`; else `WholeCondition()`/`WholeItems()`. Sim `TestNotCarryingObjectForm`.
**Deviation:** None.
**Verification:** V1 back to `All checks passed`; sample outcomes `knocked_out=3762, cleared=1062, left=176` → `3742 / 1049 / 209` (accepted delta: non-boss humans flee at 25%). V2/V3 pass.

### Step 4: `#tag` item-filter lint — `cfec779`

**Planned:** Deferred warning in `PackLoader.Items` `#` branch vs engine tags ∪ groups ∪ item_stats `tags`.
**Actual:** As planned; `knownItemTags` cache + `KnownItemTags()`, reset at end of `Finish()`; item_stats `#key` excluded (commented). Sim `TestItemTags` (4 cases).
**Deviation:** None. **Verification:** V1/V2/V3 pass.

### Step 5: `lint` command — `d1587ca`

**Planned:** `tests/Sim/Lint.cs`; `--mod`, `--strict`; ERROR/WARN lines; missing text; locale parity; exit 0/1/2; `HasMod = mods.Contains`.
**Actual:** `Lint.cs` (118 lines) per plan; `Sim.csproj` +1; `Program.Main(string[] args)` dispatches `lint`; `TestLocaleParity`.
**Deviation:** `HasMod` = framework id ∨ pack id ∨ `mods.Contains` (matches in-game: both always installed) — no effect on the 3 packs. Unknown args → usage + exit 2 (plan only specified missing folder).
**Verification:** V6 `lint sample_area: 0 error(s), 0 warning(s)`; V7 NER 0/0 (also `--mod gunsmith_expansion --strict`); V8 usage exit 2; framework `lint . --strict` 0/0. Non-vacuity probe pack → `1 error(s), 6 warning(s)`, exit 1.

### Step 6: README — `43ac92c`

**Actual:** `README.md` 29 lines changed (hunks @ 23, 195, 258, 294): problems vs warnings; `not`/`carrying` forms, `{"x": true}`, tone lists; `spec.Error`/`spec.Warn`, read rule, `ISpec` only gains members / framework Api.dll must win; lint command + framework-only-dependency limit + exit codes.
**Deviation:** None. **Verification:** V9 six probes each = 1; wording checked vs code.

### Step 7: NER acceptance + triage — `de3c2e9` (log only)

**Actual:** V2 pass; V7 0 errors / 0 warnings → triage table empty (0 false positives, 0 NER bugs). `kit.json:165` `{ "not": "#in_hand" }` is item-filter `not`, unaffected. Accepted gameplay deltas recorded: NER `rules.json:352` free-search `if` now fires out of combat; framework `main.json:188` kit `melee` punch now offered without melee in hand; sample `main.json:215` morale now matches non-boss humans.
**Deviation:** None.

---

## Complete Change Log

> `git diff --stat projex/2611120258-runtime-safety-fixes..projex/2611120258-loader-loud-failures` — 13 files, +791/−31.

### Files Created
| File | Purpose | Lines | In Plan? |
|------|---------|-------|----------|
| `src/Core/Schema.cs` | field types, per-kind shapes, tone/tag vocab | 106 | Yes |
| `tests/Sim/Lint.cs` | pack lint + locale parity | 118 | Yes |

### Files Modified
| File | Changes | Lines Affected | In Plan? |
|------|---------|----------------|----------|
| `src/Core/PackLoader.cs` | warn channel, `CheckShape` ×17, `Spec` read tracking, `#tag` check | +200/−~20, ~139–1750 | Yes |
| `tests/Sim/Program.cs` | warning gate, lint dispatch, 7 new tests | +194, 16–30, 122–132, 217–385, ~591–625 | Yes |
| `src/Core/Builtins.cs` | `not`/`carrying` object form, `say` tone | 74–117, 363–365 | Yes |
| `README.md` | severities, rules, lint | 23, 195, 258–263, 294–303 | Yes |
| `api/ExpeditionApi.cs` | `ISpec.Warn` + read-rule doc | 109–113, 126–128 | Yes |
| `src/Core/PackSet.cs` | `Compile(out problems, out warnings)` | 33–39, 68 | Yes |
| `src/Game/PackRegistry.cs` | log warnings, keep areas | 90–120 | Yes |
| `src/Core/Model.cs` | `Content.Warnings` | 338–339 | Yes |
| `tests/Sim/Sim.csproj` | compile `Lint.cs` | 11 | Yes |
| `.projex/…-plan.md`, `.projex/…-log.md` | status, log | — | Lifecycle |

### Files Deleted
None.

### Planned But Not Changed
None — all 11 planned files changed.

---

## Success Criteria Verification

| Criterion | Method | Result | Evidence |
|-----------|--------|--------|----------|
| SC1 warnings channel, never void | V1 `TestWarningsDontVoid`, V3, V4 | Pass | V4=1; patch + area kept. In-game log check not run (needs game) |
| SC2 unknown key → warning w/ known list | V1 `TestShapes`, V5, judgment | Pass | V5=18; `txet`/`gruop` cases |
| SC3 wrong-typed scalar → problem | V1 `TestShapes` | Pass | `unlocked_by` list, `weight "1+1"`, `interruptible "yes"` |
| SC4 misplaced weight/when/tags → warning | V1 `TestShapes` | Pass | `statuses.s.weight` |
| SC5 unread arg → warning, exemptions | V1 `TestUnreadArgs`, `TestPublicApi` | Pass | `valu`/`amout`/`t:ping: 3` warned; bare `true` 0; `set bogus` 1 problem 0 warnings |
| SC6 unknown tones | V1 | Pass | intent/line/intel/`say` |
| SC7 `#tag` filter | V1 `TestItemTags` | Pass | `#explosve` warned; known silent |
| SC8 `not`/`carrying` object form | V1 `TestNotCarryingObjectForm`, V2 | Pass | null/`{}` → `needs a condition`/`needs an item filter` |
| SC9 framework + sample 0/0 | V1, V6 | Pass | `All checks passed`; `lint sample_area: 0 error(s), 0 warning(s)` |
| SC10 NER exits 0, lint 0 errors, triage | V2, V7 | Pass | `lint night_expeditions_reborn: 0 error(s), 0 warning(s)` |
| SC11 lint command | V1 `TestLocaleParity`, V6–V8, probe | Pass | probe `1 error(s), 6 warning(s)` exit 1; usage exit 2 |
| SC12 game assembly builds | V3 | Pass | exit 0, 0 errors |
| SC13 README | V9, judgment | Pass | six probes = 1 each |

**Overall:** 13/13. Independently re-run by orchestrator on branch tip before close: framework sim `All checks passed`; `lint ../NightExpeditionsReborn` 0 errors 0 warnings.

---

## Deviations from Plan

1. **Step 3 V1 gate** — Planned V1 pass at Step 3; actual 2 true-positive warnings (object-form `not`) until Step 3b. Reason: read tracking surfaces exactly the bug 3b fixes. Impact none after 3b. Recommendation: future plans gate dependent steps jointly.
2. **`HasMod` in lint** — Planned `mods.Contains`; actual also true for framework + pack id (game-equivalent). No effect on outcomes.
3. **Lint unknown args** → usage + exit 2 (unspecified in plan).
4. **Commit trailer** — `a5b21e1` lacks `Co-Authored-By`; amend blocked by user hook; left as is.
5. **Status commit on base** — `8bb29f8` (`In Progress`) landed on `projex/2611120258-runtime-safety-fixes`, so that branch carries one extra doc-only commit.

---

## Issues Encountered

- **`stage-n-commit.ps1` via `pwsh -File` from Git Bash** split `--trailer Projex: …` into a stray file arg (`Files not found or unchanged`). Severity Low. Resolution: call from PowerShell directly. No partial commit.
- **Context-mode output filtering** hid long diffs; worked around with targeted reads. Low.
- **close-precheck** (close time) returned `ERROR … recorded Repo Root is missing or inaccessible` — log's `Repo Root:` holds `ExpeditionFramework (repo root)`, not a path. Advisory only; close proceeded from git state directly.

---

## Key Insights

**Rationale promoted:** `README.md` (Step 6 — severities, read rule + `true` exemption, `ISpec` only grows, lint limits). Source comments carry rejected-alternative rationale (`say` cast vs `ISpec.Warn`, item_stats `#key` exclusion, DieCheck spec not reported).

### Lessons Learned
1. **Strictness checks find real bugs first** — read tracking immediately flagged object-form `not` in shipped content. Apply: run a new lint against all owned content before deciding warn vs fix.
2. **Stacked execution needs base-aware status commits** — the `In Progress` commit lands on the stacked base, polluting a sibling branch. Apply: orchestrator should note it in the sibling's close.

### Pattern Discoveries
1. **Spec read-tracking** — mark keys on access, report remainder post-compile; exempt synthesized `value` and errored specs. Reuse: any loosely-typed config arg bag.
2. **Severity split** — problems void, warnings log. Lets stricter checks ship without breaking existing packs.

### Gotchas / Pitfalls
1. **Scalar vs object wrapping in `Spec`** — non-object args wrapped as `{value: x}`, objects kept as-is; built-ins reading only `value` silently ignore object form.
2. **`pwsh -File` arg splitting** from Git Bash for flag+value pairs.
3. **Vocab duplication** — `Schema.Tones`/`EngineItemTags` mirror `ExpeditionLocation.ToneColors`/`ItemReader`; changes there must update `Schema`.

### Technical Insights
- NER sim compiles framework `src/Core` by relative path → worktree mode would test wrong tree (plan's `Worktree: No`).
- Lint can only load framework-dependent packs; `--with` deferred.

---

## Recommendations

### Immediate Follow-ups
- [ ] In-game check: `warning:` lines in log, areas still load (SC1 judgment).
- [ ] Close 2611120258-runtime-safety-fixes-plan.md → `main` (next; carries this squash).
- [ ] Close 2611120448-phase1-hardening-plans-redteam.md with the runtime close (covers both plans).

### Future Considerations
- Lint `--with <folder>` for non-framework deps (blue team F6).
- Per-pack warning de-dup if `extends`/`use` duplicates get noisy (blue team F9).

### Plan Improvements
- Gate Step 3 V1 jointly with 3b.
- Log `Repo Root:` should be a resolvable path for close-precheck.

---

## Related Projex Updates

| Document | Update |
|----------|--------|
| 2611120258-loader-loud-failures-plan.md | Complete; Completed/Walkthrough/Log links; → closed/ |
| 2611120258-loader-loud-failures-log.md | Status Complete (verified); → closed/ |
| 2611120448-phase1-hardening-plans-redteam.md | Walkthrough link added; stays active until runtime plan closes (subject = both plans) |
| 2611120507-phase1-hardening-plans-blueteam.md | Already closed — no change |
| 2611120250-framework-expansion-synthesis-eval.md | Walkthrough link added; stays active (later phases) |
| 2611120258-runtime-safety-fixes-plan.md | Untouched — closes on own cycle |

---

## Appendix

### Commits squashed
```
413021f projex: complete loader-loud-failures
de3c2e9 projex: step 7 - NER acceptance and warning triage
43ac92c projex: step 6 - README for warnings, tones, spec reads and lint
d1587ca projex: step 5 - lint command for pack folders
cfec779 projex: step 4 - warn about item tags nothing defines
2027374 projex: step 3b - not and carrying take their object form
c52388e projex: step 3 - warn about effect arguments nothing reads
1dc9bf1 projex: step 2 - schema shapes for definitions
a5b21e1 projex: step 1 - warning channel
38cca9a projex: create execution log for loader-loud-failures
```

### References
- Plan: 2611120258-loader-loud-failures-plan.md | Log: 2611120258-loader-loud-failures-log.md
