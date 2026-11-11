using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExpeditionFramework.Api;
using ExpeditionFramework.Core;

// Loads the standard kits and the sample area, checks the content, then plays it many times with random choices.
internal static class Program
{
    private const string Framework = "expedition_framework";
    private const string Sample = "sample_area";
    private static string root;
    private static int failures;

    private static int Main()
    {
        root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        Dictionary<(string, string), string> text = LoadText();
        Content content = LoadAll(out Registry registry);
        foreach (string problem in content.Problems) Fail("content: " + problem);
        foreach ((string pack, string key) in content.TextKeys.Where(k => !text.ContainsKey(k))) Fail("missing text " + pack + ":" + key);

        TestExpressions();
        TestBadContentIsReported();
        TestOptionalData();
        TestPatches();
        TestModEffectIsAPeer();
        TestPublicApi();
        if (content.Problems.Count > 0) return Finish();
        TestWeakPointOptions(content, text);
        Play(content, text, int.Parse(Environment.GetEnvironmentVariable("SIM_RUNS") ?? "5000"));
        return Finish();
    }

    private static int Finish()
    {
        Console.WriteLine(failures == 0 ? "All checks passed" : failures + " check(s) failed");
        return failures == 0 ? 0 : 1;
    }

    private static void Fail(string message)
    {
        failures++;
        Console.WriteLine("FAIL " + message);
    }

    private static void Check(bool ok, string message) { if (!ok) Fail(message); }

    private static Func<string, string> Reader(string folder) => file => File.ReadAllText(Path.Combine(folder, file));

    private static Content LoadAll(out Registry registry)
    {
        var content = new Content();
        registry = new Registry();
        var loader = new PackLoader(content, registry);
        loader.Load(Framework, "ExpeditionFramework/main.json", Reader(root));
        loader.Load(Sample, "ExpeditionFramework/main.json", Reader(Path.Combine(root, "samples", "SampleArea")));
        loader.Finish();
        return content;
    }

    private static Dictionary<(string, string), string> LoadText()
    {
        var text = new Dictionary<(string, string), string>();
        void Read(string pack, string file)
        {
            foreach (string line in File.ReadAllLines(file))
            {
                int comma = line.IndexOf(',');
                if (comma > 0) text[(pack, line.Substring(0, comma))] = line.Substring(comma + 1);
            }
        }
        Read(Framework, Path.Combine(root, "Localization", "en.csv"));
        Read(Sample, Path.Combine(root, "samples", "SampleArea", "Localization", "en.csv"));
        return text;
    }

    private static Localizer Loc(Dictionary<(string, string), string> text) =>
        (pack, key, args) => text.TryGetValue((pack, key), out string t) ? string.Format(t, args) : throw new Exception("no text " + key);

    private static void TestExpressions()
    {
        Value Compile(string s)
        {
            Value v = Expr.Compile(s, out string error);
            Check(error == null, "expression '" + s + "': " + error);
            return v;
        }
        Check(Compile("2 + 3 * 4")(null) == 14, "precedence");
        Check(Compile("(2 + 3) * 4")(null) == 20, "parentheses");
        Check(Compile("3 <= 4 && !(1 > 2)")(null) == 1, "logic");
        Check(Compile("1 < 2 ? 7 : 9")(null) == 7, "ternary");
        Check(Compile("clamp(12, 0, 10) + min(3, 2) + max(1, 4)")(null) == 16, "functions");
        Expr.Compile("nonsense", out string bad);
        Check(bad != null, "an unknown path should be an error");
        Expr.Compile("2 +", out string partial);
        Check(partial != null, "a cut-off expression should be an error");
    }

    private static List<string> ProblemsIn(string json)
    {
        var content = new Content();
        var loader = new PackLoader(content, new Registry());
        loader.Load("t", "x.json", f => json);
        loader.Finish();
        return content.Problems;
    }

    private static void TestBadContentIsReported()
    {
        List<string> p = ProblemsIn("{\"rooms\":{\"a\":{\"text\":\"k\",\"enter\":[{\"nope\":1}]}}}");
        Check(p.Count == 1 && p[0].Contains("rooms.a.enter[0]") && p[0].Contains("nope"), "unknown effect should name its place: " + string.Join("; ", p));
        p = ProblemsIn("{\"rooms\":{\"a\":{\"text\":\"k\",\"enter\":[{\"fight\":\"ghost\"}]}}}");
        Check(p.Count == 1 && p[0].Contains("t:ghost"), "dangling reference: " + string.Join("; ", p));
        p = ProblemsIn("{\"enemies\":{\"a\":{\"name\":\"k\",\"stats\":{\"hp\":\"self.\"},\"behavior\":[]}}}");
        Check(p.Count >= 2, "bad path and empty behavior: " + string.Join("; ", p));
        p = ProblemsIn("{\"wat\":{}}");
        Check(p.Count == 1 && p[0].Contains("wat"), "unknown section");
        p = ProblemsIn("{ broken");
        Check(p.Count == 1 && p[0].Contains("line 1"), "broken JSON should give a line: " + string.Join("; ", p));
        p = ProblemsIn("{\"rooms\":{\"a\":{\"text\":\"k\",\"on\":{\"sometime\":[]}}}}");
        Check(p.Count == 1 && p[0].Contains("sometime"), "unknown hook");
    }

    // Data for another mod loads only when that mod is there, and mod(id) reads the same answer.
    private static void TestOptionalData()
    {
        Content Base(Func<string, bool> hasMod)
        {
            var content = new Content();
            var loader = new PackLoader(content, new Registry()) { HasMod = hasMod };
            loader.Load(Framework, "ExpeditionFramework/main.json", Reader(root));
            loader.Finish();
            Check(content.Problems.Count == 0, "base pack problems: " + string.Join("; ", content.Problems));
            return content;
        }
        Content without = Base(id => false);
        Check(!without.ItemStats.ContainsKey("gsx_carbine"), "gunsmith.json should be skipped without Gunsmith");
        Content with = Base(id => id == "gunsmith_expansion");
        Check(with.ItemStats.TryGetValue("gsx_carbine", out ItemStats carbine) && carbine.Props["capacity"] == 10, "Gunsmith carbine capacity");
        Check(with.ItemStats.TryGetValue("gsx_riot_gun", out ItemStats riot) && riot.Tags.Contains("non_lethal"), "riot gun is non-lethal");

        Value v = Expr.Compile("mod(other) * 2 + mod(absent)", out string error, null, id => id == "other");
        Check(error == null && v(null) == 2, "mod() in expressions: " + error);
        Expr.Compile("mod()", out string empty);
        Check(empty != null, "mod() needs an id");

        var problems = new Content();
        var bad = new PackLoader(problems, new Registry());
        bad.Load("t", "x.json", f => f == "x.json" ? "{\"include\":[3,{\"requires\":[\"a\"]},{\"file\":\"y.json\",\"when\":1}]}" : "{}");
        bad.Finish();
        Check(problems.Problems.Count == 3, "bad includes should each be reported: " + string.Join("; ", problems.Problems));
    }

    // A pack changes another pack's definitions before they compile, whatever order the packs come in.
    private static void TestPatches()
    {
        const string A = @"{
            ""loot"": { ""t"": { ""items"": [ ""x"", { ""item"": ""y"", ""weight"": 2 } ] }, ""u"": { ""items"": [ ""w"" ] } },
            ""values"": { ""v"": ""1"" }
        }";
        Content Compile(out Dictionary<string, List<string>> problems, params (string Pack, string Json)[] packs)
        {
            var set = new PackSet(new Registry());
            foreach ((string pack, string json) in packs) set.Add(pack, "ExpeditionFramework/main.json", f => json);
            return set.Compile(out problems);
        }
        List<string> Items(Content c, string table) => c.Loot[table].Entries.Select(e => e.Item).ToList();

        // The patching pack comes first, so this also checks that order doesn't matter.
        Content patched = Compile(out var p1, ("b", @"{ ""patch"": [
            { ""target"": ""a:loot.t"", ""remove"": { ""items"": [""y""] }, ""add"": { ""items"": [ { ""item"": ""z"", ""weight"": 3 } ] } },
            { ""target"": ""a:loot.*"", ""add"": { ""items"": [""all""] } },
            { ""target"": ""a:values.v"", ""replace"": ""5"" },
            { ""target"": ""absent:loot.t"", ""add"": { ""items"": [""q""] } }
        ] }"), ("a", A));
        Check(p1.Values.All(l => l.Count == 0), "clean patches: " + string.Join("; ", p1.Values.SelectMany(l => l)));
        Check(string.Join(",", Items(patched, "a:t")) == "x,z,all", "patched table: " + string.Join(",", Items(patched, "a:t")));
        Check(string.Join(",", Items(patched, "a:u")) == "w,all", "wildcard patch: " + string.Join(",", Items(patched, "a:u")));
        Check(patched.Values["a:v"](null) == 5, "replaced value");

        // A patch that breaks its target is dropped; the target loads as written and only the patcher is reported.
        Content guarded = Compile(out var p2, ("a", A), ("c", @"{ ""patch"": [ { ""target"": ""a:loot.t"", ""add"": { ""items"": [ { ""weight"": 1 } ] } } ] }"));
        Check(!p2.TryGetValue("a", out var forA) || forA.Count == 0, "the patched pack should stay clean");
        Check(p2.TryGetValue("c", out var forC) && forC.Any(m => m.Contains("left out")), "the bad patcher is reported: " + string.Join("; ", p2.Values.SelectMany(l => l)));
        Check(string.Join(",", Items(guarded, "a:t")) == "x,y", "a dropped patch changes nothing");

        // Mistakes in the patch itself belong to the patcher.
        Compile(out var p3, ("a", A), ("d", @"{ ""patch"": [ { ""target"": ""a:loot.missing"", ""add"": { ""items"": [""q""] } }, { ""target"": ""a:nonsense.t"" }, { ""target"": ""a"" } ] }"));
        Check(p3.TryGetValue("d", out var forD) && forD.Count(m => m.TrimStart().StartsWith("d/")) == 3, "bad targets: " + string.Join("; ", p3.Values.SelectMany(l => l)));
    }

    // A mod's C# effect is used from data exactly like a built-in one.
    private static void TestModEffectIsAPeer()
    {
        var content = new Content();
        var registry = new Registry();
        registry.For("t").AddEffect("double_it", spec =>
        {
            string name = spec.Text("var", "x");
            return s => s.Run.Set(name, s.Run.Get(name) * 2);
        });
        var loader = new PackLoader(content, registry);
        loader.Load("t", "x.json", f => @"{
          ""rooms"": { ""r"": { ""text"": ""k"", ""enter"": [
            { ""set"": { ""var"": ""run.x"", ""value"": 21 } }, { ""double_it"": { ""var"": ""x"" } }, { ""t:double_it"": { ""var"": ""x"" } }
          ] } },
          ""areas"": { ""a"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ] } }
        }");
        loader.Finish();
        Check(content.Problems.Count == 0, "mod effect: " + string.Join("; ", content.Problems));
        if (content.Problems.Count > 0) return;
        var run = new ExpeditionRun(content, content.Areas["t:a"], new FakeBridge(new Random(1)), n => 1, (p, k, a) => k, "you");
        run.Begin();
        Check(run.Get("x") == 84, "the mod effect should have run twice, x=" + run.Get("x"));
    }

    private static void TestWeakPointOptions(Content content, Dictionary<(string, string), string> text)
    {
        var bridge = new FakeBridge(new Random(3), withEverything: true);
        var run = new ExpeditionRun(content, content.Areas[Sample + ":flooded_cellar"], bridge, n => 1, Loc(text), "you");
        run.Begin();
        var scope = FightForeman(content, run);
        List<RunOption> options = run.Options();
        string[] ids = options.Select(o => o.Interaction.Id).ToArray();
        Check(ids.Count(i => i == "blind_foreman") == 2, "both bright items should offer the dazzle, got " + ids.Count(i => i == "blind_foreman"));
        Check(ids.Contains("blast_foreman"), "explosive should be offered");
        Check(!ids.Contains("interrupt_foreman") || run.Foes.Any(e => e.Intent.Interruptible), "interrupt only during a wind-up");
        bridge.Remove("c4");
        bridge.Remove("c4_set");
        Check(!run.Options().Any(o => o.Interaction.Id == "blast_foreman"), "no explosive means no blast option");
        Entity foreman = run.Foes.First(e => e.Id.EndsWith("foreman"));
        foreman.Intent = content.Intents[Framework + ":wind_up"];
        RunOption interrupt = run.Options().FirstOrDefault(o => o.Interaction.Id == "interrupt_foreman");
        Check(interrupt != null, "interrupt should appear during a wind-up");
        if (interrupt != null)
        {
            run.Perform(interrupt);
            Check(foreman.Intent != content.Intents[Framework + ":heavy_strike"], "interrupting should stop the heavy strike");
        }
    }

    private static object FightForeman(Content content, ExpeditionRun run)
    {
        var loader = new PackLoader(content, new Registry());
        Effect start = loader.Effect(new Dictionary<string, object> { { "fight", "foreman" } }, Sample, "test");
        loader.Finish();
        start(RootScope(run));
        return null;
    }

    private static IScope RootScope(ExpeditionRun run) => new TestScope(run);

    // Everything here goes through the public API, the way a C# mod would.
    private static void TestPublicApi()
    {
        var content = new Content();
        var registry = new Registry();
        IExpeditionRegistrar mod = registry.For("t");
        int damagedHeard = 0, overloadHeard = 0;
        mod.AddHook("overload");
        mod.AddHook("loop");
        mod.On("damaged", s => damagedHeard++);
        mod.On("overload", s => overloadHeard++);
        mod.On("loop", s => s.Run.Emit("t:loop", s.Self, s.Target));
        mod.On("knocked_out", s => throw new Exception("boom"));
        mod.Intercept("damage", d => d.Value *= 2);
        mod.Intercept("damage", d => d.Value += 1, order: -1);
        mod.Intercept("status", d => { if (d.Text == "t:poison") d.Cancelled = true; else d.Set("turns", 9); });
        mod.Intercept("heal", d => d.Value = 1);
        mod.Intercept("check", d => d.Value = 20);
        mod.Intercept("chance", d => d.Value = 0);
        mod.Intercept("loot", d => d.Text = "swapped");
        mod.Intercept("intent", d => d.Text = "t:rage");
        mod.AddEffect("tour", spec =>
        {
            IPool pool = spec.Pool("enemy");
            IReadOnlyList<ISpec> entries = spec.Entries("list");
            IItemUse use = spec.Use();
            Func<IScope, IReadOnlyList<string>> enemies = spec.Enemies();
            string status = spec.Ref("burn", "status");
            return s =>
            {
                s.Run.Set("pool_pick", pool.Pick(s) == "t:rat" ? 1 : 0);
                s.Run.Set("entries", entries.Sum(e => e.Number("n", 0)));
                s.Run.Set("melee", s.Run.Inventory.Select(use, s, null).Count);
                s.Run.Set("enemy_ids", enemies(s).Count);
                s.Run.SetText("status", status);
            };
        });
        Check(Throws(() => mod.On("sometime", s => { })), "an unknown hook should be refused");
        Check(Throws(() => mod.Intercept("nope", d => { })), "an unknown decision should be refused");

        var loader = new PackLoader(content, registry);
        loader.Load("t", "x.json", f => @"{
          ""statuses"": { ""burn"": { ""name"": ""k"" }, ""poison"": { ""name"": ""k"" } },
          ""intents"": { ""idle"": { ""label"": ""k"", ""do"": [] }, ""rage"": { ""label"": ""k"", ""do"": [] } },
          ""enemies"": { ""rat"": { ""name"": ""k"", ""stats"": { ""hp"": 20, ""armor"": 2 }, ""behavior"": [ { ""intent"": ""idle"" } ] } },
          ""loot"": { ""box"": { ""items"": [ ""plain"" ] } },
          ""events"": { ""ev"": { ""text"": ""k"", ""enter"": [ { ""set"": { ""var"": ""run.ev"", ""value"": 1 } } ] } },
          ""rooms"": { ""r"": { ""text"": ""k"",
            ""on"": { ""overload"": [ { ""set"": { ""var"": ""run.data_heard"", ""value"": 1 } } ] },
            ""enter"": [
              { ""tour"": { ""from"": [ ""rat"" ], ""select"": ""#melee"", ""list"": [ { ""n"": 1 }, { ""n"": 2 } ] } },
              { ""emit"": ""overload"" },
              { ""check"": { ""chance"": 100, ""success"": [ { ""set"": { ""var"": ""run.passed"", ""value"": 1 } } ], ""failure"": [ { ""set"": { ""var"": ""run.failed"", ""value"": 1 } } ] } }
            ] } },
          ""areas"": { ""a"": { ""title"": ""k"", ""hidden_when"": { ""carrying"": ""#melee"" }, ""player"": { ""hp"": 30 }, ""layout"": [ { ""room"": ""r"" } ] } }
        }");
        loader.Finish();
        Check(content.Problems.Count == 0, "public api: " + string.Join("; ", content.Problems));
        if (content.Problems.Count > 0) return;

        var run = new ExpeditionRun(content, content.Areas["t:a"], new FakeBridge(new Random(1)), n => 1, (p, k, a) => k, "you");
        run.Begin();
        IRun api = run;
        IScope scope = run.RootScope;

        Check(api.Get("pool_pick") == 1 && api.Get("entries") == 3 && api.Get("melee") == 1 && api.Get("enemy_ids") == 1 && api.GetText("status") == "t:burn", "spec helpers");
        Check(api.Get("data_heard") == 1 && overloadHeard == 1, "a declared hook reaches data and listeners");
        Check(api.Get("failed") == 1 && api.Get("passed") == 0, "the chance decision");

        IEntity rat = api.Combat.Make("t:rat");
        api.Combat.Muster(new[] { rat }, false);
        api.Combat.Engage(null, scope, false, null);
        Check(api.Scene == "combat" && api.Focus == rat && rat.IntentId == "t:rage", "engage and the intent decision");
        Check(api.Damage(rat, 3, api.Player, false) == 6 && rat.Get("hp") == 14 && damagedHeard == 1, "damage decisions run in order");
        api.Combat.AddStatus(rat, "t:burn");
        api.Combat.AddStatus(rat, "t:poison");
        Check(rat.HasStatus("t:burn") && !rat.HasStatus("t:poison") && ((Entity)rat).Statuses[0].Turns == 9, "the status decision");
        api.Combat.ClearStatus(rat, "t:burn");
        Check(!rat.HasStatus("t:burn"), "clearing a status");
        rat.Set("hp", 5);
        Check(api.Combat.Heal(rat, 10) == 1 && api.Get("healed") == 1, "the heal decision");
        Check(api.RollCheck(20) == 20, "the check decision");
        api.Combat.EndFight(true);
        Check(api.Scene == "room" && api.Enemies.Count == 0, "ending a fight");

        Check(api.Inventory.RollLoot("t:box") == "swapped", "the loot decision");
        Check(api.Inventory.Give("thing") == "thing" && api.Get("given") == 1, "giving an item");
        Check(api.Inventory.Items.Count == 1 && (string)api.Inventory.Items[0].Handle == "kitchen_knife", "items and their game handles");
        Check(api.Inventory.Credits == 50 && api.Inventory.SpendCredits(20) && api.Inventory.Credits == 30, "credits");

        api.Flow.Schedule(3, "t:ev");
        Check(api.Flow.ScheduledAt(3) == "t:ev", "scheduling");
        api.Flow.Unschedule(3);
        Check(api.Flow.ScheduledAt(3) == null, "unscheduling");
        api.Flow.StartEvent("t:ev", scope);
        Check(run.CurrentEvent != null, "starting an event by id");
        api.Flow.EndEvent();
        api.Roster("crew").Add(rat);
        Check(api.Roster("crew").Count == 1, "rosters");

        int lines = run.Log_.Count;
        api.Flow.StartEvent("t:nope", scope);
        Check(run.Log_.Count == lines + 1 && run.Log_[lines].Tone == "bad", "an unknown id is logged, not thrown");

        api.Emit("t:loop", api.Player, null);
        Check(run.Log_.Any(l => l.Text.Contains("too deeply")), "a listener that triggers itself should be stopped");

        api.Damage(api.Player, 999, null, true);
        Check(run.Outcome == "knocked_out", "a run should survive a listener that throws");
        Check(run.Log_.Any(l => l.Text.Contains("boom")), "a listener's failure should be logged");

        var map = new MapContext("t:a", (p, k, a) => k, name => 0);
        Check(map.Root.Run.Inventory.Items.Count == 0 && map.Root.Run.Flow.ScheduledAt(1) == null && map.Root.Run.Combat.Make("t:rat") == null, "the map has an empty world");
        Check(!content.Areas["t:a"].HiddenWhen(map.Root), "conditions can ask about items on the map");
    }

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private sealed class TestScope : IScope
    {
        public IRun Run { get; }
        public IEntity Self => Run.Player;
        public IEntity Target => null;
        public IItem Item => null;
        public IItem Paired => null;
        public int Arg(string name) => 0;
        public TestScope(IRun run) { Run = run; }
        public IScope With(IEntity self, IEntity target, IItem item) => this;
        public IScope WithPaired(IItem paired) => this;
        public IScope WithArg(string name, int value) => this;
    }

    private static void Play(Content content, Dictionary<(string, string), string> text, int runs)
    {
        var seen = new HashSet<string>();
        var outcomes = new Dictionary<string, int>();
        var random = new Random(42);
        int longest = 0;
        for (int n = 0; n < runs; n++)
        {
            var bridge = new FakeBridge(random, withEverything: n % 2 == 0);
            var run = new ExpeditionRun(content, content.Areas[Sample + ":flooded_cellar"], bridge, s => random.Next(1, s + 1), Loc(text), "you");
            run.Begin();
            int steps = 0;
            while (!run.Over)
            {
                if (++steps > 2000) { Fail("a run never ended; scene " + run.Scene); break; }
                List<RunOption> options = run.Options();
                if (options.Count == 0) { Fail("no options in scene " + run.Scene + " at room " + run.Depth); break; }
                // Fight when in a fight, and don't walk out most of the time, so runs reach the end.
                List<RunOption> pool = options.Where(o => o.Interaction.Id != "head_home" || random.Next(100) < 3).ToList();
                RunOption pick = (pool.Count > 0 ? pool : options)[random.Next((pool.Count > 0 ? pool : options).Count)];
                seen.Add(pick.Interaction.Id);
                run.Perform(pick);
            }
            longest = Math.Max(longest, steps);
            outcomes[run.Outcome ?? "unfinished"] = (outcomes.TryGetValue(run.Outcome ?? "unfinished", out int c) ? c : 0) + 1;
        }
        Console.WriteLine("Outcomes: " + string.Join(", ", outcomes.Select(o => o.Key + "=" + o.Value)) + "; longest run " + longest + " steps");
        string[] expected =
        {
            "press_on", "head_home", "shoot", "reload", "strike", "heal", "feed_rat", "blind_foreman", "blast_foreman",
            "interrupt_foreman", "give", "rob", "ignore", "pry", "search_pump", "claim",
        };
        foreach (string id in expected.Where(e => !seen.Contains(e))) Fail("never offered and picked: " + id);
    }

    private sealed class FakeBridge : IBridge
    {
        private readonly Random random;
        private readonly List<RunItem> items = new List<RunItem>();

        public FakeBridge(Random random, bool withEverything = false)
        {
            this.random = random;
            Add("kitchen_knife", "#melee #in_hand", ("bonus", 1), ("sides", 4));
            if (!withEverything) return;
            Add("pistol", "#firearm #in_hand", ("capacity", 6), ("firepower", 4), ("accuracy", 2));
            Add("rounds", "#ammo");
            Add("bandage_item", "#medicine", ("heal_count", 1), ("heal_sides", 3), ("heal_flat", 2), ("stops_bleeding", 1));
            Add("flashlight", "#bright");
            Add("flashbang_grenade", "#bright");
            Add("c4", "#explosive");
            Add("c4_set", "#explosive");
            Add("crowbar", "");
            Add("cup_noodle", "");
        }

        private void Add(string id, string tags, params (string, int)[] props)
        {
            var item = new RunItem { Handle = id, Id = id, Name = id, Count = 2 };
            foreach (string t in tags.Split(' ', StringSplitOptions.RemoveEmptyEntries)) item.Tags.Add(t.TrimStart('#'));
            foreach ((string k, int v) in props) item.Props[k] = v;
            items.Add(item);
        }

        public void Remove(string id) => items.RemoveAll(i => i.Id == id);
        public IReadOnlyList<RunItem> Items() => items;

        public void Consume(RunItem item, int count)
        {
            item.Count -= count;
            if (item.Count <= 0) items.Remove(item);
        }

        public string GiveItem(string itemId) => itemId;
        public int Credits { get; private set; } = 50;
        public void AddCredits(int amount) => Credits += amount;
        public bool SpendCredits(int amount) { if (Credits < amount) return false; Credits -= amount; return true; }
        public string RollTable(string table) => null;
        public bool Fits(RunItem gun, RunItem round) => true;
        public bool Equip(RunItem item) => false;
        public int GroundCount => 0;
        public int ClearGround() => 0;
        public string TakeBack() => null;
        public void LoseFound() { }
        public List<string> LoseCarried(int percent) => new List<string>();
        public int GameValue(string name) => 0;
        public void Wound(string kind) { }
        public void NightLog(string text, string color) { }
        public void PlaySound(string id) { }
        public void PlayItemSound(RunItem item, string fallback) { }
        public bool OpenTrade(TradeRequest request) => false;
        public TradeResult CloseTrade() => new TradeResult();
        public List<string> TakeCart(TradeRequest cart) => new List<string>();
    }
}
