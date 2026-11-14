using System;
using System.IO;
using System.Linq;

// ef-sim: lints and simulates a pack folder from data the pack carries.
internal static class Program
{
    private static int Main(string[] args)
    {
        // bin/<config>/net8.0 → tools/EfSim → repo root, the same way tests/Sim finds it.
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        if (!File.Exists(Path.Combine(root, Lint.MainFile)))
        {
            Console.WriteLine("ERROR framework not found at " + root);
            return 2;
        }
        string[] rest = args.Skip(1).ToArray();
        switch (args.Length > 0 ? args[0] : "")
        {
            case "lint": return Lint.Run(root, rest);
            case "run": return PackRun.Run(root, rest);
            default:
                Console.WriteLine("usage: ef-sim lint <pack folder> [--mod <id>]... [--strict]");
                Console.WriteLine("       ef-sim run <pack folder> [--runs <n>] [--digest] [--tally]");
                return 2;
        }
    }
}
