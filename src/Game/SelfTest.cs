using System;
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Core;
using ZohFramework.Api;

namespace ExpeditionFramework.Game
{
    /// <summary>
    /// Two entries in the debug panel's Modding category that check told intel and the ef.* verbs in the running game and
    /// write PASS, FAIL or SKIP lines to the game log, each led by the number of the check in docs/testing.md.
    /// "Expeditions: run self-tests" plays a pack of its own (<see cref="SelfTestPack"/>) that is never put on the map, in one
    /// frame, and takes everything it made out of the save again before it returns. "Expeditions: check told intel on this
    /// save" only reads the installed packs' counters, and compares them with what it read at its last press.
    /// </summary>
    internal static class SelfTest
    {
        private const string Prefix = "[Self-test] ";
        private const string Pack = SelfTestPack.Id;
        private const string Stranger = "ef_selftest_not_a_pack";
        private const string ZohModId = "zoh_framework";
        private const string ZohApiAssembly = "ZohFramework.Api";
        private const string MainFile = "ExpeditionFramework/main.json";
        private const string KeyTemplate = "{area}|{slot}|{pick}";
        private const int LockedTries = 5;

        private static int passed;
        private static int failed;
        private static int skipped;

        // What the last press of the save check saw. Kept in memory only, so it lasts until the game is closed and nothing is
        // added to the save for it. It is compared across a day change only: the game saves when a day ends, so loading the
        // save starts the day again and a card told before the load is rightly gone.
        private static Seen last;

        private sealed class Seen
        {
            public int Day;
            public HashSet<string> Showing;
            public HashSet<string> Claimed;
        }

        public static void Register()
        {
            ModHelper.AddDebugFunction(ModEntry.ModId, ModHelper.DEBUG_CATEGORY_MODDING, "Expeditions: run self-tests", Run);
            ModHelper.AddDebugFunction(ModEntry.ModId, ModHelper.DEBUG_CATEGORY_MODDING, "Expeditions: check told intel on this save", CheckSave);
        }

        private static int Today => StoreStation.Instance.dayCounter;

        private static bool Begin(string what)
        {
            if (!PlayerStore.HasInstance || StoreStation.Instance == null)
            {
                ModEntry.Log.Warning(Prefix + "Load a save first.");
                return false;
            }
            passed = failed = skipped = 0;
            ModEntry.Log.Log(Prefix + "Started: " + what + ", day " + Today + ".");
            return true;
        }

        private static void End()
        {
            string summary = Prefix + "Finished: " + passed + " passed, " + failed + " failed, " + skipped + " skipped.";
            if (failed > 0) ModEntry.Log.Warning(summary);
            else ModEntry.Log.Log(summary);
            if (StoreUIManager.Instance != null) StoreUIManager.Instance.Notify("Self-tests: " + passed + " passed, " + failed + " failed, " + skipped + " skipped. Details are in the game log.");
        }

        // ---- The self-test on its own pack.

        // The pack's three areas, built as the pack registry builds an installed pack's but never added to the map.
        private sealed class Fixture
        {
            public ExpeditionLocation Street;
            public ExpeditionLocation Cellar;
            public ExpeditionLocation Yard;
            public ExpeditionLocation[] All => new[] { Street, Cellar, Yard };
        }

        private static void Run()
        {
            if (!Begin("told intel and the ef.* verbs on the self-test pack")) return;
            int day = Today;
            try
            {
                Section("24-25", CheckZohFramework);
                Content content = Compile();
                if (content != null)
                {
                    Section("20-23", () => CheckToldIntel(content));
                    Section("26-27", () => CheckVerbs(content));
                }
            }
            finally
            {
                StoreStation.Instance.dayCounter = day;
                CleanUp();
            }
            End();
        }

        // One section throwing fails that section and lets the others run.
        private static void Section(string checks, Action body)
        {
            try
            {
                body();
            }
            catch (Exception ex)
            {
                Fail(checks + " stopped by an exception", ex.ToString());
            }
        }

        private static Content Compile()
        {
            var set = new PackSet(new Registry());
            set.Add(Pack, MainFile, file => SelfTestPack.Json);
            Content content = set.Compile(out Dictionary<string, List<string>> problems);
            List<string> all = problems.Values.SelectMany(p => p).ToList();
            if (all.Count == 0 && content.Areas.ContainsKey(Pack + ":street")) return content;
            Fail("20-27 the self-test pack compiles", string.Join("; ", all));
            return null;
        }

        // Builds the areas' cards afresh, as loading the game does: nothing is carried over in memory, so whatever the new
        // cards know they read from the save. The claim goes with the old cards, as it does when the game restarts.
        private static Fixture Rebuild(Content content, bool claim)
        {
            IntelBoard.Remove(Pack);
            ModHelper.RemoveAfterhourEvents(Pack);
            var fixture = new Fixture
            {
                Street = new ExpeditionLocation(content, content.Areas[Pack + ":street"]),
                Cellar = new ExpeditionLocation(content, content.Areas[Pack + ":cellar"]),
                Yard = new ExpeditionLocation(content, content.Areas[Pack + ":yard"]),
            };
            if (claim) IntelBoard.Claim(Pack);
            return fixture;
        }

        // Puts the areas at the start of a day nothing has been rolled on: no cards, no counters, and a day stamp that is
        // not today's. The stamp is written rather than left out because an absent counter reads 0, which is today on day 0.
        private static void Morning(Fixture fixture, bool cellarUnlocked)
        {
            foreach (ExpeditionLocation location in fixture.All)
            {
                Drop(location.Id);
                State(location).SetCounter(IntelCards.DayCounter, -1);
            }
            if (cellarUnlocked) State(fixture.Street).SetCounter(ExpeditionLocation.ClearsCounter, 1);
        }

        // Takes a location's saved state out of the save: its counters and its cards.
        private static void Drop(string locationId) => PlayerStore.Instance.afterhourState?.Remove(locationId);

        private static AfterhourLocationState State(ExpeditionLocation location) => PlayerStore.Instance.GetAfterhourState(location.Id);

        private static List<string> Showing(ExpeditionLocation location, string slot) => location.Intel.Today()[slot];

        private static string Join(IEnumerable<string> ids) => "[" + string.Join(", ", ids) + "]";

        // Runs the body with the game's day counter moved, and puts it back. Nothing else runs meanwhile: the whole
        // self-test is one call from the debug panel, so the game never sees the other day.
        private static void OnDay(int day, Action body)
        {
            int real = Today;
            StoreStation.Instance.dayCounter = day;
            try
            {
                body();
            }
            finally
            {
                StoreStation.Instance.dayCounter = real;
            }
        }

        private static void CleanUp()
        {
            foreach (string area in new[] { "street", "cellar", "yard" })
            {
                Drop(ExpeditionLocation.LocationId(Pack + ":" + area));
            }
            ModHelper.RemoveAfterhourEvents(Pack);
            IntelBoard.Remove(Pack);
            IntelBoard.Remove(Stranger);
            StoryProvider.Forget(Pack);
            StoryProvider.Forget(Stranger);
        }

        private static void CheckToldIntel(Content content)
        {
            string streetId = Pack + ":street";
            string nothing = "Intel: nothing to tell for " + Pack;

            Fixture f = Rebuild(content, claim: false);
            Morning(f, cellarUnlocked: false);
            f.Street.EnsureIntel();
            List<string> rumours = Showing(f.Street, "rumour");
            List<string> signs = Showing(f.Street, "sign");
            Report("20 unclaimed: a told slot's cards are on the map at the day's roll", rumours.Count == 2 && signs.Count == 1,
                "wanted 2 told cards and 1 rolled card, got told " + Join(rumours) + " rolled " + Join(signs));

            IntelBoard.Claim(Pack);
            Morning(f, cellarUnlocked: false);
            f.Street.EnsureIntel();
            rumours = Showing(f.Street, "rumour");
            signs = Showing(f.Street, "sign");
            Report("21a claimed before the roll: the day's roll shows no told card", rumours.Count == 0 && signs.Count == 1,
                "wanted 0 told cards and 1 rolled card, got told " + Join(rumours) + " rolled " + Join(signs));

            string first = DevTools.TellIntel(Pack);
            rumours = Showing(f.Street, "rumour");
            Report("21b a tell shows the card at once, with the line \"Intel: told <slot> -> <pick> in <area>\"",
                rumours.Count == 1 && first == "Intel: told rumour -> " + rumours[0] + " in " + streetId,
                "the teller said \"" + first + "\" and the map shows " + Join(rumours));

            string second = DevTools.TellIntel(Pack);
            string third = DevTools.TellIntel(Pack);
            rumours = Showing(f.Street, "rumour");
            Report("21c past the slot's budget the teller says \"Intel: nothing to tell for <pack>\"",
                second.StartsWith("Intel: told rumour -> ") && third == nothing && rumours.Distinct().Count() == 2,
                "second tell \"" + second + "\", third tell \"" + third + "\", the map shows " + Join(rumours));

            List<string> before = rumours.OrderBy(r => r).ToList();
            f = Rebuild(content, claim: true);
            List<string> after = Showing(f.Street, "rumour").OrderBy(r => r).ToList();
            string afterReload = DevTools.TellIntel(Pack);
            Report("21d told cards and the spent budget are read back from the save by newly built cards (as when the map is opened again)",
                before.SequenceEqual(after) && afterReload == nothing,
                "before " + Join(before) + ", after " + Join(after) + ", then the teller said \"" + afterReload + "\"");

            bool toldWhileLocked = false;
            for (int i = 0; i < LockedTries; i++) toldWhileLocked |= IntelBoard.Tell(Pack, "cellar") != null;
            AfterhourLocationState cellar = State(f.Cellar);
            // Only the stamp Morning wrote: the locked area rolled no day, no budget and no pick.
            bool untouched = cellar.counters.Count == 1 && cellar.GetCounter(IntelCards.DayCounter) != Today;
            State(f.Street).SetCounter(ExpeditionLocation.ClearsCounter, 1);
            ToldCard whisper = IntelBoard.Tell(Pack, "cellar");
            Report("23 a told slot in a locked area is never told, and is told once the area is unlocked",
                !toldWhileLocked && untouched && whisper != null && whisper.Slot.Id == "whisper" && Showing(f.Cellar, "whisper").Contains(whisper.Pick),
                "told while locked: " + toldWhileLocked + ", counters left alone: " + untouched + ", told after unlocking: " + (whisper == null ? "nothing" : whisper.Slot.Id + " -> " + whisper.Pick));

            OnDay(Today + 1, () =>
            {
                int expired = Showing(f.Street, "rumour").Count;
                f.Street.EnsureIntel();
                int atRoll = Showing(f.Street, "rumour").Count;
                int rolled = Showing(f.Street, "sign").Count;
                ToldCard next = IntelBoard.Tell(Pack, "street");
                List<string> told = Showing(f.Street, "rumour");
                Report("22 next day the told cards are gone and the slot shows nothing until told (the day counter is moved for this check)",
                    expired == 0 && atRoll == 0 && rolled == 1 && next != null && told.Count == 1 && told[0] == next.Pick,
                    "told cards before the roll " + expired + ", after it " + atRoll + ", rolled cards " + rolled + ", after a tell " + Join(told));
            });
        }

        private static void CheckVerbs(Content content)
        {
            Fixture f = Rebuild(content, claim: false);
            var story = new Story(Pack);
            StoryProvider.Register(story);
            bool verbs = story.Verbs.ContainsKey("ef.rumour") && story.Verbs.ContainsKey("ef.tell");
            Report("26a a story that uses the framework gets ef.rumour and ef.tell, and its pack is claimed", verbs && IntelBoard.IsClaimed(Pack),
                "verbs " + Join(story.Verbs.Keys) + ", claimed: " + IntelBoard.IsClaimed(Pack));
            if (!verbs)
            {
                Skip("26b-27b the ef.* verbs", "the provider registered no verbs to call");
                return;
            }
            Morning(f, cellarUnlocked: true);
            foreach (ExpeditionLocation location in f.All) location.EnsureIntel();

            object key = story.Rumour(("slot", "rumour"), ("tell", false));
            int whileHeld = Showing(f.Street, "rumour").Count;
            object shown = story.Tell(key);
            List<string> rumours = Showing(f.Street, "rumour");
            object again = story.Tell(key);
            Report("26b /ef.rumour tell: false shows no card, /ef.tell with its key shows it, and the key works once",
                key is string && whileHeld == 0 && True(shown) && rumours.Count == 1 && (string)key == IntelRoller.CardKey("rumour_{pick}", null, null, rumours[0]) && False(again),
                "key " + Text(key) + ", cards while held " + whileHeld + ", /ef.tell gave " + Text(shown) + " then " + Text(again) + ", the map shows " + Join(rumours));

            // Street's rumour still has a card to tell here, so an unfiltered pick could land on it.
            var filtered = new List<object>
            {
                story.Rumour(("key", KeyTemplate), ("area", "cellar")),
                story.Rumour(("key", KeyTemplate), ("area", "cellar")),
                story.Rumour(("key", KeyTemplate), ("area", Pack + ":cellar")),
                story.Rumour(("key", KeyTemplate), ("slot", "whisper")),
                story.Rumour(("key", KeyTemplate), ("slot", "whisper")),
                story.Rumour(("key", KeyTemplate), ("slot", "whisper")),
            };
            object noArea = story.Rumour(("area", "nowhere"));
            object noSlot = story.Rumour(("slot", "nobody"));
            object wrongArea = story.Rumour(("area", "cellar"), ("slot", "rumour"));
            Report("26c area: and slot: narrow the pick, and a filter nothing matches gives nothing",
                filtered.All(k => k is string s && s.StartsWith("cellar|whisper|")) && filtered.Distinct().Count() == filtered.Count
                    && Showing(f.Cellar, "whisper").Count == filtered.Count && noArea == null && noSlot == null && wrongArea == null,
                "filtered keys " + Join(filtered.Select(Text)) + ", unknown area " + Text(noArea) + ", unknown slot " + Text(noSlot) + ", slot of another area " + Text(wrongArea));

            object held = story.Rumour(("slot", "rumour"), ("tell", false));
            object late = null;
            int lateCards = 0;
            OnDay(Today + 1, () =>
            {
                late = story.Tell(held);
                lateCards = Showing(f.Street, "rumour").Count;
            });
            Report("26d next day a key from the day before gives false and shows nothing (the day counter is moved for this check)",
                held is string && False(late) && lateCards == 0,
                "key " + Text(held) + ", /ef.tell gave " + Text(late) + ", cards showing " + lateCards);

            var stranger = new Story(Stranger);
            StoryProvider.Register(stranger);
            object strangerFirst = stranger.Rumour();
            object strangerSecond = stranger.Rumour();
            object strangerTell = stranger.Tell("rumour_r1");
            Report("26e a mod that uses the provider without being a pack gets one warning and nothing",
                strangerFirst == null && strangerSecond == null && False(strangerTell) && stranger.Warnings.Count == 1 && stranger.Warnings[0].Contains("is not an expedition pack"),
                "/ef.rumour gave " + Text(strangerFirst) + " and " + Text(strangerSecond) + ", /ef.tell gave " + Text(strangerTell) + ", warnings " + Join(stranger.Warnings));

            // Street has told or held both its cards, cellar all six, and yard is at its limit.
            object spent = story.Rumour();
            Report("27a with every slot's budget spent /ef.rumour gives nothing", spent == null, "it gave " + Text(spent));
            int notices = Showing(f.Yard, "notice").Count;
            object capped = story.Rumour(("area", "yard"));
            Report("27b an area at its intel_max gives nothing", notices == 1 && capped == null && Showing(f.Yard, "talk").Count == 0,
                "rolled cards in the area " + notices + ", /ef.rumour gave " + Text(capped));
        }

        private static void CheckZohFramework()
        {
            bool installed = ModLoader.Mods.Any(m => m.IsEnabled && m.Manifest?.ID == ZohModId);
            if (installed)
            {
                Skip("24 without ZoH Framework the mod loads and told cards roll with the day", "ZoH Framework is enabled; disable it, restart the game and run this again");
                List<string> copies = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == ZohApiAssembly).Select(a => a.Location).ToList();
                Report("25 one copy of " + ZohApiAssembly + " is loaded, from " + string.Join(" and ", copies), copies.Count == 1, copies.Count + " copies are loaded");
                return;
            }
            List<string> claimedPacks = IntelBoard.PacksWithToldSlots.Where(IntelBoard.IsClaimed).ToList();
            Report("24 without ZoH Framework the mod loads and no pack's told slots are claimed (20 shows they roll with the day)", claimedPacks.Count == 0,
                "claimed: " + Join(claimedPacks) + "; the debug panel's claim entry claims too, so restart the game and run this before using it");
            Skip("25 one copy of " + ZohApiAssembly + " is loaded", "ZoH Framework is not enabled");
        }

        // A story as the provider sees one: it keeps the verbs it is given and calls them the way ZoH Framework would.
        private sealed class Story : IStoryRegistrar
        {
            public readonly Dictionary<string, StoryVerb> Verbs = new Dictionary<string, StoryVerb>();
            public readonly List<string> Warnings = new List<string>();

            public Story(string consumer) { ConsumerId = consumer; }

            public string ConsumerId { get; }
            public IStoryVariables Variables => null;
            public void AddVerb(string qualifiedName, StoryVerb verb, params StoryPass[] passes) => Verbs[qualifiedName] = verb;
            public void OnArrival(Action<IStoryEvent> handler) { }
            public void OnPurchase(Action<IStoryEvent> handler) { }
            public void OnSceneEnd(Action<IStoryEvent> handler) { }

            public object Rumour(params (string Name, object Value)[] named)
            {
                return Verbs["ef.rumour"](new Call(this, new object[0], named.ToDictionary(n => n.Name, n => n.Value)));
            }

            public object Tell(object key)
            {
                return Verbs["ef.tell"](new Call(this, new[] { key }, new Dictionary<string, object>()));
            }
        }

        private sealed class Call : IStoryCall
        {
            private readonly Story story;

            public Call(Story story, object[] args, Dictionary<string, object> named)
            {
                this.story = story;
                Args = args;
                Named = named;
            }

            public string ConsumerId => story.ConsumerId;
            public string Scene => "selftest";
            public StoryPass Pass => StoryPass.Live;
            public IReadOnlyList<object> Args { get; }
            public IReadOnlyDictionary<string, object> Named { get; }
            public IStoryVariables Variables => null;
            public void Warn(string message) => story.Warnings.Add(message);
        }

        private static bool True(object value) => value is bool b && b;
        private static bool False(object value) => value is bool b && !b;
        private static string Text(object value) => value == null ? "nothing" : value is string s ? "\"" + s + "\"" : value.ToString();

        // ---- The check on the installed packs.

        private static void CheckSave()
        {
            if (!Begin("told intel of the installed packs on this save")) return;
            try
            {
                CheckInstalledPacks();
            }
            catch (Exception ex)
            {
                Fail("21-23 stopped by an exception", ex.ToString());
            }
            End();
        }

        private static void CheckInstalledPacks()
        {
            List<string> packs = IntelBoard.PacksWithToldSlots.ToList();
            if (packs.Count == 0)
            {
                Skip("21-23 told intel on this save", "no installed pack has a told slot");
                return;
            }
            var claimed = new HashSet<string>(packs.Where(IntelBoard.IsClaimed));
            var states = new List<(string Pack, ToldSlotState Slot)>();
            foreach (string pack in packs)
            {
                foreach (ToldSlotState slot in IntelBoard.ToldStates(pack))
                {
                    states.Add((pack, slot));
                    ModEntry.Log.Log(Prefix + "      " + Describe(pack, claimed.Contains(pack), slot));
                }
            }
            var showing = new HashSet<string>(states.SelectMany(s => s.Slot.Cards.Where(c => c.Showing).Select(c => Name(s.Slot) + " -> " + c.Pick)));

            List<string> stray = states.Where(s => s.Slot.Cards.Any(c => c.Showing && (c.Held || !s.Slot.RolledToday))).Select(s => Name(s.Slot)).ToList();
            Report("21 every told card on the map is one of today's picks and not a held one", stray.Count == 0, "slots with a stray card: " + Join(stray));

            List<string> lockedButTold = states.Where(s => !s.Slot.Discovered && s.Slot.Cards.Any(c => c.Showing)).Select(s => Name(s.Slot)).ToList();
            if (states.All(s => s.Slot.Discovered)) Skip("23 no told card shows for an area that is off the map", "every area with a told slot is on the map in this save");
            else Report("23 no told card shows for an area that is off the map", lockedButTold.Count == 0, "slots telling about a locked or hidden area: " + Join(lockedButTold));

            if (last == null || last.Day > Today)
            {
                Skip("22 on a new day told slots show nothing until told", "nothing to compare with yet: press again on the next day, after opening the night map");
            }
            else if (last.Day == Today)
            {
                Skip("22 on a new day told slots show nothing until told", "the last press was on the same day");
            }
            else
            {
                // A pack claimed since before today's roll: its told slots hold a pick only once a tell has rolled their budget.
                List<(string Pack, ToldSlotState Slot)> watched = states.Where(s => claimed.Contains(s.Pack) && last.Claimed.Contains(s.Pack)).ToList();
                List<string> untold = watched.Where(s => s.Slot.RolledToday && !s.Slot.BudgetRolled && s.Slot.Cards.Count > 0).Select(s => Name(s.Slot)).ToList();
                if (watched.Count == 0) Skip("22 on a new day told slots show nothing until told", "no pack with told slots was claimed at the last press; unclaimed, they roll with the day");
                else if (!watched.Any(s => s.Slot.RolledToday)) Skip("22 on a new day told slots show nothing until told", "today's intel is not rolled yet; open the night map and press again");
                else Report("22 on a new day a claimed pack's told slots show nothing until told", untold.Count == 0, "slots with a card nobody told: " + Join(untold));
            }
            last = new Seen { Day = Today, Showing = showing, Claimed = claimed };
            ModEntry.Log.Log(Prefix + "Noted " + showing.Count + " told card(s) showing on day " + Today + " for the next press.");
        }

        private static string Name(ToldSlotState slot) => slot.Area + "/" + slot.Slot;

        private static string Describe(string pack, bool isClaimed, ToldSlotState slot)
        {
            string cards = slot.Cards.Count == 0 ? "no cards" : string.Join(", ", slot.Cards.Select(c => c.Pick + (c.Held ? " (held)" : c.Showing ? " (showing)" : " (not showing)")));
            return Name(slot) + ": pack " + (isClaimed ? "claimed" : "unclaimed") + ", area " + (slot.Discovered ? "on the map" : "off the map")
                + ", " + (slot.RolledToday ? "rolled today" : "not rolled today") + ", budget " + (slot.BudgetRolled ? "rolled" : "not rolled") + ", " + cards;
        }

        // ---- Results.

        private static void Report(string name, bool ok, string problem)
        {
            if (ok)
            {
                passed++;
                ModEntry.Log.Log(Prefix + "PASS  " + name);
            }
            else
            {
                Fail(name, problem);
            }
        }

        private static void Fail(string name, string problem)
        {
            failed++;
            ModEntry.Log.Warning(Prefix + "FAIL  " + name + ": " + problem);
        }

        private static void Skip(string name, string reason)
        {
            skipped++;
            ModEntry.Log.Log(Prefix + "SKIP  " + name + ": " + reason);
        }
    }
}
