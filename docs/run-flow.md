# How a run plays

What happens, in order, from the moment the player arrives until they leave. The engine is
[src/Core/ExpeditionRun.cs](../src/Core/ExpeditionRun.cs).

## Start

1. The area's `vars` are set, then the player's stats from `player`. `max_hp` is set to `hp`.
2. The [layout](areas.md#layout) picks the run's rooms.
3. The `run_start` hook fires.
4. The player enters the `entrance` room if the area has one, otherwise the first layout room.

## Rooms

Entering a room:

1. The room's `text` is logged as a heading.
2. Its `enter` effects run.
3. The `room_enter` hook fires.

`run.depth` counts the layout rooms entered so far (the entrance doesn't count), `run.rooms` is the layout's length and
`run.rooms_left` is the difference. Moving on (`next_room`, the explore kit's "Press on") enters the next layout room;
from the last one, the run ends `cleared`. `retreat` steps the depth back, so moving on enters the same room again.

Each time the player moves on to the next room, statuses on the player that last a number of `rooms` count down.

## Scenes

At any moment the run is in one **scene**: `room`, `combat`, an event's scene (`event` unless the event names its own),
or `over`. An action is offered only in the scenes its `in` lists. See [Actions](actions.md#where-and-when).

## The player's turn

The actions window offers, in this order of sources: the open event's choices, the area's kits, the area, the room, the
player's statuses, then each enemy present and that enemy's statuses. Each source's actions are filtered by scene,
`once`, `targets`, `uses` and `when`.

When the player presses a button:

1. A `once` action is marked used for this room.
2. Items named by `uses` with `consume` are used up, and the action's `sound` plays.
3. The action's effects run: `do`, then `check`, then `after`.
4. If it was a choice of the open event, and nothing moved the run (no new event, fight or room, no `end_event`), and
   neither the `stay` effect nor the action's `stay` flag kept it open, the event ends and the scene returns to `room`.
5. If the action started in a fight, the fight is still on, and it takes a turn (`turn` is not `false`), the enemies
   take their turn. If the fight has no enemies left, it is won.
6. If it takes a turn, the `after_action` hook fires.

## Fights

`fight` picks an encounter and starts it; `muster` and `engage` do the same in two steps. Starting a fight:

1. The scene becomes `combat` and `run.turn` is 1.
2. Each enemy picks its first intent: its `opening` if the fight started `surprised`, otherwise one from its
   `behavior`.
3. The first enemy is focused. `combat_start` fires.
4. Each enemy's intent `tell` is logged.

### The enemies' turn

1. `turn_end` fires for the player.
2. Each enemy still standing, in order: `turn_start` fires with that enemy as self, it carries out its intent's `do`,
   and its statuses that last `turns` count down.
3. The player's statuses that last `turns` count down.
4. Each enemy picks its next intent: its last intent's `next` if it has one, otherwise one from its behavior. A status
   with an `intent` forces that intent instead. An intent set during the enemies' turn (by an `intent` effect) is kept.
5. Each new intent's `tell` is logged.
6. If no enemy is left, the fight is won. Otherwise `run.turn` goes up by one and `turn_start` fires for the player.

### Damage and defeat

`damage` passes its amount to C# interceptors, takes off the target's `armor` unless `"armor": "ignore"`, and lowers
`hp`. `run.last_damage` holds what was dealt, and `damaged` fires with the victim as self and the source as target.

At 0 hp or below, an enemy is defeated (`defeated` fires) and the player is knocked out (`knocked_out` fires, then the
run ends `knocked_out`). An enemy taken away with `remove` is gone without being defeated.

### Ending a fight

A fight ends when every enemy is defeated or gone after the player's action or the enemies' turn, or with `end_fight`.
Then the scene returns to `room`, `run.won` is 1 or 0, `combat_end` fires, the encounter's own `combat_end` hook runs,
and if the fight was won the `won` effects of the `fight` that started it run.

## Ending a run

A run ends one of three ways:

| Outcome | How |
|---|---|
| `cleared` | Moving on from the last room, or `{ "leave": "cleared" }` |
| `knocked_out` | The player's hp reaches 0 |
| anything else | `{ "leave": "NAME" }`. The explore kit's "Head home" uses `left` |

When it ends, `run.outcome_NAME` is set to 1 and `run_end` fires. The actions window then shows one button to go home,
labelled with the area's `ef_leave_NAME` text if it has one, otherwise `ef_leave`. The run is counted as a clear of the
area (for `game.clears` and `unlocked_by`) when the player leaves after outcome `cleared`, or with `run.cleared` above 0.

## Hooks

Hooks are effects under `on` that run when something happens. Rooms, enemies, encounters, statuses, kits and areas can
all carry them.

| Hook | When | self | target |
|---|---|---|---|
| `run_start` | The run begins, before the first room | player | — |
| `room_enter` | After a room's `enter` effects | player | — |
| `combat_start` | A fight starts, after openings are chosen | player | — |
| `turn_end` | The player's action in a fight is done, before the enemies act | player | — |
| `turn_start` | Before each enemy acts; and for the player when a new turn begins | that enemy, or player | player, or — |
| `damaged` | Someone took damage | the victim | the source |
| `defeated` | An enemy reached 0 hp | the enemy | who defeated it |
| `knocked_out` | The player reached 0 hp | player | who did it |
| `after_action` | After any player action that takes a turn | player | — |
| `combat_end` | A fight ended | player | — |
| `run_end` | The run ended; `run.outcome_NAME` is set | player | — |

When a hook fires, these run in order: the area's kits, the area, the current room, the current encounter, the enemy
the hook is about (its own `on`), that one's statuses, then C# listeners. An enemy's `on.defeated` therefore runs only
for that enemy; a kit's `on.defeated` runs for every enemy.

C# mods can declare more hooks, named `pack:name`, and data can fire any hook with the [`emit`](effects.md#world)
effect. See [C# API](csharp-api.md).
