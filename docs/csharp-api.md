# C# API

Data and C# are peers: a pack can use either or both. Every built-in effect and condition is written against the same
public API, in [src/Core/Builtins.cs](../src/Core/Builtins.cs), so a mod can do anything they do. The API itself is one
file: [api/ExpeditionApi.cs](../api/ExpeditionApi.cs).

## Setting up

1. Reference `ExpeditionFramework.Api.dll`, ship it, and list it in your manifest's `<Assemblies>`. Keep it current:
   `ISpec` only ever gains members, and if an older copy shipped by some pack is the one that loads, calls to newer
   members fail.
2. Implement `IExpeditionExtension` on your mod's `IMod` class.
3. The framework calls `RegisterExpeditions` once, just before your pack's data loads, so data can use what you
   register.

```csharp
public class ModEntry : IMod, IExpeditionExtension
{
    public void Init(ModManifest manifest) { }
    public void OnEnable() { }
    public void OnDisable() { }

    public void RegisterExpeditions(IExpeditionRegistrar registrar)
    {
        // Used from data as { "drain_power": { "amount": 2 } }.
        registrar.AddEffect("drain_power", spec =>
        {
            Value amount = spec.Value("amount", 1);
            return scope => scope.Run.Set("power", scope.Run.Get("power") - amount(scope));
        });

        // Armored enemies take 2 less damage from everything.
        registrar.Intercept("damage", decision =>
        {
            if (decision.Scope.Target.HasTag("armored")) decision.Value -= 2;
        });

        registrar.On("defeated", scope => scope.Run.Inventory.AddCredits(5));
    }
}
```

## Registering

| Method | What it adds |
|---|---|
| `AddEffect(name, compiler)` | An effect, `packid:name` (just `name` from your own data) |
| `AddCondition(name, compiler)` | A condition, named the same way |
| `AddHook(name)` | A hook, `packid:name`. Fire it with `run.Emit` or the `emit` effect; data can attach to it under `on` |
| `On(hook, effect)` | C# that runs on a hook, after the data attached to it |
| `Intercept(decision, interceptor, order)` | C# that changes a [decision](#decisions) before the run acts on it. Lower `order` runs first |

`On` and `Intercept` apply to every expedition from every pack. Check `scope.Run.AreaId` to limit one to your areas.
A handler that throws is logged in the run and skipped, and one that keeps triggering itself is stopped. To attach data
to another mod's hook, load after that mod.

## Compiling from data

An effect or condition is compiled once, when packs load, from an `ISpec`: the arguments as written in the data. The
compiler returns a delegate the run calls later. Mistakes found while compiling are reported with their place, like the
built-ins' are.

| `ISpec` member | Reads |
|---|---|
| `Value(key, fallback)` | An [expression](expressions.md) |
| `Condition(key)`, `Conditions(key)` | A condition, or a list of them |
| `Effect(key)` | Effects |
| `Items(key)`, `Use(key)` | An item filter, or an item use (`select`, `order`, ...). `Use()` reads the spec's own fields |
| `Line(key)`, `InlineLine()` | A [line](text.md#lines), or the spec's own `text`/`args`/`cases` |
| `Text`, `Require`, `Flag`, `Number`, `Strings`, `IsText`, `Has` | Plain values. `Number` is fixed at load; `Value` is worked out each time |
| `Entries(key)` | A list of objects, each its own spec |
| `Ref(id, kind)` | Qualifies an id and reports a problem at the end of loading if no such status, enemy, event, loot table, ... exists |
| `Hook(key)` | A declared hook's qualified name |
| `Pool(kind)`, `Enemies()` | Candidates named the way built-ins name them (`from`, `tag`, `pool`, `role`, `intel`, ...) |
| `Error(message)`, `Warn(message)` | Report a problem, or a warning, at this spec's place |

A non-object argument is available as `"value"`. Read every key before returning: keys not asked for by then are
reported as unused. A bare `true` (`{ "my_mod:ping": true }`) counts as no arguments, and a spec you called `Error` on
isn't reported.

## At run time

An `IScope` gives `Run`, `Self`, `Target`, `Item` and `Paired` (the `with` item), plus `Arg(name)` and `With...` to make
a changed scope.

An `IRun` has run variables (`Get`, `Set`, `GetText`, `SetText`), dice (`Roll`, and `RollCheck` with luck and
interceptors), the log (`Log`, `Say`, `NightLog`), `Damage`, `Finish`, `Emit`, `Decide`, roles, lists, the area's pools,
today's `Intel`, and:

| Part | What it does |
|---|---|
| `run.Flow` | Next room, retreat, start and end events, schedule events at a depth |
| `run.Combat` | Make enemies, muster, start and end fights, spawn, focus, defeat, remove, statuses, intents, heal |
| `run.Inventory` | Carried items, select, fits, consume, equip, give, roll loot, credits, lose items |
| `run.Trade` | Stock carts, open the counters, hand over a cart |

Notes:

- Ids passed at run time are qualified, like `my_mod:rat`.
- `Set` on a variable the engine keeps does nothing; `Get` keeps answering from the engine.
- `run.Game("rep.security")` reads what `game.rep.security` reads.
- `run.Reputation(faction, amount)` changes reputation by a `double`, as the `reputation` effect does, but logs
  nothing: it returns the game's night log line, or null when nothing changed.
- `IItem.Handle` is the game's own object: a `GameItem` for a weapon, a `List<GameItem>` for a stack of anything else.
- `IRun` is also used on the map, where nothing changes and collections are empty.

## Decisions

A decision is something the run is about to do, offered to interceptors first.

| Decision | What can change | Scope |
|---|---|---|
| `damage` | `Value` (before armor), `"pierce"` (1 ignores armor), or cancel | Self is the source, Target the victim |
| `heal` | `Value`, or cancel | Target heals |
| `check` | `Value` (the natural roll). `"sides"` is the die | |
| `chance` | `Value` (the percent chance of a `chance` check) | |
| `status` | `"turns"`, `"rooms"`, or cancel. `Text` is the status | Target gets it |
| `loot` | `Text` (the rolled item), or cancel. Text `"table"` is the table | |
| `intent` | `Text` (the enemy's next move from its behavior) | Self is the enemy |

`damage` decisions don't carry the data's `hit` map; an effect a mod compiles from data (`spec.Value`) can read `hit.NAME` like any expression.

`run.Decide` makes your own decisions, named `packid:name`, for other mods to intercept. Button odds show what the data
says and don't run interceptors.

## Memory

The API has no memory calls of its own. A C# effect or condition reaches memory through what it compiles from data:
expressions read with `spec.Value`, and effects and conditions read with `spec.Effect` and `spec.Condition`, read and
write `memory.` for the pack whose data uses them, as data does.

Code that runs the engine itself, as `tests/Sim` and `ef-sim` do, provides memory through `IBridge.Memory`
(`MemoryStore` keeps it in memory). `MapContext` takes a store as its optional last argument for map-time reads; without
one, memory reads 0 and "". `IntelRoller.Roll` takes the scope that intel slots' `when` is checked against, such as a
`MapContext`'s `Root`, and applies the area's `intel_max` after every slot has rolled. A fourth argument, `toldOnDemand`,
leaves `told` slots to `IntelRoller.TellCandidates` and `Tell`, which hand out one card at a time against a `ToldDay` the
caller keeps.
