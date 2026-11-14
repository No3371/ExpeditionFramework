using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ExpeditionFramework.Api;
using Node = System.Collections.Generic.Dictionary<string, object>;

namespace ExpeditionFramework.Core
{
    /// <summary>The effects and conditions data can name. Built-ins and mods register here the same way.</summary>
    public sealed class Registry
    {
        public readonly Dictionary<string, EffectCompiler> Effects = new Dictionary<string, EffectCompiler>();
        public readonly Dictionary<string, ConditionCompiler> Conditions = new Dictionary<string, ConditionCompiler>();
        public readonly RunListeners Listeners = new RunListeners();
        private readonly HashSet<string> customHooks = new HashSet<string>();

        public Registry() { Builtins.Register(this); }

        /// <summary>The full name of a hook as written in a pack, or null if an unqualified name means nothing.</summary>
        internal string ResolveHook(string pack, string name)
        {
            if (Array.IndexOf(ExpeditionRun.HookNames, name) >= 0 || name.Contains(":")) return name;
            string full = pack + ":" + name;
            return customHooks.Contains(full) ? full : null;
        }

        internal bool IsHook(string fullName) => Array.IndexOf(ExpeditionRun.HookNames, fullName) >= 0 || customHooks.Contains(fullName);

        public IExpeditionRegistrar For(string packId) => new PackRegistrar(this, packId);

        private sealed class PackRegistrar : IExpeditionRegistrar
        {
            private readonly Registry registry;
            public string PackId { get; }

            public PackRegistrar(Registry registry, string packId) { this.registry = registry; PackId = packId; }

            public void AddEffect(string name, EffectCompiler compiler) => registry.Effects[PackId + ":" + name] = compiler;
            public void AddCondition(string name, ConditionCompiler compiler) => registry.Conditions[PackId + ":" + name] = compiler;
            public void AddHook(string name) => registry.customHooks.Add(PackId + ":" + name);

            public void On(string hook, Effect listener)
            {
                string full = registry.ResolveHook(PackId, hook);
                if (full == null) throw new ArgumentException("unknown hook \"" + hook + "\". Declare it with AddHook first, or qualify another pack's hook.");
                registry.Listeners.Listen(full, listener);
            }

            public void Intercept(string decision, Interceptor interceptor, int order = 0)
            {
                if (!decision.Contains(":") && Array.IndexOf(ExpeditionRun.DecisionNames, decision) < 0)
                {
                    throw new ArgumentException("unknown decision \"" + decision + "\" (built in: " + string.Join(", ", ExpeditionRun.DecisionNames) + ")");
                }
                registry.Listeners.Intercept(decision, interceptor, order);
            }
        }
    }

    /// <summary>The C# code mods have attached to hooks and decisions.</summary>
    public sealed class RunListeners
    {
        private static readonly List<Effect> NoListeners = new List<Effect>();
        private static readonly List<Interceptor> NoInterceptors = new List<Interceptor>();

        private readonly Dictionary<string, List<Effect>> listeners = new Dictionary<string, List<Effect>>();
        private readonly Dictionary<string, List<(int Order, Interceptor Handler)>> interceptors = new Dictionary<string, List<(int, Interceptor)>>();
        private readonly Dictionary<string, List<Interceptor>> sorted = new Dictionary<string, List<Interceptor>>();

        internal void Listen(string hook, Effect listener)
        {
            if (!listeners.TryGetValue(hook, out List<Effect> list)) listeners[hook] = list = new List<Effect>();
            list.Add(listener);
        }

        internal void Intercept(string decision, Interceptor handler, int order)
        {
            if (!interceptors.TryGetValue(decision, out List<(int, Interceptor)> list)) interceptors[decision] = list = new List<(int, Interceptor)>();
            list.Add((order, handler));
            sorted[decision] = list.OrderBy(e => e.Item1).Select(e => e.Item2).ToList();
        }

        internal List<Effect> ListenersOf(string hook) => listeners.TryGetValue(hook, out List<Effect> list) ? list : NoListeners;

        internal List<Interceptor> InterceptorsOf(string decision) => sorted.TryGetValue(decision, out List<Interceptor> list) ? list : NoInterceptors;
    }

    /// <summary>Compiled candidates of one kind, picked by weight among those whose conditions hold.</summary>
    internal sealed class DefPool<T> : IPool where T : Def
    {
        private readonly Func<IRun, List<T>> pool;

        public DefPool(Func<IRun, List<T>> pool) { this.pool = pool; }

        public IReadOnlyList<string> Ids(IScope scope) => pool(scope.Run).Select(d => d.Id).ToList();

        public string Pick(IScope scope)
        {
            List<T> open = pool(scope.Run).Where(d => d.When == null || d.When(scope)).ToList();
            int total = open.Sum(d => d.Weight);
            if (total <= 0) return null;
            int roll = scope.Run.Roll(total);
            foreach (T def in open)
            {
                roll -= def.Weight;
                if (roll <= 0) return def.Id;
            }
            return open[open.Count - 1].Id;
        }
    }

    /// <summary>Compiles pack files into <see cref="Content"/>. Every problem is reported with where it is; nothing throws.</summary>
    public sealed class PackLoader
    {
        private static readonly string[] Sections =
        {
            "include", "item_groups", "item_stats", "templates", "effects", "values", "interactions", "behaviors",
            "statuses", "intents", "loot", "carts", "enemies", "encounters", "events", "rooms", "kits", "areas", "debug_kit", "patch",
        };

        // What a patch can target: everything a pack defines by name.
        private static readonly string[] Patchable =
        {
            "item_groups", "item_stats", "templates", "effects", "values", "interactions", "behaviors",
            "statuses", "intents", "loot", "carts", "enemies", "encounters", "events", "rooms", "kits", "areas",
        };

        private readonly Content content;
        private readonly Registry registry;
        private readonly List<Action> checks = new List<Action>();
        private readonly Dictionary<string, object> behaviors = new Dictionary<string, object>();
        private readonly Dictionary<string, Node> templates = new Dictionary<string, Node>();
        private readonly Dictionary<string, Node> sharedInteractions = new Dictionary<string, Node>();
        private readonly Dictionary<string, HashSet<string>> valueUses = new Dictionary<string, HashSet<string>>();
        private readonly List<(string Pool, string Kind, string At, string Pack)> poolUses = new List<(string, string, string, string)>();
        private readonly List<(string Pack, List<(string At, Node Root)> Files)> pending = new List<(string, List<(string, Node)>)>();
        private readonly Dictionary<string, List<string>> problemsByPack = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, List<string>> warningsByPack = new Dictionary<string, List<string>>();
        // memory.* names: where each pack first reads each (owner, name), and which (owner, name) are written. Only the owner
        // writes its names, so one it never writes always reads 0 or "": a typo or a missing set. Finish warns about those
        // and about reads of a pack that isn't loaded.
        private readonly Dictionary<(string Reader, string Owner, string Name), (string At, string Path)> memoryReads =
            new Dictionary<(string Reader, string Owner, string Name), (string At, string Path)>();
        private readonly HashSet<(string Owner, string Name)> memoryWrites = new HashSet<(string Owner, string Name)>();
        private readonly HashSet<string> hitSets = new HashSet<string>();
        private readonly Dictionary<(string Reader, string Name), (string At, string Path)> hitReads = new Dictionary<(string, string), (string, string)>();

        internal void NoteHitSet(string name) => hitSets.Add(name);

        /// <summary>Notes a path an expression reads: memory paths for the read-never-written check, hit paths for the read-never-set one.</summary>
        private void NoteRead(string pack, string path, string at)
        {
            if (path.StartsWith("hit.", StringComparison.Ordinal))
            {
                string name = path.Substring(4);
                if (!hitReads.ContainsKey((pack, name))) hitReads[(pack, name)] = (at, path);
                return;
            }
            NoteMemory(pack, path, at, write: false);
        }
        private readonly HashSet<string> packs = new HashSet<string>();
        private readonly List<string> patchers = new List<string>();
        // Text-parameter shared effects compile at each call site, so an effect whose body calls itself through its text
        // parameters would expand forever. These are the effects mid-expansion; meeting one again is that recursion.
        private readonly HashSet<string> expanding = new HashSet<string>();
        // Text-parameter effects no call has compiled yet. Their body is only checked at a call site, so one nobody calls is unchecked.
        private readonly Dictionary<string, (string Pack, string At)> uncalledText = new Dictionary<string, (string Pack, string At)>();
        private static readonly Regex TextValue = new Regex("^[a-z0-9_]+$");
        private static readonly Regex TextParamName = new Regex("^\\$[a-z0-9_]+$");
        private static readonly Regex BareTextArg = new Regex("(?<![a-z0-9_.])arg\\.\\$([a-z0-9_]*)");
        private static readonly Regex Placeholder = new Regex("\\{arg\\.\\$([^}]*)\\}");
        private string compilingValue;
        private string folder = "";
        private string current = "";

        public PackLoader(Content content, Registry registry)
        {
            this.content = content;
            this.registry = registry;
            content.Listeners = registry.Listeners;
        }

        /// <summary>
        /// Whether a mod with this id is installed and enabled. Includes that require a missing mod are skipped, and
        /// mod(id) in expressions reads it. Mods can't change mid-game, so this is asked once, while loading.
        /// </summary>
        public Func<string, bool> HasMod { get; set; } = id => false;

        /// <summary>
        /// Packs not loaded here whose memory a loaded pack may still read as memory.PACK.NAME, for tools that load one pack
        /// alone. What they write is unknown, so reads of them are never warned about. Null means none.
        /// </summary>
        public ICollection<string> OtherPacks { get; set; }

        /// <summary>The packs whose patches are applied. Null applies every pack's patches.</summary>
        public ICollection<string> PatchesFrom { get; set; }

        /// <summary>Packs added so far that have a "patch" section, in the order they were added.</summary>
        public IReadOnlyList<string> Patchers => patchers;

        /// <summary>Each problem so far, under the pack that caused it. A patch's problems belong to the pack that wrote it.</summary>
        public IReadOnlyDictionary<string, List<string>> ProblemsByPack => problemsByPack;

        /// <summary>Each warning so far, under the pack being compiled when it was found. Warnings never stop a pack loading.</summary>
        public IReadOnlyDictionary<string, List<string>> WarningsByPack => warningsByPack;

        /// <summary>Loads one pack on its own: <see cref="Add"/> then <see cref="CompileAll"/>.</summary>
        public void Load(string pack, string mainFile, Func<string, string> read)
        {
            Add(pack, mainFile, read);
            CompileAll();
        }

        /// <summary>
        /// Reads main.json and the files it lists under "include", which sit in the same folder. Nothing is compiled until
        /// <see cref="CompileAll"/>, so every pack added before then can be patched.
        /// </summary>
        public void Add(string pack, string mainFile, Func<string, string> read)
        {
            current = pack;
            var files = new List<(string At, Node Root)>();
            int slash = mainFile.LastIndexOf('/');
            folder = slash >= 0 ? mainFile.Substring(0, slash + 1) : "";
            ReadFile(pack, mainFile, read, files, new HashSet<string>());
            pending.Add((pack, files));
            packs.Add(pack);
            if (files.Any(f => f.Root.ContainsKey("patch")) && !patchers.Contains(pack)) patchers.Add(pack);
        }

        /// <summary>
        /// Applies patches, then declares the named pieces of every added pack before compiling any, so a file can use one
        /// written later, in another file or in another pack.
        /// </summary>
        public void CompileAll()
        {
            foreach ((string pack, List<(string At, Node Root)> files) in pending)
            {
                if (PatchesFrom != null && !PatchesFrom.Contains(pack)) continue;
                current = pack;
                foreach ((string at, Node root) in files)
                {
                    List<object> patches = List(root, "patch");
                    for (int i = 0; i < patches.Count; i++) ApplyPatch(patches[i], at + ".patch[" + i + "]");
                }
            }
            foreach ((string pack, List<(string At, Node Root)> files) in pending)
            {
                current = pack;
                foreach ((string at, Node root) in files) Declare(pack, at, root);
            }
            foreach ((string pack, List<(string At, Node Root)> files) in pending)
            {
                foreach ((string at, Node root) in files)
                {
                    current = pack;
                    CompileFile(pack, at, root);
                }
            }
            pending.Clear();
        }

        // ---- Patches ----

        private static readonly string[] PatchKeys = { "target", "replace", "remove", "set", "add" };

        /// <summary>
        /// {"target": "pack:kind.id", "replace": def, "remove": {path: [ids]}, "set": {path: value}, "add": {path: [items]}},
        /// applied in that order to the target pack's JSON before anything is compiled. The id can be * for all of a kind.
        /// A pack that isn't installed is skipped. Paths are dotted keys inside the definition.
        /// </summary>
        private void ApplyPatch(object entry, string at)
        {
            if (!(entry is Node patch))
            {
                Problem(at, "a patch is an object with a target");
                return;
            }
            foreach (string key in patch.Keys.Where(k => Array.IndexOf(PatchKeys, k) < 0))
            {
                Problem(at, "unknown key \"" + key + "\" (a patch has " + string.Join(", ", PatchKeys) + ")");
            }
            string target = Str(patch, "target");
            int colon = target?.IndexOf(':') ?? -1;
            int dot = colon > 0 ? target.IndexOf('.', colon) : -1;
            if (dot < 0 || dot == target.Length - 1)
            {
                Problem(at + ".target", "must be \"pack:kind.id\", such as night_expeditions_reborn:loot.ne_arms");
                return;
            }
            string pack = target.Substring(0, colon);
            string kind = target.Substring(colon + 1, dot - colon - 1);
            string id = target.Substring(dot + 1);
            if (Array.IndexOf(Patchable, kind) < 0)
            {
                Problem(at + ".target", "unknown kind \"" + kind + "\" (one of " + string.Join(", ", Patchable) + ")");
                return;
            }
            List<(string At, Node Root)> files = pending.FirstOrDefault(p => p.Pack == pack).Files;
            if (files == null) return;
            var found = new List<(Node Section, string Id)>();
            foreach ((string _, Node root) in files)
            {
                if (!(root.TryGetValue(kind, out object s) && s is Node section)) continue;
                foreach (string key in section.Keys.Where(k => id == "*" || k == id).ToList()) found.Add((section, key));
            }
            if (found.Count == 0 && id != "*") Problem(at + ".target", "no " + kind + " named " + pack + ":" + id);
            foreach ((Node section, string key) in found) PatchDefinition(patch, section, key, at);
        }

        private void PatchDefinition(Node patch, Node section, string id, string at)
        {
            if (patch.TryGetValue("replace", out object replacement)) section[id] = Clone(replacement);
            if (!patch.ContainsKey("remove") && !patch.ContainsKey("set") && !patch.ContainsKey("add")) return;
            if (!(section[id] is Node def))
            {
                Problem(at, "this definition isn't an object, so only replace works on it");
                return;
            }
            foreach (KeyValuePair<string, object> edit in Map(patch, "remove"))
            {
                object there = Walk(def, edit.Key, at + ".remove." + edit.Key);
                if (there is List<object> list) list.RemoveAll(e => AsList(edit.Value).Any(m => Matches(e, m)));
                else if (there is Node map)
                {
                    foreach (string key in AsList(edit.Value).OfType<string>()) map.Remove(key);
                }
            }
            foreach (KeyValuePair<string, object> edit in Map(patch, "set")) Assign(def, edit.Key, Clone(edit.Value), at + ".set." + edit.Key);
            foreach (KeyValuePair<string, object> edit in Map(patch, "add"))
            {
                string path = at + ".add." + edit.Key;
                object there = Walk(def, edit.Key, path);
                List<object> list = there as List<object> ?? (there == null ? new List<object>() : new List<object> { there });
                list.AddRange(AsList(edit.Value).Select(Clone));
                Assign(def, edit.Key, list, path);
            }
        }

        private static List<object> AsList(object value) => value as List<object> ?? new List<object> { value };

        // A string matches an entry that is that string, or an object whose id, item, loot or table is it. An object
        // matches an entry that has all of its fields.
        private static bool Matches(object entry, object matcher)
        {
            if (matcher is string name)
            {
                if (entry is string s) return s == name;
                return entry is Node n && new[] { "id", "item", "loot", "table" }.Any(k => Str(n, k) == name);
            }
            if (matcher is Node fields && entry is Node node)
            {
                return fields.All(f => node.TryGetValue(f.Key, out object v) && Equals(v, f.Value));
            }
            return Equals(entry, matcher);
        }

        // The value at a dotted path, or null.
        private object Walk(Node def, string path, string at)
        {
            string[] keys = path.Split('.');
            Node node = def;
            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (!node.TryGetValue(keys[i], out object next) || next == null) return null;
                if (!(next is Node inner))
                {
                    Problem(at, "\"" + keys[i] + "\" isn't an object");
                    return null;
                }
                node = inner;
            }
            return node.TryGetValue(keys[keys.Length - 1], out object found) ? found : null;
        }

        // Sets the value at a dotted path, adding missing objects on the way.
        private void Assign(Node def, string path, object value, string at)
        {
            string[] keys = path.Split('.');
            Node node = def;
            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (!node.TryGetValue(keys[i], out object next) || next == null) node[keys[i]] = next = new Node();
                if (!(next is Node inner))
                {
                    Problem(at, "\"" + keys[i] + "\" isn't an object");
                    return;
                }
                node = inner;
            }
            node[keys[keys.Length - 1]] = value;
        }

        private static object Clone(object value)
        {
            if (value is Node node) return node.ToDictionary(kv => kv.Key, kv => Clone(kv.Value));
            if (value is List<object> list) return list.Select(Clone).ToList();
            return value;
        }

        private void ReadFile(string pack, string file, Func<string, string> read, List<(string, Node)> files, HashSet<string> seen)
        {
            string at = pack + "/" + file;
            if (!seen.Add(file))
            {
                Problem(at, "included more than once");
                return;
            }
            Node root;
            try
            {
                root = Json.Parse(read(file)) as Node;
            }
            catch (Exception ex)
            {
                Problem(at, ex.Message);
                return;
            }
            if (root == null)
            {
                Problem(at, "the file must be a JSON object");
                return;
            }
            foreach (string key in root.Keys)
            {
                if (Array.IndexOf(Sections, key) < 0) Problem(at, "unknown section \"" + key + "\"");
            }
            files.Add((at, root));
            List<object> includes = List(root, "include");
            for (int i = 0; i < includes.Count; i++)
            {
                string included = Include(includes[i], at + ".include[" + i + "]");
                if (included != null) ReadFile(pack, folder + included, read, files, seen);
            }
        }

        /// <summary>
        /// An include is a file name, or {"file": ..., "requires": [mod ids]} for data that only loads alongside other
        /// mods. Null when there's nothing to load.
        /// </summary>
        private string Include(object entry, string at)
        {
            if (entry is string name) return name;
            if (!(entry is Node node))
            {
                Problem(at, "must be a file name or {\"file\", \"requires\"}");
                return null;
            }
            foreach (string key in node.Keys.Where(k => k != "file" && k != "requires")) Problem(at, "unknown key \"" + key + "\" (an include has file and requires)");
            string file = Str(node, "file");
            if (file == null)
            {
                Problem(at, "needs a \"file\"");
                return null;
            }
            List<object> requires = List(node, "requires");
            if (requires.Any(r => !(r is string)))
            {
                Problem(at + ".requires", "must be mod ids");
                return null;
            }
            return requires.All(r => HasMod((string)r)) ? file : null;
        }

        private void Declare(string pack, string at, Node root)
        {
            foreach (KeyValuePair<string, object> b in Map(root, "behaviors")) behaviors[pack + ":" + b.Key] = b.Value;
            foreach (KeyValuePair<string, object> t in Map(root, "templates"))
            {
                if (t.Value is Node node) templates[pack + ":" + t.Key] = node;
                else Problem(at + ".templates." + t.Key, "must be an object");
            }
            foreach (KeyValuePair<string, object> i in Map(root, "interactions"))
            {
                if (i.Value is Node node) sharedInteractions[pack + ":" + i.Key] = node;
                else Problem(at + ".interactions." + i.Key, "must be an object");
            }
            foreach (KeyValuePair<string, object> v in Map(root, "values")) content.Values[pack + ":" + v.Key] = s => 0;
            foreach (KeyValuePair<string, object> e in Map(root, "effects")) DeclareShared(pack, at + ".effects." + e.Key, e.Key, e.Value);
        }

        private readonly List<Action> sharedBodies = new List<Action>();

        /// <summary>A named effect built from other effects. Callers use it exactly like a built-in, by name.</summary>
        private void DeclareShared(string pack, string at, string name, object definition)
        {
            var parameters = new List<KeyValuePair<string, object>>();
            object body = definition;
            if (definition is Node def)
            {
                foreach (string key in def.Keys.Where(k => k != "params" && k != "do")) Problem(at, "unknown key \"" + key + "\" (a shared effect has params and do)");
                parameters = Map(def, "params").ToList();
                body = def.TryGetValue("do", out object inner) ? inner : null;
            }
            var declared = new HashSet<string>();
            foreach (KeyValuePair<string, object> p in parameters.Where(p => p.Key.StartsWith("$", StringComparison.Ordinal)))
            {
                string where = at + ".params." + p.Key;
                if (!TextParamName.IsMatch(p.Key)) { Problem(where, "a text parameter is $ then a-z, 0-9 or _"); continue; }
                declared.Add(p.Key.Substring(1));
                if (!(p.Value is string fallback)) Problem(where, "a text parameter's default is text (\"\" for none)");
                else if (fallback != "" && !TextValue.IsMatch(fallback)) Problem(where, "a text parameter's value is a-z, 0-9 or _ (got \"" + fallback + "\")");
            }
            ScanTextUse(body, declared, at + ".do");
            if (!parameters.Any(p => p.Key.StartsWith("$", StringComparison.Ordinal)))
            {
                var compiled = new Effect[1];
                sharedBodies.Add(() =>
                {
                    current = pack;
                    compiled[0] = Effect(body, pack, at + ".do");
                });
                registry.Effects[pack + ":" + name] = call => CompileSharedCall((Spec)call, name, parameters, compiled, pack, at);
                return;
            }
            // Compiled per call site, not once like a numeric-only effect: the text a caller passes becomes part of the body's
            // JSON, so each call is a different body. Rejected: doing this for every shared effect, which would multiply
            // compile cost and change the path every numeric effect takes today.
            uncalledText[pack + ":" + name] = (pack, at);
            registry.Effects[pack + ":" + name] = call => CompileSharedCall((Spec)call, name, parameters, null, pack, at, body);
        }

        /// <summary>
        /// Reports misuse of {arg.$NAME} in a shared effect's body: a name no parameter declares, a bare arg.$NAME (an
        /// expression would read it as a number) and any use in a key. Values only, never keys: filling a key could make
        /// two keys collide without a word. Rejected: free text in the placeholder, because {arg.$x} could then put
        /// expression syntax into a value field; the a-z, 0-9, _ rule on the filled text prevents that.
        /// </summary>
        private void ScanTextUse(object node, ISet<string> declared, string at)
        {
            switch (node)
            {
                case Node map:
                    foreach (KeyValuePair<string, object> entry in map)
                    {
                        if (entry.Key.Contains("{arg.$")) Problem(at + "." + entry.Key, "{arg.$name} fills in values, not keys");
                        ScanTextUse(entry.Value, declared, at + "." + entry.Key);
                    }
                    break;
                case List<object> list:
                    for (int i = 0; i < list.Count; i++) ScanTextUse(list[i], declared, at + "[" + i + "]");
                    break;
                case string text:
                    foreach (Match m in Placeholder.Matches(text))
                    {
                        if (!declared.Contains(m.Groups[1].Value)) Problem(at, "no text parameter $" + m.Groups[1].Value);
                    }
                    Match bare = BareTextArg.Match(Placeholder.Replace(text, ""));
                    if (bare.Success) Problem(at, "write {arg.$" + (bare.Groups[1].Value == "" ? "x" : bare.Groups[1].Value) + "}: a text parameter is filled in as text, not read as a number");
                    break;
            }
        }

        /// <summary>A copy of a shared effect's body with each {arg.$NAME} replaced by the call's text. Every call site reuses the body, so it is copied, never edited in place.</summary>
        private static object Fill(object node, Dictionary<string, string> args)
        {
            switch (node)
            {
                case Node map:
                    var copy = new Node();
                    foreach (KeyValuePair<string, object> entry in map) copy[entry.Key] = Fill(entry.Value, args);
                    return copy;
                case List<object> list:
                    return list.Select(item => Fill(item, args)).ToList();
                case string text:
                    // A name no parameter declares was reported when the effect was declared; leaving it unfilled avoids a second, confusing error.
                    return Placeholder.Replace(text, m => args.TryGetValue(m.Groups[1].Value, out string v) ? v : m.Value);
                default:
                    return node;
            }
        }

        private Effect CompileSharedCall(Spec call, string name, List<KeyValuePair<string, object>> parameters, Effect[] body, string pack, string at, object rawBody = null)
        {
            // First, before any validation that can return: a call with a bad argument is reported at the call and must not also
            // draw "never called". Moving this below the checks would double-report every misused text-param effect.
            if (rawBody != null) uncalledText.Remove(pack + ":" + name);
            // Every argument is checked against the declared params just below, and an unknown one is a problem there.
            call.MarkAllRead();
            var known = new HashSet<string>(parameters.Select(p => p.Key));
            bool bare = call.Has("value") && !known.Contains("value");
            if (bare && parameters.Count == 0) call.Error(name + " takes no arguments");
            foreach (string key in call.Args.Keys.Where(k => k != "value" && !known.Contains(k))) call.Error("\"" + name + "\" has no parameter \"" + key + "\"");
            var values = new List<KeyValuePair<string, Value>>();
            var textArgs = new Dictionary<string, string>();
            for (int i = 0; i < parameters.Count; i++)
            {
                KeyValuePair<string, object> p = parameters[i];
                if (p.Key.StartsWith("$", StringComparison.Ordinal))
                {
                    if (!TextParamName.IsMatch(p.Key)) continue;   // reported when declared
                    bool given = call.Has(p.Key) || (bare && i == 0);
                    object source = call.Has(p.Key) ? call.Args[p.Key] : bare && i == 0 ? call.Args["value"] : p.Value;
                    // Only what the caller wrote is checked; a bad default was reported once, when the effect was declared.
                    string text = source as string ?? (given ? null : "");
                    if (text == null) { call.Error("\"" + p.Key + "\" is text: give a name like lower_level, not a number"); return s => { }; }
                    if (text == "") { call.Error(name + " needs " + p.Key); return s => { }; }
                    // Filling the bad default would add a second, confusing error at the call site on top of the one at the declaration.
                    if (!given && !TextValue.IsMatch(text)) return s => { };
                    if (given && !TextValue.IsMatch(text)) { call.Error("\"" + p.Key + "\" must be a-z, 0-9 or _ (got \"" + text + "\")"); return s => { }; }
                    textArgs[p.Key.Substring(1)] = text;
                    continue;
                }
                Value value;
                if (call.Has(p.Key)) value = call.Value(p.Key, 0);
                else if (bare && i == 0) value = call.Value("value", 0);
                else value = ValueOf(p.Value, pack, at + ".params." + p.Key);
                values.Add(new KeyValuePair<string, Value>(p.Key, value));
            }
            if (rawBody != null)
            {
                string qualified = pack + ":" + name;
                if (!expanding.Add(qualified)) { call.Error(name + " calls itself through its text parameters"); return s => { }; }
                try { body = new[] { Effect(Fill(rawBody, textArgs), pack, call.Path + ".do") }; }
                finally { expanding.Remove(qualified); }
            }
            return s =>
            {
                var run = (ExpeditionRun)s.Run;
                if (!run.EnterShared(name)) return;
                try
                {
                    var args = new Dictionary<string, int>();
                    foreach (KeyValuePair<string, Value> v in values) args[v.Key] = v.Value(s);
                    body[0](Scope.From(s).WithArgs(args));
                }
                finally
                {
                    // Without this, every exception that passes through leaves the run one level closer to refusing all shared effects.
                    run.LeaveShared();
                }
            };
        }

        private void CompileFile(string pack, string at, Node root)
        {
            foreach (KeyValuePair<string, object> group in Map(root, "item_groups"))
            {
                foreach (object item in group.Value as List<object> ?? new List<object>())
                {
                    string id = item as string;
                    if (id == null) { Problem(at + ".item_groups." + group.Key, "item ids must be strings"); continue; }
                    if (!content.ItemGroups.TryGetValue(id, out HashSet<string> groups)) content.ItemGroups[id] = groups = new HashSet<string>();
                    groups.Add(group.Key);
                }
            }
            foreach (KeyValuePair<string, object> entry in Map(root, "item_stats")) CompileItemStats(entry.Key, entry.Value, at + ".item_stats." + entry.Key);
            foreach (KeyValuePair<string, object> v in Map(root, "values"))
            {
                string id = pack + ":" + v.Key;
                compilingValue = id;
                content.Values[id] = ValueOf(v.Value, pack, at + ".values." + v.Key);
                compilingValue = null;
            }
            foreach (Action body in sharedBodies) body();
            sharedBodies.Clear();
            current = pack;
            Section(root, "statuses", at, pack, content.Statuses, CompileStatus);
            Section(root, "intents", at, pack, content.Intents, CompileIntent);
            Section(root, "loot", at, pack, content.Loot, CompileLoot);
            Section(root, "carts", at, pack, content.Carts, CompileCart);
            Section(root, "enemies", at, pack, content.Enemies, CompileEnemy);
            Section(root, "encounters", at, pack, content.Encounters, CompileEncounter);
            Section(root, "events", at, pack, content.Events, CompileEvent);
            Section(root, "rooms", at, pack, content.Rooms, CompileRoom);
            Section(root, "kits", at, pack, content.Kits, CompileKit);
            Section(root, "areas", at, pack, content.Areas, CompileArea);
            if (root.ContainsKey("debug_kit")) content.DebugKits[pack] = List(root, "debug_kit").OfType<string>().ToList();
        }

        private void CompileItemStats(string key, object value, string at)
        {
            if (!(value is Node node))
            {
                Problem(at, "must be an object of tags and numbers");
                return;
            }
            if (!content.ItemStats.TryGetValue(key, out ItemStats stats)) content.ItemStats[key] = stats = new ItemStats();
            foreach (KeyValuePair<string, object> field in node)
            {
                if (field.Key == "tags") stats.Tags.UnionWith(List(node, "tags").OfType<string>());
                else if (field.Value is double d) stats.Props[field.Key] = (int)d;
                else if (field.Value is bool b) stats.Props[field.Key] = b ? 1 : 0;
                else Problem(at + "." + field.Key, "must be a number");
            }
        }

        /// <summary>Checks everything that refers to something else. Call once, after every pack is loaded.</summary>
        public void Finish()
        {
            foreach (Action check in checks) check();
            checks.Clear();
            foreach ((string pool, string kind, string at, string pack) in poolUses)
            {
                current = pack;
                CheckPool(pool, kind, at);
            }
            poolUses.Clear();
            foreach (string value in valueUses.Keys.ToList())
            {
                current = PackOf(value);
                CheckValueCycle(value);
            }
            valueUses.Clear();
            foreach (KeyValuePair<(string Reader, string Owner, string Name), (string At, string Path)> read in memoryReads)
            {
                (string reader, string owner, string name) = read.Key;
                (string at, string path) = read.Value;
                current = reader;
                string empty = name.StartsWith("$", StringComparison.Ordinal) ? "\"\"" : "0";
                // Warnings, not problems: the pack still runs, the name just never holds anything. A missing pack is a
                // warning too, so reading another pack's memory is optional: failing the load would make the reading pack
                // unusable without that one installed.
                if (!packs.Contains(owner))
                {
                    if (OtherPacks == null || !OtherPacks.Contains(owner)) Warn(at, path + " reads pack " + owner + ", which isn't loaded, so it reads " + empty);
                    continue;
                }
                if (memoryWrites.Contains((owner, name))) continue;
                Warn(at, path + " is read but never written by " + (owner == reader ? "this pack" : "pack " + owner) + ", so it always reads " + empty);
            }
            memoryReads.Clear();
            memoryWrites.Clear();
            foreach (KeyValuePair<(string Reader, string Name), (string At, string Path)> read in hitReads)
            {
                if (hitSets.Contains(read.Key.Name)) continue;
                // Names are shared by every loaded pack, so with other packs named in OtherPacks (ef-sim lint --mod) the
                // setter may be in one of them and the read can't be judged here. Warning anyway would make a kit that reads
                // another pack's hit name fail lint --strict however it is run.
                if (OtherPacks != null && OtherPacks.Count > 0) continue;
                current = read.Key.Reader;
                // A warning, as with memory: the pack still runs; the name just always reads 0.
                Warn(read.Value.At, read.Value.Path + " is read but no damage in a loaded pack sets it in its \"hit\", so it always reads 0");
            }
            hitReads.Clear();
            hitSets.Clear();
            foreach ((string pack, string at) in uncalledText.Values)
            {
                current = pack;
                Warn(at, "never called, so its body is not checked (text-parameter effects compile at each call)");
            }
            uncalledText.Clear();
            knownItemTags = null;
        }

        private void CheckPool(string pool, string kind, string at)
        {
            List<AreaDef> using_ = content.Areas.Values.Where(a => a.Pools.ContainsKey(pool)).ToList();
            if (using_.Count == 0) Problem(at, "no area has a pool named " + pool);
            foreach (AreaDef area in using_)
            {
                foreach (string id in area.Pools[pool])
                {
                    if (!Exists(kind, id)) Problem(area.Id + ".pools." + pool, "no " + kind + " named " + id);
                }
            }
        }

        private void CheckValueCycle(string start)
        {
            var seen = new HashSet<string>();
            var open = new Stack<string>(valueUses[start]);
            while (open.Count > 0)
            {
                string next = open.Pop();
                if (next == start)
                {
                    Problem(start, "this value depends on itself");
                    content.Values[start] = s => 0;
                    return;
                }
                if (seen.Add(next) && valueUses.TryGetValue(next, out HashSet<string> further))
                {
                    foreach (string f in further) open.Push(f);
                }
            }
        }

        internal void Problem(string at, string message)
        {
            string problem = at + ": " + message;
            content.Problems.Add(problem);
            if (!problemsByPack.TryGetValue(current, out List<string> list)) problemsByPack[current] = list = new List<string>();
            list.Add(problem);
        }

        internal void Warn(string at, string message)
        {
            string warning = at + ": " + message;
            content.Warnings.Add(warning);
            if (!warningsByPack.TryGetValue(current, out List<string> list)) warningsByPack[current] = list = new List<string>();
            list.Add(warning);
        }

        /// <summary>
        /// Notes that <paramref name="pack"/> reads or writes <paramref name="path"/>. Only memory.NAME and memory.PACK.NAME count,
        /// with or without $; the $ stays in the name, as text and number memory are separate values.
        /// </summary>
        internal void NoteMemory(string pack, string path, string at, bool write)
        {
            const string head = "memory.";
            if (path == null || !path.StartsWith(head, StringComparison.Ordinal)) return;
            if (!Expr.MemoryKey(path.Substring(head.Length), pack, out string owner, out string name)) return;
            if (write) memoryWrites.Add((owner, name));
            else if (!memoryReads.ContainsKey((pack, owner, name))) memoryReads[(pack, owner, name)] = (at, path);
        }

        // Unknown keys and unknown tones are warnings; a value of the wrong type is a problem. The reads that follow keep their
        // old fallbacks, so a reported mistake never also stops the compile.
        private void CheckShape(Node n, Shape shape, string at)
        {
            foreach (KeyValuePair<string, object> field in n)
            {
                string path = at + "." + field.Key;
                if (!shape.Fields.TryGetValue(field.Key, out Field type))
                {
                    Warn(path, "unknown key, ignored (known: " + string.Join(", ", shape.Fields.Keys) + ")");
                    continue;
                }
                if (field.Value == null) continue;
                string wrong = Schema.Mismatch(type, field.Value);
                if (wrong != null) Problem(path, wrong);
                else if (type == Field.Tone && Array.IndexOf(Schema.Tones, (string)field.Value) < 0) Warn(path, UnknownTone((string)field.Value, Schema.Tones));
                else if (type == Field.IntelTone && Array.IndexOf(Schema.IntelTones, (string)field.Value) < 0) Warn(path, UnknownTone((string)field.Value, Schema.IntelTones));
            }
        }

        internal static string UnknownTone(string tone, string[] known) => "unknown tone \"" + tone + "\" (known: " + string.Join(", ", known) + ")";

        // A check that runs at Finish, charged to the pack being compiled now.
        private void Defer(Action check)
        {
            string pack = current;
            checks.Add(() =>
            {
                current = pack;
                check();
            });
        }

        private static Node Map(Node node, string key) => node.TryGetValue(key, out object v) && v is Node map ? map : new Node();

        private static List<object> List(Node node, string key)
        {
            if (!node.TryGetValue(key, out object v) || v == null) return new List<object>();
            return v as List<object> ?? new List<object> { v };
        }

        internal static string Str(Node node, string key, string fallback = null) => node.TryGetValue(key, out object v) && v is string s ? s : fallback;

        internal static bool Bool(Node node, string key, bool fallback) => node.TryGetValue(key, out object v) && v is bool b ? b : fallback;

        internal static int Int(Node node, string key, int fallback) => node.TryGetValue(key, out object v) && v is double d ? (int)d : fallback;

        private static string Qualify(string pack, string id) => id.Contains(":") ? id : pack + ":" + id;

        private static string PackOf(string qualified) => qualified.Substring(0, qualified.IndexOf(':'));

        /// <summary>The node with what it extends filled in underneath. Keys it sets replace the template's.</summary>
        private Node Extend(Node node, string pack, string at, int depth = 0)
        {
            string name = Str(node, "extends");
            if (name == null) return node;
            if (depth > 16)
            {
                Problem(at, "templates extend each other in a circle");
                return node;
            }
            string key = Qualify(pack, name);
            if (!templates.TryGetValue(key, out Node template))
            {
                Problem(at + ".extends", "no template named " + key);
                return node;
            }
            var merged = new Node(Extend(template, PackOf(key), at, depth + 1));
            foreach (KeyValuePair<string, object> field in node) merged[field.Key] = field.Value;
            merged.Remove("extends");
            return merged;
        }

        private void Section<T>(Node root, string key, string at, string pack, Dictionary<string, T> into, Action<T, Node, string> fill) where T : Def, new()
        {
            foreach (KeyValuePair<string, object> entry in Map(root, key))
            {
                string path = at + "." + key + "." + entry.Key;
                string id = pack + ":" + entry.Key;
                if (into.ContainsKey(id)) { Problem(path, "this id is already defined"); continue; }
                if (!(entry.Value is Node raw)) { Problem(path, "must be an object"); continue; }
                Node node = Extend(raw, pack, path);
                CheckShape(node, Schema.Sections[key], path);
                var def = new T { Id = id, Pack = pack };
                def.Weight = Int(node, "weight", 1);
                def.Tags = List(node, "tags").OfType<string>().ToArray();
                if (node.ContainsKey("when")) def.When = Condition(node["when"], pack, path + ".when");
                fill(def, node, path);
                into[id] = def;
            }
        }

        // ---- Content types ----

        private void CompileStatus(StatusDef d, Node n, string at)
        {
            d.Name = Text(n, "name", d.Pack, at, required: true);
            d.Turns = Int(n, "turns", -1);
            d.Rooms = Int(n, "rooms", -1);
            d.Intent = RefOrNull(n, "intent", "intent", d.Pack, at);
            foreach (KeyValuePair<string, object> stat in Map(n, "stats"))
            {
                if (stat.Value is double value) d.Stats[stat.Key] = (int)value;
                else Problem(at + ".stats." + stat.Key, "must be a number");
            }
            d.Interactions = Interactions(n, "interactions", d.Pack, at, "combat");
            d.Hooks = CompileHooks(n, d.Pack, at);
        }

        private void CompileIntent(IntentDef d, Node n, string at)
        {
            d.Label = LineOf(n, "label", d.Pack, at, required: true);
            d.Tell = LineOf(n, "tell", d.Pack, at, required: false);
            d.Tone = Str(n, "tone", "enemy");
            d.Interruptible = Bool(n, "interruptible", false);
            d.Next = RefOrNull(n, "next", "intent", d.Pack, at);
            d.Do = Effect(n.TryGetValue("do", out object body) ? body : null, d.Pack, at + ".do");
        }

        private void CompileLoot(LootDef d, Node n, string at)
        {
            foreach (object e in List(n, "items"))
            {
                if (e is string id) d.Entries.Add(new LootEntry { Item = id });
                else if (e is Node entry) AddLootEntry(d, entry, at + ".items");
                else Problem(at + ".items", "each entry is an item id, or {item|loot|table, weight, when}");
            }
            if (d.Entries.Count == 0) Problem(at, "a loot table needs items");
        }

        private void AddLootEntry(LootDef d, Node entry, string at)
        {
            CheckShape(entry, Schema.LootEntry, at);
            var result = new LootEntry { Item = Str(entry, "item"), GameTable = Str(entry, "table"), Weight = Int(entry, "weight", 1) };
            if (Str(entry, "loot") != null) result.Loot = Ref(Str(entry, "loot"), "loot", d.Pack, at);
            if (entry.ContainsKey("when")) result.When = Condition(entry["when"], d.Pack, at + ".when");
            if (result.Item == null && result.Loot == null && result.GameTable == null) Problem(at, "each entry names an item, loot or table");
            d.Entries.Add(result);
        }

        private void CompileCart(CartDef d, Node n, string at)
        {
            d.Name = Text(n, "name", d.Pack, at, required: true);
            d.Budget = n.ContainsKey("budget") ? ValueOf(n["budget"], d.Pack, at + ".budget") : (s => 0);
            int index = 0;
            foreach (object e in List(n, "stock"))
            {
                string path = at + ".stock[" + index++ + "]";
                if (!(e is Node entry)) { Problem(path, "must be an object"); continue; }
                CheckShape(entry, Schema.CartLine, path);
                var line = new CartLine
                {
                    Item = Str(entry, "item"),
                    Index = entry.ContainsKey("index") ? ValueOf(entry["index"], d.Pack, path + ".index") : (s => 0),
                    Count = entry.ContainsKey("count") ? ValueOf(entry["count"], d.Pack, path + ".count") : (s => 1),
                };
                if (line.Item == null)
                {
                    line.Loot = List(entry, "loot").OfType<string>().Select(l => Ref(l, "loot", d.Pack, path)).ToList();
                    if (line.Loot.Count == 0) Problem(path, "needs item or loot");
                }
                d.Stock.Add(line);
            }
        }

        private void CompileEnemy(EnemyDef d, Node n, string at)
        {
            d.Name = Text(n, "name", d.Pack, at, required: true);
            d.Stats = Stats(n, "stats", d.Pack, at);
            if (!d.Stats.ContainsKey("hp")) Problem(at + ".stats", "needs hp");
            foreach (KeyValuePair<string, object> text in Map(n, "texts")) if (text.Value is string s) d.Texts[text.Key] = s;
            d.Opening = RefOrNull(n, "opening", "intent", d.Pack, at);
            d.Drops = RefOrNull(n, "drops", "loot", d.Pack, at);
            object behavior = n.TryGetValue("behavior", out object b) ? b : null;
            d.Behavior = BehaviorList(behavior, d.Pack, at + ".behavior", new HashSet<string>());
            if (d.Behavior.Count == 0) Problem(at + ".behavior", "an enemy needs at least one intent");
            d.Interactions = Interactions(n, "interactions", d.Pack, at, "combat");
            d.Hooks = CompileHooks(n, d.Pack, at);
        }

        /// <summary>A list of entries, or the name of a shared list. A shared list's own ids belong to the pack that wrote it.</summary>
        private List<BehaviorEntry> BehaviorList(object behavior, string pack, string at, HashSet<string> open)
        {
            string owner = pack;
            string key = null;
            if (behavior is string shared)
            {
                key = Qualify(pack, shared);
                owner = PackOf(key);
                if (!open.Add(key))
                {
                    Problem(at, "behaviors include each other in a circle");
                    return new List<BehaviorEntry>();
                }
                if (!behaviors.TryGetValue(key, out behavior))
                {
                    Problem(at, "no behavior named " + key);
                    open.Remove(key);
                    return new List<BehaviorEntry>();
                }
            }
            var result = new List<BehaviorEntry>();
            int index = 0;
            foreach (object e in behavior as List<object> ?? new List<object>())
            {
                string path = at + "[" + index++ + "]";
                if (!(e is Node entry)) { Problem(path, "must be an object"); continue; }
                CheckShape(entry, Schema.BehaviorEntry, path);
                var item = new BehaviorEntry
                {
                    Weight = Int(entry, "weight", 1),
                    When = entry.ContainsKey("when") ? Condition(entry["when"], owner, path + ".when") : null,
                };
                if (Str(entry, "intent") != null) item.Intent = Ref(Str(entry, "intent"), "intent", owner, path);
                else if (entry.ContainsKey("behavior")) item.Nested = BehaviorList(entry["behavior"], owner, path + ".behavior", open);
                else { Problem(path, "needs intent or behavior"); continue; }
                if (item.Nested != null && item.Nested.Count == 0) continue;
                result.Add(item);
            }
            if (key != null) open.Remove(key);
            return result;
        }

        private void CompileEncounter(EncounterDef d, Node n, string at)
        {
            foreach (object e in List(n, "enemies"))
            {
                if (e is string id) d.Enemies.Add(Ref(id, "enemy", d.Pack, at + ".enemies"));
                else if (e is Node entry && Str(entry, "id") != null)
                {
                    CheckShape(entry, Schema.EncounterEntry, at + ".enemies");
                    string enemy = Ref(Str(entry, "id"), "enemy", d.Pack, at + ".enemies");
                    for (int i = Int(entry, "count", 1); i > 0; i--) d.Enemies.Add(enemy);
                }
                else Problem(at + ".enemies", "each entry is an enemy id, or {id, count}");
            }
            if (d.Enemies.Count == 0) Problem(at, "an encounter needs enemies");
            d.Hooks = CompileHooks(n, d.Pack, at);
        }

        private void CompileEvent(EventDef d, Node n, string at)
        {
            d.Text = LineOf(n, "text", d.Pack, at, required: false);
            d.Scene = Str(n, "scene", "event");
            d.Enter = n.TryGetValue("enter", out object enter) ? Effect(enter, d.Pack, at + ".enter") : null;
            d.Choices = Interactions(n, "choices", d.Pack, at, d.Scene);
            if (d.Choices.Count == 0 && d.Enter == null) Problem(at + ".choices", "an event needs a choice, or something to do on entering");
        }

        private void CompileRoom(RoomDef d, Node n, string at)
        {
            d.Text = LineOf(n, "text", d.Pack, at, required: false);
            d.Enter = n.TryGetValue("enter", out object enter) ? Effect(enter, d.Pack, at + ".enter") : null;
            d.Interactions = Interactions(n, "interactions", d.Pack, at, "room");
            d.Hooks = CompileHooks(n, d.Pack, at);
        }

        private void CompileKit(KitDef d, Node n, string at)
        {
            d.Interactions = Interactions(n, "interactions", d.Pack, at, "room");
            d.Hooks = CompileHooks(n, d.Pack, at);
        }

        private void CompileArea(AreaDef d, Node n, string at)
        {
            d.Title = Text(n, "title", d.Pack, at, required: true);
            d.UnlockedBy = RefOrNull(n, "unlocked_by", "area", d.Pack, at);
            d.Ambience = Str(n, "ambience");
            d.Art = Str(n, "art");
            if (n.ContainsKey("hidden_when")) d.HiddenWhen = Condition(n["hidden_when"], d.Pack, at + ".hidden_when");
            d.Player = Stats(n, "player", d.Pack, at);
            if (!d.Player.ContainsKey("hp")) Problem(at + ".player", "needs hp");
            d.Vars = Stats(n, "vars", d.Pack, at);
            foreach (string name in d.Vars.Keys)
            {
                if (Array.IndexOf(ExpeditionRun.EngineVars, name) >= 0) Problem(at + ".vars." + name, "run." + name + " is kept by the engine; vars can't set it");
            }
            foreach (KeyValuePair<string, object> pool in Map(n, "pools"))
            {
                // Which kind of thing a pool holds depends on who uses it, so its ids are checked per use.
                d.Pools[pool.Key] = (pool.Value as List<object> ?? new List<object> { pool.Value }).OfType<string>().Select(id => Qualify(d.Pack, id)).ToList();
            }
            d.Entrance = RefOrNull(n, "entrance", "room", d.Pack, at);
            foreach (object kit in List(n, "kits")) if (kit is string id) d.Kits.Add(Ref(id, "kit", d.Pack, at + ".kits"));
            if (n.ContainsKey("groups")) d.GroupOrder = List(n, "groups").OfType<string>().ToList();
            if (n.TryGetValue("luck", out object luck) && luck is Node l)
            {
                CheckShape(l, Schema.Luck, at + ".luck");
                d.Luck = new LuckRule { Percent = ValueOf(l.TryGetValue("value", out object lv) ? lv : 0d, d.Pack, at + ".luck.value"), Low = Int(l, "low", 5), High = Int(l, "high", 16) };
            }
            foreach (KeyValuePair<string, object> text in Map(n, "texts"))
            {
                if (text.Value is string key) d.Texts[text.Key] = TextKey(key, d.Pack, at + ".texts." + text.Key);
                else Problem(at + ".texts." + text.Key, "must be a text key");
            }
            if (n.TryGetValue("packing", out object packing) && packing is Node p) d.Packing = CompilePacking(p, d.Pack, at + ".packing");
            int intelIndex = 0;
            foreach (object e in List(n, "intel"))
            {
                string path = at + ".intel[" + intelIndex++ + "]";
                if (e is Node slot) d.Intel.Add(CompileIntel(slot, d, path));
                else Problem(path, "must be an object");
            }
            if (n.TryGetValue("hud", out object hud) && hud is Node h) d.Hud = CompileHud(h, d.Pack, at + ".hud");
            d.Interactions = Interactions(n, "interactions", d.Pack, at, "room");
            d.Hooks = CompileHooks(n, d.Pack, at);
            int index = 0;
            foreach (object s in List(n, "layout"))
            {
                string path = at + ".layout[" + index++ + "]";
                if (!(s is Node slot)) { Problem(path, "must be an object"); continue; }
                CheckShape(slot, Schema.LayoutSlot, path);
                d.Layout.Add(new LayoutSlot
                {
                    Pool = RoomPool(slot, d.Pack, path),
                    Count = slot.ContainsKey("count") ? ValueOf(slot["count"], d.Pack, path + ".count") : (s2 => 1),
                    Unique = Bool(slot, "unique", true),
                });
            }
            if (d.Layout.Count == 0) Problem(at + ".layout", "an area needs a layout");
        }

        private PackingDef CompilePacking(Node n, string pack, string at)
        {
            CheckShape(n, Schema.Packing, at);
            var result = new PackingDef { NoBackpack = Text(n, "no_backpack", pack, at, required: false), Card = Text(n, "card", pack, at, required: false) };
            int index = 0;
            foreach (object e in List(n, "needs"))
            {
                string path = at + ".needs[" + index++ + "]";
                if (!(e is Node need)) { Problem(path, "must be an object"); continue; }
                CheckShape(need, Schema.PackingNeed, path);
                result.Needs.Add(new PackingNeed
                {
                    Items = Items(need.TryGetValue("items", out object items) ? items : null, pack, path + ".items"),
                    Missing = Text(need, "missing", pack, path, required: true),
                });
            }
            return result;
        }

        private IntelSlot CompileIntel(Node n, AreaDef area, string at)
        {
            string pack = area.Pack;
            CheckShape(n, Schema.Intel, at);
            var slot = new IntelSlot
            {
                Id = Str(n, "id"),
                Pool = Str(n, "pool"),
                Chance = Int(n, "chance", 100),
                Title = LineOf(n, "title", pack, at, required: true),
                Description = LineOf(n, "description", pack, at, required: true),
                Effect = LineOf(n, "effect", pack, at, required: false),
                Tone = Str(n, "tone", "mixed"),
            };
            if (slot.Id == null) Problem(at, "needs \"id\"");
            if (slot.Pool == null || !area.Pools.ContainsKey(slot.Pool)) Problem(at + ".pool", "must name one of this area's pools");
            if (n.ContainsKey("counts")) slot.Counts = List(n, "counts").OfType<double>().Select(c => (int)c).ToArray();
            slot.GoodPicks.UnionWith(List(n, "good").OfType<string>().Select(id => Qualify(pack, id)));
            if (n.ContainsKey("when")) slot.When = Condition(n["when"], pack, at + ".when");
            return slot;
        }

        private HudDef CompileHud(Node n, string pack, string at)
        {
            CheckShape(n, Schema.Hud, at);
            var hud = new HudDef { Title = LineOf(n, "title", pack, at, required: false) };
            int index = 0;
            foreach (object e in List(n, "rows"))
            {
                string path = at + ".rows[" + index++ + "]";
                if (!(e is Node row)) { Problem(path, "must be an object"); continue; }
                CheckShape(row, Schema.HudRow, path);
                var compiled = new HudRow { Separator = Bool(row, "separator", false) };
                if (row.ContainsKey("when")) compiled.When = Condition(row["when"], pack, path + ".when");
                int partIndex = 0;
                foreach (object part in List(row, "parts")) compiled.Parts.Add(CompileHudPart(part, pack, path + ".parts[" + partIndex++ + "]"));
                hud.Rows.Add(compiled);
            }
            Node enemies = Map(n, "enemies");
            if (n.ContainsKey("enemies")) CheckShape(enemies, Schema.HudEnemies, at + ".enemies");
            hud.Enemy = LineOf(enemies, "line", pack, at + ".enemies", required: true);
            hud.Pending = LineOf(enemies, "pending", pack, at + ".enemies", required: false) ?? hud.Enemy;
            hud.Clear = LineOf(enemies, "none", pack, at + ".enemies", required: false);
            hud.PendingScenes.UnionWith(List(enemies, "pending_scenes").OfType<string>());
            return hud;
        }

        private HudPart CompileHudPart(object part, string pack, string at)
        {
            var result = new HudPart { Line = LineFrom(part, pack, at, Schema.HudPart) };
            if (part is Node node)
            {
                if (node.ContainsKey("when")) result.When = Condition(node["when"], pack, at + ".when");
                if (node.ContainsKey("each_item")) result.EachItem = UseOf(node["each_item"], pack, at + ".each_item", checkShape: true);
            }
            return result;
        }

        private Func<IRun, List<RoomDef>> RoomPool(Node slot, string pack, string at)
        {
            if (Str(slot, "room") != null)
            {
                string id = Ref(Str(slot, "room"), "room", pack, at);
                return run => new List<RoomDef> { content.Rooms[id] };
            }
            return Pool(slot, "rooms", "tag", content.Rooms, "room", pack, at);
        }

        /// <summary>Candidates named by id list ("from"), by tag, or by one of the area's pools.</summary>
        internal Func<IRun, List<T>> Pool<T>(Node node, string idsKey, string tagKey, Dictionary<string, T> table, string kind, string pack, string at) where T : Def
        {
            string pool = Str(node, "pool");
            if (pool != null)
            {
                poolUses.Add((pool, kind, at, current));
                return run => run.Pool(pool).Select(i => table[i]).ToList();
            }
            List<string> named = List(node, "from").Concat(List(node, idsKey)).OfType<string>().Select(i => Ref(i, kind, pack, at)).ToList();
            string tag = Str(node, tagKey);
            if (named.Count == 0 && tag == null) Problem(at, "name candidates with \"from\", \"tag\" or \"pool\"");
            if (tag != null)
            {
                Defer(() => { if (!table.Values.Any(d => d.Tags.Contains(tag))) Problem(at, "no " + kind + " has the tag " + tag); });
            }
            return run => (tag != null ? table.Values.Where(d => d.Tags.Contains(tag)) : named.Select(i => table[i])).ToList();
        }

        // ---- Shared pieces ----

        private Dictionary<string, Value> Stats(Node n, string key, string pack, string at)
        {
            var stats = new Dictionary<string, Value>();
            foreach (KeyValuePair<string, object> s in Map(n, key)) stats[s.Key] = ValueOf(s.Value, pack, at + "." + key + "." + s.Key);
            return stats;
        }

        private Hooks CompileHooks(Node n, string pack, string at)
        {
            var hooks = new Hooks();
            foreach (KeyValuePair<string, object> h in Map(n, "on"))
            {
                string hook = registry.ResolveHook(pack, h.Key);
                if (hook == null || !registry.IsHook(hook))
                {
                    Problem(at + ".on." + h.Key, "unknown hook (known: " + string.Join(", ", ExpeditionRun.HookNames) + ")");
                    continue;
                }
                hooks.On[hook] = Effect(h.Value, pack, at + ".on." + h.Key);
            }
            return hooks;
        }

        private List<Interaction> Interactions(Node n, string key, string pack, string at, string defaultScene)
        {
            var result = new List<Interaction>();
            int index = 0;
            foreach (object e in List(n, key))
            {
                string path = at + "." + key + "[" + index++ + "]";
                if (!(e is Node node))
                {
                    Problem(path, "must be an object");
                    continue;
                }
                string use = Str(node, "use");
                if (use == null)
                {
                    result.Add(CompileInteraction(node, pack, path, defaultScene));
                    continue;
                }
                string shared = Qualify(pack, use);
                if (!sharedInteractions.TryGetValue(shared, out Node template))
                {
                    Problem(path + ".use", "no interaction named " + shared);
                    continue;
                }
                var merged = new Node(template);
                foreach (KeyValuePair<string, object> field in node) if (field.Key != "use") merged[field.Key] = field.Value;
                result.Add(CompileInteraction(merged, PackOf(shared), path, defaultScene));
            }
            return result;
        }

        // checkShape is false when the use is an effect's own arguments: those keys are the effect's, and its spec reports them.
        internal ItemUse UseOf(object node, string pack, string at, bool checkShape)
        {
            if (!(node is Node u) || !u.ContainsKey("select")) return new ItemUse { Filter = Items(node, pack, at) };
            if (checkShape) CheckShape(u, Schema.Use, at);
            var use = new ItemUse
            {
                Filter = Items(u["select"], pack, at + ".select"),
                Each = Bool(u, "each", true),
                Consume = Int(u, "consume", 0),
                Limit = Int(u, "limit", 0),
            };
            if (u.ContainsKey("order")) use.Order = ValueOf(u["order"], pack, at + ".order");
            if (u.ContainsKey("where")) use.Where = Condition(u["where"], pack, at + ".where");
            return use;
        }

        private Interaction CompileInteraction(Node n, string pack, string at, string defaultScene)
        {
            CheckShape(n, Schema.Interaction, at);
            var i = new Interaction
            {
                Id = Str(n, "id") ?? at,
                Label = LineOf(n, "label", pack, at, required: true),
                Group = Str(n, "group", "move"),
                In = new HashSet<string>(List(n, "in").OfType<string>().DefaultIfEmpty(defaultScene)),
                Targets = Str(n, "targets"),
                TakesTurn = Bool(n, "turn", true),
                Once = Bool(n, "once", false),
                Stay = Bool(n, "stay", false),
                Sound = Str(n, "sound"),
                SoundElse = Str(n, "sound_else"),
            };
            Defer(() =>
            {
                foreach (string scene in i.In.Where(s => !IsScene(s))) Problem(at + ".in", "unknown scene " + scene + " (room, event, combat, or a scene an event declares)");
            });
            if (i.Targets != null && i.Targets != "enemy" && i.Targets != "focus") Problem(at + ".targets", "must be \"enemy\" or \"focus\"; leave it out to act on the owner");
            if (n.ContainsKey("when")) i.When = Condition(n["when"], pack, at + ".when");
            if (n.ContainsKey("where")) i.Where = Condition(n["where"], pack, at + ".where");
            if (n.TryGetValue("uses", out object uses))
            {
                i.Uses = UseOf(uses, pack, at + ".uses", checkShape: true);
                if (uses is Node u && u.ContainsKey("with")) i.With = UseOf(u["with"], pack, at + ".uses.with", checkShape: true);
            }
            if (n.TryGetValue("confirm", out object confirm) && confirm is Node c)
            {
                CheckShape(c, Schema.Confirm, at + ".confirm");
                i.Confirm = new ConfirmDef
                {
                    When = c.ContainsKey("when") ? Condition(c["when"], pack, at + ".confirm.when") : null,
                    Text = LineOf(c, "text", pack, at + ".confirm", required: true),
                    Yes = LineOf(c, "yes", pack, at + ".confirm", required: true),
                    No = LineOf(c, "no", pack, at + ".confirm", required: true),
                };
            }
            if (n.ContainsKey("info")) i.Info = InfoOf(n["info"], i, pack, at + ".info");
            var parts = new List<Effect>();
            if (n.ContainsKey("do")) parts.Add(Effect(n["do"], pack, at + ".do"));
            if (n.TryGetValue("check", out object check) && check is Node structured)
            {
                parts.Add(Effect(new Node { { "check", structured } }, pack, at));
                i.Preview = PreviewOf(structured, pack, at + ".check");
            }
            if (n.ContainsKey("after")) parts.Add(Effect(n["after"], pack, at + ".after"));
            if (n.ContainsKey("preview")) i.Preview = ValueOf(n["preview"], pack, at + ".preview");
            if (parts.Count == 0) Problem(at, "an interaction needs \"do\" or \"check\"");
            i.Body = parts.Count == 1 ? parts[0] : Builtins.Sequence(parts);
            return i;
        }

        /// <summary>The info beside a button: a list of text lines, {icon} and {bar} pieces, each with an optional "when".</summary>
        private List<InfoDef> InfoOf(object node, Interaction i, string pack, string at)
        {
            var info = new List<InfoDef>();
            if (!(node is List<object> entries))
            {
                Problem(at, "must be a list of lines, {icon} and {bar} pieces");
                return info;
            }
            for (int index = 0; index < entries.Count; index++)
            {
                string path = at + "[" + index + "]";
                object entry = entries[index];
                if (entry is Node icon && icon.ContainsKey("icon"))
                {
                    CheckShape(icon, Schema.InfoIcon, path);
                    string key = Str(icon, "icon") ?? "";
                    if (key == "item" && i.Uses == null) Problem(path + ".icon", "\"item\" needs the interaction to have \"uses\"");
                    else if (key == "with" && i.With == null) Problem(path + ".icon", "\"with\" needs the interaction to have \"uses.with\"");
                    else if (key.Length == 0) Problem(path + ".icon", "must be \"item\", \"with\" or a sprite key");
                    if (key != "item" && key != "with" && key.Length > 0 && key.IndexOf(':') < 0) key = pack + ":" + key;
                    info.Add(new InfoDef { Kind = InfoKind.Icon, Icon = key, When = WhenOf(icon, pack, path) });
                }
                else if (entry is Node bar && bar.ContainsKey("bar"))
                {
                    CheckShape(bar, Schema.InfoBar, path);
                    var def = new InfoDef
                    {
                        Kind = InfoKind.Bar,
                        Bar = ValueOf(bar["bar"], pack, path + ".bar"),
                        Max = bar.ContainsKey("max") ? ValueOf(bar["max"], pack, path + ".max") : (s => 100),
                        Text = LineOf(bar, "label", pack, path, required: false),
                        Tone = Str(bar, "tone"),
                        When = WhenOf(bar, pack, path),
                    };
                    def.ShowsOdds = MentionsPercent(bar["bar"]) || (bar.ContainsKey("max") && MentionsPercent(bar["max"])) || NamesPercent(def.Text);
                    info.Add(def);
                }
                else
                {
                    Line line = LineFrom(entry, pack, path, Schema.Case);
                    var def = new InfoDef { Kind = InfoKind.Text, Text = line, Tone = line.Tone, ShowsOdds = NamesPercent(line) };
                    if (entry is Node withWhen) def.When = WhenOf(withWhen, pack, path);
                    info.Add(def);
                }
            }
            return info;
        }

        private Condition WhenOf(Node n, string pack, string at) => n.ContainsKey("when") ? Condition(n["when"], pack, at + ".when") : null;

        private static bool MentionsPercent(object expression) => expression is string text && text.Contains("arg.percent");

        private static bool NamesPercent(Line line) => line != null && (line.UsesPercent || (line.Cases?.Any(c => NamesPercent(c.Line)) ?? false));

        private bool IsScene(string scene)
        {
            return scene == "room" || scene == "event" || scene == "combat" || content.Events.Values.Any(e => e.Scene == scene);
        }

        /// <summary>The chance a check succeeds, for showing on its button. Null when the check can't say.</summary>
        private Value PreviewOf(Node check, string pack, string at)
        {
            if (check.ContainsKey("chance")) return ValueOf(check["chance"], pack, at + ".chance");
            if (!check.ContainsKey("die")) return null;
            // No ReportUnread here: the same arguments compile as the check effect, which reports them once.
            Builtins.DieCheck die = Builtins.DieCheck.From(new Spec(this, pack, at, check));
            return die.Percent;
        }

        internal TextRef Text(Node n, string key, string pack, string at, bool required)
        {
            string text = Str(n, key);
            if (text == null)
            {
                if (required) Problem(at, "needs \"" + key + "\"");
                return null;
            }
            return TextKey(text, pack, at + "." + key);
        }

        /// <summary>A text key, in this pack unless it says "pack:key". {self}, {target}, {area} and the like fill in when shown.</summary>
        internal TextRef TextKey(string key, string pack, string at, int variants = 0)
        {
            int brace = key.IndexOf('{');
            int colon = key.IndexOf(':');
            var text = new TextRef { Pack = pack, Key = key };
            if (colon > 0 && (brace < 0 || colon < brace))
            {
                text.Pack = key.Substring(0, colon);
                text.Key = key.Substring(colon + 1);
            }
            if (brace >= 0) text.Dynamic = Template(text.Key, pack, at);
            else if (variants == 0) content.TextKeys.Add((text.Pack, text.Key));
            else for (int i = 0; i < variants; i++) content.TextKeys.Add((text.Pack, text.Key + "_" + i));
            return text;
        }

        private Func<IScope, string> Template(string key, string pack, string at)
        {
            var pieces = new List<Func<IScope, string>>();
            int position = 0;
            while (position < key.Length)
            {
                int open = key.IndexOf('{', position);
                if (open < 0)
                {
                    string tail = key.Substring(position);
                    pieces.Add(s => tail);
                    break;
                }
                if (open > position)
                {
                    string literal = key.Substring(position, open - position);
                    pieces.Add(s => literal);
                }
                int close = key.IndexOf('}', open);
                if (close < 0)
                {
                    Problem(at, "missing } in " + key);
                    break;
                }
                string path = key.Substring(open + 1, close - open - 1);
                Func<IScope, string> reader = Expr.TextReader(path, fieldRequired: false, pack: pack);
                if (reader == null) Problem(at, "can't fill in {" + path + "}");
                else
                {
                    NoteMemory(pack, path, at, write: false);
                    pieces.Add(reader);
                }
                position = close + 1;
            }
            return s =>
            {
                var result = new StringBuilder();
                foreach (Func<IScope, string> piece in pieces) result.Append(piece(s));
                return result.ToString();
            };
        }

        internal Line LineOf(Node n, string key, string pack, string at, bool required)
        {
            if (!n.TryGetValue(key, out object v) || v == null)
            {
                if (required) Problem(at, "needs \"" + key + "\"");
                return null;
            }
            return LineFrom(v, pack, at + "." + key, Schema.Line);
        }

        /// <summary>
        /// A line is a text key, {text, args, variants, tone}, or {cases: [{when, ...}]}. "key" means the same as "text".
        /// The shape is what keys an object line may have; null when its keys belong to an effect that reports them itself.
        /// </summary>
        internal Line LineFrom(object v, string pack, string at, Shape shape)
        {
            if (v is string s) return new Line { Text = TextKey(s, pack, at) };
            if (v is Node n)
            {
                if (shape != null) CheckShape(n, shape, at);
                if (n.ContainsKey("cases")) return CasesLine(n, pack, at);
                string text = Str(n, "text") ?? Str(n, "key");
                if (text != null)
                {
                    var line = new Line { Text = TextKey(text, pack, at, Int(n, "variants", 0)), Variants = Int(n, "variants", 0), Tone = Str(n, "tone") };
                    List<object> args = List(n, "args");
                    line.UsesPercent = args.Any(a => a as string == "percent");
                    line.Args = args.Select((a, i) => TextArg(a, pack, at + ".args[" + i + "]")).ToArray();
                    return line;
                }
            }
            Problem(at, "must be a text key, {text, args} or {cases}");
            return new Line { Text = new TextRef { Pack = pack, Key = "" } };
        }

        private Line CasesLine(Node n, string pack, string at)
        {
            var line = new Line { Cases = new List<LineCase>(), Tone = Str(n, "tone") };
            int index = 0;
            foreach (object e in List(n, "cases"))
            {
                string path = at + ".cases[" + index++ + "]";
                if (!(e is Node entry)) { Problem(path, "must be an object"); continue; }
                Line inner = LineFrom(entry, pack, path, Schema.Case);
                if (inner.Tone == null) inner.Tone = line.Tone;
                line.Cases.Add(new LineCase { When = entry.ContainsKey("when") ? Condition(entry["when"], pack, path + ".when") : null, Line = inner });
            }
            return line;
        }

        private Func<IScope, object> TextArg(object arg, string pack, string at)
        {
            if (arg is string text)
            {
                if (text == "percent") return s => s.Arg("percent");
                Func<IScope, string> reader = Expr.TextReader(text, fieldRequired: true, pack: pack);
                if (reader != null)
                {
                    NoteMemory(pack, text, at, write: false);
                    return reader;
                }
            }
            if (arg is Node n) return RichArg(n, pack, at);
            Value value = ValueOf(arg, pack, at);
            return s => value(s);
        }

        private Func<IScope, object> RichArg(Node n, string pack, string at)
        {
            if (n.ContainsKey("if"))
            {
                Condition when = Condition(n["if"], pack, at + ".if");
                Func<IScope, object> yes = n.TryGetValue("then", out object t) ? TextArg(t, pack, at + ".then") : (s => "");
                Func<IScope, object> no = n.TryGetValue("else", out object e) ? TextArg(e, pack, at + ".else") : (s => "");
                return s => when(s) ? yes(s) : no(s);
            }
            if (n.ContainsKey("bar"))
            {
                List<object> parts = List(n, "bar");
                if (parts.Count != 2) Problem(at + ".bar", "needs [value, max]");
                Value value = parts.Count > 0 ? ValueOf(parts[0], pack, at + ".bar[0]") : (s => 0);
                Value max = parts.Count > 1 ? ValueOf(parts[1], pack, at + ".bar[1]") : (s => 0);
                return s => Lines.Bar(value(s), max(s));
            }
            if (n.ContainsKey("scheduled"))
            {
                TextRef template = TextKey(Str(n, "scheduled") ?? "", pack, at + ".scheduled");
                return s => string.Join(", ", s.Run.Flow.Scheduled.Select(id => ScheduledText(template, id, s)));
            }
            if (n.ContainsKey("list"))
            {
                string list = Str(n, "list");
                return s => string.Join(", ", s.Run.Roster(list).Select(e => e.Name));
            }
            if (n.ContainsKey("items"))
            {
                ItemFilter filter = Items(n["items"], pack, at + ".items");
                return s => string.Join(", ", s.Run.Inventory.Items.Where(i => filter(i, s)).Select(i => i.Count > 1 ? i.Name + " x" + i.Count : i.Name));
            }
            Line line = LineFrom(n, pack, at, Schema.Line);
            return s => Lines.Render(line, s);
        }

        // A text key that names an event: {run.$pick} in the key becomes the event's id.
        private static string ScheduledText(TextRef template, string id, IScope scope)
        {
            scope.Run.SetText("pick", id.Substring(id.IndexOf(':') + 1));
            return ((ITextHost)scope.Run).Localize(template.Pack, template.Resolve(scope), new object[0]);
        }

        internal string Ref(string id, string kind, string pack, string at)
        {
            string full = Qualify(pack, id);
            Defer(() => { if (!Exists(kind, full)) Problem(at, "no " + kind + " named " + full); });
            return full;
        }

        private string RefOrNull(Node n, string key, string kind, string pack, string at)
        {
            return Str(n, key) != null ? Ref(Str(n, key), kind, pack, at + "." + key) : null;
        }

        private bool Exists(string kind, string id)
        {
            switch (kind)
            {
                case "intent": return content.Intents.ContainsKey(id);
                case "enemy": return content.Enemies.ContainsKey(id);
                case "encounter": return content.Encounters.ContainsKey(id);
                case "event": return content.Events.ContainsKey(id);
                case "room": return content.Rooms.ContainsKey(id);
                case "status": return content.Statuses.ContainsKey(id);
                case "loot": return content.Loot.ContainsKey(id);
                case "cart": return content.Carts.ContainsKey(id);
                case "kit": return content.Kits.ContainsKey(id);
                case "area": return content.Areas.ContainsKey(id);
                default: return false;
            }
        }

        internal Content Content => content;

        // ---- The vocabulary: values, effects, conditions, item filters ----

        private Value NamedValue(string pack, string name, string at)
        {
            string id = pack + ":" + name;
            if (!content.Values.ContainsKey(id))
            {
                Problem(at, "no value named " + name);
                return s => 0;
            }
            if (compilingValue != null)
            {
                if (!valueUses.TryGetValue(compilingValue, out HashSet<string> uses)) valueUses[compilingValue] = uses = new HashSet<string>();
                uses.Add(id);
            }
            return s => content.Values[id](s);
        }

        internal Value ValueOf(object node, string pack, string at)
        {
            if (node is double d) { int n = (int)d; return s => n; }
            if (node is bool b) { int n = b ? 1 : 0; return s => n; }
            if (node is string text)
            {
                Value value = Expr.Compile(text, out string error, name => NamedValue(pack, name, at), HasMod, pack, path => NoteRead(pack, path, at));
                if (error != null)
                {
                    Problem(at, error);
                    return s => 0;
                }
                return value;
            }
            Problem(at, "must be a number or an expression");
            return s => 0;
        }

        internal Effect Effect(object node, string pack, string at)
        {
            if (node == null) return s => { };
            if (node is List<object> list) return Builtins.Sequence(list.Select((e, i) => Effect(e, pack, at + "[" + i + "]")).ToList());
            if (node is Node map && map.Count == 1)
            {
                KeyValuePair<string, object> only = map.First();
                string name = EffectName(only.Key, pack);
                if (!registry.Effects.TryGetValue(name, out EffectCompiler compiler))
                {
                    Problem(at, "unknown effect \"" + only.Key + "\"");
                    return s => { };
                }
                var spec = new Spec(this, pack, at + "." + only.Key, only.Value);
                Effect compiled = compiler(spec) ?? (s => { });
                spec.ReportUnread();
                return compiled;
            }
            Problem(at, "an effect is a list, or an object with exactly one effect name");
            return s => { };
        }

        private string EffectName(string key, string pack)
        {
            if (key.Contains(":")) return key;
            string own = pack + ":" + key;
            return registry.Effects.ContainsKey(own) ? own : key;
        }

        internal Condition Condition(object node, string pack, string at)
        {
            if (node is bool b) return s => b;
            if (node is string || node is double)
            {
                Value value = ValueOf(node, pack, at);
                return s => value(s) != 0;
            }
            if (node is List<object> list) return Builtins.All(list.Select((e, i) => Condition(e, pack, at + "[" + i + "]")).ToList());
            if (node is Node map && map.Count == 1)
            {
                KeyValuePair<string, object> only = map.First();
                string name = only.Key.Contains(":") ? only.Key : registry.Conditions.ContainsKey(only.Key) ? only.Key : pack + ":" + only.Key;
                if (!registry.Conditions.TryGetValue(name, out ConditionCompiler compiler))
                {
                    Problem(at, "unknown condition \"" + only.Key + "\"");
                    return s => false;
                }
                var spec = new Spec(this, pack, at + "." + only.Key, only.Value);
                Condition compiled = compiler(spec) ?? (s => false);
                spec.ReportUnread();
                return compiled;
            }
            Problem(at, "a condition is an expression, a list, or an object with exactly one condition name");
            return s => false;
        }

        /// <summary>"crowbar" is an item id, "#explosive" a group or kind, a list means any of them, and {any|all|not|when} combine.</summary>
        internal ItemFilter Items(object node, string pack, string at)
        {
            if (node is string text)
            {
                if (text.StartsWith("#"))
                {
                    string tag = text.Substring(1);
                    Defer(() => { if (!KnownItemTags().Contains(tag)) Warn(at, "no item has the tag " + tag + " (not a game tag, item group or item_stats tag)"); });
                    return (item, s) => item.HasTag(tag);
                }
                return (item, s) => item.Id == text;
            }
            if (node is List<object> list) return Any(list.Select((e, i) => Items(e, pack, at + "[" + i + "]")).ToList());
            if (node is Node map && map.Count == 1)
            {
                KeyValuePair<string, object> only = map.First();
                string path = at + "." + only.Key;
                var parts = only.Value as List<object> ?? new List<object> { only.Value };
                switch (only.Key)
                {
                    case "any": return Any(parts.Select((e, i) => Items(e, pack, path + "[" + i + "]")).ToList());
                    case "all":
                    {
                        List<ItemFilter> filters = parts.Select((e, i) => Items(e, pack, path + "[" + i + "]")).ToList();
                        return (item, s) => filters.All(f => f(item, s));
                    }
                    case "not":
                    {
                        ItemFilter filter = Items(only.Value, pack, path);
                        return (item, s) => !filter(item, s);
                    }
                    case "when":
                    {
                        Condition condition = Condition(only.Value, pack, path);
                        return (item, s) => condition(s.With(s.Self, s.Target, item));
                    }
                }
                Problem(at, "unknown item filter \"" + only.Key + "\"");
                return (item, s) => false;
            }
            Problem(at, "an item filter is an id, #group, list, or {any|all|not|when}");
            return (item, s) => false;
        }

        private static ItemFilter Any(List<ItemFilter> filters) => (item, s) => filters.Any(f => f(item, s));

        // Every tag an item can have at run time, built once per Finish, when every pack's groups and stats exist.
        private HashSet<string> knownItemTags;

        // item_stats "#tag" keys are left out on purpose: they only apply to items that already have the tag, so counting them
        // would let the same misspelling in a filter and an item_stats key hide each other.
        private HashSet<string> KnownItemTags() => knownItemTags ??= new HashSet<string>(Schema.EngineItemTags
            .Concat(content.ItemGroups.Values.SelectMany(g => g))
            .Concat(content.ItemStats.Values.SelectMany(s => s.Tags)));

        internal sealed class Spec : ISpec
        {
            private readonly PackLoader loader;
            internal readonly Node Args;

            // The keys a compiler asked for. Whatever it never asked for by the time it returns is reported as unused.
            private readonly HashSet<string> read = new HashSet<string>();
            private readonly List<Spec> children = new List<Spec>();

            // {"next_room": true} is how content says "no arguments"; the "value" this wraps it in is never meant to be read.
            // Only a literal true is exempt: {"next_room": 3}, false or a string still warn, and an explicit {"value": true} was typed by the author.
            private readonly bool bareTrue;

            // Compilers that reject an argument return before reading the rest; reporting those as unused would read as a second mistake.
            private bool errored;

            public string PackId { get; }
            public string Path { get; }
            internal PackLoader Loader => loader;

            public Spec(PackLoader loader, string pack, string path, object args)
            {
                this.loader = loader;
                PackId = pack;
                Path = path;
                Args = args as Node ?? new Node { { "value", args } };
                bareTrue = args is bool b && b;
            }

            private void Mark(params string[] keys) => read.UnionWith(keys);

            internal void MarkAllRead() => read.UnionWith(Args.Keys);

            /// <summary>Warns about each argument the compiler never asked for, then lets each entry spec do the same.</summary>
            internal void ReportUnread()
            {
                if (!errored)
                {
                    foreach (KeyValuePair<string, object> arg in Args)
                    {
                        if (arg.Value == null || read.Contains(arg.Key)) continue;
                        if (bareTrue && arg.Key == "value") continue;
                        loader.Warn(Path + "." + arg.Key, "not used here (misspelled, or not a field of this effect or condition)");
                    }
                }
                foreach (Spec child in children) child.ReportUnread();
            }

            /// <summary>All of the arguments read as one condition, for built-ins whose argument is a condition.</summary>
            internal Condition WholeCondition()
            {
                MarkAllRead();
                return loader.Condition(Args, PackId, Path);
            }

            /// <summary>All of the arguments read as one item filter, for built-ins whose argument is a filter.</summary>
            internal ItemFilter WholeItems()
            {
                MarkAllRead();
                return loader.Items(Args, PackId, Path);
            }

            public bool Has(string key)
            {
                read.Add(key);
                return Args.TryGetValue(key, out object v) && v != null;
            }

            public string Text(string key, string fallback = null)
            {
                read.Add(key);
                return Str(Args, key, fallback);
            }

            public Value Value(string key, int fallback)
            {
                return Has(key) ? loader.ValueOf(Args[key], PackId, Path + "." + key) : (s => fallback);
            }

            public Effect Effect(string key) => loader.Effect(Has(key) ? Args[key] : null, PackId, Path + "." + key);

            public Condition Condition(string key)
            {
                return Has(key) ? loader.Condition(Args[key], PackId, Path + "." + key) : (s => true);
            }

            public ItemFilter Items(string key)
            {
                return Has(key) ? loader.Items(Args[key], PackId, Path + "." + key) : ((item, s) => true);
            }

            public void Error(string message)
            {
                errored = true;
                loader.Problem(Path, message);
            }

            public void Warn(string message) => loader.Warn(Path, message);

            /// <summary>Notes a memory path this spec's pack reads or writes; see PackLoader.NoteMemory.</summary>
            internal void NoteMemory(string path, bool write) => loader.NoteMemory(PackId, path, Path, write);

            public bool Flag(string key, bool fallback)
            {
                read.Add(key);
                return Bool(Args, key, fallback);
            }

            public int Number(string key, int fallback)
            {
                read.Add(key);
                return Int(Args, key, fallback);
            }

            public bool IsText(string key) => Has(key) && Args[key] is string;

            public ILine Line(string key) => Has(key) ? loader.LineFrom(Args[key], PackId, Path + "." + key, Schema.Line) : null;

            public ILine InlineLine()
            {
                bool own = Args.ContainsKey("text") || Args.ContainsKey("key") || Args.ContainsKey("cases");
                if (own) Mark("text", "key", "cases", "args", "variants", "tone");
                else Mark("value");
                return loader.LineFrom(own ? (object)Args : (Args.TryGetValue("value", out object v) ? v : null), PackId, Path, null);
            }

            public IItemUse Use(string key = null)
            {
                if (key != null) return loader.UseOf(Has(key) ? Args[key] : null, PackId, Path + "." + key, checkShape: true);
                Mark("select", "each", "consume", "limit", "order", "where");
                // Without "select", the arguments are themselves the item filter.
                if (!Args.ContainsKey("select")) MarkAllRead();
                return loader.UseOf(Args, PackId, Path, checkShape: false);
            }

            public string Require(string key)
            {
                string text = Text(key);
                if (text == null) Error("needs \"" + key + "\"");
                return text;
            }

            public string Ref(string id, string kind) => loader.Ref(id, kind, PackId, Path);

            public string Hook(string key)
            {
                string name = Require(key);
                if (name == null) return "";
                string full = loader.registry.ResolveHook(PackId, name);
                if (full == null || !loader.registry.IsHook(full)) Error("no hook named " + name);
                return full ?? name;
            }

            private List<object> ListAt(string key)
            {
                read.Add(key);
                object value = Args.TryGetValue(key, out object v) ? v : null;
                return value as List<object> ?? new List<object> { value };
            }

            public IReadOnlyList<string> Strings(string key) => ListAt(key).OfType<string>().ToList();

            public IReadOnlyList<Condition> Conditions(string key)
            {
                return ListAt(key).Select((e, i) => loader.Condition(e, PackId, Path + "[" + i + "]")).ToList();
            }

            public IReadOnlyList<ISpec> Entries(string key)
            {
                read.Add(key);
                var entries = new List<ISpec>();
                object value = Args.TryGetValue(key, out object v) ? v : null;
                int index = 0;
                foreach (object e in value as List<object> ?? new List<object>())
                {
                    string at = Path + "[" + index++ + "]";
                    if (e is Node entry)
                    {
                        var child = new Spec(loader, PackId, at, entry);
                        children.Add(child);
                        entries.Add(child);
                    }
                    else loader.Problem(at, "must be an object");
                }
                return entries;
            }

            public IPool Pool(string kind)
            {
                Content content = loader.content;
                switch (kind)
                {
                    case "event": return PoolOf(content.Events, kind);
                    case "encounter": return PoolOf(content.Encounters, kind);
                    case "enemy": return PoolOf(content.Enemies, kind);
                    default:
                        Error("no pool of kind " + kind);
                        return new DefPool<EventDef>(run => new List<EventDef>());
                }
            }

            private IPool PoolOf<T>(Dictionary<string, T> table, string kind) where T : Def
            {
                if (IsText("value"))
                {
                    string full = Ref((string)Args["value"], kind);
                    return new DefPool<T>(run => new List<T> { table[full] });
                }
                // PackLoader.Pool reads these straight from Args, past the accessors that would mark them.
                Mark("value", "pool", "from", "ids", "tag");
                return new DefPool<T>(loader.Pool(Args, "ids", "tag", table, kind, PackId, Path));
            }

            public Func<IScope, IReadOnlyList<string>> Enemies()
            {
                Content content = loader.content;
                if (Has("role"))
                {
                    IReadOnlyList<string> roles = Strings("role");
                    return s => roles.Select(s.Run.Role).Where(e => e != null && !e.IsPlayer).Select(e => e.Id).ToList();
                }
                if (IsText("value"))
                {
                    string id = Ref((string)Args["value"], "enemy");
                    return s => new[] { id };
                }
                if (Has("enemy"))
                {
                    List<string> ids = Strings("enemy").Select(e => Ref(e, "enemy")).ToList();
                    return s => ids;
                }
                if (Has("intel"))
                {
                    string intel = Text("intel");
                    return s => s.Run.Intel.TryGetValue(intel, out List<string> picks) ? picks : (IReadOnlyList<string>)new string[0];
                }
                if (Has("encounters"))
                {
                    var pool = new Node { { "pool", Args["encounters"] } };
                    Func<IRun, List<EncounterDef>> encounters = loader.Pool(pool, "ids", "tag", content.Encounters, "encounter", PackId, Path);
                    return s => encounters(s.Run).Where(e => e.Enemies.Count == 1 && (e.When == null || e.When(s))).Select(e => e.Enemies[0]).ToList();
                }
                IPool named = PoolOf(content.Enemies, "enemy");
                return s => named.Ids(s);
            }
        }
    }
}
