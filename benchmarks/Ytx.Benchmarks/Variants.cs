using Ytx.Benchmarks.Subjects;

namespace Ytx.Benchmarks;

/// <summary>Runs one variant end to end: format captions, build the output, serialise, write.</summary>
public static class Variants
{
    public const string Baseline = "baseline";
    public const string Tier1 = "tier1";
    public const string Tier1SourceGen = "tier1sg";
    public const string Tier2 = "tier2";

    public static readonly string[] All = [Baseline, Tier1, Tier1SourceGen, Tier2];

    public static void Run(string variant, Dataset d, bool compact, Stream sink)
    {
        switch (variant)
        {
            case Baseline:
            {
                var (raw, md) = Subjects.Baseline.Format(d.Id, d.Captions);
                Subjects.Baseline.Write(sink, NewOutput(d, raw, md), compact);
                break;
            }
            case Tier1:
            {
                var (raw, md) = Subjects.Tier1.Format(d.Id, d.Captions);
                Subjects.Tier1.Write(sink, NewOutput(d, raw, md), compact);
                break;
            }
            case Tier1SourceGen:
            {
                var (raw, md) = Subjects.Tier1.Format(d.Id, d.Captions);
                Subjects.Tier1.WriteSourceGen(sink, NewOutput(d, raw, md), compact);
                break;
            }
            case Tier2:
            {
                var (raw, md) = Subjects.Tier2.Format(d.Id, d.Captions);
                using (raw)
                using (md)
                {
                    Subjects.Tier2.Write(sink, d.Url, d.Title, d.Description, raw.Span, md.Span, compact);
                }
                break;
            }
            default:
                throw new ArgumentException($"Unknown variant '{variant}'. Expected one of: {string.Join(", ", All)}.");
        }
    }

    public static byte[] RunToBytes(string variant, Dataset d, bool compact)
    {
        using var ms = new MemoryStream();
        Run(variant, d, compact, ms);
        return ms.ToArray();
    }

    static Output NewOutput(Dataset d, string raw, string md) => new()
    {
        url = d.Url,
        title = d.Title,
        description = d.Description,
        transcriptRaw = raw,
        transcript = md,
    };
}
