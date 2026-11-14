using System;
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Core;

namespace ExpeditionFramework.Game
{
    /// <summary>Entries in the game's debug panel for testing a pack: its kit, and unlocking its areas.</summary>
    internal static class DevTools
    {
        public static void Add(Content content, string pack)
        {
            if (content.DebugKits.TryGetValue(pack, out List<string> kit))
            {
                ModHelper.AddDebugFunction(ModEntry.ModId, ModHelper.DEBUG_CATEGORY_MODDING, "Expeditions: give test kit (" + pack + ")", () => GiveKit(kit));
            }
            foreach (AreaDef area in content.Areas.Values.Where(a => a.Pack == pack && a.UnlockedBy != null))
            {
                string name = ModHelper.GetLocalized(area.Title.Pack, area.Title.Key);
                ModHelper.AddDebugFunction(ModEntry.ModId, ModHelper.DEBUG_CATEGORY_MODDING, "Expeditions: unlock " + name, () => Unlock(content, area, name));
            }
        }

        // Where the game keeps its melee weapons: most in their own directory, the tazer with the guns, a wrench in an unused one.
        // Only these are built, not every item the game has: building thousands of items to find a dozen would be slow and would run
        // factories nothing here needs.
        private static readonly string[] WeaponDirectories = { "MeleeWeaponItemDirectory", "GunsItemDirectory", "UnusedDirectory" };

        /// <summary>Entries for the framework itself, whatever packs are installed.</summary>
        public static void AddFramework(Content content)
        {
            ModHelper.AddDebugFunction(ModEntry.ModId, ModHelper.DEBUG_CATEGORY_MODDING, "Expeditions: log melee weapon facts", () => LogMeleeFacts(content));
        }

        public static void RemoveAll() => ModHelper.RemoveDebugFunctions(ModEntry.ModId);

        // One log line per melee weapon the game can make, read by the same code a run uses, for fitting formulas over the facts.
        // Each item is built fresh from its directory, so the line shows it as the game first makes it, not as one carried and aged.
        private static void LogMeleeFacts(Content content)
        {
            var reader = new ItemReader(content);
            var ids = new SortedSet<string>(content.ItemStats.Where(e => !e.Key.StartsWith("#") && e.Value.Tags.Contains("melee")).Select(e => e.Key));
            List<string> loaded = DirectoryMaster.GetDictionaryClassNameList<GameItem>();
            foreach (string directory in WeaponDirectories)
            {
                if (loaded.Contains(directory)) ids.UnionWith(DirectoryMaster.GetIdentifierList<GameItem>(directory));
                else ModEntry.Log.Log("Melee facts: directory " + directory + " is not loaded");
            }
            int count = 0;
            foreach (string id in ids)
            {
                try
                {
                    GameItem weapon = DirectoryMaster.Create<GameItem>(id);
                    if (weapon == null) { ModEntry.Log.Log("Melee facts: " + id + " is not a game item"); continue; }
                    if (!reader.IsMelee(weapon)) continue;
                    var item = new RunItem { Id = id };
                    ItemReader.ReadFacts(weapon, item);
                    ModEntry.Log.Log("Melee facts: " + ItemReader.Describe(weapon, item));
                    count++;
                }
                catch (Exception ex)
                {
                    ModEntry.Log.Warning("Melee facts: " + id + " failed: " + ex.Message);
                }
            }
            StoreUIManager.Instance.Notify(count + " melee weapons written to the game log.");
        }

        private static void GiveKit(List<string> kit)
        {
            foreach (string id in kit)
            {
                GameItem item = ItemIds.Spawn(id, "debug kit");
                if (item != null) ModHelper.MoveItemToInventory(item);
            }
            StoreUIManager.Instance.Notify("Expeditions test kit added. Move it into the satchel.");
        }

        private static void Unlock(Content content, AreaDef area, string name)
        {
            for (AreaDef before = area; before?.UnlockedBy != null; before = content.Areas.TryGetValue(before.UnlockedBy, out AreaDef next) ? next : null)
            {
                PlayerStore.Instance.GetAfterhourState(ExpeditionLocation.LocationId(before.UnlockedBy)).SetCounter(ExpeditionLocation.ClearsCounter, 1);
            }
            StoreUIManager.Instance.Notify(name + " unlocked.");
        }
    }
}
