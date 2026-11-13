# Packs

How a pack's files are organised, how ids work, and how a pack works with other mods.

## Files

The framework reads `ExpeditionFramework/main.json`. It may list more files from the same folder under `include`.

The files are JSON with two allowances for hand-written data: `//` comments and trailing commas.

Each file is an object of **sections**. Any section can appear in any file; a definition can use one written in
another file, or in another pack, whatever the order.

| Section | Defines | Page |
|---|---|---|
| `areas` | Destinations on the night map | [Areas](areas.md) |
| `rooms` | Rooms a layout is built from | [Rooms and events](rooms-events.md) |
| `events` | Scenes with choices, started by effects | [Rooms and events](rooms-events.md) |
| `encounters` | Groups of enemies to fight | [Combat](combat.md) |
| `enemies` | Foes, their stats, behavior and the actions that work on them | [Combat](combat.md) |
| `intents` | What an enemy is about to do | [Combat](combat.md) |
| `behaviors` | Weighted intent lists enemies can share | [Combat](combat.md) |
| `statuses` | Effects that last turns or rooms | [Combat](combat.md#statuses) |
| `kits` | Bundles of actions and hooks an area opts into | [Areas](areas.md#kits) |
| `loot` | Weighted item tables | [Items](items.md#loot-tables) |
| `carts` | A trader's stock and budget | [Items](items.md#trading) |
| `item_groups` | Names for sets of items, used as `#name` | [Items](items.md#item-groups-and-item-stats) |
| `item_stats` | Numbers and tags a pack gives game items | [Items](items.md#item-groups-and-item-stats) |
| `values` | Named expressions, read as `val.NAME` | [below](#shared-pieces) |
| `effects` | Named effects with parameters | [below](#shared-pieces) |
| `interactions` | Named actions, reused with `use` | [below](#shared-pieces) |
| `templates` | Objects other definitions build on with `extends` | [below](#templates) |
| `include` | More files to load | [below](#include) |
| `debug_kit` | Item ids the game's debug panel can hand out | [below](#debug-panel) |
| `patch` | Changes to other packs' definitions | [below](#patching-other-packs) |

An unknown section is a problem.

## Ids

Every definition belongs to the pack that defines it. Inside your pack, write `rat`; from another pack, write
`your_pack:rat`. The framework's own definitions are `expedition_framework:NAME`.

The same rule applies to text keys (`other_pack:some_key`), templates, shared effects, values and interactions.

## Problems and warnings

Loading reports two kinds of message, each with its location, such as
`my_mod/ExpeditionFramework/main.json.rooms.a.enter[0]: unknown effect "nope"`.

- **Problems** are mistakes that make the pack wrong: broken JSON, a value of the wrong type, an unknown effect, a
  reference to something that doesn't exist. A pack with any problem adds no areas.
- **Warnings** are likely mistakes that don't stop the pack: a key a definition doesn't have (the message lists the
  ones it does), an argument an effect never reads, an unknown tone, a `#tag` no item can have, `weight`/`when`/`tags`
  on a kind that ignores them, a `memory.` name the pack reads but never writes, or reads another pack's memory when
  that pack isn't loaded.

In the game both go to the game's log ([where it is](getting-started.md#6-install)), warnings prefixed `warning:`.
[ef-sim lint](testing.md#lint) prints them as `ERROR` and `WARN`; `--strict` fails on warnings too.

## Include

`include` lists more files to read. Each entry is a file name, or an object that loads the file only when other mods
are installed and enabled:

```json
"include": [
  "enemies.json",
  { "file": "with_starter.json", "requires": ["ef_starter"] }
]
```

A file whose `requires` aren't all met is skipped completely, so the ids and memory names it uses are never checked.
This is how a pack supports another mod without a separate compatibility mod. The framework does this itself:
[ExpeditionFramework/gunsmith.json](../ExpeditionFramework/gunsmith.json) gives Gunsmith Expansion's guns their
magazine sizes, and loads only with `gunsmith_expansion`.

A file may be included only once. Included files may include more files.

### mod(id) inside a file

For a switch inside a file that always loads, `mod(id)` in an [expression](expressions.md) is 1 when that mod is
installed and enabled, else 0. A loot entry with `"when": "mod(other_mod)"` adds that mod's items to a table without
defining the table twice.

Both `requires` and `mod()` are decided once, at load. Enabling a mod needs a restart anyway.

## Load order

The framework's own pack loads first, then the other packs in mod-list order. Every pack is read before any is
compiled, so a pack can use and patch definitions from packs listed after it.

`item_stats` from several packs merge per item: a later pack's number replaces an earlier one, and tags add up. If
Gunsmith Expansion shipped its own `ExpeditionFramework/` folder, its magazine sizes would replace the framework's.

Packs are loaded once, when the game has registered every mod. A pack enabled later is reported in the log and needs a
restart.

## Shared pieces

### values

A named [expression](expressions.md), read anywhere as `val.NAME`:

```json
"values": {
  "haggle_dc": "12 + run.heat / 2",
  "fence_markup": "game.rep.black_market >= 20 ? 10 : 30"
}
```

Values may use other values. A value that depends on itself is a problem.

### effects

A named effect, used like a built-in. It may declare `params` with defaults; inside, each is `arg.NAME`.

```json
"effects": {
  "raise_heat": {
    "params": { "by": 1 },
    "do": [
      { "add": { "var": "run.heat", "value": "arg.by" } },
      { "say": { "text": "sm_heat_up", "args": ["run.heat"], "tone": "bad" } }
    ]
  }
}
```

```json
{ "raise_heat": { "by": 2 } }
{ "raise_heat": 2 }
{ "raise_heat": {} }
```

The second form passes 2 to the first parameter. The third uses the defaults. An argument the effect doesn't declare is
a problem. A shared effect without parameters can be written as just the effect list, as in
`"swing_miss": [ { "say": ... } ]`.

Parameters are numbers. A shared effect calling itself more than 32 levels deep stops and logs it.

A shared effect from another pack is called by its full name, `{ "other_pack:raise_heat": { "by": 2 } }`. Inside your
own pack, your shared effect wins over a built-in of the same name.

### interactions

A named [action](actions.md), placed with `use`. Keys next to `use` replace the shared action's own:

```json
"interactions": {
  "listen": { "id": "listen", "label": "sm_action_listen", "group": "explore", "once": true, "do": [ { "say": "sm_listen" } ] }
},
"rooms": {
  "stalls": { "text": "sm_room_stalls", "interactions": [ { "use": "listen" } ] },
  "back_alley": { "text": "sm_room_alley", "interactions": [ { "use": "listen", "group": "tactics" } ] }
}
```

## Templates

A definition in `areas`, `rooms`, `events`, `encounters`, `enemies`, `intents`, `statuses`, `kits`, `loot` or `carts`
can build on a template with `extends`. Keys
the definition sets replace the template's whole value for that key; there is no deep merge. Templates can extend
other templates.

```json
"templates": {
  "market_room": { "tags": ["market"], "weight": 2 }
},
"rooms": {
  "stalls": { "extends": "market_room", "text": "sm_room_stalls" }
}
```

A template from another pack is `other_pack:name`.

## Debug panel

`debug_kit` lists item ids. The game's debug panel then has an entry "Expeditions: give test kit (PACK)" that puts them in
the inventory. Every area with `unlocked_by` also gets an "Expeditions: unlock AREA" entry.

```json
"debug_kit": ["kitchen_knife", "bandage_item", "crowbar"]
```

## Patching other packs

A `patch` entry changes another pack's definition before it is checked. `target` is `pack:kind.id`, or `pack:kind.*`
for every definition of that kind.

```json
"patch": [
  { "target": "night_expeditions_reborn:loot.ne_arms", "add": { "items": [ { "item": "my_gun", "weight": 2 } ] } },
  { "target": "night_expeditions_reborn:areas.*", "add": { "kits": ["my_mod:my_kit"] } },
  { "target": "night_expeditions_reborn:loot.ne_supplies", "remove": { "items": ["cup_noodle"] } },
  { "target": "night_expeditions_reborn:enemies.ne_enemy_looter", "set": { "stats.hp": 12 } },
  { "target": "night_expeditions_reborn:values.sneak_dc", "replace": "12 + run.alarm / 2" }
]
```

| Operation | What it does |
|---|---|
| `replace` | Replaces the whole definition |
| `remove` | For a list at the path: removes entries. A string matches an entry that is that string, or an object whose `id`, `item`, `loot` or `table` is that string. An object matches an entry that has all of its fields. For an object at the path: removes the named keys |
| `set` | Sets each dotted path, adding objects on the way |
| `add` | Appends to the list at each dotted path, creating it if needed |

One entry may use several operations; they apply in the order above. Kinds are `item_groups`, `item_stats`, `templates`,
`effects`, `values`, `interactions`, `behaviors`, `statuses`, `intents`, `loot`, `carts`, `enemies`, `encounters`,
`events`, `rooms`, `kits` and `areas`.

- What a patch adds is compiled as part of the target pack, so plain ids and text keys in it mean the target's. Write
  your own as `my_mod:name`, text keys included.
- A patch for a pack that isn't installed is skipped. To say so explicitly, put patches in an `include` with
  `requires`.
- Patches apply in mod-list order, so when two set the same thing the later mod wins.
- Each pack's patches are tried in turn and kept only if they add no problem anywhere. A pack whose patches would break
  something, including its own mistakes in a target, loses all its patches and adds no areas; the packs it tried to
  change load as written.
