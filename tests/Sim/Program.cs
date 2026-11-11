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
        TestEngineVarsAreReadOnly();
        TestSpawnOutsideFight();
        TestRenderingDoesNotRoll();
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
