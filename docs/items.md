# Items, loot and trade

Items are the game's own. The framework reads what the player carries, lets packs add facts about items, and gives
items back through loot and trade.

## Item ids

An item id such as `crowbar`, `bandage_item` or `cup_noodle` is the game's own name for an item. The framework doesn't
keep a list of them, so **lint and `ef-sim` can't check them**: a misspelled id in a loot table, `give_item`, a filter
or `item_stats` passes lint with no warning. In the game a filter or `item_stats` entry with it matches nothing,
silently; giving it (`give_item`, a loot roll, a trade cart, the `debug_kit`) gives nothing and the framework logs
`Unknown item id '...'` once per id in the game's log, and the expedition log says it could not give the item.

Places to find ids:

- the framework's [main.json](../ExpeditionFramework/main.json) `item_stats`, which names the game's medicine, melee
  weapons, guns and magazines;
- the examples in these pages and in [examples/Showcase](examples/Showcase).

Copy ids rather than typing them. To check them in the game, list them in the pack's
[`debug_kit`](packs.md#debug-panel) and give yourself the kit from the game's debug panel: a wrong id is
missing from what you get and logged. A `#tag` is different: a tag no item can have is a warning.

## Item filters

Wherever data names "which items" (`uses.select`, `carrying`, `packing.needs`, `for_items`) it takes a filter:

| Filter | Matches |
|---|---|
| `"crowbar"` | The item with that id |
| `"#explosive"` | Items with that tag |
| `["crowbar", "#blade"]` | Any of the list |
| `{ "any": [ ... ] }` | Any of the list |
| `{ "all": ["#firearm", "#in_hand"] }` | Items matching every filter in the list |
| `{ "not": "#broken" }` | Items the filter doesn't match |
| `{ "when": "item.loaded > 0" }` | Items for which the condition holds, with the candidate as `item` |

A `#tag` that no item can have (not a game tag, not an item group, not in any `item_stats` `tags`) is a warning.

In expressions, `carried(crowbar)` counts carried items with that id, and `carried(#firearm+in_hand)` counts items with
all of those tags. See [Functions](expressions.md#functions).

## What the game reports

The framework turns each carried item into something data can read.

**Weapons** (the game's firearms and melee weapons, plus any item with the `melee` tag in `item_stats`) are one item
each. Tags: `weapon`; `firearm` or `melee`; `in_hand` when in one of the two hand slots; `has_rounds` for a gun with
matching rounds carried. Guns also report `firepower`, `accuracy`, `recoil`, `ergonomics`, `reliability` and
`suppressed` (1 or 0), and their `capacity` is the `item_stats` capacity plus the `capacity_bonus` of every attached part.

Every weapon also reports what the game says about its size, price and uses, all whole numbers:

| Prop | What it is |
|---|---|
| `cells` | Grid cells the item fills in the bag (attachments not counted) |
| `reach` | The longer side of the box around those cells: a 4-long crowbar has 4, whichever way it lies |
| `breadth` | The shorter side of that box. A straight item has 1; a diagonal one has about its reach |
| `price` | The game's base price, before traders, party and condition change it |
| `kinds` | How many game item types it has besides weapon, melee weapon and firearm (including grades like junk) |
| `purposes` | How many of the game's main uses it has besides weapon: tool, household good, material and so on |

The framework's own data doesn't use them; a pack can, for example `"melee_sides": "2 + item.cells / 2"` in its `values`.
They are set after `item_stats`, so an `item_stats` number with one of these names is replaced in the game. To see them
for every melee weapon, use the debug panel's "Expeditions: log melee weapon facts" ([Debug panel](packs.md#debug-panel)).

**Everything else** is one stack per item id, with `item.count` the number carried. A **round** (ammunition with a
caliber; an ammo box is not one) is tagged `ammo` and has `kind`: 0 standard, 1 armor piercing (also tagged
`armor_piercing`), 2 non-lethal (also tagged `non_lethal`). Ammo boxes carried are opened on arrival.

Rounds match a gun by caliber (the `fits` condition and the `has_rounds` tag). The game sells no .45 or 12-gauge rounds,
so a gun of either caliber takes 10mm when none of its own are carried.

Every item also has `item.name` (its display name) and `item.id`. Everything else comes from packs' `item_stats`.

What a run writes onto an item (`set` or `add` on `item.loaded`, say) lasts until the run ends.

## Item groups and item stats

`item_groups` names a set of items. Each item in it gets the group as a tag:

```json
"item_groups": {
  "bright": ["flashlight", "flashbang_grenade"],
  "explosive": ["c4", "c4_set"]
}
```

`item_stats` gives items numbers and tags. A key is an item id, or `#tag` for every item with that tag. Numbers are
whole; `true`/`false` read as 1/0.

```json
"item_stats": {
  "#sharp": { "cut_bonus": 1 },
  "box_cutter": { "tags": ["melee"], "cut_bonus": 2 },
  "bandage_item": { "tags": ["medicine"], "heal_count": 1, "heal_sides": 6, "heal_flat": 2, "stops_bleeding": 1 }
}
```

An item gets its group tags first, then the `#tag` entries for the tags it has, then its own entry, so an item's own
numbers win. Entries from several packs merge: a later pack's number replaces an earlier one, and tags add up.

The framework's [main.json](../ExpeditionFramework/main.json) gives every `#melee` 4 `sides`, every `#firearm` 4
`capacity`, every `#ammo` 4 `rounds`, and specific numbers for the game's melee weapons, guns, magazines and medicine.

## Using items in actions

An action's `uses` picks the items it works with; one button appears per matching item. `uses.with` pairs a second
item, such as the rounds for a gun. See [Actions: items](actions.md#items).

Effects that pick items themselves:

```json
{ "pick_item": { "select": "#medicine", "order": "item.heal_flat", "do": [ ... ], "else": [ ... ] } }
{ "for_items": { "select": "#ammo", "do": [ { "add": { "var": "run.shells", "value": "item.count" } } ] } }
{ "equip": { "select": { "all": ["#firearm", { "not": "#in_hand" }] }, "then": [ { "say": "my_drew_gun" } ] } }
{ "consume": { "what": "with", "count": 2 } }
```

- `pick_item` runs `do` with the first match as `item` (or as `with`, given `"as": "with"`), or `else` with none.
- `for_items` runs `do` once per match.
- `equip` moves the first match it can into an empty hand, then runs `then` with it as `item`.
- `consume` uses up the action's `item` (or `with`): `{ "consume": 1 }`.

## Reloading

`{ "reload": true }` fills the action's `item` (a gun) from its `with` (ammo):

- Each ammo item is worth its `rounds` whatever gun it goes into. Reload takes as many rounds as fit, so it uses up the
  ammo itself: don't also `consume` it.
- Rounds already loaded stay, unless the new ammo is a different `kind`.
- No round is thrown away. An item opened but not emptied stays carried; its `spent` counts the rounds gone from it, and
  the next reload draws from it first.
- Ammo without `rounds` fills the gun with one item.
- An action with no `with` item fills the gun to `capacity` without using anything.
- Afterwards the gun's `loaded` holds its rounds, `round_kind` their kind, `item.$round` the ammo's name, and
  `run.reload_used` how many ammo items were used up.

The framework's `firearms` kit shows how it fits together: `reload` pairs a gun with ammo that `fits`, and `shoot`
needs `item.loaded > 0` and spends one.

## Loot tables

```json
"loot": {
  "cellar_scraps": { "items": [ { "item": "cup_noodle", "weight": 3 }, { "item": "bandage_item", "weight": 2 }, "flashlight" ] }
}
```

Each entry is an item id (weight 1), or an object with one of:

| Key | Rolls |
|---|---|
| `item` | That game item id. Lint can't check it: see [Item ids](#item-ids) |
| `loot` | Another loot table |
| `table` | A loot table or group the game itself knows |

plus `weight` (default 1) and `when` (the entry can only roll while it holds, such as `"mod(gunsmith_expansion)"` or
`"run.depth >= 3"`).

```json
{ "give_loot": "cellar_scraps" }
{ "give_loot": { "from": ["junk", "goods", "treasure"], "index": "run.depth / 2" } }
{ "give_loot": { "drops_of": "target" } }
{ "give_item": "crowbar" }
```

`from` picks one table of a list by `index` (clamped to the list), `drops_of` rolls an enemy's `drops` table, and
`give_item` gives one item by game id without rolling.

What a run gives goes into the night's ground window and is logged ("You found: ..."). `run.given` is 1 if the last
give worked and `run.found_items` counts what was found this run.

## Credits and losing things

| Effect | Does |
|---|---|
| `{ "credits": "2d20 + 10" }` | Gives credits; a negative amount takes them, and does nothing if the player can't pay |
| `{ "lose_items": 50 }` | Each carried piece (a stack counts as one; the backpack and the ground don't count) is destroyed with 50% chance |
| `{ "lose_items": { "per_cell": 10, "max": 90 } }` | As above, but each piece's chance is 10% per grid cell it takes up, attachments included, up to 90% |
| `{ "lose_found": true }` | Destroys everything found this run |
| `{ "take_back": { "then": [ ... ], "else": [ ... ] } }` | Destroys the newest item found on the ground; `run.$taken` is its name |
| `{ "clear_ground": true }` | Destroys everything on the ground; `run.cleared_ground` is how many |

`lose_items` logs each loss. With `night_log` (a [line](text.md#lines)) it also writes one line to the night log, with
`arg.lost` the count and `run.$lost` the names; `color` sets the line's colour.

## Trading

A **cart** is a trader's stock and budget:

```json
"carts": {
  "fence_cart": { "name": "sm_cart_fence", "budget": "150 - val.fence_markup", "stock": [ { "loot": ["market_scraps"], "count": 3 } ] }
}
```

| Field | What it does |
|---|---|
| `name` | Text key for the trader. Required |
| `budget` | How many credits the trader can spend buying from the player. An expression |
| `stock` | A list of `{ "item": id, "count": n }` or `{ "loot": [tables], "index": n, "count": n }`. `count` defaults to 1; `index` picks the table as in `give_loot` |

```json
{ "new_cart": "fence_cart" }
{ "trade": { "cart": "fence_cart", "on_close": [ ... ] } }
{ "take_cart": { "cart": "fence_cart", "do": [ ... ] } }
```

- `new_cart` stocks the cart, rolling its loot. Stocking it again in the same run replaces it.
- `trade` opens the game's own counters and negotiation screen: the cart goes on the front counter and the player drags
  items onto the back counter to buy or sell. Bought items land on the ground. While it is open, the actions window
  offers only "Open the deal" and "Done trading". When the player is done, `run.trade_cash` holds the change in their
  cash, `run.trade_bought` how many items they bought, and `on_close` runs.
- `take_cart` hands over what is left on the cart (for robbing the trader): `run.cart_taken` is how many items, `do`
  runs, then the items go on the ground and are logged.

A cart sells what it was stocked with, plus what the player sold it earlier in the run.

## Carrying things in hand

When the game asks which bag to take out at night, the framework opens an "In hand" window with two slots beside it.
Whatever is put there counts toward an area's [`packing`](areas.md#packing) needs alongside the chosen bag, so a player
without a bag, or with a gun too big for it, can still go. On arriving at any location that shows the night screen's
hands, the game's own included, the two items move into them, and the game brings them home with everything else.
Closing the menu without picking a bag, coming home, or going to bed puts them back in the inventory; the game doesn't
save the window.

The game has no hook for that menu, so this is the framework's only use of Harmony: three postfixes, on
`StoreResponse.ShowAfterhourResponse`, `ResponseList.CreateAfterhoursResponse` and `EmporiumEntry.HideResponseWindow`.
