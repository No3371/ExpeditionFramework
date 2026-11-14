# Expressions and conditions

Any field that takes a number takes an **expression**: a JSON number, `true`/`false`, or a string such as
`"60 + item.accuracy * 5 - target.dodge"`. Any field that takes a yes/no takes a **condition**. The parser is
[src/Core/Expr.cs](../src/Core/Expr.cs).

What most packs use:

- dice: `"2d6 + 1"` ([Operators](#operators));
- reading a value: `player.hp`, `target.hp`, `item.sides`, `run.heat`, `memory.visits` ([Paths](#paths));
- a comparison as a condition: `"run.heat >= 3 && player.hp > 5"` ([Conditions](#conditions));
- counting what the player carries: `carried(crowbar)`, `carried(#medicine)` ([Functions](#functions)).

The rest of the page is the full list.

## Numbers

Everything is a whole number. `true` is 1 and `false` is 0; a condition is true when its value isn't 0. A JSON number
with a fraction is cut to a whole number (`2.7` is 2). Division drops the remainder (`7 / 2` is 3), and dividing by 0
gives 0.

## Operators

From loosest to tightest:

| Operators | Meaning |
|---|---|
| `a ? b : c` | `b` if `a` isn't 0, else `c` |
| `\|\|` | Or |
| `&&` | And |
| `<` `<=` `>` `>=` `==` `!=` | Compare, giving 1 or 0. One per group: write `a < b && b < c`, not `a < b < c` |
| `+` `-` | Add, subtract |
| `*` `/` | Multiply, divide |
| `-a` `!a` | Negate; not (1 if `a` is 0, else 0) |

Parentheses group as usual.

**Dice:** `2d6` rolls two six-sided dice and adds them; `d20` is `1d20`. For a count or size that is itself an
expression, use `dice(count, sides)`.

## Paths

A path reads a value. It starts with one of these heads:

| Path | Reads |
|---|---|
| `self.STAT`, `target.STAT`, `player.STAT` | A stat of who the effect is about. Statuses' `stats` are included |
| `self.#TAG` | 1 if they have the tag |
| `item.NAME`, `with.NAME` | A number on the action's item or paired item. [What items have](items.md#what-the-game-reports) |
| `item.#TAG` | 1 if the item has the tag |
| `run.NAME` | A [run variable](#run-variables) |
| `role.NAME.STAT` | A stat of the enemy cast in a [role](combat.md#roles-and-lists) |
| `memory.NAME` | Your pack's [memory](memory.md), kept across runs |
| `memory.PACK.NAME` | Another pack's memory, read-only |
| `game.NAME` | The game's state: day, luck, reputation and more. [Game reads](game.md) |
| `arg.NAME` | An argument of a [shared effect](packs.md#shared-pieces), or a value the framework passes (such as `arg.percent`). `arg.$NAME` is not a path: text parameters are written `{arg.$NAME}` and filled in when the pack loads (see [packs.md](packs.md#text-parameters)) |
| `hit.NAME` | The `hit` value NAME of the damage in progress, 0 outside one. [Damage and defeat](run-flow.md#damage-and-defeat) |
| `val.NAME` | A named value from the pack's `values` section |

Who `self` and `target` are depends on where the expression is: see the hooks table in
[How a run plays](run-flow.md#hooks), and [Actions](actions.md#fields).

A name nothing has set reads 0. A head not in this list is a problem. `game.rep.NAME` with a faction the game doesn't
have is a problem too.

`set` and `add` write to the same paths, except `memory.PACK.NAME`, `game.`, `arg.`, `hit.` and `val.`, and the run variables
the engine keeps. `set` and `add` on a stat change its base; `self.STAT` still adds the statuses on top.

### Named values

`values` names expressions you use in several places:

```json
"values": {
  "haggle_dc": "12 + run.heat / 2",
  "fence_markup": "game.rep.black_market >= 20 ? 10 : 30"
}
```

`val.haggle_dc` is worked out wherever it is read, with that place's `self`, `target` and `item`. A value may use other
values, but not itself.

## Functions

| Function | Gives |
|---|---|
| `carried(crowbar)` | How many of that item are carried |
| `carried(#firearm+in_hand)` | How many carried items have every one of those tags |
| `best_item(#melee, item.sides, 2)` | The highest `item.sides` among matching items, or 2 if none is carried |
| `min(a, b, ...)`, `max(a, b, ...)` | The smallest or largest of two or more values |
| `clamp(v, low, high)` | `v`, kept between `low` and `high` |
| `dice(count, sides)` | A dice roll with computed count and size |
| `count_enemies()`, `count_enemies(cond)` | Enemies present, or those for which `cond` holds with each as `target` |
| `any_enemy(cond)`, `all_enemies(cond)` | 1 if any, or all, enemies present pass `cond` |
| `sum_enemies(v)`, `sum_enemies(v, cond)` | The sum of `v` over enemies (that pass `cond`), each as `target` |
| `max_enemies(v)`, `max_enemies(v, cond)` | The largest `v` over enemies (that pass `cond`), or 0 |
| `list_size(name)` | How many are on a [list](combat.md#roles-and-lists) |
| `scheduled(depth)` | 1 if an event is [scheduled](rooms-events.md#scheduling-events) at that depth |
| `intel(id)` | How many rumours today's [intel](intel.md) slot `id` picked |
| `mod(id)` | 1 if the mod with that id is installed. Fixed when packs load |

`carried` and `best_item` take one item id or a `#tag` (several joined with `+`), not a full
[item filter](items.md#item-filters).

## Conditions

A condition is one of:

- an expression: `"run.heat >= 3"`, `"true"`, or a JSON `true`/`false` or number;
- a list of conditions, which holds when all of them do;
- an object with exactly one condition name:

| Condition | Holds when |
|---|---|
| `{ "all": [c, ...] }` | Every condition holds |
| `{ "any": [c, ...] }` | At least one holds |
| `{ "not": c }` | `c` doesn't hold |
| `{ "chance": 30 }` | A d100 roll is 30 or less. Rolls each time it is checked |
| `{ "has_tag": { "who": "target", "tag": "human" } }` | `who` (default `target`) has the tag |
| `{ "has_status": { "who": "player", "status": "bleeding" } }` | `who` (default `target`) has the status |
| `{ "carrying": filter }` | The player carries an item matching the [item filter](items.md#item-filters) |
| `{ "scene": "combat" }` | The run is in that scene |
| `{ "intent_interruptible": {} }` | `who` (default `target`) has an intent marked `interruptible` |
| `{ "fits": {} }` | The action's `with` item is rounds for its `item` gun |
| `{ "credits_at_least": 50 }` | The player has at least that many credits |

`who` is `self`, `target`, `player` or `role.NAME`. C# mods can add conditions, named `pack:name`; inside that pack the
short name works.

## Run variables

`run.NAME` is a variable that lasts for one run. Any name you `set` or `add` exists from then on; the area's `vars` sets
some at the start.

The engine keeps these itself. They can be read but not set:

| Variable | Value |
|---|---|
| `run.depth` | Layout rooms entered so far |
| `run.rooms` | Rooms in the layout |
| `run.rooms_left` | `rooms - depth` |
| `run.turn` | The fight's turn, from 1 |
| `run.enemies` | Enemies present |
| `run.credits` | The player's credits |
| `run.ground` | Items in the ground window |
| `run.scheduled` | Events waiting at future depths |

The engine also sets these after something happens; you may read or change them:

| Variable | Set by |
|---|---|
| `run.won` | The end of a fight: 1 if won |
| `run.last_damage` | `damage`: what was dealt after armor |
| `run.healed` | `heal`: how much hp came back |
| `run.natural`, `run.total` | A `die` check or `roll`: the natural roll, and with `mod` added |
| `run.given`, `run.found_items` | Giving items: whether the last give worked; items found this run |
| `run.trade_cash`, `run.trade_bought` | Closing a trade |
| `run.cart_taken` | `take_cart` |
| `run.cleared_ground` | `clear_ground` |
| `run.reload_used` | `reload` |
| `run.outcome_NAME` | The end of the run: 1 for its outcome |

`run.cleared` is yours to set: above 0, it makes the run [count as a clear](areas.md#on-the-map).

Text variables are written `run.$NAME`, set with `set_text`, and used as text arguments. The engine sets `run.$lost`,
`run.$taken`, `run.$scheduled`, `run.$pick` and `run.$rep`; see the effects that set them.
