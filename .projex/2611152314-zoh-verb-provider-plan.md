# EF as ZoH Verb Provider (`/ef.rumour`, `/ef.tell`)

> **Status:** In Progress
> **Author:** Claude Fable 5.1 (plan step)
> **Parent:** 2611152305-told-intel-and-zoh-verbs-proposal.md
> **Source:** 2611152305-told-intel-and-zoh-verbs-proposal.md (Complete (Accepted)) § 2 EF as optional ZoH verb provider
> **Related Projex:** 2611152314-told-intel-slots-and-board-plan.md (sibling; Required — `IntelBoard`; closed — 2611152314-told-intel-slots-and-board-walkthrough.md) | 2611152320-zoh-optional-consumers-and-verb-providers-plan.md (ZohFramework plan; `uses:` providers — Required) | 2611152305-customer-small-talk-proposal.md (ZohFramework proposal; § 4 `uses:` providers) | 2611152305-customer-rumours-proposal.md (NER; first consumer) | 2611142108-sim-tally-and-intel-cap-plan.md (closed; proof conventions)
> **Worktree:** No — ~9 files; the csproj's relative `GameManaged` and new `ZohApi` paths (`..\..\Mods\ZohFramework\`) resolve from the main checkout only. Override only together with `-p:ZohApi=…` and `-p:GameManaged=…` on every build.

---

## Summary

EF's `ModEntry` also implements `ZohFramework.Api.IStoryExtension`. EF ships a **copy** of the installed `Mods/ZohFramework/ZohFramework.Api.dll` (never rebuilt) and lists it in `<Assemblies>`. When ZohFramework (ZF) calls `RegisterStory` for a consumer whose story has `uses: ["expedition_framework"]`, EF claims that consumer id on the `IntelBoard` and registers two Live verbs into that consumer's story: `/ef.rumour;` (tell one told card of the consumer's own pack, return its loc key) and `/ef.tell "key";` (show a card reserved with `tell: false`). Without ZF installed nothing calls `RegisterStory`, nothing is claimed, EF behaves as today. `<Prerequisites />` unchanged.

**Scope:** EF only — csproj, manifest, `src/Game/ModEntry.cs`, new `src/Game/StoryProvider.cs`, `src/Core/ExpeditionRun.cs` (`IntelRoller.CardKey`), `tests/Sim`, docs (`zoh.md` new, README/docs index, intel, contributing).
**Estimated Changes:** 2 build files (~10 lines), 2 Game files (~110 lines, 1 new), 1 Core file (~12 lines), 1 test (~20 lines), 5 docs (~70 lines).

---

## Objective

### Problem / Gap / Need
A data-only pack has no way to hand its told intel to a customer line. ZF's `uses:` lets a provider mod register C# verbs into a consumer's story; EF is the first provider, so a pack adds customer-told intel with data only: `told: true` slots + a `Stories/main.zoh` that `uses` EF.

### Success Criteria
- [ ] SC1 — Build: `ExpeditionFramework.csproj` references `ZohFramework.Api` from `$(ZohApi)` (default `..\..\Mods\ZohFramework\ZohFramework.Api.dll`, copy-local) and fails with a clear message when the file is missing; `InstallMod` copies `ZohFramework.Api.dll` next to `ExpeditionFramework.dll`; the copy is byte-identical to the installed one; `ZohFramework.dll` is never referenced.
- [ ] SC2 — `manifest.xml` lists `ZohFramework.Api.dll` in `<Assemblies>`; `<Prerequisites />` stays empty; ZF's `tests/ScanCheck` on the built mod folder prints `HARD FLAGS: 0` and `CAPABILITIES: none`.
- [ ] SC3 — `ModEntry : IMod, IStoryExtension`; `RegisterStory(story)` → `IntelBoard.Claim(story.ConsumerId)` + `AddVerb("ef.rumour", …)` + `AddVerb("ef.tell", …)`, Live pass only; never calls into ZF; never throws (a failure is a `call.Warn` or log line).
- [ ] SC4 — `/ef.rumour;` named params: `key:` template (default `"rumour_{pick}"`; placeholders `{area}` `{slot}` `{pick}`, pack prefix stripped from `{area}` and `{pick}`), `area:`, `slot:`, `tell:` (bool, default `true`). Tells one told card of **the calling consumer's pack** via `IntelBoard.Tell(consumer, area, slot, show: tell)`; returns the filled key (string) or `null` (ZoH `nothing`) when no candidate. Consumer that isn't a loaded pack → one `call.Warn` per consumer per session, `null`.
- [ ] SC5 — `tell: false` reserves the card (held; counts toward budget and `intel_max`); `/ef.tell "key";` shows it and returns `true`; unknown key, already shown, or a key from an earlier day → `false`. Reservations lapse at day end.
- [ ] SC6 — `IntelRoller.CardKey(template, areaId, slotId, pick)` (Core, tested): fills the three placeholders, strips `pack:` prefixes, leaves other text as is.
- [ ] SC7 — Docs: new `docs/zoh.md` (what a pack writes: `uses`, the two verbs, params, return values, loc keys the pack must ship, `required_verbs` listed explicitly not `"*"`, no prerequisite on ZF); linked from `README.md`, `docs/README.md`, `docs/intel.md` § Told slots; `docs/contributing.md` says the game build needs the installed ZF (or `-p:ZohApi=`).
- [ ] SC8 — `tests/Sim` green; `ef-sim` lint/run baselines for Starter, Showcase, SampleArea unchanged.

### Out of Scope
- Changes inside ZohFramework (its `uses:` plan). Small-talk timing, speaker choice, customer registry.
- Cross-pack gossip; a public `IIntelBoard`; a `ZohFramework.Api2`.
- NER's story, loc keys and slot tuning (2611152305-customer-rumours-proposal.md).
- Verbs in Setup / Schedule passes.

---

## Context

### Current State
- `ExpeditionFramework.csproj`: netstandard2.1; references game DLLs via `$(GameManaged)` with `<Private>false</Private>`; `ProjectReference api/ExpeditionFramework.Api.csproj`; `InstallMod` target copies `$(TargetPath);$(OutDir)ExpeditionFramework.Api.dll;manifest.xml` to `$(InstallDir)` (default `..\..\Mods\ExpeditionFramework\`). No conditional reference pattern yet; `docs/examples/Starter/EfStarter.csproj` has a `CheckGame` target (`BeforeTargets="ResolveAssemblyReferences" Condition="!Exists(...)"`) to copy.
- `manifest.xml`: id `expedition_framework`; `<Assemblies>` = `ExpeditionFramework.Api.dll`, `ExpeditionFramework.dll`; `<Prerequisites />`.
- `src/Game/ModEntry.cs`: `public class ModEntry : IMod` — `Init` (log), `OnEnable` (`PackRegistry.Start`, hooks), `OnDisable`. `ModId = "expedition_framework"`.
- Installed ZF (`../../Mods/ZohFramework/`): `ZohFramework.Api.dll` (v1.0.0.0, frozen, `Deterministic`), `ZohFramework.dll`, `manifest.xml` (lists Api first). ZF repo `../ZohFramework/`: `api/StoryApi.cs` — `IStoryExtension.RegisterStory(IStoryRegistrar)`; `IStoryRegistrar { ConsumerId; Variables; AddVerb(qualifiedName, StoryVerb, params StoryPass[]) /* none = Live */; OnArrival; OnPurchase; OnSceneEnd }`; `IStoryCall { ConsumerId; Scene; Pass; Args (IReadOnlyList<object>); Named (IReadOnlyDictionary<string, object>); Variables; Warn(message) }`; values cross as `null | bool | long | double | string | list | map`; verbs may return int/float too. `StoryVerbException` stops the scene.
- ZF today (`src/Core/StoryExtensions.cs:107–128`): `ExtensionSet.Collect(modInstance, consumerId, …)` type-tests the **consumer's** instance and calls `RegisterStory` once; registration throwing is caught and logged (`RegisterStory failed`). ZF registers at its `OnEnable` (`RegisterAll`) and on `OnModAssetsLoaded`. `IsConsumer` still requires the `zoh_framework` prerequisite — the ZF `uses:` plan widens it to `Stories/main.zoh` and adds provider calls per consumer.
- ZF README § "Consumer contract version 1 — C# tier": implement `IStoryExtension`; reference the installed api dll copy-local; ship + list it (else the scanner flags `unlisted-reference` / `payload-file`); never reference `ZohFramework.dll`; never call ZF from `OnEnable`. § "The api never changes": copy, never rebuild. `tests/ScanCheck <mod-folder> <managed-dir>` expects `HARD FLAGS: 0`, `CAPABILITIES: none`. `tests/SampleConsumer/SampleMod.cs` reads named args with `call.Named.TryGetValue("text", out object text)`.
- After 2611152314-told-intel-slots-and-board-plan.md: `IntelBoard.Claim(packId)`, `HasPack(packId)`, `Tell(packId, areaId?, slotId?, show) → ToldCard { Area, Slot, Index, Position, Pick (qualified) }`, `Show(ToldCard) → bool` (false once the day turned). Area ids are `pack:area`, pick ids `pack:id`.
- `tests/Sim/Sim.csproj` compiles `src/Core/**` + `api/*` only: `StoryProvider` (src/Game) is untestable there, so the key template logic goes to Core.

### Key Files

> Quick reference — detailed changes are in Implementation steps below.

| File | Role | Change Summary |
|------|------|----------------|
| `ExpeditionFramework.csproj` | build | `ZohApi` property, `CheckZohApi` target, copy-local reference, install copy |
| `manifest.xml` | mod manifest | `+ <Assembly>ZohFramework.Api.dll</Assembly>` |
| `src/Game/ModEntry.cs` | mod entry | `: IMod, IStoryExtension`; `RegisterStory` → `StoryProvider.Register` |
| `src/Game/StoryProvider.cs` (new) | verbs | `Register`, `Rumour`, `Tell`, per-consumer reservations |
| `src/Core/ExpeditionRun.cs` | `IntelRoller` | `+ CardKey(template, areaId, slotId, pick)` |
| `tests/Sim/Program.cs` | tests | `TestCardKey` |
| `docs/zoh.md` (new), `docs/README.md`, `README.md`, `docs/intel.md`, `docs/contributing.md` | docs | verbs page + links + build note |

### Dependencies
- **Requires:** 2611152314-told-intel-slots-and-board-plan.md (merged; `IntelBoard`). ZohFramework's `uses:` providers — 2611152320-zoh-optional-consumers-and-verb-providers-plan.md (from 2611152305-customer-small-talk-proposal.md § 4), merged and installed. Installed ZF with `uses:` support for the in-game checks (not for the build: the api dll is v1, unchanged).
- **Blocks:** NER's rumours (2611152305-customer-rumours-proposal.md).

### Constraints
- `ZohFramework.Api.dll`: copied from the installed `Mods/ZohFramework/`, never rebuilt, never edited (ZF README). Never reference `ZohFramework.dll`.
- `RegisterStory` must not call into ZF and must not throw (ZF logs and drops the extension set otherwise).
- `IntelBoard` stays internal; `ExpeditionFramework.Api` unchanged.
- Verbs only reach the calling consumer's own pack (`story.ConsumerId`).
- Live pass only (small talk runs Live; showing a card changes the game, so no Setup/Schedule).
- `tools/EfSim` untouched; lint/run baselines identical (SC8).

### Assumptions
- B1 — Implementing an interface from `ZohFramework.Api.dll` and listing that dll passes the game's scanner with no capability, as ZF's SampleConsumer does. **Verify first:** V3 (ScanCheck) right after Step 1–2, before writing the verbs.
- B2 — When ZF is absent, the game loads EF's `ZohFramework.Api.dll` from EF's folder and `ModEntry` resolves (the api dll has no `IMod`, so the loader logs `No IMod implementation found` for it, harmless); when ZF is present, whichever copy loads first serves both (same assembly name/version). Verify: J1 (log line `ZohFramework.Api loaded from …` when ZF present; EF loads without ZF).
- B3 — The ZF `uses:` plan calls `RegisterStory` on the **provider's** mod instance once per consumer, with `ConsumerId` = that consumer's id (proposal § 4). Verify: J2 (ZF log `Story mod <pack>: … C# verbs: ef.rumour, ef.tell`). If ZF instead passes its own id, `HasPack` fails and `/ef.rumour` warns once — fail-safe.
- B4 — Named values arrive as `bool` for `tell: false`, `string` for `key:`/`area:`/`slot:`; `/ef.tell "key"` puts the key in `Args[0]` as `string` (StoryApi comment). Verify: J3.
- B5 — Verbs run on the game's main thread during a dialogue, so `AfterhourEvents.Start` is safe there (ZF's `ps.*` verbs already change game state the same way).
- B6 — Day boundary for reservations: `StoreStation.Instance.dayCounter` (as `IntelCards` uses).

### Impact Analysis
- **Direct:** csproj / manifest (new shipped assembly), `ModEntry` (new interface), `StoryProvider` (new), `IntelRoller.CardKey`.
- **Adjacent:** `IntelBoard` (consumer only); `PackRegistry.Stop` already resets claims; ZF's registrar (foreign code) receives our verbs.
- **Downstream:** packs with `uses: ["expedition_framework"]`; `docs/examples` unaffected (no example story shipped — NER is the first consumer); ZF ScanCheck/LoadOrder suites may add EF as a provider fixture (ZF side).

---

## Implementation

### Overview
Build surface first and prove the scanner is happy (B1) before any verb code; then the entry/provider; then docs. The verb layer is thin: everything about candidates, budgets, counters and showing is `IntelBoard`.

---

### Step 1: csproj — copy-local `ZohFramework.Api` + install copy

**Objective:** SC1.
**Confidence:** High
**Depends on:** None
**Do-Projex:** Encouraged

**Files:**
- `ExpeditionFramework.csproj`

**Changes:**

```xml
<!-- PropertyGroup: after <InstallDir> -->
    <!-- The installed ZoH Framework api, copied as is: every mod shares the first copy the game loads, so it is never rebuilt here. -->
    <ZohApi>..\..\Mods\ZohFramework\ZohFramework.Api.dll</ZohApi>

<!-- new ItemGroup after the game references -->
  <ItemGroup>
    <Reference Include="ZohFramework.Api"><HintPath>$(ZohApi)</HintPath><Private>true</Private></Reference>
  </ItemGroup>

<!-- new target before InstallMod -->
  <Target Name="CheckZohApi" BeforeTargets="ResolveAssemblyReferences" Condition="!Exists('$(ZohApi)')">
    <Error Text="ZohFramework.Api.dll not found at $(ZohApi). Install ZoH Framework into Mods\ZohFramework\ or pass -p:ZohApi=&lt;path to its ZohFramework.Api.dll&gt;." />
  </Target>

<!-- InstallMod Copy -->
<!-- Before: -->
    <Copy SourceFiles="$(TargetPath);$(OutDir)ExpeditionFramework.Api.dll;manifest.xml" DestinationFolder="$(InstallDir)" />
<!-- After: -->
    <Copy SourceFiles="$(TargetPath);$(OutDir)ExpeditionFramework.Api.dll;$(OutDir)ZohFramework.Api.dll;manifest.xml" DestinationFolder="$(InstallDir)" />
```

**Rationale:** The csproj already depends on the installed game (`GameManaged`), so depending on the installed ZF the same way, with a `CheckGame`-style error (pattern from `docs/examples/Starter/EfStarter.csproj`), is the smallest honest build. Rejected: vendoring the dll in the repo (a binary in git, and a second copy to keep equal to the installed one; the README rule says copy the *installed* file); a `ProjectReference` into `../ZohFramework/api` (rebuilds the frozen api — forbidden).

**Verification:** V1 (build to scratch; copy present and identical), V2 (missing-file error message).

**If this fails:** revert the csproj.

---

### Step 2: manifest — list the api dll

**Objective:** SC2.
**Confidence:** High
**Depends on:** Step 1
**Do-Projex:** Encouraged

**Files:**
- `manifest.xml`

**Changes:**

```xml
<!-- Before: -->
  <Assemblies>
    <Assembly>ExpeditionFramework.Api.dll</Assembly>
    <Assembly>ExpeditionFramework.dll</Assembly>
  </Assemblies>
<!-- After: -->
  <Assemblies>
    <Assembly>ZohFramework.Api.dll</Assembly>
    <Assembly>ExpeditionFramework.Api.dll</Assembly>
    <Assembly>ExpeditionFramework.dll</Assembly>
  </Assemblies>
```

**Rationale:** Unlisted shipped dll = scanner `payload-file`; referenced but unlisted = `unlisted-reference` (ZF README). Listed first like ZF's own manifest, before the assembly that references it.

**Verification:** V3 (ScanCheck `HARD FLAGS: 0`, `CAPABILITIES: none`) — run **now** to settle B1 before Step 3; V4 (grep).

**If this fails:** scanner flags → stop and read the flag; do not proceed to verbs.

---

### Step 3: `IntelRoller.CardKey` (Core) + `TestCardKey`

**Objective:** SC6 — the one piece of verb logic that is pure, kept testable.
**Confidence:** High
**Depends on:** None (Core only)
**Do-Projex:** Encouraged

**Files:**
- `src/Core/ExpeditionRun.cs` (`IntelRoller`)
- `tests/Sim/Program.cs`

**Changes:**

```csharp
        /// <summary>
        /// The text key a told card is worded by: <paramref name="template"/> with {area}, {slot} and {pick} filled in, the
        /// area and pick without their pack prefix, as key templates elsewhere name picks (run.$pick).
        /// </summary>
        public static string CardKey(string template, string areaId, string slotId, string pick)
        {
            string Bare(string id) => id == null ? "" : id.Substring(id.IndexOf(':') + 1);
            return (template ?? "").Replace("{area}", Bare(areaId)).Replace("{slot}", slotId ?? "").Replace("{pick}", Bare(pick));
        }
```

`TestCardKey` (call after `TestToldIntel();`): `CardKey("rumour_{pick}", "p:a", "s", "p:fence") == "rumour_fence"`; `CardKey("{area}_{slot}_{pick}", "p:a", "s", "p:fence") == "a_s_fence"`; `CardKey("plain", …) == "plain"`; `CardKey(null, …) == ""`; unqualified ids pass through (`"a"` → `"a"`).

**Rationale:** Same `Substring(IndexOf(':') + 1)` rule as `IntelCards.Render` uses for `pick`. In `IntelRoller` because it belongs to told cards, not to ZoH; `StoryProvider` only calls it.

**Verification:** V5 (`tests/Sim` green), V6 (`TestCardKey` wired).

**If this fails:** revert both hunks.

---

### Step 4: `ModEntry : IStoryExtension` + `StoryProvider`

**Objective:** SC3, SC4, SC5.
**Confidence:** Medium (game-side; compile + in-game)
**Depends on:** Steps 1–3; sibling plan merged
**Verify-Projex:** Encouraged

**Files:**
- `src/Game/ModEntry.cs`
- `src/Game/StoryProvider.cs` (new)

**Changes:**

```csharp
// ModEntry.cs
// Before:
    public class ModEntry : IMod
// After:
using ZohFramework.Api;
...
    // IStoryExtension makes the framework a ZoH verb provider: a story that lists expedition_framework in uses: gets ef.*
    // verbs. ZoH Framework calls RegisterStory itself, once per such story; without ZoH Framework nothing calls it and the
    // api dll shipped here just sits there, so the mod needs no prerequisite on it.
    public class ModEntry : IMod, IStoryExtension
    {
        ...
        public void RegisterStory(IStoryRegistrar story) => StoryProvider.Register(story);
```

```csharp
// StoryProvider.cs (new)
using System.Collections.Generic;
using ExpeditionFramework.Core;
using ZohFramework.Api;

namespace ExpeditionFramework.Game
{
    /// <summary>
    /// The ef.* verbs a ZoH story gets by using this mod. Each verb only reaches the story's own pack: the registrar names the
    /// consumer, and that id is the pack id. A story that uses the framework without being a pack gets nothing and one warning.
    /// </summary>
    internal static class StoryProvider
    {
        private const string DefaultKey = "rumour_{pick}";

        private sealed class Held { public int Day; public readonly Dictionary<string, ToldCard> Cards = new Dictionary<string, ToldCard>(); }

        private static readonly HashSet<string> warned = new HashSet<string>();
        private static readonly Dictionary<string, Held> held = new Dictionary<string, Held>();

        public static void Register(IStoryRegistrar story)
        {
            string consumer = story.ConsumerId;
            // Claimed whether or not the id is a loaded pack: packs load after every OnEnable, so at this point the registry may
            // not know them yet; a claim nobody reads costs nothing.
            IntelBoard.Claim(consumer);
            story.AddVerb("ef.rumour", call => Rumour(consumer, call));
            story.AddVerb("ef.tell", call => Tell(consumer, call));
        }

        // /ef.rumour key: "…" area: "…" slot: "…" tell: false;  → key | null
        private static object Rumour(string consumer, IStoryCall call)
        {
            if (!IntelBoard.HasPack(consumer))
            {
                if (warned.Add(consumer)) call.Warn("ef.rumour: " + consumer + " is not an expedition pack, so there is no intel to tell");
                return null;
            }
            string template = Named(call, "key") as string ?? DefaultKey;
            bool tell = !(Named(call, "tell") is bool b) || b;
            ToldCard card = IntelBoard.Tell(consumer, Named(call, "area") as string, Named(call, "slot") as string, show: tell);
            if (card == null) return null;
            string key = IntelRoller.CardKey(template, card.Area.Id, card.Slot.Id, card.Pick);
            if (!tell) Reserve(consumer, key, card);
            return key;
        }

        // /ef.tell "key";  → bool
        private static object Tell(string consumer, IStoryCall call)
        {
            string key = call.Args.Count > 0 ? call.Args[0] as string : null;
            if (key == null || !held.TryGetValue(consumer, out Held h) || h.Day != Today() || !h.Cards.TryGetValue(key, out ToldCard card)) return false;
            h.Cards.Remove(key);
            return IntelBoard.Show(card);
        }

        private static void Reserve(string consumer, string key, ToldCard card)
        {
            if (!held.TryGetValue(consumer, out Held h) || h.Day != Today()) held[consumer] = h = new Held { Day = Today() };
            h.Cards[key] = card;   // a second reservation under the same key replaces the first; the first stays held on its counters until the day turns
        }

        private static int Today() => StoreStation.Instance != null ? StoreStation.Instance.dayCounter : -1;

        private static object Named(IStoryCall call, string name) => call.Named != null && call.Named.TryGetValue(name, out object v) ? v : null;
    }
}
```

**Rationale:** Reservations are keyed by the filled key because that is what the story holds between `/ef.rumour tell: false` and `/ef.tell` (proposal § 2); per consumer so two packs' keys can't collide. Day-stamped so a stale key is `false` (the counters' `_held` mark also lapses at the next roll — two guards, one truth: the counters). `null` return = ZoH `nothing` (StoryApi: `nothing <-> null`). Rejected: returning a map (stories pass loc keys to `/converse`); throwing `StoryVerbException` on a non-pack consumer (would kill the customer's line for a data mistake; a warning plus `nothing` lets the story decline).

**Verification:** V1, V3 (rebuild + rescan with the new type), V7 (Live only; no `ZohFramework.dll`), judgment J1–J4.

**If this fails:** `git checkout -- src/Game/ModEntry.cs`; delete `StoryProvider.cs`; Steps 1–3 stand (a shipped api dll with no implementer is harmless).

---

### Step 5: Docs

**Objective:** SC7.
**Confidence:** High
**Depends on:** Step 4
**Do-Projex:** Encouraged

**Files:**
- `docs/zoh.md` (new) — "Telling intel through ZoH Framework": what to ship (`Stories/main.zoh` with `uses: ["expedition_framework"]`; no prerequisite on either framework needed for ZF to find it — per ZF's `uses:` rules); the verbs table (as the proposal's, with `nothing` / `false` cases); the loc keys the pack must provide for every pick (`rumour_{pick}` default; lint can't check keys built from templates — same caveat as intel.md "The picked id in text"); `required_verbs`: list `ef.rumour` (and `ef.tell`) explicitly, never `"*"`; what happens without ZF (told slots roll with the day); one short example story (`smalltalk_arrive` → `/ef.rumour; -> *k;` → `/converse *k;`, with the ZoH syntax as ZF's README writes it — copy from `../ZohFramework/README.md` § small talk once the ZF plan lands; mark the example "syntax per ZoH Framework README" so a drift is a doc fix, not an EF bug).
- `docs/README.md` — Reference table row: `[Telling intel through ZoH](zoh.md) | uses:, /ef.rumour, /ef.tell`.
- `README.md` :84 — add `[Telling intel through ZoH](docs/zoh.md)` to the reference list.
- `docs/intel.md` § Told slots — replace the plain sentence from the sibling plan with the link: "The ZoH verbs that tell are in [Telling intel through ZoH](zoh.md)."
- `docs/contributing.md` after the build command (:8): "The game build also needs the installed ZoH Framework's `ZohFramework.Api.dll` (`Mods/ZohFramework/`), copied as is; elsewhere pass `-p:ZohApi=<path>`. `tests/Sim` and `ef-sim` don't need it." Repo-layout table: nothing new (no new folder).

**Verification:** V4 (greps), judgment J5.

**If this fails:** docs are independent; revert.

---

## Verification Plan

> Per-step verification (above) confirms each change in isolation. This section confirms the changes work together end-to-end. Shell: Git Bash from `<repo-root>`; `Q` = scratch dir outside the repo; `ZF=../ZohFramework`; `MANAGED="../../Probably Stolen_Data/Managed"`; `SIM="dotnet run -c Release --no-build --project tools/EfSim --"`. Baselines (`$Q/before/*`) as in the sibling plan's V0, taken before Step 1.

### Deterministic Suite

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V1 | SC1 | `ls -l "../../Mods/ExpeditionFramework" > $Q/live-before.txt`; `dotnet build -c Release ExpeditionFramework.csproj -p:InstallDir=$Q/game-install/`; `ls -l "../../Mods/ExpeditionFramework" > $Q/live-after.txt`; `cmp $Q/live-before.txt $Q/live-after.txt`; `cmp $Q/game-install/ZohFramework.Api.dll "../../Mods/ZohFramework/ZohFramework.Api.dll"` | build exit 0, `0 Error(s)`; both cmp exit 0; `$Q/game-install/` holds `ExpeditionFramework.dll`, `ExpeditionFramework.Api.dll`, `ZohFramework.Api.dll`, `manifest.xml` | Build break; live install touched; api dll missing or not byte-identical |
| V2 | SC1 | `dotnet build -c Release ExpeditionFramework.csproj -p:InstallDir=$Q/none/ -p:ZohApi=$Q/missing.dll 2>&1 \| grep -c 'ZohFramework.Api.dll not found'` | `1`, build exit ≠ 0 | Silent build without the api |
| V3 | SC2, B1 | `dotnet run -c Release --project $ZF/tests/ScanCheck -- $Q/game-install "$MANAGED" \| grep -E '^(HARD FLAGS\|CAPABILITIES):'` (after Step 2 and again after Step 4) | `HARD FLAGS: 0` and `CAPABILITIES: none` | Scanner refuses the mod (unlisted reference, payload, capability) |
| V4 | SC2, SC7 | `grep -c '<Assembly>ZohFramework.Api.dll</Assembly>' manifest.xml`; `grep -c '<Prerequisites />' manifest.xml`; `test -f docs/zoh.md`; `grep -c 'zoh.md' README.md docs/README.md docs/intel.md`; `grep -c 'ZohApi' docs/contributing.md` | `1`; `1`; exit 0; each ≥ `1`; ≥ `1` | Manifest / docs missing |
| V5 | SC6, SC8 | `dotnet run -c Release --project tests/Sim \| tail -n 1`; rebuild EfSim; the three `lint --strict` + `run` into `$Q/after/`; `cmp` each against `$Q/before/` (×6) | `All checks passed`; cmp exit 0 ×6 | Core regression; sim drift |
| V6 | SC6 | `grep -c 'TestCardKey' tests/Sim/Program.cs`; `grep -c 'CardKey' src/Game/StoryProvider.cs` | ≥ `2`; ≥ `1` | Test not wired; verb not using it |
| V7 | SC3, constraints | `grep -c 'ZohFramework.dll' ExpeditionFramework.csproj manifest.xml src/Game/*.cs`; `grep -c -E 'StoryPass\.(Setup\|Schedule)' src/Game/StoryProvider.cs`; `grep -c 'IStoryExtension' src/Game/ModEntry.cs`; `git diff --quiet -- api tools/EfSim; echo $?` | `0` each file; `0`; ≥ `1`; `0` | Forbidden reference; wrong pass; API/sim touched |
| V8 | all | `git status --porcelain` | only the planned files + `.projex/` docs | Stray output |

### Judgment Checks

| Check | Covers | Why not deterministic |
|-------|--------|-----------------------|
| J1 — Game with ZF **absent**: EF loads (Mods menu, log `Expedition packs: …`), the api dll logs its `No IMod implementation found` line only; a pack with `told: true` shows told cards at the day's roll as before. Game with ZF present: ZF's log `ZohFramework.Api loaded from …` names one path; EF loads | B2, SC3 | Needs the game |
| J2 — ZF present with its `uses:` plan, plus a consumer pack (NER's rumours pack, or a local throwaway pack: `told: true` slot, `Stories/main.zoh` with `uses: ["expedition_framework"]`, `required_verbs` listing `ef.rumour`, a `smalltalk_arrive` entry that calls `/ef.rumour` then `/converse`): ZF log `Story mod <pack>: … C# verbs: ef.rumour, ef.tell`; new day: told card absent from the map; a customer makes small talk → the card is on the area's map card right then; the line uses the pick's key | SC3, SC4, B3, B5 | Needs the game |
| J3 — Story variant with `tell: false` then `/ef.tell *k;` on a choice: card appears only after the tell; declining leaves it absent; next day the key returns `false` and the card is gone. `area:` / `slot:` filters narrow the pick; a non-pack consumer `uses` EF → one warning in ZF's log, line declines | SC4, SC5, B4 | Needs the game |
| J4 — After budget / `intel_max` are spent, further small talk returns `nothing` and the story declines (no empty line) | SC4 | Needs the game |
| J5 — `docs/zoh.md` lets a pack author (NER) add rumours with data only, and its ZoH snippet matches ZF's README syntax | SC7 | Clarity; foreign syntax |
| J6 — New comments: present tense; non-obvious choices name the rejected alternative (claim-before-load; key-as-handle; warn-not-throw; installed dll vs vendored) | Source Hygiene | Prose judgment |

### Acceptance Criteria Validation
| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| SC1 | V1, V2 | builds, copies the identical dll, fails loudly without it |
| SC2 | V3, V4 | listed; scanner clean |
| SC3 | V7 + J1, J2 | extension wired; Live only; no ZF call |
| SC4 | J2, J3, J4 | rumour verb as specified |
| SC5 | J3 | reservations show / lapse |
| SC6 | V5, V6 | `CardKey` tested |
| SC7 | V4 + J5 | docs present and usable |
| SC8 | V5 | sim green, baselines identical |

---

## Rollback Plan

1. Before any commit: `git checkout -- ExpeditionFramework.csproj manifest.xml src/Core/ExpeditionRun.cs src/Game/ModEntry.cs tests/Sim/Program.cs docs README.md`; delete `src/Game/StoryProvider.cs`, `docs/zoh.md`.
2. After step commits: `git revert` newest first. Reverting Steps 1–2 alone also removes the shipped api dll from the next install (the live `Mods/ExpeditionFramework/` keeps its copy until the user reinstalls — harmless, unlisted files are only a scanner concern when the manifest lists them).
3. Nothing is deployed by the plan: builds go to a scratch `InstallDir`.

---

## Notes

### Risks
- ZF's `uses:` plan ships a registrar shape other than assumed (B3): verbs still register (the api is frozen); only `ConsumerId` semantics matter, and a mismatch degrades to "warn once, nothing told".
- EF's api copy older/newer than the installed ZF's: v1 is frozen and `Deterministic`; V1's `cmp` proves identity at build time; the first-loaded copy wins at runtime either way.
- A consumer's story loads before packs exist (J2 timing): claims are stored regardless; verbs read the board at call time.
- `required_verbs: "*"` in a consumer story makes `ef.*` a load error when EF is missing — documented in zoh.md as the pack's concern (ZF rule).
- Split verdict: **Split required — cross-repo seam (ZF `uses:` upstream, this downstream).** This plan is the ZF-dependent half; 5 steps, well under the size heuristic — no further split.

### Open Questions
- [ ] None.
