# Runtime, Hooks & Expression Language — Status Quo and Expansion Surface

> **Status:** Complete (Done)
> **Author:** Claude Sonnet 5.5 (explore subagent)
> **Type:** Architectural
> **Parent:** Orchestrator
> **Related Projex:** 2611120240-content-expansion-synthesis-eval.md (NightExpeditionsReborn; its § Unverified Capabilities checked below), sibling explorations of this round (schema/loader, persistence/integration, UI, tooling)

---

## Summary

Runtime = `src/Core/ExpeditionRun.cs` (+`.Api.cs`), `Expr.cs`, `Builtins.cs`; all line refs below are to those unless noted. Engine is small, coherent, data-driven; **11 fixed hooks**, **7 decisions** (C#-only), **int-only expression language**, **~70 built-in effects/conditions**.

Key findings:

1. **Decisions are C#-only.** `Decide()` (ExpeditionRun.cs:740) runs only `content.Listeners` interceptors, registered via `IExpeditionRegistrar.Intercept`. A JSON-only pack (NER has no C#) cannot do: percent damage mods, vulnerability, shield/absorb, block-next-hit, crit, damage types, loot swap, intent override. Biggest single expansion: data-defined interceptors (R1).
2. **Hooks are subject-scoped and thin.** `Emit` (725-738) runs kit/area/room/encounter hooks for *every* subject, but enemy-def hooks and status hooks only for the *subject*. No observer path ("ally defeated", "hero killed something" on a status). No hooks for status apply/expire, item found, spawn, room exit, event start/end, heal.
3. **Statuses are minimal**: refresh-only (no stacks), `Turns`/`Rooms` expiry is silent (no hook), cannot read remaining turns, cannot grant tags, `clear_status` only by id. A status + `on_expire` hook = free timer/clock/bomb primitive (R2).
4. **Schedule is a passive depth-keyed lookup** (one event per depth, absolute depth only, fires only if a room hook polls `event:{scheduled:N}`). No combat-turn timers, no relative timers.
5. **Layout is frozen** at `Begin()` before `run_start` (275-293); no `enter_room`/`insert_room`/jump. Blocks Long Night, branching, loops, alarm-added rooms (R5).
6. **Several silent traps** (verified in code): writes to reserved `run.*` names are dropped; `remove` of last active enemy = fight WON; `spawn` outside combat is a no-op whose `then` still runs; spawn during enemy phase loses its Opening intent; 1-turn status applied by an enemy expires before the player acts; render-time RNG (`variants`, dice in `when`) re-rolls every 0.5 s.
7. **Expression language gaps**: no `%`, `abs`, `rand(a,b)`, no `has_status()`/status-turns/intent-id readers, no `focus.`/`source.` heads, no text compare, no roster queries beyond `list_size`.
8. Prior eval's 9 "Unverified Capabilities": runtime-side items resolved below (§ Answers 3).

**Guiding Questions:**
1. What does one run actually execute, in what order, and which hooks fire on what?
2. Where do the runtime and expression language constrain what a JSON pack can express?
3. Which runtime expansions are cheap and high-leverage vs. expensive?
4. Which assumptions in the NER content-expansion eval are true/false at runtime level?

**Scope:** In: run flow, hooks/decisions, statuses, enemies/intents, scheduling, spawning, Expr, Builtins, shared effects, render-time evaluation. Out: pack loader/schema (sibling), persistence/GameBridge (sibling), UI layout (sibling), sim/tooling (sibling). Read-only; no code changed, nothing executed (no build available in-session — all claims by code reading).

---

## Context

**Why Now:** Human requested exploration of useful framework expansions before returning to NER content work; five parallel runs, this one on the runtime slice.

**Current State:** Framework mature enough that NER (data-only: `ExpeditionFramework/*.json`, `src/ModEntry.cs` 10 lines) uses shared effects (`drop`, `run_off`, `alarm`) as functions and area/kit hooks for rules. No C# effects/interceptors used by NER.

**Stakeholders:** Framework maintainer; pack authors; NER content work.

---

## Investigation Targets

### Target: Run lifecycle and turn loop
**Rationale:** Foundation: order of everything.
**Status:** Done
**Findings:**

`Begin()` (263-273): area vars → player stats (`max_hp`=`hp`) → `BuildLayout` → `Emit run_start` → entrance room or `NextRoom`.
- `BuildLayout` (275-293): each slot picks `Count(root)` rooms by weight from `Pool` filtered by room `When(root)` and `Unique`. Done once; `rooms` private list; RNG consumed here.
- `NextRoom` (487-502): `EndCurrent` (drops event/fight/enemies) → if `depth>=rooms.Count` → `Finish("cleared")` → `rooms[depth++]` → tick Hero `Rooms` statuses (removing at 0) → `EnterRoom` (room text, `room.Enter`, then `Emit room_enter`). Consequence: status `rooms:N` sees N-1 `room_enter`s; `rooms:1` dies on next room entry *before* `room_enter`.
- `Retreat` (515) = `depth--` so `NextRoom` re-enters current room. Only flow primitive besides `next_room`.
- `Finish(outcome)` (346-355): sets `run.outcome_<x>`=1, emits `run_end` (Outcome still null, `Over` false → hook effects run normally), then sets Outcome, clears enemies. Outcomes are arbitrary strings (`leave` effect, `cleared`, `knocked_out`).

Combat:
- `Engage` (552-571): `turn=1`; each active enemy: `Acted=null`, intent = Opening if `surprised` else `NextIntent`; `prepare` per enemy; focus first; `Emit combat_start`; telegraph (logs each enemy's `Tell`).
- `Perform` (858-879): marks `usedThisRoom` (all interactions, not just `Once`), consumes items *before* body, plays sound, runs `Body`, ends own event unless `Stay`/`moved`, then if in combat and `TakesTurn` → `EnemyTurn()`, else if no active enemies → `EndFight(true)`; finally `after_action` (Hero subject) if `TakesTurn` and not Over. `after_action` also fires outside combat.
- `EnemyTurn` (881-925): `Emit turn_end(Hero)` (= end of *player* action, before enemies) → per enemy in list order (snapshot): `Emit turn_start(enemy, Hero)` → `Acted=Intent` → `Intent.Do(self=enemy,target=Hero)` → `TickStatuses(enemy)`. After all: `TickStatuses(Hero)`; re-choose every active enemy's intent (`Acted.Next` chain unless `Forced` by status; if intent was changed during the phase keep it); telegraph; if none active → `EndFight(true)`; `turn++`; `Emit turn_start(Hero, null)`.
- `EndFight` (590-609): clears enemies, `run.won`, `Emit combat_end`, then encounter's own `combat_end` hook (root scope; encounter hooks are skipped in `Emit` for `combat_end` to avoid double fire), then `won` effect if won.
- `Damage` (357-375): `damage` decision → `dealt = max(0, value − armor)` unless `pierce`; writes `Base hp` (not status-adjusted); sets `run.last_damage`; `Emit damaged(victim, source)` **even when dealt=0**; then `hp<=0` → `Defeat`. Cancelled decision: no hook, `last_damage=0`.
- `Defeat`: Hero → `Emit knocked_out` then `Finish` unconditionally (hook cannot save the hero). Enemy → `Defeated=true`, `Emit defeated`. `Defeated` is never cleared anywhere (no revive).

**Hero actions:** exactly one `TakesTurn` action per enemy phase. Free actions only via `TakesTurn:false`. No initiative, no enemy multi-action, no speed.

---

### Target: Hook dispatch (`Emit`) — what fires on what
**Rationale:** Core of the question.
**Status:** Done
**Findings:**

Hook names (ExpeditionRun.cs:174): `run_start, room_enter, combat_start, turn_start, turn_end, combat_end, damaged, defeated, knocked_out, after_action, run_end`. Custom hooks only through C# `AddHook` (PackLoader.cs:41); JSON `on:` rejects unknown names (PackLoader.cs:1006). `emit` effect (Builtins.cs:376) fires any known hook with chosen subject/other.

`Emit(name, subject, other)` scope: `Self = subject ?? Hero`, `Target = other`. Dispatch order: **kits → area → room → encounter (not for combat_end) → subject's EnemyDef hooks → subject's statuses → C# listeners** (737, guarded).

Fire matrix (subject → Self/Target):

| Hook | Subject / Self | Target |
|---|---|---|
| run_start / run_end / room_enter / combat_start / combat_end / after_action | Hero | null |
| turn_end | Hero (before enemies act) | null |
| turn_start | each enemy (Target=Hero), then Hero (Target=null) | |
| damaged | victim | source (Hero for room/event damage, since scope Self=Hero) |
| defeated | enemy victim | source |
| knocked_out | Hero | source |

Consequences (all verified by reading):
- **Area/kit/room/encounter hooks run once per subject** — `turn_start` runs N+1 times a round; packs must guard with `self.#player` (NER combat.json:339 does).
- **Enemy-def and status hooks only see their own subject events.** A status on the Hero cannot react to an enemy dying or enemy being damaged; an enemy cannot react to an ally dying. Workaround today: encounter/area hook + `for_each`/`pick_enemy`. Encounter hooks exist only when fight started via `fight`/`MusterEncounter`; plain `muster` of enemy ids sets no encounter.
- **Enemy statuses see only the enemy's own turn_start/damaged/defeated.** No `turn_end` for enemies (turn_end subject is always Hero).
- **Status on Hero `turn_start` has `target=null`** — companions/auras must choose victims with `pick_enemy`/`for_each`, no `focus` path in Expr (focus only via `focus` effect / `Run.Focus`).
- `damaged` runs *before* the lethal check → a `damaged` hook that raises hp above 0 prevents the death (works for Hero "last stand" and for enemy two-phase bosses "survive and transform"). `knocked_out` cannot.
- `damaged` fires with dealt 0 — hooks should test `run.last_damage > 0`.
- Hook table is `Dictionary<string,Effect>` → one effect per hook per def (use sequence/`all`).
- Guard: only C# listeners/interceptors go through `Guarded` (try/catch + 32-deep recursion limit via `EnterShared`); data hooks have no catch — a thrown exception propagates to `Perform` whose caller logs (ExpeditionLocation.cs:416-419) but any partial state remains. Shared effects `EnterShared`/`LeaveShared` (PackLoader.cs:477-485) have no `finally` → an exception inside a shared effect leaks `sharedDepth` permanently (after 32 leaks all shared calls log "called itself too deeply" and no-op).

Missing hook points (each a one-line `Emit` at an existing site): `room_exit` (NextRoom before EndCurrent), `status_added` (AddStatus), `status_expired` (TickStatuses / Rooms removal / ClearStatus), `item_found` (`Found` 422), `healed` (Api.cs Heal), `spawned` (Spawn 580), `intent_chosen`/`telegraphed`, `event_start`/`event_end`, `retreated`, `trade_closed`, `check_rolled` (natural 1/20 reactions), `round_start`/`round_end` (once per round, un-subjected).

---

### Target: Decisions / interceptors
**Rationale:** Candidate #1 expansion; only mod-level extension point.
**Status:** Done
**Findings:**

`DecisionNames` (ExpeditionRun.cs:180): `damage, heal, check, chance, status, loot, intent`. Fields per decision documented in api/ExpeditionApi.cs:51-62. Call sites: `damage` Damage(), `heal` ICombat.Heal (Api.cs), `check` RollCheck (300-305; applies to `die`/`roll` effects only, not `check:{roll:..}` dice-sum form or `chance`), `chance` Builtins.cs:288, `status` AddStatus (445), `loot` DecideLoot (643), `intent` NextIntent (937). Button previews skip interceptors (ExpeditionApi.cs:61) so displayed % can diverge.

Only registration path: `IExpeditionRegistrar.Intercept` (C# mod). `Listeners` (PackLoader.cs:63-86) is global across all packs ("on every expedition from every pack" — must check `Run.AreaId`). **No JSON surface.** Pack authors who need "+50% damage taken while exposed", "absorb 5 damage", "next hit misses", "crits on 20", "reroll loot" must write C#.

What exists in data for damage shaping: `armor` stat (flat subtraction, status `stats` add flat armor), `pierce` flag, `amount` expression on the *attacker's* side. Defender-side percent/absorb/cancel: impossible.

Gaps in the decision set itself: no `spawn`, `status_expire`, `flee`, `trade_price`, `intel`, `room_pick`; `damage` decision has no damage *type/tag*; `loot` has no per-entry context.

---

### Target: Statuses
**Rationale:** Prior eval leaned on statuses (companions, chilled, room-roll mods).
**Status:** Done
**Findings:**

`StatusDef` (Model.cs:165): `Turns`, `Rooms`, `Stats` (flat adds to owner's `Get`), forced `Intent`, `Interactions`, `Hooks`; inherits `Def.Tags` (unused at runtime for entity tag checks: `Entity.HasTag` reads only construction-time tags).
`AddStatus` (445-463): `status` decision (turns/rooms mutable); **re-applying only refreshes duration** (no stack count, no intensity, no extend/keep policy). `ClearStatus` by id only (no "clear all debuffs"/by tag). `has_status` is a JSON *condition* only — **no Expr function**, no way to read turns left or stacks.
Ticking: enemy status ticks after that enemy's action; Hero statuses tick once at end of enemy phase (`TickStatuses(Hero)` 906); Hero `Rooms` tick on `NextRoom`. Expiry is silent.
- **Gotcha:** status applied *to Hero by an enemy* during its action then ticked at end of the same phase → `turns:1` is gone before the player acts; needs `turns:2`. Applied *by Hero to enemy* with `turns:1` covers that enemy's next action (good for stun via forced intent).
- Statuses with `Turns>0` don't tick outside combat; `Rooms` ticks only for Hero.
- Status `Interactions` offered to owner (Hero: `Offer(i, Hero)`; enemy: `Offer(i, e)`); status can add buttons ("Throw off the chill").
- Status `stats` apply to `Get` not `Base`; `damage` writes `Base("hp")` so an hp-adding status doesn't act as temp HP shield.

Companion pattern feasibility (prior claim): Hero status with `turn_start` hook + `pick_enemy` → damage works. A companion that *dies*, *takes hits*, or is *targeted* does not (no friendly entities; `Entity.IsPlayer` true only for Hero; enemies' `Intent.Do` always targets Hero by scope).

---

### Target: Enemies, intents, behavior, spawn/remove
**Rationale:** Bosses, hidden enemies, healers, summoners in prior imagine docs.
**Status:** Done
**Findings:**

`MakeEntity` (527-535): stats from def Values evaluated with `self=enemy,target=Hero`; `max_hp` = hp. Texts copied. Tags fixed (immutable `HashSet`, no add/remove API).
Behavior: weighted tree (`Choose` 947-959) with `When`; `Intent.Next` chain; `Opening` for surprise; `intent` decision may swap; statuses with `Intent` force a move (`Forced`). Enemy intent id **not readable in Expr** (`IntentId` exists on IEntity but no path; `intent_interruptible` condition only).
- `spawn` (Builtins.cs:563-581): random pick from `Enemies()` source, `Combat.Spawn`, `then(self,arrival)`. `ExpeditionRun.Spawn` (580-588) **no-ops unless Scene=="combat"** (the arrival is already created and `then` still runs on an orphan). During enemy phase: arrival not in the snapshot so it doesn't act this phase, but the end-of-phase re-choose loop (908-917) has `Acted==null` → `NextIntent` → **overwrites its Opening/surprised intent**. Max enemies: unbounded.
- `remove`/`Gone` (Api.cs ICombat.Remove): `Active=false`; stays in list until fight end; **cannot be un-removed**. Last active enemy removed (or defeated) → `EndFight(true)` ("won"). That makes "Lurker hides" (prior imagine claim: remove + schedule) end the fight as a win unless something else keeps an active enemy. NER uses `remove` as flee/surrender/morale-break (rules.json:113,158; combat.json:456) where win-on-last is intended.
- Heal ally: `pick_enemy`/`for_each` + `heal{to:target}` works (Stitcher claim: **true** via data). `Heal` doesn't check `Active`: healing a defeated enemy raises hp but `Defeated` stays → no revive (**false** for "resurrect").
- Two-phase boss via enemy-def `on.damaged` with `self`=enemy: **true** (Self=victim); can also survive lethal by raising hp in the hook.
- No transform/morph effect (`Entity.Def` is replaceable in C# but no effect); phase change = `intent`/`set` stats/`status`, or `spawn` replacement (stats reset, state via run vars).
- Targeting: Hero is always the enemy's target; no taunt/aggro, no ally entities, no AoE helper beyond `for_each`.

---

### Target: Scheduling
**Rationale:** Dawn clock, Lurker, intel events.
**Status:** Done
**Findings:**

`schedule`: `SortedDictionary<int,string>` keyed by **absolute depth**, single event id per depth (`IFlow.Schedule` overwrites; effect-level `schedule` with `pool` refuses if one is due, Builtins.cs:413-426). Fires only when some hook runs `event:{scheduled: <depth expr>}` (Builtins.cs:345-358: starts the event and unschedules). Nothing auto-fires. `schedule:{intel,depths}` randomizes intel picks into distinct depths 1..N (389-410). Reads: `scheduled(depth)` Expr fn, `run.scheduled` (count; **shadows any var named `scheduled`**).
Missing: relative timers (`in_rooms: n`), combat-turn timers, recurring (`every`), multiple events per depth, effect-valued (non-event) schedules, cancellation by tag, timers shown in `run` readers. Events themselves are mini-rooms (no hooks, only `Enter` + `Choices`).
Workaround for clocks: status with `rooms:N` + (missing) `on_expire`; or room_enter hook decrementing `run.clock` — works today, only lacks expiry hook.

---

### Target: Expression language (`Expr.cs`)
**Rationale:** Constraint on every condition/value.
**Status:** Done
**Findings:**

Grammar: ternary → `||` → `&&` → compare (single, non-chaining) → `+ -` → `* /` → unary `- !` → atom. Ints only; booleans 1/0; `/` truncates, **divide by zero silently yields 0**; short-circuit `&&`/`||`/`?:` (so dice on the right of `&&` roll only if left true). Dice literals `2d6` and `dice(n,s)` roll via `Run.Roll` (**RNG consumed at evaluation**).
Paths: `self|target|player.NAME`, `.#tag`, `item|with.NAME/#tag`, `run.NAME`, `role.R.NAME`, `arg.NAME`, `game.NAME` (only luck, roll_luck, wounds, maimed in GameBridge.cs:184-197), `val.NAME` (named values, recomputed each read — dice in a named val re-roll per read).
Functions: `carried, best_item, intel, mod, list_size, min, max, clamp, dice, scheduled, count_enemies, any_enemy, all_enemies, sum_enemies, max_enemies`.
`run.X` reads (ExpeditionRun.cs:317-331): reserved `depth, rooms, rooms_left, turn, enemies, credits, ground, scheduled`; others = vars. **Writer (Expr.cs:124) writes the vars dict, so `set run.depth/turn/rooms` is silently ignored.** Engine-set ambient vars (contract not enumerated anywhere): `won, last_damage, healed, given, found_items, natural, total, trade_cash, trade_bought, cart_taken, lost, cleared_ground, outcome_*, focused (entity), max_hp`.
Text: `run.$x`, `self.$x`, `item.$x`, `role.R.$x`, `.name`, `.id`; only usable as line args/keys; **no text comparison, concat, or branching**.

Missing, ranked by pain: (1) `has_status(id)` / `status_turns(id)` / `stacks`; (2) `%` / `abs` / `rand(a,b)` (use `dice(1,n)`); (3) intent-id test (`self.intent == id` / `intent(id)`); (4) `focus.` and `source.`/`attacker.` heads; (5) roster queries (`list_has`, `list_size` only); (6) percent helper (`x*150/100` fine, but no round-to-nearest); (7) `enemies_with_tag(tag)` (use `count_enemies(target.#tag)`); (8) enemy-count by role; (9) `scene` as expression (condition `scene` exists).

Shared effects (PackLoader.cs:442-486): params are ints only (`arg.NAME`), no text/effect params, no return values, recursion cap 32; calls evaluate args then run body with `WithArgs`. This is the only abstraction mechanism (+ `templates`, `interactions` via `use`).

---

### Target: Builtins vocabulary (effects/conditions)
**Rationale:** Know what's in/out.
**Status:** Done
**Findings:**

Conditions: `all any not chance has_tag has_status carrying scene intent_interruptible fits credits_at_least` (+ any Expr string).
Effects: control `if cond check roll repeat once random for_each bind set add set_text`; world `say hint sound event end_event stay next_room retreat leave schedule unschedule wound night_log emit`; cast `cast uncast list_add list_clear muster engage focus pick_enemy`; combat `fight end_fight enemy_turn damage heal status clear_status defeat remove spawn intent`; items `consume reload for_items pick_item equip give_item give_loot credits lose_items clear_ground take_back lose_found`; trade `new_cart trade take_cart`.
Notable absences: `while`/`break`, `enter_room`/`insert_room`/`skip_rooms`, `revive`, `become` (morph), `set_hp`/`add` works on `self.hp` (writes Base), `add_tag`/`remove_tag`, `list_remove`/`list_pick`, `delay`/`after`, `damage` with `type`/`from`/`non_lethal`, `heal` non-clamp, `stack_status`, `extend_status`, `reveal` (hidden enemy), `swap_places`, `give_status_to_all`. `damage` source is always `s.Self` (room/event damage has Hero as source — `damaged.target=Hero`, not "environment").

---

### Target: Render-time evaluation (UI-driven re-evaluation)
**Rationale:** Interaction between Expr RNG and UI polling.
**Status:** Done
**Findings:**

`ExpeditionLocation.Tick` (src/Game/ExpeditionLocation.cs:173-178) calls `Refresh()` when dirty **or every 0.5 s** (`RecheckSeconds` line 17). Refresh → `run.PanelTitle`, `run.Hud()` and `run.Options()` (345) every time. These evaluate `Interaction.When/Where/Label/Preview`, HUD row `When`/lines, `Lines.Render`.
- `Lines.Render` (139-150): a line with `variants:N` calls `Run.Roll(N)` → **label/HUD text with variants re-rolls every refresh (flicker + unseeded RNG consumption)**. Button list rebuilt only when `Group+Label` signature changes (351-353), so a variant label would rebuild buttons every 0.5 s.
- Expr with dice (`2d6`, `dice()`) in a `when`/`where`/`preview`/value used by `Options()` also consumes RNG per refresh.
- NER currently uses `variants` only in `say` log lines (kit.json:70,89, combat.json:344,372), which render once — no live bug, latent trap for new content. `RNG` is the game's (`RNG.GetRandomInt`, ExpeditionLocation.cs:124), not seeded.

---

## Revisit Pass

Re-read whole doc; added targets:

### Target: Ordering/timing semantics worth documenting (cross-check)
**Rationale:** Re-derive timing gotchas from first pass for correctness.
**Status:** Done
**Findings:** Re-traced `EnemyTurn` with a status-applying intent: enemy.Do → `AddStatus(Hero)` (turns T) → … → `TickStatuses(Hero)` T−1 at phase end → `turn_start(Hero)` fires with status still alive if T≥2 ✓ (so T=1 expires before turn_start hooks of Hero → a 1-turn debuff's own `turn_start` hook never fires). Confirmed `Spawn`/re-choose overwrite via lines 580-588 vs 908-917. Confirmed `Engage` accepts `Over`/scene change mid-hooks (569). Confirmed `Defeat(Hero)` always finishes (432-439). Confirmed `Retreat` loses nothing else (515). Confirmed `Offer` Once-per-room only (794). `Perform` `usedThisRoom.Add(i)` for all, harmless.

### Target: C# surface vs data surface parity
**Rationale:** What is mod-only that could be data.
**Status:** Done
**Findings:** C#-only today: custom hooks (`AddHook`), interceptors, custom effects/conditions/decisions, any text compare/status reader. Data-only extension: shared effects, templates, shared interactions. NER proves the data-only route works but caps rules at hooks + armor + flags.

---

## Patterns & History

**Patterns Found:**
- **Subject-scoped hooks + broadcast containers:** container hooks (kit/area/room/encounter) are broadcast, entity-owned (enemy/status) are subject-only. Asymmetry creates the need for `self.#player` guards and blocks observer behaviors.
- **Silent no-op on misuse:** unknown run var write, spawn out of combat, ClearStatus of absent id, heal on gone, division by zero. Loader catches shape errors, runtime swallows semantic ones.
- **Everything is a `Value/Condition/Effect` delegate:** adding a runtime feature is cheap when it fits an existing Emit/Decide site; expensive when it changes ownership (friendly entities, mutable layout).
- **Mod escape hatch before data parity:** features exist first as C# (interceptors, hooks) and the data surface lags.

**Evolution:** Not traced (no git history in the folder: no repo metadata present; `Related` history lives in projex docs only).

---

## Findings

### Discoveries
1. **Interceptors are the biggest data-expressiveness gap** (damage mods, shields, block, crit, loot swap, intent override); mechanism already exists in `Decide()`.
2. **Statuses lack lifecycle hooks and stacking**; adding `on_apply`/`on_expire`/`stacks` unlocks clocks, bombs, DoT scaling, companions leaving.
3. **Observer hooks missing** (`ally_defeated`, `foe_damaged`, Hero status reacting to kills).
4. **Layout is immutable post-`Begin`**; run-shape ideas (Long Night, Dawn Clock, branching, alarm-spawned rooms) need `enter_room`/`insert_room`.
5. **Timers are an illusion**: schedule is a poll table; real timers = statuses.
6. **Silent traps** (list in Summary 6) deserve either fixes or loader/linter warnings.
7. **Render-time RNG** is a latent correctness bug class for new content.
8. **JSON cannot declare hooks**; custom hooks need C#.

### Mental Model
Run = state (`vars`, `texts`, `roles`, `lists`, `schedule`, enemies, Hero) + a small scene machine (`room`/`event`/`combat`/`over`) driven by `Perform`. Content = delegates compiled once. Extension points in order of power: (a) data effects/shared effects; (b) 11 hooks (broadcast + owner-scoped); (c) 7 decisions (C# only); (d) C# effects/conditions/hooks. The engine is one `Emit`/`Decide` call per event, so every new event type is cheap; ownership changes (allies, dynamic layout) are the expensive axis.

### Implications — Expansion Candidates (descriptive, ranked by leverage/cost; not a plan)

| # | Expansion | Leverage | Cost | Touches |
|---|---|---|---|---|
| R1 | Data-defined interceptors: `intercept: {damage: {when, do}}` on status/kit/area/enemy; `decision.value/pierce/text` path + `modify`/`cancel` effects | Very high (shields, vulnerability, block, crit, loot swap, intent override, luck) | Medium | `Decide()` + loader section + Expr head `decision.` + 2 effects |
| R2 | Status upgrades: `on_apply`/`on_expire` hooks, `stacks` (+`add_stack`), tags granted to owner, `clear_status{tag}`, `has_status()`/`status_turns()` Expr fns, tick policy (owner/round), `disables:[group]` | Very high (clocks, bombs, DoT, stun, buffs) | Low–Medium | `ActiveStatus`, `AddStatus`, `TickStatuses`, NextRoom, `Expr.Call` |
| R3 | Hook vocabulary: `room_exit status_added status_expired item_found healed spawned intent_chosen event_start event_end retreated trade_closed check_rolled round_start round_end` | High, cheap | Low (one `Emit` each + names) | HookNames + call sites |
| R4 | Observer hooks: `ally_defeated`, `ally_damaged`, `foe_*`; Hero statuses/kit observing enemy events with `self`=holder | High (Stitcher, morale, enrage, adrenaline) | Low–Medium | `Emit` |
| R5 | Flow: `enter_room{id}`, `insert_room{id,at}`, `skip_rooms`, runtime `run.rooms` mutation, layout resolved after/within `run_start` | High (Long Night, branching, loops, alarm rooms) | Medium | `rooms`, `NextRoom`, `BuildLayout` ordering |
| R6 | Timers: relative `in_rooms`/`in_turns`, `every`, effect timers, multiple per depth, auto-fire on room enter | Medium–High | Medium | `schedule`, EnterRoom, EnemyTurn |
| R7 | Enemy state: tags mutable, `hidden/untargetable` (alive but inactive-for-win), `revive`, `become{enemy}` (morph, keep hp ratio), `Gone` ≠ win option | Medium–High (Lurker, Stitcher, phases) | Low–Medium | `Entity`, win check at 877/918 |
| R8 | Expr additions: `%`, `abs`, `rand(a,b)`, `has_status`, `status_turns`, `intent`, `focus.`/`source.` heads, `list_has`, text equality | Medium (quality of life, removes workarounds) | Low | `Expr.Call`/`Reader` |
| R9 | JSON hook declaration (`hooks:["name"]`) and JSON-declared decisions | Medium (data-only cross-pack events) | Low | `PackLoader.customHooks` |
| R10 | Interaction cooldown/`per_fight`/`per_turn` counters (today `Once` = per room only) | Medium | Low | `usedThisRoom`, `Offer` |
| R11 | Damage type/tag on `damage` effect + `damage` decision text; resist/weakness | Medium | Low–Medium | `Damage`, decision, effect |
| R12 | Friendly/companion entities that act in the enemy phase and can be targeted/killed | High but expensive | High | `Entity.IsPlayer` assumptions, `Offer`, intents, HUD, `Emit` |
| R13 | Hardening: finally-block for `LeaveShared`; reserved-var write warning (loader/lint); `spawn` outside combat warning; render-time RNG isolation (evaluate `Roll` as 1 / deterministic during `Options/Hud`, like `MapContext.Roll`); document ambient vars | Prevents bugs | Low | PackLoader, ExpeditionRun |

---

## Answers

**Q1. What does a run execute and which hooks fire on what?**
See § Run lifecycle and § Hook dispatch: `Begin → run_start → room_enter loop`; in combat per action: body → `turn_end`(Hero) → per enemy `turn_start` → intent → status tick → Hero tick → re-choose intents → `turn_start`(Hero) → `after_action`. Hooks dispatch kits → area → room → encounter → enemy def → statuses → C# listeners; only the first four are broadcast.

**Q2. Where does the runtime constrain JSON packs?**
Decisions (C#-only), no stacks/expiry hooks/status readers, no observer hooks, frozen layout, passive timers, no friendly entities, int-only Expr without status/intent readers, no custom hooks from JSON.

**Q3. Cheap vs expensive expansions?**
Cheap: R3, R8, R9, R10, R13 (one-liners at existing sites). Medium: R1, R2, R4, R5, R6, R7, R11. Expensive: R12. Highest leverage per cost: R2 + R1 + R3.

**Q4. Verdict on the prior eval's unverified capabilities (runtime-related):**
- Status `on` hooks on `room_enter`/`damaged` — **True** for Hero statuses (subject Hero) and for the owner on `damaged`; caveat `rooms:N` yields N−1 `room_enter`s; status cannot react to *other* entities' events.
- `damaged` hook on enemy with `self`=enemy (two-phase bosses) — **True**; can even prevent lethal by raising hp inside the hook.
- Spawn by id — **True, combat-only** (no-op otherwise); opening intent lost when spawned mid enemy-phase.
- Remove + schedule for hidden enemies (Lurker) — **Partially false**: `remove` is irreversible and last-active removal **wins** the fight; scheduled event can re-spawn a *new* entity (state lost) but needs a polling `event:{scheduled}` in a room hook and a fight that stays alive.
- Enemy intents healing allies (Stitcher) — **True** via `pick_enemy`/`for_each` + `heal{to:target}`; no resurrection.
- `item_stats` arbitrary numeric props via `best_item` — **True** at runtime (`ReadItem` → `Props`); loader-side details are a sibling's question.
- Not runtime-answerable here (loader/game side): third intel card, `unlocked_by` list, per-area `room_table` override, pure-buy trade, new item ids/crafting.

---

## Open Questions

- [ ] Does any NER content rely on a reserved `run.*` write being honoured (`set run.turn` etc.)? (Not grepped exhaustively.)
- [ ] Sim harness (`tests/Sim/Program.cs`) behaviour for Hud/Options render RNG and for hooks — sibling (tooling) scope.
- [ ] Was the `heal`-on-`Defeated` behaviour intended? (No test in repo observed.)
- [ ] Should `Gone` count as win when the last active enemy is merely hidden? Needs maintainer intent (NER relies on win-on-flee).
- [ ] Behaviour when `run_start` hook calls `Finish` (Begin returns early at 270) — no issue seen, untested.
- [ ] No build/run in this session: every behavioural claim is from reading code, none from execution.

---

## Appendix

**Sources:** `src/Core/ExpeditionRun.cs`, `ExpeditionRun.Api.cs`, `Expr.cs`, `Builtins.cs`, `Model.cs`; `api/ExpeditionApi.cs`; `src/Core/PackLoader.cs` (lines 16-90, 440-520, 985-1045); `src/Game/ExpeditionLocation.cs` (140-184, 281-424), `GameBridge.cs` (150-198); NER `ExpeditionFramework/{combat,kit,rules,events,areas}.json` (spot checks), `src/ModEntry.cs`; NER `.projex/2611120240-content-expansion-synthesis-eval.md`.
**Limitations:** Static reading only; no compile/run; PackLoader read selectively (sibling covers it); Sim and persistence not examined; NER JSON checked by grep, not end-to-end.
