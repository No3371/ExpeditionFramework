# Rooms and events

## Rooms

A room is one step of a run. The [layout](areas.md#layout) decides which rooms a run visits.

```json
"rooms": {
  "pump_room": {
    "tags": ["cellar"],
    "text": "sa_room_pump",
    "enter": [ { "fight": { "tag": "cellar" } } ],
    "interactions": [
      { "id": "search_pump", "label": "sa_action_search", "group": "explore", "once": true,
        "check": { "chance": "60", "success": [ { "give_loot": "cellar_scraps" } ], "failure": [ { "say": "sa_nothing" } ] } }
    ]
  }
}
```

| Field | What it does |
|---|---|
| `text` | A [line](text.md#lines) logged as a heading when the player enters |
| `enter` | [Effects](effects.md) run on entering, after the text |
| `interactions` | [Actions](actions.md) offered while in this room. Default scene: `room` |
| `on` | [Hooks](run-flow.md#hooks) that fire while the player is in this room |
| `tags` | Names a layout slot or effect can pick the room by |
| `weight` | How likely it is to be picked, against the other candidates. Default 1 |
| `when` | A condition; the room can only be picked while it holds |
| `extends` | A [template](packs.md#templates) |

`when` is checked when the layout is built, at the start of the run, so it can read `memory.`, `game.` and the area's
`vars`.

## Events

An event is a scene with its own choices: a stranger, a locked crate, a trader. Effects start events; rooms don't
contain them.

```json
"events": {
  "locked_crate": {
    "tags": ["cellar"],
    "text": "sa_event_crate",
    "choices": [
      { "id": "pry", "label": "sa_choice_pry", "group": "tactics",
        "uses": { "select": ["crowbar", "screwdriver"], "each": false },
        "check": { "chance": "80",
          "success": [ { "say": { "text": "sa_pried", "tone": "good" } }, { "give_loot": "cellar_scraps" } ],
          "failure": [ { "say": { "text": "sa_pry_noise", "tone": "bad" } }, { "fight": { "tag": "cellar" } } ] } },
      { "id": "leave", "label": "sa_choice_leave_crate", "group": "move", "do": [ { "say": "sa_ignored" } ] }
    ]
  }
}
```

| Field | What it does |
|---|---|
| `text` | A line logged when the event starts |
| `enter` | Effects run when the event starts, after the text |
| `choices` | [Actions](actions.md) offered while the event is open. Default scene: the event's scene |
| `scene` | The scene's name while this event is open. Default `event` |
| `tags`, `weight`, `when` | How [effects pick the event](#starting-an-event) |
| `extends` | A [template](packs.md#templates) |

An event needs `choices` or `enter` effects. One without choices should move the run on from `enter`, or the player is
left with only the area's and kits' actions.

### Starting an event

```json
{ "event": "locked_crate" }
{ "event": { "tag": "cellar" } }
{ "event": { "from": ["stranger", "locked_crate"] } }
{ "event": { "pool": "rumoured" } }
{ "event": { "scheduled": "run.depth" } }
```

The first four pick by `weight` among candidates whose `when` holds; if none does, nothing happens. The last starts the
event [scheduled](#scheduling-events) at that depth, if any.

### Ending an event

After a choice, the event ends and the scene returns to `room`, unless:

- the choice moved the run on: started another event or a fight, entered a room, or ended the event itself;
- the choice ran the `stay` effect, or has `"stay": true`. Use this for choices that can be made more than once, such as
  haggling before a trade.

`end_event` closes the open event from any effect.

### Event scenes

An event with `"scene": "standoff"` puts the run in the scene `standoff` while it is open. Its choices default to that
scene, and any other action can opt in with `"in": ["standoff"]`. A HUD can list it in
[`pending_scenes`](areas.md#hud) to show enemies present before a fight starts. Naming a scene in `in` that no event
declares is a problem.

## Scheduling events

`schedule` puts an event at a future depth: when the player reaches that room, an effect there can start it. Only one
event can wait at each depth.

```json
{ "schedule": { "tag": "rumour", "at": "run.depth + 2", "then": [ { "say": { "text": "my_rumour", "args": ["run.$scheduled"] } } ] } }
```

Picks an event like `event` does and puts it at depth `at`, unless one is already waiting there. `run.$scheduled` is
the id (without its pack) of the event waiting at `at`, and `then` runs afterwards.

```json
{ "schedule": { "intel": "rooms", "depths": "run.rooms" } }
```

Puts each event today's [intel](intel.md) slot `rooms` named at a different random depth from 1 to `depths`.

Then, in the rooms:

```json
"enter": [ { "if": { "when": "scheduled(run.depth)", "then": [ { "event": { "scheduled": "run.depth" } } ] } } ]
```

`scheduled(depth)` is 1 when something waits at that depth, and `run.scheduled` counts what waits. `unschedule` removes
what waits at a depth: `{ "unschedule": "run.depth" }`. A text argument `{ "scheduled": "key_{run.$pick}" }` lists
every waiting event, each through that text key with `run.$pick` set to its id.
