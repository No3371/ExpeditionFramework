using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ExpeditionFramework.Core;

// ef-sim run: plays every row of the pack's sim.json and runs the checks every pack gets.
internal static class PackRun
{
    private const string Usage = "usage: ef-sim run <pack folder> [--runs <n>]";
    private const int MaxSteps = 2000;

    public static int Run(string frameworkRoot, string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("--")) { Console.WriteLine(Usage); return 2; }
        string folder = Path.GetFullPath(args[0]);
        int runsOverride = 0;
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--runs" && i + 1 < args.Length && int.TryParse(args[i + 1], out int n) && n > 0) { runsOverride = n; i++; }
            else { Console.WriteLine("unknown argument " + args[i]); Console.WriteLine(Usage); return 2; }
        }
        string id = Lint.ReadId(folder);
        if (id == null) { Console.WriteLine("ERROR no <ID> in manifest.xml"); return 2; }
        SimConfig config;
        try { config = SimConfig.Read(Path.Combine(folder, "sim.json")); }
        catch (FormatException e) { Console.WriteLine("ERROR sim.json: " + e.Message); return 2; }

        // Packs whose memory the rows seed or check stand in as installed, so the pack's reads of them load.
        var memoryPacks = new HashSet<string>(config.Rows.Where(row => row.Memory != null).SelectMany(row => row.Memory.Packs));
        Content content = Lint.Load(frameworkRoot, folder, id, new HashSet<string>(), out Dictionary<string, Dictionary<string, string>> text,
            out List<string> errors, out List<string> notes, memoryPacks);
        foreach (string note in notes) Console.WriteLine("WARN " + note);
        var failures = new List<string>(errors);          // load problems + text keys missing from en.csv
        if (content.Problems.Count == 0)
        {
            var areas = new Dictionary<string, AreaDef>();
            foreach (string name in config.Rows.Select(row => row.Area).Concat(config.Packing.Select(p => p.Area)).Concat(config.Loot.Select(l => l.Area)))
            {
                string areaId = AreaId(id, name);
                if (!content.Areas.TryGetValue(areaId, out AreaDef area)) { Console.WriteLine("ERROR sim.json: no area " + areaId); return 2; }
                areas[name] = area;
            }
            for (int r = 0; r < config.Rows.Count; r++)
            {
                foreach (SimRowCheck c in config.Rows[r].Expect.Where(c => c.Name.StartsWith("intel.", StringComparison.Ordinal)))
                {
                    string slot = c.Name.Substring("intel.".Length);
                    if (!areas[config.Rows[r].Area].Intel.Any(s => s.Id == slot))
                    {
                        Console.WriteLine("ERROR sim.json: runs[" + r + "].expect." + c.Name + ": area " + config.Rows[r].Area + " has no intel " + slot);
                        return 2;
                    }
                }
            }
            var problems = new HashSet<string>();
            var offered = new HashSet<string>();
            Localizer localize = (pack, key, a) =>
            {
                if (!text.TryGetValue(pack, out Dictionary<string, string> rows) || !rows.TryGetValue(key, out string t))
                {
                    problems.Add("missing text at run time: " + pack + ":" + key);
                    return key;
                }
                try { return string.Format(t, a); }
                catch (FormatException) { problems.Add("too few arguments (" + a.Length + ") for " + key + ": " + t); return t; }
            };
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,-18} {1,-12} {2,-8} {3,6} {4,8} {5,12} {6,10}",
                "area", "kit", "bot", "runs", "cleared", "knocked out", "extracted"));
            var memory = new List<string>();
            for (int r = 0; r < config.Rows.Count; r++)
            {
                SimRow row = config.Rows[r];
                config.Bots.TryGetValue(row.Bot, out RuleBot bot);
                SimBridge settings = row.Game == null ? config.Bridge : config.Bridge.With(row.Game);
                RowResult last = Play(content, id, areas[row.Area], row, row.Kits.Select(k => config.Kits[k]).ToList(), bot, settings, runsOverride > 0 ? runsOverride : row.Runs, localize, offered, problems);
                if (row.Memory != null) memory.AddRange(row.Memory.Check(id, last.Memory, "runs[" + r + "]"));
                memory.AddRange(CheckRow(row, last, "runs[" + r + "]"));
            }
            // A rule that never chose anything is usually a typo in its ids or a condition that can't hold; warn, don't fail.
            foreach (RuleBot used in config.Rows.Select(row => row.Bot).Distinct().Where(config.Bots.ContainsKey).Select(b => config.Bots[b]))
            {
                foreach (BotRule rule in used.Rules.Where(rule => rule.Picked == 0)) Console.WriteLine("WARN sim.json: " + rule.At + " never picked an option");
            }
            List<string> packing = CheckPacking(id, config, areas, localize);
            List<string> loot = CheckLoot(frameworkRoot, folder, id, content, config, localize, problems, memoryPacks);
            failures.AddRange(problems.OrderBy(p => p, StringComparer.Ordinal));
            failures.AddRange(memory);
            failures.AddRange(packing);
            failures.AddRange(loot);
            failures.AddRange(config.Offered.Where(o => !offered.Contains(o)).Select(o => "never offered: " + o));
        }
        foreach (string failure in failures) Console.WriteLine("FAIL " + failure);
        Console.WriteLine("ef-sim " + id + ": " + (failures.Count == 0 ? "all checks passed" : failures.Count + " check(s) failed"));
        return failures.Count == 0 ? 0 : 1;
    }

    // expect.packing: what MapContext.Missing says a bag lacks for an area, compared as the joined text the player sees.
    private static List<string> CheckPacking(string id, SimConfig config, Dictionary<string, AreaDef> areas, Localizer localize)
    {
        var failures = new List<string>();
        if (config.Packing.Count == 0) return failures;
        foreach (SimPacking c in config.Packing)
        {
            var map = new MapContext(AreaId(id, c.Area), localize, name => 0);
            List<RunItem> bag = c.Bag?.Select(s =>
            {
                var item = new RunItem { Handle = s.Id, Id = s.Id, Name = s.Id, Count = s.Count };
                item.Tags.UnionWith(s.Tags);
                foreach (KeyValuePair<string, int> p in s.Props) item.Props[p.Key] = p.Value;
                return item;
            }).ToList();
            string got = string.Join(", ", map.Missing(areas[c.Area], bag)), want = string.Join(", ", c.Missing);
            if (got != want) failures.Add(c.At + ": " + c.Area + " should need [" + want + "], got [" + got + "]");
        }
        Console.WriteLine("packing: " + config.Packing.Count + " case(s), " + failures.Count + " failed");
        return failures;
    }

    // expect.loot: rolls loot tables as a run of the case's area does, the pack reloaded with the case's mods counted as installed.
    private static List<string> CheckLoot(string frameworkRoot, string folder, string id, Content plain, SimConfig config, Localizer localize,
        HashSet<string> problems, ISet<string> memoryPacks)
    {
        var failures = new List<string>();
        if (config.Loot.Count == 0) return failures;
        int failed = 0;
        foreach (SimLoot c in config.Loot)
        {
            int before = failures.Count;
            Content content = plain;
            if (c.Mods.Count > 0)
            {
                content = Lint.Load(frameworkRoot, folder, id, new HashSet<string>(c.Mods), out _, out List<string> errors, out _, memoryPacks);
                failures.AddRange(errors.Select(e => c.At + " with " + string.Join(", ", c.Mods) + ": " + e));
            }
            string areaId = AreaId(id, c.Area);
            if (failures.Count == before && !content.Areas.ContainsKey(areaId)) failures.Add(c.At + ": no area " + areaId);
            if (failures.Count == before)
            {
                var random = new Random(c.Seed);
                var run = new ExpeditionRun(content, content.Areas[areaId], new KitBridge(new SimKit(), config.Bridge, content, random, problems),
                    s => random.Next(1, s + 1), localize, "you");
                var rolled = new HashSet<string>();
                foreach (string table in c.Tables)
                {
                    string tableId = AreaId(id, table);
                    if (!content.Loot.ContainsKey(tableId)) { failures.Add(c.At + ": no loot table " + tableId); continue; }
                    for (int n = 0; n < c.Rolls; n++)
                    {
                        string item = run.Inventory.RollLoot(tableId);
                        if (item != null) rolled.Add(item);
                    }
                }
                List<string> banned = c.NeverPrefix == null ? new List<string>()
                    : rolled.Where(item => item.StartsWith(c.NeverPrefix, StringComparison.Ordinal)).OrderBy(item => item, StringComparer.Ordinal).ToList();
                if (banned.Count > 0) failures.Add(c.At + ": rolled " + string.Join(", ", banned) + " (never_prefix " + c.NeverPrefix + ")");
                foreach (string must in c.MustRoll.Where(m => !rolled.Contains(m))) failures.Add(c.At + ": " + must + " never rolled");
            }
            if (failures.Count > before) failed++;
        }
        Console.WriteLine("loot: " + config.Loot.Count + " case(s), " + failed + " failed");
        return failures;
    }

    private static string AreaId(string id, string name) => name.Contains(':') ? name : id + ":" + name;

    // What a row leaves behind: the last night's memory and bridge, and how many nights each intel slot named anything.
    private sealed class RowResult { public IMemory Memory; public KitBridge Bridge; public readonly Dictionary<string, int> IntelNights = new Dictionary<string, int>(); }

    // runs[n].expect: end reputation and intel nights, compared as a memory expect is.
    private static IEnumerable<string> CheckRow(SimRow row, RowResult result, string at)
    {
        foreach (SimRowCheck c in row.Expect)
        {
            int value = c.Name.StartsWith("intel.", StringComparison.Ordinal)
                ? result.IntelNights.TryGetValue(c.Name.Substring("intel.".Length), out int n) ? n : 0
                : result.Bridge?.GameValue(c.Name) ?? 0;
            string where = at + ": " + c.Name;
            if (c.Min != null && c.Min == c.Max) { if (value != c.Min) yield return where + " should be " + c.Min + ", got " + value; }
            else if (c.Min is int min && value < min) yield return where + " should be at least " + min + ", got " + value;
            else if (c.Max is int max && value > max) yield return where + " should be at most " + max + ", got " + value;
        }
    }

    // Plays a row's runs and returns what its last night left and the intel tally.
    private static RowResult Play(Content content, string pack, AreaDef area, SimRow row, List<SimKit> kits, RuleBot bot, SimBridge settings, int runs,
        Localizer localize, HashSet<string> offered, HashSet<string> problems)
    {
        var random = new Random(row.Seed);
        // Intel has its own random so a row that turns intel on doesn't shift the dice of its runs against a row that doesn't.
        Random intelRandom = row.Intel ? new Random(unchecked(row.Seed * 7919 + 1)) : null;
        SimWorld world = row.Days ? new SimWorld() : null;
        int firstDay = settings.Game.TryGetValue("day", out int d0) ? d0 : 0;
        var result = new RowResult();
        string where = row.Area + " " + row.Kit + " " + row.Bot;
        int cleared = 0, knocked = 0, extracted = 0;
        // Runs share one store only when the row carries memory: rows sample independent nights, and a shared store would
        // make each run's odds depend on the runs before it.
        IMemory carried = row.Memory != null && row.Memory.Carry ? row.Memory.Start(pack) : null;
        IMemory memory = null;
        for (int n = 0; n < runs; n++)
        {
            memory = carried ?? row.Memory?.Start(pack) ?? new MemoryStore();
            if (world != null) world.Day = firstDay + n;
            var bridge = new KitBridge(kits[n % kits.Count], settings, content, random, problems, memory, world);
            Dictionary<string, List<string>> intel = row.Intel ? RollIntel(area, bridge, memory, localize, intelRandom, result.IntelNights) : null;
            var run = new ExpeditionRun(content, area, bridge, s => random.Next(1, s + 1), localize, "you", intel);
            result.Bridge = bridge;
            try
            {
                run.Begin();
                int steps = 0;
                while (!run.Over)
                {
                    if (run.Trading) run.FinishTrade();
                    if (++steps > MaxSteps) { problems.Add(where + ": a run never ended; scene " + run.Scene + " room " + run.Depth); break; }
                    List<RunOption> options = run.Options();
                    if (options.Count == 0) { problems.Add(where + ": no options in scene " + run.Scene + " at room " + run.Depth); break; }
                    foreach (RunOption option in options)
                    {
                        if (option.Label != null && option.Label.Contains("{")) problems.Add("unfilled label: " + option.Label);
                        foreach (InfoPiece piece in option.Info)
                        {
                            if (piece.Text != null && piece.Text.Contains("{")) problems.Add("unfilled info: " + piece.Text);
                        }
                        offered.Add(option.Interaction.Id);
                    }
                    RunOption choice = bot == null ? Wander(options, random) : bot.Choose(run, bridge, options, random);
                    if (choice == null) { problems.Add(where + ": no rule picked an option in scene " + run.Scene + " at room " + run.Depth); break; }
                    run.Perform(choice);
                    run.Hud();
                }
            }
            catch (Exception e)
            {
                problems.Add(where + ": a run threw " + e.GetType().Name + ": " + e.Message);
            }
            if (run.Outcome == "cleared" || run.Get("cleared") > 0) cleared++;
            if (run.Outcome == "knocked_out") knocked++;
            else if (run.Outcome != null) extracted++;
        }
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,-18} {1,-12} {2,-8} {3,6} {4,7:0.0}% {5,11:0.0}% {6,9:0.0}%",
            row.Area, row.Kit, row.Bot, runs, 100.0 * cleared / runs, 100.0 * knocked / runs, 100.0 * extracted / runs));
        result.Memory = memory;
        return result;
    }

    // Today's intel as the map would show it: each slot's picks as pool entries, its when read against this night's game
    // and memory. The game shows only cards whose afterhour event started; every rolled pick counts here.
    // A coin game read in a when draws from the run's random.
    private static Dictionary<string, List<string>> RollIntel(AreaDef area, KitBridge bridge, IMemory memory, Localizer localize, Random random,
        Dictionary<string, int> nights)
    {
        var map = new MapContext(area.Id, localize, bridge.GameValue, memory);
        Dictionary<string, List<int>> picks = IntelRoller.Roll(area, s => random.Next(1, s + 1), map.Root);
        var today = new Dictionary<string, List<string>>();
        foreach (IntelSlot slot in area.Intel)
        {
            today[slot.Id] = picks[slot.Id].Select(p => area.Pools[slot.Pool][p]).ToList();
            if (today[slot.Id].Count > 0) nights[slot.Id] = (nights.TryGetValue(slot.Id, out int k) ? k : 0) + 1;
        }
        return today;
    }

    // The built-in "random" bot: any option, but heads home only rarely so runs reach the end.
    private static RunOption Wander(List<RunOption> options, Random random)
    {
        List<RunOption> pool = options.Where(o => o.Interaction.Id != "head_home" || random.Next(100) < 5).ToList();
        if (pool.Count == 0) pool = options;
        return pool[random.Next(pool.Count)];
    }
}
