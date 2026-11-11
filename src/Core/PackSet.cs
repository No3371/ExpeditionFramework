using System;
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Api;

namespace ExpeditionFramework.Core
{
    /// <summary>
    /// Every pack, compiled together so any pack can patch any other regardless of load order. A pack whose patches cause
    /// problems anywhere loses its patches and is reported as broken; the packs it tried to change load as they are.
    /// </summary>
    public sealed class PackSet
    {
        private readonly Registry registry;
        private readonly List<(string Pack, string Main, Func<string, string> Read)> packs = new List<(string, string, Func<string, string>)>();

        public PackSet(Registry registry) { this.registry = registry; }

        /// <summary>Whether a mod is installed and enabled. See <see cref="PackLoader.HasMod"/>.</summary>
        public Func<string, bool> HasMod { get; set; } = id => false;

        /// <summary>Adds a pack. Packs compile in the order added, which decides which of two patches that set the same thing wins.</summary>
        public void Add(string pack, string mainFile, Func<string, string> read)
        {
            var cache = new Dictionary<string, string>();
            packs.Add((pack, mainFile, file => cache.TryGetValue(file, out string text) ? text : cache[file] = read(file)));
        }

        /// <summary>
        /// Compiles every pack. Patches are tried one pack at a time, in order; a pack's patches stay only if they add no
        /// problem anywhere. Problems are returned under the pack that caused them.
        /// </summary>
        public Content Compile(out Dictionary<string, List<string>> problems)
        {
            var effects = new Dictionary<string, EffectCompiler>(registry.Effects);
            var conditions = new Dictionary<string, ConditionCompiler>(registry.Conditions);
            var accepted = new HashSet<string>();
            (Content Content, PackLoader Loader) best = Attempt(accepted, effects, conditions);
            var rejected = new Dictionary<string, List<string>>();
            foreach (string patcher in best.Loader.Patchers.ToList())
            {
                accepted.Add(patcher);
                (Content Content, PackLoader Loader) trial = Attempt(accepted, effects, conditions);
                List<string> added = trial.Content.Problems.Except(best.Content.Problems).ToList();
                if (added.Count == 0)
                {
                    best = trial;
                    continue;
                }
                accepted.Remove(patcher);
                rejected[patcher] = added;
            }

            problems = best.Loader.ProblemsByPack.ToDictionary(p => p.Key, p => new List<string>(p.Value));
            foreach (KeyValuePair<string, List<string>> r in rejected)
            {
                if (!problems.TryGetValue(r.Key, out List<string> list)) problems[r.Key] = list = new List<string>();
                list.Add(r.Key + ": its patches were left out because they cause these problems:");
                list.AddRange(r.Value.Select(p => "  " + p));
                best.Content.Problems.AddRange(list.Skip(list.Count - r.Value.Count - 1));
            }
            return best.Content;
        }

        private (Content, PackLoader) Attempt(HashSet<string> patches, Dictionary<string, EffectCompiler> effects, Dictionary<string, ConditionCompiler> conditions)
        {
            // A pack's shared effects register themselves while it compiles; start each attempt from what C# registered.
            Restore(registry.Effects, effects);
            Restore(registry.Conditions, conditions);
            var content = new Content();
            var loader = new PackLoader(content, registry) { HasMod = HasMod, PatchesFrom = patches };
            foreach ((string pack, string main, Func<string, string> read) in packs) loader.Add(pack, main, read);
            loader.CompileAll();
            loader.Finish();
            return (content, loader);
        }

        private static void Restore<T>(Dictionary<string, T> into, Dictionary<string, T> from)
        {
            into.Clear();
            foreach (KeyValuePair<string, T> entry in from) into[entry.Key] = entry.Value;
        }
    }
}
