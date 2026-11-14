# Patch: damage-hit-context audit fixes

> **Status:** Complete
> **Author:** Claude Sonnet 5.5
> **Parent:** 2611141629-damage-hit-context-audit.md
> **Related Projex:** 2611141629-damage-hit-context-audit.md | 2611141406-damage-hit-context-plan.md | 2611140500-authoring-language-text-params-hit-context-proposal.md
> **Directive:** Fix audit S1, M1–M7 and the evaluate-after-early-return nit; leave M8; keep lint/run/digest identical
> **Source Plan:** Direct (findings of the audit above, on branch `projex/2611141406-damage-hit-context`)
> **Result:** Success

---

## Summary

Code commit `26c927a` (trailer `Projex: 2611141637-damage-hit-context-audit-fixes`). No behavior change for packs without `hit`; Starter, Showcase and NER lint, run and digest match the baselines.

## Changes

### S1: hit warning vs other packs
**File:** `src/Core/PackLoader.cs` (`Finish`, hit-read loop). Skips the "read but no damage … sets it" warning when `OtherPacks` is non-empty (`ef-sim lint --mod`, memory packs): the setter may live in a pack not loaded, and warning made a cross-pack reader fail `lint --strict`. Comment states why. Empty or null `OtherPacks` still warns.
**Docs:** `docs/run-flow.md` (warning text, names shared by every loaded pack, quiet under `--mod`), `docs/testing.md` (`--mod` row).
**Test:** `TestHitContext`, three loads of one reader: null → 1 warning, empty set → 1, `{ "a" }` → 0.

### M1–M4 docs and path
- `docs/expressions.md`: read-only list gains `hit.`.
- `docs/run-flow.md`: "in written order"; warning and shared names documented (M2).
- `src/Core/Builtins.cs` `HitMap`: non-object `hit` now a problem at `<path>.hit` (`spec.Loader.Problem`) instead of `<path>`. Test asserts `.damage.hit`.

### M5 tests (`tests/Sim/Program.cs`, `TestHitContext`)
Ported from the audit probes P1, P2, P3, P4, P5. `Play` takes an optional registry and `begin`.
- Case 4 rewritten: a mod effect calls `IRun.Damage` from inside a soft hit's `damaged` hook; inner reads 0 (40), outer reads 31 after. It fails if the C# damage pushes no frame (inner would read 1).
- Throwing hook (`t:boom` in `damaged`): `Begin` throws, `Hit("soft")` is 0 afterward.
- `defeat` effect and `leave` inside `damaged`: `knocked_out` / `run_end` read the hit (11, 21, 21); frames popped.
- Interceptor dealing damage during the decision nests: inner reads 0, outer its hit (`h_sum` 100), `damaged` fires twice.

### M6, M7 comments
- `src/Core/ExpeditionRun.cs` `hits`: dropped "the old run.soft pattern"; hazard stated in present tense.
- `src/Core/Builtins.cs` `damage` closure `else` branch: why dropping the hit is safe (only `MapContext`, whose `Damage` does nothing).
- `src/Core/Expr.cs` `hit` case: why a cast, not an `IRun` member.

### Nit: hit values after the early return
`src/Core/Builtins.cs` `damage` closure: the hit path now returns before evaluating hit values when `!t.Active || s.Run.Over`, the same test as the start of `ExpeditionRun.Damage`. The hit-less path is untouched; `amount` is still evaluated first as before. Packs with dice in `hit` on an inactive target no longer draw RNG.

### Not changed
- **M8:** execution log timestamps left as they are. They disagree with the reflog (log: started 14:40, steps 14:45–16:18; reflog and evidence mtimes 16:06–16:17). Provenance caveat, recorded here only.
- Audit nits not asked for: `NoteRead` position among fields, empty hit name path `….hit.`.

## Verification

**Method:** sequential from `tools/EfSim` and `tests/Sim`; outputs in the orchestrator scratchpad (`c2-proof/patch/`).

```
tests/Sim: All checks passed
ST/SC/NER lint --strict == c-proof/base: ok x3
ST/SC/NER run             == c-proof/base: ok x3
ST/SC/NER run --digest '^digest ' lines == c-proof/digest2: ok x3
digest minus digest lines == plain run: ok x3
dotnet build -c Release ExpeditionFramework.csproj -p:InstallDir=<S>\c2-proof\game-install\ : 0 warnings, 0 errors
```

**Status:** PASS

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611141629-damage-hit-context-audit.md | Parent audit | Left uncommitted as-is; closed with the plan |
| 2611141406-damage-hit-context-plan.md | Source work | None here; closed by its walkthrough |

## Notes

- Audit conditions: M1–M3 done, S1 fixed (no follow-up needed), M8 deferred by decision.
- Docs `docs/*.md` ride the code commit.
