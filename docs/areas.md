# Areas

An area is one destination on the night map. It says which rooms a run goes through, what the player starts with, which
actions are available, and how the area looks on the map and on screen.

```json
"areas": {
  "night_market": {
    "title": "sm_area_title",
    "hidden_when": "game.rep.security <= -50",
    "ambience": "dumping_ground",
    "player": { "hp": 20, "dodge": 10, "charm": "game.rep_exact.black_market >= 1000 ? 2 : 0" },
    "vars": { "heat": 0 },
    "kits": ["expedition_framework:explore", "expedition_framework:melee", "expedition_framework:medicine", "market_rules"],
    "groups": ["explore", "attack", "tactics", "supplies", "move"],
    "layout": [
      { "tag": "market", "count": 2 },
      { "room": "gate" }
    ]
  }
}
```

## Fields

A working area needs only `title`, `player` and `layout`, and most list `kits`. Everything else is optional; the
sections after [Button groups](#button-groups) cover the fields most areas never use.

| Field | Type | What it does |
|---|---|---|
| `title` | text key | The area's name on the map and on its panel. Required |
| `player` | object of expressions | The player's starting stats. `hp` is required; `max_hp` is set to `hp`. Any other name is a stat you can read as `player.NAME` |
| `vars` | object of expressions | Run variables set before anything else, read as `run.NAME` |
| `layout` | list | The rooms one run goes through. Required. [Below](#layout) |
| `entrance` | room id | A room the run starts in, before the layout. Pressing on from it enters the first layout room |
| `kits` | list of kit ids | Actions and hooks this area uses. [Below](#kits) |
| `interactions` | list | Actions offered in every room of this area. [Actions](actions.md) |
| `on` | object | [Hooks](run-flow.md#hooks) for this area |
| `pools` | object of id lists | Named lists of ids that layouts, effects and intel draw from. [Below](#pools) |
| `unlocked_by` | area id | The area shows on the map only after that area has been cleared once |
| `hidden_when` | condition | Keeps the area off the map while it holds. [Below](#on-the-map) |
| `ambience` | text | The game's ambience id while there. Default `dumping_ground`; `""` plays none |
| `audio` | list | Your own music, ambience and timed sounds, on as many rails as you like. [Below](#audio) |
| `art` | text | The sprite shown behind the panel. Default: the area's own id |
| `packing` | object | What the player must bring. [Below](#packing) |
| `intel` | list | Daily rumour cards on the map. [Intel cards](intel.md) |
| `intel_max` | expression | The most intel cards a day across all slots. [Intel cards](intel.md#limiting-the-cards) |
| `hud` | object | What the info window shows. [Below](#hud) |
| `groups` | list of names | The order of button groups. [Below](#button-groups) |
| `texts` | object | Replaces framework strings for this area. [Below](#framework-texts) |
| `luck` | object | How the player's luck bends d20 rolls. [Below](#luck) |
| `extends` | template id | Builds on a [template](packs.md#templates) |

`player` and `vars` are expressions evaluated once when the run begins, so they can read `game.` and `memory.`:
`"hp": "max(10, 30 - game.wounds * 2)"`. `vars` may not name a variable the engine keeps (such as `depth`); see
[engine variables](expressions.md#run-variables).

## Layout

`layout` is a list of slots. Each slot adds rooms to the run, in order, when the run begins.

| Slot | Picks from |
|---|---|
| `{ "room": "office" }` | That room |
| `{ "tag": "cellar" }` | Every room with that tag |
| `{ "from": ["a", "b"] }` or `{ "rooms": ["a", "b"] }` | Those rooms |
| `{ "pool": "name" }` | The rooms in one of the area's [pools](#pools) |

| Key | Default | What it does |
|---|---|---|
| `count` | 1 | How many rooms this slot adds. An expression, rolled once: `"1d2 + 1"` |
| `unique` | `true` | A room already in the run can't be picked again. `false` allows repeats |

Rooms are picked by `weight` among those whose `when` holds. A slot that runs out of rooms adds fewer. When the player
presses on from the last room, the run ends `cleared`.

## Kits

A kit is a bundle of actions and hooks defined once and listed by areas:

```json
"kits": {
  "market_rules": {
    "interactions": [ ... ],
    "on": { "run_start": [ ... ], "run_end": [ ... ] }
  }
}
```

Kit actions are offered in rooms unless their `in` says otherwise. The framework's own kits are ordinary data in
[ExpeditionFramework/main.json](../ExpeditionFramework/main.json):

| Kit | Gives |
|---|---|
| `expedition_framework:explore` | `press_on` (next room) and `head_home` (leave). Logs a closing line for each outcome |
| `expedition_framework:firearms` | `shoot` an enemy with a gun in hand that has rounds `loaded`, and `reload` a gun from carried ammo |
| `expedition_framework:melee` | `strike` with a melee weapon in hand, or `punch` without one |
| `expedition_framework:medicine` | `heal` with a `#medicine` item, using its `heal_count`, `heal_sides`, `heal_flat` and `stops_bleeding` |

The same file gives game items the numbers these kits read (melee `sides` and `bonus`, gun `capacity`, ammo `rounds`,
medicine heal values), and defines the `bleeding` status, the intents `surprised`, `staggered`, `strike`, `wind_up`,
`heavy_strike` and `flee`, and the behaviors `brawler` and `coward`. Use them as `expedition_framework:NAME`.

## Pools

`pools` names lists of ids. What kind of id a pool holds depends on where it is used:

```json
"pools": {
  "fences": ["fence"],
  "bosses": ["boss_rat", "boss_foreman"]
}
```

- A layout slot `{ "pool": "rooms_a" }` treats it as rooms.
- Effects that pick events, encounters or enemies accept `{ "pool": "name" }`, such as `{ "fight": { "pool": "bosses" } }`.
- An [intel slot](intel.md) names a pool to pick rumours from.

Each pool use is checked against every area that has a pool of that name. Naming a pool no area has is a problem.

## On the map

An area is on the map when both hold:

- `unlocked_by` is absent, or the area it names has been cleared at least once.
- `hidden_when` is absent, or doesn't hold.

`hidden_when` is checked on the map, where no run exists: it can read `memory.` and `game.`, and `run.` reads 0. To
show an area when **either** of two things is true, leave out `unlocked_by` and test both in `hidden_when`:

```json
"hidden_when": "game.clears.flooded_cellar == 0 && memory.met_smuggler == 0"
```

A run counts as a clear when the player leaves after it ended `cleared`, or with `run.cleared` above 0. Set
`run.cleared` when the player has done what the area is for but can still walk on.

## Button groups

Every action has a `group` (default `move`). The actions window shows each group under a heading, in the order
`groups` lists them; groups it doesn't list come after. A group's heading is the text `ef_group_NAME`, or the group's
name if there is no such text. The framework provides `explore`, `attack`, `tactics`, `supplies` and `move`.

## Less common fields

Packing requirements, a custom HUD, replaced framework texts and luck. Skip these until an area needs one.

### Packing

`packing` stops the player setting out without what the area needs. While anything is missing the map card's
"unavailable" line says what, and the area can't be chosen. The card's title stays the area's title.

```json
"packing": {
  "no_backpack": "sm_need_weapon",
  "card": "sm_card_missing",
  "needs": [ { "items": "#weapon", "missing": "sm_need_weapon" } ]
}
```

| Key | What it does |
|---|---|
| `needs` | A list of `{ "items": filter, "missing": text key }`. Each need is met by one packed item that matches the [item filter](items.md#item-filters); otherwise its `missing` text is listed |
| `no_backpack` | Text listed when the player carries neither a backpack nor anything [in hand](items.md#carrying-things-in-hand) |
| `card` | Text for the map card's status line while something is missing. `{0}` is the game's "unavailable" label, `{1}` the missing texts joined with commas. Without `card` the line keeps the game's label |

What counts as packed is the chosen backpack's contents plus the two hand slots.
[`expect.packing`](testing.md#packing) in `sim.json` tests this without the game.

### HUD

The info window shows the player's state and the enemies. Without `hud` it shows health, the turn in a fight, the
player's statuses and one line per enemy. With `hud` the area decides, and none of that is kept: the window shows
only the rows you list and your own enemy line, so `enemies.line` is required.

```json
"hud": {
  "rows": [
    { "parts": [ { "text": "sm_hud_hp", "args": [ { "bar": ["player.hp", "player.max_hp"] }, "player.hp", "player.max_hp" ] } ] },
    { "when": "run.heat > 0", "parts": [ { "text": "sm_hud_heat", "args": ["run.heat"], "tone": "bad" } ] }
  ],
  "enemies": {
    "line": { "text": "sm_hud_enemy", "args": ["target.name", "target.hp", "target.max_hp"] },
    "none": "sm_hud_clear"
  }
}
```

| Key | What it does |
|---|---|
| `title` | A [line](text.md#lines) for the window title. Without it: the framework's `ef_panel_title` (area name, room, room count) |
| `rows` | Each row: `parts` (lines shown side by side), an optional `when`, or `"separator": true` for a divider |
| `enemies.line` | **Required.** One line per enemy present, with the enemy as `target`. Each enemy's intent is shown under its line, and the focused one is marked |
| `enemies.none` | Shown when no enemy is present |
| `enemies.pending` | Used instead of `line` in the scenes listed in `pending_scenes` (by default the same as `line`) |
| `enemies.pending_scenes` | Scene names where enemies are present but not yet fighting, such as a standoff event |

A part may have its own `when`, and `each_item` with an [item use](actions.md#items) repeats the part once per
matching carried item, with that item as `item`:

```json
{ "when": "carried(#firearm+in_hand) > 0", "parts": [
  { "each_item": { "all": ["#firearm", "#in_hand"] }, "text": "ne_status_gun", "args": ["item.name", "item.loaded", "item.capacity"] }
] }
```

Like button labels, the HUD is drawn [without rolling](actions.md#drawn-without-rolling).

### Framework texts

`texts` replaces the framework's own strings for this area, mapping a framework key to one of your text keys:

```json
"texts": { "ef_found": "sm_found", "ef_leave_knocked_out": "my_blackout" }
```

Any key in the framework's [Localization/en.csv](../Localization/en.csv) can be replaced. Two families exist only
when an area provides them:

- `ef_leave_OUTCOME`: the label of the button that ends a run with that outcome (default `ef_leave`).
- `ef_roll_dN`: how a roll of an N-sided die is shown in the log (default `ef_roll`).

### Luck

`luck` lets the player's luck bend d20 checks and rolls:

```json
"luck": { "value": "game.luck", "low": 5, "high": 16 }
```

On a d20, when `value` is positive, a natural roll at or below `low` (default 5) is rerolled with `value`% chance. When
`value` is negative, a roll at or above `high` (default 16) is rerolled with -`value`% chance. Other dice are never
changed.

### Audio

`audio` is a list of rails. Each rail is its own channel, so every rail plays at once: music over two ambience loops over
an alarm that goes off now and then. The game's own `ambience` keeps playing under them; set `"ambience": ""` when
your rails replace it.

```json
"ambience": "",
"audio": [
  { "type": "music", "volume": 0.6, "sounds": [ { "audio": "my_mod:theme_a" }, { "audio": "my_mod:theme_b" } ] },
  { "volume": 0.3, "sounds": [ { "audio": "my_mod:cave" } ] },
  { "sounds": [ { "audio": "my_mod:drips", "chance_to_skip": 50 } ] },
  { "every": [15, 60], "sounds": [ { "audio": "my_mod:alarm", "chance_to_skip": 25 }, { "audio": "my_mod:clank" } ] }
]
```

| Rail key | Default | What it does |
|---|---|---|
| `sounds` | required | The sounds this rail picks from, each `{ "audio": id, "chance_to_skip": percent }` |
| `every` | none | Seconds between plays: one number, or `[shortest, longest]` for a random wait. Without it the rail loops |
| `volume` | 1 | How loud the rail is, from 0 up. 1 is the file as it is |
| `type` | `ambience`, or `sound` with `every` | Which volume setting the rail follows: `music`, `ambience` (the game's background volume) or `sound` (master volume only) |
| `when` | always | A condition. The rail plays only while it holds: `"player.hp * 3 < player.max_hp"`. Checked when the run begins and after each of the player's actions, not on a clock |
| `fade` | 0 | Seconds the rail takes to fade in, and out when its `when` stops holding |
| `once` | false | Plays its sound one time instead of looping. Not with `every` |
| `start` | 0 | Where in the file a loop or a `once` begins, as a part of its length: `0.5` is the middle |

A rail without `every` picks one of its sounds when the player arrives and loops it until they leave. A rail with
`every` waits, picks one of its sounds and plays it once, then waits again; the first play comes after a first wait.
Every pick is even between the rail's sounds. `chance_to_skip` is the percent chance that the picked sound stays
silent: for a loop the rail is quiet for that visit, for a timed rail that one play is skipped.

What plays never touches the run's dice, so audio changes nothing about how a run rolls, and [ef-sim](testing.md) ignores
it.

#### Your own sound files

The game loads every `.wav`, `.ogg` and `.mp3` in your mod's `Sounds` folder and names each one
`MOD_ID:FILE_NAME`, without the extension: `Sounds/alarm.ogg` in the mod `my_mod` is `my_mod:alarm`. Those ids work
in `audio`, in an action's [`sound`](actions.md) and in the [`sound` effect](effects.md), next to the game's own
sound ids. A sound id nothing registered is skipped, with a line in the game log.
