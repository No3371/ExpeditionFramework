using System;
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Core;

namespace ExpeditionFramework.Game
{
    /// <summary>
    /// What the street says about an area today, shown on its map card through the game's own afterhour events. Each
    /// intel slot an area declares gets as many cards as it can name picks; the day's picks are rolled once and kept.
    /// </summary>
    internal sealed class IntelCards
    {
        private const string DayCounter = "ef_intel_day";

        // The game rolls Random.Range(0, 100), which can return 100 itself, so 100 isn't quite always.
        private const float AlwaysPercent = 101f;

        private readonly AreaDef area;
        private readonly string locationId;
        private readonly MapContext map;
        private readonly Dictionary<string, AfterhourEventDefinition> cards = new Dictionary<string, AfterhourEventDefinition>();

        public IntelCards(AreaDef area, string locationId, MapContext map)
        {
            this.area = area;
            this.locationId = locationId;
            this.map = map;
            foreach (IntelSlot slot in area.Intel)
            {
                for (int i = 0; i < slot.MaxPicks; i++) Register(slot, i);
            }
        }

        private void Register(IntelSlot slot, int index)
        {
            string card = CardId(slot, index);
            cards[card] = ModHelper.AddAfterhourEvent(area.Pack, card, locationId,
                () => Render(slot.Title, slot, index), 1,
                description: () => Render(slot.Description, slot, index),
                effects: slot.Effect != null ? new[] { ModHelper.AfterhourEffect(() => Render(slot.Effect, slot, index)) } : null,
                dailyChancePercent: AlwaysPercent,
                tone: Tone(slot.Tone),
                canStart: () => Rolled() && Pick(slot, index) != null);
        }

        private static AfterhourEventTone Tone(string tone)
        {
            switch (tone)
            {
                case "good": return AfterhourEventTone.Good;
                case "bad": return AfterhourEventTone.Bad;
                default: return AfterhourEventTone.Mixed;
            }
        }

        private string Render(Line line, IntelSlot slot, int index)
        {
            string pick = Pick(slot, index);
            if (pick == null) return "";
            map.SetText("pick", pick.Substring(pick.IndexOf(':') + 1));
            return map.Render(line);
        }

        /// <summary>
        /// Rolls and shows today's intel if the game hasn't yet, which happens on the day a pack is installed or an area
        /// unlocked. The map card asks for the title before it lists the events, so this is called from there.
        /// </summary>
        public void EnsureToday()
        {
            if (!PlayerStore.HasInstance || StoreStation.Instance == null || State.GetCounter(DayCounter) == StoreStation.Instance.dayCounter) return;
            Rolled();
            foreach (IntelSlot slot in area.Intel)
            {
                for (int i = 0; i < slot.MaxPicks; i++) Show(CardId(slot, i), Pick(slot, i) != null);
            }
        }

        private static void Show(string card, bool known)
        {
            if (known) AfterhourEvents.Start(card);
            else if (AfterhourEvents.IsActive(card)) AfterhourEvents.End(card);
        }

        /// <summary>The intel showing on the map today: slot id to the qualified ids it named.</summary>
        public Dictionary<string, List<string>> Today()
        {
            var today = new Dictionary<string, List<string>>();
            foreach (IntelSlot slot in area.Intel)
            {
                today[slot.Id] = Enumerable.Range(0, slot.MaxPicks)
                    .Where(i => AfterhourEvents.IsActive(CardId(slot, i)))
                    .Select(i => Pick(slot, i))
                    .Where(pick => pick != null)
                    .ToList();
            }
            return today;
        }

        // Rolls today's intel the first time it's asked for each day. Always returns true, so it can lead a canStart.
        private bool Rolled()
        {
            if (!PlayerStore.HasInstance || StoreStation.Instance == null) return true;
            AfterhourLocationState state = State;
            int today = StoreStation.Instance.dayCounter;
            if (state.GetCounter(DayCounter) == today) return true;
            state.SetCounter(DayCounter, today);
            Dictionary<string, List<int>> picks = IntelRoller.Roll(area, sides => RNG.GetRandomInt(1, sides), map.Root);
            foreach (IntelSlot slot in area.Intel)
            {
                for (int i = 0; i < slot.MaxPicks; i++)
                {
                    int position = i < picks[slot.Id].Count ? picks[slot.Id][i] : -1;
                    state.SetCounter(PickCounter(slot, i), position + 1);
                    if (slot.GoodPicks.Count > 0 && cards.TryGetValue(CardId(slot, i), out AfterhourEventDefinition card) && card != null)
                    {
                        card.toneOverride = position >= 0 && slot.GoodPicks.Contains(area.Pools[slot.Pool][position]) ? AfterhourEventTone.Good : Tone(slot.Tone);
                    }
                }
            }
            return true;
        }

        private AfterhourLocationState State => PlayerStore.Instance.GetAfterhourState(locationId);

        private string Pick(IntelSlot slot, int index)
        {
            int position = PlayerStore.HasInstance ? State.GetCounter(PickCounter(slot, index)) - 1 : -1;
            List<string> pool = area.Pools[slot.Pool];
            return position >= 0 && position < pool.Count ? pool[position] : null;
        }

        private string CardId(IntelSlot slot, int index) => locationId + "_intel_" + slot.Id + "_" + index;

        private static string PickCounter(IntelSlot slot, int index) => "ef_intel_" + slot.Id + "_" + index;
    }
}
