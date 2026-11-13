# Getting started

This page builds a small pack, the Old Garage, and checks it without starting the game. The finished pack is
[examples/Starter](examples/Starter).

## What a pack is

A pack is an ordinary Probably Stolen mod with an `ExpeditionFramework/main.json` file. The framework finds every
enabled mod that has one and adds its areas to the night map. A pack needs no code and no reference to the framework:
if the framework isn't installed, the folder is never read.

```
MyPack/
  manifest.xml
  MyPack.dll                    an empty IMod class; see below
  ExpeditionFramework/
    main.json                   your content
  Localization/
    en.csv                      every string the player sees
  sim.json                      optional: how ef-sim tests the pack
```

The game lists a mod only if it ships a DLL with an `IMod` class, so even a data-only pack needs one. An empty class is
enough:

```csharp
public class ModEntry : IMod
{
    public void Init(ModManifest manifest) { }
    public void OnEnable() { }
    public void OnDisable() { }
}
```

List the DLL under `<Assemblies>` in `manifest.xml`. List `expedition_framework` under `<Prerequisites>` only if the mod
is useless without it.

## 1. The manifest

```xml
<Manifest>
  <ID>ef_starter</ID>
  <Name>Expedition Starter: The Old Garage</Name>
  ...
  <Prerequisites>
    <Mod>expedition_framework</Mod>
  </Prerequisites>
  <Assemblies>
    <Assembly>EfStarter.dll</Assembly>
  </Assemblies>
</Manifest>
```

The `<ID>` is your **pack id**. Every id you define belongs to it: `squatter` in this pack is `ef_starter:squatter`
everywhere else.

## 2. An area

An area is one destination on the night map. It needs a title, the player's starting stats, and a layout of rooms.

```json
"areas": {
  "old_garage": {
    "title": "gs_area_title",
    "player": { "hp": 20, "dodge": 10 },
    "kits": ["expedition_framework:explore", "expedition_framework:melee", "expedition_framework:medicine"],
    "layout": [
      { "tag": "garage", "count": 2 },
      { "room": "office" }
    ]
  }
}
```

- `title` is a text key, looked up in `Localization/en.csv`.
- `kits` add ready-made actions. The framework's own kits give "Press on" and "Head home" (`explore`), melee attacks
  (`melee`) and healing (`medicine`). See [Areas](areas.md#kits).
- `layout` picks the rooms for one run: two different rooms tagged `garage`, then the room `office`.

## 3. Rooms, a fight and an event

```json
"rooms": {
  "workshop": { "tags": ["garage"], "text": "gs_room_workshop", "enter": [ { "fight": { "tag": "garage" } } ] },
  "pit": { "tags": ["garage"], "text": "gs_room_pit", "enter": [ { "event": { "tag": "garage" } } ] },
  "office": {
    "text": "gs_room_office",
    "interactions": [
      { "id": "take_cashbox", "label": "gs_action_cashbox", "group": "explore", "once": true,
        "do": [ { "credits": "2d10" }, { "add": { "var": "memory.cashboxes", "value": 1 } }, { "leave": "cleared" } ] }
    ]
  }
}
```

A room shows its `text`, runs its `enter` effects, and offers its `interactions` as buttons. Entering the workshop
starts a fight with an encounter tagged `garage`; the pit starts an event tagged `garage`. The office's button pays out,
counts the cash box in [pack memory](memory.md), and ends the run as `cleared`.

The fight and the event:

```json
"enemies": {
  "squatter": {
    "name": "gs_enemy_squatter",
    "tags": ["human"],
    "stats": { "hp": 6, "attack": 1, "accuracy": 0, "dodge": 5 },
    "behavior": "expedition_framework:brawler",
    "on": { "defeated": [ { "give_loot": "garage_junk" } ] }
  }
},
"encounters": {
  "squatters": { "tags": ["garage"], "enemies": ["squatter"] }
},
"events": {
  "toolbox": {
    "tags": ["garage"],
    "text": "gs_event_toolbox",
    "choices": [
      { "id": "open_toolbox", "label": "gs_choice_open", "group": "explore",
        "check": { "chance": "70",
          "success": [ { "give_loot": "garage_junk" } ],
          "failure": [ { "say": { "text": "gs_toolbox_jammed", "tone": "bad" } } ] } },
      { "id": "leave_toolbox", "label": "gs_choice_leave", "group": "move", "do": [ { "say": "gs_left_alone" } ] }
    ]
  }
},
"loot": {
  "garage_junk": { "items": [ { "item": "cup_noodle", "weight": 3 }, { "item": "bandage_item", "weight": 2 }, "crowbar" ] }
}
```

`behavior` borrows the framework's `brawler`, which attacks and sometimes winds up a heavy blow. The toolbox's first
choice has a `check`, so its button shows the odds ("70% chance") beside it.

## 4. Text

Every string the player reads is a key in `Localization/en.csv`, one `key,text` per line:

```
gs_area_title,The Old Garage
gs_enemy_squatter,Squatter
gs_event_toolbox,A rusted toolbox sits under the workbench.
```

See [Text and localization](text.md) for arguments, variants and other languages.

## 5. Check it

`ef-sim` loads your pack the way the game does and reports every problem with its location. Build it once from the
framework's folder:

```bash
dotnet build -c Release tools/EfSim
```

Then lint the pack:

```bash
dotnet run -c Release --project tools/EfSim -- lint ../MyPack --strict
```

```
lint ef_starter: 0 error(s), 0 warning(s)
```

Add a `sim.json` to play the pack hundreds of times with a bot and check that every action was offered:

```json
{
  "version": 1,
  "kits": {
    "knife": { "items": [ { "id": "kitchen_knife", "tags": ["melee", "in_hand"] }, { "id": "bandage_item", "count": 2 } ] }
  },
  "runs": [ { "area": "old_garage", "kit": "knife", "runs": 500 } ],
  "expect": { "offered": ["press_on", "head_home", "strike", "heal", "open_toolbox", "leave_toolbox", "take_cashbox"] }
}
```

```bash
dotnet run -c Release --project tools/EfSim -- run ../MyPack
```

```
area               kit          bot        runs  cleared  knocked out  extracted
old_garage         knife        random      500    89.8%         1.6%      98.4%
ef-sim ef_starter: all checks passed
```

[Testing with ef-sim](testing.md) covers bots, memory, packing and loot checks.

## 6. Install

Copy the pack folder into the game's `Mods` folder next to `ExpeditionFramework`, with your DLL, and enable both mods.
Problems are written to the game's mod log; a pack with problems adds no areas, and the log says why.

## Next

- [Packs](packs.md): splitting content into files, supporting other mods, patching other packs.
- [How a run plays](run-flow.md): rooms, scenes, fights and hooks, in the order they happen.
- [Effects reference](effects.md) and [Expressions](expressions.md): the vocabulary.
- [examples/Showcase](examples/Showcase): a larger pack that uses memory, reputation, intel, trade and the HUD.
