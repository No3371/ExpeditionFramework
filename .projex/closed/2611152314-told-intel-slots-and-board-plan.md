# Told Intel Slots + Internal Board (`told: true`, `IntelBoard`)

> **Status:** Complete
> **Author:** Claude Fable 5.1 (plan step)
> **Parent:** 2611152305-told-intel-and-zoh-verbs-proposal.md
> **Source:** 2611152305-told-intel-and-zoh-verbs-proposal.md (Complete (Accepted)) § 1 Told slots
> **Related Projex:** 2611152314-zoh-verb-provider-plan.md (sibling; Requires this) | 2611152305-customer-small-talk-proposal.md (ZohFramework; the eventual claimant) | 2611142108-sim-tally-and-intel-cap-plan.md (closed; `intel_max`, `IntelRoller.Roll`, proof conventions reused) | 2611152305-customer-rumours-proposal.md (NER; first told pack)
> **Completed:** 2026-11-16
> **Walkthrough:** 2611152314-told-intel-slots-and-board-walkthrough.md
> **Log:** 2611152314-told-intel-slots-and-board-log.md
> **Worktree:** No — repo clean but for the untracked proposal (`main` @ `492d5cc`), no `projex/*` branches, ~10 files. The csproj's relative `GameManaged` path and the lint baselines below are relative to the main checkout; a worktree would break them. Override if `ef-sim.exe` is locked by another session (see 2611150108-melee-weapon-facts-plan.md Worktree note).

---

## Summary

Intel slots gain `told: true`. Nothing changes until some mod **claims** the pack through the new internal `IntelBoard` (`Claim(packId)`): then the pack's told slots skip the day's roll and each card is rolled **at the moment of telling** (`IntelBoard.Tell(packId, area?, slot?, show)`), shown on the map right then, kept through the same `ef_intel_{slot}_{i}` counters as rolled picks. Unclaimed packs (no claimant, or no ZohFramework) behave exactly as today. No ZohFramework dependency: the only claimant for now is a debug-panel entry; the ZoH verbs come with 2611152314-zoh-verb-provider-plan.md.

**Determinism:** `IntelRoller.Roll(area, roll, scope)` keeps its signature and, for packs with no told slots and for unclaimed packs, its exact dice and picks (existing `tests/Sim` and `ef-sim` baselines unchanged). ef-sim rolls told slots as today (every card told) — no EfSim code change.

**Scope:** EF only — `src/Core` (Model, Schema, PackLoader, ExpeditionRun's `IntelRoller`), `src/Game` (IntelCards, ExpeditionLocation, new IntelBoard, PackRegistry, DevTools), `tests/Sim`, `docs/` (intel, testing, csharp-api). No `api/` change; no csproj/manifest change.
**Estimated Changes:** 4 Core files (~90 lines), 4 Game files (~150 lines, 1 new), 1 test file (~90 lines), 3 docs (~45 lines).

---

## Objective

### Problem / Gap / Need
`IntelCards.Rolled` rolls every slot once a day; a card can only appear at the day's roll. The proposal needs intel that reaches the map when a customer tells it: a per-tell roll with the slot's own budget, `when` at tell time, `intel_max` counting what already shows, and no hidden cards. EF must hold that state itself (claims, per-day budgets, told picks) so a later verb provider only calls `Claim` / `Tell`.

### Success Criteria
- [ ] SC1 — `told` is a boolean slot field: `told: true` loads cleanly; `told: "yes"` is a problem at `…intel[n].told` (`must be true or false`); default `false`.
- [ ] SC2 — `IntelRoller.Roll(area, roll, scope)` (3 args) is byte-for-byte the same behaviour: `tests/Sim` `TestMapMemory` / `TestIntelMax` pass unchanged; `ef-sim lint --strict` and `run` output for Starter, Showcase, SampleArea identical to before.
- [ ] SC3 — `Roll(area, roll, scope, toldOnDemand: true)`: a told slot stays a key with no picks and draws no dice; other slots issue the same dice requests as with `toldOnDemand: false`. With `false`, a told slot rolls exactly like an untold one.
- [ ] SC4 — `IntelRoller.TellCandidates` / `Tell` (Core, game-free): candidates = told slots whose `when` holds now, whose pool has an unshown position, whose area is under `intel_max` counting all shown positions, whose lazily rolled budget (`HowMany`) is > 0; `Tell` returns a distinct unshown position, decrements the budget; the budget is rolled once per day per slot (one `roll(100)` on the first candidacy, none after). Covered by `TestToldIntel` in `tests/Sim`.
- [ ] SC5 — In game: `IntelBoard.Claim(pack)` before the day's roll makes that pack's told slots skip the roll; `IntelBoard.Tell(pack, area?, slot?, show: true)` never picks an area the player hasn't discovered (`ExpeditionLocation.IsDiscovered()` false: locked by `unlocked_by` or hidden by `hidden_when`), and stores the pick in `ef_intel_{slot}_{i}` and starts its afterhour event mid-day; `show: false` stores the pick as held (counts toward budget and `intel_max`, no card shown) until `IntelBoard.Show(card)` or the next day's roll; `Today()` sees shown cards only. Unclaimed packs: unchanged.
- [ ] SC6 — Debug panel (Modding): for each pack with a told slot, "Expeditions: claim intel (pack)" and "Expeditions: tell intel (pack)" entries; tell logs the card or `nothing to tell`.
- [ ] SC7 — Docs: `docs/intel.md` § Told slots; `docs/testing.md` `intel` row notes told slots roll as told; `docs/csharp-api.md` names the 4-arg `Roll` and `TellCandidates` / `Tell`.
- [ ] SC8 — Game assembly compiles (`dotnet build -c Release ExpeditionFramework.csproj` into a scratch `InstallDir`); nothing deployed into the live `Mods/` folder; `api/` untouched.

### Out of Scope
- ZoH verbs, `ZohFramework.Api.dll`, `IStoryExtension` (2611152314-zoh-verb-provider-plan.md).
- A public C# board in `ExpeditionFramework.Api` (proposal: waits for a C# teller).
- Cross-pack telling; told cards expiring or carrying over; new ef-sim keys or rows.
- Changing which slots NER or any pack marks `told` (pack-side).

---

## Context

### Current State
- `src/Core/Model.cs:280` `IntelSlot { Id, Pool, Chance=100, Counts, Title, Description, Effect, Tone, GoodPicks, When; MaxPicks => Counts != null ? Counts.Length - 1 : 1 }`. `AreaDef.Intel` (list), `AreaDef.IntelMax` (`Value`, null = no limit), `AreaDef.Pools`.
- `src/Core/Schema.cs:62` `Schema.Intel` shape: `id pool chance counts title description effect tone good when`. `CheckShape` (PackLoader:812) warns unknown keys, reports `Schema.Mismatch` as a problem (`Field.Flag` → `must be true or false`).
- `src/Core/PackLoader.cs:1164` `CompileIntel`: `Int(n,"chance",100)`, `Bool(n, key, default)` helper exists (used at :924, :1139, :1196).
- `src/Core/ExpeditionRun.cs:1255` `IntelRoller.Roll(area, roll, scope)`: every slot gets a key; gated (`When` false) or poolless slot `continue`s **before any dice**; else `HowMany(slot, roll)` (one `roll(100)`) then `roll(open.Count)` per pick; then `Limit` by `intel_max` in slot order. `HowMany` is private.
- `src/Game/IntelCards.cs` (per area, built by `ExpeditionLocation` ctor :60): registers one afterhour event per slot index (`CardId = locationId + "_intel_" + slot.Id + "_" + i`, `dailyChancePercent` 101, `canStart: () => Rolled() && Pick(slot, index) != null`). `Rolled()` rolls once per day (`ef_intel_day` counter), writes `ef_intel_{slot}_{i}` = position+1 (0 = none), sets `toneOverride` for `good` picks. `EnsureToday()` (from `ExpeditionLocation.GetTitle` when discovered) starts/ends cards for the install/unlock day. `Today()` = active cards' picks → `ExpeditionRun` on `Arrive`. Roll RNG: `RNG.GetRandomInt(1, sides)`.
- `src/Game/PackRegistry.cs`: `Start/Stop/Register/LoadAll`; `Stop` removes afterhour events/locations per pack and `DevTools.RemoveAll()`. `LoadAll` runs on the first `OnModAssetsLoaded` call, i.e. **after** every mod's `OnEnable` — a claimant may call EF before packs exist.
- `src/Game/DevTools.cs`: `Add(content, pack)` adds Modding-category entries per pack (`ModHelper.AddDebugFunction(ModEntry.ModId, ModHelper.DEBUG_CATEGORY_MODDING, label, action)`).
- `tools/EfSim/PackRun.cs:305` `RollIntel` calls the 3-arg `Roll` ("every rolled pick counts here"). `tests/Sim/Sim.csproj` and `EfSim.csproj` compile `src/Core/**` + `api/*` only — `src/Game` is never in tests, so every testable rule must live in Core.
- `tests/Sim/Program.cs`: `TestMapMemory` (:690–721, dice-log pattern `100,1`), `TestIntelMax` (:725–799), helpers `Check`, `ProblemsIn`, `WarningsIn`; test list at :26–53.

### Key Files

> Quick reference — detailed changes are in Implementation steps below.

| File | Role | Change Summary |
|------|------|----------------|
| `src/Game/ExpeditionLocation.cs` | builds each area's `IntelCards` | ctor passes `IsDiscovered` into `IntelCards` |
| `src/Core/Model.cs` | `IntelSlot` | `+ public bool Told` |
| `src/Core/Schema.cs` | slot shape | `+ ("told", Field.Flag)` |
| `src/Core/PackLoader.cs` | `CompileIntel` | `Told = Bool(n, "told", false)` |
| `src/Core/ExpeditionRun.cs` | `IntelRoller` | 4-arg `Roll` overload; `ToldDay`; `TellCandidates`; `Tell` |
| `src/Game/IntelBoard.cs` (new) | claims + pack → cards registry + cross-area tell | new |
| `src/Game/IntelCards.cs` | per-area day state | claimed roll; told state counters; `TellCandidates` / `Tell` / `Show`; held cards |
| `src/Game/PackRegistry.cs` | lifecycle | `Stop` → `IntelBoard.Reset()` |
| `src/Game/DevTools.cs` | debug panel | claim / tell entries per pack with told slots |
| `tests/Sim/Program.cs` | tests | `TestToldIntel` |
| `docs/intel.md`, `docs/testing.md`, `docs/csharp-api.md` | docs | told slots; sim note; API note |

### Dependencies
- **Requires:** nothing (ZohFramework not involved).
- **Blocks:** 2611152314-zoh-verb-provider-plan.md (needs `IntelBoard.Claim` / `Tell` / `Show` / `HasPack`); NER's rumours (2611152305-customer-rumours-proposal.md).

### Constraints
- 3-arg `IntelRoller.Roll` signature and dice order unchanged (SC2); `tools/EfSim` not edited.
- No ZohFramework types, no csproj / manifest change, no `api/` change.
- Told state persists through `AfterhourLocationState` counters only (reload-safe like today's picks); claims are per session (re-made by the claimant on every load).
- Source Hygiene (SKILL.md): comments name the rejected alternative; no projex refs; present tense.
- Game-facing code (`src/Game`) is checked by compile + in-game judgment only; every rule that can be tested lives in `IntelRoller`.

### Assumptions
- A1 — The game evaluates an afterhour event's `canStart` in its day-start roll (that is why `dailyChancePercent` is 101 and `EnsureToday` exists for install day), and `AfterhourEvents.Start(id)` shows a card mid-day. Evidence: `EnsureToday` already starts cards mid-session on install/unlock day. **Verify first** (Step 7 debug entry: tell → card appears on the map card without a day change). If the game also re-runs `canStart` mid-day (e.g. on load), held cards are protected by the `_held` counter check in `canStart` (Step 5), so only the "shows right then" half needs the game.
- A2 — `AfterhourLocationState.GetCounter/SetCounter` take any string key and int ≥ 0 and are saved with the game (today's `ef_intel_*` counters rely on this). Verify: existing behaviour; the new keys follow the same prefix.
- A3 — A claim arrives before the day's first roll (claimant registers at mod load). Accepted risk per proposal: a claim after `Rolled()` ran today applies from the next day (told slots already rolled today stay shown; `Tell` still works for budget left).
- A4 — `RNG.GetRandomInt(1, sides)` is inclusive (existing `Rolled` and `ExpeditionLocation.Arrive` use it as the roller's `1..sides`).
- A5 — `ModHelper.AddDebugFunction` entries may be added from `DevTools.Add` for packs (existing pattern) and are removed by `RemoveDebugFunctions(ModEntry.ModId)`.

### Impact Analysis
- **Direct:** `IntelRoller` (new overload + two methods), `IntelCards` (told path), new `IntelBoard`, `DevTools`, `PackRegistry.Stop`.
- **Adjacent:** `ExpeditionLocation` (unchanged; its `IntelCards` registers itself with the board); `ExpeditionRun` (`Today()` contract unchanged); lint (`told` becomes a known key — `told` on a slot was an "unknown key" warning before).
- **Downstream:** 2611152314-zoh-verb-provider-plan.md consumes `IntelBoard`; NER adds `told: true` to its slots; ef-sim users: none (told rolls as before).

---

## Implementation

### Overview
Core first (schema + roller, fully tested), then the game board on top, then a debug claimant so the claimed path can be played before ZohFramework exists, then docs. The roller gains **no new state**: `ToldDay` (budgets + shown positions for one area, one day) is passed in by the caller — `IntelCards` builds it from counters and writes rolled budgets back, `tests/Sim` builds it by hand.

Told-state counters on `AfterhourLocationState` (same prefix family as `ef_intel_{slot}_{i}`):

| Counter | Meaning |
|---|---|
| `ef_intel_{slot}_{i}` | position+1 of the pick at card index `i` (0 = none) — existing; told picks use it too |
| `ef_intel_{slot}_{i}_held` | 1 while the pick at `i` is reserved but not shown (`show: false`); 0 otherwise |
| `ef_intel_{slot}_budget` | told slots: cards still to hand out today + 1; 0 = budget not rolled yet |

All three are reset by `Rolled()` each day, so a held card lapses at the next roll without extra code.

---

### Step 1: `told` slot field (Model, Schema, PackLoader)

**Objective:** A slot can declare `told: true`; wrong type is a problem; default false.
**Confidence:** High
**Depends on:** None
**Do-Projex:** Encouraged

**Files:**
- `src/Core/Model.cs`
- `src/Core/Schema.cs`
- `src/Core/PackLoader.cs`

**Changes:**

```csharp
// Model.cs, IntelSlot — after `public Condition When;`
// Before:
        public Condition When;

        public int MaxPicks => ...
// After:
        public Condition When;
        /// <summary>
        /// Whether a customer tells this slot's cards one at a time (IntelRoller.Tell) when something has claimed the pack. While
        /// nothing has, the slot rolls with the day like any other, so a pack never loses intel for lack of a teller.
        /// </summary>
        public bool Told;

        public int MaxPicks => ...
```

```csharp
// Schema.cs — Intel shape
// Before:
        public static readonly Shape Intel = new Shape(("id", Field.Text), ("pool", Field.Text), ("chance", Field.Number), ("counts", Field.Numbers),
            ("title", Field.Any), ("description", Field.Any), ("effect", Field.Any), ("tone", Field.IntelTone), ("good", Field.Texts), ("when", Field.Any));
// After:
        public static readonly Shape Intel = new Shape(("id", Field.Text), ("pool", Field.Text), ("chance", Field.Number), ("counts", Field.Numbers),
            ("title", Field.Any), ("description", Field.Any), ("effect", Field.Any), ("tone", Field.IntelTone), ("good", Field.Texts), ("when", Field.Any),
            ("told", Field.Flag));
```

```csharp
// PackLoader.cs, CompileIntel — object initializer
// Before:
                Tone = Str(n, "tone", "mixed"),
            };
// After:
                Tone = Str(n, "tone", "mixed"),
                Told = Bool(n, "told", false),
            };
```

**Rationale:** `Field.Flag` + `Bool` is the existing pattern (`interruptible`, `unique`, `separator`); `CheckShape` already turns a wrong type into the `must be true or false` problem, so no new validation code.

**Verification:** V1 (`TestToldIntel` SC1 checks), V2 (lint baselines unchanged — no shipped pack uses `told`).

**If this fails:** revert the three hunks; nothing else depends on them yet.

---

### Step 2: `IntelRoller` — claimed roll + told day state + candidates + tell

**Objective:** Game-free rules for the whole told path.
**Confidence:** High
**Depends on:** Step 1
**Verify-Projex:** Encouraged

**Files:**
- `src/Core/ExpeditionRun.cs` (`IntelRoller`, :1255–1312)

**Changes:**

```csharp
// Before:
        public static Dictionary<string, List<int>> Roll(AreaDef area, Func<int, int> roll, IScope scope)
        {
            var result = new Dictionary<string, List<int>>();
            foreach (IntelSlot slot in area.Intel)
            {
                var picks = new List<int>();
                // Every slot is a key, including gated and poolless ones: ...
                result[slot.Id] = picks;
                if (slot.When != null && !slot.When(scope)) continue;
                if (!area.Pools.TryGetValue(slot.Pool, out List<string> pool)) continue;
                ...

// After:
        public static Dictionary<string, List<int>> Roll(AreaDef area, Func<int, int> roll, IScope scope) => Roll(area, roll, scope, false);

        /// <summary>
        /// As above; with <paramref name="toldOnDemand"/>, told slots pick nothing and roll no dice here, like gated ones: their
        /// cards come one at a time from <see cref="Tell"/>. Without it a told slot rolls like any other, so a pack with no
        /// teller keeps its intel.
        /// </summary>
        public static Dictionary<string, List<int>> Roll(AreaDef area, Func<int, int> roll, IScope scope, bool toldOnDemand)
        {
            var result = new Dictionary<string, List<int>>();
            foreach (IntelSlot slot in area.Intel)
            {
                var picks = new List<int>();
                // (existing comment)
                result[slot.Id] = picks;
                if (toldOnDemand && slot.Told) continue;
                if (slot.When != null && !slot.When(scope)) continue;
                ...  // rest unchanged, Limit unchanged
```

Add after `Limit` (before `HowMany`):

```csharp
        /// <summary>
        /// One area's told intel for the day, kept by the caller: what each slot has shown or reserved so far (rolled picks
        /// included, since intel_max counts them too) and how many cards each told slot may still hand out. A slot missing
        /// from <see cref="Budget"/> has not rolled its dice yet.
        /// </summary>
        public sealed class ToldDay
        {
            public readonly Dictionary<string, int> Budget = new Dictionary<string, int>();
            public readonly Dictionary<string, List<int>> Shown = new Dictionary<string, List<int>>();

            public int ShownCount => Shown.Values.Sum(s => s.Count);

            public List<int> ShownOf(IntelSlot slot)
            {
                if (!Shown.TryGetValue(slot.Id, out List<int> shown)) Shown[slot.Id] = shown = new List<int>();
                return shown;
            }
        }

        /// <summary>
        /// The told slots that can hand out a card now: when holds, an unshown pool position is left, the area is under its
        /// intel_max counting every card shown today, and the slot's daily budget is left. The budget is the slot's own dice
        /// (chance or counts), rolled the first time the slot gets this far in a day and kept in <paramref name="day"/>, so a
        /// told slot hands out at most what it would have shown at the day's roll. The cheap checks run before the dice so a
        /// slot that cannot show anyway draws none. <paramref name="slotId"/> limits to one slot.
        /// </summary>
        public static List<IntelSlot> TellCandidates(AreaDef area, Func<int, int> roll, IScope scope, ToldDay day, string slotId = null)
        {
            var candidates = new List<IntelSlot>();
            int limit = area.IntelMax != null ? Math.Max(0, area.IntelMax(scope)) : int.MaxValue;
            foreach (IntelSlot slot in area.Intel)
            {
                if (!slot.Told || (slotId != null && slot.Id != slotId)) continue;
                if (slot.When != null && !slot.When(scope)) continue;
                if (!area.Pools.TryGetValue(slot.Pool, out List<string> pool)) continue;
                if (day.ShownOf(slot).Count >= pool.Count) continue;
                if (day.ShownCount >= limit) continue;
                if (!day.Budget.TryGetValue(slot.Id, out int left)) day.Budget[slot.Id] = left = HowMany(slot, roll);
                if (left > 0) candidates.Add(slot);
            }
            return candidates;
        }

        /// <summary>
        /// Hands out one card of a slot <see cref="TellCandidates"/> returned: a random position of its pool not shown today,
        /// recorded in <paramref name="day"/> and charged to the slot's budget. Returns the pool position.
        /// </summary>
        public static int Tell(AreaDef area, IntelSlot slot, Func<int, int> roll, ToldDay day)
        {
            List<int> shown = day.ShownOf(slot);
            List<int> open = Enumerable.Range(0, area.Pools[slot.Pool].Count).Where(p => !shown.Contains(p)).ToList();
            int at = open[roll(open.Count) - 1];
            shown.Add(at);
            day.Budget[slot.Id]--;
            return at;
        }
```

**Rationale:** The 3-arg `Roll` becomes a thunk, so no caller or dice sequence changes (SC2). Skipping told slots **before** `When` mirrors the gated-slot rule ("rolls no dice") and the existing comment's rationale. Candidate order (cheap checks → dice) is what makes the lazy budget deterministic and testable (one `roll(100)` per slot per day). `ToldDay` is a plain bag rather than hidden static state so the game and the tests own persistence. Rejected: rolling the budget in `Roll` and hiding cards (user rejected the premise); a `Func<IntelSlot,bool>` skip predicate (more general than needed; `bool` names the one case).

**Verification:** V1 (`TestToldIntel` SC3, SC4), V2.

**If this fails:** revert the hunk; Step 1 stands alone.

---

### Step 3: `TestToldIntel` in `tests/Sim`

**Objective:** Deterministic coverage of Steps 1–2.
**Confidence:** High
**Depends on:** Steps 1–2

**Files:**
- `tests/Sim/Program.cs` (add `TestToldIntel();` after `TestIntelMax();` at :49; method after `TestIntelMax`)

**Changes:** pack built like `TestIntelMax`'s `Pack(...)` (pool `leads` = `r r2 r3`; slots `first` chance 100 untold, `told1` `told: true` chance 100, `told2` `told: true` counts `[0, 0, 100]` with `when: "memory.open >= 1"`, `last` chance 100 untold; `intel_max` via `memory.cap`). Checks (each a `Check(...)` with message):

1. SC1 — loads cleanly; `ProblemsIn(... "told": "yes" ...)` has one problem containing `intel[1].told` and `must be true or false`; `told` absent → `Told == false`.
2. SC3 — dice log: `Roll(area, logged, scope)` vs `Roll(area, logged, scope, false)` identical sequences and picks; `Roll(..., true)`: `told1`, `told2` keys present with `Count == 0`; logged sides = `100,3,100,3` (only `first` and `last` roll — same requests as if told slots were gated).
3. SC4 budgets — fresh `ToldDay`; `TellCandidates(area, logged, scope, day)` with `memory.open = 0`, cap 9: returns `[told1]` and logged `100` once; second call: no new dice, same list; `day.Budget["told1"] == 1`.
4. SC4 when — `memory.open = 1`: candidates `[told1, told2]` and the second slot rolled once (`100`); `told2` budget == 2.
5. SC4 tell — `Tell(area, told1, n => 1, day)` returns 0; `day.ShownOf(told1) == [0]`; budget 0 → `TellCandidates` no longer lists `told1`. `Tell(told2)` twice with `n => 1` returns 0 then 1 (distinct, next open); budget 0.
6. SC4 intel_max — new `ToldDay` with `Shown["first"] = [0]`, `Shown["last"] = [1]`, cap 2 → no candidates; cap 3 → `told1` listed; after one `Tell`, cap 3 → none.
7. SC4 pool exhausted — `ToldDay` with `Shown["told1"] = [0,1,2]` → `told1` not a candidate, and no budget roll for it (dice log empty).
8. SC4 slot filter — `TellCandidates(..., "told2")` lists only `told2`; unknown id → empty.
9. Budget 0 — slot with `chance: 0`: `TellCandidates` rolls once, lists nothing, `Budget[id] == 0`; no re-roll next call.

**Rationale:** Same harness/idioms as `TestMapMemory` (dice log) and `TestIntelMax` (pack builder), so the file stays one style.

**Verification:** V1 (`All checks passed`), V6 (`TestToldIntel` present and called).

**If this fails:** fix test or Step 2; nothing in the game depends on it yet.

---

### Step 4: `IntelBoard` (new, `src/Game/IntelBoard.cs`)

**Objective:** Pack-level claim state and the cross-area tell, independent of pack load order.
**Confidence:** Medium (game-side; compile-checked, played in Step 7)
**Depends on:** Step 2

**Files:**
- `src/Game/IntelBoard.cs` (new)

**Changes:**

```csharp
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Core;

namespace ExpeditionFramework.Game
{
    /// <summary>One card a told slot handed out: enough to show it later or word a line about it.</summary>
    internal sealed class ToldCard
    {
        public IntelCards Cards;
        public AreaDef Area;
        public IntelSlot Slot;
        public int Index;      // card index on the map (0..MaxPicks-1)
        public int Position;   // pool position
        public string Pick;    // qualified id the card names
    }

    /// <summary>
    /// Who tells a pack's told intel. A claim says some mod hands out the pack's told cards itself, so the day's roll leaves
    /// them alone; without one a pack rolls told slots with the rest. Claims live here, not on the pack's areas, because a
    /// claimant may speak before the packs are loaded (packs load on the first OnModAssetsLoaded, after every OnEnable),
    /// and a claim for an id that never becomes a pack is simply never read.
    /// </summary>
    internal static class IntelBoard
    {
        private static readonly HashSet<string> claimed = new HashSet<string>();
        private static readonly Dictionary<string, List<IntelCards>> packs = new Dictionary<string, List<IntelCards>>();

        public static void Claim(string packId) { if (!string.IsNullOrEmpty(packId)) claimed.Add(packId); }
        public static bool IsClaimed(string packId) => claimed.Contains(packId);
        /// <summary>Whether the id is a loaded pack with areas on the map.</summary>
        public static bool HasPack(string packId) => packs.ContainsKey(packId);
        public static IEnumerable<string> PacksWithToldSlots => packs.Where(p => p.Value.Any(c => c.HasToldSlots)).Select(p => p.Key);

        public static void Add(IntelCards cards) { ... packs[cards.Pack] list add ... }

        /// <summary>Forgets packs and claims; the pack registry calls this when the framework stops.</summary>
        public static void Reset() { claimed.Clear(); packs.Clear(); }

        /// <summary>
        /// Hands out one told card of the pack: a random candidate slot across its areas (area / slot filters optional, ids
        /// with or without the pack prefix), then a random unshown pick of it. show false reserves the card (budget and
        /// intel_max count it) for a later <see cref="Show"/>. Null when nothing can be told now.
        /// </summary>
        public static ToldCard Tell(string packId, string areaId = null, string slotId = null, bool show = true)
        {
            if (!packs.TryGetValue(packId, out List<IntelCards> areas)) return null;
            var candidates = new List<(IntelCards Cards, IntelSlot Slot)>();
            foreach (IntelCards cards in areas)
            {
                if (areaId != null && !cards.AreaIs(areaId)) continue;
                candidates.AddRange(cards.TellCandidates(slotId).Select(slot => (cards, slot)));
            }
            if (candidates.Count == 0) return null;
            (IntelCards picked, IntelSlot slot) = candidates[RNG.GetRandomInt(1, candidates.Count) - 1];
            return picked.Tell(slot, show);
        }

        public static bool Show(ToldCard card) => card != null && card.Cards.Show(card);
    }
}
```

`IntelCards.AreaIs(id)`: `area.Id == id || area.Id == area.Pack + ":" + id`.

**Rationale:** The board is per pack, `IntelCards` is per area; claims must outlive pack loading, so they can't sit on `IntelCards` (proposal placed "claim state" in `IntelCards.cs` — moved here for the load-order reason; same internal surface `Claim` / `Tell`). Picking the slot first then the position (proposal § 1.3) rather than pooling all open positions keeps a big pool from drowning a small one. Rejected: persisting claims in save counters (a claim is a fact about installed mods, re-made each load).

**Verification:** V3 (compiles), Step 7 judgment.

**If this fails:** delete the file; Steps 5–6 depend on it, so revert them together.

---

### Step 5: `IntelCards` — claimed roll, told state, tell, held cards

**Objective:** Per-area day state for told slots on the existing counters; mid-day show.
**Confidence:** Medium
**Depends on:** Steps 2, 4
**Verify-Projex:** Encouraged

**Files:**
- `src/Game/IntelCards.cs`
- `src/Game/ExpeditionLocation.cs`

**Changes:**

0. Discovery: `IntelCards` ctor gains a last parameter `Func<bool> discovered`, kept in a field. `ExpeditionLocation` ctor (:60): `intel = new IntelCards(area, Id, map, IsDiscovered);`. Rolled cards already respect discovery (`GetTitle` calls `EnsureToday` only when discovered, and the map hides undiscovered locations); told cards must too, since a customer naming a place the player can't see yet reads as a bug.
1. Ctor: after the register loop, `IntelBoard.Add(this);`. Add `public string Pack => area.Pack;`, `public bool HasToldSlots => area.Intel.Any(s => s.Told);`, `public bool AreaIs(string id)`.
2. `Register`: `canStart: () => Rolled() && Pick(slot, index) != null && !Held(slot, index)`.
3. `EnsureToday`: `Show(CardId(slot, i), Pick(slot, i) != null && !Held(slot, i))`.
4. `Rolled()`:

```csharp
// Before:
            Dictionary<string, List<int>> picks = IntelRoller.Roll(area, sides => RNG.GetRandomInt(1, sides), map.Root);
            foreach (IntelSlot slot in area.Intel)
            {
                for (int i = 0; i < slot.MaxPicks; i++)
                {
                    int position = i < picks[slot.Id].Count ? picks[slot.Id][i] : -1;
                    state.SetCounter(PickCounter(slot, i), position + 1);
                    if (slot.GoodPicks.Count > 0 && cards.TryGetValue(...)) { card.toneOverride = ...; }
                }
            }
// After:
            // A claimed pack's told slots are left to IntelBoard.Tell, which is also why their budget and held marks reset here:
            // a card reserved yesterday and never shown lapses with the day, like an untold rumour.
            Dictionary<string, List<int>> picks = IntelRoller.Roll(area, Dice, map.Root, IntelBoard.IsClaimed(area.Pack));
            foreach (IntelSlot slot in area.Intel)
            {
                state.SetCounter(BudgetCounter(slot), 0);
                for (int i = 0; i < slot.MaxPicks; i++)
                {
                    int position = i < picks[slot.Id].Count ? picks[slot.Id][i] : -1;
                    state.SetCounter(PickCounter(slot, i), position + 1);
                    state.SetCounter(HeldCounter(slot, i), 0);
                    SetTone(slot, i, position);
                }
            }
```

with `private static int Dice(int sides) => RNG.GetRandomInt(1, sides);` and `SetTone(slot, i, position)` extracted from the old inline tone code (used by `Tell` too).

5. New members:

```csharp
        // Today's told state as IntelRoller reads it: every pick on the counters counts as shown (held ones too, since they
        // are promised), and a told slot's budget is on its own counter, +1 so 0 can mean "not rolled yet".
        private IntelRoller.ToldDay Day()
        {
            var day = new IntelRoller.ToldDay();
            foreach (IntelSlot slot in area.Intel)
            {
                List<int> shown = day.ShownOf(slot);
                for (int i = 0; i < slot.MaxPicks; i++) { int p = State.GetCounter(PickCounter(slot, i)) - 1; if (p >= 0) shown.Add(p); }
                int budget = State.GetCounter(BudgetCounter(slot));
                if (slot.Told && budget > 0) day.Budget[slot.Id] = budget - 1;
            }
            return day;
        }

        private void Keep(IntelRoller.ToldDay day)
        {
            foreach (KeyValuePair<string, int> b in day.Budget) State.SetCounter("ef_intel_" + b.Key + "_budget", b.Value + 1);
        }

        /// <summary>The told slots that can hand out a card now (the day is rolled first if it hasn't been).</summary>
        public List<IntelSlot> TellCandidates(string slotId)
        {
            if (!PlayerStore.HasInstance || StoreStation.Instance == null) return new List<IntelSlot>();
            // Checked before Rolled() and the budget roll, so an undiscovered area draws no dice and keeps no budget for later.
            if (!discovered()) return new List<IntelSlot>();
            Rolled();
            IntelRoller.ToldDay day = Day();
            List<IntelSlot> candidates = IntelRoller.TellCandidates(area, Dice, map.Root, day, slotId);
            Keep(day);
            return candidates;
        }

        /// <summary>Hands out one card of a candidate slot and shows it now, or holds it for <see cref="Show"/>.</summary>
        public ToldCard Tell(IntelSlot slot, bool show)
        {
            Rolled();
            IntelRoller.ToldDay day = Day();
            int position = IntelRoller.Tell(area, slot, Dice, day);
            Keep(day);
            int index = Enumerable.Range(0, slot.MaxPicks).First(i => State.GetCounter(PickCounter(slot, i)) == 0);  // budget <= MaxPicks, so one is free
            State.SetCounter(PickCounter(slot, index), position + 1);
            SetTone(slot, index, position);
            var card = new ToldCard { Cards = this, Area = area, Slot = slot, Index = index, Position = position, Pick = area.Pools[slot.Pool][position] };
            if (show) AfterhourEvents.Start(CardId(slot, index));
            else State.SetCounter(HeldCounter(slot, index), 1);
            return card;
        }

        /// <summary>Shows a held card. False when the day has turned since, so the hold lapsed.</summary>
        public bool Show(ToldCard card)
        {
            if (!PlayerStore.HasInstance || StoreStation.Instance == null) return false;
            if (State.GetCounter(DayCounter) != StoreStation.Instance.dayCounter || State.GetCounter(PickCounter(card.Slot, card.Index)) != card.Position + 1) return false;
            State.SetCounter(HeldCounter(card.Slot, card.Index), 0);
            AfterhourEvents.Start(CardId(card.Slot, card.Index));
            return true;
        }

        private bool Held(IntelSlot slot, int index) => PlayerStore.HasInstance && State.GetCounter(HeldCounter(slot, index)) == 1;
        private static string HeldCounter(IntelSlot slot, int index) => PickCounter(slot, index) + "_held";
        private static string BudgetCounter(IntelSlot slot) => "ef_intel_" + slot.Id + "_budget";
```

Class summary comment: extend with the told sentence (a claimed pack's told slots are handed out by `IntelBoard.Tell`, one card at a time, on the same counters).

**Rationale:** Reusing `ef_intel_{slot}_{i}` keeps reload, `Today()`, `Render` and `canStart` unchanged for told picks (proposal § 1.4). A separate `_held` counter rather than a sign trick on the pick counter keeps `Pick()` readable by the game-side reader who only knows "position+1". `Day()` rebuilt from counters on every call rather than cached: cheap (a handful of counters) and reload-proof. Rejected: a static per-area cache (dies on reload while counters survive; two truths).

**Verification:** V3 (compiles); Step 7 judgment (A1).

**If this fails:** `git checkout -- src/Game/IntelCards.cs src/Game/IntelBoard.cs src/Game/ExpeditionLocation.cs`; Core steps unaffected.

---

### Step 6: `PackRegistry.Stop` → `IntelBoard.Reset()`

**Objective:** No stale `IntelCards` references or claims after the framework stops.
**Confidence:** High
**Depends on:** Step 4
**Do-Projex:** Encouraged

**Files:**
- `src/Game/PackRegistry.cs`

**Changes:** in `Stop()`, after `DevTools.RemoveAll();` add `IntelBoard.Reset();`.

**Rationale:** `Stop` is where every per-pack registration is undone; `Start` needs nothing since `Reset` leaves the board empty.

**Verification:** V3.

**If this fails:** revert the line.

---

### Step 7: Debug-panel claimant (`DevTools`)

**Objective:** Play the claimed path before any ZoH story exists (A1 check; SC6).
**Confidence:** High
**Depends on:** Steps 4–5
**Do-Projex:** Encouraged

**Files:**
- `src/Game/DevTools.cs`

**Changes:** in `Add(content, pack)`, after the unlock entries:

```csharp
            if (content.Areas.Values.Any(a => a.Pack == pack && a.Intel.Any(s => s.Told)))
            {
                // A stand-in teller for packs played without a story mod: claim for this session, then tell one card at a time.
                ModHelper.AddDebugFunction(ModEntry.ModId, ModHelper.DEBUG_CATEGORY_MODDING, "Expeditions: claim intel (" + pack + ")", () => IntelBoard.Claim(pack));
                ModHelper.AddDebugFunction(ModEntry.ModId, ModHelper.DEBUG_CATEGORY_MODDING, "Expeditions: tell intel (" + pack + ")", () =>
                {
                    ToldCard card = IntelBoard.Tell(pack);
                    ModEntry.Log.Log(card == null ? "Intel: nothing to tell for " + pack : "Intel: told " + card.Slot.Id + " -> " + card.Pick + " in " + card.Area.Id);
                });
            }
```

**Rationale:** Same `AddDebugFunction` pattern as the kit/unlock entries; removed with them by `RemoveAll`. Claim is per session, so the entry must be pressed before the day rolls (A3) — the log line says what was told either way.

**Verification:** V3; judgment J1–J3.

**If this fails:** revert the hunk; the board still works for the verb plan.

---

### Step 8: Docs

**Objective:** SC7.
**Confidence:** High
**Depends on:** Steps 1–7
**Do-Projex:** Encouraged

**Files:**
- `docs/intel.md` — slot table row `told` (`true`: when a mod that tells intel claims this pack, the slot's cards are handed out one at a time as customers tell them, and don't appear at the day's roll; without a teller it rolls as usual. Default `false`); new section `## Told slots` after "When it is rolled": what claiming means; each tell rolls one card (slot's own `chance` / `counts` as its daily budget, rolled the first time it's asked; `when` checked at the moment of telling; `intel_max` counts cards already showing; a pool position shows at most once a day; an area the player hasn't discovered is never told about); nothing hidden, nothing carries over; ef-sim rolls told slots as rolled; a pack's told slots play as today until a teller mod claims it; pointer: "the ZoH verbs that tell are in zoh.md" (added by the provider plan — write the sentence without the link now, the provider plan links it).
- `docs/testing.md` `intel` row (:258): add "Told slots roll with the rest: the sim counts every card as told."
- `docs/csharp-api.md` (:134–135): "`IntelRoller.Roll` … and applies the area's `intel_max` after every slot has rolled. A fourth argument, `toldOnDemand`, leaves `told` slots to `IntelRoller.TellCandidates` and `Tell`, which hand out one card at a time against a `ToldDay` the caller keeps."

**Verification:** V4; judgment J4.

**If this fails:** docs are independent; revert the files.

---

## Verification Plan

> Per-step verification (above) confirms each change in isolation. This section confirms the changes work together end-to-end. Shell: Git Bash from `<repo-root>`; `Q` = scratch dir outside the repo; `SIM="dotnet run -c Release --no-build --project tools/EfSim --"`. Baselines are taken **before Step 1** on `main` @ `492d5cc`.

### Deterministic Suite

| ID | Covers | Command | Expected | Catches |
|----|--------|---------|----------|---------|
| V0 | SC2 baseline | `dotnet build -c Release tools/EfSim`; for `n` in `docs/examples/Starter docs/examples/Showcase samples/SampleArea`: `$SIM lint $n --strict > $Q/before/$(basename $n)-lint.txt`; `$SIM run $n > $Q/before/$(basename $n)-run.txt` | files written | — |
| V1 | SC1, SC3, SC4 | `dotnet run -c Release --project tests/Sim \| tail -n 1` | `All checks passed` | Any roller / schema regression, told rules wrong |
| V2 | SC2 | after all steps: rebuild EfSim, same three lint + run commands into `$Q/after/`; `cmp $Q/before/$n-lint.txt $Q/after/$n-lint.txt` and `cmp $Q/before/$n-run.txt $Q/after/$n-run.txt` (×3) | exit 0 ×6 | Dice / lint drift for packs without `told` |
| V3 | SC5, SC6, SC8 | `ls -l "../../Mods/ExpeditionFramework" > $Q/live-before.txt`; `dotnet build -c Release ExpeditionFramework.csproj -p:InstallDir=$Q/game-install/`; `ls -l "../../Mods/ExpeditionFramework" > $Q/live-after.txt`; `cmp $Q/live-before.txt $Q/live-after.txt` | build exit 0 with `0 Error(s)`; `$Q/game-install/ExpeditionFramework.dll` exists; cmp exit 0 | Game assembly break; live install touched |
| V4 | SC7 | `grep -c '## Told slots' docs/intel.md`; `grep -c 'told' docs/testing.md`; `grep -c 'TellCandidates' docs/csharp-api.md` | each ≥ `1` | Missing doc |
| V5 | SC8, scope | `git diff --quiet -- api tools/EfSim ExpeditionFramework.csproj manifest.xml; echo $?` | `0` | API / sim / build surface touched |
| V6 | SC4 | `grep -c 'TestToldIntel' tests/Sim/Program.cs`; `grep -c 'IntelBoard' src/Game/IntelCards.cs src/Game/PackRegistry.cs src/Game/DevTools.cs` | ≥ `2`; each ≥ `1` | Test not wired; board not wired |
| V7 | SC2 | `git diff -- src/Core/ExpeditionRun.cs \| grep -E '^-' \| grep -v '^---' \| grep -c 'public static Dictionary<string, List<int>> Roll(AreaDef area, Func<int, int> roll, IScope scope)'` | `0` (the 3-arg signature line is not removed) | Signature change |
| V8 | all | `git status --porcelain` | only the planned files + `.projex/` docs | Stray build output / scratch |

### Judgment Checks

| Check | Covers | Why not deterministic |
|-------|--------|-----------------------|
| J1 — In game, a local throwaway pack (not committed) with one slot `told: true`: **without** pressing claim, its card appears at the day's roll as before | SC5 (unclaimed) | Needs the game |
| J2 — Press "claim intel" before the day rolls (new day), then open the map: the told card is absent; press "tell intel": the card appears on the area's map card right then (A1); log line names slot and pick; pressing again past the budget logs `nothing to tell`; reload mid-day keeps the told card | SC5, A1 | Needs the game |
| J3 — Save/reload after J2 then next day: told card gone, told slot absent again until told | SC5 | Needs the game |
| J6 — Throwaway pack with a told slot in an area locked by `unlocked_by` (not yet cleared): claim, then "tell intel" repeatedly → only discovered areas' cards are told; with only the locked area, the log says `nothing to tell`. After unlocking, the area's told cards come through | SC5 (discovery) | `IsDiscovered` reads game state (`GameValues.Clears`, `hidden_when` on the map scope); `src/Game` isn't in `tests/Sim` |
| J4 — `docs/intel.md` § Told slots reads so a pack author knows: nothing is hidden, budget = the slot's dice, `when` at tell time | SC7 | Clarity |
| J5 — New comments: present tense, each non-obvious choice names its rejected alternative (board vs cards; `_held` counter vs sign; `Day()` rebuilt vs cached; slot-then-position pick) | Source Hygiene | Prose judgment |

### Acceptance Criteria Validation
| Criterion | Check IDs | Expected Result |
|-----------|-----------|-----------------|
| SC1 | V1 | told flag parsed, wrong type is a problem |
| SC2 | V1, V2, V7 | 3-arg `Roll` and all baselines unchanged |
| SC3 | V1 | told slots skipped without dice when claimed |
| SC4 | V1, V6 | candidates / tell / lazy budget rules |
| SC5 | V3 + J1–J3, J6 | compiles; plays as specified; undiscovered areas never told |
| SC6 | V3, V6 + J2 | entries present and working |
| SC7 | V4 + J4 | docs present and clear |
| SC8 | V3, V5, V8 | compiles, nothing deployed, surfaces untouched |

---

## Rollback Plan

1. Before any commit: `git checkout -- src/Core src/Game tests/Sim docs` and delete `src/Game/IntelBoard.cs` (named paths only).
2. After step commits: `git revert` each step commit, newest first.
3. Nothing is deployed: builds go to a scratch `InstallDir`.

---

## Revision Log

- **2026-11-15:** Undiscovered areas give no tell candidates: `IntelCards` takes `ExpeditionLocation.IsDiscovered` and `TellCandidates` returns none before rolling (Step 5 change 0 + guard; SC5; Key Files; J6). Trigger: NER plan 2611152343-customer-rumours-plan.md Assumption A4 (`IntelBoard.Tell` didn't filter by `IsDiscovered()`, so a rumour could name a locked area; NER can't fix that in data). User: "Yes, add it".

## Notes

### Risks
- A claim after today's roll leaves told slots rolled for the day (A3): accepted; `Tell` still hands out whatever budget the roll didn't use (the rolled picks count as shown, so budget − shown is usually 0). Documented in intel.md.
- Skipping told slots moves which RNG values later slots draw on a claimed day (stateful RNG), as a gated slot does today: accepted per proposal; no seed is fixed per day.
- The game may re-evaluate `canStart` mid-day on some path (A1): held cards are guarded by `_held`; told-but-unshown cards have counter 0, so nothing can leak.
- Split verdict: **Split required — cross-repo seam.** This plan is the ZohFramework-free half; the provider half is 2611152314-zoh-verb-provider-plan.md. Within this plan: 8 steps > 5 but under 500 lines/50 KB, and Steps 4–7 are one feature over one counter family — no further split.

### Open Questions
- [ ] None.
