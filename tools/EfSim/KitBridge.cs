using System;
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Core;

// The game's side of a run, faked from a kit: carried items, credits, and a ground pile for what the run gives.
// Game values, trading, take-back order and carried losses follow the pack's sim.json "bridge" settings.
internal sealed class KitBridge : IBridge
{
    private readonly SimBridge settings;
    private readonly Random random;
    private readonly HashSet<string> problems;
    private readonly List<RunItem> items = new List<RunItem>();
    private readonly List<RunItem> ground = new List<RunItem>();

    public KitBridge(SimKit kit, SimBridge settings, Content content, Random random, HashSet<string> problems, IMemory memory = null)
    {
        this.settings = settings;
        this.random = random;
        this.problems = problems;
        Memory = memory ?? new MemoryStore();
        Credits = kit.Credits;
        foreach (SimItem s in kit.Items)
        {
            var item = new RunItem { Handle = s.Id, Id = s.Id, Name = s.Id, Count = s.Count };
            item.Tags.UnionWith(s.Tags);
            item.ApplyStats(content);                                        // pack data first…
            foreach (KeyValuePair<string, int> p in s.Props) item.Props[p.Key] = p.Value;  // …kit props win
            items.Add(item);
        }
    }

    // What a bot can see of its gear, worked out as NightExpeditionsReborn's sim does.
    public bool HasGun => items.Any(i => i.Tags.Contains("firearm"));
    public int Loaded => items.Where(i => i.Tags.Contains("firearm")).Sum(i => i.Get("loaded"));
    public int ShotsLeft => Loaded + items.Where(i => i.Tags.Contains("ammo")).Sum(i => i.Count * Math.Max(1, i.Get("rounds")) - i.Get("spent"));

    public IReadOnlyList<RunItem> Items() => items;
    public void Consume(RunItem item, int count) { item.Count -= count; if (item.Count <= 0) items.Remove(item); }

    public string GiveItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        ground.Add(new RunItem { Handle = itemId + "#" + ground.Count, Id = itemId, Name = itemId });
        return itemId;
    }

    public string RollTable(string table) => table;
    public bool Fits(RunItem gun, RunItem round) => true;
    public bool Equip(RunItem item) { item.Tags.Add("in_hand"); return true; }
    public int Credits { get; private set; }
    public void AddCredits(int amount) => Credits += amount;
    public bool SpendCredits(int amount) { if (Credits < amount) return false; Credits -= amount; return true; }
    public int GroundCount => ground.Count;
    public int ClearGround() { int n = ground.Count; ground.Clear(); return n; }

    public string TakeBack()
    {
        if (ground.Count == 0) return null;
        int at = settings.TakeOldest ? 0 : ground.Count - 1;
        RunItem taken = ground[at];
        ground.RemoveAt(at);
        return taken.Name;
    }

    public void LoseFound() => ground.Clear();

    // A fixed list stands in for the loss without rolling or removing anything, so it draws nothing from the run's random.
    public List<string> LoseCarried(Func<int, int> chanceForSize)
    {
        if (settings.LoseCarried != null) return new List<string>(settings.LoseCarried);
        var lost = new List<string>();
        foreach (RunItem item in items.ToList())
        {
            if (random.Next(100) >= chanceForSize(Math.Max(1, item.Get("size")))) continue;
            items.Remove(item);
            lost.Add(item.Name);
        }
        return lost;
    }

    public int GameValue(string name) =>
        reputation.TryGetValue(name, out int changed) ? changed
        : settings.CoinGame.Contains(name) ? random.Next(2) : settings.Game.TryGetValue(name, out int value) ? value : 0;
    // Reputation this run changed, by game.NAME: later reads see it, as in the game. Every faction exists, clamped as the game clamps.
    private readonly Dictionary<string, int> reputation = new Dictionary<string, int>();
    public string Reputation(string faction, int amount)
    {
        if (amount == 0 || !GameReads.IsFaction(faction)) return null;
        string name = "rep." + faction;
        reputation[name] = Math.Max(-200, Math.Min(200, GameValue(name) + amount));
        return "Reputation " + (amount > 0 ? "gained" : "lost") + " with " + faction + " (" + (amount > 0 ? "+" : "") + amount + ")";
    }
    // The row's store when sim.json gives the row memory; otherwise an empty one for this run alone.
    public IMemory Memory { get; }
    public void Wound(string kind) { }
    public void NightLog(string text, string color) { if (text != null && text.Contains("{")) problems.Add("unfilled night log: " + text); }
    public void PlaySound(string id) { }
    public void PlayItemSound(RunItem item, string fallback) { }
    public bool OpenTrade(TradeRequest request) => settings.TradeOpens;
    public TradeResult CloseTrade() => new TradeResult { CashChange = settings.LoseCashUpTo is int most ? -random.Next(0, most + 1) : 0 };
    public List<string> TakeCart(TradeRequest cart) => new List<string>(settings.Cart);
}
