using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Core;

namespace ExpeditionFramework.Game
{
    /// <summary>One card a told slot handed out: enough to show it later or word a line about it.</summary>
    internal sealed class ToldCard
    {
        public IntelCards Cards;
        public AreaDef Area;
        public IntelSlot Slot;
        /// <summary>Which of the slot's cards on the map this is, 0 to MaxPicks - 1.</summary>
        public int Index;
        /// <summary>The pick's position in the slot's pool.</summary>
        public int Position;
        /// <summary>The qualified id the card names.</summary>
        public string Pick;
    }

    /// <summary>
    /// Who tells a pack's told intel. A claim says some mod hands out the pack's told cards itself, so the day's roll leaves
    /// them alone; without one a pack rolls told slots with the rest. Claims live here, not on the pack's areas, because a
    /// claimant may speak before the packs are loaded (packs load on the first OnModAssetsLoaded, after every OnEnable),
    /// and a claim for an id that never becomes a pack is simply never read. Claims are not saved either: a claim is a fact
    /// about the mods installed now, so a save that kept one would go on hiding told cards after their teller was removed.
    /// </summary>
    internal static class IntelBoard
    {
        private static readonly HashSet<string> claimed = new HashSet<string>();
        private static readonly Dictionary<string, List<IntelCards>> packs = new Dictionary<string, List<IntelCards>>();

        public static void Claim(string packId) { if (!string.IsNullOrEmpty(packId)) claimed.Add(packId); }
        public static bool IsClaimed(string packId) => claimed.Contains(packId);
        /// <summary>Whether the id is a loaded pack with areas on the map.</summary>
        public static bool HasPack(string packId) => packId != null && packs.ContainsKey(packId);
        public static IEnumerable<string> PacksWithToldSlots => packs.Where(p => p.Value.Any(c => c.HasToldSlots)).Select(p => p.Key);

        /// <summary>Lists an area's cards under its pack; each area's cards add themselves as they are built.</summary>
        public static void Add(IntelCards cards)
        {
            if (!packs.TryGetValue(cards.Pack, out List<IntelCards> areas)) packs[cards.Pack] = areas = new List<IntelCards>();
            areas.Add(cards);
        }

        /// <summary>Forgets packs and claims; the pack registry calls this when the framework stops.</summary>
        public static void Reset() { claimed.Clear(); packs.Clear(); }

        /// <summary>
        /// Hands out one told card of the pack: a random candidate slot across its areas (area / slot filters optional, the
        /// area id with or without the pack prefix), then a random unshown pick of it. The slot is drawn first, not a position
        /// out of every open one, so a slot with a big pool doesn't crowd out one with a small pool. show false reserves the
        /// card (budget and intel_max count it) for a later <see cref="Show"/>. Null when nothing can be told now.
        /// </summary>
        public static ToldCard Tell(string packId, string areaId = null, string slotId = null, bool show = true)
        {
            if (packId == null || !packs.TryGetValue(packId, out List<IntelCards> areas)) return null;
            var candidates = new List<(IntelCards Cards, IntelSlot Slot)>();
            foreach (IntelCards cards in areas)
            {
                if (areaId != null && !cards.AreaIs(areaId)) continue;
                candidates.AddRange(cards.TellCandidates(slotId).Select(s => (cards, s)));
            }
            if (candidates.Count == 0) return null;
            (IntelCards picked, IntelSlot slot) = candidates[RNG.GetRandomInt(1, candidates.Count) - 1];
            return picked.Tell(slot, show);
        }

        /// <summary>Shows a card <see cref="Tell"/> reserved. False when it can't be shown any more: the day has turned.</summary>
        public static bool Show(ToldCard card) => card != null && card.Cards.Show(card);
    }
}
