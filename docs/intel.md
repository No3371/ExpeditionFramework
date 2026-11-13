# Intel cards

Intel is what the street says about an area today: cards on the area's map card, such as "The fence is in" or "Something
big is nesting in the cellar". Each day the framework picks what the cards name from one of the area's
[pools](areas.md#pools), and a run that day can act on the same picks.

```json
"pools": { "fences": ["fence"] },
"intel": [
  {
    "id": "fence", "pool": "fences", "chance": 60, "tone": "good",
    "when": "memory.visits > 0",
    "title": "sm_intel_fence_title",
    "description": { "text": "sm_intel_fence_desc", "args": ["memory.visits"] }
  }
]
```

## Slot fields

Each entry in `intel` is a **slot**:

| Field | What it does |
|---|---|
| `id` | The slot's name, read by `intel(id)`. Required |
| `pool` | One of this area's pools; the cards name ids from it. Required |
| `title`, `description` | [Lines](text.md#lines) for the card. Required |
| `effect` | An optional line shown as the card's effect |
| `chance` | Percent chance of one pick today. Default 100 |
| `counts` | Instead of `chance`: percent chances of 0, 1, 2, ... picks. `[35, 45, 20]` gives none 35%, one 45%, two 20% |
| `tone` | The card's tone: `good`, `bad` or `mixed` (default) |
| `good` | Ids from the pool whose cards show as `good` whatever the slot's tone |
| `when` | A condition; while it doesn't hold, the slot picks nothing that day |

Picks from one slot are different ids. A slot can show at most one card per possible pick: one with `chance`,
`counts` length minus one with `counts`.

## The picked id in text

While a card is drawn, `run.$pick` holds the id it names (without its pack). Use it in key templates to give each pick
its own text:

```json
"title": "ne_forecast_{run.$pick}_title",
"description": "ne_forecast_{run.$pick}_desc"
```

Lint can't check keys built this way. Every id in the pool needs its keys.

## When it is rolled

Intel is rolled once per day, the first time the game asks for the area's cards that day, and kept until the next day. `when` is
checked at that moment, on the map, where `run.` reads 0 but `memory.` and [`game.`](game.md) work. So what a run
writes to memory tonight changes the cards from the next day.

## Using intel in a run

During a run, the area's slots hold the ids their cards named today:

| Where | What |
|---|---|
| `intel(id)` | How many picks slot `id` has today |
| `{ "schedule": { "intel": "rooms", "depths": "run.rooms" } }` | Puts each picked event at a different random depth. [Scheduling](rooms-events.md#scheduling-events) |
| `{ "spawn": { "intel": "boss" } }`, `{ "cast": { "as": "boss", "intel": "boss" } }` | Picks from the enemies named |

```json
{ "if": { "when": { "any": ["intel(fence) > 0", { "chance": 50 }] },
          "then": [ { "event": "fence" } ],
          "else": [ { "say": "sm_alley_empty" } ] } }
```

`ef-sim run` rolls no intel unless a row sets `"intel": true`; then `intel(id)` and `schedule` with `intel` work as on
the map. See [Testing: nights in a row](testing.md#nights-in-a-row).
