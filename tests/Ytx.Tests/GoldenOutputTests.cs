using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using YoutubeExplode.Videos.ClosedCaptions;

// Proves the transcript stays byte-identical to v1.0.7 and pins the 1.1.0 JSON contract.
// The one intended difference: 1.1.0 replaces &nbsp; before collapsing whitespace, so the
// legacy reference is given text with entities already replaced. All caption text is synthetic.
public class GoldenOutputTests
{
    private const string VideoId = "AbCdEfGhIjK";

    private static ClosedCaption Caption(string text, TimeSpan offset) =>
        new(text, offset, TimeSpan.FromSeconds(2), []);

    private static readonly string[] EdgeCaseTexts =
    [
        "plain caption text",
        "two  spaces",
        "tab\tand\nnewline\r\nand carriage return",
        "  leading whitespace",
        "trailing whitespace   ",
        " \t both ends \n ",
        "",
        "   ",
        "\n\t",
        "&nbsp;",
        "html&nbsp;space",
        "&nbsp;leading entity",
        "trailing entity&nbsp;",
        "double&nbsp;&nbsp;entity",
        "no\u00A0break space",
        "\u00A0nbsp at start",
        "line\u2028separator",
        "paragraph\u2029separator",
        "next\u0085line",
        "ideographic\u3000space",
        "en\u2002space and em\u2003space",
        "zero\u200Bwidth is not whitespace",
        "unit\u001Fseparator is not whitespace",
        "\"quotes\" and \\backslash\\ and <tags> & ampersands",
        "control\u0001char",
        "accents caf\u00E9 na\u00EFve",
        "emoji \U0001F600 surrogate pair",
        "[music]",
        "x",
    ];

    private static List<ClosedCaption> EdgeCaseCaptions()
    {
        var captions = new List<ClosedCaption>();
        TimeSpan[] offsets =
        [
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(999),
            TimeSpan.FromSeconds(59.5),
            new TimeSpan(0, 59, 59),
            new TimeSpan(1, 0, 0),
            new TimeSpan(9, 59, 59),
            new TimeSpan(10, 0, 1),
            new TimeSpan(99, 59, 59),
            new TimeSpan(100, 0, 0),
            new TimeSpan(123, 4, 5),
        ];

        for (var i = 0; i < EdgeCaseTexts.Length; i++)
        {
            captions.Add(Caption(EdgeCaseTexts[i], offsets[i % offsets.Length] + TimeSpan.FromSeconds(i)));
        }

        // A trailing entity on the last caption exercises the final TrimEnd/Trim.
        captions.Add(Caption("last caption&nbsp;", new TimeSpan(101, 2, 3)));
        return captions;
    }

    private static List<ClosedCaption> ManyCaptions()
    {
        var captions = new List<ClosedCaption>();
        for (var i = 0; i < 5000; i++)
        {
            var text = EdgeCaseTexts[i % EdgeCaseTexts.Length] + " " + i;
            captions.Add(Caption(text, TimeSpan.FromSeconds(i * 83.7)));
        }

        return captions;
    }

    public static TheoryData<string> CaptionSets => new() { "edge", "many", "empty", "blank" };

    private static List<ClosedCaption> Captions(string set) => set switch
    {
        "edge" => EdgeCaseCaptions(),
        "many" => ManyCaptions(),
        "empty" => [],
        "blank" => [Caption("", TimeSpan.Zero), Caption("  \n ", TimeSpan.FromSeconds(1)), Caption("&nbsp;", TimeSpan.FromSeconds(2))],
        _ => throw new ArgumentOutOfRangeException(nameof(set)),
    };

    private static Output CreateOutput(string raw, string markdown) => new()
    {
        url = $"https://www.youtube.com/watch?v={VideoId}",
        title = "Synthetic title \u2014 \"quoted\" <b>bold</b> caf\u00E9 \U0001F3A5",
        description = "Line one\nLine two\twith tab\r\nhttps://example.com/?a=1&b=2 \u2028 \u00A0 end",
        transcriptRaw = raw,
        transcript = markdown,
    };

    [Theory]
    [MemberData(nameof(CaptionSets))]
    public void BuildTranscript_MatchesLegacyOutput(string set)
    {
        var captions = Captions(set);

        var expected = Legacy.BuildTranscript(VideoId, captions.Select(c => Caption(WithoutEntities(c.Text), c.Offset)));
        var actual = Program.BuildTranscript(VideoId, captions);

        Assert.Equal(expected.Raw, actual.Raw);
        Assert.Equal(expected.Markdown, actual.Markdown);
    }

    [Theory]
    [MemberData(nameof(EdgeCaseTextData))]
    public void NormalizeCaption_MatchesLegacyOutput(string text)
    {
        Assert.Equal(Legacy.NormalizeCaption(WithoutEntities(text)), Program.NormalizeCaption(text));
    }

    [Fact]
    public void NormalizeCaption_MatchesLegacyOutputForEveryBmpCharacter()
    {
        for (var code = 0; code <= char.MaxValue; code++)
        {
            var c = ((char)code).ToString();
            foreach (var text in new[] { "a" + c + "b", c + "a", "a" + c, "a " + c + "b" })
            {
                Assert.Equal(Legacy.NormalizeCaption(text), Program.NormalizeCaption(text));
            }
        }
    }

    private static string WithoutEntities(string text) => text.Replace("&nbsp;", " ");

    public static TheoryData<string> EdgeCaseTextData()
    {
        var data = new TheoryData<string>();
        foreach (var text in EdgeCaseTexts) data.Add(text);
        return data;
    }

    [Theory]
    [MemberData(nameof(CaptionSetsAndModes))]
    public void WriteOutput_IsByteIdenticalToLegacyOutput(string set, bool compact)
    {
        var (raw, markdown) = Legacy.BuildTranscript(VideoId, Captions(set));
        var output = CreateOutput(raw, markdown);

        var expected = Legacy.Write(output, compact);
        using var stream = new MemoryStream();
        Program.WriteOutput(stream, output, compact);

        Assert.Equal(expected, stream.ToArray());
    }

    public static TheoryData<string, bool> CaptionSetsAndModes()
    {
        var data = new TheoryData<string, bool>();
        foreach (var set in new[] { "edge", "many", "empty", "blank" })
        {
            data.Add(set, false);
            data.Add(set, true);
        }

        return data;
    }

    [Fact]
    public void WriteOutput_MatchesGoldenIndentedJson()
    {
        var captions = new[]
        {
            Caption("  hello\u00A0 world ", new TimeSpan(0, 0, 3)),
            Caption("", new TimeSpan(0, 0, 5)),
            Caption("caf\u00E9 & \"tea\"", new TimeSpan(1, 2, 3)),
            Caption("late&nbsp;", new TimeSpan(100, 0, 7)),
        };
        var (raw, markdown) = Program.BuildTranscript(VideoId, captions);
        var output = new Output
        {
            url = $"https://www.youtube.com/watch?v={VideoId}",
            videoId = VideoId,
            title = "T\u00EFtle <x>",
            channel = "Chännel",
            channelId = "UC0123456789abcdefghijkl",
            uploadDate = "2024-02-29",
            durationSeconds = 360008,
            viewCount = 12345678901,
            keywords = ["one", "two words"],
            description = "a\nb",
            captionStatus = CaptionStatus.Ok,
            captionLanguage = "en-GB",
            captionLanguageName = "English (United Kingdom)",
            captionIsAutoGenerated = false,
            transcriptRaw = raw,
            transcript = markdown,
            segments = Program.BuildSegments(captions),
            chapters =
            [
                new Chapter { start = 0, end = 3723, title = "Intro" },
                new Chapter { start = 3723, end = null, title = "Caf\u00E9 talk" },
            ],
        };

        var expected = """
            {
              "url": "https://www.youtube.com/watch?v=AbCdEfGhIjK",
              "videoId": "AbCdEfGhIjK",
              "title": "Tïtle <x>",
              "channel": "Chännel",
              "channelId": "UC0123456789abcdefghijkl",
              "uploadDate": "2024-02-29",
              "durationSeconds": 360008,
              "viewCount": 12345678901,
              "keywords": [
                "one",
                "two words"
              ],
              "description": "a\nb",
              "captionStatus": "ok",
              "captionLanguage": "en-GB",
              "captionLanguageName": "English (United Kingdom)",
              "captionIsAutoGenerated": false,
              "captionError": null,
              "transcriptRaw": "hello world café & \"tea\" late",
              "transcript": "- [00:03](https://www.youtube.com/watch?v=AbCdEfGhIjK&t=3s) hello world{NL}- [01:02:03](https://www.youtube.com/watch?v=AbCdEfGhIjK&t=3723s) café & \"tea\"{NL}- [100:00:07](https://www.youtube.com/watch?v=AbCdEfGhIjK&t=360007s) late",
              "segments": [
                {
                  "start": 3,
                  "end": 5,
                  "text": "hello world"
                },
                {
                  "start": 3723,
                  "end": 3725,
                  "text": "café & \"tea\""
                },
                {
                  "start": 360007,
                  "end": 360009,
                  "text": "late"
                }
              ],
              "chapters": [
                {
                  "start": 0,
                  "end": 3723,
                  "title": "Intro"
                },
                {
                  "start": 3723,
                  "end": null,
                  "title": "Café talk"
                }
              ]
            }

            """.Replace("\n", Environment.NewLine)
            // Markdown lines are joined with the platform newline, which JSON escapes.
            .Replace("{NL}", Environment.NewLine == "\r\n" ? "\\r\\n" : "\\n");

        AssertWrites(expected, output, compact: false);
    }

    [Fact]
    public void WriteOutput_MatchesGoldenCompactJson()
    {
        var output = new Output
        {
            url = "u",
            videoId = "v",
            title = "\u2028\u0085\u3000\U0001F600",
            captionStatus = CaptionStatus.Blocked,
            captionError = "Sign in to confirm you're not a bot",
        };

        var expected = "{\"url\":\"u\",\"videoId\":\"v\",\"title\":\"\\u2028\\u0085\\u3000\\uD83D\\uDE00\",\"channel\":\"\",\"channelId\":\"\","
            + "\"uploadDate\":null,\"durationSeconds\":null,\"viewCount\":null,\"keywords\":[],\"description\":\"\","
            + "\"captionStatus\":\"blocked\",\"captionLanguage\":null,\"captionLanguageName\":null,\"captionIsAutoGenerated\":null,"
            + "\"captionError\":\"Sign in to confirm you're not a bot\",\"transcriptRaw\":\"\",\"transcript\":\"\",\"segments\":null,\"chapters\":[]}" + Environment.NewLine;

        AssertWrites(expected, output, compact: true);
    }

    private static void AssertWrites(string expected, Output output, bool compact)
    {
        using var stream = new MemoryStream();
        Program.WriteOutput(stream, output, compact);
        var bytes = stream.ToArray();

        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble), "Output must not start with a BOM.");
        Assert.Equal(expected, Encoding.UTF8.GetString(bytes));
    }

    // Verbatim copy of the v1.0.7 transcript and output code, kept as the reference.
    private static class Legacy
    {
        public static (string Raw, string Markdown) BuildTranscript(string videoId, IEnumerable<ClosedCaption> captions)
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

        public static byte[] Write(Output output, bool compact)
        {
            var json = JsonSerializer.Serialize(output, new JsonSerializerOptions
            {
                WriteIndented = !compact,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            // Console.WriteLine with Console.OutputEncoding = UTF-8 writes no BOM.
            return new UTF8Encoding(false).GetBytes(json + Environment.NewLine);
        }

        private static string ToHhMmSs(TimeSpan ts)
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
}
