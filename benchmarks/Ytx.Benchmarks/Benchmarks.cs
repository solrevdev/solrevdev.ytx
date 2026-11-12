using BenchmarkDotNet.Attributes;
using Ytx.Benchmarks.Subjects;

namespace Ytx.Benchmarks;

/// <summary>Datasets are discovered at run time: data/synthetic/*.json plus anything in data/captured/.</summary>
public abstract class DatasetBench
{
    public static IEnumerable<string> Datasets()
    {
        var dir = DataDir.Current;
        return DataDir.AllDatasets(dir).Select(f => Path.GetRelativePath(dir, f).Replace('\\', '/'));
    }

    [ParamsSource(nameof(Datasets))]
    public string Source { get; set; } = "";

    protected Dataset Data = null!;

    [GlobalSetup]
    public void Setup() => Data = Dataset.Load(Path.Combine(DataDir.Current, Source));
}

/// <summary>Caption formatting only: normalise, build the raw and Markdown strings.</summary>
[MemoryDiagnoser]
public class FormatBench : DatasetBench
{
    [Benchmark(Baseline = true)]
    public object Baseline_Format() => Baseline.Format(Data.Id, Data.Captions);

    [Benchmark]
    public object Tier1_Format() => Tier1.Format(Data.Id, Data.Captions);

    [Benchmark]
    public int Tier2_Format()
    {
        var (raw, md) = Tier2.Format(Data.Id, Data.Captions);
        int n = raw.Length + md.Length;
        raw.Dispose();
        md.Dispose();
        return n;
    }
}

/// <summary>Everything ytx does after the network: format, serialise and write (to Stream.Null).</summary>
[MemoryDiagnoser]
public class EndToEndBench : DatasetBench
{
    [Benchmark(Baseline = true)]
    public void Baseline_All() => Variants.Run(Variants.Baseline, Data, false, Stream.Null);

    [Benchmark]
    public void Tier1_All() => Variants.Run(Variants.Tier1, Data, false, Stream.Null);

    [Benchmark]
    public void Tier1SourceGen_All() => Variants.Run(Variants.Tier1SourceGen, Data, false, Stream.Null);

    [Benchmark]
    public void Tier2_All() => Variants.Run(Variants.Tier2, Data, false, Stream.Null);
}

/// <summary>NormalizeCaption over every cue of the largest dataset.</summary>
[MemoryDiagnoser]
public class NormalizeBench
{
    string[] _texts = [];

    [GlobalSetup]
    public void Setup()
    {
        var largest = DataDir.AllDatasets(DataDir.Current).MaxBy(f => new FileInfo(f).Length)
            ?? throw new InvalidOperationException("No datasets found. Run the 'generate' command first.");
        _texts = Dataset.Load(largest).Captions.Select(c => c.Text).ToArray();
    }

    [Benchmark(Baseline = true)]
    public int Baseline_Normalize()
    {
        int n = 0;
        foreach (var t in _texts) n += Baseline.NormalizeCaption(t).Length;
        return n;
    }

    [Benchmark]
    public int Tier1_Normalize()
    {
        int n = 0;
        foreach (var t in _texts) n += Tier1.NormalizeCaption(t).Length;
        return n;
    }
}
