using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ExpeditionFramework.Core;

// Checks one pack folder offline: what the game would report on load, missing English text, and other locales' parity.
// It loads the framework and that pack only, so it fits a pack that depends on nothing but the framework; --mod answers
// mod(...) checks and include requirements without loading the named mod, and lets memory.MOD.NAME read it.
internal static class Lint
{
    internal const string Framework = "expedition_framework";
    internal const string MainFile = "ExpeditionFramework/main.json";
    private const string Usage = "usage: ef-sim lint <pack folder> [--mod <id>]... [--strict]";

    public static int Run(string frameworkRoot, string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("--"))
        {
            Console.WriteLine(Usage);
            return 2;
        }
        string folder = Path.GetFullPath(args[0]);
        var mods = new HashSet<string>();
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--mod" && i + 1 < args.Length) mods.Add(args[++i]);
            else if (args[i] != "--strict")
            {
                Console.WriteLine("unknown argument " + args[i]);
                Console.WriteLine(Usage);
                return 2;
            }
        }
        bool strict = args.Contains("--strict");

        string id = ReadId(folder);
        if (id == null)
        {
            Console.WriteLine("ERROR no <ID> in manifest.xml");
            return 2;
        }
        Load(frameworkRoot, folder, id, mods, out Dictionary<string, Dictionary<string, string>> text, out List<string> errors, out List<string> notes);
        Dictionary<string, string> en = text[id];

        string localization = Path.Combine(folder, "Localization");
        if (Directory.Exists(localization))
        {
            foreach (string file in Directory.GetFiles(localization, "*.csv").OrderBy(f => f))
            {
                string name = Path.GetFileName(file);
                if (name == "en.csv") continue;
                notes.AddRange(ParityProblems(en, ReadCsv(file), name));
            }
        }

        foreach (string error in errors) Console.WriteLine("ERROR " + error);
        foreach (string note in notes) Console.WriteLine("WARN " + note);
        Console.WriteLine("lint " + id + ": " + errors.Count + " error(s), " + notes.Count + " warning(s)");
        return errors.Count > 0 || (strict && notes.Count > 0) ? 1 : 0;
    }

    // The pack's <ID> from manifest.xml, or null.
    internal static string ReadId(string folder)
    {
        string manifest = Path.Combine(folder, "manifest.xml");
        Match idMatch = File.Exists(manifest) ? Regex.Match(File.ReadAllText(manifest), @"<ID>\s*([^<\s]+)\s*</ID>") : Match.Empty;
        return idMatch.Success ? idMatch.Groups[1].Value : null;
    }

    // Compiles the framework and the pack as the game would. errors: load problems, then text keys missing from en.csv;
    // notes: warnings. text: en.csv rows by pack (the pack and the framework). The mods and memoryPacks stand in as installed
    // packs whose memory the pack may read; what they write is unknown, so those reads never warn.
    internal static Content Load(string frameworkRoot, string folder, string id, ISet<string> mods,
        out Dictionary<string, Dictionary<string, string>> text, out List<string> errors, out List<string> notes, IEnumerable<string> memoryPacks = null)
    {
        // In the game the framework and the pack itself are always installed, so mod() answers yes for them too.
        var set = new PackSet(new Registry())
        {
            HasMod = mod => mod == Framework || mod == id || mods.Contains(mod),
            OtherPacks = new HashSet<string>(mods.Concat(memoryPacks ?? Enumerable.Empty<string>())),
        };
        if (id != Framework) set.Add(Framework, MainFile, file => File.ReadAllText(Path.Combine(frameworkRoot, file)));
        set.Add(id, MainFile, file => File.ReadAllText(Path.Combine(folder, file)));
        Content content = set.Compile(out Dictionary<string, List<string>> problems, out Dictionary<string, List<string>> warnings);

        errors = new List<string>();
        notes = new List<string>();
        foreach (List<string> list in problems.Values) errors.AddRange(list);
        foreach (List<string> list in warnings.Values) notes.AddRange(list);

        text = new Dictionary<string, Dictionary<string, string>> { [id] = ReadCsv(Path.Combine(folder, "Localization", "en.csv")) };
        if (id != Framework) text[Framework] = ReadCsv(Path.Combine(frameworkRoot, "Localization", "en.csv"));
        foreach ((string pack, string key) in content.TextKeys.OrderBy(k => k.Pack).ThenBy(k => k.Key))
        {
            if (text.TryGetValue(pack, out Dictionary<string, string> rows) && !rows.ContainsKey(key)) errors.Add("missing text " + pack + ":" + key);
        }
        return content;
    }

    // Key → text. Skips blank lines and lines starting with '#'; splits on the first comma, like the sims. A missing file has no rows.
    internal static Dictionary<string, string> ReadCsv(string file)
    {
        var rows = new Dictionary<string, string>();
        if (!File.Exists(file)) return rows;
        foreach (string line in File.ReadAllLines(file))
        {
            if (line.Trim().Length == 0 || line.StartsWith("#")) continue;
            int comma = line.IndexOf(',');
            if (comma > 0) rows[line.Substring(0, comma)] = line.Substring(comma + 1);
        }
        return rows;
    }

    private static readonly Regex Placeholder = new Regex(@"\{\d+\}");

    // Keys missing from or extra in another locale, and keys whose {n} placeholders differ from English.
    internal static List<string> ParityProblems(Dictionary<string, string> en, Dictionary<string, string> other, string file)
    {
        var result = new List<string>();
        foreach (string key in en.Keys.Where(k => !other.ContainsKey(k))) result.Add(file + ": " + key + " is missing (en.csv has it)");
        foreach (string key in other.Keys.Where(k => !en.ContainsKey(k))) result.Add(file + ": " + key + " is not in en.csv");
        foreach (string key in en.Keys.Where(other.ContainsKey))
        {
            var want = new HashSet<string>(Placeholder.Matches(en[key]).Select(m => m.Value));
            var have = new HashSet<string>(Placeholder.Matches(other[key]).Select(m => m.Value));
            if (!want.SetEquals(have))
            {
                result.Add(file + ": " + key + " has placeholders " + Listed(have) + " but en.csv has " + Listed(want));
            }
        }
        return result;
    }

    private static string Listed(HashSet<string> placeholders) => placeholders.Count == 0 ? "none" : string.Join(" ", placeholders.OrderBy(p => p));
}
