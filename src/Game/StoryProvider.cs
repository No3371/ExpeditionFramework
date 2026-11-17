using System.Collections.Generic;
using ExpeditionFramework.Core;
using ZohFramework.Api;

namespace ExpeditionFramework.Game
{
    /// <summary>
    /// The ef.* verbs a ZoH story gets by using this mod. Each verb only reaches the story's own pack: the registrar names the
    /// consumer, and that id is the pack id. A story that uses the framework without being a pack gets nothing and one warning.
    /// </summary>
    internal static class StoryProvider
    {
        private const string DefaultKey = "rumour_{pick}";

        private sealed class Held { public int Day; public readonly Dictionary<string, ToldCard> Cards = new Dictionary<string, ToldCard>(); }

        private static readonly HashSet<string> warned = new HashSet<string>();
        // Reserved cards by consumer, then by the key the story was given: the key is the only thing a story holds between
        // /ef.rumour tell: false and /ef.tell, so it is the handle. Handing the story a separate ticket would give it two
        // values to carry for one card.
        private static readonly Dictionary<string, Held> held = new Dictionary<string, Held>();

        /// <summary>
        /// Gives one story its verbs. ZoH Framework may call this again for a story it already registered, so everything
        /// here can repeat: the claim is a set entry, and reservations are kept by consumer id, not by registration.
        /// </summary>
        public static void Register(IStoryRegistrar story)
        {
            string consumer = story.ConsumerId;
            // Claimed without asking whether the id is a loaded pack: packs load after every OnEnable, so the registry may
            // not know this one yet, and claiming only known packs would leave it unclaimed and its told slots rolling with
            // the day. A claim for an id that never becomes a pack is never read.
            IntelBoard.Claim(consumer);
            story.AddVerb("ef.rumour", call => Rumour(consumer, call));
            story.AddVerb("ef.tell", call => Tell(consumer, call));
        }

        /// <summary>Drops what is kept for one consumer: its held cards and the mark that it was warned.</summary>
        public static void Forget(string consumer)
        {
            warned.Remove(consumer);
            held.Remove(consumer);
        }

        // /ef.rumour key: "…" area: "…" slot: "…" tell: false;  gives the card's text key, or nothing when no card can be told.
        private static object Rumour(string consumer, IStoryCall call)
        {
            if (!IntelBoard.HasPack(consumer))
            {
                // A warning and nothing, not a StoryVerbException: that would stop the customer's scene over a mistake in
                // the pack's data, where nothing lets the story carry on without a rumour.
                if (warned.Add(consumer)) call.Warn("ef.rumour: " + consumer + " is not an expedition pack, so there is no intel to tell");
                return null;
            }
            string template = Named(call, "key") as string ?? DefaultKey;
            bool tell = !(Named(call, "tell") is bool b) || b;
            ToldCard card = IntelBoard.Tell(consumer, Named(call, "area") as string, Named(call, "slot") as string, show: tell);
            if (card == null) return null;
            string key = IntelRoller.CardKey(template, card.Area.Id, card.Slot.Id, card.Pick);
            if (!tell) Reserve(consumer, key, card);
            return key;
        }

        // /ef.tell "key";  shows the card reserved under that key and gives true; false when there is none to show.
        private static object Tell(string consumer, IStoryCall call)
        {
            string key = call.Args != null && call.Args.Count > 0 ? call.Args[0] as string : null;
            if (key == null || !held.TryGetValue(consumer, out Held h) || h.Day != Today() || !h.Cards.TryGetValue(key, out ToldCard card)) return false;
            h.Cards.Remove(key);
            return IntelBoard.Show(card);
        }

        // The day stamp only drops yesterday's keys; whether a card can still be shown is the board's answer, read off the
        // area's own counters.
        private static void Reserve(string consumer, string key, ToldCard card)
        {
            if (!held.TryGetValue(consumer, out Held h) || h.Day != Today()) held[consumer] = h = new Held { Day = Today() };
            // A second reservation under the same key replaces the first, which stays held on its counters until the day turns.
            h.Cards[key] = card;
        }

        private static int Today() => StoreStation.Instance != null ? StoreStation.Instance.dayCounter : -1;

        private static object Named(IStoryCall call, string name) => call.Named != null && call.Named.TryGetValue(name, out object v) ? v : null;
    }
}
