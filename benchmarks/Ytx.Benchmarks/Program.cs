using System.Diagnostics;
using BenchmarkDotNet.Running;

namespace Ytx.Benchmarks;

public static class Program
{
    const string Usage = """
        Ytx.Benchmarks: measure and verify ytx's caption formatting and JSON output.

        Usage:
          dotnet run -c Release -- generate [--seed N]           Write synthetic datasets to data/synthetic/
          dotnet run -c Release -- capture [-l LANG] ID_OR_URL... Fetch real captions into data/captured/ (gitignored)
          dotnet run -c Release -- verify                        Check every variant is byte-identical to Baseline
          dotnet run -c Release -- replay VARIANT FILE           One cold run in this process (pipe stdout to /dev/null)
          dotnet run -c Release -- [BenchmarkDotNet args]        Run benchmarks, e.g. --filter '*EndToEnd*'

        Options:
          --data DIR   Dataset folder (default: data/ next to the .csproj, or $YTX_BENCH_DATA)

        Variants: baseline, tier1, tier1sg, tier2
        """;

    public static async Task<int> Main(string[] args)
    {
        var list = args.ToList();
        string? dataOverride = TakeOption(list, "--data");
        var dataDir = DataDir.Resolve(dataOverride);
        var command = list.Count > 0 ? list[0] : "";

        switch (command)
        {
            case "-h" or "--help" or "help":
                Console.WriteLine(Usage);
                return 0;

            case "generate":
            {
                var seed = TakeOption(list, "--seed");
                return Synthetic.WriteAll(dataDir, seed == null ? 42 : int.Parse(seed));
            }

            case "capture":
            {
                var language = TakeOption(list, "-l") ?? TakeOption(list, "--language") ?? "English";
                if (list.Count < 2)
                {
                    Console.Error.WriteLine("capture needs at least one video ID or URL.");
                    return 2;
                }
                return await Capture.RunAsync(dataDir, list.Skip(1).ToList(), language);
            }

            case "verify":
                Synthetic.EnsureExists(dataDir);
                return Verify(dataDir);

            case "replay":
                if (list.Count < 3)
                {
                    Console.Error.WriteLine("replay needs VARIANT and FILE.");
                    return 2;
                }
                return Replay(list[1], list[2]);

            default:
                Synthetic.EnsureExists(dataDir);
                BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(list.ToArray());
                return 0;
        }
    }

    /// <summary>
    /// Every variant must produce exactly the bytes v1.0.7 would, for every dataset, with and without
    /// the edge-case captions, in indented and compact mode. Returns the number of mismatches.
    /// </summary>
    static int Verify(string dataDir)
    {
        int bad = 0, checks = 0;
        foreach (var file in DataDir.AllDatasets(dataDir))
        {
            var original = Dataset.Load(file);
            var name = Path.GetRelativePath(dataDir, file);
            foreach (var d in new[] { original, original.WithEdgeCases() })
            foreach (var compact in new[] { false, true })
            {
                var expected = Variants.RunToBytes(Variants.Baseline, d, compact);
                foreach (var v in Variants.All.Where(v => v != Variants.Baseline))
                {
                    var actual = Variants.RunToBytes(v, d, compact);
                    bool same = expected.AsSpan().SequenceEqual(actual);
                    checks++;
                    if (!same) bad++;
                    Console.WriteLine($"{(same ? "IDENTICAL" : "DIFF     ")} {v,-8} {name} cues={d.Captions.Length} compact={compact} bytes={expected.Length}{(same ? "" : $" vs {actual.Length}")}");
                }
            }
        }

        Console.WriteLine(bad == 0 ? $"OK: {checks} checks, all identical." : $"FAILED: {bad} of {checks} checks differ.");
        return bad == 0 ? 0 : 1;
    }

    // A cold, one-shot run in a fresh process: includes JIT and serialiser warm-up, which is what a CLI pays.
    static int Replay(string variant, string file)
    {
        var d = Dataset.Load(file);
        var sw = Stopwatch.StartNew();
        long before = GC.GetTotalAllocatedBytes(true);
        using (var stdout = Console.OpenStandardOutput())
        {
            Variants.Run(variant, d, false, stdout);
        }
        long after = GC.GetTotalAllocatedBytes(true);
        Console.Error.WriteLine(
            $"{variant}\t{sw.Elapsed.TotalMilliseconds:F1} ms\t{(after - before) / 1048576.0:F2} MB\t" +
            $"gen0={GC.CollectionCount(0)} gen1={GC.CollectionCount(1)} gen2={GC.CollectionCount(2)}");
        return 0;
    }

    static string? TakeOption(List<string> args, string name)
    {
        int i = args.IndexOf(name);
        if (i < 0 || i + 1 >= args.Count) return null;
        var value = args[i + 1];
        args.RemoveRange(i, 2);
        return value;
    }
}
