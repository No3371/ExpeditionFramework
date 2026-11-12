using System;
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Api;
using ExpeditionFramework.Core;

// One rule of a sim.json bot: when its condition holds, it picks the first offered option it names. A rule that finds
// nothing to pick falls through to the next rule.
internal sealed class BotRule
{
    public string At;                       // "bots.NAME[i]", for messages
    public Value When;                      // null: always
    public List<string> Pick;               // ids in priority order; null when Except is used
    public bool Weakest;                    // only options aimed at the weakest active foe
    public List<string> Except;             // pick the first offered option whose id is not listed
    public int Picked;                      // how often this rule chose, across every row that used the bot
}

// A bot from sim.json: an ordered list of rules, read in the framework expression language. Besides the usual paths
// (self = the player, target = the focused foe, run.*, any_enemy(...), …) a rule reads the decision at hand through val.NAME.
internal sealed class RuleBot
{
    public readonly string Name;
    public readonly List<BotRule> Rules = new List<BotRule>();

    private ExpeditionRun run;
    private KitBridge bridge;
    private List<RunOption> options;
    private Random random;

    public RuleBot(string name) { Name = name; }

    /// <summary>Resolves val.NAME for this bot's rules, or null for a name it doesn't know (which fails the compile).</summary>
    public Value Named(string name)
    {
        switch (name)
        {
            case "loaded": return s => bridge.Loaded;
            case "shots_left": return s => bridge.ShotsLeft;
            case "has_gun": return s => bridge.HasGun ? 1 : 0;
            case "focus_is_weakest": return s => Weakest() == run.Focus ? 1 : 0;
            case "coin": return s => random.Next(2) == 0 ? 1 : 0;   // draws from the run's RNG only when evaluated
        }
        int dot = name.IndexOf('.');
        if (dot <= 0 || dot == name.Length - 1) return null;
        string head = name.Substring(0, dot), arg = name.Substring(dot + 1);
        switch (head)
        {
            case "scene": return s => run.Scene == arg ? 1 : 0;
            case "offered": return s => options.Count(o => o.Interaction.Id == arg);
            case "intent_ends": return s => run.Foes.Count(e => e.Active && e.Intent != null && e.Intent.Id.EndsWith(arg, StringComparison.Ordinal));
            case "foes_with": return s => run.Foes.Count(e => e.Get(arg) > 0);   // every foe of the fight, defeated or gone included
            default: return null;
        }
    }

    /// <summary>The option the first matching rule picks, or null if none picks anything.</summary>
    public RunOption Choose(ExpeditionRun run, KitBridge bridge, List<RunOption> options, Random random)
    {
        this.run = run;
        this.bridge = bridge;
        this.options = options;
        this.random = random;
        var scope = new Scope(run, run.Player, run.Focus, null);
        foreach (BotRule rule in Rules)
        {
            if (rule.When != null && rule.When(scope) == 0) continue;
            RunOption picked = rule.Except != null
                ? options.FirstOrDefault(o => !rule.Except.Contains(o.Interaction.Id))
                : Find(rule);
            if (picked == null) continue;
            rule.Picked++;
            return picked;
        }
        return null;
    }

    // The first offered option per id, in the rule's id order; for a targeted pick, only options aimed at the weakest foe.
    private RunOption Find(BotRule rule)
    {
        Entity weakest = rule.Weakest ? Weakest() : null;
        if (rule.Weakest && weakest == null) return null;
        foreach (string id in rule.Pick)
        {
            RunOption o = options.FirstOrDefault(x => x.Interaction.Id == id && (!rule.Weakest || x.Target == weakest));
            if (o != null) return o;
        }
        return null;
    }

    // Lowest hp among active foes; ties go to the earlier foe.
    private Entity Weakest() => run.Foes.Where(e => e.Active).OrderBy(e => e.Get("hp")).FirstOrDefault();
}
