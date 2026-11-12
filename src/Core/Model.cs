using System;
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Api;

namespace ExpeditionFramework.Core
{
    /// <summary>A text key. Keys with {placeholders} are worked out when shown, such as "{self}_look".</summary>
    public sealed class TextRef
    {
        public string Pack;
        public string Key;
        public Func<IScope, string> Dynamic;

        public string Resolve(IScope scope) => Dynamic != null ? Dynamic(scope) : Key;
    }

    /// <summary>
    /// A label or log line. Either one text with arguments, optionally one of several numbered variants, or a list of cases
    /// where the first whose condition holds is used.
    /// </summary>
    public sealed class Line : ILine
    {
        public TextRef Text;
        public int Variants;
        public Func<IScope, object>[] Args = new Func<IScope, object>[0];
        public bool UsesPercent;
        public string Tone;
        public List<LineCase> Cases;

        public string Render(IScope scope) => Lines.Render(this, scope);
    }

    public sealed class LineCase
    {
        public Condition When;
        public Line Line;
    }

    public abstract class Def
    {
        public string Id;
        public string Pack;
        public string[] Tags = new string[0];
        public int Weight = 1;
        public Condition When;
    }

    /// <summary>Which carried items something works with: a filter, then how to order and how many to offer.</summary>
    public sealed class ItemUse : IItemUse
    {
        public ItemFilter Filter;
        /// <summary>Lowest first.</summary>
        public Value Order;
        public int Limit;
        public bool Each = true;
        public int Consume;
        /// <summary>For a paired item: whether it goes with this main item. Item and with are both set.</summary>
        public Condition Where;
    }

    public enum InfoKind { Text, Icon, Bar }

    /// <summary>One piece of the info area beside an action's button: a text line, an icon, or a bar.</summary>
    public sealed class InfoDef
    {
        public InfoKind Kind;
        public Condition When;
        public Line Text;
        /// <summary>"item", "with", or a sprite key ("pack:sprite", or a sprite in the pack's own folder).</summary>
        public string Icon;
        public Value Bar;
        public Value Max;
        public string Tone;
        /// <summary>Whether this piece shows the check's odds itself, so the automatic odds line is left out.</summary>
        public bool ShowsOdds;
    }

    /// <summary>An info piece worked out for one button.</summary>
    public sealed class InfoPiece
    {
        public InfoKind Kind;
        public string Text;
        public string Tone;
        /// <summary>For an icon: the item whose picture to show, or else <see cref="Sprite"/>.</summary>
        public RunItem Item;
        public string Sprite;
        public int Value;
        public int Max;

        public override string ToString() => Kind == InfoKind.Icon ? "icon:" + (Item?.Id ?? Sprite) : Kind == InfoKind.Bar ? "bar:" + Value + "/" + Max + ":" + Text : Text;
    }

    public sealed class ConfirmDef
    {
        public Condition When;
        public Line Text;
        public Line Yes;
        public Line No;
    }

    /// <summary>
    /// Anything the player can do. It answers: what can be done (<see cref="Label"/>, <see cref="When"/>), to whom
    /// (<see cref="Targets"/>), with which items (<see cref="Uses"/>, <see cref="With"/>), and what happens (<see cref="Body"/>).
    /// Enemies, rooms, events, statuses, areas and kits all carry the same thing.
    /// </summary>
    public sealed class Interaction
    {
        public string Id;
        public Line Label;
        public string Group = "move";
        /// <summary>Scenes it's offered in: room, event, combat, or a scene an event declares.</summary>
        public HashSet<string> In;
        public Condition When;
        /// <summary>"enemy" offers one option per enemy that passes <see cref="Where"/>, "focus" acts on the current focus. Null keeps the owner as target.</summary>
        public string Targets;
        public Condition Where;
        public ItemUse Uses;
        public ItemUse With;
        /// <summary>Whether doing this passes time: enemies act in a fight, and after_action fires.</summary>
        public bool TakesTurn = true;
        public bool Once;
        public bool Stay;
        public string Sound;
        public string SoundElse;
        /// <summary>The odds shown beside the button, and passed to its label as the last argument.</summary>
        public Value Preview;
        public ConfirmDef Confirm;
        /// <summary>What shows beside the button. Null when the pack gives none; a check's odds still show there.</summary>
        public List<InfoDef> Info;
        public Effect Body;
    }

    public sealed class Hooks
    {
        public readonly Dictionary<string, Effect> On = new Dictionary<string, Effect>();
    }

    public sealed class KitDef : Def
    {
        public List<Interaction> Interactions = new List<Interaction>();
        public Hooks Hooks = new Hooks();
    }

    public sealed class IntentDef : Def
    {
        public Line Label;
        public Line Tell;
        public string Tone = "enemy";
        public bool Interruptible;
        public string Next;
        public Effect Do;
    }

    /// <summary>One weighted choice: an intent, or a nested behavior to pick from.</summary>
    public sealed class BehaviorEntry
    {
        public string Intent;
        public List<BehaviorEntry> Nested;
        public int Weight = 1;
        public Condition When;
    }

    public sealed class EnemyDef : Def
    {
        public TextRef Name;
        public Dictionary<string, Value> Stats = new Dictionary<string, Value>();
        public Dictionary<string, string> Texts = new Dictionary<string, string>();
        public List<BehaviorEntry> Behavior = new List<BehaviorEntry>();
        public string Opening;
        public string Drops;
        public List<Interaction> Interactions = new List<Interaction>();
        public Hooks Hooks = new Hooks();
    }

    public sealed class EncounterDef : Def
    {
        public List<string> Enemies = new List<string>();
        public Hooks Hooks = new Hooks();
    }

    public sealed class EventDef : Def
    {
        public Line Text;
        /// <summary>The scene name while this event is open. Interactions choose scenes by name.</summary>
        public string Scene = "event";
        public Effect Enter;
        public List<Interaction> Choices = new List<Interaction>();
    }

    public sealed class RoomDef : Def
    {
        public Line Text;
        public Effect Enter;
        public List<Interaction> Interactions = new List<Interaction>();
        public Hooks Hooks = new Hooks();
    }

    public sealed class StatusDef : Def
    {
        public TextRef Name;
        public int Turns = -1;
        /// <summary>Rooms it lasts: counted down each time the player enters a room. -1 never runs out this way.</summary>
        public int Rooms = -1;
        /// <summary>Added to the owner's stats while it lasts.</summary>
        public Dictionary<string, int> Stats = new Dictionary<string, int>();
        /// <summary>While it lasts, the owner does this instead of choosing.</summary>
        public string Intent;
        public List<Interaction> Interactions = new List<Interaction>();
        public Hooks Hooks = new Hooks();
    }

    /// <summary>One weighted loot choice: an item id, another of this framework's tables, or a table the game knows.</summary>
    public sealed class LootEntry
    {
        public string Item;
        public string Loot;
        public string GameTable;
        public int Weight = 1;

        /// <summary>Null means always. Otherwise the entry only rolls while this holds, such as mod(other_mod) or run.depth >= 3.</summary>
        public Condition When;
    }

    public sealed class LootDef : Def
    {
        public List<LootEntry> Entries = new List<LootEntry>();
    }

    public sealed class CartLine
    {
        public string Item;
        public List<string> Loot;
        public Value Index;
        public Value Count;
    }

    /// <summary>A trader's stock and purse, for the game's own trade counters.</summary>
    public sealed class CartDef : Def
    {
        public TextRef Name;
        public Value Budget;
        public List<CartLine> Stock = new List<CartLine>();
    }

    public sealed class ItemStats
    {
        public readonly HashSet<string> Tags = new HashSet<string>();
        public readonly Dictionary<string, int> Props = new Dictionary<string, int>();
    }

    public sealed class LayoutSlot
    {
        public Func<IRun, List<RoomDef>> Pool;
        public Value Count;
        public bool Unique = true;
    }

    public sealed class LuckRule
    {
        public Value Percent;
        public int Low = 5;
        public int High = 16;
    }

    public sealed class PackingNeed
    {
        public ItemFilter Items;
        public TextRef Missing;
    }

    public sealed class PackingDef
    {
        public TextRef NoBackpack;
        public TextRef Card;
        public List<PackingNeed> Needs = new List<PackingNeed>();
    }

    /// <summary>One thing the street might know on a given day. Each pick from its pool gets its own card on the map.</summary>
    public sealed class IntelSlot
    {
        public string Id;
        public string Pool;
        public int Chance = 100;
        /// <summary>Percent chance of naming 0, 1, 2... picks. Null means one pick, at <see cref="Chance"/>.</summary>
        public int[] Counts;
        public Line Title;
        public Line Description;
        public Line Effect;
        public string Tone = "mixed";
        public HashSet<string> GoodPicks = new HashSet<string>();
        /// <summary>Whether the street talks about this at all today, checked on the map when the day is rolled. Null means always.</summary>
        public Condition When;

        public int MaxPicks => Counts != null ? Counts.Length - 1 : 1;
    }

    public sealed class HudPart
    {
        public Line Line;
        public Condition When;
        public ItemUse EachItem;
    }

    public sealed class HudRow
    {
        public Condition When;
        public bool Separator;
        public List<HudPart> Parts = new List<HudPart>();
    }

    /// <summary>What the info window shows. Everything is a line; enemy lines run with the enemy as target.</summary>
    public sealed class HudDef
    {
        public Line Title;
        public List<HudRow> Rows = new List<HudRow>();
        public Line Enemy;
        public Line Pending;
        public Line Clear;
        public HashSet<string> PendingScenes = new HashSet<string>();
    }

    public sealed class AreaDef : Def
    {
        public TextRef Title;
        public string UnlockedBy;
        public string Ambience;
        public string Art;
        public Condition HiddenWhen;
        public Dictionary<string, Value> Player = new Dictionary<string, Value>();
        public Dictionary<string, Value> Vars = new Dictionary<string, Value>();
        public Dictionary<string, List<string>> Pools = new Dictionary<string, List<string>>();
        public string Entrance;
        public List<LayoutSlot> Layout = new List<LayoutSlot>();
        public List<string> Kits = new List<string>();
        public List<string> GroupOrder;
        public LuckRule Luck;
        public PackingDef Packing;
        public List<IntelSlot> Intel = new List<IntelSlot>();
        public HudDef Hud;
        /// <summary>Framework text keys this area words its own way.</summary>
        public Dictionary<string, TextRef> Texts = new Dictionary<string, TextRef>();
        public List<Interaction> Interactions = new List<Interaction>();
        public Hooks Hooks = new Hooks();
    }

    /// <summary>Everything loaded from every pack.</summary>
    public sealed class Content
    {
        public readonly Dictionary<string, KitDef> Kits = new Dictionary<string, KitDef>();
        public readonly Dictionary<string, IntentDef> Intents = new Dictionary<string, IntentDef>();
        public readonly Dictionary<string, EnemyDef> Enemies = new Dictionary<string, EnemyDef>();
        public readonly Dictionary<string, EncounterDef> Encounters = new Dictionary<string, EncounterDef>();
        public readonly Dictionary<string, EventDef> Events = new Dictionary<string, EventDef>();
        public readonly Dictionary<string, RoomDef> Rooms = new Dictionary<string, RoomDef>();
        public readonly Dictionary<string, StatusDef> Statuses = new Dictionary<string, StatusDef>();
        public readonly Dictionary<string, LootDef> Loot = new Dictionary<string, LootDef>();
        public readonly Dictionary<string, CartDef> Carts = new Dictionary<string, CartDef>();
        public readonly Dictionary<string, AreaDef> Areas = new Dictionary<string, AreaDef>();
        public readonly Dictionary<string, Value> Values = new Dictionary<string, Value>();

        /// <summary>Item id to the shared group names it belongs to, like "explosive".</summary>
        public readonly Dictionary<string, HashSet<string>> ItemGroups = new Dictionary<string, HashSet<string>>();

        /// <summary>Facts about items, by item id or "#group". Group entries apply first, so an item's own entry wins.</summary>
        public readonly Dictionary<string, ItemStats> ItemStats = new Dictionary<string, ItemStats>();

        /// <summary>Items to hand out from the debug menu, by pack.</summary>
        public readonly Dictionary<string, List<string>> DebugKits = new Dictionary<string, List<string>>();

        public RunListeners Listeners = new RunListeners();

        public readonly List<string> Problems = new List<string>();
        /// <summary>Likely mistakes that don't stop the pack loading, such as a key nothing reads.</summary>
        public readonly List<string> Warnings = new List<string>();
        public readonly HashSet<(string Pack, string Key)> TextKeys = new HashSet<(string, string)>();
    }

    /// <summary>One stack or object the player carries. The game side must return the same object for the same thing.</summary>
    public sealed class RunItem : IItem
    {
        public object Handle { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public int Count { get; set; } = 1;
        public string Sound { get; set; }
        public readonly HashSet<string> Tags = new HashSet<string>();
        public readonly Dictionary<string, int> Props = new Dictionary<string, int>();
        private readonly Dictionary<string, string> texts = new Dictionary<string, string>();

        public bool HasTag(string tag) => Tags.Contains(tag);
        public int Get(string property) => property == "count" ? Count : Props.TryGetValue(property, out int v) ? v : 0;
        public void Set(string property, int value) => Props[property] = value;
        public string GetText(string key) => texts.TryGetValue(key, out string v) ? v : "";
        public void SetText(string key, string value) => texts[key] = value;

        /// <summary>Applies the item's facts from data: groups first, then the item's own entry.</summary>
        public void ApplyStats(Content content)
        {
            if (content.ItemGroups.TryGetValue(Id, out HashSet<string> groups)) Tags.UnionWith(groups);
            foreach (string tag in Tags.ToList())
            {
                if (content.ItemStats.TryGetValue("#" + tag, out ItemStats forGroup)) Apply(forGroup);
            }
            if (content.ItemStats.TryGetValue(Id, out ItemStats own)) Apply(own);
        }

        private void Apply(ItemStats stats)
        {
            Tags.UnionWith(stats.Tags);
            foreach (KeyValuePair<string, int> prop in stats.Props) Props[prop.Key] = prop.Value;
        }
    }

    public sealed class TradeRequest
    {
        public string Key;
        public List<string> Stock = new List<string>();
        public int Budget;
        public string Name;
    }

    public sealed class TradeResult
    {
        public int CashChange;
        public readonly List<string> Bought = new List<string>();
    }

    /// <summary>What the engine needs from the game.</summary>
    public interface IBridge
    {
        IReadOnlyList<RunItem> Items();
        void Consume(RunItem item, int count);
        /// <summary>Puts an item on the ground. Returns its name, or null if that failed.</summary>
        string GiveItem(string itemId);
        /// <summary>Rolls a loot table the game knows and returns an item id, or null.</summary>
        string RollTable(string table);
        /// <summary>Whether this round can go in this gun.</summary>
        bool Fits(RunItem gun, RunItem round);
        /// <summary>Moves a carried item into an empty hand. Returns whether it got there.</summary>
        bool Equip(RunItem item);
        int Credits { get; }
        void AddCredits(int amount);
        bool SpendCredits(int amount);
        int GroundCount { get; }
        int ClearGround();
        /// <summary>Hands over the newest thing found. Returns its name, or null if the ground is empty.</summary>
        string TakeBack();
        void LoseFound();
        /// <summary>Destroys carried pieces, each with the percent chance <paramref name="chanceForSize"/> gives for its
        /// size in grid cells. Returns the names lost.</summary>
        List<string> LoseCarried(Func<int, int> chanceForSize);
        /// <summary>
        /// The player fact game.NAME reads, 0 for a name the host doesn't know. The game knows luck, roll_luck, wounds,
        /// maimed, day (the game's own day counter), clears / clears.AREA (how often the player has cleared the reading
        /// area / AREA of its pack), rep.FACTION (the player's reputation with FACTION, -200 to 200, cut to a whole number)
        /// and rep_exact.FACTION (the same in hundredths, rounded; see GameReads).
        /// A sim reads whatever its config lists for the name.
        /// </summary>
        int GameValue(string name);
        /// <summary>
        /// Changes the player's reputation with a faction (a GameReads.Factions name) by a decimal amount, the way the game's
        /// own events do, so perks and difficulty scale it. Returns the game's own night log line for the change, or null if
        /// the amount is 0 or the player has no such faction (the cartel exists only on the drug dealer start) and nothing changed.
        /// </summary>
        string Reputation(string faction, double amount);
        /// <summary>Where packs keep memory.* across nights. The game keeps it in the player's save; a sim can keep it in a MemoryStore.</summary>
        IMemory Memory { get; }
        void Wound(string kind);
        void NightLog(string text, string color);
        void PlaySound(string id);
        void PlayItemSound(RunItem item, string fallback);
        bool OpenTrade(TradeRequest request);
        TradeResult CloseTrade();
        /// <summary>Puts what's left on a trader's cart on the ground and returns the names.</summary>
        List<string> TakeCart(TradeRequest cart);
    }

    /// <summary>
    /// The game.NAME reads the framework itself defines. game.clears counts how often the player has cleared the reading
    /// area; game.clears.AREA counts AREA of the reading area's pack. The named form lets hidden_when unlock an area on
    /// another area's clear OR on memory: unlocked_by is ANDed with hidden_when, so it can't say OR.
    /// </summary>
    internal static class GameReads
    {
        private const string Clears = "clears";

        /// <summary>Qualified id of the area whose clears game.NAME counts, or null when NAME isn't a clears read.</summary>
        public static string ClearsArea(string name, string areaId)
        {
            if (name == Clears) return areaId;
            if (name == null || areaId == null || name.Length <= Clears.Length + 1 || !name.StartsWith(Clears + ".", StringComparison.Ordinal)) return null;
            int colon = areaId.IndexOf(':');
            return areaId.Substring(0, colon + 1) + name.Substring(Clears.Length + 1);
        }

        private const string Rep = "rep.";
        private const string RepExact = "rep_exact.";

        /// <summary>
        /// The factions the game keeps reputation with, as packs name them: the game's FACTION_ ids in lower case without
        /// the prefix. The cartel exists only on the drug dealer start.
        /// </summary>
        public static readonly string[] Factions = { "lower_level", "upper_level", "security", "black_market", "revolution", "cartel" };

        public static bool IsFaction(string faction) => Array.IndexOf(Factions, faction) >= 0;

        /// <summary>The game's id for a faction, such as FACTION_BLACK_MARKET for black_market.</summary>
        public static string FactionId(string faction) => "FACTION_" + faction.ToUpperInvariant();

        /// <summary>
        /// The faction game.NAME reads the reputation of, or null when NAME is neither rep.FACTION (a whole number, cut
        /// toward zero as the game shows it) nor rep_exact.FACTION (hundredths, rounded); <paramref name="exact"/> says which.
        /// </summary>
        public static string RepFaction(string name, out bool exact)
        {
            exact = name != null && name.StartsWith(RepExact, StringComparison.Ordinal);
            if (exact) return name.Substring(RepExact.Length);
            return name != null && name.StartsWith(Rep, StringComparison.Ordinal) ? name.Substring(Rep.Length) : null;
        }

        /// <summary>What game.rep.FACTION, or game.rep_exact.FACTION when exact, reads for a reputation of <paramref name="amount"/>.</summary>
        public static int RepRead(double amount, bool exact) =>
            exact ? (int)Math.Round(amount * 100, MidpointRounding.AwayFromZero) : (int)amount;

        /// <summary>A reputation change as the night log shows it: +3, -0.5, +1.25.</summary>
        public static string RepAmount(double amount) => amount.ToString("+0.##;-0.##", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The problem with game.NAME when it reads reputation of a faction the game doesn't have, else null.</summary>
        public static string BadRead(string name)
        {
            string faction = RepFaction(name, out _);
            return faction == null || IsFaction(faction) ? null : UnknownFaction(faction);
        }

        public static string UnknownFaction(string faction) =>
            "unknown faction '" + faction + "' (factions are " + string.Join(", ", Factions) + ")";
    }

    /// <summary>
    /// Values a pack keeps across nights, as memory.NAME (numbers) and memory.$NAME (text). Each pack has its own names,
    /// so two packs using the same name keep separate values; another pack can only read them, as memory.PACK.NAME. Unset
    /// numbers read 0 and unset text "".
    /// </summary>
    public interface IMemory
    {
        int Get(string pack, string name);
        void Set(string pack, string name, int value);
        string GetText(string pack, string name);
        void SetText(string pack, string name, string value);
    }

    /// <summary>Memory held in this process only, for sims and tests.</summary>
    public sealed class MemoryStore : IMemory
    {
        private readonly Dictionary<(string, string), int> numbers = new Dictionary<(string, string), int>();
        private readonly Dictionary<(string, string), string> texts = new Dictionary<(string, string), string>();

        public int Get(string pack, string name) => numbers.TryGetValue((pack, name), out int v) ? v : 0;
        public void Set(string pack, string name, int value) => numbers[(pack, name)] = value;
        public string GetText(string pack, string name) => texts.TryGetValue((pack, name), out string v) ? v : "";
        public void SetText(string pack, string name, string value) => texts[(pack, name)] = value ?? "";
    }

    public delegate string Localizer(string pack, string key, object[] args);

    public readonly struct LogLine
    {
        public readonly string Tone;
        public readonly string Text;
        public readonly string Roll;

        public LogLine(string tone, string text, string roll)
        {
            Tone = tone;
            Text = text;
            Roll = roll;
        }
    }

    /// <summary>A piece of the info window.</summary>
    public readonly struct HudSpan
    {
        public readonly string Text;
        public readonly string Tone;

        public HudSpan(string text, string tone)
        {
            Text = text;
            Tone = tone;
        }
    }
}
