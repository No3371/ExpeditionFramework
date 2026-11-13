using System.Collections.Generic;

namespace ExpeditionFramework.Game
{
    /// <summary>
    /// Spawns game items by id, refusing ids the game doesn't know. ItemSpawner.Spawn makes a blank "unknown" item for
    /// those instead of returning null, so a misspelled id in pack data would hand the player junk with no sign of the
    /// typo. Here it gives nothing and warns once per id in the mod log.
    /// </summary>
    internal static class ItemIds
    {
        private static readonly HashSet<string> reported = new HashSet<string>();

        /// <summary>True when the game can make an item with this id: a prebuilt item or one in the item directory.</summary>
        public static bool Known(string id) =>
            id != null && (PreBuiltItemHelper.preBuiltItems.ContainsKey(id) || DirectoryMaster.Has<GameItem>(id));

        /// <summary>The spawned item, or null for an empty or unknown id. <paramref name="where"/> names the caller in the warning.</summary>
        public static GameItem Spawn(string id, string where)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (Known(id)) return ItemSpawner.Spawn(id);
            if (reported.Add(id)) ModEntry.Log.Warning("Unknown item id '" + id + "' (" + where + "): nothing given. Check the id's spelling.");
            return null;
        }
    }
}
