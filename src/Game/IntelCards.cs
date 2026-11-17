using System;
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Core;

namespace ExpeditionFramework.Game
{
    /// <summary>
    /// What the street says about an area today, shown on its map card through the game's own afterhour events. Each
    /// intel slot an area declares gets as many cards as it can name picks; the day's picks are rolled once and kept.
    /// When something has claimed the pack, its told slots sit out the day's roll and IntelBoard.Tell hands their cards out
    /// one at a time, kept on the same counters as rolled picks so a reload and the run read both alike.
    /// </summary>
    internal sealed class IntelCards
    {
        internal const string DayCounter = "ef_intel_day";

        // The game rolls Random.Range(0, 100), which can return 100 itself, so 100 isn't quite always.
        private const float AlwaysPercent = 101f;

        private readonly AreaDef area;
        private readonly string locationId;
        private readonly MapContext map;
        private readonly Func<bool> discovered;
        private readonly Dictionary<string, AfterhourEventDefinition> cards = new Dictionary<string, AfterhourEventDefinition>();

        public IntelCards(AreaDef area, string locationId, MapContext map, Func<bool> discovered)
        {
            this.area = area;
            this.locationId = locationId;
            this.map = map;
            this.discovered = discovered;
            foreach (IntelSlot slot in area.Intel)
            {
                for (int i = 0; i < slot.MaxPicks; i++) Register(slot, i);
            }
            IntelBoard.Add(this);
        }

        public string Pack => area.Pack;
        public bool HasToldSlots => area.Intel.Any(s => s.Told);
        /// <summary>Whether this is the area of that id, given with or without its pack prefix.</summary>
        public bool AreaIs(string id) => area.Id == id || area.Id == area.Pack + ":" + id;

        private void Register(IntelSlot slot, int index)
        {
            string card = CardId(slot, index);
            cards[card] = ModHelper.AddAfterhourEvent(area.Pack, card, locationId,
                () => Render(slot.Title, slot, index), 1,
                description: () => Render(slot.Description, slot, index),
                effects: slot.Effect != null ? new[] { ModHelper.AfterhourEffect(() => Render(slot.Effect, slot, index)) } : null,
                dailyChancePercent: AlwaysPercent,
                tone: Tone(slot.Tone),
                canStart: () => Rolled() && Pick(slot, index) != null && !Held(slot, index));
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
                for (int i = 0; i < slot.MaxPicks; i++) Show(CardId(slot, i), Pick(slot, i) != null && !Held(slot, i));
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

        /// <summary>Each told slot's counters and cards as they stand, without rolling the day or a budget.</summary>
        public List<ToldSlotState> ToldStates()
        {
            var states = new List<ToldSlotState>();
            if (!PlayerStore.HasInstance || StoreStation.Instance == null) return states;
            bool rolled = State.GetCounter(DayCounter) == StoreStation.Instance.dayCounter;
            bool known = discovered();
            foreach (IntelSlot slot in area.Intel.Where(s => s.Told))
            {
                var state = new ToldSlotState { Area = area.Id, Slot = slot.Id, Discovered = known, RolledToday = rolled, BudgetRolled = State.GetCounter(BudgetCounter(slot)) > 0 };
                for (int i = 0; i < slot.MaxPicks; i++)
                {
                    string pick = Pick(slot, i);
                    if (pick != null) state.Cards.Add((pick, Held(slot, i), AfterhourEvents.IsActive(CardId(slot, i))));
                }
                states.Add(state);
            }
            return states;
        }

        // Rolls today's intel the first time it's asked for each day. Always returns true, so it can lead a canStart.
        private bool Rolled()
        {
            if (!PlayerStore.HasInstance || StoreStation.Instance == null) return true;
            AfterhourLocationState state = State;
            int today = StoreStation.Instance.dayCounter;
            if (state.GetCounter(DayCounter) == today) return true;
            state.SetCounter(DayCounter, today);
            // A claimed pack's told slots are left to IntelBoard.Tell, which is also why their budget and held marks reset here:
            // a card reserved yesterday and never shown lapses with the day, like an untold rumour.
            Dictionary<string, List<int>> picks = IntelRoller.Roll(area, Dice, map.Root, IntelBoard.IsClaimed(area.Pack));
            foreach (IntelSlot slot in area.Intel)
            {
                state.SetCounter(BudgetCounter(slot), 0);
                for (int i = 0; i < slot.MaxPicks; i++)
                {
                    int position = i < picks[slot.Id].Count ? picks[slot.Id][i] : -1;
                    state.SetCounter(PickCounter(slot, i), position + 1);
                    state.SetCounter(HeldCounter(slot, i), 0);
                    SetTone(slot, i, position);
                }
            }
            return true;
        }

        private static int Dice(int sides) => RNG.GetRandomInt(1, sides);

        private void SetTone(IntelSlot slot, int index, int position)
        {
            if (slot.GoodPicks.Count > 0 && cards.TryGetValue(CardId(slot, index), out AfterhourEventDefinition card) && card != null)
            {
                card.toneOverride = position >= 0 && slot.GoodPicks.Contains(area.Pools[slot.Pool][position]) ? AfterhourEventTone.Good : Tone(slot.Tone);
            }
        }

        // Today's told state as IntelRoller reads it: every pick on the counters counts as shown (held ones too, since they
        // are promised), and a told slot's budget is on its own counter, +1 so 0 can mean "not rolled yet". It is rebuilt from
        // the counters on every call rather than cached here: a cache would be lost on a reload the counters survive, leaving
        // two answers to what was told today.
        private IntelRoller.ToldDay Day()
        {
            var day = new IntelRoller.ToldDay();
            foreach (IntelSlot slot in area.Intel)
            {
                List<int> shown = day.ShownOf(slot);
                for (int i = 0; i < slot.MaxPicks; i++)
                {
                    int position = State.GetCounter(PickCounter(slot, i)) - 1;
                    if (position >= 0) shown.Add(position);
                }
                int budget = State.GetCounter(BudgetCounter(slot));
                if (slot.Told && budget > 0) day.Budget[slot.Id] = budget - 1;
            }
            return day;
        }

        private void Keep(IntelRoller.ToldDay day)
        {
            foreach (IntelSlot slot in area.Intel)
            {
                if (day.Budget.TryGetValue(slot.Id, out int left)) State.SetCounter(BudgetCounter(slot), left + 1);
            }
        }

        /// <summary>The told slots that can hand out a card now (the day is rolled first if it hasn't been).</summary>
        public List<IntelSlot> TellCandidates(string slotId)
        {
            if (!PlayerStore.HasInstance || StoreStation.Instance == null) return new List<IntelSlot>();
            // Checked before Rolled() and the budget roll, so an undiscovered area draws no dice and keeps no budget for later.
            if (!discovered()) return new List<IntelSlot>();
            Rolled();
            IntelRoller.ToldDay day = Day();
            List<IntelSlot> candidates = IntelRoller.TellCandidates(area, Dice, map.Root, day, slotId);
            Keep(day);
            // A slot has one map card per pick it can name, so it can tell only while one is unused. The budget alone doesn't
            // guarantee that: when the pack is claimed after today's roll, the slot's rolled picks already hold cards and
            // its budget is rolled afresh on top of them.
            return candidates.Where(slot => FreeCard(slot) >= 0).ToList();
        }

        /// <summary>Hands out one card of a candidate slot and shows it now, or holds it for <see cref="Show(ToldCard)"/>.</summary>
        public ToldCard Tell(IntelSlot slot, bool show)
        {
            Rolled();
            IntelRoller.ToldDay day = Day();
            int position = IntelRoller.Tell(area, slot, Dice, day);
            Keep(day);
            int index = FreeCard(slot);
            State.SetCounter(PickCounter(slot, index), position + 1);
            SetTone(slot, index, position);
            var card = new ToldCard { Cards = this, Area = area, Slot = slot, Index = index, Position = position, Pick = area.Pools[slot.Pool][position] };
            if (show) AfterhourEvents.Start(CardId(slot, index));
            else State.SetCounter(HeldCounter(slot, index), 1);
            return card;
        }

        /// <summary>Shows a held card. False when the day has turned since, so the hold lapsed.</summary>
        public bool Show(ToldCard card)
        {
            if (!PlayerStore.HasInstance || StoreStation.Instance == null) return false;
            if (State.GetCounter(DayCounter) != StoreStation.Instance.dayCounter || State.GetCounter(PickCounter(card.Slot, card.Index)) != card.Position + 1) return false;
            State.SetCounter(HeldCounter(card.Slot, card.Index), 0);
            AfterhourEvents.Start(CardId(card.Slot, card.Index));
            return true;
        }

        // The first of the slot's cards with no pick on it today, or -1.
        private int FreeCard(IntelSlot slot)
        {
            for (int i = 0; i < slot.MaxPicks; i++)
            {
                if (State.GetCounter(PickCounter(slot, i)) == 0) return i;
            }
            return -1;
        }

        // A held pick is marked on a counter of its own rather than by a negative pick counter: counters hold 0 and up, and
        // every reader of the pick counter can go on taking it as position + 1.
        private bool Held(IntelSlot slot, int index) => PlayerStore.HasInstance && State.GetCounter(HeldCounter(slot, index)) == 1;

        private AfterhourLocationState State => PlayerStore.Instance.GetAfterhourState(locationId);

        private string Pick(IntelSlot slot, int index)
        {
            int position = PlayerStore.HasInstance ? State.GetCounter(PickCounter(slot, index)) - 1 : -1;
            List<string> pool = area.Pools[slot.Pool];
            return position >= 0 && position < pool.Count ? pool[position] : null;
        }

        private string CardId(IntelSlot slot, int index) => locationId + "_intel_" + slot.Id + "_" + index;

        private static string PickCounter(IntelSlot slot, int index) => "ef_intel_" + slot.Id + "_" + index;

        private static string HeldCounter(IntelSlot slot, int index) => PickCounter(slot, index) + "_held";

        private static string BudgetCounter(IntelSlot slot) => "ef_intel_" + slot.Id + "_budget";
    }
}
