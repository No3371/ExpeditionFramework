using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ExpeditionFramework.Core;

// A fingerprint of what a row's runs leave behind in the ef-sim's own model of the game, in this order per run: the outcome,
// every run-log line (tone, roll text, text), every night-log line, the memory store at the end of the run (0 and "" count
// as unset), the reputation the run changed, the credits left, the items still carried (sorted id and count) and the ground
// pile in order.
//
// Left out on purpose: run vars (run.*), because a refactor may rename or drop a scratch var without changing anything
// observable and hashing them would flag that intended change as a regression. Also left out: anything the ef-sim's bridge
// does not model, such as the real game's inventory grid, item stats and sounds; a change that only shows there passes.
//
// SHA-256 over the canonical text, not string.GetHashCode: that is randomized per process, so two ef-sim runs could
// never be compared.
internal sealed class RunDigest
{
    private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private int runs;

    public void Add(ExpeditionRun run, List<LogLine> lines, KitBridge bridge, IMemory memory)
    {
        var text = new StringBuilder();
        text.Append("run ").Append(runs++.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(run.Outcome ?? "-").Append('\n');
        foreach (LogLine line in lines) text.Append("L ").Append(line.Tone).Append('\t').Append(line.Roll ?? "").Append('\t').Append(line.Text).Append('\n');
        foreach (string night in bridge.NightLines) text.Append("N ").Append(night).Append('\n');
        if (memory is MemoryStore store)
        {
            foreach (var n in store.Numbers.Where(p => p.Value != 0).OrderBy(p => p.Key.Pack, StringComparer.Ordinal).ThenBy(p => p.Key.Name, StringComparer.Ordinal))
                text.Append("M ").Append(n.Key.Pack).Append('\t').Append(n.Key.Name).Append('\t').Append(n.Value.ToString(CultureInfo.InvariantCulture)).Append('\n');
            foreach (var t in store.Texts.Where(p => p.Value != "").OrderBy(p => p.Key.Pack, StringComparer.Ordinal).ThenBy(p => p.Key.Name, StringComparer.Ordinal))
                text.Append("T ").Append(t.Key.Pack).Append('\t').Append(t.Key.Name).Append('\t').Append(t.Value).Append('\n');
        }
        foreach (KeyValuePair<string, double> rep in bridge.ReputationNow.OrderBy(p => p.Key, StringComparer.Ordinal))
            text.Append("R ").Append(rep.Key).Append('\t').Append(rep.Value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        text.Append("C ").Append(bridge.Credits.ToString(CultureInfo.InvariantCulture)).Append('\n');
        foreach (RunItem item in bridge.Items().OrderBy(i => i.Id, StringComparer.Ordinal).ThenBy(i => i.Count))
            text.Append("I ").Append(item.Id).Append('\t').Append(item.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        foreach (RunItem item in bridge.GroundNow) text.Append("G ").Append(item.Id).Append('\n');
        hash.AppendData(Encoding.UTF8.GetBytes(text.ToString()));
    }

    public string Hex() => BitConverter.ToString(hash.GetHashAndReset()).Replace("-", "").ToLowerInvariant();
}
