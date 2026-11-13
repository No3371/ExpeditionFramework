using System;
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Core;
using UnityEngine;

namespace ExpeditionFramework.Game
{
    /// <summary>Shows the engine what the player carries, and applies what the engine does to the game.</summary>
    internal sealed class GameBridge : IBridge
    {
        private readonly ItemReader reader;
        private readonly TradeCounter counter;
        private readonly GameSounds sounds;
        private readonly string pack;
        private readonly string areaId;
        private readonly List<GameItem> found = new List<GameItem>();
        private List<RunItem> current = new List<RunItem>();
        private int readFrame = -1;

        public GameBridge(Content content, AreaDef area, TradeCounter counter, GameSounds sounds)
        {
            reader = new ItemReader(content);
            pack = area.Pack;
            areaId = area.Id;
            this.counter = counter;
            this.sounds = sounds;
        }

        public static void OpenAmmoBoxes()
        {
            foreach (GameItem box in Carried().Where(AmmoBoxHelper.IsAmmoBox).ToList()) AmmoBoxHelper.Unpackage(box);
        }

        private static IEnumerable<GameSlotInventory> Hands()
        {
            EmporiumEntry entry = EmporiumEntry.Instance;
            return new[] { entry.afterhourPocketSlotInvLeft, entry.afterhourPocketSlotInvRight }.Where(slot => slot != null);
        }

        private static List<GameItem> InHands() => Hands().Select(slot => slot.childItem).Where(item => item != null && GeneralHelper.IsItemOwned(item)).ToList();

        private static List<GameItem> Carried() => EmporiumEntry.Instance.GetAllAfterhourOwnedItems();

        private static GameInventory Ground => EmporiumEntry.Instance.afterhourInventory;

        public IReadOnlyList<RunItem> Items()
        {
            // The engine asks once per possible button, so read the bag at most once a frame.
            if (readFrame != Time.frameCount)
            {
                readFrame = Time.frameCount;
                current = reader.Read(Carried(), new HashSet<GameItem>(InHands()));
            }
            return current;
        }

        public void Consume(RunItem item, int count)
        {
            if (item.Handle is List<GameItem> stack) GraphUtils.TryDestroyAll(stack, count);
            else if (item.Handle is GameItem single) single.Destroy();
            readFrame = -1;
        }

        public string GiveItem(string itemId) => PutOnGround(ItemIds.Spawn(itemId, "give"));

        private string PutOnGround(GameItem item)
        {
            if (item == null) return null;
            string name = item.GetDisplayName();
            readFrame = -1;
            if (!GraphUtils.TryAcceptAllOrDestroy(Ground, item, pack)) return null;
            found.Add(item);
            return name;
        }

        public string RollTable(string table) => LootRegistry.HasGroup(table) ? LootRegistry.RollGroup(table) : LootRegistry.Roll(table);

        public bool Fits(RunItem gun, RunItem round) { Items(); return reader.Fits(gun, round); }

        public bool Equip(RunItem item)
        {
            GameSlotInventory hand = Hands().FirstOrDefault(slot => slot.childItem == null);
            if (hand == null || !(item.Handle is GameItem weapon)) return false;
            GraphUtils.TryAcceptAll(hand, weapon);
            readFrame = -1;
            return hand.childItem == weapon;
        }

        public int Credits => PlayerStore.Instance.GetCash();

        public void AddCredits(int amount) => ModHelper.AddCredit(amount);

        public bool SpendCredits(int amount)
        {
            if (Credits < amount) return false;
            PlayerStore.Instance.ModCash(-amount, false);
            return true;
        }

        public int GroundCount => EmporiumEntry.Instance.afterhourInventory.childItems.Count;

        public int ClearGround()
        {
            List<GameItem> ground = EmporiumEntry.Instance.afterhourInventory.childItems.ToList();
            foreach (GameItem item in ground)
            {
                found.Remove(item);
                item.Destroy();
            }
            return ground.Count;
        }

        public string TakeBack()
        {
            List<GameItem> ground = EmporiumEntry.Instance.afterhourInventory.childItems;
            if (ground.Count == 0) return null;
            // The newest thing found is the one they saw you take.
            GameItem item = ground.OrderByDescending(i => found.IndexOf(i)).First();
            string name = item.GetDisplayName();
            found.Remove(item);
            item.Destroy();
            return name;
        }

        public void LoseFound()
        {
            foreach (GameItem item in found.Where(i => i.parentInventory != null).ToList()) item.Destroy();
            found.Clear();
            readFrame = -1;
        }

        public List<string> LoseCarried(Func<int, int> chanceForSize)
        {
            EmporiumEntry entry = EmporiumEntry.Instance;
            GameItem backpack = entry.afterhourPocketSlotInvBackpack != null ? entry.afterhourPocketSlotInvBackpack.childItem : null;
            var kit = new HashSet<GameItem>(Carried().Where(item => item != backpack && item.parentInventory != entry.afterhourInventory));
            var lost = new List<string>();
            foreach (GameItem piece in kit.Where(item => !IsPartOf(item, kit)).ToList())
            {
                if (RNG.GetRandomIntExculsive(0, 100) >= chanceForSize(SizeOf(piece))) continue;
                lost.Add(piece.unitCount > 1 ? piece.GetDisplayName() + " x" + piece.unitCount : piece.GetDisplayName());
                piece.Destroy();
            }
            readFrame = -1;
            return lost;
        }

        // Grid cells the piece takes up as carried, so a gun with a stock attached counts bigger than a bare one.
        private static int SizeOf(GameItem piece)
        {
            int size = piece.GetModifiedItemSize();
            return size > 0 ? size : Math.Max(1, piece.GetItemSize());
        }

        // Whether the item sits inside another item of the set, such as a magazine inside a gun.
        private static bool IsPartOf(GameItem item, HashSet<GameItem> set)
        {
            GameInventory holder = item.parentInventory;
            return holder != null && holder.parents != null && holder.parents.Any(owner => owner is GameItem parent && set.Contains(parent));
        }

        public int GameValue(string name) => GameValues.Get(name, areaId);

        public IMemory Memory { get; } = new SaveMemory();

        public void Wound(string kind)
        {
            if (kind == "major") HealthData.ReceiveMajorWound();
            else HealthData.ReceiveMinorWound();
        }

        // As the game settles its own overnight reputation changes (VendingMachineHelper.SettleMislabelRep): ModReputation
        // without the store's popup, which isn't on screen at night, and its own gained/lost text with the amount.
        public string Reputation(string faction, double amount)
        {
            string id = GameReads.FactionId(faction);
            StoreReputation rep = amount == 0 || !PlayerStore.HasInstance || PlayerStore.Instance.storeReputations == null ? null : StoreReputation.GetStoreReputation(id);
            if (rep == null) return null;
            if (amount == Math.Floor(amount) && Math.Abs(amount) <= int.MaxValue) StoreReputation.ModReputation(id, (int)amount, notify: false);
            else
            {
                // The static ModReputation takes whole numbers only. This is its body for a decimal: lower level gains add
                // wild favor at 40 per point (rounded), then the faction's own ModReputation applies perks and difficulty.
                if (id == StoreReputation.FACTION_LOWER_LEVEL && amount > 0) PlayerStore.Instance.wildFavor += (int)Math.Round(amount * 40, MidpointRounding.AwayFromZero);
                rep.ModReputation(amount);
                if (AchievementTracker.Instance != null) AchievementTracker.Instance.CheckPerfectMediator();
            }
            string name = StoreReputation.GetFactionNameByID(id);
            return LocHelper.GetLocalizedUI(amount > 0 ? "ui_rep_gained" : "ui_rep_lost", name) + " (" + GameReads.RepAmount(amount) + ")";
        }

        public void NightLog(string text, string color) => PlayerStore.Instance.AddNightLog(text, color);

        public void PlaySound(string id) => sounds.Play(id);

        public void PlayItemSound(RunItem item, string fallback) => sounds.PlayItem(item, fallback);

        public bool OpenTrade(TradeRequest request) => counter.Open(request);

        public TradeResult CloseTrade() => counter.Close();

        public List<string> TakeCart(TradeRequest cart)
        {
            var names = new List<string>();
            foreach (GameItem item in counter.TakeCart(cart))
            {
                string name = PutOnGround(item);
                if (name != null) names.Add(name);
            }
            return names;
        }
    }

    /// <summary>
    /// The player facts packs can read as <c>game.NAME</c>, for the area they are read from. Anything else is read through
    /// a pack's own C# effect or condition.
    /// </summary>
    internal static class GameValues
    {
        public static int Get(string name, string areaId)
        {
            string clearsOf = GameReads.ClearsArea(name, areaId);
            if (clearsOf != null) return Clears(clearsOf);
            string faction = GameReads.RepFaction(name, out bool exact);
            if (faction != null) return Reputation(faction, exact);
            switch (name)
            {
                case "luck": return HealthData.GetLuck();
                case "roll_luck": return HealthData.RollLuck() ? 1 : 0;
                case "wounds": return PlayerStore.HasInstance ? PlayerStore.Instance.healthData.woundState : 0;
                case "maimed": return StartingPerk.IsMaimed() ? 1 : 0;
                case "day": return StoreStation.GetDayCounter();
                default: return 0;
            }
        }

        /// <summary>
        /// The player's reputation with a faction: whole as the game shows it, or in hundredths when exact. 0 without a save
        /// or that faction.
        /// </summary>
        public static int Reputation(string faction, bool exact)
        {
            if (!PlayerStore.HasInstance || PlayerStore.Instance.storeReputations == null || !GameReads.IsFaction(faction)) return 0;
            StoreReputation rep = StoreReputation.GetStoreReputation(GameReads.FactionId(faction));
            return rep == null ? 0 : GameReads.RepRead(rep.GetReputationExact(), exact);
        }

        /// <summary>
        /// How often the player has cleared an area. Reads the save's afterhour states directly: GetAfterhourState adds an
        /// empty state to the save for any id it is asked about, so a mistyped game.clears.AREA read on every map refresh
        /// would fill the save with junk entries.
        /// </summary>
        public static int Clears(string areaId)
        {
            Dictionary<string, AfterhourLocationState> states = PlayerStore.HasInstance ? PlayerStore.Instance.afterhourState : null;
            if (states == null || !states.TryGetValue(ExpeditionLocation.LocationId(areaId), out AfterhourLocationState state) || state == null) return 0;
            return state.GetCounter(ExpeditionLocation.ClearsCounter);
        }
    }

    /// <summary>
    /// Pack memory in the player's save, through the game's mod data under the framework's own mod id, so it stays with
    /// that save and lasts across nights. Text names carry a "$" so memory.x and memory.$x never share a key. Mod data
    /// is per save; the mod data folder is shared by every save and would leak memory between them.
    /// </summary>
    internal sealed class SaveMemory : IMemory
    {
        private static string Key(string pack, string name) => "memory:" + pack + ":" + name;

        public int Get(string pack, string name) => ModHelper.GetModDataInt(ExpeditionRun.FrameworkPack, Key(pack, name));
        public void Set(string pack, string name, int value) => ModHelper.SetModDataInt(ExpeditionRun.FrameworkPack, Key(pack, name), value);
        public string GetText(string pack, string name) => ModHelper.GetModData(ExpeditionRun.FrameworkPack, Key(pack, "$" + name), "");
        public void SetText(string pack, string name, string value) => ModHelper.SetModData(ExpeditionRun.FrameworkPack, Key(pack, "$" + name), value ?? "");
    }
}
