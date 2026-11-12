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
    public string Kit => string.Join("+", Kits);
}
internal sealed class SimPacking { public string At, Area; public List<SimItem> Bag; public List<string> Missing; }  // Bag null = no backpack

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
            Only(row, at, "area", "kit", "bot", "runs", "seed");
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
        if (obj.TryGetValue("game", out object g))
        {
            foreach (KeyValuePair<string, object> v in Obj(g, "bridge.game"))
            {
                string at = "bridge.game." + v.Key;
                if (v.Value is string s)
                {
                    if (s != "coin") throw new FormatException(at + " must be a whole number or coin");
                    bridge.CoinGame.Add(v.Key);
                }
                else bridge.Game[v.Key] = Int(v.Value, at);
            }
        }
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
