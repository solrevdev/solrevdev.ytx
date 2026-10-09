using System.Text;

namespace Ytx.Benchmarks;

/// <summary>
/// Generates caption tracks shaped like real YouTube auto-generated captions, so the benchmarks run
/// offline and the repository holds no third-party transcript text.
///
/// Shape measured from two real auto-caption tracks (17 min and 5 h 15 min):
/// - Cues alternate between a text line and a cue whose text is a single "\n" (rolling captions).
///   About half of all cues are that whitespace-only filler.
/// - Text cues average 34-37 characters, about 7 words of about 4.3 characters, with no extra spaces.
/// - A text cue starts about 10 ms after the previous filler cue. Durations are about 3-8 s.
/// The words are nonsense built from a fixed syllable list, so the output is deterministic for a seed.
/// </summary>
public static class Synthetic
{
    public sealed record Preset(string Name, string Id, int Cues, TimeSpan Length);

    // Cue counts and lengths match the two videos used in the original measurements.
    public static readonly Preset[] Presets =
    [
        new("short", "synthShort1", 924, TimeSpan.FromSeconds(1004)),
        new("long", "synthLong01", 16284, TimeSpan.FromSeconds(18940)),
    ];

    static readonly string[] Syllables =
    [
        "ka", "lo", "ri", "ten", "mar", "so", "vel", "an", "di", "po", "ques", "tra", "mi", "nor", "el",
        "ba", "sun", "ti", "ro", "fen", "da", "lu", "ger", "o", "is", "pa", "the", "wen", "at", "cy",
    ];

    public static Dataset Generate(Preset p, int seed = 42)
    {
        var rng = new Random(seed);
        var caps = new List<Cap>(p.Cues);
        double step = p.Length.TotalSeconds / Math.Max(1, p.Cues / 2);
        double t = 0.16;

        for (int i = 0; i < p.Cues; i++)
        {
            if (i % 2 == 0)
            {
                caps.Add(new Cap(Line(rng), TimeSpan.FromSeconds(t), TimeSpan.FromSeconds(3 + rng.NextDouble() * 5)));
            }
            else
            {
                // Rolling-caption filler: whitespace only, normalised away by every variant.
                double filler = t + step * (0.95 + rng.NextDouble() * 0.1);
                caps.Add(new Cap("\n", TimeSpan.FromSeconds(filler), TimeSpan.FromSeconds(1.5 + rng.NextDouble() * 3)));
                t = filler + 0.01;
            }
        }

        var description = new StringBuilder();
        for (int i = 0; i < 12; i++) description.Append(Line(rng)).Append(i % 3 == 2 ? "\n\n" : " ");
        // Characters that exercise the relaxed encoder: emoji (surrogate pairs), accents, dashes, quotes.
        description.Append("\U0001F916 caf\u00E9 \u2014 \"quoted\" <tag> & https://example.com/?a=1&b=2");

        return new Dataset(
            Id: p.Id,
            Url: $"https://www.youtube.com/watch?v={p.Id}",
            Title: $"Synthetic {p.Name} caption track ({p.Cues} cues)",
            Description: description.ToString(),
            Captions: caps.ToArray());
    }

    static string Line(Random rng)
    {
        var sb = new StringBuilder(40);
        int words = 5 + rng.Next(5);
        for (int w = 0; w < words; w++)
        {
            if (w > 0) sb.Append(' ');
            int syllables = 1 + rng.Next(3);
            for (int s = 0; s < syllables; s++) sb.Append(Syllables[rng.Next(Syllables.Length)]);
        }
        return sb.ToString();
    }

    public static int WriteAll(string dataDir, int seed)
    {
        var dir = Path.Combine(dataDir, "synthetic");
        foreach (var p in Presets)
        {
            var path = Path.Combine(dir, p.Name + ".json");
            var d = Generate(p, seed);
            d.Save(path);
            int text = d.Captions.Count(c => !string.IsNullOrWhiteSpace(c.Text));
            Console.WriteLine($"wrote {path}  cues={d.Captions.Length} text-cues={text} last={d.Captions[^1].Offset:hh\\:mm\\:ss}");
        }
        return 0;
    }

    /// <summary>
    /// Creates the default synthetic files if any are missing. Only touches the default data folder:
    /// a folder passed with --data or YTX_BENCH_DATA is used exactly as given.
    /// </summary>
    public static void EnsureExists(string dataDir)
    {
        if (DataDir.IsDefault && !Presets.All(p => File.Exists(Path.Combine(dataDir, "synthetic", p.Name + ".json"))))
            WriteAll(dataDir, 42);
    }
}
