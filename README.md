# Expedition Framework

Plays night expeditions defined in data. Any mod that ships `ExpeditionFramework/main.json` is a pack: it needs no
dependency on this mod, and without it installed the folder is simply never read. A pack can extend anything in C# through [the API](#the-c-api). Nothing about any particular area, enemy or weapon lives in the engine.

Status: 0.1.0. The engine passes its simulator. The in-game side builds and passes the mod scanner, but has not been played.

## Making a pack

```
MyMod/
  manifest.xml               list expedition_framework as a prerequisite only if the mod is nothing without it
  MyMod.dll                  the game needs every mod to ship a DLL with an IMod class, even an empty one
  ExpeditionFramework/
    main.json                may list more files in this folder under "include"
  Localization/en.csv
```

`samples/SampleArea` is a complete pack. Every string that players see is a text key in the pack's CSV.

Ids belong to the pack that defines them. Write `rat` inside your pack and `other_pack:rat` to use someone else's.
Problems are logged with their location, such as `my_mod/ExpeditionFramework/main.json.rooms.a.enter[0]: unknown effect "nope"`.
Problems (broken structure, values of the wrong type, references to things that don't exist) stop a pack's areas loading.
Warnings are likely mistakes that don't: keys a definition doesn't have (the message lists the ones it does), arguments an
effect or condition never reads, unknown tones, `#tags` no item can have, `memory.` names a pack reads but never writes,
and `weight`, `when` or `tags` on kinds that
ignore them (only rooms, events, encounters and enemies are picked by weight, condition or tag). They are logged as
`warning: …` and the pack still loads.

### Data for other mods

An `include` entry can be a file name or `{ "file": "...", "requires": ["mod_id", ...] }`. The file loads only when every
listed mod is installed and enabled; otherwise it is skipped, so the ids it names are never checked. That is how a pack
supports another mod without a separate compat mod. For a switch inside a file that always loads, `mod(mod_id)` in an
expression is 1 when that mod is there. Both are decided once, at load; enabling a mod needs a restart anyway.
A loot entry with `"when": "mod(other_mod)"` adds that mod's items to a table without defining the table twice.

The framework's own [ExpeditionFramework/gunsmith.json](ExpeditionFramework/gunsmith.json) gives Gunsmith Expansion's
guns their magazine sizes this way.

The framework's own pack loads first, then other packs in mod-list order. `item_stats` merge per item: a later pack's
number replaces an earlier one and tags add up. So if Gunsmith Expansion shipped its own `ExpeditionFramework/` folder,
its numbers would replace the framework's defaults.

### Patching other packs

Every pack is read before any is compiled, so a pack can use or change another pack's definitions whatever the mod
order. A `patch` entry names a definition as `pack:kind.id`, or `pack:kind.*` for all of a kind, and changes its JSON
before it is checked:

```json
"patch": [
  { "target": "night_expeditions_reborn:loot.ne_arms", "add": { "items": [ { "item": "my_gun", "weight": 2 } ] } },
  { "target": "night_expeditions_reborn:areas.*", "add": { "kits": ["my_mod:my_kit"], "hud.rows": [ ... ] } },
  { "target": "night_expeditions_reborn:loot.ne_supplies", "remove": { "items": ["cup_noodle"] } },
  { "target": "night_expeditions_reborn:enemies.ne_enemy_looter", "set": { "stats.hp": 12 } },
  { "target": "night_expeditions_reborn:values.sneak_dc", "replace": "12 + run.alarm / 2" }
]
```

| Operation | What it does |
|---|---|
| `replace` | Replaces the whole definition |
| `remove` | Takes entries out of a list: a string matches an entry that is that string or whose `id`, `item`, `loot` or `table` is it; an object matches an entry with all its fields. On an object, removes the named keys |
| `set` | Sets dotted paths inside the definition, adding objects on the way |
| `add` | Appends to the list at a dotted path, creating it if needed |

One entry can use several, applied in that order. Kinds are every section that defines things by name: `loot`, `areas`,
`enemies`, `rooms`, `events`, `encounters`, `kits`, `statuses`, `intents`, `carts`, `values`, `effects`, `templates`,
`interactions`, `behaviors`, `item_groups` and `item_stats`.

- What a patch adds is compiled as part of the target pack, so ids and text keys in it mean the target's. Write your
  own as `my_mod:name`, text keys included.
- A patch for a pack that isn't installed is skipped. Put patches in an `include` with `requires` to say so explicitly.
- Patches apply in mod-list order, so when two set the same thing the later mod wins.
- Each pack's patches are tried in turn and kept only if they cause no problem anywhere. A pack whose patches would
  break something, its own mistakes in a target included, loses them and adds no areas; the packs it tried to change
  load as written.

## Sections

| Section | What it defines |
|---|---|
| `areas` | Title, `player` stats, `vars`, `layout`, `kits`, `unlocked_by`, plus the map and UI fields below |
| `rooms` | `text`, `enter` effects, `interactions`, `on` hooks |
| `encounters` | `enemies`, with `weight` and `when` for random picks |
| `events` | `text`, `enter`, and `choices` |
| `enemies` | `name`, `tags`, `stats`, `behavior`, `opening`, `interactions`, `on` |
| `intents` | What an enemy is about to do: `label`, `tell`, `interruptible`, `next`, `do` |
| `behaviors` | Weighted intent lists that enemies can share |
| `statuses` | `name`, `turns`, `on` hooks, `interactions` |
| `kits` | Bundles of interactions and hooks that an area opts into |
| `loot` | Weighted item tables. Each entry names an `item`, another `loot` table or a game `table`, with a `weight` and an optional `when` |
| `carts` | A trader's stock and budget, for the `trade` and `take_cart` effects |
| `item_groups` | Names for sets of items, used as `#name` |
| `item_stats` | Props and tags a pack gives to game items, such as what a medicine heals |
| `values`, `effects`, `interactions` | Named pieces to reuse with `val.name` or `{ "use": "name" }` |
| `templates` | Objects other entries build on with `"extends"` |
| `include`, `debug_kit` | More files to load; items for the debug panel's test kit |
| `patch` | Changes to other packs' definitions, below |

`layout` is a list of slots: `{ "room": "office" }`, or `{ "tag": "cellar", "count": "1d2 + 1" }`, or `{ "from": [...] }`.

Areas also control how they appear and play on screen:

| Field | What it does |
|---|---|
| `hidden_when` | Condition that keeps the area off the map. It can read `memory.` and `game.` (see [Memory across nights](#memory-across-nights)) |
| `ambience`, `art` | The game's ambience id, and a sprite id under `Textures/Items` (defaults to the area's own id) |
| `pools` | Named id lists that layouts, encounters and intel draw from |
| `packing` | `needs` (items that must be in the backpack or in hand, each with a `missing` text), `no_backpack` (shown when carrying neither), `card` |
| `intel` | Daily map cards: `id`, `pool`, `chance`, `counts`, `title`, `description`, `effect`, `tone`, `good`, `when`. Read during a run with `intel(slot)`. A slot whose `when` doesn't hold when the day's cards are rolled shows nothing that day |
| `hud` | `title`, `rows` (`when`, `parts`, or `separator`) and `enemies` (`line`, `pending`, `none`, `pending_scenes`) |
| `groups` | Order of the button groups. Each group's heading is the text key `ef_group_<name>` |
| `texts` | Replaces framework strings for this area, by key |
| `luck` | How the player's luck bends rolls |

The framework's own `shoot`, `reload`, `melee`, `heal` and `press on` are ordinary kits in [ExpeditionFramework/main.json](ExpeditionFramework/main.json).

## Carrying things in hand

When the game asks which bag to take out at night, the framework opens an "In hand" window with two slots beside it.
Whatever is put there counts toward an area's `needs` alongside the chosen bag, so a player without a bag, or with a gun
too big for it, can still go. On arriving at any location that shows the night screen's hands, the game's own included,
the two items move into them, and the game brings them home with everything else. Closing the menu without picking a
bag, coming home from the map or from a location without hands, or going to bed puts them back in the inventory; the
game doesn't save the window.

The game has no hook for that menu, so this is the framework's only use of Harmony: three postfixes, on
`StoreResponse.ShowAfterhourResponse`, `ResponseList.CreateAfterhoursResponse` and `EmporiumEntry.HideResponseWindow`.

## Interactions

There is no weak-point field and no list of player verbs. Enemies, rooms, events, statuses, areas and kits all carry
the same thing, and it answers each question separately:

```json
{
  "id": "blast_foreman",
  "label": { "text": "sa_action_blast", "args": ["item.name"] },
  "group": "tactics",
  "uses": { "select": "#explosive", "consume": 1 },
  "when": "target.hp > 3",
  "check": {
    "chance": "75",
    "success": [ { "damage": { "amount": "3d6 + 4", "armor": "ignore" } } ],
    "failure": [ { "damage": { "to": "player", "amount": "1d4" } } ]
  }
}
```

| Question | Field |
|---|---|
| What can the player do, and when? | `label`, `when`, `in` (`room`, `event`, `combat`), `once` |
| To whom? | `targets: "enemy"` gives one button per enemy, filtered by `where`. Otherwise the owner. |
| What can be used? | `uses.select`: an id, `#group`, a list, or `{any\|all\|not\|when}`. One button per match unless `each` is false. |
| What happens? | `do` and `check` |
| Are you sure? | `confirm` shows the game's dialog first |

If an interaction has a `check` with a `chance`, the percent is added as the label's last argument.

Labels, `when`/`where` on buttons, previews and HUD lines are drawn without rolling: dice count as 1 and `variants`
show the first text. So a button whose `when` uses `chance` always shows, and one that needs a high roll never shows
(the gate isn't checked again when pressed). Roll into a `run.` var when entering the room and test that instead.

## Vocabulary

**Expressions** go wherever a number or condition is expected: `60 + item.accuracy * 5 - target.dodge`, `2d6+1`,
`self.hp <= 5 && run.depth >= 2`. Paths start with `self`, `target`, `player`, `item`, `with` (the second item of a pair),
`arg`, `val` (a named value), `game`, `run` or `memory` (see [Memory across nights](#memory-across-nights)). `run` also has `depth`, `rooms`, `rooms_left`, `turn`, `enemies`,
`credits`, `ground` and `scheduled`, kept by the engine: `set` or `add` on them, or an area's `vars` naming them, is a
problem. Unknown names read as 0, so any new stat or variable just works. Functions include `carried`, `best_item`,
`intel`, `list_size`, `mod`, `min`, `max`, `clamp`, `dice`, `scheduled` and `count_enemies`, `any_enemy`, `all_enemies`,
`sum_enemies`, `max_enemies`.

The engine also sets `run` variables for data to read: `won` (after a fight), `last_damage`, `healed`, `given` (1 if
the last give worked), `found_items`, `natural` and `total` (the last check or roll), `trade_cash` and `trade_bought`,
`cart_taken`, `cleared_ground`, `reload_used` (ammo items the last reload took), and `outcome_<outcome>` when the run ends. Texts: `run.$lost`, `run.$taken`,
`run.$scheduled` and `run.$pick`.

`game` reads the player outside the run: `game.luck`, `game.roll_luck`, `game.wounds`, `game.maimed`, `game.day` (the
game's day counter, 0 with no save loaded), `game.clears` (how many times the player has cleared this area) and
`game.clears.AREA` (the same for another area of this pack, such as `game.clears.flooded_cellar`). A clear counts when the
player leaves an area after a run that ended `cleared` or with `run.cleared` above 0, so during a run these count earlier
nights, not tonight. They work on the map too.

**Effects** are lists, or objects with one effect name:

| Kind | Effects |
|---|---|
| Control | `if`, `cond`, `check`, `roll`, `random`, `repeat`, `once`, `for_each`, `for_items`, `set`, `add`, `bind`, `set_text`, `list_add`, `list_clear`, `schedule`, `unschedule` |
| World | `say`, `hint`, `sound`, `night_log`, `event`, `end_event`, `next_room`, `stay`, `leave`, `wound`, `emit` |
| Combat | `fight`, `engage`, `end_fight`, `retreat`, `enemy_turn`, `focus`, `pick_enemy`, `damage`, `heal`, `status`, `clear_status`, `defeat`, `remove`, `spawn`, `muster`, `intent`, `cast`, `uncast` |
| Items | `consume`, `reload`, `equip`, `pick_item`, `give_item`, `give_loot`, `credits`, `lose_items`, `lose_found`, `take_back`, `clear_ground` |
| Trading | `trade`, `new_cart`, `take_cart` |

`spawn` outside a fight does nothing and runs its `else`.

`reload` fills the gun (the item) from the paired ammo. Each ammo item is worth its `rounds` stat whatever gun it goes
into, and reload takes as many rounds as fit, so it uses the ammo itself: don't also `consume` it. Rounds already
loaded stay unless the new ammo is a different `kind`. No round is thrown away: an item opened but not emptied stays
carried, its `spent` counts the rounds gone from it, and the next reload draws from it first. Ammo without `rounds`
fills the gun with one item. The standard kits give every `#ammo` 4 rounds.

`lose_items` destroys carried pieces (a stack counts as one). `{"lose_items": 50}` gives each a 50% chance;
`{"lose_items": {"per_cell": 10, "max": 90}}` gives each 10% per grid cell it takes up, attachments included, up to 90%.

**Conditions** are expressions, lists, or: `all`, `any`, `not`, `chance`, `has_tag`, `has_status`, `carrying`, `scene`,
`intent_interruptible`, `fits`, `credits_at_least`. `not` and `carrying` take their condition or item filter directly,
`{"not": {"scene": "combat"}}`, or under `value`; given nothing (`null` or `{}`) they are a problem.

Effects and conditions that take nothing are written with `true`, like `{"next_room": true}`.

**Tones** for lines, intents and `say`: `good`, `bad`, `dim`, `heading`, `enemy`, `hint`, `plain`. Intel cards: `good`, `bad`, `mixed`.

**Hooks** under `on`: `run_start`, `room_enter`, `combat_start`, `turn_start`, `turn_end`, `combat_end`, `damaged`,
`defeated`, `knocked_out`, `after_action`, `run_end`, plus any a C# mod declares. Self is the one it happened to; Target is the other party.

**What the game side reports about items:** tags `weapon`, `firearm`, `melee`, `in_hand`, `has_rounds`, `ammo`,
`armor_piercing`, `non_lethal`. Firearms have `firepower`, `accuracy`, `recoil`, `ergonomics`, `reliability` and
`suppressed`. Only items with a caliber count as rounds, so a container typed as ammunition doesn't. Ammo has `kind` (0 standard, 1 armor piercing, 2 non-lethal). Everything else comes from a pack's
`item_stats`, including `capacity`, to which attached parts add their `capacity_bonus`. An item with the `melee` tag in
`item_stats` is treated as a melee weapon even if the game doesn't class it as one. A run's item props, such as a gun's
`loaded`, last until the run ends.

## Memory across nights

`run.` variables last one run. `memory.` values stay in the player's save from night to night, so a pack can keep a
count, remember a name, or open something up after an earlier night.

```json
{ "add": { "var": "memory.foreman_beaten", "value": 1 } }
{ "set_text": { "var": "memory.$rival", "value": "target.name" } }
{ "if": { "when": "memory.foreman_beaten >= 3", "then": [ ... ] } }
```

- `memory.NAME` holds a whole number and `memory.$NAME` holds text. They are separate values, even with the same NAME.
  Use them as you would `run.NAME` and `run.$NAME`: in expressions and conditions, as the `var` of `set`, `add` and
  `set_text`, and in text `args`. A name never written reads 0, or "" as text.
- Names are fixed: write each one out in full in the data.
- Each pack has its own memory. Two packs that both write `memory.visits` keep separate counts.
- A write goes into the save straight away, so it stays however the run ends. The game keeps memory in the save's mod
  data under the framework's own mod id, so every save has its own and a new game starts with none. With no save loaded,
  memory reads 0 and "" and writes do nothing.
- The map reads memory too. An area's `hidden_when` and an intel slot's `when` can test `memory.` and `game.` (`run.`
  reads 0 there). Intel is rolled once a day, so what a run writes tonight changes the street's cards from the next day.
- `unlocked_by` puts an area on the map once the area it names has been cleared, and `hidden_when` keeps it off while its
  condition holds. With both, the area needs both. To open an area on either, leave out `unlocked_by` and test both in
  `hidden_when`:

  ```json
  "hidden_when": "game.clears.flooded_cellar == 0 && memory.met_smuggler == 0"
  ```

- Loading warns about a `memory.` name a pack reads but never writes, such as `memory.ghost is read but never written by
  this pack, so it always reads 0`. It is usually a typo. `ef-sim lint --strict` fails on it.
- In `ef-sim`, memory lasts one run unless a `sim.json` row asks for more (see [Memory](#memory) under ef-sim run).

`samples/SampleArea` counts the Foreman's defeats in `memory.foreman_beaten`, and the Foreman mentions the count when you
reach his office again. Its `sim.json` carries the count across 500 runs and checks it.

## The C# API

Data and C# are peers: a pack can use either or both. Ship `ExpeditionFramework.Api.dll`, list it in your own
`<Assemblies>`, and implement `IExpeditionExtension` on your IMod class. Every built-in effect and condition is written
against this same public API, so a mod can do anything they do.

| Register | What it adds |
|---|---|
| `AddEffect`, `AddCondition` | New vocabulary for data, compiled at load so mistakes are reported with their location |
| `AddHook` | A new hook, `packid:name`. Fire it with `run.Emit` or the `emit` effect; data can attach to it under `on` |
| `On` | C# that runs on a hook, after the data attached to it |
| `Intercept` | C# that changes a decision before the run acts on it |

```csharp
public void RegisterExpeditions(IExpeditionRegistrar registrar)
{
    // Used from data as { "drain_power": { "amount": 2 } }.
    registrar.AddEffect("drain_power", spec =>
    {
        Value amount = spec.Value("amount", 1);
        return scope => scope.Run.Set("power", scope.Run.Get("power") - amount(scope));
    });

    // Armored enemies take 2 less damage from everything.
    registrar.Intercept("damage", decision =>
    {
        if (decision.Scope.Target.HasTag("armored")) decision.Value -= 2;
    });

    registrar.On("defeated", scope => scope.Run.Inventory.AddCredits(5));
}
```

An `IScope` gives `Run`, `Self`, `Target`, `Item` and `Paired`. An `IRun` has variables, dice, log and damage directly.
`Set` on a name the engine keeps does nothing: `Get` keeps answering from the engine. An `IRun` also has:

| Part | What it does |
|---|---|
| `run.Flow` | Next room, retreat, start and end events, schedule events at a depth |
| `run.Combat` | Make enemies, muster, start and end fights, spawn, focus, defeat, statuses, intents, heal |
| `run.Inventory` | Carried items, select, consume, equip, give, roll loot, credits, lose items |
| `run.Trade` | Stock carts, open the counters, hand over a cart |

`IItem.Handle` is the game's own object: a `GameItem` for a weapon, a `List<GameItem>` for a stack of anything else.
Ids passed at run time are qualified, like `my_mod:rat`. `spec.Ref` qualifies an id from data and checks that it exists.

`spec.Error` reports a problem at the spec's place; `spec.Warn` reports a warning there and the pack still loads.
Read every key while compiling, before returning the effect: keys not asked for by the time the compiler returns are
reported as unused. A bare `true` (`{"my_mod:ping": true}`) counts as no arguments, and a spec you called `Error` on isn't
reported. `ISpec` only ever gains members; the framework's own `ExpeditionFramework.Api.dll` must be the copy that loads —
a pack shipping an older copy that wins the load breaks calls to newer members such as `Warn`.

| Decision | Change | Scope |
|---|---|---|
| `damage` | `Value` (before armor), `"pierce"`, or cancel | Self is the source, Target the victim |
| `heal` | `Value`, or cancel | Target heals |
| `check` | `Value` (the natural roll). `"sides"` is the die | |
| `chance` | `Value` (percent chance of a percent check) | |
| `status` | `"turns"`, `"rooms"`, or cancel. `Text` is the status | Target gets it |
| `loot` | `Text` (the item), or cancel. Text `"table"` is the table | |
| `intent` | `Text` (the enemy's next move from its behavior) | Self is the enemy |

`run.Decide` makes your own decisions, named `packid:name`, for other mods to intercept.

Listeners and interceptors apply to every expedition from every pack, so check `Run.AreaId` to limit one. Lower `order`
runs first. A handler that throws is logged in the run and skipped, and one that keeps triggering itself is stopped.
Button previews show the odds written in data and don't run interceptors. To use another mod's hook under `on`, load
after that mod.

The API has no memory calls of its own. A C# effect or condition reaches memory through what it compiles from data:
expressions read with `spec.Value`, and effects and conditions read with `spec.Effect` and `spec.Condition`, read and
write `memory.` for the pack whose data uses them, as data does.

Code that runs the engine itself, as `tests/Sim` and `ef-sim` do, provides memory through `IBridge.Memory`
(`MemoryStore` keeps it in memory). `MapContext` takes a store as its optional last argument for map-time reads; without
one, memory reads 0 and "". `IntelRoller.Roll` takes the scope that intel slots' `when` is checked against, such as a
`MapContext`'s `Root`.

## Building and testing

```bash
dotnet build -c Release
```

Installs to `Mods/ExpeditionFramework`. Pass `-p:InstallDir=<folder>/` to build somewhere else. `samples/SampleArea` is copied by hand to `Mods/SampleArea`.

```bash
dotnet run -c Release --project tests/Sim
```

Validates the content, tests the compiler's error reporting, then plays the sample area with random choices. `SIM_RUNS` sets the count.

```bash
dotnet run -c Release --project tools/EfSim -- lint ../MyPack [--mod gunsmith_expansion] [--strict]
```

Checks a pack folder without the game: loads the framework and the pack as the game would, prints each problem as `ERROR`
and each warning as `WARN`, reports text keys missing from `Localization/en.csv`, and compares every other locale CSV with
`en.csv` (missing or extra keys, different `{0}` placeholders). It fits a pack that depends only on the framework: `--mod`
answers `mod(...)` and include `requires` checks but loads no other pack. Exits 1 on errors (or warnings with `--strict`),
2 on bad arguments, else 0.
`tests/Sim -- lint` runs the same code.

### ef-sim run

```bash
dotnet run -c Release --project tools/EfSim -- run ../MyPack [--runs 200]
```

Reads `<pack>/sim.json`, plays each row of its run matrix with the row's bot against a framework-provided stand-in
for the game, prints one `area kit bot runs cleared knocked out extracted` line per row, then runs the checks. `--runs`
overrides every row's run count (small counts can miss rarely offered options). In the table, **cleared** is a run that ends
with outcome `cleared` or run variable `cleared` above 0, **knocked out** is outcome `knocked_out`, and **extracted** is any
other ending (cleared runs included). A run stopped by a guard counts in none.

Checks, each a `FAIL` line: load problems and missing text keys (as in `lint`), text missing or given too few arguments at run
time, option labels or night-log lines left with an unfilled `{`, a run that never ends (over 2000 steps), a scene with no
options, a run that throws, a decision no rule of the row's bot picks an option for, and every `expect.offered` option id that no run was offered, every `runs[n].memory.expect` value that comes out different (see Memory below), every `expect.packing` case that comes out different (see Packing below), and every `expect.loot` case whose rolls break its rule (see Loot below). Load warnings print as `WARN` and
never fail. Exits 0 when all checks pass, 1 when any fails, 2 on bad arguments or a missing or malformed `sim.json`.

`sim.json` version 1:

```json
{
  "version": 1,
  "kits": {
    "knife": {
      "credits": 50,
      "items": [ { "id": "kitchen_knife", "count": 2, "tags": ["melee", "in_hand"], "props": { "sides": 4 } } ]
    }
  },
  "runs": [
    { "area": "flooded_cellar", "kit": "knife", "bot": "random", "runs": 1000, "seed": 42 }
  ],
  "expect": { "offered": ["press_on", "head_home"] }
}
```

`version`, `kits`, `runs` and each item `id`, row `area`, `kit` and `runs` are required; `runs` must be above 0. Defaults:
`credits` 0, `items` empty, `count` 1, `tags` and `props` empty, `bot` `random`, `seed` 42 plus the row's index, `expect`
empty, row `memory` none (see Memory below). Unknown keys are errors, and numbers must be whole. Tags may start with `#`. An `area` without `:` gets the pack's id
as prefix. A kit item's tags and props are layered over the pack's `item_stats` for that item,
the kit's props winning. `samples/SampleArea/sim.json` is a working example.

A row's `kit` may also be a list of kit names: run `n` of the row (counting from 0) uses `kit[n % length]`, so
`"kit": ["light", "heavy"]` alternates. The table names such a row `light+heavy`.

#### Bots

A row's `bot` is `random` (built in: a uniformly random offered option, with `head_home` left out 95% of the time) or a
name from `bots`. A bot is an ordered list of rules. Each decision takes the first rule whose `when` holds and that finds an
offered option; a rule that finds nothing falls through to the next. If no rule picks anything the run stops with a `FAIL`.

```json
"bots": {
  "cautious": [
    { "when": "player.hp * 100 <= player.max_hp * 45", "pick": ["heal"] },
    { "when": "val.scene.combat && !val.focus_is_weakest", "pick": ["shoot", "strike"], "target": "weakest" },
    { "when": "val.scene.event", "first_except": ["rob"] },
    { "pick": ["press_on"] }
  ]
}
```

A rule has an optional `when` (an expression; no `when` always holds) and exactly one of `pick` (option ids in priority
order: the first id that has an offered option wins, with its first such option) or `first_except` (the first offered option
whose id is not listed). `"target": "weakest"` limits `pick` to options aimed at the active foe with the lowest hp (the earlier
foe on ties).

In `when`, `self` and `player` are the player, `target` is the focused foe, and every other path and function works as in pack
data (`run.depth`, `run.rooms`, `run.credits`, `run.<var>`, `any_enemy(target.hp < 3)`, …) except `memory.`, which bots
can't read. Bots also read:

| `val.` name | Value |
|-------------|-------|
| `scene.NAME` | 1 while the scene is `NAME` (`room`, `combat`, `event`, or an event's own scene name) |
| `offered.ID` | how many offered options have id `ID` |
| `intent_ends.SUFFIX` | active foes whose intent id ends with `SUFFIX` |
| `foes_with.STAT` | foes in the fight, defeated or gone included, with `STAT` above 0 |
| `focus_is_weakest` | 1 if the focused foe is the weakest active foe (or there is neither) |
| `loaded` | rounds loaded in carried `firearm` items |
| `shots_left` | `loaded` plus the rounds in carried `ammo` items |
| `has_gun` | 1 if carrying a `firearm` item |
| `coin` | 1 or 0 at even odds, drawn from the row's random stream each time it is evaluated; `&&` and `\|\|` stop early, so put it last |

Errors (exit 2), each naming the place, e.g. `bots.cautious[2].when: …`: unknown keys, a `when` that doesn't compile or uses an
unknown `val.` name, a rule with neither or both of `pick` and `first_except`, an empty `pick`, a `target` other than
`weakest` or without `pick`, a bot named `random`, a bot with no rules, and a row naming an unknown bot. After the table, each
rule of a bot that some row used and that never picked an option prints a `WARN` (often a mistyped id or a condition that never
holds).

#### Memory

A run's `memory.` starts empty and is dropped when the run ends, so every run plays a first night. A row's `memory`
changes that:

```json
{ "area": "flooded_cellar", "kit": "everything", "runs": 500, "seed": 45,
  "memory": { "start": { "foreman_beaten": 1 }, "carry": true, "expect": { "foreman_beaten": { "min": 2 } } } }
```

| Key | What it does |
|---|---|
| `start` | What memory holds before the row's runs, by name without `memory.`: `"visits": 3` for a number, `"$rival": "Dex"` for text. Without `carry` every run starts from these; with it, only the first |
| `carry` | `true` makes the row's runs share one memory, each run seeing what the runs before it wrote, like nights in a row. Default `false` |
| `expect` | What memory must hold after the row's last run: a whole number (exactly), `{ "min": 1, "max": 5 }` (either may be left out), or text for a `$` name. Without `carry` it checks the last run alone |

Each value that comes out different is a `FAIL` such as `runs[3]: memory.foreman_beaten should be at least 2, got 1`.
Writing `memory.` in a name, an empty name, unknown keys, wrong types and `min` above `max` are errors (exit 2).
`--runs` changes how many runs a carried row plays, so an exact `expect` on a carried count fails under it; use `min`
and `max` where that matters.

#### Packing

`expect.packing` checks what an area's `packing` says a bag still needs, the way the map does before an expedition:

```json
"expect": {
  "packing": [
    { "area": "ne_sump", "bag": [ { "id": "combat_knife", "tags": ["weapon", "melee"] } ], "missing": [] },
    { "area": "ne_sump", "bag": [ { "id": "handmade_pistol", "tags": ["weapon", "firearm"] } ], "missing": ["ammo for the gun"] },
    { "area": "ne_sump", "missing": ["weapon"] }
  ]
}
```

Each case needs `area` and `missing` (the expected texts in order, as the player reads them; `[]` means ready to go). `bag`
items take `id`, `count`, `tags` and `props` like kit items but are not layered over `item_stats`: the case states everything
the item carries. Leaving `bag` out means no backpack at all (the area's `no_backpack` text); `"bag": []` is an empty
backpack. After the table a `packing: N case(s), M failed` line prints, and each case that comes out different is a `FAIL`
such as `expect.packing[2]: ne_sump should need [ammo for the gun], got []`. Unknown keys, a missing `area` or `missing`, and
an unknown area are errors (exit 2).

#### Loot

`expect.loot` rolls loot tables the way a run does and checks what came out, e.g. entries that only exist when another mod
is installed:

```json
"expect": {
  "loot": [
    { "area": "ne_foundry", "tables": ["ne_supplies", "ne_arms"], "rolls": 2000, "seed": 7, "never_prefix": "gsx_" },
    { "area": "ne_foundry", "tables": ["ne_supplies", "ne_arms"], "rolls": 2000, "seed": 7, "mods": ["gunsmith_expansion"],
      "must_roll": ["gsx_crate_ammo", "gsx_crate_parts"] }
  ]
}
```

Each case needs `area`, `tables` (not empty) and `rolls` (above 0), and at least one of `never_prefix` (no rolled id may
start with it) and `must_roll` (each listed id must roll at least once). A table or area without `:` gets the pack's id as
prefix. Every table is rolled `rolls` times, in order, in one fresh run of `area` that is never begun (empty kit, `bridge`
settings applied), drawing from its own random seeded with `seed` (default 42), so cases never shift the table above.
`mods` (default none) reloads the framework and the pack with those mod ids also counted as installed for `mod(...)` and
include `requires`, as `lint --mod` does; no other pack is loaded, and load errors or missing text keys of that reload fail
the case. After the packing line a `loot: N case(s), M failed` line prints, and each failure is a `FAIL` such as
`expect.loot[1]: gsx_crate_ammo never rolled`, `expect.loot[0]: rolled gsx_crate_ammo (never_prefix gsx_)` or
`expect.loot[0]: no loot table night_expeditions_reborn:ne_armz`. Unknown keys, wrong types, an empty `tables` or
`never_prefix`, a case with neither check, and an unknown area are errors (exit 2).

#### Bridge

The stand-in game reads every `game.NAME` as 0, never opens a trade screen, hands nothing over for `take_cart`, gives back
the newest found item for `take_back`, and rolls each carried item for `lose_items`. A top-level `bridge` object changes
that, for packs whose balance depends on it:

```json
"bridge": {
  "game": { "luck": 50, "roll_luck": "coin" },
  "trade": { "opens": true, "lose_cash_up_to": 29, "cart": ["cart_goods"] },
  "take_back": "oldest",
  "lose_carried": ["something"]
}
```

`game` maps a name to a whole number, or to `coin`: 0 or 1 drawn from the run's random each time it is read. Names are
written as after `game.`, such as `"day": 30` or `"clears.ne_sump": 2`. Unlisted names read 0. `trade.opens` (default `false`) lets the `trade` effect open; closing it then changes cash by minus a random
0 to `lose_cash_up_to` (0 to 2147483646; left out: no change, nothing drawn) and buys nothing. `trade.cart` lists the names `take_cart`
hands over (default none). `take_back` is `newest` (default) or `oldest`. `lose_carried` is `roll` (default: each carried
item is lost with the effect's chance) or a list of names reported lost every time, with nothing removed or rolled. Every
field is optional; unknown keys and other values are errors (exit 2). Draws come from the same random as dice and bots,
so a setting that draws shifts every later roll of the run.
