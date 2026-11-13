# Text and localization

Every string a player sees is a **text key** looked up in the pack's `Localization` files. Data never holds the words
themselves, so a pack can be translated without touching its JSON.

## The CSV files

```
Localization/
  en.csv
  zh.csv
```

Each line is a key, a comma, and the text. Only the first comma splits, so the text may contain commas. Blank lines and
lines starting with `#` are skipped.

Write the text bare, without quotes. They are never needed, and the two readers disagree about them: the game removes
one pair of quotes around the whole text, but `ef-sim` keeps them, so its output then differs from the game's. A `\n`
in the text is a line break in the game.

```
# The night market
sm_area_title,Night Market
sm_room_stalls,Rows of tarps and folding tables. Someone is selling batteries by the handful.
sm_hud_heat,Heat: {0}
```

`{0}`, `{1}` and so on are filled from the line's `args`, in order. `en.csv` is the reference: every key the pack uses
must be in it. Other files, such as `zh.csv`, are translations with the same keys. The game picks the file for the
player's language.

[`ef-sim lint`](testing.md#lint) reports a key missing from `en.csv` as an error, and a translation that is missing
keys, has extra keys, or uses different `{n}` placeholders from `en.csv` as a warning.

## Lines

Wherever data shows text (`label`, `text`, `say`, HUD parts, `info`), it takes a **line**:

```json
"sm_nothing"
{ "text": "sm_hud_heat", "args": ["run.heat"], "tone": "bad" }
{ "text": "sm_rumour", "variants": 3 }
{ "cases": [
  { "when": "run.heat >= 3", "text": "sm_gate_hot", "tone": "bad" },
  { "text": "sm_gate_calm" }
] }
```

| Field | What it does |
|---|---|
| `text` (or `key`) | The text key |
| `args` | Values for `{0}`, `{1}`, ... [Below](#arguments) |
| `variants` | Picks one of `key_0` to `key_{N-1}` at random each time. Each must be in the CSV |
| `tone` | The colour, where the line is logged or shown |
| `cases` | A list of lines, each with an optional `when`. The first whose `when` holds is used; if none holds, nothing is shown |

A `tone` on the `cases` object is the default for cases that don't set one. A line's own tone wins over the tone
`say` gives it.

**Tones:** `good`, `bad`, `dim`, `heading`, `enemy`, `hint`, `plain`. An unknown tone is a warning.

### Keys from other packs

A key is your pack's unless it names a pack: `"expedition_framework:ef_leave"`. The framework's own keys are in its
[Localization/en.csv](../Localization/en.csv); an area can replace them with its [`texts`](areas.md#framework-texts).

### Key templates

A key with `{path}` in it is filled in when shown:

```json
{ "text": "{self}_look" }
{ "text": "{area}_title" }
{ "text": "ne_forecast_{run.$pick}_title" }
```

`{self}`, `{target}`, `{player}`, `{item}`, `{with}` and `{role.NAME}` give the id (without its pack); `{area}` the
area's id. Any [text path](#arguments) works. Lint can't check keys built this way, so test them with a run.

## Arguments

Each entry in `args` is one of:

| Argument | Gives |
|---|---|
| `"self.name"`, `"target.name"`, `"item.name"`, `"role.rival.name"` | A display name |
| `"target.id"`, `"area.id"` | An id, without its pack |
| `"run.$NAME"`, `"self.$NAME"`, `"item.$NAME"` | A text variable |
| `"memory.$NAME"`, `"memory.PACK.$NAME"` | A text kept in [memory](memory.md) |
| `"percent"` | The action's odds (in a label or `info`) |
| Any other string or number | An [expression](expressions.md): `"run.heat"`, `"player.max_hp - player.hp"` |
| `{ "if": c, "then": arg, "else": arg }` | One argument or the other |
| `{ "bar": ["player.hp", "player.max_hp"] }` | A ten-cell bar such as `[######....]` |
| `{ "items": filter }` | The names of carried items matching the [filter](items.md#item-filters), joined with commas |
| `{ "list": "witnesses" }` | The names of everyone on the [list](combat.md#roles-and-lists) |
| `{ "scheduled": "key_{run.$pick}" }` | Every [scheduled event](rooms-events.md#scheduling-events), each through that key with `run.$pick` set to its id |
| A line object | Another line, rendered and put in as text |

## Text variables

Text variables hold words, not numbers. Their names start with `$`:

```json
{ "set_text": { "var": "run.$who", "value": "target.name" } }
{ "set_text": { "var": "memory.$last_deal", "line": { "text": "sm_deal_note", "args": ["game.day"] } } }
```

`set_text` writes `run.$NAME`, `memory.$NAME`, or `$NAME` on `self`, `target`, `player`, `item`, `with` or
`role.NAME`, from a text path (`value`) or a rendered line (`line`). A line saved this way is stored in the player's
language at the time it was written.

## Drawing

Labels, info and the HUD are drawn without rolling: dice read 1 and `variants` shows the first one. See
[Actions](actions.md#drawn-without-rolling).
