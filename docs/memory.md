# Memory across nights

`run.` variables last one run. `memory.` values stay in the player's save from night to night, so a pack can keep a
count, remember a name, or open something up after an earlier night.

```json
{ "add": { "var": "memory.visits", "value": 1 } }
{ "set_text": { "var": "memory.$last_deal", "line": { "text": "sm_deal_note", "args": ["game.day"] } } }
{ "if": { "when": "memory.visits > 1", "then": [ { "say": { "text": "sm_greet_back", "args": ["memory.visits", "memory.$last_deal"] } } ] } }
```

## Numbers and text

`memory.NAME` holds a whole number and `memory.$NAME` holds text. They are separate values, even with the same NAME.
Use them as you would `run.NAME` and `run.$NAME`: in expressions and conditions, as the `var` of `set`, `add` and
`set_text`, and in text `args`. A name never written reads 0, or "" as text.

Names are fixed: write each one out in full in the data. A name has no `.` in it.

## Each pack has its own

Two packs that both write `memory.visits` keep separate counts. A pack's memory is keyed by its manifest `<ID>`.

Another pack's memory can be read but never changed: `memory.PACK.NAME` and `memory.PACK.$NAME` read pack `PACK`'s
values wherever `memory.NAME` reads. Using one as the `var` of `set`, `add` or `set_text` is a problem.

Reading another pack is optional. If `PACK` isn't installed, its memory reads 0 (or "") and loading warns:
`memory.other.visits reads pack other, which isn't loaded, so it reads 0`. To avoid the warning, put the reads in an
[`include`](packs.md#include) that `requires` the pack, so the file loads only when the pack is there. The Showcase
example does this in [with_starter.json](examples/Showcase/ExpeditionFramework/with_starter.json):

```json
"when": "memory.ef_starter.cashboxes > 0"
```

## When it is saved

A write goes into the save straight away, so it stays however the run ends. The game keeps memory in the save's mod data
under the framework's mod id, so every save has its own and a new game starts with none. With no save loaded, memory
reads 0 and "" and writes do nothing.

## On the map

The map reads memory too: an area's [`hidden_when`](areas.md#on-the-map), and an [intel](intel.md) slot's `when`. On the
map no run exists, so `run.` reads 0 there. Intel is rolled once a day, so what a run writes tonight changes the street's
cards from the next day.

## Checks

Loading warns about a `memory.` name a pack reads but never writes, such as
`memory.ghost is read but never written by this pack, so it always reads 0`, or
`memory.other.ghost is read but never written by pack other, so it always reads 0` for another pack's. It is usually a typo.
`ef-sim lint --strict` fails on it.

In `ef-sim`, memory starts empty for each run unless `sim.json` seeds it or carries it from run to run. See
[Testing: memory](testing.md#memory).

## From C#

A C# mod reads and writes memory through the same paths, by compiling them in its pack. See
[C# API: memory](csharp-api.md#memory).
