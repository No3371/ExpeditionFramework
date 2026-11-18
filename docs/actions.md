# Actions

An action (in the JSON, an **interaction**) is a button the player can press. Rooms, events (as `choices`), enemies,
statuses, kits and areas all carry them. The engine collects every action that applies right now, one button per
target and item, and shows them in the actions window under their [group](areas.md#button-groups).

```json
{
  "id": "hush_lookout",
  "label": "sm_action_hush",
  "group": "tactics",
  "uses": { "select": "#sharp", "each": false },
  "when": { "intent_interruptible": {} },
  "info": [ { "icon": "item" }, "sm_info_hush" ],
  "do": [ { "intent": { "who": "target", "id": "expedition_framework:staggered" } } ]
}
```

## Fields

| Field | What it does |
|---|---|
| `label` | A [line](text.md#lines): the button's text. Required |
| `id` | A name for the action. [`expect.offered`](testing.md#simjson) in tests refers to it |
| `group` | The button group. Default `move` |
| `do` | [Effects](effects.md) run when pressed |
| `check` | A [check](effects.md#control): `do` runs first, then the check. Its odds show on the button |
| `after` | Effects run after `do` and `check` |
| `in` | Scenes where it is offered. Default: depends on the owner, [below](#where-and-when) |
| `when` | A [condition](expressions.md#conditions); the button shows only while it holds |
| `targets` | `"enemy"`: one button per enemy present. `"focus"`: the focused enemy only. Left out: the owner |
| `where` | A condition checked for each target, with it as `target`, before items are looked at |
| `uses` | Items the action works with. [Below](#items) |
| `once` | `true`: once per room |
| `turn` | `false`: in a fight, pressing it doesn't end the player's turn |
| `stay` | `true`: as an event's choice, it doesn't close the event |
| `confirm` | Asks before acting. [Below](#confirm) |
| `info` | Lines, icons and bars beside the button. [Below](#the-info-panel) |
| `preview` | An expression for the odds shown, instead of the check's own |
| `sound` | A game sound id, or one of [your own](areas.md#your-own-sound-files), played when pressed. `"item"` plays the item's own sound, with `sound_else` as the fallback |
| `use` | A [shared interaction](packs.md#shared-pieces) to build on; other keys override it |

An action needs `do` or `check`.

Inside the effects, `self` is the player, `target` is the target (the owner when there is no `targets`), `item` is the
item from `uses` and `with` the item from `uses.with`.

## Where and when

An action is offered only in the scenes its `in` lists. Without `in`, it is offered in its owner's own scene:

| Owner | Default `in` |
|---|---|
| Room, kit, area | `room` |
| Enemy, status | `combat` |
| Event choice | The event's scene (`event` unless the event names one) |

So a kit's "Press on" isn't offered mid-fight, and an enemy's actions aren't offered before the fight starts unless
they list the scene: `"in": ["combat", "standoff"]`. Naming a scene no event declares is a problem.

The order of checks, for each possible button:

1. The scene is in `in`, and a `once` action hasn't been used in this room. `once` counts the action, not the target:
   used on one enemy, it is gone for all of them.
2. For each target: `where` holds.
3. For each item from `uses` (and `with`): `when` holds.

`when` sees the item, so it can test it: `"when": "item.loaded > 0"`.

## Items

`uses` makes the action need a carried item. It is an [item filter](items.md#item-filters), or an object:

```json
"uses": {
  "select": { "all": ["#firearm", "#in_hand"] },
  "each": true,
  "with": { "select": "#ammo", "where": { "fits": {} }, "consume": 0 }
}
```

| Key | Default | What it does |
|---|---|---|
| `select` | | The item filter |
| `each` | `true` | One button per matching item. `false`: one button, for the first match that passes `when` |
| `consume` | 0 | How many of the item to use up when pressed |
| `order` | | An expression; matches are tried lowest first, with the candidate as `item` (as `with` inside `with`) |
| `limit` | 0 | Only the first this many matches. 0 is no limit |
| `where` | | Inside `with` only: a condition with the candidate as `with` and the main item as `item` |
| `with` | | A second item to pair with each first item, with the same keys |

The `fits` condition holds when the `with` item is rounds of the `item` gun's caliber.

## Checks

```json
"check": { "chance": "60 + player.charm * 5", "success": [ ... ], "failure": [ ... ] }
"check": { "die": 20, "mod": "player.charm", "dc": "val.haggle_dc", "success": [ ... ], "failure": [ ... ] }
"check": { "roll": "2d6 + player.str", "dc": 9, "success": [ ... ], "failure": [ ... ] }
```

The button shows the odds of a `chance` or `die` check. For a `die` check they are worked out exactly from the die,
`mod`, `dc`, `fail_at` and `pass_at`. A `roll` check shows none, unless the action has `preview`. See
[`check`](effects.md#control) for what each form does.

The odds are given to the label as one more argument after its own (so a label `"Haggle ({0}%)"` with no `args` shows
them as `{0}`), or wherever the label lists `"percent"` among its args.

## Confirm

```json
"confirm": { "when": "run.heat >= 3", "text": "sm_confirm_slip", "yes": "sm_confirm_yes", "no": "sm_confirm_no" }
```

Asks before acting, while `when` holds (always, if it's left out). `text`, `yes` and `no` are lines, all required.

## The info panel

`info` is a list of pieces shown beside the button, top to bottom. Each piece may have a `when`.

| Piece | Shows |
|---|---|
| A line: `"sm_info_hush"` or `{ "text": ..., "args": [...], "tone": "bad" }` | A line of text. `cases` work too |
| `{ "icon": "item" }` | The `uses` item's icon (needs `uses`) |
| `{ "icon": "with" }` | The `uses.with` item's icon (needs `uses.with`) |
| `{ "icon": "NAME" }` | A sprite from your pack (or `pack:NAME`) |
| `{ "bar": expr, "max": expr, "label": line, "tone": tone }` | A bar filled to `bar` out of `max` (default 100), with an optional label |

If the action has odds, the info panel ends with the framework's `ef_odds` line ("60% chance"), green from 67%, red
below 34%. It is left out when a piece already shows the odds: a line with `"percent"` in its args, or a bar whose
`bar`, `max` or `label` uses them:

```json
"info": [ { "bar": "arg.percent", "label": { "text": "sm_info_haggle", "args": ["percent"] } } ]
```

Inside `info`, the odds read as `arg.percent` in expressions and `"percent"` as a text argument.

## Drawn without rolling

Labels, `info`, `confirm` and the HUD are drawn often, so nothing in them may change the run. While drawing, every die
reads 1 and a line with `variants` shows its first variant. Don't put dice in labels.
