using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ytx.Benchmarks;

public sealed record Cap(string Text, TimeSpan Offset, TimeSpan Duration);

// Same shape and property names as the Output class in src/Ytx/Program.cs (v1.0.7).
public class Output
{
    public string url { get; set; } = "";
    public string title { get; set; } = "";
    public string description { get; set; } = "";
    public string transcriptRaw { get; set; } = "";
    public string transcript { get; set; } = "";
}

[JsonSerializable(typeof(Output))]
public partial class OutputContext : JsonSerializerContext { }

/// <summary>
/// One video's metadata and caption track, as captured from YouTube or generated synthetically.
/// File format: {"id","url","title","description","captions":[{"text","offsetTicks","durationTicks"}]}.
/// </summary>
public sealed record Dataset(string Id, string Url, string Title, string Description, Cap[] Captions)
{
    public static Dataset Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
        var r = doc.RootElement;
        var caps = r.GetProperty("captions").EnumerateArray()
            .Select(c => new Cap(
                c.GetProperty("text").GetString()!,
                TimeSpan.FromTicks(c.GetProperty("offsetTicks").GetInt64()),
                c.TryGetProperty("durationTicks", out var d) ? TimeSpan.FromTicks(d.GetInt64()) : TimeSpan.Zero))
            .ToArray();
        return new Dataset(
            r.GetProperty("id").GetString()!,
            r.GetProperty("url").GetString()!,
            r.GetProperty("title").GetString()!,
            r.GetProperty("description").GetString()!,
            caps);
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var fs = File.Create(path);
        using var w = new Utf8JsonWriter(fs);
        w.WriteStartObject();
        w.WriteString("id", Id);
        w.WriteString("url", Url);
        w.WriteString("title", Title);
        w.WriteString("description", Description);
        w.WriteStartArray("captions");
        foreach (var c in Captions)
        {
            w.WriteStartObject();
            w.WriteString("text", c.Text);
            w.WriteNumber("offsetTicks", c.Offset.Ticks);
            w.WriteNumber("durationTicks", c.Duration.Ticks);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    /// <summary>Adds captions that exercise every normalisation edge case the variants must agree on.</summary>
    public Dataset WithEdgeCases() => this with
    {
        Captions =
        [
            .. Captions,
            new Cap("  a\t\tb &nbsp; ", TimeSpan.FromHours(101.5), TimeSpan.Zero),
            new Cap("&nbsp;", TimeSpan.Zero, TimeSpan.Zero),
            new Cap("x\u00A0\u2028y\u0085 z\u3000", TimeSpan.FromSeconds(59.9), TimeSpan.Zero),
            new Cap("caf\u00E9 \U0001F600 na\u00EFve", TimeSpan.FromSeconds(3599.99), TimeSpan.Zero),
            new Cap("\n", TimeSpan.FromSeconds(1), TimeSpan.Zero),
        ],
    };
}

public static class DataDir
{
    public const string EnvVar = "YTX_BENCH_DATA";

    public static bool IsDefault { get; private set; }

    /// <summary>
    /// Resolves the data folder once and exports it as an absolute path, because BenchmarkDotNet
    /// runs each benchmark in a child process with a different working directory.
    /// </summary>
    public static string Resolve(string? overridePath = null)
    {
        var dir = overridePath ?? Environment.GetEnvironmentVariable(EnvVar);
        IsDefault = dir == null;
        dir ??= FindDefault();
        dir = Path.GetFullPath(dir);
        Environment.SetEnvironmentVariable(EnvVar, dir);
        return dir;
    }

    public static string Current => Environment.GetEnvironmentVariable(EnvVar) ?? Resolve();

    public static IEnumerable<string> AllDatasets(string dir) =>
        Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            : [];

    // Walk up from the build output to the folder that holds the .csproj, then use its data/ folder.
    static string FindDefault()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            if (File.Exists(Path.Combine(d.FullName, "Ytx.Benchmarks.csproj")))
                return Path.Combine(d.FullName, "data");
        }
        return Path.Combine(Directory.GetCurrentDirectory(), "data");
    }
}
