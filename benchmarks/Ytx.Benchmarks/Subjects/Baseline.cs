using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ytx.Benchmarks.Subjects;

/// <summary>
/// The formatting and output code from ytx v1.0.7 (commit 07d0e12, src/Ytx/Program.cs), kept as the
/// reference every other variant must match byte for byte. Do not "improve" this file.
/// The only change is that output goes to a supplied stream instead of Console.Out.
/// </summary>
public static class Baseline
{
    public static (string Raw, string Markdown) Format(string videoId, IReadOnlyList<Cap> captions)
    {
        var rawSb = new StringBuilder();
        var mdSb = new StringBuilder();

        foreach (var caption in captions)
        {
            var text = NormalizeCaption(caption.Text);
            if (string.IsNullOrWhiteSpace(text)) continue;

            if (rawSb.Length > 0) rawSb.Append(' ');
            rawSb.Append(text);

            var timestamp = ToHhMmSs(caption.Offset);
            var link = $"https://www.youtube.com/watch?v={videoId}&t={(int)caption.Offset.TotalSeconds}s";
            mdSb.AppendLine($"- [{timestamp}]({link}) {text}");
        }

        return (rawSb.ToString().Trim(), mdSb.ToString().TrimEnd());
    }

    /// <summary>Mirrors <c>JsonSerializer.Serialize</c> to a string followed by <c>Console.WriteLine</c>.</summary>
    public static void Write(Stream stdout, Output output, bool compact)
    {
        var json = JsonSerializer.Serialize(output, new JsonSerializerOptions
        {
            WriteIndented = !compact,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        // Console.OutputEncoding = UTF8 writes no BOM, and WriteLine appends Environment.NewLine.
        using var writer = new StreamWriter(stdout, new UTF8Encoding(false), leaveOpen: true);
        writer.WriteLine(json);
        writer.Flush();
    }

    public static string ToHhMmSs(TimeSpan ts)
    {
        int h = (int)ts.TotalHours;
        int m = ts.Minutes;
        int s = ts.Seconds;
        return h > 0 ? $"{h:00}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
    }

    public static string NormalizeCaption(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text.Replace("&nbsp;", " ");
    }
}
