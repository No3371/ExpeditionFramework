using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExpeditionFramework.Core;

internal sealed class SimItem { public string Id; public int Count = 1; public readonly List<string> Tags = new List<string>(); public readonly Dictionary<string, int> Props = new Dictionary<string, int>(); }
internal sealed class SimKit { public int Credits; public readonly List<SimItem> Items = new List<SimItem>(); }
internal sealed class SimRow
{
    public string Area, Bot; public int Runs, Seed;
    public readonly List<string> Kits = new List<string>();     // run n uses Kits[n % Kits.Count]
    public SimMemory Memory;                                    // null: no "memory", every run starts with empty memory
    public SimGame Game;                                        // null: the file's bridge.game alone
    public bool Days;                                           // nights follow each other: game.day +1 a night, reputation carries
    public bool Intel;                                          // roll each night's intel
    public readonly List<SimRowCheck> Expect = new List<SimRowCheck>();
    public string Kit => string.Join("+", Kits);
}
// runs[n].game: game.NAME values for one row, laid over bridge.game.
internal sealed class SimGame { public readonly Dictionary<string, int> Numbers = new Dictionary<string, int>(); public readonly HashSet<string> Coin = new HashSet<string>(); }
// runs[n].expect entry: rep.F / rep_exact.F (end of the row's last night) or intel.SLOT (nights the slot named anything).
internal sealed class SimRowCheck { public string Name; public int? Min, Max; }
internal sealed class SimPacking { public string At, Area; public List<SimItem> Bag; public List<string> Missing; }  // Bag null = no backpack

// runs[n].memory: what memory.* holds before the row's runs, whether one run's writes reach the next, and what it must
// hold after the last run. Names are as the pack writes or reads them after "memory." ($ for text): NAME is the pack's own,
// PACK.NAME another pack's.
internal sealed class SimMemory
{
    // Keyed by owning pack (null: the sim's pack) and name, without the $ for text, as the store keeps them.
    public readonly Dictionary<(string Pack, string Name), int> Numbers = new Dictionary<(string Pack, string Name), int>();
    public readonly Dictionary<(string Pack, string Name), string> Texts = new Dictionary<(string Pack, string Name), string>();
    public bool Carry;                                                                     // false: every run starts from the start values
    public readonly List<SimMemoryCheck> Expect = new List<SimMemoryCheck>();

    // The other packs named in start or expect.
    public IEnumerable<string> Packs =>
        Numbers.Keys.Concat(Texts.Keys).Select(k => k.Pack).Concat(Expect.Select(c => c.Pack)).Where(p => p != null).Distinct();

    public MemoryStore Start(string pack)
    {
        var store = new MemoryStore();
        foreach (KeyValuePair<(string Pack, string Name), int> v in Numbers) store.Set(v.Key.Pack ?? pack, v.Key.Name, v.Value);
        foreach (KeyValuePair<(string Pack, string Name), string> v in Texts) store.SetText(v.Key.Pack ?? pack, v.Key.Name, v.Value);
        return store;
    }

    // One failure per expect entry the store misses.
    public List<string> Check(string pack, IMemory memory, string at)
    {
        var failures = new List<string>();
        foreach (SimMemoryCheck c in Expect)
        {
            string where = at + ": memory." + c.Name;
            string owner = c.Pack ?? pack;
            if (c.Key.StartsWith("$"))
            {
                string got = memory.GetText(owner, c.Key.Substring(1));
                if (got != c.Text) failures.Add(where + " should be \"" + c.Text + "\", got \"" + got + "\"");
                continue;
            }
            int value = memory.Get(owner, c.Key);
            if (c.Min != null && c.Min == c.Max) { if (value != c.Min) failures.Add(where + " should be " + c.Min + ", got " + value); }
            else if (c.Min is int min && value < min) failures.Add(where + " should be at least " + min + ", got " + value);
            else if (c.Max is int max && value > max) failures.Add(where + " should be at most " + max + ", got " + value);
        }
        return failures;
    }
}
// Name as written; Pack (null: the sim's pack) and Key split from it, Key keeping its $. Text only for $ names.
internal sealed class SimMemoryCheck { public string Name, Pack, Key, Text; public int? Min, Max; }

// expect.loot[n]: roll each table Rolls times in one run of Area, with Mods also counted as installed.
internal sealed class SimLoot
{
    public string At, Area, NeverPrefix; public int Rolls, Seed;   // NeverPrefix null = no prefix check
    public readonly List<string> Tables = new List<string>();
    public readonly List<string> Mods = new List<string>();
    public readonly List<string> MustRoll = new List<string>();
}

// sim.json "bridge": how the stand-in game answers the engine. Each field left out keeps the default noted beside it.
internal sealed class SimBridge
{
    public readonly Dictionary<string, int> Game = new Dictionary<string, int>();   // game.NAME values; unlisted names read 0
    public readonly HashSet<string> CoinGame = new HashSet<string>();             // game.NAME read as 0 or 1 from the run's random
    public bool TradeOpens;                                                        // false: the trade effect never opens a screen
    public int? LoseCashUpTo;                                                      // null: a closed trade changes no cash
    public readonly List<string> Cart = new List<string>();                        // what take_cart hands over; empty by default
    public bool TakeOldest;                                                        // false: take_back hands over the newest found item
    public List<string> LoseCarried;                                               // null: lose_items rolls each carried item

    // A copy with a row's game values laid over this one's. A row's rep.F or rep_exact.F drops the file's other spelling of
    // that faction: KitBridge reads rep_exact first, so a file-wide rep_exact would otherwise hide the row's rep.
    public SimBridge With(SimGame row)
    {
        var copy = new SimBridge { TradeOpens = TradeOpens, LoseCashUpTo = LoseCashUpTo, TakeOldest = TakeOldest, LoseCarried = LoseCarried };
        foreach (KeyValuePair<string, int> v in Game) copy.Game[v.Key] = v.Value;
        copy.CoinGame.UnionWith(CoinGame);
        copy.Cart.AddRange(Cart);
        foreach (string name in row.Numbers.Keys.Concat(row.Coin))
        {
            string faction = GameReads.RepFaction(name, out _);
            if (faction != null) { copy.Game.Remove("rep." + faction); copy.Game.Remove("rep_exact." + faction); copy.CoinGame.Remove("rep." + faction); copy.CoinGame.Remove("rep_exact." + faction); }
            copy.Game.Remove(name); copy.CoinGame.Remove(name);
        }
        foreach (KeyValuePair<string, int> v in row.Numbers) copy.Game[v.Key] = v.Value;
        copy.CoinGame.UnionWith(row.Coin);
        return copy;
    }
}

// sim.json v1. Read strictly: an unknown key or a wrong type is an error, so a typo can't silently change what runs.
internal sealed class SimConfig
{
    public const string RandomBot = "random";
    public readonly Dictionary<string, SimKit> Kits = new Dictionary<string, SimKit>();
    public readonly Dictionary<string, RuleBot> Bots = new Dictionary<string, RuleBot>();
    public readonly List<SimRow> Rows = new List<SimRow>();
    public readonly List<string> Offered = new List<string>();
    public readonly List<SimPacking> Packing = new List<SimPacking>();
    public readonly List<SimLoot> Loot = new List<SimLoot>();
    public SimBridge Bridge = new SimBridge();

    public static SimConfig Read(string file)
    {
        if (!File.Exists(file)) throw new FormatException("no sim.json in the pack folder");
        Dictionary<string, object> top = Obj(Json.Parse(File.ReadAllText(file)), "sim.json");
        Only(top, "sim.json", "version", "kits", "bots", "runs", "expect", "bridge");
        if (Int(Need(top, "version", "sim.json"), "version") != 1) throw new FormatException("version must be 1");
        var config = new SimConfig();
        foreach (KeyValuePair<string, object> k in Obj(Need(top, "kits", "sim.json"), "kits"))
        {
            string at = "kits." + k.Key;
            Dictionary<string, object> kit = Obj(k.Value, at);
            Only(kit, at, "credits", "items");
            var parsed = new SimKit { Credits = kit.TryGetValue("credits", out object c) ? Int(c, at + ".credits") : 0 };
            if (kit.TryGetValue("items", out object i)) parsed.Items.AddRange(Items(i, at + ".items"));
            config.Kits[k.Key] = parsed;
        }
        if (config.Kits.Count == 0) throw new FormatException("kits is empty");
        if (top.TryGetValue("bots", out object bots))
        {
            foreach (KeyValuePair<string, object> b in Obj(bots, "bots")) config.Bots[b.Key] = Bot(b.Key, b.Value);
        }
        List<object> rows = List(Need(top, "runs", "sim.json"), "runs");
        if (rows.Count == 0) throw new FormatException("runs is empty");
        for (int n = 0; n < rows.Count; n++)
        {
            string at = "runs[" + n + "]";
            Dictionary<string, object> row = Obj(rows[n], at);
            Only(row, at, "area", "kit", "bot", "runs", "seed", "memory", "game", "days", "intel", "expect");
            var r = new SimRow
            {
                Area = Str(Need(row, "area", at), at + ".area"),
                Bot = row.TryGetValue("bot", out object b) ? Str(b, at + ".bot") : RandomBot,
                Runs = Int(Need(row, "runs", at), at + ".runs"),
                Seed = row.TryGetValue("seed", out object s) ? Int(s, at + ".seed") : 42 + n,
            };
            object kitValue = Need(row, "kit", at);
            if (kitValue is string one) r.Kits.Add(one);
            else if (kitValue is List<object> many) r.Kits.AddRange(many.Select(x => Str(x, at + ".kit")));
            else throw new FormatException(at + ".kit must be a kit name or a list of kit names");
            if (r.Kits.Count == 0) throw new FormatException(at + ".kit is empty");
            foreach (string kit in r.Kits.Where(kit => !config.Kits.ContainsKey(kit))) throw new FormatException(at + ".kit: no kit named " + kit);
            if (r.Bot != RandomBot && !config.Bots.ContainsKey(r.Bot))
                throw new FormatException(at + ".bot: unknown bot " + r.Bot + " (known: " + string.Join(", ", new[] { RandomBot }.Concat(config.Bots.Keys)) + ")");
            if (r.Runs <= 0) throw new FormatException(at + ".runs must be above 0");
            if (row.TryGetValue("memory", out object m)) r.Memory = MemoryOf(m, at + ".memory");
            if (row.TryGetValue("game", out object g)) { r.Game = new SimGame(); ReadGame(g, at + ".game", r.Game.Numbers, r.Game.Coin); }
            if (row.TryGetValue("days", out object d)) r.Days = d is bool db ? db : throw new FormatException(at + ".days must be true or false");
            if (row.TryGetValue("intel", out object it)) r.Intel = it is bool ib ? ib : throw new FormatException(at + ".intel must be true or false");
            if (row.TryGetValue("expect", out object x)) r.Expect.AddRange(RowExpect(x, at + ".expect", r.Intel));
            config.Rows.Add(r);
        }
        if (top.TryGetValue("expect", out object e))
        {
            Dictionary<string, object> expect = Obj(e, "expect");
            Only(expect, "expect", "offered", "packing", "loot");
            if (expect.TryGetValue("offered", out object o)) config.Offered.AddRange(List(o, "expect.offered").Select(x => Str(x, "expect.offered")));
            if (expect.TryGetValue("packing", out object pk))
            {
                List<object> cases = List(pk, "expect.packing");
                for (int n = 0; n < cases.Count; n++)
                {
                    string at = "expect.packing[" + n + "]";
                    Dictionary<string, object> c = Obj(cases[n], at);
                    Only(c, at, "area", "bag", "missing");
                    config.Packing.Add(new SimPacking
                    {
                        At = at,
                        Area = Str(Need(c, "area", at), at + ".area"),
                        Bag = c.TryGetValue("bag", out object bag) ? Items(bag, at + ".bag") : null,
                        Missing = List(Need(c, "missing", at), at + ".missing").Select(x => Str(x, at + ".missing")).ToList(),
                    });
                }
            }
            if (expect.TryGetValue("loot", out object lt))
            {
                List<object> cases = List(lt, "expect.loot");
                for (int n = 0; n < cases.Count; n++) config.Loot.Add(LootCase(cases[n], "expect.loot[" + n + "]"));
            }
        }
        if (top.TryGetValue("bridge", out object br)) config.Bridge = ReadBridge(br);
        return config;
    }

    // bridge: { game?, trade?, take_back?, lose_carried? }.
    private static SimBridge ReadBridge(object value)
    {
        Dictionary<string, object> obj = Obj(value, "bridge");
        Only(obj, "bridge", "game", "trade", "take_back", "lose_carried");
        var bridge = new SimBridge();
        if (obj.TryGetValue("game", out object g)) ReadGame(g, "bridge.game", bridge.Game, bridge.CoinGame);
        if (obj.TryGetValue("trade", out object t))
        {
            Dictionary<string, object> trade = Obj(t, "bridge.trade");
            Only(trade, "bridge.trade", "opens", "lose_cash_up_to", "cart");
            if (trade.TryGetValue("opens", out object o)) bridge.TradeOpens = o is bool b ? b : throw new FormatException("bridge.trade.opens must be true or false");
            if (trade.TryGetValue("lose_cash_up_to", out object l))
            {
                bridge.LoseCashUpTo = Int(l, "bridge.trade.lose_cash_up_to");
                // KitBridge draws Next(0, most + 1); int.MaxValue would overflow that bound.
                if (bridge.LoseCashUpTo < 0 || bridge.LoseCashUpTo == int.MaxValue) throw new FormatException("bridge.trade.lose_cash_up_to must be between 0 and " + (int.MaxValue - 1));
            }
            if (trade.TryGetValue("cart", out object c)) bridge.Cart.AddRange(List(c, "bridge.trade.cart").Select(x => Str(x, "bridge.trade.cart")));
        }
        if (obj.TryGetValue("take_back", out object tb))
        {
            string order = Str(tb, "bridge.take_back");
            if (order != "newest" && order != "oldest") throw new FormatException("bridge.take_back must be newest or oldest");
            bridge.TakeOldest = order == "oldest";
        }
        if (obj.TryGetValue("lose_carried", out object lc))
        {
            if (lc is string roll) { if (roll != "roll") throw new FormatException("bridge.lose_carried must be roll or a list of names"); }
            else if (lc is List<object> names) bridge.LoseCarried = names.Select(x => Str(x, "bridge.lose_carried")).ToList();
            else throw new FormatException("bridge.lose_carried must be roll or a list of names");
        }
        return bridge;
    }

    // NAME: a whole number or "coin", as bridge.game and runs[n].game take.
    private static void ReadGame(object value, string at, Dictionary<string, int> numbers, HashSet<string> coin)
    {
        foreach (KeyValuePair<string, object> v in Obj(value, at))
        {
            string vat = at + "." + v.Key;
            if (v.Value is string s)
            {
                if (s != "coin") throw new FormatException(vat + " must be a whole number or coin");
                coin.Add(v.Key);
            }
            else numbers[v.Key] = Int(v.Value, vat);
        }
    }

    // A whole number (exactly) or { min?, max? } with at least one.
    private static (int? Min, int? Max) ReadRange(object value, string at)
    {
        if (value is Dictionary<string, object> range)
        {
            Only(range, at, "min", "max");
            if (range.Count == 0) throw new FormatException(at + " needs min or max");
            int? min = range.TryGetValue("min", out object lo) ? Int(lo, at + ".min") : (int?)null;
            int? max = range.TryGetValue("max", out object hi) ? Int(hi, at + ".max") : (int?)null;
            if (min > max) throw new FormatException(at + ".min is above max");
            return (min, max);
        }
        if (value is double) { int n = Int(value, at); return (n, n); }
        throw new FormatException(at + " must be a whole number or {min, max}");
    }

    // runs[n].expect: rep.F / rep_exact.F of a faction the game has, or intel.SLOT on a row with "intel": true.
    private static List<SimRowCheck> RowExpect(object value, string at, bool intel)
    {
        var checks = new List<SimRowCheck>();
        foreach (KeyValuePair<string, object> v in Obj(value, at))
        {
            string vat = at + "." + v.Key;
            string faction = GameReads.RepFaction(v.Key, out _);
            if (faction != null) { if (!GameReads.IsFaction(faction)) throw new FormatException(vat + ": " + GameReads.UnknownFaction(faction)); }
            else if (v.Key.StartsWith("intel.", StringComparison.Ordinal) && v.Key.Length > "intel.".Length)
            { if (!intel) throw new FormatException(vat + " needs \"intel\": true on the row"); }
            else throw new FormatException(vat + ": a row expects rep.FACTION, rep_exact.FACTION or intel.SLOT");
            (int? min, int? max) = ReadRange(v.Value, vat);
            checks.Add(new SimRowCheck { Name = v.Key, Min = min, Max = max });
        }
        return checks;
    }

    // runs[n].memory: { start?, carry?, expect? }. start and expect are keyed by name (visits, $rival, other_pack.visits);
    // expect takes a whole number (exactly), { min?, max? } or, for a $ name, text.
    private static SimMemory MemoryOf(object value, string at)
    {
        Dictionary<string, object> obj = Obj(value, at);
        Only(obj, at, "start", "carry", "expect");
        var memory = new SimMemory();
        if (obj.TryGetValue("carry", out object c)) memory.Carry = c is bool b ? b : throw new FormatException(at + ".carry must be true or false");
        if (obj.TryGetValue("start", out object s))
        {
            foreach (KeyValuePair<string, object> v in Obj(s, at + ".start"))
            {
                string vat = at + ".start." + v.Key;
                (string pack, string name) = MemoryName(v.Key, vat);
                if (name.StartsWith("$")) memory.Texts[(pack, name.Substring(1))] = Str(v.Value, vat);
                else memory.Numbers[(pack, name)] = Int(v.Value, vat);
            }
        }
        if (obj.TryGetValue("expect", out object e))
        {
            foreach (KeyValuePair<string, object> v in Obj(e, at + ".expect"))
            {
                string vat = at + ".expect." + v.Key;
                (string pack, string name) = MemoryName(v.Key, vat);
                var check = new SimMemoryCheck { Name = v.Key, Pack = pack, Key = name };
                if (name.StartsWith("$")) check.Text = Str(v.Value, vat);
                else (check.Min, check.Max) = ReadRange(v.Value, vat);
                memory.Expect.Add(check);
            }
        }
        return memory;
    }

    // Splits a memory name into its owning pack (null: the sim's pack) and the name, $ kept. "memory.visits" would seed a
    // name the pack never reads, so it is refused.
    private static (string Pack, string Name) MemoryName(string name, string at)
    {
        if (name.StartsWith("memory.")) throw new FormatException(at + ": leave out memory. (write visits or $rival)");
        if (name.Length == 0 || name == "$") throw new FormatException(at + ": empty memory name");
        if (!Expr.MemoryKey(name, "", out string pack, out string key))
        {
            throw new FormatException(at + ": a memory name is NAME or PACK.NAME (visits, $rival, other_pack.visits)");
        }
        return (pack.Length == 0 ? null : pack, key);
    }

    // expect.loot[n]: { area, tables, rolls, seed?, mods?, never_prefix?, must_roll? } with at least one of never_prefix and must_roll.
    private static SimLoot LootCase(object value, string at)
    {
        Dictionary<string, object> c = Obj(value, at);
        Only(c, at, "area", "tables", "rolls", "seed", "mods", "never_prefix", "must_roll");
        var loot = new SimLoot
        {
            At = at,
            Area = Str(Need(c, "area", at), at + ".area"),
            Rolls = Int(Need(c, "rolls", at), at + ".rolls"),
            Seed = c.TryGetValue("seed", out object s) ? Int(s, at + ".seed") : 42,
            NeverPrefix = c.TryGetValue("never_prefix", out object p) ? Str(p, at + ".never_prefix") : null,
        };
        loot.Tables.AddRange(List(Need(c, "tables", at), at + ".tables").Select(x => Str(x, at + ".tables")));
        if (c.TryGetValue("mods", out object m)) loot.Mods.AddRange(List(m, at + ".mods").Select(x => Str(x, at + ".mods")));
        if (c.TryGetValue("must_roll", out object r)) loot.MustRoll.AddRange(List(r, at + ".must_roll").Select(x => Str(x, at + ".must_roll")));
        if (loot.Tables.Count == 0) throw new FormatException(at + ".tables is empty");
        if (loot.Rolls <= 0) throw new FormatException(at + ".rolls must be above 0");
        if (loot.NeverPrefix == "") throw new FormatException(at + ".never_prefix is empty");
        if (loot.NeverPrefix == null && loot.MustRoll.Count == 0) throw new FormatException(at + ": needs never_prefix or must_roll");
        return loot;
    }

    // bots.NAME: a non-empty list of rules { when?, pick, target? } or { when?, first_except }.
    private static RuleBot Bot(string name, object value)
    {
        string at = "bots." + name;
        if (name == RandomBot) throw new FormatException(at + ": random is built in and can't be redefined");
        List<object> rules = List(value, at);
        if (rules.Count == 0) throw new FormatException(at + " has no rules");
        var bot = new RuleBot(name);
        for (int n = 0; n < rules.Count; n++)
        {
            string rat = at + "[" + n + "]";
            Dictionary<string, object> rule = Obj(rules[n], rat);
            Only(rule, rat, "when", "pick", "target", "first_except");
            var parsed = new BotRule { At = rat };
            if (rule.TryGetValue("when", out object w))
            {
                parsed.When = Expr.Compile(Str(w, rat + ".when"), out string error, bot.Named);
                if (error != null) throw new FormatException(rat + ".when: " + error);
            }
            bool pick = rule.TryGetValue("pick", out object p), except = rule.TryGetValue("first_except", out object x);
            if (pick == except) throw new FormatException(rat + ": needs exactly one of pick or first_except");
            if (pick)
            {
                parsed.Pick = List(p, rat + ".pick").Select(id => Str(id, rat + ".pick")).ToList();
                if (parsed.Pick.Count == 0) throw new FormatException(rat + ".pick is empty");
            }
            else parsed.Except = List(x, rat + ".first_except").Select(id => Str(id, rat + ".first_except")).ToList();
            if (rule.TryGetValue("target", out object t))
            {
                if (!pick) throw new FormatException(rat + ".target only goes with pick");
                if (Str(t, rat + ".target") != "weakest") throw new FormatException(rat + ".target must be weakest");
                parsed.Weakest = true;
            }
            bot.Rules.Add(parsed);
        }
        return bot;
    }

    // A list of items { id, count?, tags?, props? }, as in kits.NAME.items and expect.packing[n].bag.
    private static List<SimItem> Items(object value, string at)
    {
        List<object> items = List(value, at);
        var parsed = new List<SimItem>();
        for (int n = 0; n < items.Count; n++)
        {
            string iat = at + "[" + n + "]";
            Dictionary<string, object> item = Obj(items[n], iat);
            Only(item, iat, "id", "count", "tags", "props");
            var si = new SimItem { Id = Str(Need(item, "id", iat), iat + ".id") };
            if (item.TryGetValue("count", out object cnt)) si.Count = Int(cnt, iat + ".count");
            if (item.TryGetValue("tags", out object tags)) si.Tags.AddRange(List(tags, iat + ".tags").Select(t => Str(t, iat + ".tags").TrimStart('#')));
            if (item.TryGetValue("props", out object props))
            {
                foreach (KeyValuePair<string, object> p in Obj(props, iat + ".props")) si.Props[p.Key] = Int(p.Value, iat + ".props." + p.Key);
            }
            parsed.Add(si);
        }
        return parsed;
    }

    private static object Need(Dictionary<string, object> obj, string key, string at) =>
        obj.TryGetValue(key, out object v) ? v : throw new FormatException(at + ": missing " + key);

    private static void Only(Dictionary<string, object> obj, string at, params string[] keys)
    {
        foreach (string key in obj.Keys.Where(k => !keys.Contains(k))) throw new FormatException(at + ": unknown key " + key);
    }

    private static Dictionary<string, object> Obj(object v, string at) => v as Dictionary<string, object> ?? throw new FormatException(at + " must be an object");
    private static List<object> List(object v, string at) => v as List<object> ?? throw new FormatException(at + " must be a list");
    private static string Str(object v, string at) => v as string ?? throw new FormatException(at + " must be text");
    private static int Int(object v, string at) => v is double d && d == Math.Floor(d) && Math.Abs(d) <= int.MaxValue ? (int)d : throw new FormatException(at + " must be a whole number");
}
