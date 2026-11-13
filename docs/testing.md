# Testing with ef-sim

`ef-sim` checks and plays a pack without the game. It loads the framework and your pack exactly as the game does, then
either reports what is wrong (`lint`) or plays many runs with a bot against a stand-in for the game (`run`).

`ef-sim` comes with the framework's source, in `tools/EfSim`, not with the installed mod. It needs the
[.NET 8 SDK](https://dotnet.microsoft.com/download) but not the game. The commands on this page run from the
framework's source folder, with the pack beside it as `../MyPack`. Build it once:

```bash
dotnet build -c Release tools/EfSim
```

## Lint

```bash
dotnet run -c Release --no-build --project tools/EfSim -- lint ../MyPack --strict
```

```
lint ef_showcase: 0 error(s), 0 warning(s)
```

Lint prints each load problem as `ERROR` and each warning as `WARN`, naming the file and the place in it:

```
ERROR ef_starter/ExpeditionFramework/main.json.areas.old_garage.layout[1]: no room named ef_starter:offise
lint ef_starter: 1 error(s), 0 warning(s)
```

It also reports:

- every text key the pack uses that is missing from `Localization/en.csv` (an error);
- for every other locale CSV, keys missing from it or not in `en.csv`, and keys whose `{n}` placeholders differ from
  `en.csv` (warnings).

| Option | What it does |
|---|---|
| `--strict` | Warnings fail too |
| `--mod ID` | Counts mod `ID` as installed: `mod(ID)` reads 1, includes that require it load, and `memory.ID.NAME` reads aren't warned about. Repeatable |

**What lint can't see.** Lint knows your pack, the framework and the text, but not the game's items. A misspelled
game item id (`"item": "crowbarr"`) passes lint and `ef-sim run`, and in the game gives nothing. See
[Item ids](items.md#item-ids).

Lint loads no pack but the framework and yours. With `--mod`, files that need that mod are checked too, but against
nothing from it. Lint a pack with and without each `--mod` it cares about.

Exit codes: 0 clean, 1 on errors (or on warnings with `--strict`), 2 on bad arguments.

## Run

```bash
dotnet run -c Release --no-build --project tools/EfSim -- run ../MyPack [--runs 200]
```

```
area               kit          bot        runs  cleared  knocked out  extracted
night_market       blade        random      400    92.5%         0.8%      99.3%
night_market       blade        careful     200   100.0%         0.0%     100.0%
packing: 3 case(s), 0 failed
loot: 1 case(s), 0 failed
ef-sim ef_showcase: all checks passed
```

`run` reads the pack's `sim.json`, plays each row of its run matrix, prints one line per row, then runs the checks.
`--runs` overrides every row's run count; small counts can miss rarely offered options.

In the table, **cleared** is a run that ended `cleared` or with `run.cleared` above 0; **knocked out** ended
`knocked_out`; **extracted** is any other ending, cleared runs included. A run stopped by a guard counts in none.

Each check that fails prints a `FAIL` line:

- load problems and missing text keys, as in `lint`;
- text missing, or given too few arguments, while playing;
- a label, info line or night-log line left with an unfilled `{`;
- a run that never ends (over 2000 steps), a scene with no options, or a run that throws;
- a decision for which no rule of the row's bot picks an option;
- every `expect.offered` id no run was offered;
- every [memory](#memory), [packing](#packing) or [loot](#loot) expectation that comes out different.

Load warnings print as `WARN` and never fail a run. Exit codes: 0 when all checks pass, 1 when any fails, 2 on bad
arguments or a missing or malformed `sim.json`.

## sim.json

The [Showcase example's sim.json](examples/Showcase/sim.json), shortened:

```json
{
  "version": 1,
  "kits": {
    "blade": {
      "credits": 100,
      "items": [
        { "id": "kitchen_knife", "tags": ["weapon", "melee", "in_hand"] },
        { "id": "bandage_item", "count": 2 }
      ]
    }
  },
  "runs": [
    { "area": "night_market", "kit": "blade", "runs": 400 }
  ],
  "expect": {
    "offered": ["press_on", "head_home", "strike", "heal", "hush_lookout", "trade_fence", "haggle"]
  }
}
```

| Key | What it is |
|---|---|
| `version` | `1`. Required |
| `kits` | Named sets of what the player carries. Required |
| `runs` | The run matrix: one row per area, kit and bot to play. Required |
| `bots` | Named [bots](#bots) |
| `expect` | `offered`, [`packing`](#packing), [`loot`](#loot) |
| `bridge` | How the stand-in game behaves. [Below](#bridge) |

Unknown keys are errors, and numbers must be whole.

**Kits.** A kit has `credits` (default 0) and `items`. Each item has `id` (required), `count` (default 1), `tags` and
`props` (numbers, such as `"loaded": 4`). Tags may start with `#`. A kit item's tags and props are layered over the pack's
`item_stats` for that item, the kit's props winning. Give each weapon the tags the game would (`weapon`, `melee` or
`firearm`, `in_hand`).

**Rows.** A row has `area`, `kit` and `runs` (above 0), all required, and optional `bot` (default `random`), `seed`
(default 42 plus the row's index) and [`memory`](#memory). An `area` without `:` is in your pack. `kit` may be a list:
run `n` uses `kit[n % length]`, so `["light", "heavy"]` alternates.

**`expect.offered`** lists action ids that some run must have been offered.

## Bots

A row's `bot` is `random` or a name from `bots`. The `random` bot presses a random offered button, skipping
`head_home` 95% of the time.

A named bot is a list of rules. For each decision it takes the first rule whose `when` holds and that finds an offered
option; a rule that finds nothing falls through. If no rule picks anything, the run stops with a `FAIL`.

```json
"bots": {
  "careful": [
    { "when": "player.hp * 2 <= player.max_hp", "pick": ["heal"] },
    { "when": "val.scene.combat && val.intent_ends.whistle > 0", "pick": ["hush_lookout"] },
    { "when": "val.scene.combat", "pick": ["strike", "punch"], "target": "weakest" },
    { "when": "val.scene.event", "pick": ["haggle", "trade_fence", "walk_away"] },
    { "pick": ["listen", "press_on", "slip_out"] }
  ]
}
```

| Key | What it does |
|---|---|
| `when` | An expression. Left out: always |
| `pick` | Action ids in priority order. The first id with an offered option wins, with its first such option |
| `first_except` | Instead of `pick`: the first offered option whose id isn't listed |
| `target` | `"weakest"`: `pick` only options aimed at the active enemy with the lowest hp |

In `when`, `self` and `player` are the player and `target` is the focused enemy. Paths and functions work as in pack
data, except `memory.`, which bots can't read. Bots also read:

| Name | Value |
|---|---|
| `val.scene.NAME` | 1 while the scene is `NAME` |
| `val.offered.ID` | How many offered options have id `ID` |
| `val.intent_ends.SUFFIX` | Active enemies whose intent id ends with `SUFFIX` |
| `val.foes_with.STAT` | Enemies in the fight, defeated or gone included, with `STAT` above 0 |
| `val.focus_is_weakest` | 1 if the focused enemy is the weakest active one (or there is none) |
| `val.loaded` | Rounds loaded in carried `firearm` items |
| `val.shots_left` | `loaded` plus the rounds in carried `ammo` items |
| `val.has_gun` | 1 if carrying a `firearm` item |
| `val.coin` | 1 or 0 at even odds, drawn each time it is read. `&&` and `\|\|` stop early, so put it last |

Mistakes in a bot (unknown keys, a `when` that doesn't compile, an unknown `val.` name, both or neither of `pick` and
`first_except`, an empty `pick`, a bad `target`, a bot named `random`, a bot with no rules, a row naming an unknown bot)
are errors (exit 2). After the table, each rule of a used bot that never picked anything prints a `WARN`: usually a
mistyped id or a condition that never holds.

## Memory

A run's memory starts empty and is dropped when the run ends, so every run plays a first night. A row's `memory`
changes that:

```json
{ "area": "night_market", "kit": "blade", "bot": "careful", "runs": 200,
  "memory": { "start": { "visits": 3, "$last_deal": "a deal on day 9" }, "carry": true, "expect": { "visits": 203 } } }
```

| Key | What it does |
|---|---|
| `start` | What memory holds before the row's runs, by name without `memory.`: `"visits": 3`, `"$rival": "Dex"` for text, `"other_pack.visits": 2` for another pack's. Without `carry`, every run starts from these |
| `carry` | `true`: the row's runs share one memory, each seeing what the runs before wrote, like nights in a row |
| `expect` | What memory must hold after the row's last run: a whole number, `{ "min": 1, "max": 5 }` (either may be left out), or text for a `$` name |

A pack named in any row's `start` or `expect` counts as installed, so `memory.PACK.NAME` reads of it aren't warned
about. `--runs` changes how many runs a carried row plays, so use `min` and `max` rather than an exact count where that
matters.

## Packing

`expect.packing` checks what an area's [`packing`](areas.md#packing) says a bag still needs, as the map does:

```json
"packing": [
  { "area": "night_market", "bag": [ { "id": "kitchen_knife", "tags": ["weapon", "melee"] } ], "missing": [] },
  { "area": "night_market", "bag": [], "missing": ["You need a weapon."] },
  { "area": "night_market", "missing": ["You need a weapon."] }
]
```

`missing` is the expected texts in order, as the player reads them; `[]` means ready to go. `bag` items take `id`,
`count`, `tags` and `props` like kit items, but aren't layered over `item_stats`: the case states everything the item
has. Leaving `bag` out means no backpack (the area's `no_backpack` text); `"bag": []` is an empty backpack.

## Loot

`expect.loot` rolls loot tables the way a run does and checks what comes out:

```json
"loot": [
  { "area": "night_market", "tables": ["market_scraps"], "rolls": 500, "must_roll": ["box_cutter"], "never_prefix": "gsx_" }
]
```

Each case needs `area`, `tables` and `rolls`, and at least one of `must_roll` (each id must roll at least once) and
`never_prefix` (no rolled id may start with it). `seed` defaults to 42; each case has its own random, so cases never
shift the runs. `mods` reloads the packs with those mods counted as installed, as `lint --mod` does, to test entries
that only roll with another mod.

## Bridge

The stand-in game reads every `game.NAME` as 0, never opens a trade screen, hands nothing over for `take_cart`, gives
back the newest found item for `take_back`, and rolls each carried item for `lose_items`. `bridge` changes that:

```json
"bridge": {
  "game": { "day": 12, "luck": 30, "rep.black_market": 25, "rep_exact.security": -4050, "roll_luck": "coin" },
  "trade": { "opens": true, "lose_cash_up_to": 20, "cart": ["cart_goods"] },
  "take_back": "oldest",
  "lose_carried": ["something"]
}
```

| Key | What it does |
|---|---|
| `game` | `game.NAME` values, named as after `game.`: a whole number, or `"coin"` for 0 or 1 at random each read. Unlisted names read 0 |
| `trade.opens` | `true` lets `trade` open. Closing it changes cash by minus a random 0 to `lose_cash_up_to` and buys nothing |
| `trade.cart` | The item names `take_cart` hands over. Default none |
| `take_back` | `newest` (default) or `oldest` |
| `lose_carried` | `roll` (default: each item is lost with the effect's chance), or a list of names reported lost each time, removing nothing |

Reputation starts from `rep_exact.FACTION` (hundredths) if given, else `rep.FACTION`, and both reads come from it. The
`reputation` effect changes the run's own copy, kept between -200 and 200, without the game's scaling, so later reads in
that run see the change. Every faction exists in the sim, the cartel too.

Draws come from the same random as dice and bots, so a setting that draws shifts every later roll of the run.

## The framework's own tests

```bash
dotnet run -c Release --project tests/Sim
```

Checks the framework's content, tests the loader's error reporting with small packs written in the test, then plays the
sample area with random choices (`SIM_RUNS` sets how many, default 5000). `tests/Sim -- lint FOLDER` runs the same lint
as `ef-sim`.
