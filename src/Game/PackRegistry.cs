using System;
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Api;
using ExpeditionFramework.Core;

namespace ExpeditionFramework.Game
{
    /// <summary>
    /// Finds enabled mods that ship ExpeditionFramework/main.json and adds their areas. The folder is the whole contract, so
    /// a mod can carry expedition data without depending on the framework; without it, the folder is just never read.
    /// </summary>
    internal static class PackRegistry
    {
        private const string MainFile = "ExpeditionFramework/main.json";

        private static Registry registry;
        private static readonly HashSet<string> Loaded = new HashSet<string>();

        /// <summary>Whether any pack added an area, so there is somewhere to take things in hand.</summary>
        public static bool HasAreas { get; private set; }

        public static void Start()
        {
            registry = new Registry();
            Loaded.Clear();
            HasAreas = false;
        }

        public static void Stop()
        {
            foreach (string pack in Loaded)
            {
                ModHelper.RemoveAfterhourEvents(pack);
                ModHelper.RemoveAfterhourLocations(pack);
            }
            DevTools.RemoveAll();
            Loaded.Clear();
            HasAreas = false;
            registry = null;
        }

        // Called as each mod finishes loading. The game lists a mod only after its OnEnable returns, so the framework
        // can't find its own folder at startup; by the first call that can, the game has registered every mod, so all
        // packs are read and compiled together then. That lets any pack patch any other, whatever the mod order.
        public static void Register(LoadedMod mod)
        {
            try
            {
                if (registry == null || ModHelper.GetModFolder(ModEntry.ModId) == null) return;
                if (Loaded.Count == 0)
                {
                    LoadAll();
                    return;
                }
                if (mod?.Manifest != null && !Loaded.Contains(mod.Manifest.ID) && IsPack(mod))
                {
                    ModEntry.Log.Warning("Expedition pack " + mod.Manifest.ID + " appeared after the others loaded. Restart the game to add it.");
                }
            }
            catch (Exception ex)
            {
                ModEntry.Log.Warning("Could not load expedition packs: " + ex);
            }
        }

        private static bool IsPack(LoadedMod mod)
        {
            return mod.IsEnabled && mod.Manifest?.ID != ModEntry.ModId && ModHelper.ModFileExists(mod.Manifest.ID, MainFile);
        }

        // A pack's listed ExpeditionFramework.Api.dll has no IMod, so it creates no entry with an instance.
        private static object InstanceOf(string id)
        {
            return ModLoader.Mods.FirstOrDefault(m => m.Manifest?.ID == id && m.Instance != null)?.Instance;
        }

        private static void LoadAll()
        {
            // The framework's own pack first: its defaults are what other packs build on and override.
            var packs = new List<string> { ModEntry.ModId };
            packs.AddRange(ModLoader.Mods.Where(IsPack).Select(m => m.Manifest.ID).Distinct());
            var set = new PackSet(registry) { HasMod = id => ModLoader.Mods.Any(m => m.IsEnabled && m.Manifest?.ID == id) };
            foreach (string pack in packs)
            {
                Loaded.Add(pack);
                (InstanceOf(pack) as IExpeditionExtension)?.RegisterExpeditions(registry.For(pack));
                set.Add(pack, MainFile, file => ModHelper.ReadModFileText(pack, file));
            }
            Content content = set.Compile(out Dictionary<string, List<string>> problems);
            foreach (string pack in packs)
            {
                if (problems.TryGetValue(pack, out List<string> list) && list.Count > 0)
                {
                    foreach (string problem in list) ModEntry.Log.Warning(problem);
                    ModEntry.Log.Warning("Expedition pack " + pack + " has problems, so its areas were not added.");
                    continue;
                }
                foreach (AreaDef area in content.Areas.Values.Where(a => a.Pack == pack))
                {
                    ModHelper.AddAfterhourLocation(new ExpeditionLocation(content, area));
                    HasAreas = true;
                }
                DevTools.Add(content, pack);
            }
            foreach (KeyValuePair<string, List<string>> other in problems.Where(p => !packs.Contains(p.Key) && p.Value.Count > 0))
            {
                foreach (string problem in other.Value) ModEntry.Log.Warning(problem);
            }
            ModEntry.Log.Log("Expedition packs: " + string.Join(", ", packs));
        }
    }
}
