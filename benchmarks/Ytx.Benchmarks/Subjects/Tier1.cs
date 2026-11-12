using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ytx.Benchmarks.Subjects;

/// <summary>
/// Tier 1: small, readable changes that keep the output byte-identical to v1.0.7.
/// 1. Skip the regex when a caption is already in normal form.
/// 2. Format numbers straight into the StringBuilder instead of building temporary strings.
/// 3. Serialise straight to the output stream (reflection or source-generated metadata).
/// </summary>
public static partial class Tier1
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    public static (string Raw, string Markdown) Format(string videoId, IReadOnlyList<Cap> captions)
    {
        var rawSb = new StringBuilder();
        var mdSb = new StringBuilder();

        foreach (var caption in captions)
        {
            var text = NormalizeCaption(caption.Text);
            if (string.IsNullOrWhiteSpace(text)) continue;

            // v1.0.7 used AppendLine then TrimEnd, so the separator is Environment.NewLine.
            if (rawSb.Length > 0)
            {
                rawSb.Append(' ');
                mdSb.Append(Environment.NewLine);
            }
            rawSb.Append(text);

            // The StringBuilder interpolation handler formats the numbers in place.
            var ts = caption.Offset;
            int h = (int)ts.TotalHours;
            if (h > 0) mdSb.Append($"- [{h:00}:{ts.Minutes:00}:{ts.Seconds:00}");
            else mdSb.Append($"- [{ts.Minutes:00}:{ts.Seconds:00}");
            mdSb.Append($"](https://www.youtube.com/watch?v={videoId}&t={(int)ts.TotalSeconds}s) ").Append(text);
        }

        return (rawSb.ToString().Trim(), mdSb.ToString().TrimEnd());
    }

    public static string NormalizeCaption(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        // \s+ matches every ordinary single space, so Regex.Replace returns a new string for nearly
        // every caption even when nothing changes. Only run it when the text is not already normal.
        if (NeedsNormalizing(text)) text = Whitespace().Replace(text, " ").Trim();
        return text.Contains("&nbsp;") ? text.Replace("&nbsp;", " ") : text;
    }

    static bool NeedsNormalizing(ReadOnlySpan<char> t)
    {
        if (char.IsWhiteSpace(t[0]) || char.IsWhiteSpace(t[^1])) return true;
        for (int i = 1; i < t.Length; i++)
        {
            char c = t[i];
            if (c == ' ')
            {
                if (t[i - 1] == ' ') return true;
            }
            else if (char.IsWhiteSpace(c))
            {
                return true;
            }
        }
        return false;
    }

    static readonly byte[] NewLine = Encoding.UTF8.GetBytes(Environment.NewLine);

    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    static readonly JsonSerializerOptions Compact = new() { WriteIndented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Reflection-based serialiser, written as UTF-8 to the stream with no intermediate string.</summary>
    public static void Write(Stream stdout, Output output, bool compact)
    {
        JsonSerializer.Serialize(stdout, output, compact ? Compact : Indented);
        stdout.Write(NewLine);
        stdout.Flush();
    }

    static readonly OutputContext IndentedContext = new(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    static readonly OutputContext CompactContext = new(new JsonSerializerOptions { WriteIndented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    /// <summary>Source-generated metadata: trim and AOT safe, and skips reflection warm-up.</summary>
    public static void WriteSourceGen(Stream stdout, Output output, bool compact)
    {
        JsonSerializer.Serialize(stdout, output, (compact ? CompactContext : IndentedContext).Output);
        stdout.Write(NewLine);
        stdout.Flush();
    }
}
