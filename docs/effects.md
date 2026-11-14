# Effects

An effect does something: logs a line, starts a fight, gives an item. Effects go in `do`, `enter`, `on` hooks, check
outcomes and many other places. Every built-in is in [src/Core/Builtins.cs](../src/Core/Builtins.cs).

Most packs get far with a handful:

- `say` logs a line; `if` and `check` branch ([Control](#control), [Check](#check));
- `set` and `add` change a number, such as `run.heat` or `memory.visits` ([Control](#control));
- `fight` and `event` start a fight or an event; `next_room` and `leave` move on or end the run ([World](#world),
  [Combat](#combat));
- `give_loot`, `give_item` and `credits` reward the player ([Items](#items)).

The rest of the page lists every effect, for when you need one of the others.

## Writing effects

An effect is an object with one key, the effect's name. A list of effects runs in order, and stops if the run ends.

```json
[
  { "say": { "text": "sa_found_cash", "tone": "good" } },
  { "credits": "2d20 + 10" },
  { "next_room": true }
]
```

When the value isn't an object, it is the effect's `value`: `{ "credits": "2d20" }` is `{ "credits": { "value": "2d20" } }`.
An effect that takes no arguments is written with `true`: `{ "next_room": true }`.

An argument the effect doesn't use is a warning, so a misspelling is caught. An unknown effect name is a problem.

Your pack's [shared effects](packs.md#shared-pieces) are used the same way, by name, and win over a built-in of the
same name. C# mods add effects named `pack:name`.

**Who** arguments (`to`, `who`, `drops_of`) take `self`, `target`, `player` or `role.NAME`.

**Picking** arguments, for effects that pick an event, encounter or enemy, take one of:

| Form | Picks from |
|---|---|
| `"foreman"` (the value) | That one |
| `{ "from": ["a", "b"] }` | Those |
| `{ "tag": "cellar" }` | Every one with that tag, from any pack |
| `{ "pool": "bosses" }` | The area's [pool](areas.md#pools) of that name |

Among those whose `when` holds, one is picked by `weight`. If none qualifies, nothing happens.

## Control

| Effect | What it does |
|---|---|
| `{ "if": { "when": c, "then": [...], "else": [...] } }` | Runs `then` if the [condition](expressions.md#conditions) holds, else `else` |
| `{ "cond": [ { "when": c, "do": [...] }, ... ] }` | Runs the first entry whose `when` holds. An entry without `when` always does |
| `{ "check": { ... } }` | A roll with `success` and `failure` effects. [Below](#check) |
| `{ "roll": { "die": 20, "mod": "player.str", "do": [...] } }` | Rolls the die (default 20), sets `run.natural` and `run.total`, logs the roll unless `"note": false`, then runs `do` |
| `{ "repeat": { "count": "1d3", "do": [...] } }` | Runs `do` that many times |
| `{ "once": { "do": [...], "else": [...] } }` | Runs `do` the first time this effect runs in a run, `else` after. `key` shares the count between places |
| `{ "random": [ { "weight": 3, "do": [...] }, ... ] }` | Runs one entry, picked by `weight` (default 1) |
| `{ "for_each": { "in": "enemies", "where": c, "do": [...] } }` | Runs `do` once per member, each as `target`. `in`: `enemies` (default), `others` (enemies but `self`), or a list name |
| `{ "bind": { "self": "target", "target": "player", "do": [...] } }` | Runs `do` with `self` and/or `target` changed |
| `{ "set": { "var": "run.heat", "value": 0 } }` | Sets a number. [What can be set](expressions.md#paths) |
| `{ "add": { "var": "memory.visits", "value": 1 } }` | Adds to a number (a negative `value` subtracts) |
| `{ "set_text": { "var": "run.$who", "value": "target.name" } }` | Sets a text variable from a text path, or from a line: `"line": { "text": ..., "args": [...] }` |

### Check

```json
{ "check": { "chance": "60", "success": [...], "failure": [...] } }
{ "check": { "die": 20, "mod": "player.charm", "dc": 12, "fail_at": 1, "pass_at": 20, "success": [...], "failure": [...] } }
{ "check": { "roll": "2d6 + player.str", "dc": 9, "success": [...], "failure": [...] } }
```

- **chance**: succeeds if a d100 roll is at most `chance` (kept between 0 and 100).
- **die**: rolls a `die`-sided die (default 20). It succeeds when the natural roll plus `mod` is at least `dc`, or the
  natural roll is at least `pass_at`, but never when the natural roll is at or below `fail_at` (default 0). `pass_at`
  defaults to the die's size, so a natural 20 always passes. Sets `run.natural` and `run.total`. The area's
  [luck](areas.md#luck) bends d20 rolls.
- **roll**: succeeds if the `roll` expression is at least `dc`.

Every roll is noted in the log. On a button, `chance` and `die` checks show their odds; see
[Actions: checks](actions.md#checks).

## World

| Effect | What it does |
|---|---|
| `{ "say": "key" }` or `{ "say": { "text": "key", "args": [...], "tone": "good" } }` | Logs a [line](text.md#lines). Tones: `good`, `bad`, `dim`, `heading`, `enemy`, `hint`, `plain` (default) |
| `{ "hint": { "text": "key", "missing": filter } }` | Logs a hint, once per run per text, and only while no carried item matches `missing` |
| `{ "sound": "id" }` | Plays a game sound |
| `{ "event": ... }` | Starts an event. [Starting an event](rooms-events.md#starting-an-event) |
| `{ "end_event": true }` | Closes the open event |
| `{ "stay": true }` | Keeps the open event open after this choice |
| `{ "next_room": true }` | Moves on to the next layout room, or ends the run `cleared` after the last |
| `{ "retreat": true }` | Steps the depth back one, so moving on enters the same room again |
| `{ "leave": "cleared" }` | Ends the run with that outcome. Default `left` |
| `{ "schedule": ... }`, `{ "unschedule": "run.depth" }` | Puts an event at a future depth, or removes one. [Scheduling](rooms-events.md#scheduling-events) |
| `{ "emit": { "value": "my_hook", "who": "self", "other": "target" } }` | Fires a [hook](run-flow.md#hooks): `who` (default `self`) is its self, `other` (default `target`) its target |
| `{ "wound": "major" }` | Gives the player a game wound: `major`, or anything else for minor |
| `{ "reputation": { ... } }` | Changes reputation with a faction. [Game reads and reputation](game.md#the-reputation-effect) |
| `{ "night_log": { "text": "key", "args": [...], "color": "5c6f46" } }` | Writes a line to the game's night log, the summary shown when the night ends |

`night_log` takes the line the same way `say` does, with an optional `color` (a hex colour without `#`).

## Story

| Effect | What it does |
|---|---|
| `{ "cast": { "as": "rival", "tag": "rivals" } }` | Casts an enemy into a role. [Roles and lists](combat.md#roles-and-lists) |
| `{ "uncast": "rival" }` | Clears a role |
| `{ "list_add": { "list": "witnesses", "who": "target" } }` | Puts `who` (default `self`) on a list |
| `{ "list_clear": "witnesses" }` | Empties a list |
| `{ "muster": ... }`, `{ "engage": ... }` | Puts enemies in the room, then starts the fight. [Encounters](combat.md#encounters) |
| `{ "focus": { "who": "target" } }` | Focuses that enemy |
| `{ "pick_enemy": { "where": c, "order": "target.hp", "max": 1, "do": [...], "else": [...] } }` | Runs `do` for up to `max` (default 1) enemies that pass `where`, lowest `order` first, each as `target`; `else` if none |

## Combat

| Effect | What it does |
|---|---|
| `{ "fight": ... }` | Starts a fight. [Encounters](combat.md#encounters) |
| `{ "end_fight": { "won": 1 } }` | Ends the fight, won (default) or not |
| `{ "enemy_turn": true }` | Gives the enemies a turn now |
| `{ "damage": { "to": "target", "amount": "1d6", "armor": "ignore", "hit": { "soft": 1 } } }` | Deals damage to `to` (default `target`), less its `armor` unless `"armor": "ignore"`. `hit` names numbers (name `a-z`, `0-9` or `_`, value an expression) that the hooks this damage sets off can read as `hit.NAME`. [Damage and defeat](run-flow.md#damage-and-defeat) |
| `{ "heal": { "to": "player", "amount": "1d4" } }` | Restores hp, up to `max_hp`, to `to` (default `player`). Sets `run.healed` |
| `{ "status": { "to": "target", "id": "blinded", "turns": 2, "rooms": 1 } }` | Gives a status. `turns` and `rooms` override its own lengths |
| `{ "clear_status": { "to": "player", "id": "bleeding" } }` | Takes a status away |
| `{ "defeat": { "who": "target" } }` | Defeats an enemy outright, as if its hp ran out |
| `{ "remove": { "who": "self" } }` | Takes `who` (default `self`) out of the fight without defeating it, such as an enemy fleeing |
| `{ "spawn": { "tag": "rats", "surprised": 0, "then": [...], "else": [...] } }` | Adds an enemy to the fight, chosen at random from those named; `then` runs with it as `target`. Only in a fight; otherwise `else` |
| `{ "intent": { "who": "target", "id": "expedition_framework:staggered" } }` | Sets an enemy's next intent |

`spawn` and `cast` name enemies by `enemy` (one id or a list), `tag`, `from`, `pool`, `role`, `intel` (today's intel
picks) or `encounters` (the single enemy of each one-enemy encounter in a [pool](areas.md#pools)).

## Items

See [Items, loot and trade](items.md).

| Effect | What it does |
|---|---|
| `{ "consume": 1 }`, `{ "consume": { "what": "with", "count": 2 } }` | Uses up the action's `item` (or `with`) |
| `{ "reload": true }` | Loads the `item` gun from the `with` ammo. [Reloading](items.md#reloading) |
| `{ "for_items": { "select": filter, "do": [...] } }` | Runs `do` once per matching carried item, as `item` |
| `{ "pick_item": { "select": filter, "order": expr, "do": [...], "else": [...] } }` | Runs `do` with the first match as `item` (`"as": "with"`: as `with`), or `else` |
| `{ "equip": { "select": filter, "then": [...] } }` | Moves the first match that fits into an empty hand slot, then runs `then` with it |
| `{ "give_item": "crowbar" }` | Gives an item by game id |
| `{ "give_loot": "table" }` | Rolls a loot table and gives the item. [Loot tables](items.md#loot-tables) |
| `{ "credits": "2d20" }` | Gives credits; negative takes them, if the player has enough |
| `{ "lose_items": 50 }` | Destroys carried items by chance. [Credits and losing things](items.md#credits-and-losing-things) |
| `{ "lose_found": true }`, `{ "take_back": {...} }`, `{ "clear_ground": true }` | Destroy found items |

`for_items`, `pick_item` and `equip` take the same keys as an [item use](actions.md#items) (`select`, `order`,
`limit`), or a plain item filter.

## Trade

| Effect | What it does |
|---|---|
| `{ "new_cart": "fence_cart" }` | Stocks a cart |
| `{ "trade": { "cart": "fence_cart", "on_close": [...] } }` | Opens the trade screen |
| `{ "take_cart": { "cart": "fence_cart", "do": [...] } }` | Hands over what is left on the cart |

See [Trading](items.md#trading).
