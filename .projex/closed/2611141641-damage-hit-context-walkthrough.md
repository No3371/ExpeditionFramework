# Walkthrough: Damage Hit Context (`damage.hit` → `hit.NAME`)

> **Execution Date:** 2026-11-14
> **Completed By:** Claude Opus 5.5 (execute), Claude Sonnet 5.5 (patch, close)
> **Source Plan:** 2611141406-damage-hit-context-plan.md
> **Parent:** 2611141406-damage-hit-context-plan.md
> **Duration:** execution 16:06–16:17 by reflog; patch 16:37–16:40
> **Result:** Success

---

## Summary

`damage` takes an optional `hit` map; `hit.NAME` reads the innermost damage in progress from its decision through its last hook (`damaged`, `defeated`, `knocked_out`, the `run_end` a knockout fires), 0 outside any. Starter, Showcase and NER lint, run and digest are identical to the baselines; no public API, `src/Game` or csproj change. The audit (`2611141629-damage-hit-context-audit.md`) verified it and its findings were fixed by `2611141637-damage-hit-context-audit-fixes-patch.md`.

---

## Objectives Completion

| Objective | Status | Notes |
|-----------|--------|-------|
| SC1 `hit` map, names `a-z0-9_`, after `amount`, written order, problems at `<path>.hit[.NAME]` | Complete | Non-object `hit` first reported at `<path>`; fixed to `<path>.hit` by the patch |
| SC2 innermost frame, 0 outside, pop on exception | Complete | `try/finally`; throw test added by the patch |
| SC3 read-only, unknown `hit.$x` / `hit.a.b`, one warning per (pack, name) | Complete | Warning skipped when `OtherPacks` is non-empty (patch) |
| SC4 `tests/Sim` green with `TestHitContext` | Complete | `All checks passed` after the patch |
| SC5 docs | Complete | Patch added `hit.` to the read-only list, the warning, shared names, "in written order" |
| SC6 baselines at start | Complete | 9/9 compares ok |
| SC7 lint / run / digest identical x3 | Complete | Rerun after the patch, 12/12 ok |
| SC8 no public API change | Complete | `api/`, `src/Game/`, csproj untouched; V5g 0 errors |

---

## Execution Detail

| Step | Commit | Actual |
|------|--------|--------|
| 0 | `479e92b` | Baselines reconfirmed at current HEADs; tests green |
| 1 | `046c285` | `hits` frame list, `Hit(name)`, internal `Damage(…, hit)` with `try/finally` pop; `hit` head in `Expr.Reader`, `IsPlainName`, read callback also gets `hit.` words |
| 2 | `ec4bb22` | `HitMap` in `Builtins`; `damage` evaluates `to`, `amount`, then hit values; `PackLoader` `hitSets`/`hitReads`/`NoteRead`, read-never-set warning in `Finish` |
| 3 | `8cae3c8` | `TestHitContext` (nesting, knockout/`run_end`, enemy defeat, C# damage, frames popped, six problems, two lint checks) |
| 4 | `f68faa1` | `docs/effects.md`, `expressions.md`, `run-flow.md`, `csharp-api.md` |
| 5 | `bf1dbcc` | Full proof bar, V5g redirected game compile, plan status Complete |
| patch | `26c927a`, `1dfc20c` | Audit fixes S1, M1–M7, nit (below) |

**Deviations:** commit `eaaeb81` (plan → In Progress, on `main`) lacks the Co-Authored-By trailer; the history guard blocks amend. `HitMap` sits after `All` in `Builtins.cs`. Test pack for the enemy-defeat case puts the enemy hook in an `enemies` prefix.

### Patch (post-audit)
- Hit warning skipped when `OtherPacks` is non-empty (`ef-sim lint --mod`): a reader of another pack's hit name no longer fails `lint --strict`.
- Non-object `hit` reported at `<path>.hit`; hit values worked out after the inactive/`Over` early return.
- Tests: C# damage inside a hit's hook (case 4 can now fail), interceptor nesting, `defeat` and `leave` inside `damaged`, throwing hook pops its frame, `OtherPacks` both ways.
- Comments (hazard in present tense, `else` branch, cast) and docs per the audit.
- Not changed: execution log timestamps (14:40–16:18 against reflog 16:06–16:17).

---

## Complete Change Log

> **Derived from:** `git diff --stat main..projex/2611141406-damage-hit-context` (13 files, +444/−26).

### Files Modified
| File | Changes | In Plan? |
|------|---------|----------|
| `src/Core/ExpeditionRun.cs` | Frame list, `Hit`, internal `Damage` overload | Yes |
| `src/Core/Expr.cs` | `hit` head, `IsPlainName`, path-read callback | Yes |
| `src/Core/Builtins.cs` | `HitMap`, `damage` evaluates and passes hit | Yes |
| `src/Core/PackLoader.cs` | `NoteHitSet`, `NoteRead`, warning in `Finish` | Yes |
| `tests/Sim/Program.cs` | `TestHitContext` | Yes |
| `docs/effects.md`, `expressions.md`, `run-flow.md`, `csharp-api.md` | `hit` field, path, semantics, C# note | Yes |
| `docs/testing.md` | `--mod` row mentions `hit.NAME` | No (patch) |

### Files Created
Projex documents only: log, patch (and, at close, this walkthrough and the audit under `.projex/closed/`).

---

## Success Criteria Verification

**Method:** sequential `tools/EfSim` lint `--strict`, plain `run`, `--digest` for Starter, Showcase, NER against `c-proof/base/` and `c-proof/digest2/`; `tests/Sim`; redirected game compile.

```
12/12 compares ok (lint, run, digest, digest-minus-lines == plain; x3 packs)
tests/Sim: All checks passed
dotnet build -c Release ExpeditionFramework.csproj -p:InstallDir=<redirect>: 0 warnings, 0 errors
git diff --stat main -- api src/Game '*.csproj': empty
```

**Overall:** 8/8 criteria passed. Audit independently reproduced the bar (12/12) and probed nesting, exceptions, interceptors, `defeat`/`leave` in hooks, and the NER follow-up edit (mutation changed all 39 digest lines).

---

## Issues Encountered

- Audit S1: cross-pack hit reader failed `lint --strict` under `--mod`. Fixed in the patch.
- Audit: live `Mods/ExpeditionFramework` DLL was rewritten 16:05:09, a minute before execution started and not by it; left for the orchestrator to confirm it was the human-approved deploy.

---

## Key Insights

**Rationale promoted:** `docs/run-flow.md` § Damage and defeat now states that `hit` names are shared by every loaded pack, the read-never-set warning and why `--mod` silences it. Push placement (after the inactive/`Over` return, before the decision) lives in the `ExpeditionRun.Damage` comments.

### Lessons Learned
1. **A frame stack, not a run var, for per-event context**
   - Context: NER's `run.soft` leaks into damage dealt inside hooks and is cleared under the outer one.
   - Insight: every damage pushes its own frame, empty without `hit`, popped in `finally`.
   - Application: C3 `event.*` vars can reuse the pattern.
2. **A test that cannot fail proves nothing**
   - Context: the first C# damage case ran outside any damage, so it read 0 either way.
   - Application: put the probe inside the thing whose isolation is being tested.
3. **Execution log timestamps come from the clock at write time**
   - Context: log times disagreed with the reflog.

### Gotchas
- Lint loads only the framework and the pack under test, so a cross-pack name can't be judged there; `OtherPacks` is the escape hatch.
- `lint --strict` exits 1 on any warning, so a new warning is a breaking change for packs that read across packs.

---

## Recommendations

### Immediate Follow-ups
- [ ] NER `run.soft` → `hit.soft` (edit proven by the audit, lint 0/0, run and digest identical; the audit's edit script is reusable).

### Future Considerations
- List sugar `"hit": ["soft"]`, text-valued hit names, C3 `event.*` vars.

---

## Related Projex Updates

| Document | Update |
|----------|--------|
| 2611141406-damage-hit-context-plan.md | Complete, links here; moved to `closed/` |
| 2611141406-damage-hit-context-log.md | Moved to `closed/` |
| 2611141629-damage-hit-context-audit.md | Committed into `closed/` |
| 2611141637-damage-hit-context-audit-fixes-patch.md | Already in `closed/` |
| 2611140500-authoring-language-text-params-hit-context-proposal.md | Related Projex gains this walkthrough; Progress updated; stays In Progress |
