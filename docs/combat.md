# Combat

Fights are built from **encounters** (who shows up), **enemies** (their stats and what can be done to them),
**intents** (what each is about to do) and **statuses** (effects that last). The order a fight plays in is on
[How a run plays](run-flow.md#fights).

## Encounters

```json
"encounters": {
  "rats": { "tags": ["cellar"], "weight": 3, "enemies": [ { "id": "rat", "count": 2 } ] },
  "scrappers": { "tags": ["cellar"], "weight": 2, "enemies": ["scrapper"], "when": "run.depth >= 2" },
  "foreman": { "enemies": ["foreman", "rat"] }
}
```

| Field | What it does |
|---|---|
| `enemies` | Enemy ids, or `{ "id": ..., "count": n }`. Required |
| `on` | [Hooks](run-flow.md#hooks) that fire while this fight is on. Its `combat_end` runs after the fight ends |
| `tags`, `weight`, `when` | How effects pick the encounter |

Start one with `fight`:

```json
{ "fight": "foreman" }
{ "fight": { "tag": "cellar", "won": [ { "credits": "2d6" } ] } }
{ "fight": { "from": ["rats", "scrappers"], "surprised": "run.sneaking" } }
```

The encounter is picked by `weight` among candidates whose `when` holds (`from`, `tag`, `pool`, or a plain id). `won`
runs if the player wins. With `surprised` non-zero, each enemy opens with its `opening` intent.

To put enemies in the room without fighting yet (a standoff, a conversation), use `muster`, then `engage` later:

```json
{ "muster": { "tag": "guards" } }
{ "engage": { "won": [ ... ], "prepare": [ { "status": { "id": "wary" } } ] } }
```

`muster` takes an encounter like `fight` does, or `enemy` (ids) or `role` (role names) for specific enemies. `add: true`
adds to whoever is there instead of replacing them, and `do` runs once per arrival with it as `target`. `engage` turns
whoever is present into a fight; `prepare` runs once per enemy, with it as `target`, before they pick their openings.

## Enemies

```json
"enemies": {
  "foreman": {
    "name": "sa_enemy_foreman",
    "tags": ["human", "boss"],
    "stats": { "hp": 16, "attack": 3, "accuracy": 5, "dodge": 0, "armor": 1 },
    "behavior": "foreman",
    "interactions": [ ... ],
    "on": { "defeated": [ { "credits": "2d20 + 10" } ] }
  }
}
```

| Field | What it does |
|---|---|
| `name` | Text key for its name. Required |
| `stats` | Expressions, worked out when it is created, with the enemy as `self` and the player as `target`. `hp` is required; `max_hp` is set to `hp`. Any other name is a stat |
| `behavior` | A list of [behavior entries](#behaviors), or the name of a shared behavior. Needs at least one intent |
| `opening` | An intent it starts with when the fight starts `surprised` |
| `drops` | A loot table, rolled by `{ "give_loot": { "drops_of": "target" } }` |
| `texts` | Starting text variables, read as `self.$NAME`: `{ "texts": { "title": "the boss" } }` |
| `interactions` | [Actions](actions.md) offered against this enemy while it is present. Default scene: `combat` |
| `on` | Hooks that fire when the hook is about this enemy: its `damaged`, `defeated`, `turn_start` |
| `tags`, `weight`, `when` | Tags read as `self.#tag` and by `has_tag`; weight and when for effects that pick an enemy |

The engine reads only a few stats itself: `hp`, `max_hp`, and `armor` (taken off damage). `focused` is 1 on the focused
enemy. Everything else (`attack`, `dodge`, `accuracy`) means what your data makes it mean.

### Actions on an enemy

An enemy has no weak-point field. Instead, an action in the enemy's `interactions` says what can be done to it, with
what, and what happens:

```json
{
  "id": "blast_foreman",
  "label": "sa_action_blast",
  "group": "tactics",
  "uses": { "select": "#explosive", "consume": 1 },
  "check": {
    "chance": "75",
    "success": [ { "damage": { "amount": "3d6 + 4", "armor": "ignore" } } ],
    "failure": [ { "damage": { "to": "player", "amount": "1d4", "armor": "ignore" } } ]
  }
}
```

The button appears once per matching explosive while the foreman is present. Inside, `target` is the foreman and
`item` the explosive. See [Actions](actions.md).

## Intents

An intent is what an enemy will do on its turn. The player sees its `label` in the HUD before it happens.

```json
"intents": {
  "wind_up": {
    "label": "ef_intent_wind_up",
    "tell": { "text": "ef_tell_wind_up", "args": ["self.name"] },
    "tone": "bad",
    "interruptible": true,
    "next": "heavy_strike",
    "do": [ { "say": { "text": "ef_do_wind_up", "args": ["self.name"], "tone": "bad" } } ]
  }
}
```

| Field | What it does |
|---|---|
| `label` | A line naming the move, shown under the enemy in the HUD. Required |
| `tell` | A line logged when the enemy picks this intent |
| `tone` | Colour of the label and tell. Default `enemy` |
| `do` | Effects run on the enemy's turn, with the enemy as `self` and the player as `target` |
| `next` | The intent it moves to next, instead of picking from its behavior |
| `interruptible` | Read by the `intent_interruptible` condition, so an action can offer to break it |

An action can change an enemy's next move with `{ "intent": { "who": "target", "id": "expedition_framework:staggered" } }`.

## Behaviors

A behavior is a weighted list of intents. Each enemy turn, one entry is picked by `weight` among those whose `when` holds
(with the enemy as `self`). If none holds, the first is used.

```json
"behaviors": {
  "foreman": [
    { "intent": "expedition_framework:strike", "weight": 50 },
    { "intent": "expedition_framework:wind_up", "weight": 30 },
    { "intent": "call_help", "weight": 20, "when": "run.called_help == 0" }
  ]
}
```

An entry may name a nested behavior instead of an intent: `{ "behavior": "coward", "weight": 30 }`, or an inline list.
An enemy's `behavior` can be the list itself or a shared behavior's name. Ids inside a shared behavior belong to the pack
that wrote it.

## Statuses

A status is something that lasts on the player or an enemy: bleeding, blinded, spotted.

```json
"statuses": {
  "blinded": {
    "name": "sa_status_blinded",
    "turns": 2,
    "on": { "turn_start": [ { "intent": { "who": "self", "id": "expedition_framework:staggered" } } ] }
  },
  "spotted": { "name": "sm_status_spotted", "rooms": 2, "stats": { "dodge": -5 } }
}
```

| Field | What it does |
|---|---|
| `name` | Text key. Required |
| `turns` | Fight turns it lasts. Left out: no limit |
| `rooms` | Rooms it lasts on the player, counted down each time they move on. Left out: no limit |
| `stats` | Numbers added to the owner's stats while it lasts. `self.dodge` reads the total; `set` and `add` change the base |
| `intent` | While it lasts, the owner does this intent instead of picking one |
| `interactions` | Actions offered while someone has it. Default scene: `combat` |
| `on` | Hooks that fire when the hook is about the one who has it |

```json
{ "status": { "to": "target", "id": "blinded" } }
{ "status": { "to": "player", "id": "spotted", "rooms": 3 } }
{ "clear_status": { "to": "player", "id": "bleeding" } }
```

Giving a status someone already has resets its length. `has_status` tests for one.

## Focus and targets

In a fight one enemy is **focused**: the first one at the start, the next one standing when it falls. Actions with
`"targets": "enemy"` give one button per enemy; `"targets": "focus"` acts on the focused one only. `focus` changes it:
`{ "focus": { "who": "target" } }`.

`pick_enemy`, `for_each` and the `*_enemies` [functions](expressions.md#functions) work across everyone in the fight.

## Roles and lists

For a story that follows one person across rooms, cast them into a **role**:

```json
{ "cast": { "as": "rival", "tag": "rivals" } }
{ "say": { "text": "my_rival_returns", "args": ["role.rival.name"] } }
{ "muster": { "role": "rival" } }
```

`cast` makes a new enemy, picked like `spawn` picks (`enemy`, `tag`, `from`, `pool`, `intel`, `encounters`), keeps it
under that name for the rest of the run, and leaves it out of the room until mustered. Paths read it as
`role.NAME.STAT`. Options: `where` (a condition with the candidate as `target`), `except` (a role whose enemy can't be
picked), `fallback` (an enemy id if nothing qualifies), and `from_list` with `take` to pick from a list instead.
`uncast` clears a role.

`muster` and `spawn` with `role` bring in a new enemy of the same kind as the role's, freshly made from its definition;
what you stored on the role (`role.rival.hp`, `role.rival.$title`) stays on the role.

A **list** is a named group of people for the run: `list_add` puts someone on it (`{ "list_add": { "list": "witnesses",
"who": "target" } }`), `list_clear` empties it, `list_size(witnesses)` counts it, `for_each` with `"in": "witnesses"`
goes through it, and the text argument `{ "list": "witnesses" }` names them all.
