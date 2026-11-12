using System;
using System.Collections.Generic;

namespace ExpeditionFramework.Api
{
    // Compiled pieces of content. Built-ins and mods produce the same three kinds of delegate.
    public delegate void Effect(IScope scope);
    public delegate bool Condition(IScope scope);
    public delegate int Value(IScope scope);
    public delegate bool ItemFilter(IItem item, IScope scope);

    // Turns one JSON node into a delegate at load time, so bad content is reported before anyone plays it.
    public delegate Effect EffectCompiler(ISpec spec);
    public delegate Condition ConditionCompiler(ISpec spec);

    /// <summary>Changes a decision before the run acts on it. See <see cref="Decision"/>.</summary>
    public delegate void Interceptor(Decision decision);

    /// <summary>Implemented by the consumer's IMod class. Called once, before the consumer's data files load.</summary>
    public interface IExpeditionExtension
    {
        void RegisterExpeditions(IExpeditionRegistrar registrar);
    }

    public interface IExpeditionRegistrar
    {
        string PackId { get; }

        /// <summary>Data refers to it as "packid:name", or just "name" from the same pack.</summary>
        void AddEffect(string name, EffectCompiler compiler);
        void AddCondition(string name, ConditionCompiler compiler);

        /// <summary>Declares a hook as "packid:name". Fire it with <see cref="IRun.Emit"/> or the "emit" effect; data can then use it under "on".</summary>
        void AddHook(string name);

        /// <summary>
        /// Runs after the data attached to a hook, on every expedition from every pack. Check Run.AreaId to limit it.
        /// Self is who it happened to and Target the other party. Throws on an unqualified name that isn't a hook.
        /// </summary>
        void On(string hook, Effect listener);

        /// <summary>
        /// Lets this mod change a decision, on every expedition from every pack. Lower order runs first; equal orders
        /// run in registration order. Built-in decisions are damage, heal, check, chance, status, loot and intent. Other
        /// mods' decisions are named "packid:name". Throws on an unqualified name that isn't built in.
        /// </summary>
        void Intercept(string decision, Interceptor interceptor, int order = 0);
    }

    /// <summary>
    /// Something the run is about to do, offered to interceptors first. What each field means depends on the decision:
    /// <list type="table">
    /// <item><term>damage</term><description>Value is the amount before armor. Self is the source, Target the victim. Set "pierce" to 1 to ignore armor. Cancel to prevent it.</description></item>
    /// <item><term>heal</term><description>Value is the amount. Target is who heals. Cancel to prevent it.</description></item>
    /// <item><term>check</term><description>Value is the natural die result, "sides" the die. Change Value to change the roll.</description></item>
    /// <item><term>chance</term><description>Value is the percent chance of a percent check succeeding.</description></item>
    /// <item><term>status</term><description>Text is the status id, "turns" and "rooms" its length. Target is who gets it. Cancel to prevent it.</description></item>
    /// <item><term>loot</term><description>Text is the rolled item id and the "table" text its table. Change Text to swap the item. Cancel for nothing.</description></item>
    /// <item><term>intent</term><description>Text is the intent an enemy chose from its behavior. Self is the enemy. Change Text to change its next move.</description></item>
    /// </list>
    /// Button previews show the odds written in data and don't run interceptors.
    /// </summary>
    public sealed class Decision
    {
        private readonly Dictionary<string, int> numbers = new Dictionary<string, int>();
        private readonly Dictionary<string, string> texts = new Dictionary<string, string>();

        public Decision(string name, IScope scope, int value = 0, string text = null)
        {
            Name = name;
            Scope = scope;
            Value = value;
            Text = text;
        }

        public string Name { get; }
        public IScope Scope { get; }
        public int Value { get; set; }
        public string Text { get; set; }
        public bool Cancelled { get; set; }

        public int Get(string key) => numbers.TryGetValue(key, out int v) ? v : 0;
        public void Set(string key, int value) => numbers[key] = value;
        public string GetText(string key) => texts.TryGetValue(key, out string v) ? v : null;
        public void SetText(string key, string value) => texts[key] = value;
    }

    /// <summary>A line of player-facing text compiled from data.</summary>
    public interface ILine
    {
        string Render(IScope scope);
    }

    /// <summary>A compiled choice of carried items. Pass it to <see cref="IInventory.Select"/>.</summary>
    public interface IItemUse
    {
    }

    /// <summary>Compiled candidates of one kind, each with its own weight and condition.</summary>
    public interface IPool
    {
        /// <summary>Every candidate's qualified id, whether or not it can appear right now.</summary>
        IReadOnlyList<string> Ids(IScope scope);

        /// <summary>A weighted pick from those whose conditions hold, or null if none do.</summary>
        string Pick(IScope scope);
    }

    /// <summary>
    /// The arguments of one effect or condition in a data file. A non-object argument is available as "value".
    /// Keys an effect never asks for while compiling are reported as warnings, so read every key before returning the
    /// effect. A bare true ({"my:effect": true}) counts as no arguments.
    /// </summary>
    public interface ISpec
    {
        string PackId { get; }
        string Path { get; }
        bool Has(string key);
        string Text(string key, string fallback = null);
        Value Value(string key, int fallback);
        Effect Effect(string key);
        Condition Condition(string key);
        ItemFilter Items(string key);
        void Error(string message);

        /// <summary>Reports a likely mistake at this spec's place without stopping the pack loading.</summary>
        void Warn(string message);

        /// <summary>The text at key, reporting a problem if it's missing.</summary>
        string Require(string key);
        bool Flag(string key, bool fallback);

        /// <summary>A number fixed at load time, unlike <see cref="Value"/>.</summary>
        int Number(string key, int fallback);

        /// <summary>Whether the argument is a plain string.</summary>
        bool IsText(string key);

        /// <summary>A string, or the strings of a list.</summary>
        IReadOnlyList<string> Strings(string key);

        /// <summary>The objects of a list, each as its own spec. Anything else in the list is reported.</summary>
        IReadOnlyList<ISpec> Entries(string key);

        /// <summary>A condition, or each condition of a list.</summary>
        IReadOnlyList<Condition> Conditions(string key);

        /// <summary>
        /// Qualifies an id and reports a problem at the end of loading if it doesn't exist. Kinds include status, intent,
        /// enemy, encounter, event, room, loot, cart, area and kit.
        /// </summary>
        string Ref(string id, string kind);

        /// <summary>The hook named at key, qualified. Reports a problem if no such hook is declared.</summary>
        string Hook(string key);

        /// <summary>The line at key, or null if there's none.</summary>
        ILine Line(string key);

        /// <summary>A line written as this spec's own fields (text, key, cases), or else as its "value".</summary>
        ILine InlineLine();

        /// <summary>A selection of carried items at key, or written as this spec's own fields when key is null.</summary>
        IItemUse Use(string key = null);

        /// <summary>Candidates of a kind ("event", "encounter" or "enemy") named by "from", "ids", "tag", "pool", or a plain string value.</summary>
        IPool Pool(string kind);

        /// <summary>Enemy ids named by "role", "enemy", "intel", "encounters" (its lone enemies), or the fields <see cref="Pool"/> reads.</summary>
        Func<IScope, IReadOnlyList<string>> Enemies();
    }

    /// <summary>
    /// Who and what an effect is about. Self is the owner, Target the thing acted on, Item the item in use, and Paired a
    /// second item used along with it, such as the rounds going into a gun.
    /// </summary>
    public interface IScope
    {
        IRun Run { get; }
        IEntity Self { get; }
        IEntity Target { get; }
        IItem Item { get; }
        IItem Paired { get; }

        /// <summary>An argument passed to the shared effect this is running inside, or 0.</summary>
        int Arg(string name);
        IScope With(IEntity self, IEntity target, IItem item);
        IScope WithPaired(IItem paired);
        IScope WithArg(string name, int value);
    }

    public interface IEntity
    {
        string Id { get; }
        string Name { get; }
        bool IsPlayer { get; }
        bool Active { get; }
        bool HasTag(string tag);

        /// <summary>The stat including what statuses add to it.</summary>
        int Get(string stat);

        /// <summary>The stat without what statuses add to it.</summary>
        int Base(string stat);
        void Set(string stat, int value);
        string GetText(string key);
        void SetText(string key, string value);

        /// <summary>Takes a qualified status id.</summary>
        bool HasStatus(string statusId);

        /// <summary>The qualified id of its next move, or null.</summary>
        string IntentId { get; }
        bool IntentInterruptible { get; }

        /// <summary>The qualified id of the loot table it drops, or null.</summary>
        string Drops { get; }
    }

    public interface IItem
    {
        string Id { get; }
        string Name { get; }
        int Count { get; }
        bool HasTag(string tag);
        int Get(string property);
        void Set(string property, int value);
        string GetText(string key);
        void SetText(string key, string value);

        /// <summary>The game's own object: a GameItem for a weapon, or a List&lt;GameItem&gt; for a stack of anything else.</summary>
        object Handle { get; }
    }

    /// <summary>One expedition in progress, or the map when none is. On the map, nothing changes and collections are empty.</summary>
    public interface IRun
    {
        string AreaId { get; }
        IEntity Player { get; }
        IReadOnlyList<IEntity> Enemies { get; }
        IEntity Focus { get; }
        bool Over { get; }

        /// <summary>"room", "combat", "over", or whatever scene the current event declares.</summary>
        string Scene { get; }

        IFlow Flow { get; }
        ICombat Combat { get; }
        IInventory Inventory { get; }
        ITrade Trade { get; }

        /// <summary>A number from 1 to sides.</summary>
        int Roll(int sides);

        /// <summary>A die for a check. Applies the area's luck rule and any "check" interceptors.</summary>
        int RollCheck(int sides);
        int Get(string variable);
        void Set(string variable, int value);
        string GetText(string variable);
        void SetText(string variable, string value);

        /// <summary>True the first time it's called with this key in a run.</summary>
        bool FirstTime(string key);

        /// <summary>Someone cast into a named part for this run, or null.</summary>
        IEntity Role(string name);
        void SetRole(string name, IEntity who);

        /// <summary>A named list of people for this run. Created empty the first time it's asked for.</summary>
        IList<IEntity> Roster(string name);

        /// <summary>The qualified ids in one of the area's pools. Empty if it has no such pool.</summary>
        IReadOnlyList<string> Pool(string name);

        /// <summary>What the street said about this area today: intel slot to qualified ids.</summary>
        IReadOnlyDictionary<string, List<string>> Intel { get; }

        /// <summary>A number the game provides, by name. Unknown names are 0.</summary>
        int Game(string name);
        void Log(string tone, string text);
        void Say(string tone, ILine line, IScope scope);

        /// <summary>Attaches roll details to the next log line.</summary>
        void NoteRoll(string text);
        void NoteCheck(int sides, int natural, int modifier, int target);
        void NoteDie(int sides, int natural);

        void Finish(string outcome);

        /// <summary>Returns the damage dealt after armor. Goes through "damage" interceptors.</summary>
        int Damage(IEntity target, int amount, IEntity source, bool pierce);

        void Wound(string kind);

        /// <summary>
        /// Changes the player's reputation with a faction (lower_level, upper_level, security, black_market, revolution or
        /// cartel) the way the game's own events do, so perks and difficulty scale it. Returns the game's own night log line
        /// for the change, or null when nothing changed: the player has no such faction, or the run is on the map.
        /// Logs nothing itself.
        /// </summary>
        string Reputation(string faction, int amount);
        void NightLog(string text, string color);
        void PlaySound(string id);

        /// <summary>Fires a hook on data and listeners. Subject is who it happened to, other the other party.</summary>
        void Emit(string hook, IEntity subject, IEntity other);

        /// <summary>Offers a decision to its interceptors and returns it. Mods can make their own, named "packid:name".</summary>
        Decision Decide(Decision decision);
    }

    /// <summary>Moving through the expedition. Ids are qualified: "packid:name".</summary>
    public interface IFlow
    {
        void NextRoom();

        /// <summary>Back out of the current room, so going on leads to it again.</summary>
        void Retreat();
        void StartEvent(string eventId, IScope scope);
        void EndEvent();

        /// <summary>Keeps the current event open after the choice that's being carried out.</summary>
        void StayInEvent();

        void Schedule(int depth, string eventId);
        void Unschedule(int depth);

        /// <summary>The event due at a depth, or null.</summary>
        string ScheduledAt(int depth);
        IEnumerable<string> Scheduled { get; }
    }

    /// <summary>Fights and those in them. Ids are qualified: "packid:name".</summary>
    public interface ICombat
    {
        /// <summary>A new enemy, not yet in the room. Null if there's no such enemy.</summary>
        IEntity Make(string enemyId);

        /// <summary>Puts people in the room without starting a fight. Replaces whoever is there unless add is set.</summary>
        void Muster(IEnumerable<IEntity> group, bool add);
        void MusterEncounter(string encounterId, bool add);

        /// <summary>Turns whoever is present into a fight. Won runs with scope if it's won; prepare runs for each enemy first.</summary>
        void Engage(Effect won, IScope scope, bool surprised, Effect prepare);
        void StartFight(string encounterId, Effect won, IScope scope, bool surprised);
        void EndFight(bool won);

        /// <summary>Runs the enemies' side of the turn now.</summary>
        void EnemyTurn();

        /// <summary>Adds someone to the fight in progress.</summary>
        void Spawn(IEntity arrival, bool surprised);
        void SetFocus(IEntity who);

        void Defeat(IEntity victim, IEntity source);

        /// <summary>Takes someone out of the scene without defeating them.</summary>
        void Remove(IEntity who);

        /// <summary>Null turns or rooms use the status's own length. Goes through "status" interceptors.</summary>
        void AddStatus(IEntity who, string statusId, int? turns = null, int? rooms = null);
        void ClearStatus(IEntity who, string statusId);
        void SetIntent(IEntity enemy, string intentId);

        /// <summary>Returns how much was healed, and sets run.healed. Goes through "heal" interceptors.</summary>
        int Heal(IEntity who, int amount);
    }

    /// <summary>What the player carries and finds.</summary>
    public interface IInventory
    {
        IReadOnlyList<IItem> Items { get; }

        /// <summary>The carried items a selection matches, in its order. With a main item, this chooses what to pair with it.</summary>
        IReadOnlyList<IItem> Select(IItemUse use, IScope scope, IItem main);
        bool Fits(IItem gun, IItem round);
        void Consume(IItem item, int count);

        /// <summary>Puts a carried item in an empty hand. Returns false if that failed.</summary>
        bool Equip(IItem item);

        /// <summary>Puts a new item on the ground, logs it and sets run.given. Returns its name, or null if that failed.</summary>
        string Give(string itemId);

        /// <summary>Rolls a qualified loot table for an item id, or null. Goes through "loot" interceptors.</summary>
        string RollLoot(string tableId);

        int Credits { get; }
        void AddCredits(int amount);

        /// <summary>Returns false, changing nothing, if the player can't afford it.</summary>
        bool SpendCredits(int amount);

        /// <summary>Destroys this percent of the carried kit and logs it. Returns the names lost.</summary>
        IReadOnlyList<string> LoseCarried(int percent);
        /// <summary>Like <see cref="LoseCarried"/>, but each piece's chance is <paramref name="percentPerCell"/> times the
        /// grid cells it takes up (attachments included), capped at <paramref name="maxPercent"/>. A stack rolls once.</summary>
        IReadOnlyList<string> LoseCarriedBySize(int percentPerCell, int maxPercent);
        void LoseFound();

        /// <summary>Destroys the newest found item on the ground. Returns its name, or null if there's none.</summary>
        string TakeBack();

        /// <summary>Destroys everything on the ground. Returns how many items that was.</summary>
        int ClearGround();
    }

    /// <summary>Traders. Cart ids are qualified: "packid:name".</summary>
    public interface ITrade
    {
        /// <summary>Stocks a cart. Replaces any cart this run already made from the same definition.</summary>
        void NewCart(string cartId, IScope scope);

        /// <summary>Opens the counters. Closed runs with scope once the player puts them away.</summary>
        void Open(string cartId, Effect closed, IScope scope);

        /// <summary>Hands over the whole cart. Before runs once run.cart_taken is set and before anything is logged.</summary>
        void TakeCart(string cartId, Effect before, IScope scope);
    }
}
