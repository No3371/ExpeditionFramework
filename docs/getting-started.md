# Getting started

This page builds a small pack, the Old Garage, and checks it without starting the game. The finished pack is
[examples/Starter](examples/Starter).

## What a pack is

A pack is an ordinary Probably Stolen mod with an `ExpeditionFramework/main.json` file. The framework finds every
enabled mod that has one and adds its areas to the night map. A pack needs no code of its own and no reference to the
framework: if the framework isn't installed, the folder is never read.

```
MyPack/
  manifest.xml
  ExpeditionFramework/
    main.json                   your content
  Localization/
    en.csv                      every string the player sees
  MyPack.csproj                 builds MyPack.dll and installs the pack; see below
  ModEntry.cs                   an empty IMod class
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

`IMod` and `ModManifest` come from the game's `Assembly-CSharp.dll` and are in the global namespace, so no `using` is
needed.

You don't have to write the project yourself. Copy [examples/Starter](examples/Starter) to
`Probably Stolen/Modding/MyPack` (the project finds the game two folders up) and change three things:

- `EfStarter.csproj` to `MyPack.csproj`. The DLL is named after the project, so this makes `MyPack.dll`;
- `<Assembly>EfStarter.dll</Assembly>` in `manifest.xml` to `MyPack.dll`, and `<ID>` to your own id;
- `<InstallDir>` in the csproj to `..\..\Mods\MyPack\`.

The project is the [.NET SDK](https://dotnet.microsoft.com/download)'s usual kind. It targets `netstandard2.1`,
references `Assembly-CSharp.dll` from `Probably Stolen_Data/Managed`, and after each build copies the DLL,
`manifest.xml`, `ExpeditionFramework/*.json` and `Localization/*.csv` into the game's `Mods` folder. Step 6 runs it.
The copy also brings the Starter's finished `main.json` and text, which the steps below walk through.

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
- `kits` add ready-made actions. An **action** is a button the player can press; in the JSON it is called an
  `interaction`. The framework's own kits give "Press on" and "Head home" (`explore`), melee attacks (`melee`) and
  healing (`medicine`). See [Areas](areas.md#kits).
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

The office's action, field by field:

- `id` names the action, so tests can refer to it (step 5); `label` is the text key on the button.
- `group` is the heading the button sits under in the actions window ([button groups](areas.md#button-groups)).
- `once: true` means the button can be pressed once per room.
- `do` is the list of [effects](effects.md) run when it is pressed.
- `"2d10"` is a dice roll: two ten-sided dice, added. Any number in the data can be an
  [expression](expressions.md) like this.
- `memory.cashboxes` is a number kept in the player's save from night to night ([memory](memory.md)). Nothing in
  this pack reads it: it is here because the [Showcase](examples/Showcase) example reads it from this pack. Leave it
  out of your own pack if you don't need it.

[Actions](actions.md) has every field an action can have.

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

`behavior` borrows the framework's `brawler`, which attacks and sometimes winds up a heavy blow. An event's `choices`
are actions too, shown while the event is open.

The toolbox's first choice has a `check`: a roll that runs `success` or `failure`. `"chance": "70"` succeeds 70 times
in 100, and the button shows the odds ("70% chance") beside it. `say` writes a line to the expedition's log; its
`tone` (`good`, `bad`, `dim` and others) colours it. See [Check](effects.md#check) and [tones](text.md#lines).

The loot table's `cup_noodle`, `bandage_item` and `crowbar` are the game's own item ids. Lint can't check them: a
misspelled id passes lint, gives the player nothing, and is logged as an unknown item id in the game. [Item ids](items.md#item-ids) says where to find them.

## 4. Text

Every string the player reads is a key in `Localization/en.csv`, one `key,text` per line:

```
gs_area_title,The Old Garage
gs_enemy_squatter,Squatter
gs_event_toolbox,A rusted toolbox sits under the workbench.
```

See [Text and localization](text.md) for arguments, variants and other languages.

## 5. Check it

`ef-sim` loads your pack the way the game does and reports every problem with its location. It isn't part of the
installed mod: it comes with the framework's source, in `tools/EfSim`, and needs the
[.NET 8 SDK](https://dotnet.microsoft.com/download) but not the game. Put the framework's source next to your pack, at
`Probably Stolen/Modding/ExpeditionFramework`, and run these commands from that folder. Build `ef-sim` once:

```bash
dotnet build -c Release tools/EfSim
```

Then lint the pack. `--no-build` reuses that build, so it starts quickly; build again after updating the framework.

```bash
dotnet run -c Release --no-build --project tools/EfSim -- lint ../MyPack --strict
```

```
lint ef_starter: 0 error(s), 0 warning(s)
```

Lint catches misspelled ids of your own, missing text keys, unknown fields and wrong types. It can't catch a
misspelled game item id (see step 3).

Add a `sim.json` to play the pack hundreds of times with a bot and check that every action was offered:

```json
{
  "version": 1,
  "kits": {
    "knife": { "items": [ { "id": "kitchen_knife", "tags": ["weapon", "melee", "in_hand"] }, { "id": "bandage_item", "count": 2 } ] }
  },
  "runs": [ { "area": "old_garage", "kit": "knife", "runs": 500 } ],
  "expect": { "offered": ["press_on", "head_home", "strike", "heal", "open_toolbox", "leave_toolbox", "take_cashbox"] }
}
```

A `sim.json` kit is what the bot carries, not an area's kit: here a kitchen knife in hand, with the tags the game
gives a melee weapon, and two bandages. Each row of `runs` plays an area that many times. `expect.offered` lists action
ids that some run must have been offered, so an action that can never appear fails the test.

```bash
dotnet run -c Release --no-build --project tools/EfSim -- run ../MyPack
```

```
area               kit          bot        runs  cleared  knocked out  extracted
old_garage         knife        random      500    89.8%         1.6%      98.4%
ef-sim ef_starter: all checks passed
```

[Testing with ef-sim](testing.md) covers bots, memory, packing and loot checks.

## 6. Install

From your pack's folder, build the DLL:

```bash
dotnet build -c Release
```

This builds `MyPack.dll` and copies it, with `manifest.xml`, `ExpeditionFramework/*.json` and `Localization/*.csv`, to
`Probably Stolen/Mods/MyPack`. Run it again after every change to the data. In the game, enable the Expedition
Framework and your pack.

The framework writes to the game's log, `Player.log` in
`%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\` on Windows. Its lines start with
`[Expedition Framework]`, and `Expedition packs:` lists every pack it found. A pack with problems adds no areas; the
log lists each problem, then says the pack's areas were not added.

## Next

- [Packs](packs.md): splitting content into files, supporting other mods, patching other packs.
- [How a run plays](run-flow.md): rooms, scenes, fights and hooks, in the order they happen.
- [Effects reference](effects.md) and [Expressions](expressions.md): the vocabulary.
- [examples/Showcase](examples/Showcase): a larger pack that uses memory, reputation, intel, trade and the HUD.
