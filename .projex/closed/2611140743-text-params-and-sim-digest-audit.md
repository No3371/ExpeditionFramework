# Audit: Text Params for Shared Effects + ef-sim Run Digest

> **Status:** Complete
> **Author:** Claude Opus 5.5 (audit step)
> **Parent:** 2611140627-text-params-and-sim-digest-log.md
> **Related Projex:** 2611140627-text-params-and-sim-digest-plan.md | 2611140627-text-params-and-sim-digest-log.md | 2611140500-authoring-language-text-params-hit-context-proposal.md
> **Audit Date:** 2026-11-14 | **Auditor:** Claude Opus 5.5 | **Work Period:** 2026-11-14 07:06–07:30 (commits)
> **Subject:** branch `projex/2611140627-text-params-and-sim-digest`, `b53730f..9de7421` (base `main` @ `b53730f`, plan commit `7e659e9`)

---

## Audit Summary

**Claim:** `ef-sim run --digest` (per-row SHA-256 over observable run results) landed and baselined before C1; C1 text params (`$NAME` params, `{arg.$NAME}` filled into body values, per-call-site compile) added to the loader with guards; Showcase / Starter / NER give identical lint, byte-identical `run` stdout and identical digests before vs after; SC1–SC8 met.

**Verdict:** Verified (with conditions on docs + digest coverage)

**Assessment:** Completeness: High | Correctness: High | Quality: High | Value: High

**Top Issues:**
1. Digest blind to credits and inventory: a silent `credits` change (`2d10`→`2d12` in Starter) changes neither table nor digest. Class comment says it hashes everything "a player could see"; testing.md's "Proving a refactor" leans on it as the full proof.
2. `docs/packs.md:163` example `{ "settle_rep": "security" }` is a load error (`settle_rep needs $short`) — the documented bare form does not work with the documented effect.
3. Test case 15 (`arg.$f` in an expression) passes without the `Expr.cs` change: `$` is not an expression word char, so `arg.$f` already failed pre-C1 as `unknown path 'arg.'`. The `Expr.Reader` edit is reachable only via raw `Reader` paths; the error message the author sees is unhelpful.

---

## Claims vs Evidence

| Claim | Evidence | Status | Notes |
|-------|----------|--------|-------|
| SC1 plain stdout unchanged, one digest line per row | `aud-c/{ST,SC,NER}-run.txt` == `c-proof/base/*` == `c-baseline/*` (bytewise); digest counts 1/3/39 | ✓ | Rerun by auditor at `9de7421` |
| SC2 digest stable + neutral; baseline before C1 | `aud-c/*-d.txt` minus digest lines == plain (CR-preserving `grep -U`); `c-proof/digest/*-d1.txt` == `aud-c/*-d.txt` bytewise; pre-C1 build (`c95f16d`) NER digest == post-C1 | ✓ | See § Digest; CR deviation harmless |
| SC3 text params work, numeric compile-once kept | `PackLoader.DeclareShared` branch on `$`; tests 1–5; Starter E1/E2 equivalence probes | ✓ | |
| SC4 guards | tests 6–16; lint probes L1, L3, L5 | ✓ | Test 15 vacuous w.r.t. Expr change (finding) |
| SC5 identical lint/run/digest after C1, 3 packs | auditor rerun, see § Balance Bar | ✓ | |
| SC6 tests/Sim green | `aud-c/tests.txt`: `All checks passed`, exit 0; `TestTextParams()` count 2 | ✓ | |
| SC7 docs | `docs/packs.md`, `expressions.md`, `text.md`, `testing.md` diffs | ⚠ | Bare-form example broken; digest coverage overclaimed in code comment |
| SC8 no game csproj build | `git diff --stat 6b99edf..9de7421 -- '*.csproj'` empty; every `dotnet` in log names `tools/EfSim`/`tests/Sim` | ✓ | Auditor built only `tools/EfSim` (repo + scratch extract of `c95f16d`) and `tests/Sim` |
| Deviation: CR stripped in digest-base | bytewise CRLF compare of full `--digest` outputs pre/post C1 | ✓ | Cannot hide a difference: digest lines are ASCII hex/ids; full CRLF files also compare equal |
| Deviation: error-message wording | `PackLoader.ScanTextUse` names actual param | ✓ | Improvement |
| Deviation: `packs.md` reworded for V5a | "Plain parameters are numbers, read in the body as `arg.NAME`." | ✓ | Accurate |
| Deviation: test fixes | see § Testing | ✓ | None weakens an assertion materially; bare-form assertion is thinner than plan (minor) |

---

## Digest Verification (focus 1)

Mutations in scratch copies only (never the repo, never NER's tree). Pairs compared on table (stdout minus `digest` lines) and digest hashes.

**Starter copies** (`aud-c/var/*`, outputs `aud-c/varout/*`; `S0-control` == repo Starter bytewise):

| Pair | Mutation | Table | Digest |
|------|----------|-------|--------|
| S3a vs S3b | quiet `reputation` faction `security`→`upper_level` | same | **DIFF** |
| S3a vs S3c | quiet `reputation` amount 1→2 | same | **DIFF** |
| S4a vs S4b | `night_log` key `gs_room_office`→`gs_room_pit` | same | **DIFF** |
| S0 vs S2 | end memory `memory.cashboxes` +1→+2 | same | **DIFF** |
| S0 vs S1 | `credits` `2d10`→`2d12` | same | **same** ✗ blind spot |
| S0 vs E1 | hand-written `add memory.cashboxes` vs text-param `bump "cashboxes"` | same | same ✓ (equivalent refactor) |
| S3a vs E2 | hand-written quiet rep vs text-param `book {"$f":"security","n":1}` | same | same ✓ |

**NER copies** (`aud-c/ner/*`, kit.json one-line mutations; vs auditor's unmutated NER run `aud-c/NER-d.txt`):

| Copy | Mutation (`kit.json`) | Table rows changed | Digest rows changed (of 39) | Row `expect` FAIL |
|------|-----------------------|--------------------|-----------------------------|-------------------|
| N1 | security gain block `faction` → `upper_level` | 1 | 20 | 1 (`runs[35]: rep_exact.upper_level`) |
| N2 | lower_level gain `value_x100` → `run.rep_out + 1` | **0** | 38 | 0 |
| N3 | black_market gain night-log key → `ne_rep_up_lower` | **0** | 37 | 0 |
| N4 | `memory.rep_last_area` → `run.area_no + 100` | 12 | 39 | 2 |

N2 and N3 pass lint, the table and every existing row expect, and are caught only by the digest — the exact failure mode the proposal named. All four lint clean (`varout/N*-lint.txt`).

**CR deviation:** Git Bash `grep` (without `-U`) drops `\r`, so `*-digest-base.txt` are LF while stdout is CRLF. Step 6 used the same pipeline both sides → like for like. Auditor also compared the full CRLF `--digest` outputs bytewise (`c-proof/digest/*-d1.txt` vs `aud-c/*-d.txt`): identical for all 3 packs. Digest lines carry only `[a-z0-9_ \[\]]` + hex, so a CR-only difference has no meaning; nothing hidden.

**Baseline-before-C1:** commit times show Step 3/4 committed 4 min after Step 1 (`07:21`→`07:25`) while Step 2's runs used `--no-build`, so which binary produced `digest-base` was not provable from artifacts. Auditor rebuilt `tools/EfSim` from a `git archive c95f16d` extract (scratch `aud-c/pre/`) and ran NER `--digest`: output `aud-c/pre-NER-d.txt` == post-C1 `aud-c/NER-d.txt` bytewise (CRLF kept). The baseline is valid whichever binary Step 2 used. The same pre-C1 build rejects probe L3 (`arg.$f` in a `set` value) with the identical `unknown path 'arg.'` error, which confirms that test case 15 does not depend on the `Expr.cs` change.

---

## Code/Implementation Inspection

### `src/Core/PackLoader.cs` (C1)

**Claimed:** `$` params, `{arg.$NAME}` substitution in values only, per-site compile, guards, cycle check, uncalled warning; numeric path unchanged.
**Actual:** Matches. Quality: High.

- Values only: `Fill` copies keys verbatim; `ScanTextUse` reports any key containing `{arg.$` (`PackLoader.cs:514-537`). ✓
- Per-call-site compile: text effects skip `sharedBodies`; `CompileSharedCall` compiles `Fill(rawBody)` at `call.Path + ".do"` (`:591-597`). Deep copy, raw body never mutated. ✓
- Numeric path: identical code moved under `if (!parameters.Any($))`; only addition is the read-only `ScanTextUse` (problems only if a numeric body contains `{arg.$`/`arg.$` — none in the 3 packs, lint byte-identical). ✓
- Guards: number → "is text"; missing → "needs $f"; caller value checked against `^[a-z0-9_]+$`; bad name/default reported at declare. ✓
- Cycle: `expanding` set keyed `pack:name`, `try/finally` removal; self → exactly 1 problem, mutual → ≥1, no hang (tests 13). Fan-out probe L6 (4 levels × 4 calls = 256 per-site compiles) lints clean and fast. ✓
- `current` stays the caller's pack during per-site compile; text keys and memory reads/writes use the explicit `pack`/`text.Pack`, not `current` (`PackLoader.cs:772-773`, `:1424`) — only problem attribution moves, as the plan intends. ✓
- `uncalledText.Remove` runs before any error return (`:560`), so a bad call doesn't also warn "never called". ✓

**Issues Found:**
- Invalid default is still filled into the body when the caller omits it (`:576-578` only validates caller-given text) → second error at the call site (probe L5: `a text parameter's value is …` + `unknown faction 'Security Guys'`). Contradicts the stated "avoid a second, confusing error" approach used for unknown placeholders. — Severity: Low
- `BareTextArg` (`:156`) has no left word boundary: any string containing `arg.$` (e.g. a var named `run.bigarg.$x`, or text) is flagged. Practically unreachable today. — Severity: Low
- Per-site compile cost grows k^d for nested fan-out; no cap. Informational.

### `src/Core/Expr.cs`

**Actual:** `case "arg"` returns null for `$` names (`Expr.cs:149`). In expressions `$` is not a word char (`IsWordChar`, `:340`), so `arg.$f` tokenizes as `arg.` and fails as `unknown path 'arg.'` before and after the change (probe L3). The edit only matters for raw `Expr.Reader(path, raw: true)` callers (`Builtins.Assign`), which fail at `Writer` anyway. Harmless, effectively defensive. — Severity: Low (the misleading error text is the user-facing issue).

### `src/Core/ExpeditionRun.cs`, `src/Core/Model.cs` (focus 4)

- `Logged` (`ExpeditionRun.cs:359`, invoked `:367`): auto-property `Action<LogLine>`, invoked after the log append/trim; one null-check on the hot path; nothing in `src/Game` or `api/` sets or reads it. Observe-only ✓ — caveat: a subscriber that throws would now propagate out of `Log` (only ef-sim subscribes; `List.Add` can't throw here).
- `MemoryStore.Numbers` / `Texts` (`Model.cs:565-566`): expose the live dictionaries as `IEnumerable` — read-only by type, but a caller could down-cast to `Dictionary`. RunDigest only enumerates. Observe-only ✓.
- Game assembly risk: game csproj compiles `src\**\*.cs` for `netstandard2.1`. New APIs used in `src/Core`: `Regex` (first use in the game assembly; part of netstandard2.1), `string.StartsWith(string, StringComparison)`, value-tuple deconstruction in `foreach` (already used at `PackLoader.cs:222-235`). No net8-only API in `src/Core` (`IncrementalHash` is in `tools/EfSim/RunDigest.cs`, outside the game build). `src/Game` has no `partial class ExpeditionRun`/`MemoryStore` and no member named `Logged`/`Numbers`/`Texts` on those types (grep). Not compile-checked (forbidden) — Low risk.

### `tools/EfSim` (digest)

- `RunDigest` record: outcome, every log line (via `Logged`, uncapped), night lines, non-zero memory numbers / non-empty texts sorted ordinal, reputation sorted ordinal with `"R"` invariant format. Deterministic (d1 == d2 == auditor rerun). ✓
- Not hashed: run vars (intentional, documented), **credits, carried items, ground pile, take-back/lose results** (not documented). `credits` and `lose_found`/`consume` change state silently (no log line), so a refactor can alter them without any digest change (probe S1). — Severity: Medium
- `memory is MemoryStore` guard silently skips memory if a store of another type ever arrives. Today always `MemoryStore` (`SimConfig.Start`). Low.

---

## Testing Validation

**Execution:** `dotnet run -c Release --project tests/Sim` → `All checks passed`, exit 0 (`aud-c/tests.txt`). `TestTextParams()` defined + called (count 2).
**Coverage:** plan cases 1–16 all present (`tests/Sim/Program.cs` `TestTextParams`).

**Executor's three test-side fixes:**
1. Bare form asserts `Rep` key **absent** + `k_security` registered (`Program.cs:484`): the call passes `n` = default 0, and `reputation` with 0 returns before calling the bridge (`Builtins.cs:440`), so "absent" is the correct expectation, not a weakening. But the case no longer proves the bare value reaches `reputation.faction` — only the text key. Thinner than plan ("same as named"); acceptable since one `Fill` serves all fields. Minor.
2. Undeclared placeholder uses a single-use body → exactly 1 problem: `ScanTextUse` reports once per occurrence; assertion still exact. Not weakened.
3. Message names the real param (`write {arg.$f}`): code change toward the plan's case 10; assertion unchanged. Not weakened.

**Quality Issues:** case 15 passes independently of the `Expr.cs` change (see above). Case 5 asserts only the run result, not compile-once (plan allowed).
**Missing:** cross-pack text-param call; bare form with a non-first text param required (the docs example's failure mode); invalid default + omitted arg.

---

## Documentation Audit

**Completeness:** packs.md, expressions.md, text.md, testing.md updated — Complete.
**Accuracy:** Partial.
- `docs/packs.md:163` `{ "settle_rep": "security" }` + `:166` "The second form fills the first parameter, as for numbers" — with the example's `"$short": ""` (required) this is a load error (probe L1: `settle_rep needs $short`). Fix: give `$short` a default, or drop the bare line / show it on a one-text-param effect.
- `docs/testing.md:75-90` lists what is hashed accurately but omits what is not (credits, items); "the digest is the part that proves the rest. Any difference is a bug" reads as complete coverage. `tools/EfSim/RunDigest.cs:9` "everything … that a player could see" is false for credits/items.
- `docs/expressions.md` `arg.NAME` row note: accurate. `docs/text.md`: accurate (lint checks the filled key — L4 probe registers both keys, lint 0/0).
- testing.md sample `digest runs[0] night_market blade random 9b1f...` matches Showcase row 0 shape. Anchors `packs.md#text-parameters` resolve to `#### Text parameters`.

---

## Source Hygiene

**Scope:** Diff only (`b53730f..9de7421`, excluding `**/.projex/**`).

| Rule | Location (symbol) | Quote | Severity |
|------|-------------------|-------|----------|
| — | — | No projex IDs, step labels, `file:ln`, banners, or changelog narration in added lines (regex sweep + read) | — |

**Rule 6 gaps:**
- `PackLoader` → `CompileSharedCall`, `uncalledText.Remove` as first statement — ordering is deliberate (a bad call must not also warn "never called") but uncommented; a later "move after validation" edit would reintroduce the double report. Low.

**Rule 6 vacuity:** none. Per-site compile, values-only, a-z/0-9/_ rule, SHA-256-vs-GetHashCode, run-vars exclusion, digest-after-summary, `Fill` copy all name the rejected alternative or the trap.

**Accuracy (not a hygiene rule):** `RunDigest` class comment overclaims coverage (see Documentation).

**Rationale promotion:** values-only, value rule, digest contents and the refactor recipe are promoted to `docs/packs.md` / `docs/testing.md`. Walkthrough not yet written (close step) — its `**Rationale promoted:**` line should name those two docs.

**Commit composition** (informational — fix forward, never rewrite history): 4 landed commits touch files outside `.projex/` (`c95f16d`, `d99d283`, `2e1661e`, `a579ff8`) — `Projex:` trailer 4/4; subjects are ephemeral `projex: step …` (allowed). Landing commit not yet made (close squash).
**Violations:** None.

**Trailer survival on `main`:** 5/30 recent commits carry `Projex:` — OK (channel alive; untrailered ones are mostly doc/plan commits or pre-convention direct work).

---

## Gap Analysis

### Promised But Not Delivered
| Promise | Status | Impact |
|---------|--------|--------|
| Docs example usable as written | Partial (bare line errors) | Medium — copy-paste authors hit a load error |
| Digest = proof of "behaviour left alone" | Partial (credits/items unhashed) | Medium — future refactors touching loot/credits get a false pass |

### Undocumented Issues
| Issue | Severity | Affects |
|-------|----------|---------|
| Digest omits credits / items / ground | Medium | ef-sim proof bar, testing.md recipe |
| Invalid default filled → duplicate error | Low | pack authors |
| `arg.$f` in an expression reports `unknown path 'arg.'` | Low | pack authors |
| Execution-log timestamps (07:30…09:10) disagree with commit times (07:21…07:30); log `:21` path mangled to `c-proofase` (`\b` escape) | Low | provenance |

### Unhandled Edge Cases
- Text-param effect called only from another uncalled text-param effect → both warn "never called" (true, but the inner one's warning is a consequence). Low.
- `BareTextArg` matches inside longer identifiers. Low.

---

## Quality Assessment

### Completeness: High
**Strengths:** proof sequence executed in order; digest determinism and neutrality shown; 16 test cases; docs in 4 files.
**Gaps:** digest coverage, one broken docs example.

### Correctness: High
**Works:** substitution values-only: Yes | per-site compile: Yes | numeric path unchanged: Yes (byte-identical NER/SC/ST) | guards: Yes | cycles: Yes | uncalled warning once: Yes.
**Bugs:** duplicate error on invalid default — Low.

### Code Quality: High
**Positive:** small, localized; copy-not-mutate; rationale comments name alternatives; observe-only hooks.
**Tech Debt:** digest record format will need extending (credits/items) — changing it invalidates frozen digest baselines, so do it before the NER refactor baselines. Low.

### Value Delivered: High
**Intended:** language feature to remove NER's 5× settle copy + a proof instrument stronger than the table.
**Actual:** both; digest demonstrably catches faction / amount / night-log key / memory changes the table misses, and an equivalent text-param refactor yields an identical digest.

---

## Findings

### Critical (Must Address)
- None.

### Significant (Should Address)
- **Digest blind to credits and inventory** (`tools/EfSim/RunDigest.cs:23-38`) — probe S1 (`credits 2d10→2d12`) changes neither table nor digest; class comment (`:9`) and `docs/testing.md:75-90` imply full observable coverage. → Append to the record, after reputation: `C <bridge.Credits>`; carried items sorted ordinal `I <id>\t<count>\t<props…>`; ground pile in order `G <id>`. Needs `KitBridge` read-only accessors (`Items()` exists; add `Ground` view). Land and re-baseline before the NER `rep_book` plan captures its baseline; document the list (and what is excluded) in testing.md. Alternatively, keep the record and correct the comment + testing.md to name credits/items as not covered.
- **Broken docs example** (`docs/packs.md:163`, `:166`) — `{ "settle_rep": "security" }` fails `settle_rep needs $short`. → Patch (doc-only): replace `:163` with `{ "settle_rep": { "$faction": "upper_level", "$short": "upper" } }` and `:166` with "A bare string, `{ "name": "text" }`, fills the first parameter as for numbers; every other text parameter then needs a default." Giving `$short` a default instead would make the example's night-log key silently shared across factions.

### Minor (Nice to Fix)
- **Invalid default filled into body** (`src/Core/PackLoader.cs:576-578`) → when `!given && !TextValue.IsMatch(text)`, `return s => { }` silently (default already reported at declare).
- **`arg.$NAME` in an expression gives `unknown path 'arg.'`** (`src/Core/Expr.cs:340`, `:386`) → in `Atom`, if the next char after a word ending in `arg.` is `$`, `Fail("arg.$NAME is text: write {arg.$NAME} in a shared effect's body")`; add a test asserting that message so case 15 exercises the new behaviour.
- **Bare-form test thinner than plan** (`tests/Sim/Program.cs:484`) → call a one-param effect `{ "book1": "security" }` with fixed `value: 1` and assert `Rep["security"] == 1`.
- **Rule 6 gap** `CompileSharedCall` first line → comment: "Before the checks below: a call with bad arguments still counts as a call, or the effect would also warn that it is never called."
- **`BareTextArg` word boundary** (`PackLoader.cs:156`) → `(?<![a-z0-9_.])arg\.\$([a-z0-9_]*)`.
- **Log provenance** (`2611140627-text-params-and-sim-digest-log.md:21`, step headers) → fix `c-proof\base\` path; timestamps should match commit times.

### Positive
- Proof ordering held: digest frozen pre-C1, and independently re-proven by building `c95f16d` from a scratch extract.
- Digest neutrality is structural (printed after summary, observe-only callback, no RNG) and verified bytewise with CR preserved.
- Executor's deviations are honest and correctly characterized; no assertion weakened to pass.

---

## Recommendations

**Immediate:** fix `docs/packs.md` bare-form example before merge (doc-only); decide credits/items digest coverage before the NER refactor plan baselines.
**Future:** friendlier `arg.$` expression error; default-fill early return; test additions above.
**Process:** proof scripts should use `grep -U` (or `tr -d '\r'` both sides) by default on Windows; execution logs should take timestamps from `date` at write time.

---

## Final Verdict

**Status:** Accept with Conditions

**Overall Assessment:**
- Completeness: High
- Correctness: High
- Quality: High
- Value: High

**Conditions:**
- [ ] Fix `docs/packs.md:163-166` bare-form example (doc-only patch).
- [ ] Before the NER `rep_book` plan captures its digest baseline: either extend `RunDigest` to credits/items or state the exclusion in `RunDigest` comment + `docs/testing.md`.

**Sign-off:** Yes — balance bar independently reproduced (lint / run / digest identical for all 3 packs, pre- and post-C1 binaries), C1 semantics correct, `src/Core` additions observe-only; remaining issues are doc accuracy and proof-instrument coverage, none affecting shipped behaviour.
