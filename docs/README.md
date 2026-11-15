# Expedition Framework documentation

Start with [Getting started](getting-started.md): it builds a small working pack step by step. Then read the
"Writing a pack" pages in order; the reference pages are for looking things up.

## Writing a pack

| Page | What it covers |
|---|---|
| [Getting started](getting-started.md) | A first pack: manifest, area, rooms, a fight, an event, text, testing, installing |
| [Packs](packs.md) | Files and sections, ids, problems and warnings, `include`, load order, shared pieces, templates, patching other packs |
| [Areas](areas.md) | Areas, layouts, kits, pools, the map, packing, the HUD, button groups, luck |
| [How a run plays](run-flow.md) | The order of everything in a run, fights, outcomes, hooks |
| [Rooms and events](rooms-events.md) | Rooms, events, event scenes, scheduling |
| [Actions](actions.md) | Buttons: scenes, targets, items, checks, confirm, the info panel |
| [Combat](combat.md) | Encounters, enemies, intents, behaviors, statuses, focus, roles and lists |
| [Items, loot and trade](items.md) | Item filters, item stats, reloading, loot tables, credits, trading, carrying things in hand |

## Reference

| Page | What it covers |
|---|---|
| [Expressions and conditions](expressions.md) | Operators, paths, functions, conditions, run variables |
| [Effects](effects.md) | Every built-in effect |
| [Text and localization](text.md) | CSV files, lines, arguments, key templates, tones |
| [Memory across nights](memory.md) | `memory.NAME`, `memory.$NAME`, reading other packs' memory |
| [Game reads and reputation](game.md) | `game.day`, `game.clears`, `game.rep`, and the `reputation` effect |
| [Intel cards](intel.md) | Daily rumours on the map and using them in a run |
| [Telling intel through ZoH](zoh.md) | `uses:`, `/ef.rumour`, `/ef.tell`: customers who tell a pack's told intel |
| [Testing with ef-sim](testing.md) | `lint`, `run`, `sim.json`, bots |

## C# and the framework itself

| Page | What it covers |
|---|---|
| [C# API](csharp-api.md) | Adding effects, conditions and hooks; listeners; decisions |
| [Working on the framework](contributing.md) | Building, layout, tests, adding built-ins |

## Examples

Both examples load cleanly with `ef-sim lint --strict` and pass `ef-sim run`.

- [examples/Starter](examples/Starter): the pack from [Getting started](getting-started.md). One area, three rooms, a
  fight, an event, and the small C# project that builds the pack's DLL and installs it. Copy it to start your own.
- [examples/Showcase](examples/Showcase): most features in one area: shared effects and values, templates, an enemy
  action with an info panel, an intent, a trader, a haggling check, pack memory, a cross-pack read, game reads,
  reputation, intel, packing, a custom HUD, and a `sim.json` with a bot, memory, packing, loot and bridge settings.
- [samples/SampleArea](../samples/SampleArea): the framework's own sample, played by its tests.
