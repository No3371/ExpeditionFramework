# Working on the framework

For changes to the framework itself. Pack authors don't need this page.

## Building

```bash
dotnet build -c Release
```

This builds the framework and its API and installs them, with `manifest.xml`, `Localization/*.csv` and
`ExpeditionFramework/*.json`, to `Mods/ExpeditionFramework`. The project expects to sit at
`Modding/ExpeditionFramework` inside the game folder, and finds the game's assemblies at
`../../Probably Stolen_Data/Managed/`.

The game build also needs the installed ZoH Framework's `ZohFramework.Api.dll` (`Mods/ZohFramework/`). It is copied
as is into the install, never rebuilt, because every mod in the game shares the first copy that loads. Elsewhere pass
`-p:ZohApi=<path to ZohFramework.Api.dll>`. `tests/Sim` and `ef-sim` don't need it.

From anywhere else, such as a git worktree, pass both folders:

```bash
dotnet build -c Release -p:GameManaged="<game>\Probably Stolen_Data\Managed\\" -p:InstallDir=<scratch folder>/
```

Pointing `InstallDir` at a scratch folder keeps a test build out of the game's `Mods` folder. Build output (`bin`,
`obj`) is ignored by git.

`samples/SampleArea` is installed by hand: copy it to `Mods/SampleArea`.

## Layout

| Folder | What is in it |
|---|---|
| `api/` | `ExpeditionFramework.Api.dll`: the public interfaces C# packs build against |
| `src/Core/` | The engine, with no game dependencies: loader, expressions, built-ins, the run |
| `src/Game/` | Everything that touches the game: the map location, item reading, trade counters, hands window, save memory |
| `ExpeditionFramework/` | The framework's own pack: kits, intents, behaviors, item stats |
| `Localization/` | The framework's text |
| `samples/SampleArea/` | A complete example pack, played by `tests/Sim` |
| `tools/EfSim/` | `ef-sim`, the pack author's lint and run tool |
| `tests/Sim/` | The framework's own tests |
| `docs/` | This documentation, with loadable example packs in `docs/examples/` |

The main files in `src/Core`:

| File | What it does |
|---|---|
| `PackLoader.cs` | Reads JSON into definitions and compiles every effect, condition, line and filter, reporting problems with their place |
| `PackSet.cs` | Loads several packs together, applies patches, and drops a pack's patches if they add problems |
| `Schema.cs` | The allowed keys of each object, the tones, and the engine's item tags |
| `Expr.cs` | The expression language and text paths |
| `Builtins.cs` | Every built-in effect and condition |
| `ExpeditionRun.cs` | The run: rooms, events, fights, options, hooks |
| `Model.cs` | Definitions, the bridge to the game (`IBridge`), memory stores, game reads |

## Testing

```bash
dotnet run -c Release --project tests/Sim
```

Checks the framework's content, tests the loader's error reporting with small packs written in the test, then plays the
sample area (`SIM_RUNS` sets how many runs, default 5000). Run it after any change to `src/Core`.

Then lint and run the example packs and the sample with [ef-sim](testing.md):

```bash
dotnet run -c Release --project tools/EfSim -- lint docs/examples/Showcase --strict
```

```bash
dotnet run -c Release --project tools/EfSim -- run docs/examples/Showcase
```

`src/Game` can't be tested outside the game. A change there needs the build to succeed and a play in the game. For told
intel and the `ef.*` verbs, the debug panel's [in-game self-tests](testing.md#in-game-self-tests) do most of that play.

## Adding a built-in

A built-in effect or condition goes in `Builtins.cs`, registered like a mod's: a compiler from `ISpec` to a delegate.
Use only the public API where you can, so a mod could have written it too. Then:

- document it in [effects.md](effects.md) or [expressions.md](expressions.md#conditions);
- add a test to `tests/Sim` for what it does and for the problems it reports;
- if it needs something from the game, add it to `IBridge`, implement it in `src/Game/GameBridge.cs`, and give
  `ef-sim`'s `tools/EfSim/KitBridge.cs` a stand-in.

## Keep in step

Some lists live in more than one place:

- Tones: `Schema.Tones`, the colours in `ExpeditionLocation.ToneColors`, and [text.md](text.md#lines).
- Intel tones: `Schema.IntelTones` and `IntelCards.Tone`.
- New keys in an object: its `Schema` shape, or loading warns about them as unknown.
- Engine-kept run variables: `ExpeditionRun.EngineVars`, `ExpeditionRun.Get`, and
  [expressions.md](expressions.md#run-variables).
- Game reads: `GameValues.Get`, `GameReads`, `ef-sim`'s bridge, and [game.md](game.md).
