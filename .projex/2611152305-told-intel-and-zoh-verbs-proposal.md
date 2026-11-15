# Told Intel and ZoH Verbs

> **Status:** Complete (Accepted)
> **Author:** Claude Opus 5.5 (Agent)
> **Parent:** 2611151847-customer-small-talk-intel-proposal.md
> **Related Projex:** 2611152314-told-intel-slots-and-board-plan.md (plan: § 1 told slots + `IntelBoard`, no ZF dependency; closed — 2611152314-told-intel-slots-and-board-walkthrough.md) | 2611152314-zoh-verb-provider-plan.md (plan: § 2 provider verbs; Requires ZF's `uses:` plan) | 2611152305-customer-small-talk-proposal.md (ZohFramework: small talk, `uses:` providers) | 2611152305-customer-rumours-proposal.md (NER: first told pack) | 2611142108-sim-tally-and-intel-cap-plan.md (closed: `intel_max`, `IntelRoller.Roll`)

Accepted by the user 2026-11-15 ("split and plan them"). Split from the parent (in ZohFramework's `.projex/closed/`), which also covered ZohFramework and NER and keeps the game-probe evidence.

---

## Summary

Intel is EF's. EF gains **told** intel slots: in a pack that some ZoH story claims, a told slot doesn't roll at the start of the day; each card is rolled **at the moment a customer tells it**, and shows on the map right then. EF also becomes an optional ZoH **verb provider**: a story that writes `uses: ["expedition_framework"]` gets `/ef.rumour`. So an EF pack adds customer-told intel with data only: `told: true` slots + a ZoH story + loc keys. Without ZohFramework (ZF), told slots roll and show at the day's roll as today.

**Design goal (user):** a future pack like NER does areas + intel with EF alone, and adds small-talk intel through ZF as easily as NER, with no C# of its own.

**Told on demand (user, 2026-11-15):** an earlier draft rolled told cards in the morning and hid them until told. The user rejected the premise ("why is this assuming the day's cards exist before talk?"): with ZF installed, telling is the main way intel reaches the map, so there are no hidden cards, nothing untold to expire or carry over.

---

## Problem Statement

### Current State
- `IntelCards` (internal) rolls once a day, the first time the game asks for an area's cards (`IntelRoller.Roll` → `ef_intel_{slot}_{i}` counters on `AfterhourLocationState`), and shows every pick at once through `AfterhourEvents.Start`.
- `IntelRoller.Roll(area, roll, scope)`: per slot, checks `when` once per rolled day, draws `HowMany` (from `chance` or `counts`) distinct pool positions; then `intel_max` trims in slot order.
- Packs: found by file (`PackRegistry.IsPack`: enabled + `main.json`). Pack id = mod `Manifest.ID`.
- EF knows nothing of ZF.

### Need
- Intel delivered by customers, not appearing on its own.
- Every EF pack gets it without C#.

---

## Proposed Change

### 1. Told slots

- **Slot field `told`** (bool, default `false`), validated by `Schema` / `PackLoader`.
- **Unclaimed pack** (no ZF, or no story uses EF for this pack): told slots roll at the day's roll exactly as today. A pack never loses intel for lack of a teller.
- **Claimed pack:** told slots are skipped by the day's roll (no picks). Each tell rolls one card:
  1. **Candidates:** the pack's told slots (optionally filtered by area / slot) in areas the player has discovered (`ExpeditionLocation.IsDiscovered()`, added 2026-11-15 after NER's plan found the gap), whose `when` holds now, whose area is under its `intel_max` (counting cards already shown today, told or not), whose pool has a position not yet shown today, and whose **daily budget** isn't used up.
  2. **Daily budget:** the slot's own dice (`chance` / `counts` → `HowMany`), rolled lazily the first time the slot is a candidate that day. So a told slot hands out at most as many cards per day as it would have shown at the roll, and the pack's balance doesn't change; only the delivery does.
  3. **Pick:** a candidate slot at random, then a random unshown position of its pool.
  4. **Show:** stored in the same `ef_intel_{slot}_{i}` counters as rolled picks (so a reload keeps it) and started through `AfterhourEvents.Start` for today.
- **`when` is checked at tell time,** not once at the roll. A told card appearing mid-day is the point, so the roller's "no mid-day appear/vanish" rationale doesn't apply to told slots; already-shown cards never vanish.
- Runs see shown cards only, as today (`IntelCards.Today()` reads active cards).
- **Internal board** (EF.dll, not `ExpeditionFramework.Api.dll`): `Claim(packId)`, `Tell(packId, area?, slot?) → card | none`. Exposing it publicly waits for a C# teller (content-first; avoids the older-API-copy risk).
- **ef-sim:** told slots simulate as rolled (every card told). Balance rows are unchanged, so no new sim key.

### 2. EF as optional ZoH verb provider

- EF's `ModEntry` also implements `ZohFramework.Api.IStoryExtension`. EF ships `ZohFramework.Api.dll` copied from the installed `Mods/ZohFramework/` (never rebuilt; ZF README rule) and lists it in `<Assemblies>`. `<Prerequisites>` unchanged.
- **Without ZF:** the api DLL loads (harmless `No IMod implementation found` log line), nothing calls `RegisterStory`, nothing is claimed; EF behaves exactly as today.
- **With ZF:** for each consumer whose story `uses` EF, ZF calls EF's `RegisterStory` (once per consumer):
  1. If `ConsumerId` is an EF pack id, EF claims that pack.
  2. EF registers its verbs for that consumer:

| Verb | Params | Result |
|---|---|---|
| `/ef.rumour;` | named: `key:` template (default `"rumour_{pick}"`; placeholders `{area}` `{slot}` `{pick}`, pack prefix stripped); `area:` / `slot:` filters; `tell:` (bool, default `true`) | Rolls one told card of **this consumer's pack** (section 1), shows it unless `tell: false`, returns the filled loc key; `nothing` when no candidate |
| `/ef.tell "key";` | a key that `/ef.rumour tell: false` returned today | Shows that card; bool |

- `/ef.rumour` returns a loc key (not a map) because stories pass loc keys to `/converse`.
- `tell: false` reserves the card (counts toward budget and `intel_max`) so a story can branch before telling; an unreserved reservation lapses at day end.
- Scope: verbs only touch the using consumer's own pack. A consumer that `uses` EF but isn't an EF pack → `/ef.rumour` warns once and returns `nothing`. Cross-pack gossip: no (out of scope).
- ZF keeps `required_verbs: "*"` strict, so a pack story that calls `ef.*` lists its required verbs explicitly (pack-side concern; EF docs say so).

---

## Approach Options
- **A (chosen): EF is the provider.** One rumour implementation shared by every pack; packs stay data-only.
- **B: provider in a third mod:** needs a public `IIntelBoard`, a third mod to install. Rejected for now.
- **C: per-pack C# bridge:** fails the design goal. Rejected.
- **D: EF injects talk itself:** EF grows store-dialogue code. Fallback only.
- **Morning roll + hidden until told** (earlier draft): rejected by the user, see Summary.

---

## Impact Analysis

### Affected Areas
- `src/Core/Model.cs` (`IntelSlot.Told`), `Schema.cs`, `PackLoader.cs`
- `src/Core/ExpeditionRun.cs` `IntelRoller`: skip told slots when claimed; single-card tell roll (budget, `when`, `intel_max`, unshown positions)
- `src/Game/IntelCards.cs`: claim state, tell, show mid-day, persistence through existing counters
- new `src/Game/StoryProvider.cs`: `IStoryExtension`, `ef.*` verbs
- `ModEntry`, csproj, manifest: `ZohFramework.Api.dll` copy-local + `<Assemblies>`
- `tools/EfSim`: told slots simulate as rolled
- docs: `intel.md` (told), new `zoh.md` (verbs)
- ScanCheck: EF gains a listed assembly

### Dependencies
- Section 1 (told + board) needs nothing from ZF and can land first.
- Section 2 (provider) **requires** ZF's `uses:` (2611152305-customer-small-talk-proposal.md).
- **Blocks:** NER's rumours.

### Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Day rolled before the claim (map asked before ZF registers consumers) → told slots roll that day as unclaimed | Low | Low | ZF registers on `OnModAssetsLoaded`, before any day; at worst one day plays as today |
| Too few talks → fewer cards than today | Med | Med | pack tunes its small-talk `chance`; budget never exceeds today's |
| EF's `ZohFramework.Api.dll` copy older than the installed ZF's | Low | Low | api v1 frozen; copy, never rebuild |
| Claimed told slots skipping dice shift other slots' rolls vs an unclaimed day | Certain | Low | accept: seeds aren't fixed per day; document |

### Breaking Changes
None. `told` defaults to `false`; EF's public API unchanged; packs without told slots behave as today.

---

## Open Questions
None blocking. Cross-pack gossip and a public C# board are out of scope.

---

## Next Steps
Plans in `ExpeditionFramework/.projex/`: told slots + board first (independent), provider verbs after ZF's `uses:` plan.
