# Patch: docs/zoh.md required_verbs vs absent provider

> **Status:** Complete
> **Author:** Claude Fable 5.2 (patch agent)
> **Parent:** 2611160623-zoh-doc-smalltalk-recheck-patch.md
> **Directive:** Check what `docs/zoh.md` says about `required_verbs` and an absent provider against ZohFramework as merged (master f40f653); correct where it disagrees.
> **Source Plan:** Direct
> **Result:** Success
> **Related Projex:** 2611160623-zoh-doc-smalltalk-recheck-patch.md | 2611152314-zoh-verb-provider-plan.md | NightExpeditionsReborn 2611152343-customer-rumours-log.md (deviation 4, source of the finding)

---

## Summary

Doc disagreed with shipped ZohFramework. Doc: list `ef.rumour` / `ef.tell` in `required_verbs`; missing provider only skips lines. Shipped: every entry of a `required_verbs` list must be a registered verb, else Fatal `missing_required_verb` → story fails to load. Only verbs left out of the list are skipped (warned) when their provider is absent. Doc corrected: example lists `ps.entry` only; `required_verbs` section rewritten.

---

## Changes

### docs/zoh.md

**File:** `docs/zoh.md`
**Change Type:** Modified
**What Changed:**
- L70 example header: `required_verbs: ["ps.entry", "ef.rumour"];` → `required_verbs: ["ps.entry"];`
- L107: "This story also lists `ef.tell` in `required_verbs`." → "`ef.tell` stays out of `required_verbs` as well."
- L109-116 `required_verbs` section: now says list only verbs the story can't do without, leave `ef.*` out; a listed missing verb and `"*"` both fail the load; unlisted `ef.*` lines are skipped with a log warning.

**Why:** ZohFramework evidence (read-only):
- `vendor/Zoh.Runtime/Validation/RequiredVerbsValidator.cs` L66-77: each list entry with `registry.GetCanonicalName(s.Value) is null` → `DiagnosticSeverity.Fatal`, `missing_required_verb`, "Required verb not available: /{name} ...".
- `tests/Harness/SelfTests.cs` F44 (L729-745) + `tests/Harness/Fixtures/f37_uses_optional.zoh`: the "plays on" case uses `required_verbs: ["ps.entry"]`, provider verb `sample.double` not listed; expects both lines played and a warning naming `sample.double`.
- NightExpeditionsReborn executor observed the same at load: `Story load failed (BadStory): missing_required_verb: Required verb not available: /ef.rumour`; its shipped `Stories/main.zoh` uses `required_verbs: ["ps.entry"]`.

---

## Verification

**Method:** Source reading of validator + harness test F44 against the edited doc; `git diff` review. No build or test run (doc-only; no game launch).

**Result:**
```
docs/zoh.md: required_verbs mentions at L70, L107, L109, L112, L113 — none lists an ef.* verb
commit 4ed38e9 docs(zoh): keep ef.* verbs out of required_verbs
```

**Status:** PASS

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| 2611160623-zoh-doc-smalltalk-recheck-patch.md | Parent; earlier recheck of same doc | None (closed record) |
| 2611152314-zoh-verb-provider-plan.md / walkthrough | Authored the wrong claim | None (closed records; this patch supersedes that doc text) |

---

## Notes

- ZohFramework `README.md` § Verb providers (L342-356) invites the misreading: its example lists `ef.tell` in `required_verbs` beside "A story that treats a provider as optional lists its required verbs explicitly instead, so a missing provider only skips those lines". Accurate only if "required verbs" excludes the provider's. Repo read-only here; not changed. Candidate follow-up for ZohFramework.
- No other file in this repo outside `.projex/` mentions `required_verbs`.
