# Expedition Framework

A Probably Stolen mod that plays night expeditions defined in data. A pack describes areas, rooms, enemies, events,
loot and traders in JSON; the framework puts the areas on the night map and runs them. Nothing about any particular
area, enemy or weapon lives in the engine, and a pack can extend anything in C# through the API.

Any mod that ships `ExpeditionFramework/main.json` is a pack. It needs no dependency on this mod: without the framework
installed, the folder is simply never read.

Status: 0.1.0. The engine passes its simulator. The in-game side builds and passes the mod scanner, but has not been
played.

## Install

Copy the built `ExpeditionFramework` folder into the game's `Mods` folder and enable it, together with any packs.

## Build

```bash
dotnet build -c Release
```

Builds and installs to `Mods/ExpeditionFramework`. See [Working on the framework](docs/contributing.md) for building
from elsewhere and running the tests.

## A pack in one file

```
MyPack/
  manifest.xml
  MyPack.dll               the game lists only mods with a DLL that has an IMod class, even an empty one
  ExpeditionFramework/
    main.json
  Localization/en.csv
```

```json
{
  "encounters": {
    "squatters": { "tags": ["garage"], "enemies": ["squatter"] }
  },
  "rooms": {
    "workshop": { "tags": ["garage"], "text": "gs_room_workshop", "enter": [ { "fight": { "tag": "garage" } } ] },
    "office": {
      "text": "gs_room_office",
      "interactions": [
        { "id": "take_cashbox", "label": "gs_action_cashbox", "group": "explore", "once": true,
          "do": [ { "credits": "2d10" }, { "leave": "cleared" } ] }
      ]
    }
  },
  "areas": {
    "old_garage": {
      "title": "gs_area_title",
      "player": { "hp": 20, "dodge": 10 },
      "kits": ["expedition_framework:explore", "expedition_framework:melee", "expedition_framework:medicine"],
      "layout": [ { "tag": "garage", "count": 2 }, { "room": "office" } ]
    }
  }
}
```

This is part of the [Starter example](docs/examples/Starter), which also defines the `squatter` enemy, an event and
the text. [Getting started](docs/getting-started.md) builds it step by step.

Check a pack without the game:

```bash
dotnet run -c Release --project tools/EfSim -- lint ../MyPack --strict
```

## Documentation

Everything else is in [docs/](docs/README.md):

- [Getting started](docs/getting-started.md), [Packs](docs/packs.md), [Areas](docs/areas.md),
  [How a run plays](docs/run-flow.md)
- [Rooms and events](docs/rooms-events.md), [Combat](docs/combat.md), [Items, loot and trade](docs/items.md),
  [Actions](docs/actions.md)
- Reference: [Expressions](docs/expressions.md), [Effects](docs/effects.md), [Text](docs/text.md),
  [Memory](docs/memory.md), [Game reads and reputation](docs/game.md), [Intel cards](docs/intel.md)
- [Testing with ef-sim](docs/testing.md), [C# API](docs/csharp-api.md)
