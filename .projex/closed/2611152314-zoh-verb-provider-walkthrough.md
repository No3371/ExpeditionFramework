# Walkthrough: EF as ZoH Verb Provider (`/ef.rumour`, `/ef.tell`)

> **Execution Date:** 2026-11-16
> **Completed By:** Claude Fable 5.2 (execute agent; close agent wrote this record)
> **Source Plan:** 2611152314-zoh-verb-provider-plan.md
> **Parent:** 2611152314-zoh-verb-provider-plan.md
> **Log:** 2611152314-zoh-verb-provider-log.md
> **Duration:** ~20 min execution (05:38 → 05:56), close same day
> **Result:** Partial Success — every offline criterion passed; in-game checks J1–J5 not run (game never launched in this run; they are the human's)

---

## Summary

EF is now an optional ZoH Framework (ZF) verb provider. `ModEntry` implements `ZohFramework.Api.IStoryExtension`; a story with `uses: ["expedition_framework"]` gets `/ef.rumour` and `/ef.tell`, which tell the calling pack's told intel through `IntelBoard`. EF ships a byte-identical copy of the installed `ZohFramework.Api.dll`, listed in the manifest, with no prerequisite on ZF. Build, scanner, tests and sim baselines pass; the behaviour inside the game (SC3–SC5, J5 of SC7) is unverified.

---

## Objectives Completion

| Objective | Status | Notes |
|-----------|--------|-------|
| SC1 build references + installs the api dll, fails loudly without it | Complete | V1, V2; V1 re-run at close |
| SC2 manifest lists the dll, no prerequisite, scanner clean | Complete | V3 ×3 (executor), V4 re-run at close |
| SC3 `ModEntry : IMod, IStoryExtension`, Live only, no call into ZF, never throws | Partial | Offline: V7 + scratch `ProviderCheck` 13/13. In game: J1, J2 open |
| SC4 `/ef.rumour` params, own pack only, key or `nothing` | Partial | Offline: non-pack path only. Pack path needs the game: J2, J3, J4 open |
| SC5 `tell: false` reservation + `/ef.tell` | Partial | Offline: no arg / unknown key / non-string → `false`. Rest: J3 open |
| SC6 `IntelRoller.CardKey` tested | Complete | `TestCardKey`, 7 cases |
| SC7 docs | Partial | Present and linked (V4). J5 open: example uses small-talk syntax of a ZF plan not yet on ZF master |
| SC8 `tests/Sim` green, sim baselines unchanged | Complete | `All checks passed` (re-run at close); `cmp` ×6 exit 0 (executor) |

---

## Execution Detail

> Derived from `git diff main..projex/2611152314-zoh-verb-provider` and the execution log. Line numbers are those of the final tree.

### Step 0: Baselines (not a plan step; plan V5 `$Q/before`)

**Actual:** EfSim built; `lint --strict` + `run` for Starter, Showcase, SampleArea written to scratch before any source edit.
**Deviation:** taken on the ephemeral branch, not on `main` — same tree.

### Step 1: csproj — copy-local `ZohFramework.Api` + install copy

**Planned:** `ZohApi` property, copy-local reference, `CheckZohApi` error target, api dll in the `InstallMod` copy.
**Actual:** as planned, verbatim. Commit fbe8c9d.
**Deviation:** none in code. V1 used `ls -lR` (not `ls -l`) on the live mod folder; V2 `grep -c` gave 2, not 1 (msbuild prints the error inline and in its summary).

| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `ExpeditionFramework.csproj` | Modified | Yes | L10–11 `ZohApi` property + comment; L35–37 `Reference Include="ZohFramework.Api"` (`Private` true); L39–41 `CheckZohApi` target; L48 `$(OutDir)ZohFramework.Api.dll` added to the install copy |

**Verification:** V1 ✓ build exit 0, 0 warnings / 0 errors, live `Mods/ExpeditionFramework` listing identical before/after, installed copy `cmp` exit 0 against `Mods/ZohFramework/ZohFramework.Api.dll`. V2 ✓ build exit 1 with `ZohFramework.Api.dll not found at …`.
**Issues:** a ZF api dll rebuilt from ZF master (another plan's scratch build) is not byte-identical to the installed one — see Issue 1.

### Step 2: manifest — list the api dll

**Planned:** `<Assembly>ZohFramework.Api.dll</Assembly>` first in `<Assemblies>`.
**Actual:** as planned. Commit 412025e.
**Deviation:** ScanCheck run from a scratch copy of ZF's `tests/ScanCheck` (sources unchanged) with `-p:GameManaged=<abs>`, not `--project $ZF/tests/ScanCheck`: building in place would write `bin/` `obj/` into the ZF repo.

| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `manifest.xml` | Modified | Yes | L13 new first `<Assembly>` entry; `<Prerequisites />` untouched |

**Verification:** V3 ✓ `HARD FLAGS: 0`, `CAPABILITIES: none` (listed-payload half; the reference half is the Step 4 rerun). V4 manifest greps 1 / 1.

### Step 3: `IntelRoller.CardKey` (Core) + `TestCardKey`

**Planned:** pure key-template fn in Core, five test cases.
**Actual:** fn verbatim; seven cases (two added: a placeholder used twice; all ids null → `"__"`). Commit fdf64bb.
**Deviation:** two extra test cases.

| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Core/ExpeditionRun.cs` | Modified | Yes | L1365–1373 `IntelRoller.CardKey(template, areaId, slotId, pick)` |
| `tests/Sim/Program.cs` | Modified | Yes | L51 call; L920–934 `TestCardKey` |

**Verification:** `dotnet run -c Release --project tests/Sim` → `All checks passed`.

### Step 4: `ModEntry : IStoryExtension` + `StoryProvider`

**Planned:** `RegisterStory` → `StoryProvider.Register` (claim + two Live verbs); `Rumour`, `Tell`, `Reserve`, per-consumer day-stamped reservations.
**Actual:** logic as planned. Commit 483b2a3.
**Deviation:** (1) `ModEntry` comment drops the plan's "once per such story" (ZF's README says `RegisterStory` can run more than once for one story) and says why there is no prerequisite; `Register` gets a summary saying it can repeat. (2) Comments name the rejected alternatives (claim only known packs | separate ticket | `StoryVerbException`). (3) `Tell` guards `call.Args != null`.

| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Game/ModEntry.cs` | Modified | Yes | L1 `using ZohFramework.Api;`; L5–9 comment + `: IMod, IStoryExtension`; L37 `RegisterStory` |
| `src/Game/StoryProvider.cs` | Created | Yes | 79 lines: `Register`, `Rumour`, `Tell`, `Reserve`, `Today`, `Named`, `Held` |

**Verification:** V1 rerun ✓; V3 rerun ✓ (settles assumption B1 with the reference in use); V7 ✓ (`ZohFramework.dll` 0 hits; `StoryPass.(Setup|Schedule)` 0; `IStoryExtension` 2; `api`, `tools/EfSim` undiffed); V6 ✓. Extra, not in plan: scratch `ProviderCheck` (net8 console loading the built dll with a fake registrar / call) 13/13 — references `ZohFramework.Api` only; registering twice yields exactly `ef.rumour`, `ef.tell` with no passes; non-pack consumer → `null` + one `Warn` across three calls; `ef.tell` → `false` for no arg / unknown key / non-string; claim recorded. B3 (provider called per consumer with the consumer's id) read from ZF master source only.
**Issues:** the pack path (`IntelBoard.Tell` / `Show`) can't run outside the game.

### Step 5: Docs

**Planned:** new `docs/zoh.md`; links from `README.md`, `docs/README.md`, `docs/intel.md`; build note in `docs/contributing.md`.
**Actual:** as planned; `docs/zoh.md` has two example stories (plain tell; `tell: false` + `/choose` + `/ef.tell`) and an in-game checks section. Commit 66a22eb.
**Deviation:** the plan said to copy small-talk syntax from ZF's README "once the ZF plan lands"; it had not landed. The `smalltalk:` header and `@smalltalk_arrive` label come from 2611152320-zoh-customer-small-talk-plan.md (ZohFramework), still executing. The page names ZF's README as the reference. Example stories were not run.

| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `docs/zoh.md` | Created | Yes | 114 lines |
| `docs/README.md` | Modified | Yes | L29 Reference table row |
| `README.md` | Modified | Yes | L84–85 reference list entry |
| `docs/intel.md` | Modified | Yes | L92 § Told slots sentence → link |
| `docs/contributing.md` | Modified | Yes | L16–18 `ZohFramework.Api.dll` / `-p:ZohApi=` note |

**Verification:** V4 ✓; anchors `intel.md#told-slots`, `intel.md#the-picked-id-in-text`, `#the-text-keys` exist.

### Completion (e929f37)

Whole suite rerun on the final tree (V1–V8 ✓); proposal § 2 compliance read; log rebuilt (Issue 3).

---

## Complete Change Log

> **Derived from:** `git diff --stat main..projex/2611152314-zoh-verb-provider` — 13 files, +356 / −5 (incl. plan + log).

### Files Created
| File | Purpose | Lines | In Plan? |
|------|---------|-------|----------|
| `src/Game/StoryProvider.cs` | the `ef.*` verbs, reservations | 79 | Yes |
| `docs/zoh.md` | pack author page: `uses:`, verbs, keys, examples | 114 | Yes |
| `.projex/2611152314-zoh-verb-provider-log.md` | execution log | 105 | — |

### Files Modified
| File | Changes | Lines Affected | In Plan? |
|------|---------|----------------|----------|
| `ExpeditionFramework.csproj` | `ZohApi`, reference, `CheckZohApi`, install copy | 10–11, 35–41, 48 (+11 −1) | Yes |
| `manifest.xml` | api dll listed | 13 (+1) | Yes |
| `src/Core/ExpeditionRun.cs` | `IntelRoller.CardKey` | 1365–1373 (+10) | Yes |
| `src/Game/ModEntry.cs` | `IStoryExtension`, `RegisterStory` | 1–9, 37 (+9 −1) | Yes |
| `tests/Sim/Program.cs` | `TestCardKey` | 51, 920–934 (+17) | Yes |
| `README.md` | link | 84–85 (+2 −1) | Yes |
| `docs/README.md` | table row | 29 (+1) | Yes |
| `docs/intel.md` | link sentence | 92 (+1 −1) | Yes |
| `docs/contributing.md` | build note | 16–18 (+4) | Yes |
| `.projex/2611152314-zoh-verb-provider-plan.md` | status, log link | header | — |

### Files Deleted
None.

### Planned But Not Changed
None.

---

## Success Criteria Verification

Close-time re-runs (2026-11-16, close agent, on the ephemeral branch tip e929f37) are marked **re-run**; the rest is the executor's evidence from the log.

### SC1 — build references, installs, fails loudly
**Method:** V1, V2.
**Evidence:** **re-run** `dotnet build -c Release ExpeditionFramework.csproj -p:InstallDir=<scratch>/close/install/` → 0 warnings, 0 errors; install holds `ExpeditionFramework.dll`, `ExpeditionFramework.Api.dll`, `ZohFramework.Api.dll` (6144 B), `manifest.xml`, `Localization/`, `ExpeditionFramework/`; `cmp` against `Mods/ZohFramework/ZohFramework.Api.dll` exit 0; `ls -lR` of live `Mods/ExpeditionFramework` identical before/after. V2 (executor): exit 1, `error : ZohFramework.Api.dll not found at …/missing.dll. Install ZoH Framework into Mods\ZohFramework\ or pass -p:ZohApi=<path to its ZohFramework.Api.dll>.` `ZohFramework.dll` never referenced: **re-run** grep 0 hits.
**Result:** PASS

### SC2 — manifest + scanner
**Method:** V3, V4.
**Evidence:** **re-run** greps: `<Assembly>ZohFramework.Api.dll</Assembly>` 1; `<Prerequisites />` 1. ScanCheck (executor, three runs incl. final tree): `HARD FLAGS: 0`, `CAPABILITIES: none`. Not re-run at close.
**Result:** PASS

### SC3 — extension wiring
**Method:** V7, `ProviderCheck`; J1, J2.
**Evidence:** **re-run** `IStoryExtension` in `ModEntry.cs` 2; `StoryPass.(Setup|Schedule)` 0. `ProviderCheck` 13/13 (executor, scratch, not committed).
**Result:** PASS offline — UNVERIFIED in game (J1, J2)

### SC4 — `/ef.rumour`
**Method:** code read against the plan; `ProviderCheck` non-pack path; J2, J3, J4.
**Evidence:** non-pack consumer → `null`, one `Warn`. Tell path never executed.
**Result:** UNVERIFIED (J2, J3, J4) — non-pack path PASS

### SC5 — reservations + `/ef.tell`
**Method:** `ProviderCheck` false paths; J3.
**Evidence:** no arg / unknown key / non-string → `false`. Reserve → show → lapse never executed.
**Result:** UNVERIFIED (J3)

### SC6 — `CardKey`
**Method:** V5, V6.
**Evidence:** **re-run** `tests/Sim` → `All checks passed`; `TestCardKey` 2; `CardKey` in `StoryProvider.cs` 1.
**Result:** PASS

### SC7 — docs
**Method:** V4; J5.
**Evidence:** **re-run** `zoh.md` linked once each from `README.md`, `docs/README.md`, `docs/intel.md`; `ZohApi` in `docs/contributing.md` 1.
**Result:** PASS for presence — J5 UNVERIFIED (example syntax vs ZF README once the small-talk plan lands)

### SC8 — sim
**Method:** V5.
**Evidence:** **re-run** `All checks passed`. Baselines `cmp` exit 0 ×6 (executor); `git diff --quiet main..projex/2611152314-zoh-verb-provider -- api tools/EfSim` exit 0 (**re-run**).
**Result:** PASS

### Acceptance Criteria Summary

| Criterion | Method | Result | Evidence |
|-----------|--------|--------|----------|
| SC1 | V1, V2 | Pass | build, cmp, error text |
| SC2 | V3, V4 | Pass | scanner 0 flags, greps |
| SC3 | V7, ProviderCheck / J1 J2 | Pass offline, in-game open | 13/13 |
| SC4 | J2 J3 J4 | Unverified (non-pack path pass) | — |
| SC5 | J3 | Unverified (false paths pass) | — |
| SC6 | V5, V6 | Pass | `TestCardKey` |
| SC7 | V4 / J5 | Pass, J5 open | greps |
| SC8 | V5 | Pass | `All checks passed`, cmp ×6 |

**Overall:** 4/8 fully passed (SC1, SC2, SC6, SC8); SC3, SC7 passed on everything checkable offline; SC4, SC5 rest on the in-game checks below. No criterion failed.

### In-game checks left open (human)

Install: the built mod in the scratch `game-install/` (or a fresh build) into `Mods/ExpeditionFramework/`; ZF with its `uses:` support for J1b–J4.

| ID | Do | Expect |
|----|----|--------|
| J1a | Start the game with ZF **absent**, EF + a pack with a `told: true` slot | EF loads (Mods menu; log `Expedition packs: …`); the shipped api dll logs only `No IMod implementation found`; told cards show at the day's roll as before |
| J1b | Start with ZF present | ZF log `ZohFramework.Api loaded from …` names one path; EF loads |
| J2 | ZF present + a consumer pack (`told: true` slot, `Stories/main.zoh` with `uses: ["expedition_framework"]`, `required_verbs` listing `ef.rumour`, a small-talk entry calling `/ef.rumour` then `/converse`); start a new day, wait for small talk | ZF log `Story mod <pack>: … C# verbs: ef.rumour, ef.tell`; told card absent from the map at day start; on the customer's line the card is on the area's map card; the line uses the pick's key |
| J3 | Story variant: `/ef.rumour tell: false`, then `/ef.tell *k;` on a choice. Also `area:` / `slot:` filters; also a non-pack mod whose story `uses` EF | Card appears only after the tell; declining leaves it absent; next day the old key gives `false` and the card is gone; filters narrow the pick; non-pack: one warning in ZF's log, line declines |
| J4 | Keep getting small talk after the slot budget / `intel_max` is spent | `/ef.rumour` gives `nothing`; the story declines with no empty line |
| J5 | Read `docs/zoh.md` against ZF's README after 2611152320-zoh-customer-small-talk-plan.md lands | Header (`smalltalk:`), label (`@smalltalk_arrive`), `/if`, `/choose` syntax match; a pack author can add rumours with data only |

---

## Deviations from Plan

1. **V2 count** — planned `grep -c` = 1 → actual 2 (msbuild repeats errors in its summary). No impact.
2. **V1 listing** — `ls -l` → `ls -lR`. Stricter.
3. **ScanCheck location** — run from a scratch copy, not inside the ZF repo. Same sources; keeps the other repo clean.
4. **Two extra `TestCardKey` cases.**
5. **Step 4 comments + `call.Args != null` guard** — comment wording corrected to ZF's documented repeat registration; logic otherwise verbatim.
6. **`ProviderCheck`** — extra offline harness, scratch only, not committed.
7. **Baselines on the ephemeral branch** — same tree as `main`.
8. **`docs/zoh.md` example written before the ZF small-talk plan landed** — plan wanted the syntax copied from ZF's README after landing. Recheck = J5.

All accepted by the orchestrator as in scope. No plan update needed.

---

## Issues Encountered

### Issue 1: rebuilt ZF api dll differs from the installed one
- **Description:** a `ZohFramework.Api.dll` built from ZF master in another plan's scratch dir is the same size (6144 B) but differs from the installed one from byte 137. EF ships the installed copy (SC1).
- **Severity:** Medium (install-time)
- **Resolution:** none here — ZF-side. When installing the new ZF, keep the already-installed `ZohFramework.Api.dll`, or rebuild EF afterwards so both copies match. Same name and version, so the first loaded serves both either way.
- **Prevention:** ZF install step should copy the frozen api dll, never a rebuilt one.

### Issue 2: claim lost when EF is disabled and re-enabled in one session
- **Description:** `PackRegistry.Stop` → `IntelBoard.Reset` clears claims; ZF calls `RegisterStory` again only when it rebuilds a consumer's set. Until then told slots roll with the day while the verbs still tell. `StoryProvider`'s reservations also outlive the reset and point at the old `IntelCards`; they lapse with the day.
- **Severity:** Low (needs a mid-session toggle)
- **Resolution:** left as is; outside the plan's steps.

### Issue 3: log truncated in two intermediate commits
- **Description:** a tool hook replaced a whole-file read of the log with a stub once the file passed ~4 KB; the append helper wrote the stub back. The log inside 483b2a3 and 66a22eb is truncated; source files in those commits are unaffected.
- **Severity:** Low
- **Resolution:** rebuilt in e929f37 (header + Steps 0–3 from fdf64bb, later entries from the executor's own text). The squash close leaves only the full log on `main`.
- **Prevention:** append to logs through the shell or slice reads; never read-modify-write a file through a tool whose output can be rewritten.

---

## Key Insights

**Rationale promoted:** csproj comment (installed dll, never rebuilt) | `ModEntry` type comment (why no prerequisite) | `StoryProvider` comments (claim before packs load; key as handle; warn, not `StoryVerbException`) | `docs/contributing.md` (copied as is) | `docs/zoh.md` § `required_verbs`.

### Lessons Learned
1. **Scanner first** — settling B1 (ScanCheck) after Steps 1–2 and again once a type used the api split "listed payload" from "reference in use"; both needed their own run.
2. **A fake registrar covers the wiring** — loading the built dll with stub `IStoryRegistrar` / `IStoryCall` proved registration, repeat calls and every refusal path without the game; only the board path stays in-game.

### Gotchas / Pitfalls
1. **msbuild error count** — an `<Error>` text appears twice in output; `grep -c` checks should expect ≥ 1.
2. **Deterministic ≠ reproducible across machines/paths** — the rebuilt frozen api dll differed in bytes (Issue 1).
3. **Hooked reads** — see Issue 3.

### Technical Insights
- Packs load after every `OnEnable`, so a provider can't ask "is this consumer a pack?" at registration; it claims always and answers at call time.
- `tests/Sim` compiles `src/Core` + `api` only; anything a verb computes that is pure belongs in Core to be testable.

---

## Recommendations

### Immediate Follow-ups
- [ ] Run J1–J4 in game once the whole set is installed.
- [ ] J5: recheck `docs/zoh.md` examples against ZF's README after 2611152320-zoh-customer-small-talk-plan.md lands.
- [ ] Decide the ZF install step for the api dll (Issue 1).

### Future Considerations
- Re-claim after an EF disable/enable in one session, and drop reservations on `IntelBoard.Reset` (Issue 2).

### Plan Improvements
- V2: expect ≥ 1. V3: say where ScanCheck may build. Step 5: don't depend on another repo's unlanded plan for example syntax, or order it after.

---

## Related Projex Updates

| Document | Update |
|----------|--------|
| 2611152314-zoh-verb-provider-plan.md | Complete; walkthrough + log linked; moved to closed |
| 2611152314-zoh-verb-provider-log.md | moved to closed |
| 2611152305-told-intel-and-zoh-verbs-proposal.md | both plans closed → walkthrough linked; moved to closed |
| 2611152305-customer-rumours-proposal.md (NER) | unblocked; its own cycle |

### New Projex Suggested
| Type | Description |
|------|-------------|
| Patch | Issue 2: re-claim / drop reservations on EF re-enable — only if it shows up in play |

---

## Appendix

### References
- Step commits (squashed at close): fbe8c9d | 412025e | fdf64bb | 483b2a3 | 66a22eb | e929f37
- Base: `main` @ 5f5f37b
