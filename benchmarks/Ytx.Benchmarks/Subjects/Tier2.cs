using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Ytx.Benchmarks.Subjects;

/// <summary>
/// Tier 2: pooled buffers, hand-written digit formatting and a chunked Utf8JsonWriter.
/// Kept as a measuring stick for "how far can this go". The write-up recommends against shipping it:
/// it saves about 3 ms more than Tier 1 on a 5-hour video and is much harder to keep correct.
/// </summary>
public static class Tier2
{
    /// <summary>A growable pooled char buffer (a minimal ValueStringBuilder that can live on the heap).</summary>
    public sealed class CharBuffer : IDisposable
    {
        char[] _array;
        int _length;

        public CharBuffer(int capacity) => _array = ArrayPool<char>.Shared.Rent(capacity);

        public int Length => _length;
        public ReadOnlySpan<char> Span => _array.AsSpan(0, _length);

        Span<char> Reserve(int needed)
        {
            if (_length + needed > _array.Length)
            {
                var bigger = ArrayPool<char>.Shared.Rent(Math.Max(_array.Length * 2, _length + needed));
                _array.AsSpan(0, _length).CopyTo(bigger);
                ArrayPool<char>.Shared.Return(_array);
                _array = bigger;
            }
            return _array.AsSpan(_length);
        }

        public void Append(char c)
        {
            Reserve(1)[0] = c;
            _length++;
        }

        public void Append(ReadOnlySpan<char> s)
        {
            s.CopyTo(Reserve(s.Length));
            _length += s.Length;
        }

        public void AppendTwoDigits(int v)
        {
            var d = Reserve(2);
            d[0] = (char)('0' + v / 10);
            d[1] = (char)('0' + v % 10);
            _length += 2;
        }

        public void AppendInt(int v)
        {
            v.TryFormat(Reserve(11), out int written);
            _length += written;
        }

        // Matches the "00" format specifier: at least two digits, more if needed (hours above 99).
        public void AppendPadded(int v)
        {
            if (v < 100) AppendTwoDigits(v);
            else AppendInt(v);
        }

        public void Dispose()
        {
            ArrayPool<char>.Shared.Return(_array);
            _array = [];
        }
    }

    // Collapse runs of whitespace to one space and trim both ends, writing into dst. Returns the length.
    static int Normalize(ReadOnlySpan<char> src, Span<char> dst)
    {
        int n = 0;
        bool pendingSpace = false;
        foreach (var c in src)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = n > 0;
                continue;
            }
            if (pendingSpace)
            {
                dst[n++] = ' ';
                pendingSpace = false;
            }
            dst[n++] = c;
        }
        return n;
    }

    public static (CharBuffer Raw, CharBuffer Markdown) Format(string videoId, IReadOnlyList<Cap> captions)
    {
        int estimate = 0;
        foreach (var c in captions) estimate += c.Text.Length;

        var raw = new CharBuffer(estimate + captions.Count);
        var md = new CharBuffer(estimate + captions.Count * (60 + videoId.Length));
        char[] scratch = ArrayPool<char>.Shared.Rent(256);
        var newLine = Environment.NewLine.AsSpan();

        foreach (var caption in captions)
        {
            var src = caption.Text.AsSpan();
            if (src.Length > scratch.Length)
            {
                ArrayPool<char>.Shared.Return(scratch);
                scratch = ArrayPool<char>.Shared.Rent(src.Length);
            }

            ReadOnlySpan<char> text = scratch.AsSpan(0, Normalize(src, scratch));
            // Trap: v1.0.7 replaces &nbsp; after collapsing whitespace, so "a &nbsp; b" keeps three spaces.
            // Fall back to the legacy code for that rare case instead of re-implementing the quirk.
            if (text.IndexOf("&nbsp;") >= 0) text = Baseline.NormalizeCaption(caption.Text);
            if (text.IsWhiteSpace()) continue;

            if (raw.Length > 0)
            {
                raw.Append(' ');
                md.Append(newLine);
            }
            raw.Append(text);

            var ts = caption.Offset;
            int h = (int)ts.TotalHours;
            md.Append("- [");
            if (h > 0)
            {
                md.AppendPadded(h);
                md.Append(':');
            }
            md.AppendTwoDigits(ts.Minutes);
            md.Append(':');
            md.AppendTwoDigits(ts.Seconds);
            md.Append("](https://www.youtube.com/watch?v=");
            md.Append(videoId);
            md.Append("&t=");
            md.AppendInt((int)ts.TotalSeconds);
            md.Append("s) ");
            md.Append(text);
        }

        ArrayPool<char>.Shared.Return(scratch);
        return (raw, md);
    }

    static readonly JsonWriterOptions IndentedWriter = new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    static readonly JsonWriterOptions CompactWriter = new() { Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    static readonly byte[] NewLine = Encoding.UTF8.GetBytes(Environment.NewLine);

    // Writes a large string in 8K-char segments so the writer's buffer stays small.
    static void WriteChunked(Utf8JsonWriter w, string name, ReadOnlySpan<char> value)
    {
        w.WritePropertyName(name);
        if (value.IsEmpty)
        {
            w.WriteStringValue(value);
            return;
        }

        const int Chunk = 8192;
        while (value.Length > 0)
        {
            int n = Math.Min(Chunk, value.Length);
            // Trap: splitting a surrogate pair across segments changes the escaped output.
            if (n < value.Length && char.IsHighSurrogate(value[n - 1])) n--;
            w.WriteStringValueSegment(value[..n], isFinalSegment: n == value.Length);
            value = value[n..];
            w.Flush();
        }
    }

    public static void Write(Stream stdout, string url, string title, string description,
        ReadOnlySpan<char> raw, ReadOnlySpan<char> markdown, bool compact)
    {
        using (var w = new Utf8JsonWriter(stdout, compact ? CompactWriter : IndentedWriter))
        {
            w.WriteStartObject();
            w.WriteString("url", url);
            w.WriteString("title", title);
            w.WriteString("description", description);
            w.Flush();
            WriteChunked(w, "transcriptRaw", raw.Trim());
            WriteChunked(w, "transcript", markdown.TrimEnd());
            w.WriteEndObject();
        }
        stdout.Write(NewLine);
        stdout.Flush();
    }
}
