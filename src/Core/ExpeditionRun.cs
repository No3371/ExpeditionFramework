using System;
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Api;

namespace ExpeditionFramework.Core
{
    public sealed class ActiveStatus
    {
        public StatusDef Def;
        public int Turns;
        public int Rooms;
    }

    public sealed class Entity : IEntity
    {
        private readonly HashSet<string> tags;
        private readonly Dictionary<string, int> stats = new Dictionary<string, int>();
        private readonly Dictionary<string, string> texts = new Dictionary<string, string>();

        public EnemyDef Def;
        public IntentDef Intent;
        /// <summary>The intent it carried out this enemy phase, so one set meanwhile isn't replaced.</summary>
        public IntentDef Acted;
        public readonly List<ActiveStatus> Statuses = new List<ActiveStatus>();
        public bool Defeated;
        public bool Gone;

        public string Id { get; }
        public string Name { get; }
        public bool IsPlayer { get; }
        public bool Active => !Defeated && !Gone;

        public Entity(string id, string name, bool isPlayer, IEnumerable<string> tags)
        {
            Id = id;
            Name = name;
            IsPlayer = isPlayer;
            this.tags = new HashSet<string>(tags);
        }

        public bool HasTag(string tag) => tags.Contains(tag);

        public int Base(string stat) => stats.TryGetValue(stat, out int v) ? v : 0;

        public int Get(string stat)
        {
            int value = Base(stat);
            foreach (ActiveStatus status in Statuses)
            {
                if (status.Def.Stats.TryGetValue(stat, out int added)) value += added;
            }
            return value;
        }

        public void Set(string stat, int value) => stats[stat] = value;
        public string GetText(string key) => texts.TryGetValue(key, out string v) ? v : "";
        public void SetText(string key, string value) => texts[key] = value;
        public bool HasStatus(string statusId) => Statuses.Any(s => s.Def.Id == statusId);
        public string IntentId => Intent?.Id;
        public bool IntentInterruptible => Intent != null && Intent.Interruptible;
        public string Drops => Def?.Drops;
    }

    /// <summary>One button: an interaction, bound to a target and items.</summary>
    public sealed class RunOption
    {
        public Interaction Interaction;
        public Entity Owner;
        public Entity Target;
        public RunItem Item;
        public RunItem Paired;
        public string Label;
        /// <summary>What shows beside the button, in order: the pack's "info" pieces, then a check's odds.</summary>
        public List<InfoPiece> Info = new List<InfoPiece>();
        public string ConfirmText;
        public string ConfirmYes;
        public string ConfirmNo;
        public string Group => Interaction.Group;
    }

    internal sealed class Scope : IScope
    {
        private readonly Dictionary<string, int> args;

        public IRun Run { get; }
        public IEntity Self { get; }
        public IEntity Target { get; }
        public IItem Item { get; }
        public IItem Paired { get; }

        public Scope(IRun run, IEntity self, IEntity target, IItem item, IItem paired = null, Dictionary<string, int> args = null)
        {
            Run = run;
            Self = self;
            Target = target;
            Item = item;
            Paired = paired;
            this.args = args;
        }

        public int Arg(string name) => args != null && args.TryGetValue(name, out int v) ? v : 0;

        public IScope With(IEntity self, IEntity target, IItem item) => new Scope(Run, self, target, item, Paired, args);

        public static Scope From(IScope s) => s as Scope ?? new Scope(s.Run, s.Self, s.Target, s.Item, s.Paired);

        public Scope WithPaired(IItem paired) => new Scope(Run, Self, Target, Item, paired, args);
        IScope IScope.WithPaired(IItem paired) => WithPaired(paired);
        IScope IScope.WithArg(string name, int value) => WithArg(name, value);

        public Scope WithArgs(Dictionary<string, int> values) => new Scope(Run, Self, Target, Item, Paired, values);

        public Scope WithArg(string name, int value)
        {
            var merged = args != null ? new Dictionary<string, int>(args) : new Dictionary<string, int>();
            merged[name] = value;
            return new Scope(Run, Self, Target, Item, Paired, merged);
        }
    }

    /// <summary>What rendering a line needs. A run provides it, and so does the map, which shows lines with no run going.</summary>
    internal interface ITextHost
    {
        string Localize(string pack, string key, object[] args);
    }

    /// <summary>
    /// Where memory.* paths find the store. Kept off IRun, which is the public API: putting it there would make every
    /// IRun implementation outside this assembly provide a store. A run that isn't a host, or a host without a store, reads
    /// memory as 0 and "" and drops writes.
    /// </summary>
    internal interface IMemoryHost
    {
        IMemory Memory { get; }
    }

    internal static class Lines
    {
        /// <summary>The line to use right now: the line itself, or its first case that holds. Null if none does.</summary>
        public static Line Choose(Line line, IScope scope)
        {
            if (line?.Cases == null) return line;
            foreach (LineCase option in line.Cases)
            {
                if (option.When == null || option.When(scope)) return Choose(option.Line, scope);
            }
            return null;
        }

        public static string Render(Line line, IScope scope, int? percent = null)
        {
            Line chosen = Choose(line, scope);
            if (chosen == null || chosen.Text == null) return "";
            var host = (ITextHost)scope.Run;
            IScope inner = percent != null ? Scope.From(scope).WithArg("percent", percent.Value) : scope;
            IEnumerable<object> args = chosen.Args.Select(a => a(inner));
            if (percent != null && !chosen.UsesPercent) args = args.Concat(new object[] { percent.Value });
            string key = chosen.Text.Resolve(scope);
            if (chosen.Variants > 0) key += "_" + (scope.Run.Roll(chosen.Variants) - 1);
            return host.Localize(chosen.Text.Pack, key, args.ToArray());
        }

        public static string Bar(int value, int max, int cells = 10)
        {
            int filled = max <= 0 ? 0 : Math.Max(0, Math.Min(cells, (int)Math.Ceiling(value * (double)cells / max)));
            return "[" + new string('#', filled) + new string('.', cells - filled) + "]";
        }

        public static int Percent(int sides, int modifier, int target, int failAt, int passAt)
        {
            int passes = 0;
            for (int natural = 1; natural <= sides; natural++)
            {
                if (natural > failAt && (natural >= passAt || natural + modifier >= target)) passes++;
            }
            return sides > 0 ? passes * 100 / sides : 0;
        }
    }

    /// <summary>Plays one expedition from compiled content. Knows nothing about Unity or specific content.</summary>
    public sealed partial class ExpeditionRun : IRun, ITextHost, IMemoryHost
    {
        public const string FrameworkPack = "expedition_framework";

        public static readonly string[] HookNames =
        {
            "run_start", "room_enter", "combat_start", "turn_start", "turn_end", "combat_end",
            "damaged", "defeated", "knocked_out", "after_action", "run_end", "appeared",
        };

        public static readonly string[] DecisionNames = { "damage", "heal", "check", "chance", "status", "loot", "intent" };

        private const int LogLimit = 200;
        private const int SharedDepthLimit = 32;

        private sealed class CartState
        {
            public CartDef Def;
            public TradeRequest Request;
        }

        private readonly Content content;
        private readonly AreaDef area;
        private readonly Func<int, int> roll;
        private readonly Localizer localize;
        private readonly List<KitDef> kits;
        private readonly Dictionary<string, int> vars = new Dictionary<string, int>();
        private readonly Dictionary<string, string> texts = new Dictionary<string, string>();
        private readonly Dictionary<string, Entity> roles = new Dictionary<string, Entity>();
        private readonly Dictionary<string, List<IEntity>> lists = new Dictionary<string, List<IEntity>>();
        private readonly Dictionary<string, CartState> carts = new Dictionary<string, CartState>();
        private readonly SortedDictionary<int, string> schedule = new SortedDictionary<int, string>();
        private readonly HashSet<string> onceDone = new HashSet<string>();
        private readonly List<RoomDef> rooms = new List<RoomDef>();
        private readonly List<Entity> enemies = new List<Entity>();
        private readonly List<LogLine> log = new List<LogLine>();
        private readonly HashSet<Interaction> usedThisRoom = new HashSet<Interaction>();
        private readonly Scope root;

        private RoomDef room;
        private EventDef currentEvent;
        private EncounterDef fight;
        private bool inFight;
        private bool enemyPhase;
        private Effect fightWon;
        private IScope fightScope;
        private Entity focus;
        private string pendingRoll;
        private int depth;
        private int turn;
        private bool moved;
        private bool stayInEvent;
        private int cartCounter;
        private int sharedDepth;
        private Effect tradeClosed;
        private IScope tradeScope;

        public IBridge Bridge { get; }
        public Entity Hero { get; }
        public string Scene { get; private set; } = "room";
        public string Outcome { get; private set; }
        public bool Over => Outcome != null;
        public string AreaId => area.Id;
        public AreaDef Area => area;
        public IEntity Player => Hero;
        public IReadOnlyList<IEntity> Enemies => enemies;
        public IReadOnlyList<Entity> Foes => enemies;
        public IReadOnlyList<LogLine> Log_ => log;
        public int Depth => depth;
        public int RoomCount => rooms.Count;
        public EventDef CurrentEvent => currentEvent;
        public Entity Focus => focus;
        public IScope RootScope => root;

        /// <summary>A trade is open on the counters. No options are offered until it's finished.</summary>
        public bool Trading { get; private set; }

        /// <summary>What the street said about this area today, by intel id.</summary>
        public IReadOnlyDictionary<string, List<string>> Intel { get; }

        public ExpeditionRun(Content content, AreaDef area, IBridge bridge, Func<int, int> roll, Localizer localize, string playerName, IReadOnlyDictionary<string, List<string>> intel = null)
        {
            this.content = content;
            this.area = area;
            this.roll = roll;
            this.localize = localize;
            Intel = intel ?? new Dictionary<string, List<string>>();
            Bridge = bridge;
            kits = area.Kits.Select(k => content.Kits[k]).ToList();
            Hero = new Entity("player", playerName, true, new[] { "player" });
            root = new Scope(this, Hero, null, null);
        }

        public void Begin()
        {
            foreach (KeyValuePair<string, Value> variable in area.Vars) vars[variable.Key] = variable.Value(root);
            foreach (KeyValuePair<string, Value> stat in area.Player) Hero.Set(stat.Key, stat.Value(root));
            Hero.Set("max_hp", Hero.Get("hp"));
            BuildLayout();
            Emit("run_start", Hero, null);
            if (Over) return;
            if (area.Entrance != null) EnterRoom(content.Rooms[area.Entrance]);
            else NextRoom();
        }

        private void BuildLayout()
        {
            foreach (LayoutSlot slot in area.Layout)
            {
                List<RoomDef> pool = slot.Pool(this);
                for (int n = slot.Count(root); n > 0; n--)
                {
                    List<RoomDef> open = pool.Where(r => (r.When == null || r.When(root)) && (!slot.Unique || !rooms.Contains(r))).ToList();
                    int total = open.Sum(r => r.Weight);
                    if (total <= 0) break;
                    int pick = Roll(total);
                    foreach (RoomDef r in open)
                    {
                        pick -= r.Weight;
                        if (pick <= 0) { rooms.Add(r); break; }
                    }
                }
            }
        }

        // ---- IRun ----

        // While the UI asks what to show, dice read as 1 and variants as the first, like MapContext: the game redraws every
        // half second, so real rolls here would flicker labels and use up the run's randomness.
        private int rendering;
        public int Roll(int sides) => sides <= 0 ? 0 : rendering > 0 ? 1 : roll(sides);

        // Dice the player's action has thrown so far, for the bridge to sound out. Roll itself isn't counted: it also picks
        // text variants and weighted entries, which nobody would call a die.
        private int diceThrown;

        internal void CountDice(int count)
        {
            if (rendering == 0) diceThrown += count;
        }

        internal void PlaySounds(SoundSpec spec) => Bridge.PlaySounds(spec);

        internal void StopSound(string channel) => Bridge.StopSound(channel);

        /// <summary>Whether a condition written outside any action holds right now, with dice reading 1 as they do for labels.</summary>
        public bool Holds(Condition condition)
        {
            rendering++;
            try { return condition(root); }
            finally { rendering--; }
        }

        /// <summary>A die for a check. On a d20 the area's luck rule can reroll a low result, or spoil a high one.</summary>
        public int RollCheck(int sides)
        {
            CountDice(1);
            var decision = new Decision("check", root, RollWithLuck(sides));
            decision.Set("sides", sides);
            return Decide(decision).Value;
        }

        private int RollWithLuck(int sides)
        {
            int result = Roll(sides);
            if (sides != 20 || area.Luck == null) return result;
            int luck = area.Luck.Percent(root);
            if (luck > 0 && result <= area.Luck.Low && Roll(100) <= luck) return Roll(20);
            if (luck < 0 && result >= area.Luck.High && Roll(100) <= -luck) return Roll(20);
            return result;
        }

        /// <summary>The run.* names the engine answers itself. Writing one would only shadow it in the vars, which nothing reads.</summary>
        public static readonly string[] EngineVars = { "depth", "rooms", "rooms_left", "turn", "enemies", "credits", "ground", "scheduled" };

        public int Get(string variable)
        {
            switch (variable)
            {
                case "depth": return depth;
                case "rooms": return rooms.Count;
                case "rooms_left": return rooms.Count - depth;
                case "turn": return turn;
                case "enemies": return enemies.Count(e => e.Active);
                case "credits": return Bridge.Credits;
                case "ground": return Bridge.GroundCount;
                case "scheduled": return schedule.Count;
                default: return vars.TryGetValue(variable, out int v) ? v : 0;
            }
        }

        public void Set(string variable, int value) => vars[variable] = value;
        public string GetText(string variable) => texts.TryGetValue(variable, out string v) ? v : "";
        public void SetText(string variable, string value) => texts[variable] = value;
        public IEntity Role(string name) => roles.TryGetValue(name, out Entity e) ? e : null;
        public int Game(string name) => Bridge.GameValue(name);
        IMemory IMemoryHost.Memory => Bridge.Memory;

        /// <summary>Called with every line as it is logged. The log itself keeps only the last 200 lines; tools that need all of them listen here.</summary>
        public Action<LogLine> Logged { get; set; }

        public void Log(string tone, string text)
        {
            var line = new LogLine(tone, text, pendingRoll);
            log.Add(line);
            pendingRoll = null;
            if (log.Count > LogLimit) log.RemoveRange(0, log.Count - LogLimit);
            Logged?.Invoke(line);
        }

        public void Finish(string outcome)
        {
            if (Over) return;
            Set("outcome_" + outcome, 1);
            Emit("run_end", Hero, null);
            Outcome = outcome;
            Scene = "over";
            enemies.Clear();
            SetFocus(null);
        }

        // One frame per damage in progress, innermost last; null for a damage without a hit map. A damage dealt inside a
        // hook pushes its own frame, so it never sees the outer damage's names. A single run var would leak
        // into that nested damage and be cleared under the outer one.
        private readonly List<Dictionary<string, int>> hits = new List<Dictionary<string, int>>();

        /// <summary>hit.NAME: the innermost damage in progress's value for name, or 0.</summary>
        internal int Hit(string name)
        {
            Dictionary<string, int> top = hits.Count > 0 ? hits[hits.Count - 1] : null;
            return top != null && top.TryGetValue(name, out int v) ? v : 0;
        }

        public int Damage(IEntity target, int amount, IEntity source, bool pierce)
        {
            return Damage(target, amount, source, pierce, null);
        }

        /// <summary>As the public Damage, with the hit map the damage's hooks read as hit.NAME. Not on IRun: adding a member there would break every IRun outside this assembly.</summary>
        internal int Damage(IEntity target, int amount, IEntity source, bool pierce, Dictionary<string, int> hit)
        {
            var victim = (Entity)target;
            if (!victim.Active || Over) return 0;
            hits.Add(hit);
            try
            {
                var decision = new Decision("damage", new Scope(this, source, victim, null), amount);
                decision.Set("pierce", pierce ? 1 : 0);
                Decide(decision);
                if (decision.Cancelled || !victim.Active || Over)
                {
                    Set("last_damage", 0);
                    return 0;
                }
                int dealt = Math.Max(0, decision.Get("pierce") != 0 ? decision.Value : decision.Value - victim.Get("armor"));
                victim.Set("hp", victim.Base("hp") - dealt);
                Set("last_damage", dealt);
                Emit("damaged", victim, source as Entity);
                if (victim.Get("hp") <= 0) Defeat(victim, source as Entity);
                return dealt;
            }
            finally
            {
                // Without this, an exception escaping a hook would leave its frame on top and every later hook would read it.
                hits.RemoveAt(hits.Count - 1);
            }
        }

        // ---- Text ----

        string ITextHost.Localize(string pack, string key, object[] args) => localize(pack, key, args);

        /// <summary>A framework line, in the area's own words if it has them.</summary>
        public string FrameworkText(string key, params object[] args)
        {
            return area.Texts.TryGetValue(key, out TextRef own)
                ? localize(own.Pack, own.Key, args)
                : localize(FrameworkPack, key, args);
        }

        internal bool EnterShared(string name)
        {
            if (sharedDepth >= SharedDepthLimit)
            {
                Log("bad", "shared effect " + name + " called itself too deeply");
                return false;
            }
            sharedDepth++;
            return true;
        }

        internal void LeaveShared() => sharedDepth--;

        public void NoteRoll(string text) => pendingRoll = text;

        public void NoteCheck(int sides, int natural, int modifier, int target)
        {
            string shown = modifier == 0 ? "" : modifier > 0 ? "+" + modifier : modifier.ToString();
            NoteRoll(FrameworkText("ef_roll_vs", natural, shown, natural + modifier, target, sides));
        }

        public void NoteDie(int sides, int natural)
        {
            string specific = "ef_roll_d" + sides;
            NoteRoll(area.Texts.ContainsKey(specific) ? FrameworkText(specific, natural) : FrameworkText("ef_roll", natural, sides));
        }

        internal void Say(string tone, Line line, IScope scope)
        {
            Line chosen = Lines.Choose(line, scope);
            if (chosen != null) Log(chosen.Tone ?? tone, Lines.Render(chosen, scope));
        }

        internal void Found(string name)
        {
            Set("given", name != null ? 1 : 0);
            if (name == null) return;
            Set("found_items", Get("found_items") + 1);
            Log("good", FrameworkText("ef_found", name));
        }

        // ---- Used by effects ----

        internal void Defeat(Entity victim, Entity source)
        {
            if (!victim.Active) return;
            if (victim.IsPlayer)
            {
                Emit("knocked_out", victim, source);
                Finish("knocked_out");
                return;
            }
            victim.Defeated = true;
            Emit("defeated", victim, source);
        }

        internal void AddStatus(Entity who, StatusDef def, int? turns, int? roomsLeft)
        {
            var decision = new Decision("status", new Scope(this, Hero, who, null), 0, def.Id);
            decision.Set("turns", turns ?? def.Turns);
            decision.Set("rooms", roomsLeft ?? def.Rooms);
            if (Decide(decision).Cancelled) return;
            int length = decision.Get("turns");
            int span = decision.Get("rooms");
            ActiveStatus existing = who.Statuses.FirstOrDefault(s => s.Def == def);
            if (existing != null)
            {
                existing.Turns = length;
                existing.Rooms = span;
            }
            else
            {
                who.Statuses.Add(new ActiveStatus { Def = def, Turns = length, Rooms = span });
            }
        }

        internal void StartEvent(EventDef def, IScope scope)
        {
            moved = true;
            currentEvent = def;
            Scene = def.Scene;
            if (def.Text != null) Say("plain", def.Text, scope);
            def.Enter?.Invoke(scope);
        }

        internal void EndEvent()
        {
            moved = true;
            if (currentEvent == null) return;
            currentEvent = null;
            enemies.Clear();
            SetFocus(null);
            Scene = "room";
        }

        /// <summary>Keeps the current event open after the choice that's being carried out.</summary>
        internal void StayInEvent() => stayInEvent = true;

        internal void NextRoom()
        {
            moved = true;
            EndCurrent();
            if (depth >= rooms.Count)
            {
                Finish("cleared");
                return;
            }
            RoomDef next = rooms[depth++];
            foreach (ActiveStatus status in Hero.Statuses.ToList())
            {
                if (status.Rooms > 0 && --status.Rooms == 0) Hero.Statuses.Remove(status);
            }
            EnterRoom(next);
        }

        private void EnterRoom(RoomDef next)
        {
            room = next;
            Bridge.EndSounds("room");
            usedThisRoom.Clear();
            Scene = "room";
            if (room.Text != null) Say("heading", room.Text, root);
            room.Enter?.Invoke(root);
            if (!Over) Emit("room_enter", Hero, null);
        }

        /// <summary>Back out of the current room, so going on leads to it again.</summary>
        internal void Retreat() { if (depth > 0) depth--; }

        private void EndCurrent()
        {
            currentEvent = null;
            fight = null;
            inFight = false;
            fightWon = null;
            enemies.Clear();
            SetFocus(null);
        }

        internal Entity MakeEntity(EnemyDef def)
        {
            var e = new Entity(def.Id, Lines_Name(def), false, def.Tags) { Def = def };
            foreach (KeyValuePair<string, string> text in def.Texts) e.SetText(text.Key, text.Value);
            var scope = new Scope(this, e, Hero, null);
            foreach (KeyValuePair<string, Value> stat in def.Stats) e.Set(stat.Key, stat.Value(scope));
            e.Set("max_hp", e.Get("hp"));
            return e;
        }

        private string Lines_Name(EnemyDef def) => localize(def.Name.Pack, def.Name.Key, new object[0]);

        internal void Muster(IEnumerable<Entity> group, bool add)
        {
            if (!add) enemies.Clear();
            enemies.AddRange(group);
        }

        internal void MusterEncounter(EncounterDef encounter, bool add)
        {
            fight = encounter;
            Muster(encounter.Enemies.Select(id => MakeEntity(content.Enemies[id])).ToList(), add);
        }

        /// <summary>Turns whoever is present into a fight.</summary>
        internal void Engage(Effect won, IScope scope, bool surprised, Effect prepare)
        {
            moved = true;
            currentEvent = null;
            inFight = true;
            fightWon = won;
            fightScope = scope;
            Scene = "combat";
            turn = 1;
            foreach (Entity e in enemies.Where(e => e.Active))
            {
                e.Acted = null;
                e.Intent = surprised && e.Def.Opening != null ? content.Intents[e.Def.Opening] : NextIntent(e);
                prepare?.Invoke(new Scope(this, Hero, e, null));
            }
            SetFocus(enemies.FirstOrDefault(e => e.Active));
            foreach (Entity e in enemies.Where(e => e.Active).ToList()) Emit("appeared", e, Hero);
            if (Over || Scene != "combat") return;
            Emit("combat_start", Hero, null);
            if (Over || Scene != "combat") return;
            foreach (Entity e in enemies.Where(e => e.Active)) Telegraph(e);
        }

        internal void StartFight(EncounterDef encounter, Effect won, IScope scope, bool surprised)
        {
            MusterEncounter(encounter, add: false);
            Engage(won, scope, surprised, null);
        }

        /// <summary>Adds someone to the fight in progress.</summary>
        internal void Spawn(Entity arrival, bool surprised)
        {
            if (Scene != "combat") return;
            enemies.Add(arrival);
            arrival.Acted = null;
            arrival.Intent = surprised && arrival.Def.Opening != null ? content.Intents[arrival.Def.Opening] : NextIntent(arrival);
            // During the enemy phase their next move is chosen, and shown, along with everyone else's.
            if (!enemyPhase) Telegraph(arrival);
            Emit("appeared", arrival, Hero);
        }

        internal void EndFight(bool won)
        {
            if (!inFight) return;
            Bridge.EndSounds("fight");
            moved = true;
            EncounterDef ended = fight;
            Effect onWon = fightWon;
            IScope scope = fightScope;
            fight = null;
            inFight = false;
            fightWon = null;
            enemies.Clear();
            SetFocus(null);
            Scene = "room";
            Set("won", won ? 1 : 0);
            Emit("combat_end", Hero, null);
            if (Over) return;
            if (ended != null && ended.Hooks.On.TryGetValue("combat_end", out Effect hook)) hook(root);
            if (Over) return;
            if (won) onWon?.Invoke(scope);
        }

        internal void SetFocus(Entity next)
        {
            focus?.Set("focused", 0);
            focus = next;
            focus?.Set("focused", 1);
        }

        private void EnsureFocus()
        {
            if (focus == null || !focus.Active) SetFocus(enemies.FirstOrDefault(e => e.Active));
        }

        // ---- Rosters, roles and the schedule ----

        public IList<IEntity> Roster(string name)
        {
            if (!lists.TryGetValue(name, out List<IEntity> list)) lists[name] = list = new List<IEntity>();
            return list;
        }

        public void SetRole(string name, IEntity who)
        {
            if (who is Entity e) roles[name] = e;
            else roles.Remove(name);
        }

        public IReadOnlyList<string> Pool(string name) => area.Pools.TryGetValue(name, out List<string> ids) ? ids : (IReadOnlyList<string>)new string[0];

        public bool FirstTime(string key) => onceDone.Add(key);

        // ---- Loot and trade ----

        private string DecideLoot(LootDef table)
        {
            string item = RollLoot(table);
            if (item == null) return null;
            var decision = new Decision("loot", root, 0, item);
            decision.SetText("table", table.Id);
            Decide(decision);
            return decision.Cancelled ? null : decision.Text;
        }

        private string RollLoot(LootDef table)
        {
            List<LootEntry> open = table.Entries.Where(e => e.When == null || e.When(root)).ToList();
            int total = open.Sum(e => e.Weight);
            if (total <= 0) return null;
            int pick = Roll(total);
            foreach (LootEntry entry in open)
            {
                pick -= entry.Weight;
                if (pick > 0) continue;
                if (entry.Item != null) return entry.Item;
                if (entry.Loot != null) return RollLoot(content.Loot[entry.Loot]);
                return Bridge.RollTable(entry.GameTable);
            }
            return null;
        }

        internal void NewCart(CartDef def, IScope scope)
        {
            var request = new TradeRequest
            {
                Key = def.Id + "#" + ++cartCounter,
                Budget = def.Budget(scope),
                Name = localize(def.Name.Pack, def.Name.Key, new object[0]),
            };
            foreach (CartLine line in def.Stock)
            {
                for (int n = line.Count(scope); n > 0; n--)
                {
                    string id = line.Item;
                    if (id == null)
                    {
                        int index = Math.Max(0, Math.Min(line.Loot.Count - 1, line.Index(scope)));
                        id = DecideLoot(content.Loot[line.Loot[index]]);
                    }
                    if (id != null) request.Stock.Add(id);
                }
            }
            carts[def.Id] = new CartState { Def = def, Request = request };
        }

        internal void OpenTrade(string cart, Effect closed, IScope scope)
        {
            if (!carts.TryGetValue(cart, out CartState state) || !Bridge.OpenTrade(state.Request)) return;
            Trading = true;
            tradeClosed = closed;
            tradeScope = scope;
        }

        /// <summary>Called when the player puts the counters away.</summary>
        public void FinishTrade()
        {
            if (!Trading) return;
            Trading = false;
            TradeResult result = Bridge.CloseTrade();
            foreach (string name in result.Bought) Found(name);
            Set("trade_cash", result.CashChange);
            Set("trade_bought", result.Bought.Count);
            tradeClosed?.Invoke(tradeScope);
            tradeClosed = null;
        }

        internal void TakeCart(string cart, Effect before, IScope scope)
        {
            List<string> names = carts.TryGetValue(cart, out CartState state) ? Bridge.TakeCart(state.Request) : new List<string>();
            Set("cart_taken", names.Count);
            before?.Invoke(scope);
            foreach (string name in names) Found(name);
        }

        // ---- Hooks ----

        private void Emit(string name, Entity subject, Entity other)
        {
            var scope = new Scope(this, subject ?? Hero, other, null);
            foreach (KitDef kit in kits) Run(kit.Hooks, name, scope);
            Run(area.Hooks, name, scope);
            if (room != null) Run(room.Hooks, name, scope);
            if (fight != null && name != "combat_end") Run(fight.Hooks, name, scope);
            if (subject?.Def != null) Run(subject.Def.Hooks, name, scope);
            if (subject != null)
            {
                foreach (ActiveStatus status in subject.Statuses.ToList()) Run(status.Def.Hooks, name, scope);
            }
            foreach (Effect listener in content.Listeners.ListenersOf(name)) Guarded(name, () => listener(scope));
        }

        public Decision Decide(Decision decision)
        {
            foreach (Interceptor interceptor in content.Listeners.InterceptorsOf(decision.Name)) Guarded(decision.Name, () => interceptor(decision));
            return decision;
        }

        // A mod's handler can't take the run down with it, and can't recurse forever by triggering itself.
        private void Guarded(string name, Action handler)
        {
            if (!EnterShared(name)) return;
            try
            {
                handler();
            }
            catch (Exception ex)
            {
                Log("bad", "a mod's handler for " + name + " failed: " + ex.Message);
            }
            finally
            {
                LeaveShared();
            }
        }

        private void Run(Hooks hooks, string name, IScope scope)
        {
            if (hooks.On.TryGetValue(name, out Effect effect)) effect(scope);
        }

        // ---- Offering and doing things ----

        public List<RunOption> Options()
        {
            rendering++;
            try { return BuildOptions(); }
            finally { rendering--; }
        }

        private List<RunOption> BuildOptions()
        {
            var options = new List<RunOption>();
            if (Over || Trading) return options;
            EnsureFocus();
            if (currentEvent != null)
            {
                foreach (Interaction i in currentEvent.Choices) Offer(i, Hero, options);
            }
            foreach (KitDef kit in kits) foreach (Interaction i in kit.Interactions) Offer(i, Hero, options);
            foreach (Interaction i in area.Interactions) Offer(i, Hero, options);
            if (room != null) foreach (Interaction i in room.Interactions) Offer(i, Hero, options);
            foreach (ActiveStatus status in Hero.Statuses) foreach (Interaction i in status.Def.Interactions) Offer(i, Hero, options);
            foreach (Entity e in enemies.Where(e => e.Active))
            {
                foreach (Interaction i in e.Def.Interactions) Offer(i, e, options);
                foreach (ActiveStatus status in e.Statuses) foreach (Interaction i in status.Def.Interactions) Offer(i, e, options);
            }
            return options;
        }

        private void Offer(Interaction i, Entity owner, List<RunOption> options)
        {
            if (!i.In.Contains(Scene) || (i.Once && usedThisRoom.Contains(i))) return;
            IEnumerable<Entity> targets;
            if (i.Targets == "enemy") targets = enemies.Where(e => e.Active);
            else if (i.Targets == "focus") targets = focus != null ? new[] { focus } : new Entity[0];
            else targets = new[] { owner };
            foreach (Entity target in targets.ToList())
            {
                var scope = new Scope(this, Hero, target, null);
                if (i.Where != null && !i.Where(scope)) continue;
                if (i.Uses == null)
                {
                    AddOption(i, owner, target, null, null, scope, options);
                    continue;
                }
                bool any = false;
                foreach (RunItem item in Select(i.Uses, scope, null))
                {
                    if (any && !i.Uses.Each) break;
                    any |= OfferItem(i, owner, target, item, options);
                }
            }
        }

        private bool OfferItem(Interaction i, Entity owner, Entity target, RunItem item, List<RunOption> options)
        {
            var scope = new Scope(this, Hero, target, item);
            if (i.With == null) return AddOption(i, owner, target, item, null, scope, options);
            bool any = false;
            foreach (RunItem paired in Select(i.With, scope, item))
            {
                if (any && !i.With.Each) break;
                any |= AddOption(i, owner, target, item, paired, scope.WithPaired(paired), options);
            }
            return any;
        }

        /// <summary>The carried items a use matches, in its order. With a main item, this is choosing what to pair with it.</summary>
        internal IEnumerable<RunItem> Select(ItemUse use, Scope scope, RunItem main)
        {
            IEnumerable<RunItem> items = Bridge.Items().Where(it => use.Filter(it, scope));
            if (main != null && use.Where != null) items = items.Where(it => use.Where(scope.WithPaired(it)));
            if (use.Order != null)
            {
                items = items.OrderBy(it => use.Order(main != null ? (IScope)scope.WithPaired(it) : scope.With(scope.Self, scope.Target, it)));
            }
            if (use.Limit > 0) items = items.Take(use.Limit);
            return items.ToList();
        }

        private bool AddOption(Interaction i, Entity owner, Entity target, RunItem item, RunItem paired, IScope scope, List<RunOption> options)
        {
            if (i.When != null && !i.When(scope)) return false;
            int? percent = i.Preview != null ? Math.Max(0, Math.Min(100, i.Preview(scope))) : (int?)null;
            var option = new RunOption { Interaction = i, Owner = owner, Target = target, Item = item, Paired = paired, Label = Lines.Render(i.Label, scope, percent) };
            option.Info = Info(i, scope, percent, item, paired);
            if (i.Confirm != null && (i.Confirm.When == null || i.Confirm.When(scope)))
            {
                option.ConfirmText = Lines.Render(i.Confirm.Text, scope);
                option.ConfirmYes = Lines.Render(i.Confirm.Yes, scope);
                option.ConfirmNo = Lines.Render(i.Confirm.No, scope);
            }
            options.Add(option);
            return true;
        }

        private const int GoodOdds = 67;
        private const int BadOdds = 34;

        // Drawn like labels: without rolling. A check's odds go last unless a piece already shows them.
        private List<InfoPiece> Info(Interaction i, IScope scope, int? percent, RunItem item, RunItem paired)
        {
            IScope inner = percent != null ? Scope.From(scope).WithArg("percent", percent.Value) : scope;
            var pieces = new List<InfoPiece>();
            foreach (InfoDef def in i.Info ?? Enumerable.Empty<InfoDef>())
            {
                if (def.When != null && !def.When(inner)) continue;
                switch (def.Kind)
                {
                    case InfoKind.Text:
                        string text = Lines.Render(def.Text, inner);
                        if (text.Length > 0) pieces.Add(new InfoPiece { Kind = InfoKind.Text, Text = text, Tone = Lines.Choose(def.Text, inner)?.Tone ?? def.Tone });
                        break;
                    case InfoKind.Icon:
                        RunItem shown = def.Icon == "item" ? item : def.Icon == "with" ? paired : null;
                        if (shown != null) pieces.Add(new InfoPiece { Kind = InfoKind.Icon, Item = shown });
                        else if (def.Icon != "item" && def.Icon != "with") pieces.Add(new InfoPiece { Kind = InfoKind.Icon, Sprite = def.Icon });
                        break;
                    case InfoKind.Bar:
                        pieces.Add(new InfoPiece
                        {
                            Kind = InfoKind.Bar, Value = def.Bar(inner), Max = def.Max(inner), Tone = def.Tone,
                            Text = def.Text != null ? Lines.Render(def.Text, inner) : null,
                        });
                        break;
                }
            }
            if (percent != null && !(i.Info?.Any(d => d.ShowsOdds) ?? false))
            {
                string tone = percent.Value >= GoodOdds ? "good" : percent.Value < BadOdds ? "bad" : "plain";
                pieces.Add(new InfoPiece { Kind = InfoKind.Text, Text = FrameworkText("ef_odds", percent.Value), Tone = tone });
            }
            return pieces;
        }

        public void Perform(RunOption option)
        {
            if (Over) return;
            // Before anything the action does, so a sound the action itself starts lasts until the next press.
            Bridge.EndSounds("action");
            Interaction i = option.Interaction;
            string startedIn = Scene;
            bool ownChoice = currentEvent != null && currentEvent.Choices.Contains(i);
            moved = false;
            stayInEvent = false;
            usedThisRoom.Add(i);
            var scope = new Scope(this, Hero, option.Target, option.Item, option.Paired);
            if (i.Uses != null && i.Uses.Consume > 0 && option.Item != null) Bridge.Consume(option.Item, i.Uses.Consume);
            if (i.With != null && i.With.Consume > 0 && option.Paired != null) Bridge.Consume(option.Paired, i.With.Consume);
            // "item" plays the item's own sound, so each gun sounds like itself.
            if (i.Sound == "item") Bridge.PlayItemSound(option.Item, i.SoundElse);
            else if (!string.IsNullOrEmpty(i.Sound)) Bridge.PlaySound(i.Sound);
            diceThrown = 0;
            i.Body(scope);
            // Sounded before the enemies answer, so only the player's own dice are heard.
            if (diceThrown > 0) Bridge.PlayDice(diceThrown);
            if (Over) return;
            if (ownChoice && currentEvent != null && !moved && !stayInEvent && !i.Stay) EndEvent();
            if (startedIn == "combat" && Scene == "combat" && i.TakesTurn) EnemyTurn();
            else if (Scene == "combat" && !enemies.Any(e => e.Active)) EndFight(true);
            if (!Over && i.TakesTurn) Emit("after_action", Hero, null);
        }

        internal void EnemyTurn()
        {
            if (!enemies.Any(e => e.Active))
            {
                EndFight(true);
                return;
            }
            Emit("turn_end", Hero, null);
            enemyPhase = true;
            foreach (Entity enemy in enemies.ToList())
            {
                if (Over || Scene != "combat") break;
                if (!enemy.Active) continue;
                var scope = new Scope(this, enemy, Hero, null);
                Emit("turn_start", enemy, Hero);
                if (Over || Scene != "combat") break;
                if (!enemy.Active) continue;
                IntentDef acting = enemy.Intent;
                enemy.Acted = acting;
                acting.Do(scope);
                if (Over || Scene != "combat") break;
                TickStatuses(enemy);
            }
            enemyPhase = false;
            if (Over || Scene != "combat") return;
            TickStatuses(Hero);
            // Next moves are chosen once everyone has acted, so they see the room as it now is.
            foreach (Entity enemy in enemies.Where(e => e.Active))
            {
                bool intentChanged = enemy.Acted != null && enemy.Intent != enemy.Acted;
                if (!intentChanged)
                {
                    enemy.Intent = enemy.Acted?.Next != null && !Forced(enemy) ? content.Intents[enemy.Acted.Next] : NextIntent(enemy);
                }
                enemy.Acted = null;
                Telegraph(enemy);
            }
            if (!enemies.Any(e => e.Active))
            {
                EndFight(true);
                return;
            }
            turn++;
            Emit("turn_start", Hero, null);
        }

        private void TickStatuses(Entity who)
        {
            foreach (ActiveStatus status in who.Statuses.ToList())
            {
                if (status.Turns > 0 && --status.Turns == 0) who.Statuses.Remove(status);
            }
        }

        private static bool Forced(Entity enemy) => enemy.Statuses.Any(s => s.Def.Intent != null);

        private IntentDef NextIntent(Entity enemy)
        {
            ActiveStatus forcing = enemy.Statuses.FirstOrDefault(s => s.Def.Intent != null);
            if (forcing != null) return content.Intents[forcing.Def.Intent];
            var scope = new Scope(this, enemy, Hero, null);
            string chosen = Choose(enemy.Def.Behavior, scope);
            string decided = Decide(new Decision("intent", scope, 0, chosen)).Text;
            return (decided != null ? Find(content.Intents, decided, "intent") : null) ?? content.Intents[chosen];
        }

        private string Choose(List<BehaviorEntry> entries, IScope scope)
        {
            List<BehaviorEntry> open = entries.Where(b => b.When == null || b.When(scope)).ToList();
            int total = open.Sum(b => b.Weight);
            if (total <= 0) return FirstIntent(entries);
            int pick = Roll(total);
            foreach (BehaviorEntry entry in open)
            {
                pick -= entry.Weight;
                if (pick <= 0) return entry.Intent ?? Choose(entry.Nested, scope);
            }
            return FirstIntent(entries);
        }

        private static string FirstIntent(List<BehaviorEntry> entries) => entries[0].Intent ?? FirstIntent(entries[0].Nested);

        private void Telegraph(Entity enemy)
        {
            if (enemy.Intent?.Tell != null) Say(enemy.Intent.Tone, enemy.Intent.Tell, new Scope(this, enemy, Hero, null));
        }

        public string IntentLabel(Entity enemy) => enemy.Intent != null ? Lines.Render(enemy.Intent.Label, new Scope(this, enemy, Hero, null)) : "";

        // ---- The info window ----

        /// <summary>The info window's title: the area's own line if it has one, otherwise a default around the area's name.</summary>
        public string PanelTitle(string areaName)
        {
            rendering++;
            try { return BuildPanelTitle(areaName); }
            finally { rendering--; }
        }

        private string BuildPanelTitle(string areaName)
        {
            return area.Hud?.Title != null ? Lines.Render(Lines.Choose(area.Hud.Title, root), root) : FrameworkText("ef_panel_title", areaName, depth, rooms.Count);
        }

        /// <summary>What the info window shows right now, as rows of tinted pieces. An empty row is a separator.</summary>
        public List<List<HudSpan>> Hud()
        {
            rendering++;
            try { return BuildHud(); }
            finally { rendering--; }
        }

        private List<List<HudSpan>> BuildHud()
        {
            HudDef hud = area.Hud;
            var rows = new List<List<HudSpan>>();
            if (hud == null)
            {
                DefaultHud(rows);
                return rows;
            }
            foreach (HudRow row in hud.Rows)
            {
                if (row.When != null && !row.When(root)) continue;
                if (row.Separator)
                {
                    rows.Add(new List<HudSpan>());
                    continue;
                }
                var spans = new List<HudSpan>();
                foreach (HudPart part in row.Parts) AddParts(part, spans);
                if (spans.Count > 0) rows.Add(spans);
            }
            List<Entity> present = enemies.Where(e => e.Active).ToList();
            if (hud.PendingScenes.Contains(Scene))
            {
                foreach (Entity e in present) rows.Add(new List<HudSpan> { Span(hud.Pending, new Scope(this, Hero, e, null), "enemy") });
            }
            else if (present.Count == 0)
            {
                if (hud.Clear != null) rows.Add(new List<HudSpan> { Span(hud.Clear, root, "dim") });
            }
            else
            {
                foreach (Entity e in present)
                {
                    string marker = e == focus ? "> " : "  ";
                    HudSpan line = Span(hud.Enemy, new Scope(this, Hero, e, null), "plain");
                    rows.Add(new List<HudSpan> { new HudSpan(marker, "heading"), line });
                    rows.Add(new List<HudSpan> { new HudSpan("    " + IntentLabel(e), e.Intent?.Tone ?? "enemy") });
                }
            }
            return rows;
        }

        private void AddParts(HudPart part, List<HudSpan> spans)
        {
            if (part.EachItem != null)
            {
                foreach (RunItem item in Select(part.EachItem, root, null))
                {
                    var scope = new Scope(this, Hero, null, item);
                    if (part.When == null || part.When(scope)) spans.Add(Span(part.Line, scope, "plain"));
                }
                return;
            }
            if (part.When != null && !part.When(root)) return;
            if (Lines.Choose(part.Line, root) != null) spans.Add(Span(part.Line, root, "plain"));
        }

        private static HudSpan Span(Line line, IScope scope, string fallback)
        {
            Line chosen = Lines.Choose(line, scope);
            return new HudSpan(Lines.Render(chosen, scope), chosen?.Tone ?? fallback);
        }

        private void DefaultHud(List<List<HudSpan>> rows)
        {
            string status = FrameworkText("ef_status_hp", Math.Max(0, Hero.Get("hp")), Hero.Get("max_hp"));
            if (Scene == "combat") status += "   " + FrameworkText("ef_status_turn", turn);
            rows.Add(new List<HudSpan> { new HudSpan(status, "plain") });
            if (Hero.Statuses.Count > 0)
            {
                string names = string.Join(", ", Hero.Statuses.Select(s => localize(s.Def.Name.Pack, s.Def.Name.Key, new object[0])));
                rows.Add(new List<HudSpan> { new HudSpan(FrameworkText("ef_status_effects", names), "plain") });
            }
            foreach (Entity enemy in enemies.Where(e => e.Active))
            {
                rows.Add(new List<HudSpan> { new HudSpan(FrameworkText("ef_enemy_line", enemy.Name, enemy.Get("hp"), enemy.Get("max_hp"), IntentLabel(enemy)), enemy.Intent?.Tone ?? "plain") });
            }
        }
    }

    /// <summary>
    /// Lets the map show lines and check conditions when no run is going. Given a memory, memory.* there reads the same
    /// store runs write, so a condition can hide an area or quiet the street until something has happened on an earlier night.
    /// Without one, memory reads 0 and "".
    /// </summary>
    public sealed class MapContext : IRun, ITextHost, IMemoryHost
    {
        private readonly Localizer localize;
        private readonly Func<string, int> game;
        private readonly IMemory memory;
        private readonly Dictionary<string, string> texts = new Dictionary<string, string>();
        private readonly Entity nobody = new Entity("nobody", "", true, new string[0]);

        public MapContext(string areaId, Localizer localize, Func<string, int> game, IMemory memory = null)
        {
            AreaId = areaId;
            this.localize = localize;
            this.game = game;
            this.memory = memory;
            Root = new Scope(this, nobody, null, null);
        }

        public IScope Root { get; }
        public string AreaId { get; }
        public IEntity Player => nobody;
        public IReadOnlyList<IEntity> Enemies => new IEntity[0];
        public IEntity Focus => null;
        public bool Over => true;
        public string Scene => "map";
        public IFlow Flow => NoWorld.Instance;
        public ICombat Combat => NoWorld.Instance;
        public IInventory Inventory => NoWorld.Instance;
        public ITrade Trade => NoWorld.Instance;
        public int RollCheck(int sides) => Roll(sides);
        public bool FirstTime(string key) => false;
        public void SetRole(string name, IEntity who) { }
        public IList<IEntity> Roster(string name) => new List<IEntity>();
        public IReadOnlyList<string> Pool(string name) => new string[0];
        public IReadOnlyDictionary<string, List<string>> Intel => new Dictionary<string, List<string>>();
        public void Say(string tone, ILine line, IScope scope) { }
        public void NoteRoll(string text) { }
        public void NoteCheck(int sides, int natural, int modifier, int target) { }
        public void NoteDie(int sides, int natural) { }
        public void Wound(string kind) { }
        public string Reputation(string faction, double amount) => null;
        public void NightLog(string text, string color) { }
        public void PlaySound(string id) { }
        public void Emit(string hook, IEntity subject, IEntity other) { }
        public Decision Decide(Decision decision) => decision;
        public int Roll(int sides) => sides > 0 ? 1 : 0;
        public int Get(string variable) => 0;
        public void Set(string variable, int value) { }
        public string GetText(string variable) => texts.TryGetValue(variable, out string v) ? v : "";
        public void SetText(string variable, string value) => texts[variable] = value;
        public IEntity Role(string name) => null;
        public int Game(string name) => game(name);
        public void Log(string tone, string text) { }
        public void Finish(string outcome) { }
        public int Damage(IEntity target, int amount, IEntity source, bool pierce) => 0;
        string ITextHost.Localize(string pack, string key, object[] args) => localize(pack, key, args);
        IMemory IMemoryHost.Memory => memory;

        public string Render(Line line) => Lines.Render(line, Root);

        /// <summary>What the player's bag lacks for this area, as text. Empty means ready. A null bag means no backpack.</summary>
        public List<string> Missing(AreaDef area, IReadOnlyList<RunItem> bag)
        {
            var missing = new List<string>();
            if (area.Packing == null) return missing;
            if (bag == null)
            {
                if (area.Packing.NoBackpack != null) missing.Add(localize(area.Packing.NoBackpack.Pack, area.Packing.NoBackpack.Key, new object[0]));
                return missing;
            }
            foreach (PackingNeed need in area.Packing.Needs)
            {
                if (!bag.Any(item => need.Items(item, Root))) missing.Add(localize(need.Missing.Pack, need.Missing.Key, new object[0]));
            }
            return missing;
        }
    }

    /// <summary>Works out what the street says about an area today.</summary>
    public static class IntelRoller
    {
        /// <summary>
        /// For each slot, the positions in its pool that were picked. <paramref name="roll"/> gives 1 to sides. A slot whose
        /// when doesn't hold at <paramref name="scope"/> picks nothing and rolls no dice, so adding a when to one slot doesn't
        /// shift what the others pick. The check runs here, once per rolled day, rather than when a card asks whether it can
        /// start: that would run on every check and let a card appear or vanish mid-day as memory changes. Then, if the area has
        /// <c>intel_max</c>, keeps that many picks in slot order and drops the rest.
        /// </summary>
        public static Dictionary<string, List<int>> Roll(AreaDef area, Func<int, int> roll, IScope scope) => Roll(area, roll, scope, false);

        /// <summary>
        /// As above; with <paramref name="toldOnDemand"/>, told slots pick nothing and roll no dice here, like gated ones: their
        /// cards come one at a time from <see cref="Tell"/>. Without it a told slot rolls like any other, so a pack with no
        /// teller keeps its intel.
        /// </summary>
        public static Dictionary<string, List<int>> Roll(AreaDef area, Func<int, int> roll, IScope scope, bool toldOnDemand)
        {
            var result = new Dictionary<string, List<int>>();
            foreach (IntelSlot slot in area.Intel)
            {
                var picks = new List<int>();
                // Every slot is a key, including gated and poolless ones: IntelCards.Rolled indexes the result for each slot,
                // so registering after the early continues below would throw KeyNotFoundException in the game.
                result[slot.Id] = picks;
                if (toldOnDemand && slot.Told) continue;
                if (slot.When != null && !slot.When(scope)) continue;
                if (!area.Pools.TryGetValue(slot.Pool, out List<string> pool)) continue;
                var open = Enumerable.Range(0, pool.Count).ToList();
                for (int n = HowMany(slot, roll); n > 0 && open.Count > 0; n--)
                {
                    int at = open[roll(open.Count) - 1];
                    open.Remove(at);
                    picks.Add(at);
                }
            }
            if (area.IntelMax != null) Limit(area, result, Math.Max(0, area.IntelMax(scope)));
            return result;
        }

        // Every slot rolls first, as it would without a limit, so setting intel_max changes which cards show but not the
        // dice the slots draw. Trimming slot by slot instead would make a later slot's picks depend on the limit. Earlier
        // slots keep their cards first; within a slot, its first picks.
        private static void Limit(AreaDef area, Dictionary<string, List<int>> result, int left)
        {
            foreach (IntelSlot slot in area.Intel)
            {
                List<int> picks = result[slot.Id];
                if (picks.Count > left) picks.RemoveRange(left, picks.Count - left);
                left -= picks.Count;
            }
        }

        /// <summary>
        /// One area's told intel for the day, kept by the caller: what each slot has shown or reserved so far (rolled picks
        /// included, since intel_max counts them too) and how many cards each told slot may still hand out. A slot missing
        /// from <see cref="Budget"/> has not rolled its dice yet.
        /// </summary>
        public sealed class ToldDay
        {
            public readonly Dictionary<string, int> Budget = new Dictionary<string, int>();
            public readonly Dictionary<string, List<int>> Shown = new Dictionary<string, List<int>>();

            public int ShownCount => Shown.Values.Sum(s => s.Count);

            public List<int> ShownOf(IntelSlot slot)
            {
                if (!Shown.TryGetValue(slot.Id, out List<int> shown)) Shown[slot.Id] = shown = new List<int>();
                return shown;
            }
        }

        /// <summary>
        /// The told slots that can hand out a card now: when holds, an unshown pool position is left, the area is under its
        /// intel_max counting every card shown today, and the slot's daily budget is left. The budget is the slot's own dice
        /// (chance or counts), rolled the first time the slot gets this far in a day and kept in <paramref name="day"/>, so a
        /// told slot hands out at most what it would have shown at the day's roll. The cheap checks run before the dice so a
        /// slot that cannot show anyway draws none. <paramref name="slotId"/> limits to one slot.
        /// </summary>
        public static List<IntelSlot> TellCandidates(AreaDef area, Func<int, int> roll, IScope scope, ToldDay day, string slotId = null)
        {
            var candidates = new List<IntelSlot>();
            int limit = area.IntelMax != null ? Math.Max(0, area.IntelMax(scope)) : int.MaxValue;
            foreach (IntelSlot slot in area.Intel)
            {
                if (!slot.Told || (slotId != null && slot.Id != slotId)) continue;
                if (slot.When != null && !slot.When(scope)) continue;
                if (!area.Pools.TryGetValue(slot.Pool, out List<string> pool)) continue;
                if (day.ShownOf(slot).Count >= pool.Count) continue;
                if (day.ShownCount >= limit) continue;
                if (!day.Budget.TryGetValue(slot.Id, out int left)) day.Budget[slot.Id] = left = HowMany(slot, roll);
                if (left > 0) candidates.Add(slot);
            }
            return candidates;
        }

        /// <summary>
        /// Hands out one card of a slot <see cref="TellCandidates"/> returned: a random position of its pool not shown today,
        /// recorded in <paramref name="day"/> and charged to the slot's budget. Returns the pool position.
        /// </summary>
        public static int Tell(AreaDef area, IntelSlot slot, Func<int, int> roll, ToldDay day)
        {
            List<int> shown = day.ShownOf(slot);
            List<int> open = Enumerable.Range(0, area.Pools[slot.Pool].Count).Where(p => !shown.Contains(p)).ToList();
            int at = open[roll(open.Count) - 1];
            shown.Add(at);
            day.Budget[slot.Id]--;
            return at;
        }

        /// <summary>
        /// The text key a told card is worded by: <paramref name="template"/> with {area}, {slot} and {pick} filled in, the
        /// area and pick without their pack prefix, as key templates elsewhere name picks (run.$pick).
        /// </summary>
        public static string CardKey(string template, string areaId, string slotId, string pick)
        {
            string Bare(string id) => id == null ? "" : id.Substring(id.IndexOf(':') + 1);
            return (template ?? "").Replace("{area}", Bare(areaId)).Replace("{slot}", slotId ?? "").Replace("{pick}", Bare(pick));
        }

        private static int HowMany(IntelSlot slot, Func<int, int> roll)
        {
            int r = roll(100);
            if (slot.Counts == null) return r <= slot.Chance ? 1 : 0;
            int threshold = 0;
            for (int count = 0; count < slot.Counts.Length; count++)
            {
                threshold += slot.Counts[count];
                if (r <= threshold) return count;
            }
            return slot.Counts.Length - 1;
        }
    }
}
