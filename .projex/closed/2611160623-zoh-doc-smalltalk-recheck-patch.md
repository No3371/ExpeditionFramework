# Patch: ZoH doc small-talk recheck

> **Status:** Complete
> **Author:** Claude Fable 5.2 (patch agent)
> **Parent:** 2611152314-zoh-verb-provider-walkthrough.md
> **Directive:** `docs/zoh.md` was written before ZohFramework's small-talk feature landed. Recheck its story examples and prose against ZohFramework as merged (master `f40f653`: README.md, tests/Harness/Fixtures); correct where it disagrees.
> **Source Plan:** Direct (open item J5 of 2611152314-zoh-verb-provider-walkthrough.md)
> **Result:** Success
> **Related Projex:** 2611152314-zoh-verb-provider-walkthrough.md | 2611152314-zoh-verb-provider-plan.md | 2611152320-zoh-customer-small-talk-walkthrough.md (ZohFramework)

---

## Summary

Read `docs/zoh.md` against ZohFramework `f40f653` (README § Small talk from regular customers, § Verb providers, § Story syntax notes; fixtures `f39_smalltalk.zoh`, `f40_smalltalk_bad.zoh`, `f37_uses_optional.zoh`, `f12_persist.zoh`). Two disagreements, both in the example stories; fixed. Rest of page agrees.

---

## Changes

### Example stories

**File:** `docs/zoh.md`
**Change Type:** Modified
**What Changed:**
- L69: `smalltalk: { chance: 20, at: ["arrive"] };` → `smalltalk: { "chance": 20, "at": ["arrive"] };`
- L82: `/exit;` added as last line of first `@smalltalk_arrive` entry
- L85–86: new sentence — keys of `smalltalk` map are quoted, each `@smalltalk_*` entry ends with `/exit;`
- L104: `/exit;` added as last line of reserved-card `@smalltalk_arrive` entry

**Why:**
- ZF README: "The keys are quoted: ZoH map keys are values, so `{ chance: 20 }` fails the story load (`invalid_syntax`)". Example as written would not load.
- ZF README: "End each entry with `/exit;`: a label does not end the script, so without it the talk runs on into whatever follows." Second example is a fragment an author pastes above other labels.

### Checked, agrees — unchanged

- Header `uses: ["expedition_framework"];` and explicit `required_verbs` list ← README § Verb providers (same example; `"*"` strict on missing provider, listed verbs → lines skipped).
- Label `@smalltalk_arrive`, `at: ["arrive"]` ← README entries table, `f39_smalltalk.zoh`.
- `/ps.entry; -> *entry;` + `/jump ?, *entry;` ← README start block; `/ps.entry` returns the entry name in small talk.
- `/if *key, is: ?, /exit;;` ← `f12_persist.zoh:11`, `sami.zoh:10`; README § Story syntax notes (`is:` takes any value, unwritten variable is `nothing`).
- `/choose/ prompt: … true "key" "value" /; -> *pick;` ← `f39_smalltalk.zoh` `@smalltalk_dismiss`.
- "ends without a line and the customer behaves as usual" ← README: no line → nothing happens, customer behaves as vanilla.
- No `scenes:` header in example: small talk needs no scene; not contradicted by README.

---

## Verification

**Method:** line-by-line read of both sides; `git diff` of `docs/zoh.md`. Nothing built or run (docs only; example stories not played through the ZF harness — ZohFramework is read-only here, and `ef.*` verbs do not exist in its harness).

**Result:**
```
4ce20db docs(zoh): quote smalltalk map keys and end entries with /exit
 docs/zoh.md | 7 ++++++-
```

**Status:** PASS (static comparison)

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611152314-zoh-verb-provider-walkthrough.md | Parent; listed this recheck as J5 | J5 follow-up checked off, linked here |

---

## Notes

- ZF README lists "a provider's verbs inside an entry" under not played in the game yet — `/ef.rumour` inside `@smalltalk_arrive` is exactly that. Covered by the walkthrough's in-game checks J2–J4, still the human's.
- Prior finding from 2611152320-zoh-customer-small-talk-walkthrough.md (quoted keys, trailing `/exit;`) re-verified against README and fixtures: accurate.
