# Patch: Text-Params Audit Fixes

> **Status:** Complete
> **Author:** Claude Sonnet 5.5 (patch step)
> **Parent:** 2611140743-text-params-and-sim-digest-audit.md
> **Related Projex:** 2611140627-text-params-and-sim-digest-plan.md | 2611140627-text-params-and-sim-digest-log.md
> **Directive:** Fix Significant 1-2 and the listed minors from the audit (branch `projex/2611140627-text-params-and-sim-digest`).
> **Source Plan:** Direct
> **Result:** Success

---

## Summary

Digest now covers credits, carried items and the ground pile, and its docs say what it covers and omits. The `packs.md` bare-form example loads. Six minors fixed. Code commit `f2001dd`.

---

## Changes

**`tools/EfSim/RunDigest.cs`, `tools/EfSim/KitBridge.cs`** (Modified): records `C credits`, `I id<TAB>count` (sorted id then count), `G id` (ground, in order) after the reputation lines. `KitBridge.GroundNow` is a read-only view. Class comment lists coverage and exclusions (run vars, anything the ef-sim bridge does not model). Why: a `credits` 2d10 to 2d12 change was invisible.

**`docs/testing.md`** (Modified): digest paragraph names the new coverage and what it cannot see; "proves the rest" qualified with "within what it covers".

**`docs/packs.md`** (Modified): example `$short` default `"sec"`; text explains the bare form fills only the first parameter and a required `""` second parameter fails with `settle_rep needs $short`.

**`src/Core/Expr.cs`** (Modified): `arg.$NAME` in an expression fails with `arg.$NAME is text, not a number: write {arg.$NAME} ...` instead of `unknown path 'arg.'`.

**`src/Core/PackLoader.cs`** (Modified):
- `CompileSharedCall`: an unpassed default that is not `a-z0-9_` returns a no-op effect, so no second error at the call.
- `uncalledText.Remove` carries its ordering rationale.
- `BareTextArg` gets a left boundary `(?<![a-z0-9_.])`.

**`tests/Sim/Program.cs`** (Modified): expression-error test now asserts the message (1 problem); new unpassed-bad-default test (1 problem, at the declaration); new one-param bare form asserts `Rep["security"] == 1`.

**`.projex/2611140627-text-params-and-sim-digest-log.md`** (Modified): step-0 path `c-proofase` (a `\b` escape) corrected to `c-proof\base`.

---

## Verification

**Method:** `scratchpad/c-proof/patch-proof.sh`, sequential, `tools/EfSim` and `tests/Sim` only. Output `scratchpad/c-proof/patch-proof.out`.

**Result:**
```
ST/SC/NER: lint ok, run ok (byte-identical to c-proof/base), digest stable (two runs), digest-minus-lines == plain
digest lines: ST 1, SC 3, NER 39
credits mutation (aud-c/var S0-control vs S1-credits): digest differs
All checks passed / tests exit 0
```
New digest baseline: `scratchpad/c-proof/digest2/{ST,SC,NER}-digest-base.txt` (full runs `*-d1.txt`, `*-d2.txt`, mutation lines `mut-*.txt`).

**Status:** PASS

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611140743-text-params-and-sim-digest-audit.md | Parent | Untouched (orchestrator commits) |
| 2611140627-text-params-and-sim-digest-log.md | Audited work log | Path typo fixed |

---

## Notes

- The digest baseline in `c-proof/digest/` predates the new records and is superseded by `digest2/`; hash values changed by design, line counts did not.
- Not changed: `memory is MemoryStore` guard, k^d per-site compile cost (informational in the audit).
