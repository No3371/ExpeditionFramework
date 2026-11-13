# Game reads and reputation

`game.NAME` reads the state of the player's game outside the run. It works in any expression, during a run and on the
map (`hidden_when`, intel `when`). Anything not listed here reads 0, and so does everything with no save loaded.

## What can be read

| Read | Value |
|---|---|
| `game.day` | The game's day counter |
| `game.luck` | The player's luck |
| `game.roll_luck` | Rolls the player's luck the way the game does: 1 on success, else 0. Rolls each time it is read |
| `game.wounds` | The player's wound state |
| `game.maimed` | 1 if the player has the maimed start |
| `game.clears` | How many times the player has cleared the area this run is in (on the map: the area being checked) |
| `game.clears.AREA` | How many times the player has cleared AREA, an area of the same pack |
| `game.rep.FACTION` | Reputation with a faction, as the game shows it: a whole number, cut toward zero |
| `game.rep_exact.FACTION` | Reputation in hundredths: 12.5 reads 1250. Rounded, halves away from zero |

**Factions:** `lower_level`, `upper_level`, `security`, `black_market`, `revolution`, `cartel`. The cartel exists only on
the drug dealer start; elsewhere it reads 0. A `game.rep.` or `game.rep_exact.` read of another name is a problem.

A run counts as a clear when the player leaves after it ended `cleared`, or with `run.cleared` above 0.

```json
"hidden_when": "game.rep.security <= -50",
"player": { "hp": 20, "charm": "game.rep_exact.black_market >= 1000 ? 2 : 0" },
"luck": { "value": "game.luck" }
```

In `ef-sim`, game reads come from `bridge.game` in `sim.json`. See [Testing: bridge](testing.md#bridge).

## The reputation effect

`reputation` changes the player's reputation with a faction and logs it to the night log the way the game logs its own
overnight changes: the game's own "gained" or "lost" text with the amount, green for a gain and red for a loss.

```json
{ "reputation": { "faction": "security", "value": -0.5 } }
{ "reputation": { "faction": "black_market", "value": "run.deals * 2" } }
{ "reputation": { "faction": "black_market", "value_x100": "run.deals * 25",
                  "night_log": { "text": "sm_rep_log", "args": ["run.$rep"] } } }
```

| Field | What it does |
|---|---|
| `faction` | The faction. Required |
| `value` | The amount: a whole-number expression, or a literal number with up to two decimals (`0.25`) |
| `value_x100` | The amount in hundredths, as an expression: `"run.deals * 25"` is a quarter point per deal |
| `night_log` | A [line](text.md#lines) to log instead of the game's text |
| `color` | The log line's colour (hex, without `#`) instead of green or red |
| `quiet` | `true`: change it without logging |

Give exactly one of `value` and `value_x100`. A literal `value` with more than two decimals is a problem; computed
fractions go through `value_x100`.

An amount of 0, or a faction the player doesn't have, changes and logs nothing.

With `night_log`, the line can show the amount three ways: `run.$rep` is the text the game would show (`+0.25`),
`arg.rep` the whole part, and `arg.rep_x100` the amount in hundredths.

The change goes through the game's own reputation code, as the game's own overnight changes do, so the game's perks
and difficulty can scale it. For a decimal amount the framework does what the game's whole-number path does: a
`lower_level` gain also adds wild favor, 40 per point, rounded. `ef-sim` doesn't scale anything; see
[Testing: bridge](testing.md#bridge).
