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

        public static void RemoveAll() => ModHelper.RemoveDebugFunctions(ModEntry.ModId);

        private static void GiveKit(List<string> kit)
        {
            foreach (string id in kit) ModHelper.MoveItemToInventory(ModHelper.SpawnItem(id));
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
