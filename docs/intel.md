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
| `told` | `true`: when a mod that tells intel claims this pack, the slot's cards are handed out one at a time as customers tell them, and don't appear at the day's roll. Without a teller it rolls as usual. Default `false`. [Told slots](#told-slots) |

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

## Told slots

A slot with `"told": true` is one a customer can tell the player about during the day, instead of a card that is on the
map from the morning:

```json
{ "id": "rumour", "pool": "rumours", "told": true, "counts": [40, 40, 20],
  "title": "sm_rumour_title", "description": "sm_rumour_desc" }
```

Nothing changes until a mod that tells intel **claims** the pack. Unclaimed, a told slot rolls with the day like any other,
so a pack played without a teller mod keeps all its intel. Once the pack is claimed:

- The day's roll skips the pack's told slots. They show no card and draw no dice, like a slot whose `when` fails.
- Each time the teller tells, one card of one told slot is rolled right then and appears on the area's map card at once.
- The slot's own `chance` or `counts` is its budget for the day: how many cards it can hand out, rolled the first time
  the slot is asked that day. A slot with `"chance": 60` has one card to tell on 60% of days and none on the rest.
- `when` is checked at the moment of telling, not in the morning, so memory or [`game.`](game.md) values that change
  during the day count.
- `intel_max` counts the cards already showing in the area, rolled and told alike. An area at its limit tells nothing more.
- A slot names each id in its pool at most once a day.
- An area the player hasn't discovered yet (locked by `unlocked_by` or hidden by `hidden_when`) is never told about.

Nothing is hidden and nothing carries over: a card that wasn't told doesn't exist, there is no rolled card waiting out of
sight, and the next day every told slot starts again with a new budget. A told card is an ordinary card from then on:
`intel(id)`, `schedule` with `intel` and `spawn` with `intel` see it in a run that day, and it survives a reload.

A claim is made by the teller mod each time the game loads, before the day's first roll. If a pack is claimed after its
day was already rolled, the cards its told slots rolled that morning stay, and telling starts properly the next day.

`ef-sim` has no teller: with `"intel": true` it rolls told slots with the rest, as if every card had been told.

The debug panel's Modding category has a stand-in teller for each pack with a told slot: "Expeditions: claim intel (pack)"
claims it for the session, and "Expeditions: tell intel (pack)" tells one card and writes what it told to the game log.

The ZoH verbs that tell are in [Telling intel through ZoH](zoh.md).

## Limiting the cards

An area can cap how many cards it shows a day, across all its slots:

```json
"intel_max": 2
```

The value is a number or an expression. Every slot rolls as usual; when more cards come up than the limit, earlier slots in
the `intel` list keep theirs first, and within a slot its first picks. Put the slots that matter most at the top. Below 0
reads as 0. Without `intel_max` there is no limit. A template's `intel_max` can be overridden by an area that extends it.

It is checked when the day is rolled, like `when`: `memory.` and [`game.`](game.md) work, `run.` reads 0. The day's cards
come before any run, and a run's rooms are only laid out when the run begins, so `run.rooms` is not known yet. Write the
number: half the rooms, rounded down, so a layout of 4 floors plus a final room makes 5 rooms and `"intel_max": 2`. A layout
with `when` rooms or `unique` pools can lay out fewer rooms than the constant you counted. Lint warns when `intel_max`
reads `run.`, and when an area has `intel_max` but no `intel`. Slot ids in one area must be distinct; lint reports a repeat.

The limit drops picks, so `intel(id)`, `schedule` with `intel` and ef-sim's `intel.SLOT` see only the cards that show.

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
