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

    private static int Main(string[] args)
    {
        root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        if (args.Length > 0 && args[0] == "lint") return Lint.Run(root, args.Skip(1).ToArray());
        Dictionary<(string, string), string> text = LoadText();
        Content content = LoadAll(out Registry registry);
        foreach (string problem in content.Problems) Fail("content: " + problem);
        foreach (string warning in content.Warnings) Fail("warning: " + warning);
        foreach ((string pack, string key) in content.TextKeys.Where(k => !text.ContainsKey(k))) Fail("missing text " + pack + ":" + key);

        TestExpressions();
        TestBadContentIsReported();
        TestOptionalData();
        TestPatches();
        TestWarningsDontVoid();
        TestShapes();
        TestUnreadArgs();
        TestNotCarryingObjectForm();
        TestItemTags();
        TestLocaleParity();
        TestModEffectIsAPeer();
        TestPublicApi();
        TestSharedEffectSurvivesThrow();
        TestTextParams();
        TestEngineVarsAreReadOnly();
        TestSpawnOutsideFight();
        TestRenderingDoesNotRoll();
        TestActionInfo();
        TestReloadUsesWholeItems();
        TestMemory();
        TestMapMemory();
        TestGameReads();
        TestReputation();
        TestMemoryWarnings();
        TestPackMemory();
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

    private static List<string> WarningsIn(string json) => Compiled(json).Warnings;

    private static Content Compiled(string json, Registry registry = null)
    {
        var content = new Content();
        var loader = new PackLoader(content, registry ?? new Registry());
        loader.Load("t", "x.json", f => json);
        loader.Finish();
        return content;
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

    // A warning is reported under its pack but neither voids that pack nor costs a patch its place.
    private static void TestWarningsDontVoid()
    {
        var registry = new Registry();
        registry.For("b").AddEffect("warn_me", spec =>
        {
            spec.Warn("x");
            return s => { };
        });
        var set = new PackSet(registry);
        set.Add("a", "ExpeditionFramework/main.json", f => @"{
          ""rooms"": { ""r"": { ""text"": ""k"" } },
          ""areas"": { ""a"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ] } }
        }");
        set.Add("b", "ExpeditionFramework/main.json", f => @"{ ""patch"": [
          { ""target"": ""a:rooms.r"", ""set"": { ""enter"": [ { ""b:warn_me"": true } ] } }
        ] }");
        Content content = set.Compile(out Dictionary<string, List<string>> problems, out Dictionary<string, List<string>> warnings);
        Check(problems.Values.All(l => l.Count == 0), "a warning is not a problem: " + string.Join("; ", problems.Values.SelectMany(l => l)));
        Check(warnings.TryGetValue("a", out List<string> forA) && forA.Count == 1 && forA[0].Contains("x"), "the warning is reported under the patched pack: " + string.Join("; ", warnings.Values.SelectMany(l => l)));
        Check(content.Warnings.Count == 1, "content keeps the warning: " + string.Join("; ", content.Warnings));
        Check(content.Rooms.TryGetValue("a:r", out RoomDef room) && room.Enter != null, "the patch that warned is kept");
        Check(content.Areas.ContainsKey("a:a"), "the pack that warned keeps its area");
    }

    // Keys outside a definition's shape are warned about, wrong-typed values are problems, and unknown tones are warned about.
    private static void TestShapes()
    {
        string Joined(List<string> list) => string.Join("; ", list);
        const string Room = "\"rooms\":{\"r\":{\"text\":\"k\"}}";
        const string Area = "\"title\":\"k\",\"player\":{\"hp\":1},\"layout\":[{\"room\":\"r\"}]";

        string typo = "{\"rooms\":{\"a\":{\"text\":\"k\",\"txet\":\"k2\"}}}";
        List<string> w = WarningsIn(typo);
        Check(w.Count == 1 && w[0].Contains("rooms.a.txet") && w[0].Contains("known:"), "an unknown key is warned about: " + Joined(w));
        Check(ProblemsIn(typo).Count == 0, "an unknown key is not a problem: " + Joined(ProblemsIn(typo)));

        List<string> p = ProblemsIn("{" + Room + ",\"areas\":{\"a\":{" + Area + ",\"unlocked_by\":[\"b\"]},\"b\":{" + Area + "}}}");
        Check(p.Count == 1 && p[0].Contains("unlocked_by") && p[0].Contains("must be a string"), "unlocked_by as a list is a problem: " + Joined(p));

        const string Enemy = "\"intents\":{\"i\":{\"label\":\"k\",\"do\":[]}},\"enemies\":{\"x\":{\"name\":\"k\",\"stats\":{\"hp\":1},\"behavior\":[{\"intent\":\"i\"}]}}";
        p = ProblemsIn("{" + Enemy + ",\"encounters\":{\"e\":{\"weight\":\"1+1\",\"enemies\":[\"x\"]}}}");
        Check(p.Count == 1 && p[0].Contains("weight") && p[0].Contains("must be a number"), "a weight expression is a problem: " + Joined(p));

        p = ProblemsIn("{\"intents\":{\"i\":{\"label\":\"k\",\"do\":[],\"interruptible\":\"yes\"}}}");
        Check(p.Count == 1 && p[0].Contains("must be true or false"), "a flag as a string is a problem: " + Joined(p));

        w = WarningsIn("{\"statuses\":{\"s\":{\"name\":\"k\",\"weight\":2}}}");
        Check(w.Count == 1 && w[0].Contains("statuses.s.weight"), "weight on a status is warned about: " + Joined(w));

        w = WarningsIn("{\"intents\":{\"i\":{\"label\":\"k\",\"do\":[],\"tone\":\"angry\"}}}");
        Check(w.Count == 1 && w[0].Contains("unknown tone"), "an unknown intent tone is warned about: " + Joined(w));
        w = WarningsIn("{\"rooms\":{\"r\":{\"text\":{\"text\":\"k\",\"tone\":\"loud\"}}}}");
        Check(w.Count == 1 && w[0].Contains("unknown tone"), "an unknown line tone is warned about: " + Joined(w));
        string Intel(string tone) =>
            "{" + Room + ",\"areas\":{\"a\":{" + Area + ",\"pools\":{\"p\":[\"x\"]},\"intel\":[{\"id\":\"i\",\"pool\":\"p\",\"title\":\"k\",\"description\":\"k\",\"tone\":\"" + tone + "\"}]}}}";
        w = WarningsIn(Intel("good"));
        Check(w.Count == 0 && ProblemsIn(Intel("good")).Count == 0, "a known intel tone is fine: " + Joined(w) + Joined(ProblemsIn(Intel("good"))));
        w = WarningsIn(Intel("great"));
        Check(w.Count == 1 && w[0].Contains("unknown tone") && w[0].Contains("good, bad, mixed"), "an unknown intel tone is warned about: " + Joined(w));

        w = WarningsIn("{\"rooms\":{\"r\":{\"text\":\"k\",\"interactions\":[{\"id\":\"x\",\"label\":\"k\",\"do\":[],\"gruop\":\"g\"}]}}}");
        Check(w.Count == 1 && w[0].Contains("interactions[0].gruop"), "a misspelled interaction key is warned about: " + Joined(w));
    }

    // An argument no compiler asks for is warned about, for built-ins and C# effects alike.
    private static void TestUnreadArgs()
    {
        string Joined(List<string> list) => string.Join("; ", list);
        string Enter(string effects) => "{\"rooms\":{\"r\":{\"text\":\"k\",\"enter\":[" + effects + "]}}}";

        List<string> w = WarningsIn(Enter("{\"set\":{\"var\":\"run.x\",\"valu\":1}}"));
        Check(w.Count == 1 && w[0].Contains("enter[0].set.valu"), "a misspelled built-in argument is warned about: " + Joined(w));

        var registry = new Registry();
        registry.For("t").AddEffect("drain", spec =>
        {
            Value amount = spec.Value("amount", 0);
            return s => s.Run.Set("drained", amount(s));
        });
        registry.For("t").AddEffect("ping", spec => s => { });
        w = Compiled(Enter("{\"t:drain\":{\"amount\":1,\"amout\":2}}"), registry).Warnings;
        Check(w.Count == 1 && w[0].Contains("amout"), "a misspelled C# effect argument is warned about: " + Joined(w));
        w = Compiled(Enter("{\"t:ping\":3}"), registry).Warnings;
        Check(w.Count == 1 && w[0].Contains("t:ping.value"), "a scalar nothing reads is warned about: " + Joined(w));
        w = Compiled(Enter("{\"t:ping\":true}"), registry).Warnings;
        Check(w.Count == 0, "a C# effect given true has no arguments: " + Joined(w));

        // The framework's own {"x": true} idiom for effects and conditions that take nothing.
        Content bare = Compiled(@"{
          ""intents"": { ""i"": { ""label"": ""k"", ""do"": [ { ""if"": { ""when"": { ""intent_interruptible"": true }, ""then"": [ { ""end_fight"": true } ] } } ] } },
          ""rooms"": { ""r"": { ""text"": ""k"", ""enter"": [ { ""next_room"": true } ], ""interactions"": [
            { ""id"": ""x"", ""label"": ""k"", ""where"": { ""fits"": true }, ""when"": { ""intent_interruptible"": true }, ""do"": [ { ""reload"": true } ] }
          ] } }
        }");
        Check(bare.Warnings.Count == 0 && bare.Problems.Count == 0, "a bare true means no arguments: " + Joined(bare.Warnings) + Joined(bare.Problems));

        Content bogus = Compiled(Enter("{\"set\":{\"var\":\"bogus\",\"value\":1}}"));
        Check(bogus.Problems.Count == 1 && bogus.Warnings.Count == 0, "a rejected argument is one problem and no warnings: " + Joined(bogus.Problems) + Joined(bogus.Warnings));
        Content engine = Compiled(Enter("{\"set\":{\"var\":\"run.turn\",\"value\":1}}"));
        Check(engine.Problems.Count == 1 && engine.Warnings.Count == 0, "setting an engine var is one problem and no warnings: " + Joined(engine.Problems) + Joined(engine.Warnings));

        Content shared = Compiled("{\"effects\":{\"e\":{\"params\":{\"n\":1},\"do\":[]}},\"rooms\":{\"r\":{\"text\":\"k\",\"enter\":[{\"e\":{\"n\":2,\"m\":3}}]}}}");
        Check(shared.Problems.Count == 1 && shared.Warnings.Count == 0, "an unknown shared-effect parameter is reported once: " + Joined(shared.Problems) + Joined(shared.Warnings));

        w = WarningsIn(Enter("{\"say\":{\"text\":\"k\",\"tone\":\"loud\"}}"));
        Check(w.Count == 1 && w[0].Contains("unknown tone"), "an unknown say tone is warned about: " + Joined(w));
    }

    // not and carrying take their condition or filter as written, or under "value"; with nothing at all it's a problem.
    private static void TestNotCarryingObjectForm()
    {
        string Joined(List<string> list) => string.Join("; ", list);
        string Area(string hiddenWhen) =>
            "{\"rooms\":{\"r\":{\"text\":\"k\"}},\"areas\":{\"a\":{\"title\":\"k\",\"player\":{\"hp\":1},\"layout\":[{\"room\":\"r\"}],\"hidden_when\":" + hiddenWhen + "}}}";
        var map = new MapContext("t:a", (p, k, a) => k, name => 0);
        foreach (string form in new[] { "{\"not\":{\"scene\":\"combat\"}}", "{\"not\":{\"value\":{\"scene\":\"combat\"}}}" })
        {
            Content c = Compiled(Area(form));
            Check(c.Problems.Count == 0 && c.Warnings.Count == 0, form + " compiles cleanly: " + Joined(c.Problems) + Joined(c.Warnings));
            if (c.Problems.Count == 0) Check(c.Areas["t:a"].HiddenWhen(map.Root), form + " holds on the map");
        }
        foreach (string empty in new[] { "{\"not\":{}}", "{\"not\":null}" })
        {
            Content c = Compiled(Area(empty));
            Check(c.Problems.Count == 1 && c.Problems[0].Contains("needs a condition") && c.Warnings.Count == 0, empty + " is one problem: " + Joined(c.Problems) + Joined(c.Warnings));
        }

        Content carrying = Compiled(Area("{\"carrying\":{\"all\":[\"#melee\",\"#in_hand\"]}}"));
        Check(carrying.Problems.Count == 0 && carrying.Warnings.Count == 0, "carrying an object filter compiles cleanly: " + Joined(carrying.Problems) + Joined(carrying.Warnings));
        if (carrying.Problems.Count == 0)
        {
            var bridge = new FakeBridge(new Random(1), withEverything: true);
            var run = new ExpeditionRun(carrying, carrying.Areas["t:a"], bridge, n => 1, (p, k, a) => k, "you");
            Condition hidden = carrying.Areas["t:a"].HiddenWhen;
            Check(hidden(run.RootScope), "carrying {all: [#melee, #in_hand]} holds with a knife in hand");
            bridge.Remove("kitchen_knife");
            Check(!hidden(run.RootScope), "carrying {all: [#melee, #in_hand]} fails with only a gun in hand");
        }
        Content none = Compiled(Area("{\"carrying\":null}"));
        Check(none.Problems.Count == 1 && none.Problems[0].Contains("needs an item filter"), "carrying null is a problem: " + Joined(none.Problems));
    }

    // A #tag filter naming a tag no game tag, item group or item_stats tag defines is warned about.
    private static void TestItemTags()
    {
        string Uses(string tag, string extra = "") =>
            "{" + extra + "\"rooms\":{\"r\":{\"text\":\"k\",\"interactions\":[{\"id\":\"x\",\"label\":\"k\",\"uses\":\"#" + tag + "\",\"do\":[]}]}}}";
        List<string> w = WarningsIn(Uses("explosve"));
        Check(w.Count == 1 && w[0].Contains("explosve"), "an unknown item tag is warned about: " + string.Join("; ", w));
        w = WarningsIn(Uses("melee"));
        Check(w.Count == 0, "a game tag is known: " + string.Join("; ", w));
        w = WarningsIn(Uses("bright", "\"item_groups\":{\"bright\":[\"torch\"]},"));
        Check(w.Count == 0, "an item group is a known tag: " + string.Join("; ", w));
        w = WarningsIn(Uses("noisy", "\"item_stats\":{\"#loud\":{\"tags\":[\"noisy\"]}},"));
        Check(w.Count == 0, "an item_stats tag is known: " + string.Join("; ", w));
    }

    // Another locale is compared with English key by key, and by the {n} placeholders each text fills in.
    private static void TestLocaleParity()
    {
        var en = new Dictionary<string, string> { ["a"] = "x {0}", ["b"] = "y" };
        List<string> p = Lint.ParityProblems(en, new Dictionary<string, string> { ["a"] = "x", ["c"] = "z" }, "zh.csv");
        Check(p.Count == 3 && p.Any(m => m.Contains("a has placeholders")) && p.Any(m => m.Contains("b is missing")) && p.Any(m => m.Contains("c is not in")),
            "locale parity: " + string.Join("; ", p));
        p = Lint.ParityProblems(en, new Dictionary<string, string>(en), "zh.csv");
        Check(p.Count == 0, "identical locales: " + string.Join("; ", p));
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

    // A shared effect whose body throws still gives back its recursion level, so later shared effects keep working.
    private static void TestSharedEffectSurvivesThrow()
    {
        var content = new Content();
        var registry = new Registry();
        registry.For("t").AddEffect("boom", spec => s => throw new InvalidOperationException("boom"));
        const string Json = @"{
          ""effects"": { ""risky"": [ { ""t:boom"": {} } ], ""fine"": [ { ""set"": { ""var"": ""run.ok"", ""value"": 1 } } ] },
          ""rooms"": { ""r"": { ""text"": ""k"" } },
          ""areas"": { ""a"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ] } }
        }";
        var loader = new PackLoader(content, registry);
        loader.Load("t", "x.json", f => Json);
        loader.Finish();
        Check(content.Problems.Count == 0, "shared effect throw: " + string.Join("; ", content.Problems));
        if (content.Problems.Count > 0) return;
        var run = new ExpeditionRun(content, content.Areas["t:a"], new FakeBridge(new Random(1)), n => 1, (p, k, a) => k, "you");
        run.Begin();
        var calls = new PackLoader(content, registry);
        Effect risky = calls.Effect(new Dictionary<string, object> { { "t:risky", new Dictionary<string, object>() } }, "t", "test");
        Effect fine = calls.Effect(new Dictionary<string, object> { { "t:fine", new Dictionary<string, object>() } }, "t", "test");
        calls.Finish();
        Check(content.Problems.Count == 0, "shared effect calls: " + string.Join("; ", content.Problems));
        for (int i = 0; i < 40; i++)
        {
            try { risky(run.RootScope); }
            catch (InvalidOperationException) { }
        }
        fine(run.RootScope);
        Check(run.Get("ok") == 1 && !run.Log_.Any(l => l.Text.Contains("too deeply")), "a throwing shared effect should not use up the recursion limit");
    }

    // A shared effect's $ params are text: {arg.$NAME} in the body is filled in at load, per call site, so fields that are
    // literals (a faction, a var path, a text key) can differ between calls. Misuse is a problem at load, never a run-time surprise.
    private static void TestTextParams()
    {
        const string Book = @"""book"": { ""params"": { ""$f"": """", ""n"": 0 }, ""do"": [
              { ""reputation"": { ""faction"": ""{arg.$f}"", ""value"": ""arg.n"", ""quiet"": true } },
              { ""set"": { ""var"": ""run.{arg.$f}_n"", ""value"": ""arg.n"" } },
              { ""say"": ""k_{arg.$f}"" } ] }";
        string Pack(string effects, string enter) => @"{ ""effects"": { " + effects + @" },
          ""rooms"": { ""r"": { ""text"": ""k"", ""enter"": [ " + enter + @" ] } },
          ""areas"": { ""a"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ] } } }";
        (Content Content, FakeBridge Bridge, ExpeditionRun Run) Play(string json)
        {
            Content content = Compiled(json);
            if (content.Problems.Count > 0) { Check(false, "text params pack: " + string.Join("; ", content.Problems)); return (content, null, null); }
            var bridge = new FakeBridge(new Random(1));
            var run = new ExpeditionRun(content, content.Areas["t:a"], bridge, n => 1, (pk, k, a) => k, "you");
            run.Begin();
            return (content, bridge, run);
        }

        // Works: each call site gets its own filled body.
        var works = Play(Pack(Book, @"{ ""book"": { ""$f"": ""security"", ""n"": 2 } }, { ""book"": { ""$f"": ""upper_level"", ""n"": 3 } }"));
        if (works.Run != null)
        {
            Check(works.Bridge.Rep["security"] == 2 && works.Bridge.Rep["upper_level"] == 3, "each call fills its own faction");
            Check(works.Run.Get("security_n") == 2 && works.Run.Get("upper_level_n") == 3, "filled var paths: " + works.Run.Get("security_n") + "/" + works.Run.Get("upper_level_n"));
            Check(works.Content.TextKeys.Contains(("t", "k_security")) && works.Content.TextKeys.Contains(("t", "k_upper_level")), "filled text keys register for lint");
        }

        // Bare form fills the first param; a default fills what the call leaves out.
        var bare = Play(Pack(Book, @"{ ""book"": ""security"" }"));
        if (bare.Run != null) Check(!bare.Bridge.Rep.ContainsKey("security") && bare.Content.TextKeys.Contains(("t", "k_security")), "bare form fills the first text param");
        var bareOne = Play(Pack(@"""settle"": { ""params"": { ""$f"": """" }, ""do"": { ""reputation"": { ""faction"": ""{arg.$f}"", ""value"": 1, ""quiet"": true } } }", @"{ ""settle"": ""security"" }"));
        if (bareOne.Run != null) Check(bareOne.Bridge.Rep["security"] == 1, "a one-param bare form reaches the filled faction");
        var fallback = Play(Pack(Book.Replace(@"""$f"": """"", @"""$f"": ""security"""), @"{ ""book"": { ""n"": 4 } }"));
        if (fallback.Run != null) Check(fallback.Bridge.Rep["security"] == 4, "a default fills a missing text param");

        // Nested: a text param can be passed down as the caller's own {arg.$NAME}.
        var nested = Play(Pack(Book + @", ""outer"": { ""params"": { ""$g"": """" }, ""do"": { ""book"": { ""$f"": ""{arg.$g}"", ""n"": 1 } } }",
            @"{ ""outer"": { ""$g"": ""security"" } }"));
        if (nested.Run != null) Check(nested.Bridge.Rep["security"] == 1 && nested.Run.Get("security_n") == 1, "a text param passes down through a nested call");

        // Numeric-only effects behave as before.
        var numeric = Play(Pack(@"""raise"": { ""params"": { ""n"": 1 }, ""do"": { ""add"": { ""var"": ""run.heat"", ""value"": ""arg.n"" } } }",
            @"{ ""raise"": 2 }, { ""raise"": { ""n"": 3 } }"));
        if (numeric.Run != null) Check(numeric.Run.Get("heat") == 5, "numeric shared effects still run: " + numeric.Run.Get("heat"));

        List<string> Problems(string effects, string enter) => ProblemsIn(Pack(effects, enter));
        void One(List<string> p, string what, params string[] parts) =>
            Check(p.Count == 1 && parts.All(x => p[0].Contains(x)), what + ": " + string.Join("; ", p));

        List<string> p6 = Problems(Book, @"{ ""book"": { ""$f"": ""run.x"", ""n"": 1 } }");
        One(p6, "a value outside a-z, 0-9, _ is a problem at the call site", "rooms.r.enter[0].book", "$f");
        One(Problems(Book, @"{ ""book"": { ""$f"": 3 } }"), "a number is not text", "is text");
        One(Problems(Book, @"{ ""book"": { ""n"": 1 } }"), "a missing required text param", "book needs $f");
        One(Problems(@"""book"": { ""params"": { ""$f"": """" }, ""do"": { ""say"": ""k_{arg.$g}"" } }", ""), "an undeclared {arg.$g}", "effects.book.do", "no text parameter $g");
        One(Problems(@"""book"": { ""params"": { ""$f"": """" }, ""do"": { ""set"": { ""var"": ""run.x"", ""value"": ""arg.$f"" } } }", ""),
            "arg.$f outside braces", "{arg.$f}");
        One(Problems(@"""book"": { ""params"": { ""$F"": """" }, ""do"": [] }", ""), "a bad param name", "effects.book.params.$F");
        One(Problems(@"""book"": { ""params"": { ""$f"": 3 }, ""do"": [] }", ""), "a non-text default", "effects.book.params.$f");
        One(Problems(@"""book"": { ""params"": { ""$f"": ""Run.X"" }, ""do"": [] }", ""), "a default outside a-z, 0-9, _", "effects.book.params.$f");
        One(Problems(@"""book"": { ""params"": { ""$f"": """" }, ""do"": { ""set"": { ""var"": ""run.x"", ""value"": 1, ""{arg.$f}"": 1 } } }", ""),
            "a {arg.$f} in a key", "{arg.$f}");

        List<string> bad = Problems(Book, @"{ ""book"": { ""$f"": ""church"", ""n"": 1 } }");
        string site = "rooms.r.enter[0].book";
        Check(bad.Count >= 1 && bad.Any(x => x.IndexOf(site, StringComparison.Ordinal) >= 0 && x.IndexOf(".do[0].reputation", StringComparison.Ordinal) > x.IndexOf(site, StringComparison.Ordinal)),
            "a problem the filled body causes names the call site and the body place: " + string.Join("; ", bad));

        List<string> self = Problems(@"""a"": { ""params"": { ""$x"": """" }, ""do"": { ""a"": { ""$x"": ""{arg.$x}"" } } }", @"{ ""a"": { ""$x"": ""security"" } }");
        One(self, "an effect that calls itself through its text params", "calls itself");
        List<string> mutual = Problems(@"""a"": { ""params"": { ""$x"": """" }, ""do"": { ""b"": { ""$x"": ""{arg.$x}"" } } },
            ""b"": { ""params"": { ""$x"": """" }, ""do"": { ""a"": { ""$x"": ""{arg.$x}"" } } }", @"{ ""a"": { ""$x"": ""security"" } }");
        Check(mutual.Count >= 1 && mutual.Any(x => x.Contains("calls itself")), "mutual recursion ends in a problem: " + string.Join("; ", mutual));

        List<string> numericUse = Problems(@"""raise"": { ""params"": { ""n"": 1 }, ""do"": { ""say"": ""k_{arg.$x}"" } }", "");
        Check(numericUse.Count >= 1 && numericUse.Any(x => x.Contains("no text parameter $x")), "{arg.$x} in a numeric-only effect is a problem: " + string.Join("; ", numericUse));
        List<string> asExpr = Problems("", @"{ ""set"": { ""var"": ""run.x"", ""value"": ""arg.$f"" } }");
        Check(asExpr.Count == 1 && asExpr[0].Contains("arg.$NAME is text"), "arg.$f in an expression names the text-param rule: " + string.Join("; ", asExpr));
        One(Problems(@"""book"": { ""params"": { ""$f"": ""Security Guys"" }, ""do"": { ""reputation"": { ""faction"": ""{arg.$f}"", ""value"": 1 } } }", @"{ ""book"": { } }"),
            "an invalid default that is not passed is reported once, at its declaration", "effects.book.params.$f");

        // A text-param effect nobody calls is not checked, so it warns, once however often Finish runs.
        var uncalled = new Content();
        var loader = new PackLoader(uncalled, new Registry());
        loader.Load("t", "x.json", f => Pack(Book, ""));
        loader.Finish();
        loader.Finish();
        Check(uncalled.Problems.Count == 0 && uncalled.Warnings.Count(w => w.Contains("never called")) == 1, "an uncalled text-param effect warns once: " + string.Join("; ", uncalled.Warnings));
        Check(works.Content.Warnings.All(w => !w.Contains("never called")), "a called one does not warn");
    }

    // Names the engine answers itself can't be written from data, and the list matches what Get answers.
    private static void TestEngineVarsAreReadOnly()
    {
        foreach (string name in ExpeditionRun.EngineVars)
        {
            List<string> p = ProblemsIn("{\"rooms\":{\"a\":{\"text\":\"k\",\"enter\":[{\"set\":{\"var\":\"run." + name + "\",\"value\":1}}]}}}");
            Check(p.Count == 1 && p[0].Contains("run." + name), "set run." + name + " should be a problem: " + string.Join("; ", p));
        }
        List<string> added = ProblemsIn("{\"rooms\":{\"a\":{\"text\":\"k\",\"enter\":[{\"add\":{\"var\":\"run.turn\",\"value\":1}}]}}}");
        Check(added.Count == 1 && added[0].Contains("run.turn"), "add run.turn should be a problem: " + string.Join("; ", added));
        List<string> own = ProblemsIn("{\"rooms\":{\"a\":{\"text\":\"k\",\"enter\":[{\"set\":{\"var\":\"run.alarm\",\"value\":1}}]}}}");
        Check(own.Count == 0, "set run.alarm is fine: " + string.Join("; ", own));

        string Area(string vars) =>
            "{\"rooms\":{\"r\":{\"text\":\"k\"}},\"areas\":{\"a\":{\"title\":\"k\",\"player\":{\"hp\":1},\"vars\":" + vars + ",\"layout\":[{\"room\":\"r\"}]}}}";
        List<string> engine = ProblemsIn(Area("{\"turn\":3}"));
        Check(engine.Count == 1 && engine[0].Contains("vars.turn"), "area vars naming turn should be a problem: " + string.Join("; ", engine));
        List<string> plain = ProblemsIn(Area("{\"alarm\":7}"));
        Check(plain.Count == 0, "area vars naming alarm is fine: " + string.Join("; ", plain));

        var content = new Content();
        var loader = new PackLoader(content, new Registry());
        loader.Load("t", "x.json", f => Area("{\"alarm\":7}"));
        loader.Finish();
        if (content.Problems.Count > 0) return;
        var run = new ExpeditionRun(content, content.Areas["t:a"], new FakeBridge(new Random(1)), n => 1, (p, k, a) => k, "you");
        run.Begin();
        Check(run.Get("alarm") == 7, "area vars reach the run");
        foreach (string name in ExpeditionRun.EngineVars)
        {
            run.Set(name, 999);
            Check(run.Get(name) != 999, "Get should answer " + name + " from the engine");
        }
    }

    // Nobody can arrive outside a fight, so spawn takes its else branch and says so.
    private static void TestSpawnOutsideFight()
    {
        var content = new Content();
        var loader = new PackLoader(content, new Registry());
        loader.Load("t", "x.json", f => @"{
          ""intents"": { ""idle"": { ""label"": ""k"", ""do"": [] } },
          ""enemies"": { ""rat"": { ""name"": ""k"", ""stats"": { ""hp"": 5 }, ""behavior"": [ { ""intent"": ""idle"" } ] } },
          ""rooms"": { ""r"": { ""text"": ""k"", ""enter"": [
            { ""spawn"": { ""enemy"": ""rat"", ""then"": [ { ""set"": { ""var"": ""run.then"", ""value"": 1 } } ], ""else"": [ { ""set"": { ""var"": ""run.else"", ""value"": 1 } } ] } }
          ] } },
          ""areas"": { ""a"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ] } }
        }");
        loader.Finish();
        Check(content.Problems.Count == 0, "spawn outside a fight: " + string.Join("; ", content.Problems));
        if (content.Problems.Count > 0) return;
        var run = new ExpeditionRun(content, content.Areas["t:a"], new FakeBridge(new Random(1)), n => 1, (p, k, a) => k, "you");
        run.Begin();
        Check(run.Get("then") == 0 && run.Get("else") == 1, "spawn outside a fight should run else, not then");
        Check(run.Log_.Count(l => l.Tone == "bad" && l.Text.Contains("outside a fight")) == 1, "spawn outside a fight should log one bad line");
    }

    // memory.* belongs to the pack that wrote the data and lives in the bridge's store, so it outlasts a run.
    private static void TestMemory()
    {
        string Pack(string enter) => @"{
          ""rooms"": { ""r"": { ""text"": ""k"", ""enter"": " + enter + @" } },
          ""areas"": { ""a"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ] } }
        }";
        var set = new PackSet(new Registry());
        set.Add("a", "ExpeditionFramework/main.json", f => Pack(@"[
            { ""add"": { ""var"": ""memory.visits"", ""value"": 1 } },
            { ""set"": { ""var"": ""run.seen"", ""value"": ""memory.visits"" } },
            { ""if"": { ""when"": ""memory.visits >= 2"", ""then"": [ { ""set"": { ""var"": ""run.again"", ""value"": 1 } } ] } },
            { ""set_text"": { ""var"": ""memory.$who"", ""value"": ""player.name"" } },
            { ""set_text"": { ""var"": ""run.$who"", ""value"": ""memory.$who"" } },
            { ""set_text"": { ""var"": ""run.$greet"", ""line"": ""hi_{memory.$who}"" } }
        ]"));
        set.Add("b", "ExpeditionFramework/main.json", f => Pack(@"[ { ""set"": { ""var"": ""memory.visits"", ""value"": 100 } } ]"));
        Content content = set.Compile(out Dictionary<string, List<string>> problems);
        Check(problems.Values.All(l => l.Count == 0), "memory packs: " + string.Join("; ", problems.Values.SelectMany(l => l)));
        if (problems.Values.Any(l => l.Count > 0)) return;

        ExpeditionRun Play(string area, IMemory memory)
        {
            var run = new ExpeditionRun(content, content.Areas[area], new FakeBridge(new Random(1), memory: memory), n => 1, (p, k, a) => k, "you");
            run.Begin();
            return run;
        }
        var store = new MemoryStore();
        ExpeditionRun first = Play("a:a", store);
        Check(first.Get("seen") == 1 && first.Get("again") == 0, "first run sees memory.visits 1: " + first.Get("seen"));
        ExpeditionRun second = Play("a:a", store);
        Check(second.Get("seen") == 2 && second.Get("again") == 1, "second run sees the first run's memory: " + second.Get("seen"));
        Check(second.GetText("who") == "you" && second.GetText("greet") == "hi_you", "memory text reads back: " + second.GetText("who") + " / " + second.GetText("greet"));
        Play("b:a", store);
        Check(store.Get("b", "visits") == 100 && store.Get("a", "visits") == 2, "packs keep separate memory: a=" + store.Get("a", "visits") + " b=" + store.Get("b", "visits"));
        Check(store.GetText("a", "who") == "you" && store.GetText("b", "who") == "", "memory text is per pack too");
        Check(Play("a:a", new MemoryStore()).Get("seen") == 1, "a fresh store starts empty");

        string Enter(string effect) => "{\"rooms\":{\"r\":{\"text\":\"k\",\"enter\":[" + effect + "]}}}";
        List<string> p = ProblemsIn(Enter("{\"set\":{\"var\":\"memory.$x\",\"value\":1}}"));
        Check(p.Count == 1, "set on memory.$x is one problem: " + string.Join("; ", p));
        p = ProblemsIn(Enter("{\"add\":{\"var\":\"memory.$x\",\"value\":1}}"));
        Check(p.Count == 1, "add on memory.$x is one problem: " + string.Join("; ", p));
        p = ProblemsIn(Enter("{\"set_text\":{\"var\":\"memory.x\",\"value\":\"player.name\"}}"));
        Check(p.Count == 1, "set_text on memory.x is one problem: " + string.Join("; ", p));
        Expr.Compile("memory.x", out string packless);
        Check(packless != null, "memory.x without a pack should not compile");
        Expr.Compile("memory.x + 1", out string owned, pack: "t");
        Check(owned == null, "memory.x with a pack compiles: " + owned);
    }

    // On the map, hidden_when, an intel slot's when and its line cases read the memory the map was given.
    private static void TestMapMemory()
    {
        var set = new PackSet(new Registry());
        set.Add("a", "ExpeditionFramework/main.json", f => @"{
          ""rooms"": { ""r"": { ""text"": ""k"", ""enter"": [
            { ""set"": { ""var"": ""memory.unlocked"", ""value"": 1 } }, { ""add"": { ""var"": ""memory.heard"", ""value"": 1 } } ] } },
          ""areas"": { ""a"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ],
            ""hidden_when"": ""memory.unlocked < 1"",
            ""pools"": { ""leads"": [ ""r"" ] },
            ""intel"": [
              { ""id"": ""tip"", ""pool"": ""leads"", ""when"": ""memory.heard >= 1"", ""description"": ""k"",
                ""title"": { ""cases"": [ { ""when"": ""memory.heard >= 2"", ""text"": ""loud"" }, { ""text"": ""quiet"" } ] } },
              { ""id"": ""rumor"", ""pool"": ""leads"", ""title"": ""k"", ""description"": ""k"" }
            ] } }
        }");
        Content content = set.Compile(out Dictionary<string, List<string>> problems, out Dictionary<string, List<string>> warnings);
        string all = string.Join("; ", problems.Values.SelectMany(l => l).Concat(warnings.Values.SelectMany(l => l)));
        Check(problems.Values.All(l => l.Count == 0) && warnings.Values.All(l => l.Count == 0), "map memory pack loads cleanly: " + all);
        if (problems.Values.Any(l => l.Count > 0)) return;

        AreaDef area = content.Areas["a:a"];
        int Picks(MapContext m, string slot) => IntelRoller.Roll(area, n => 1, m.Root)[slot].Count;

        var bare = new MapContext(area.Id, (p, k, a) => k, name => 0);
        Check(area.HiddenWhen(bare.Root) && Picks(bare, "tip") == 0, "a map without memory reads it as 0");

        var store = new MemoryStore();
        var map = new MapContext(area.Id, (p, k, a) => k, name => 0, store);
        Check(area.HiddenWhen(map.Root), "the area stays hidden until memory unlocks it");
        Check(Picks(map, "tip") == 0 && Picks(map, "rumor") == 1, "an intel slot whose when fails names nothing; one without when still rolls");
        store.Set("a", "unlocked", 1);
        store.Set("a", "heard", 1);
        Check(!area.HiddenWhen(map.Root), "memory unlocks the area on the map");
        Check(Picks(map, "tip") == 1 && Picks(map, "rumor") == 1, "an intel slot whose when holds rolls as usual");
        Check(map.Render(area.Intel[0].Title) == "quiet", "intel line cases read memory on the map: " + map.Render(area.Intel[0].Title));
        store.Set("a", "heard", 2);
        Check(map.Render(area.Intel[0].Title) == "loud", "intel line cases follow memory: " + map.Render(area.Intel[0].Title));
        // A gated slot rolls no dice, so the slots after it draw the same dice they would without it: the log of requested
        // sides starts with rumor's own count roll and pick roll.
        var sides = new List<int>();
        Func<int, int> logged = n => { sides.Add(n); return 1; };
        store.Set("a", "heard", 0);
        Dictionary<string, List<int>> gatedRoll = IntelRoller.Roll(area, logged, map.Root);
        Check(gatedRoll.ContainsKey("tip") && gatedRoll["tip"].Count == 0, "a gated slot stays a key with no picks");
        Check(string.Join(",", sides) == "100,1", "a gated slot consumes no dice: " + string.Join(",", sides));
        sides.Clear();
        store.Set("a", "heard", 1);
        IntelRoller.Roll(area, logged, map.Root);
        Check(string.Join(",", sides) == "100,1,100,1", "an open slot rolls its own dice first: " + string.Join(",", sides));
        var other = new MemoryStore();
        other.Set("b", "unlocked", 1);
        Check(area.HiddenWhen(new MapContext(area.Id, (p, k, a) => k, name => 0, other).Root), "another pack's memory doesn't unlock this area");
    }

    // game.rep.FACTION reads reputation whole and game.rep_exact.FACTION in hundredths; the reputation effect changes it
    // through the bridge by a decimal amount (value: whole expression or literal with two decimals; value_x100: hundredths),
    // logging the game's line, a pack's own, or nothing. Zero or a faction the player lacks changes and logs nothing.
    private static void TestReputation()
    {
        Check(GameReads.RepFaction("rep.security", out bool whole) == "security" && !whole
            && GameReads.RepFaction("rep_exact.security", out bool exact) == "security" && exact
            && GameReads.RepFaction("day", out _) == null && GameReads.RepFaction(null, out _) == null,
            "rep.FACTION and rep_exact.FACTION name a faction read");
        Check(GameReads.FactionId("black_market") == "FACTION_BLACK_MARKET", "faction names map to the game's ids");
        Check(GameReads.BadRead("rep.security") == null && GameReads.BadRead("rep.church") != null && GameReads.BadRead("rep_exact.church") != null
            && GameReads.BadRead("luck") == null, "only rep reads of unknown factions are bad");
        Check(GameReads.RepRead(-1.5, false) == -1 && GameReads.RepRead(-1.5, true) == -150 && GameReads.RepRead(0.7499999999, true) == 75,
            "rep reads cut toward zero, rep_exact rounds hundredths");
        Check(GameReads.RepAmount(0.5) == "+0.5" && GameReads.RepAmount(-3) == "-3" && GameReads.RepAmount(1.25) == "+1.25",
            "amounts show signed, with decimals only when there are some");

        var content = new Content();
        var loader = new PackLoader(content, new Registry());
        loader.Load("t", "x.json", f => @"{
          ""rooms"": { ""r"": { ""text"": ""k"", ""enter"": [
            { ""set"": { ""var"": ""run.before"", ""value"": ""game.rep.security"" } },
            { ""reputation"": { ""faction"": ""security"", ""value"": 5 } },
            { ""set"": { ""var"": ""run.after"", ""value"": ""game.rep.security"" } },
            { ""reputation"": { ""faction"": ""revolution"", ""value"": ""0 - run.after"", ""night_log"": { ""text"": ""rev"", ""args"": [""arg.rep"", ""arg.rep_x100"", ""run.$rep""] } } },
            { ""reputation"": { ""faction"": ""upper_level"", ""value"": 0.5, ""quiet"": true } },
            { ""reputation"": { ""faction"": ""upper_level"", ""value_x100"": ""5 * 5"", ""quiet"": true } },
            { ""set"": { ""var"": ""run.upper"", ""value"": ""game.rep.upper_level"" } },
            { ""set"": { ""var"": ""run.upper_x100"", ""value"": ""game.rep_exact.upper_level"" } },
            { ""reputation"": { ""faction"": ""black_market"", ""value"": 0.25, ""night_log"": { ""text"": ""bm"", ""args"": [""arg.rep"", ""arg.rep_x100"", ""run.$rep""] } } },
            { ""reputation"": { ""faction"": ""lower_level"", ""value"": -0.5 } },
            { ""reputation"": { ""faction"": ""security"", ""value"": 0 } },
            { ""reputation"": { ""faction"": ""security"", ""value_x100"": ""0"" } },
            { ""reputation"": { ""faction"": ""cartel"", ""value"": 3 } }
          ] } },
          ""areas"": { ""a"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ] } }
        }");
        loader.Finish();
        Check(content.Problems.Count == 0, "reputation pack: " + string.Join("; ", content.Problems));
        if (content.Problems.Count > 0) return;
        var bridge = new FakeBridge(new Random(1));
        bridge.Rep["security"] = 10;
        var run = new ExpeditionRun(content, content.Areas["t:a"], bridge, n => 1, (p, k, a) => k + ":" + string.Join(",", a), "you");
        run.Begin();
        Check(run.Get("before") == 10 && run.Get("after") == 15, "a reputation change shows in later reads: " + run.Get("before") + " -> " + run.Get("after"));
        Check(bridge.Rep["revolution"] == -15 && run.Get("upper") == 0 && run.Get("upper_x100") == 75,
            "value is an expression, decimal and hundredths amounts add up, rep reads whole and rep_exact hundredths: "
            + run.Get("upper") + " / " + run.Get("upper_x100"));
        Check(bridge.Rep["black_market"] == 0.25 && bridge.Rep["lower_level"] == -0.5, "decimal literals reach the bridge exactly");
        Check(string.Join(",", bridge.Calls) == "security 5,revolution -15,upper_level 0.5,upper_level 0.25,black_market 0.25,lower_level -0.5",
            "zero and a missing faction change nothing: " + string.Join(",", bridge.Calls));
        string logs = string.Join(" | ", bridge.NightLogs.Select(l => l.Text + "@" + l.Color));
        Check(logs == "rep security +5@5c6f46 | rev:-15,-1500,-15@9c3738 | bm:0,25,+0.25@5c6f46 | rep lower_level -0.5@9c3738",
            "the game's line or the pack's own (whole part, hundredths, text), green for a gain and red for a loss; quiet logs nothing: " + logs);

        foreach ((string effect, string expected) in new[]
        {
            (@"{ ""reputation"": { ""faction"": ""church"", ""value"": 1 } }", "unknown faction 'church'"),
            (@"{ ""reputation"": { ""value"": 1 } }", "needs \"faction\""),
            (@"{ ""reputation"": { ""faction"": ""security"" } }", "needs \"value\" or \"value_x100\""),
            (@"{ ""reputation"": { ""faction"": ""security"", ""value"": 1, ""value_x100"": 100 } }", "not both"),
            (@"{ ""reputation"": { ""faction"": ""security"", ""value"": 0.125 } }", "more than two decimals"),
            (@"{ ""set"": { ""var"": ""run.x"", ""value"": ""game.rep.securty"" } }", "unknown faction 'securty'"),
            (@"{ ""set"": { ""var"": ""run.x"", ""value"": ""game.rep_exact.securty"" } }", "unknown faction 'securty'"),
        })
        {
            var bad = new Content();
            var badLoader = new PackLoader(bad, new Registry());
            badLoader.Load("t", "x.json", f => @"{
              ""rooms"": { ""r"": { ""text"": ""k"", ""enter"": [ " + effect + @" ] } },
              ""areas"": { ""a"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ] } }
            }");
            badLoader.Finish();
            Check(bad.Problems.Any(p => p.Contains(expected)), "bad reputation use is reported (" + expected + "): " + string.Join("; ", bad.Problems));
        }
        List<string> unread = WarningsIn(@"{""rooms"":{""r"":{""text"":""k"",""enter"":[{""reputation"":{""faction"":""security"",""value"":1,""amount"":1}}]}}}");
        Check(unread.Count == 1 && unread[0].Contains("reputation.amount"), "a misspelled reputation argument is warned about: " + string.Join("; ", unread));
    }

    // game.clears reads the reading area's clears and game.clears.AREA another area of its pack, so hidden_when can
    // unlock an area on a clear OR on memory.
    private static void TestGameReads()
    {
        Check(GameReads.ClearsArea("clears", "a:cellar") == "a:cellar", "game.clears counts the reading area");
        Check(GameReads.ClearsArea("clears.vault", "a:cellar") == "a:vault", "game.clears.AREA counts AREA of the reading area's pack");
        Check(GameReads.ClearsArea("clears.", "a:cellar") == null && GameReads.ClearsArea("clearsvault", "a:cellar") == null
            && GameReads.ClearsArea("day", "a:cellar") == null && GameReads.ClearsArea(null, "a:cellar") == null, "other names aren't clears reads");

        var set = new PackSet(new Registry());
        set.Add("a", "ExpeditionFramework/main.json", f => @"{
          ""rooms"": { ""r"": { ""text"": ""k"", ""enter"": [ { ""set"": { ""var"": ""memory.key"", ""value"": 1 } } ] } },
          ""areas"": {
            ""vault"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ] },
            ""cellar"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ],
              ""hidden_when"": ""game.clears.vault < 1 && memory.key < 1"" } }
        }");
        Content content = set.Compile(out Dictionary<string, List<string>> problems, out Dictionary<string, List<string>> warnings);
        string all = string.Join("; ", problems.Values.SelectMany(l => l).Concat(warnings.Values.SelectMany(l => l)));
        Check(problems.Values.All(l => l.Count == 0) && warnings.Values.All(l => l.Count == 0), "game reads pack loads cleanly: " + all);
        if (problems.Values.Any(l => l.Count > 0)) return;

        AreaDef cellar = content.Areas["a:cellar"];
        var asked = new List<string>();
        int vaultClears = 0;
        var store = new MemoryStore();
        var map = new MapContext(cellar.Id, (p, k, a) => k, name => { asked.Add(name); return name == "clears.vault" ? vaultClears : 0; }, store);
        Check(cellar.HiddenWhen(map.Root), "the cellar stays hidden before the vault is cleared or the key remembered");
        Check(asked.Contains("clears.vault"), "game.clears.AREA reaches the host as clears.AREA: " + string.Join(",", asked));
        vaultClears = 1;
        Check(!cellar.HiddenWhen(map.Root), "clearing the vault unlocks the cellar");
        vaultClears = 0;
        store.Set("a", "key", 1);
        Check(!cellar.HiddenWhen(map.Root), "remembering the key also unlocks the cellar");
    }

    // A memory name a pack reads but never writes always reads 0 or "", so loading warns once per name, at its first read.
    // Another pack's write doesn't count, and memory.count and memory.$count are different names.
    private static void TestMemoryWarnings()
    {
        var set = new PackSet(new Registry());
        set.Add("a", "ExpeditionFramework/main.json", f => @"{
          ""rooms"": { ""r"": { ""text"": ""k"", ""enter"": [
            { ""add"": { ""var"": ""memory.kept"", ""value"": 1 } },
            { ""set"": { ""var"": ""memory.count"", ""value"": 1 } },
            { ""set_text"": { ""var"": ""memory.$name"", ""value"": ""player.name"" } },
            { ""set"": { ""var"": ""run.x"", ""value"": ""memory.kept + memory.ghost + memory.ghost"" } },
            { ""set_text"": { ""var"": ""run.$y"", ""value"": ""memory.$name"" } },
            { ""set_text"": { ""var"": ""run.$z"", ""value"": ""memory.$lost"" } },
            { ""set_text"": { ""var"": ""run.$w"", ""line"": ""hi_{memory.$gone}"" } },
            { ""set"": { ""var"": ""run.v"", ""value"": ""memory.theirs"" } },
            { ""set_text"": { ""var"": ""run.$c"", ""value"": ""memory.$count"" } }
          ] } },
          ""areas"": { ""a"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ], ""hidden_when"": ""memory.ghost > 0"" } }
        }");
        set.Add("b", "ExpeditionFramework/main.json", f => @"{
          ""rooms"": { ""r"": { ""text"": ""k"", ""enter"": [ { ""set"": { ""var"": ""memory.theirs"", ""value"": 1 } } ] } }
        }");
        set.Compile(out Dictionary<string, List<string>> problems, out Dictionary<string, List<string>> warnings);
        Check(problems.Values.All(l => l.Count == 0), "memory warning packs: " + string.Join("; ", problems.Values.SelectMany(l => l)));
        List<string> a = warnings.TryGetValue("a", out List<string> wa) ? wa : new List<string>();
        string all = string.Join("; ", a);
        const string Unwritten = " is read but never written by this pack";
        foreach (string name in new[] { "ghost", "$lost", "$gone", "theirs", "$count" })
        {
            Check(a.Count(w => w.Contains("memory." + name + Unwritten)) == 1, "one warning for memory." + name + ": " + all);
        }
        Check(a.Count == 5, "exactly the five unwritten names warn: " + all);
        Check(a.Any(w => w.Contains("memory.ghost" + Unwritten + ", so it always reads 0")), "a number warning says it reads 0: " + all);
        Check(a.Any(w => w.Contains("memory.$lost" + Unwritten + ", so it always reads \"\"")), "a text warning says it reads \"\": " + all);
        Check(!warnings.TryGetValue("b", out List<string> wb) || wb.Count == 0, "a pack that only writes doesn't warn: " + string.Join("; ", wb ?? new List<string>()));
    }

    // memory.PACK.NAME reads another pack's memory and can't write it. A pack that isn't loaded reads 0 or "" and warns, and
    // a name that pack never writes warns, as an own-pack read does.
    private static void TestPackMemory()
    {
        string Pack(string enter) => @"{
          ""rooms"": { ""r"": { ""text"": ""k"", ""enter"": " + enter + @" } },
          ""areas"": { ""a"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ] } }
        }";
        var set = new PackSet(new Registry());
        set.Add("a", "ExpeditionFramework/main.json", f => Pack(@"[
            { ""set"": { ""var"": ""memory.visits"", ""value"": 7 } },
            { ""set_text"": { ""var"": ""memory.$who"", ""value"": ""player.name"" } }
        ]"));
        set.Add("b", "ExpeditionFramework/main.json", f => Pack(@"[
            { ""add"": { ""var"": ""memory.visits"", ""value"": 1 } },
            { ""set"": { ""var"": ""run.theirs"", ""value"": ""memory.a.visits * 10 + memory.visits"" } },
            { ""set_text"": { ""var"": ""run.$who"", ""value"": ""memory.a.$who"" } },
            { ""set_text"": { ""var"": ""run.$greet"", ""line"": ""hi_{memory.a.$who}"" } },
            { ""set"": { ""var"": ""run.ghost"", ""value"": ""memory.a.ghost + memory.b.visits"" } }
        ]"));
        Content content = set.Compile(out Dictionary<string, List<string>> problems, out Dictionary<string, List<string>> warnings);
        Check(problems.Values.All(l => l.Count == 0), "pack memory packs: " + string.Join("; ", problems.Values.SelectMany(l => l)));
        if (problems.Values.Any(l => l.Count > 0)) return;
        List<string> wb = warnings.TryGetValue("b", out List<string> w) ? w : new List<string>();
        Check(wb.Count == 1 && wb[0].Contains("memory.a.ghost is read but never written by pack a, so it always reads 0"),
            "a never-written name of another pack warns once: " + string.Join("; ", wb));
        Check(!warnings.TryGetValue("a", out List<string> wa) || wa.Count == 0, "the read pack doesn't warn: " + string.Join("; ", wa ?? new List<string>()));

        ExpeditionRun Play(string area, IMemory memory)
        {
            var run = new ExpeditionRun(content, content.Areas[area], new FakeBridge(new Random(1), memory: memory), n => 1, (p, k, a) => k, "you");
            run.Begin();
            return run;
        }
        var store = new MemoryStore();
        Play("a:a", store);
        ExpeditionRun b = Play("b:a", store);
        Check(b.Get("theirs") == 71, "memory.a.visits reads pack a's value beside b's own: " + b.Get("theirs"));
        Check(b.GetText("who") == "you" && b.GetText("greet") == "hi_you", "memory.a.$who reads pack a's text: " + b.GetText("who") + " / " + b.GetText("greet"));
        Check(store.Get("a", "visits") == 7 && store.Get("b", "visits") == 1, "reading another pack leaves both values as written: a="
            + store.Get("a", "visits") + " b=" + store.Get("b", "visits"));

        string Enter(string effect) => "{\"rooms\":{\"r\":{\"text\":\"k\",\"enter\":[" + effect + "]}}}";
        foreach (string effect in new[]
        {
            "{\"set\":{\"var\":\"memory.a.x\",\"value\":1}}",
            "{\"add\":{\"var\":\"memory.a.x\",\"value\":1}}",
            "{\"set_text\":{\"var\":\"memory.a.$x\",\"value\":\"player.name\"}}",
            "{\"set\":{\"var\":\"memory.t.x\",\"value\":1}}",
        })
        {
            List<string> p = ProblemsIn(Enter(effect));
            Check(p.Count == 1 && p[0].Contains("can't be changed"), "writing memory.PACK.NAME is one problem: " + effect + " -> " + string.Join("; ", p));
        }
        var lone = new PackSet(new Registry());
        lone.Add("t", "ExpeditionFramework/main.json", f => Pack(@"[
            { ""set"": { ""var"": ""run.x"", ""value"": ""memory.nobody.x + 5"" } },
            { ""set_text"": { ""var"": ""run.$g"", ""line"": ""hi_{memory.nobody.$x}"" } }
        ]"));
        Content loneContent = lone.Compile(out Dictionary<string, List<string>> lp, out Dictionary<string, List<string>> lw);
        Check(lp.Values.All(l => l.Count == 0), "an unloaded pack's memory doesn't fail the load: " + string.Join("; ", lp.Values.SelectMany(l => l)));
        List<string> wt = lw.TryGetValue("t", out List<string> lwt) ? lwt : new List<string>();
        Check(wt.Count == 2 && wt.Count(x => x.Contains("memory.nobody.x reads pack nobody, which isn't loaded, so it reads 0")) == 1
            && wt.Count(x => x.Contains("memory.nobody.$x reads pack nobody, which isn't loaded, so it reads \"\"")) == 1,
            "each read of an unloaded pack warns once: " + string.Join("; ", wt));
        if (lp.Values.All(l => l.Count == 0))
        {
            var loneRun = new ExpeditionRun(loneContent, loneContent.Areas["t:a"], new FakeBridge(new Random(1), memory: new MemoryStore()), n => 1, (p, k, a) => k, "you");
            loneRun.Begin();
            Check(loneRun.Get("x") == 5 && loneRun.GetText("g") == "hi_", "an unloaded pack's memory reads 0 and \"\": " + loneRun.Get("x") + " / " + loneRun.GetText("g"));
        }

        var standIn = new PackSet(new Registry()) { OtherPacks = new[] { "nobody" } };
        standIn.Add("t", "ExpeditionFramework/main.json", f => Pack(@"[ { ""set"": { ""var"": ""run.x"", ""value"": ""memory.nobody.x"" } } ]"));
        standIn.Compile(out Dictionary<string, List<string>> sp, out Dictionary<string, List<string>> sw);
        Check(sp.Values.All(l => l.Count == 0) && sw.Values.All(l => l.Count == 0), "a stand-in pack's memory reads without problem or warning: "
            + string.Join("; ", sp.Values.Concat(sw.Values).SelectMany(l => l)));

        Expr.Compile("memory.a.b.c", out string three, pack: "t");
        Check(three != null, "memory.a.b.c should not compile");
        Expr.Compile("memory.a.x", out string packless);
        Check(packless != null, "memory.a.x without a pack should not compile");
    }

    // An ammo item is worth its `rounds` in any gun, and reload uses as many items as it takes to fill the gun.
    private static void TestReloadUsesWholeItems()
    {
        var content = new Content();
        var loader = new PackLoader(content, new Registry());
        loader.Load("t", "x.json", f => @"{
          ""rooms"": { ""r"": { ""text"": ""k"", ""interactions"": [
            { ""id"": ""rl"", ""label"": ""k"", ""uses"": { ""select"": ""#firearm"", ""with"": { ""select"": ""#ammo"", ""each"": false } }, ""do"": [ { ""reload"": true } ] }
          ] } },
          ""areas"": { ""a"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ] } }
        }");
        loader.Finish();
        Check(content.Problems.Count == 0, "reload: " + string.Join("; ", content.Problems));
        if (content.Problems.Count > 0) return;
        var cases = new (string name, int loaded, int gunKind, int perItem, int spent, int count, int ammoKind, int wantLoaded, int wantUsed, int wantSpent)[]
        {
            ("empty gun, 4 a piece", 0, 0, 4, 0, 5, 0, 8, 2, 0),
            ("top up 5 empties one and opens the next", 3, 0, 4, 0, 5, 0, 8, 1, 1),
            ("top up 2 opens one, keeps it", 6, 0, 4, 0, 5, 0, 8, 0, 2),
            ("opened item is drawn first", 6, 0, 4, 2, 5, 0, 8, 1, 0),
            ("other kind dumps what's loaded", 6, 0, 4, 0, 5, 1, 8, 2, 0),
            ("only one item left", 0, 0, 4, 0, 1, 0, 4, 1, 0),
            ("last opened item gives what's left", 0, 0, 4, 2, 1, 0, 2, 1, 0),
            ("12 a piece fills an 8 and keeps the item", 0, 0, 12, 0, 5, 0, 8, 0, 8),
            ("no rounds stat fills with one", 0, 0, 0, 0, 5, 0, 8, 1, 0),
        };
        foreach (var c in cases)
        {
            var bridge = new FakeBridge(new Random(1), withEverything: true);
            RunItem gun = bridge.Items().First(i => i.Id == "pistol");
            RunItem ammo = bridge.Items().First(i => i.Id == "rounds");
            gun.Props["capacity"] = 8;
            gun.Props["loaded"] = c.loaded;
            gun.Props["round_kind"] = c.gunKind;
            ammo.Count = c.count;
            ammo.Props["rounds"] = c.perItem;
            ammo.Props["spent"] = c.spent;
            ammo.Props["kind"] = c.ammoKind;
            var run = new ExpeditionRun(content, content.Areas["t:a"], bridge, n => 1, (p, k, a) => k, "you");
            run.Begin();
            RunOption reload = run.Options().FirstOrDefault(o => o.Interaction.Id == "rl");
            Check(reload != null, "reload (" + c.name + ") should be offered");
            if (reload == null) continue;
            run.Perform(reload);
            int used = c.count - (bridge.Items().Contains(ammo) ? ammo.Count : 0);
            Check(gun.Get("loaded") == c.wantLoaded && used == c.wantUsed && run.Get("reload_used") == c.wantUsed,
                "reload (" + c.name + "): loaded " + gun.Get("loaded") + " using " + used + ", wanted " + c.wantLoaded + " using " + c.wantUsed);
            Check(gun.Get("round_kind") == c.ammoKind, "reload (" + c.name + ") should take the new kind");
            if (c.perItem <= 0) continue;
            bool left = bridge.Items().Contains(ammo);
            Check(!left || ammo.Get("spent") == c.wantSpent, "reload (" + c.name + "): spent " + ammo.Get("spent") + ", wanted " + c.wantSpent);
            int roundsBefore = c.count * c.perItem - c.spent;
            int roundsAfter = left ? ammo.Count * c.perItem - ammo.Get("spent") : 0;
            int keptInGun = c.ammoKind == c.gunKind ? c.loaded : 0;
            Check(roundsBefore - roundsAfter == gun.Get("loaded") - keptInGun, "reload (" + c.name + ") should neither lose nor make rounds");
        }
    }

    // Drawing buttons and the info window never rolls: no flicker, and the run's dice are left for real actions.
    private static void TestRenderingDoesNotRoll()
    {
        var content = new Content();
        var loader = new PackLoader(content, new Registry());
        loader.Load("t", "x.json", f => @"{
          ""rooms"": { ""r"": { ""text"": ""k"", ""interactions"": [
            { ""id"": ""v"", ""label"": { ""text"": ""k"", ""variants"": 3 }, ""when"": ""1d6 >= 1"", ""do"": [] }
          ] } },
          ""areas"": { ""a"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ],
            ""hud"": { ""rows"": [ { ""when"": ""1d6 > 0"", ""parts"": [ ""k"" ] } ], ""enemies"": { ""line"": ""k"" } } } }
        }");
        loader.Finish();
        Check(content.Problems.Count == 0, "rendering: " + string.Join("; ", content.Problems));
        if (content.Problems.Count > 0) return;
        int rolls = 0;
        var run = new ExpeditionRun(content, content.Areas["t:a"], new FakeBridge(new Random(1)), n => { rolls++; return n; }, (p, k, a) => k, "you");
        run.Begin();
        int before = rolls;
        var labels = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            labels.AddRange(run.Options().Where(o => o.Interaction.Id == "v").Select(o => o.Label));
            run.Hud();
            run.PanelTitle("x");
        }
        Check(rolls == before, "rendering rolled " + (rolls - before) + " times");
        Check(labels.Count == 10 && labels.All(l => l == "k_0"), "a variants label should render as its first text: " + string.Join(",", labels.Distinct()));
    }

    // A button's info: text lines, icons and bars in order, "when" leaving pieces out, and a check's odds added last
    // unless a piece already shows them.
    private static void TestActionInfo()
    {
        var content = new Content();
        var loader = new PackLoader(content, new Registry());
        loader.Load("t", "x.json", f => @"{
          ""rooms"": { ""r"": { ""text"": ""k"", ""interactions"": [
            { ""id"": ""a"", ""label"": ""short"", ""uses"": ""pistol"",
              ""info"": [ { ""icon"": ""item"" }, { ""text"": ""ammo"", ""args"": [""item.loaded""] }, { ""text"": ""never"", ""when"": ""0"" },
                { ""bar"": ""item.loaded"", ""max"": ""item.capacity"", ""tone"": ""good"" }, { ""icon"": ""pic"" } ],
              ""check"": { ""chance"": ""40"", ""success"": [], ""failure"": [] } },
            { ""id"": ""b"", ""label"": ""short"", ""info"": [ { ""bar"": ""arg.percent"", ""label"": { ""text"": ""odds"", ""args"": [""percent""] } } ],
              ""check"": { ""chance"": ""80"", ""success"": [], ""failure"": [] } },
            { ""id"": ""c"", ""label"": ""old"", ""check"": { ""chance"": ""80"", ""success"": [], ""failure"": [] } }
          ] } },
          ""areas"": { ""a"": { ""title"": ""k"", ""player"": { ""hp"": 1 }, ""layout"": [ { ""room"": ""r"" } ] } }
        }");
        loader.Finish();
        Check(content.Problems.Count == 0, "info: " + string.Join("; ", content.Problems));
        if (content.Problems.Count > 0) return;
        var bridge = new FakeBridge(new Random(1), withEverything: true);
        RunItem gun = bridge.Items().First(i => i.Id == "pistol");
        gun.Props["capacity"] = 8;
        gun.Props["loaded"] = 5;
        var run = new ExpeditionRun(content, content.Areas["t:a"], bridge, n => 1, (p, k, a) => k + "(" + string.Join(",", a) + ")", "you");
        run.Begin();
        RunOption a = run.Options().FirstOrDefault(o => o.Interaction.Id == "a");
        Check(a != null && string.Join("|", a.Info) == "icon:pistol|ammo(5)|bar:5/8:|icon:t:pic|ef_odds(40)",
            "info pieces in order, with odds last: " + (a == null ? "missing" : string.Join("|", a.Info)));
        Check(a != null && a.Info[2].Tone == "good" && a.Info[4].Tone == "plain", "a bar keeps its tone and middling odds are plain");
        RunOption b = run.Options().FirstOrDefault(o => o.Interaction.Id == "b");
        Check(b != null && string.Join("|", b.Info) == "bar:80/100:odds(80)", "a piece showing the odds replaces the odds line: " + (b == null ? "missing" : string.Join("|", b.Info)));
        RunOption c = run.Options().FirstOrDefault(o => o.Interaction.Id == "c");
        Check(c != null && string.Join("|", c.Info) == "ef_odds(80)" && c.Label == "old(80)", "without info, the label still gets the odds and the info shows them");
        List<string> problems = Compiled(@"{""rooms"":{""r"":{""text"":""k"",""interactions"":[{""label"":""k"",""info"":[{""icon"":""with""}],""do"":[]}]}}}").Problems;
        Check(problems.Any(p => p.Contains("uses.with")), "an icon of the paired item needs uses.with: " + string.Join("; ", problems));
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
            int total = entries.Sum(e => e.Number("n", 0));
            IItemUse use = spec.Use();
            Func<IScope, IReadOnlyList<string>> enemies = spec.Enemies();
            string status = spec.Ref("burn", "status");
            return s =>
            {
                s.Run.Set("pool_pick", pool.Pick(s) == "t:rat" ? 1 : 0);
                s.Run.Set("entries", total);
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
        Check(content.Warnings.Count == 0, "public api warnings: " + string.Join("; ", content.Warnings));
        if (content.Problems.Count > 0) return;

        var bridge = new FakeBridge(new Random(1));
        var run = new ExpeditionRun(content, content.Areas["t:a"], bridge, n => 1, (p, k, a) => k, "you");
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
        bridge.Unknown.Add("ghost");
        Check(api.Inventory.Give("ghost") == null && run.Log_.Last().Tone == "bad" && run.Log_.Last().Text.Contains("ghost"), "an item the game can't give is logged");
        int beforeEmpty = run.Log_.Count;
        api.Inventory.Give("");
        api.Inventory.Give(null);
        Check(run.Log_.Count == beforeEmpty, "giving nothing is silent");
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

        public FakeBridge(Random random, bool withEverything = false, IMemory memory = null)
        {
            this.random = random;
            Memory = memory ?? new MemoryStore();
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

        public readonly HashSet<string> Unknown = new HashSet<string>();
        public string GiveItem(string itemId) => string.IsNullOrEmpty(itemId) || Unknown.Contains(itemId) ? null : itemId;
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
        public List<string> LoseCarried(Func<int, int> chanceForSize) => new List<string>();
        public readonly Dictionary<string, double> Rep = new Dictionary<string, double>();
        public readonly List<string> Calls = new List<string>();
        public int GameValue(string name)
        {
            string faction = GameReads.RepFaction(name, out bool exact);
            return faction != null && Rep.TryGetValue(faction, out double rep) ? GameReads.RepRead(rep, exact) : 0;
        }
        public IMemory Memory { get; }
        public void Wound(string kind) { }
        // Every faction but the cartel, as on a start other than the drug dealer's.
        public string Reputation(string faction, double amount)
        {
            if (faction == "cartel") return null;
            Calls.Add(faction + " " + amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Rep[faction] = (Rep.TryGetValue(faction, out double rep) ? rep : 0) + amount;
            return "rep " + faction + " " + GameReads.RepAmount(amount);
        }
        public readonly List<(string Text, string Color)> NightLogs = new List<(string, string)>();
        public void NightLog(string text, string color) => NightLogs.Add((text, color));
        public void PlaySound(string id) { }
        public void PlayItemSound(RunItem item, string fallback) { }
        public bool OpenTrade(TradeRequest request) => false;
        public TradeResult CloseTrade() => new TradeResult();
        public List<string> TakeCart(TradeRequest cart) => new List<string>();
    }
}
