using System.Net;
using System.Text.RegularExpressions;
using Xunit;
using YoutubeExplode.Common;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.ClosedCaptions;

public class ProgramTests
{
    [Fact]
    public void ParseOptions_AllowsNoUrlForPipedJsonInput()
    {
        var (options, error) = Program.ParseOptions([]);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Null(options.Url);
    }

    [Fact]
    public void ParseOptions_PreservesPositionalUrlContract()
    {
        var (options, error) = Program.ParseOptions(["https://www.youtube.com/watch?v=dQw4w9WgXcQ"]);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", options.Url);
        Assert.Equal("English", options.Language);
        Assert.False(options.MetadataOnly);
        Assert.False(options.Compact);
    }

    [Fact]
    public void ParseOptions_ParsesExplicitOptionsInAnyOrder()
    {
        var (options, error) = Program.ParseOptions([
            "--compact",
            "--language", "fr",
            "--metadata-only",
            "--url", "dQw4w9WgXcQ"
        ]);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal("dQw4w9WgXcQ", options.Url);
        Assert.Equal("fr", options.Language);
        Assert.True(options.MetadataOnly);
        Assert.True(options.Compact);
    }

    [Fact]
    public void ParseOptions_RecognizesShortOptionAliases()
    {
        var (options, error) = Program.ParseOptions(["-c", "-l", "es", "-u", "video-id"]);

        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal("video-id", options.Url);
        Assert.Equal("es", options.Language);
        Assert.True(options.Compact);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("-?")]
    [InlineData("--help")]
    public void ParseOptions_RecognizesHelpAliases(string argument)
    {
        var (options, error) = Program.ParseOptions([argument]);

        Assert.Null(error);
        Assert.True(options?.ShowHelp);
    }

    [Theory]
    [InlineData("-v")]
    [InlineData("--version")]
    public void ParseOptions_RecognizesVersionAliases(string argument)
    {
        var (options, error) = Program.ParseOptions([argument]);

        Assert.Null(error);
        Assert.True(options?.ShowVersion);
    }

    [Fact]
    public void ParseOptions_TreatsArgumentsAfterDoubleDashAsPositional()
    {
        var (options, error) = Program.ParseOptions(["--", "-video-id"]);

        Assert.Null(error);
        Assert.Equal("-video-id", options?.Url);
    }

    [Theory]
    [InlineData("--unknown", "Unknown option '--unknown'.")]
    [InlineData("--url", "Option '--url' requires a value.")]
    [InlineData("--language", "Option '--language' requires a value.")]
    public void ParseOptions_RejectsInvalidOptions(string argument, string expectedError)
    {
        var (options, error) = Program.ParseOptions([argument]);

        Assert.Null(options);
        Assert.Equal(expectedError, error);
    }

    [Theory]
    [InlineData("--timeout", "Option '--timeout' requires a value.")]
    [InlineData("--timeout 0", "Option '--timeout' requires a positive number of seconds.")]
    [InlineData("--timeout -5", "Option '--timeout' requires a positive number of seconds.")]
    [InlineData("--timeout abc", "Option '--timeout' requires a positive number of seconds.")]
    [InlineData("--timeout NaN", "Option '--timeout' requires a positive number of seconds.")]
    [InlineData("--timeout 1e300", "Option '--timeout' requires a positive number of seconds.")]
    public void ParseOptions_RejectsInvalidTimeouts(string arguments, string expectedError)
    {
        var (options, error) = Program.ParseOptions(arguments.Split(' '));

        Assert.Null(options);
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public void ParseOptions_ParsesTimeoutInSeconds()
    {
        var (options, error) = Program.ParseOptions(["--timeout", "2.5", "id"]);

        Assert.Null(error);
        Assert.Equal(TimeSpan.FromSeconds(2.5), options?.Timeout);
    }

    [Fact]
    public void ParseOptions_RecordsWhetherLanguageWasGiven()
    {
        Assert.False(Program.ParseOptions(["id"]).Options?.LanguageSpecified);
        Assert.True(Program.ParseOptions(["-l", "English", "id"]).Options?.LanguageSpecified);
    }

    [Fact]
    public void ParseOptions_ParsesFeatureOptions()
    {
        var (options, error) = Program.ParseOptions(
            ["-f", "SRT", "--proxy", "socks5://127.0.0.1:1080", "id"]);

        Assert.Null(error);
        Assert.Equal(OutputFormat.Srt, options?.Format);
        Assert.Equal(new Uri("socks5://127.0.0.1:1080"), options?.Proxy);
        Assert.True(Program.ParseOptions(["--segments", "id"]).Options?.Segments);
        Assert.True(Program.ParseOptions(["--list-languages", "id"]).Options?.ListLanguages);
    }

    [Theory]
    [InlineData("--format xml", "Option '--format' must be one of: json, md, txt, srt, vtt.")]
    [InlineData("--proxy ftp://host", "Option '--proxy' requires an http, https, socks4, socks4a or socks5 URL.")]
    [InlineData("--proxy host:8080", "Option '--proxy' requires an http, https, socks4, socks4a or socks5 URL.")]
    [InlineData("--format srt --metadata-only", "Option '--format srt' writes only the transcript, so it cannot be used with '--metadata-only'.")]
    [InlineData("--format txt --segments", "Option '--format txt' writes only the transcript, so it cannot be used with '--segments'.")]
    [InlineData("--list-languages --format vtt", "Option '--list-languages' cannot be used with '--metadata-only', '--segments' or '--format'.")]
    [InlineData("--list-languages --metadata-only", "Option '--list-languages' cannot be used with '--metadata-only', '--segments' or '--format'.")]
    public void ParseOptions_RejectsInvalidFeatureOptions(string arguments, string expectedError)
    {
        var (options, error) = Program.ParseOptions([.. arguments.Split(' '), "id"]);

        Assert.Null(options);
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public void ParseOptions_RejectsDuplicateUrlSources()
    {
        var (options, error) = Program.ParseOptions(["first", "--url", "second"]);

        Assert.Null(options);
        Assert.Equal("The YouTube URL or video ID can only be specified once.", error);
    }

    [Fact]
    public void ParseOptions_RejectsMultiplePositionalUrls()
    {
        var (options, error) = Program.ParseOptions(["first", "second"]);

        Assert.Null(options);
        Assert.Equal("Only one YouTube URL or video ID can be specified.", error);
    }

    [Fact]
    public void ParseOptions_RejectsHelpAndVersionTogether()
    {
        var (options, error) = Program.ParseOptions(["--help", "--version"]);

        Assert.Null(options);
        Assert.Equal("Options '--help' and '--version' cannot be used together.", error);
    }

    [Theory]
    [InlineData("en", "English", "en")]
    [InlineData("en", "English", "english")]
    [InlineData("en-GB", "English (United Kingdom)", "United Kingdom")]
    public void LanguageMatches_MatchesCodesAndNames(string code, string name, string preference)
    {
        Assert.True(Program.LanguageMatches(code, name, preference));
    }

    [Fact]
    public void LanguageMatches_RejectsDifferentLanguage()
    {
        Assert.False(Program.LanguageMatches("fr", "French", "English"));
    }

    [Theory]
    [InlineData("af", "Afrikaans", "fr")]
    [InlineData("fr", "French", "en")]
    [InlineData("az", "Azerbaijani", "ja")]
    public void LanguageMatches_RejectsShortCodeInsideAnotherName(string code, string name, string preference)
    {
        Assert.False(Program.LanguageMatches(code, name, preference));
    }

    // Mirrors the track list order seen on TED talk iG9CE55wbtY.
    private static readonly ClosedCaptionTrackInfo[] TedTracks =
    [
        Track("af", "Afrikaans"),
        Track("az", "Azerbaijani"),
        Track("zh-CN", "Chinese (China)"),
        Track("en", "English (auto-generated)", auto: true),
        Track("en", "English"),
        Track("fr", "French"),
        Track("ja", "Japanese"),
        Track("es", "Spanish"),
        Track("pt-BR", "Portuguese (Brazil)"),
        Track("pt-PT", "Portuguese (Portugal)"),
    ];

    private static ClosedCaptionTrackInfo Track(string code, string name, bool auto = false) =>
        new($"https://example.test/{code}{(auto ? "-auto" : "")}", new Language(code, name), auto);

    [Theory]
    [InlineData("English", "en", false)]
    [InlineData("en", "en", false)]
    [InlineData("fr", "fr", false)]
    [InlineData("es", "es", false)]
    [InlineData("ja", "ja", false)]
    [InlineData("Japanese", "ja", false)]
    [InlineData("pt", "pt-BR", false)]
    [InlineData("Portugal", "pt-PT", false)]
    [InlineData("xx", "en", false)]
    [InlineData("Klingon", "en", false)]
    public void SelectTrack_PrefersBestMatchThenManual(string preference, string expectedCode, bool expectedAuto)
    {
        var track = Program.SelectTrack(TedTracks, preference);

        Assert.NotNull(track);
        Assert.Equal(expectedCode, track.Language.Code);
        Assert.Equal(expectedAuto, track.IsAutoGenerated);
    }

    [Fact]
    public void SelectTrack_UsesAutoTrackWhenItIsTheOnlyMatch()
    {
        var track = Program.SelectTrack([Track("fr", "French"), Track("en", "English (auto-generated)", auto: true)], "English");

        Assert.True(track!.IsAutoGenerated);
    }

    [Fact]
    public void SelectTrack_ReturnsNullForNoTracks()
    {
        Assert.Null(Program.SelectTrack([], "English"));
    }

    [Theory]
    [InlineData(0, 3, 7, "03:07")]
    [InlineData(2, 3, 7, "02:03:07")]
    public void ToHhMmSs_FormatsCaptionOffsets(int hours, int minutes, int seconds, string expected)
    {
        Assert.Equal(expected, Program.ToHhMmSs(new TimeSpan(hours, minutes, seconds)));
    }

    [Theory]
    [InlineData("  hello\n  world  ", "hello world")]
    [InlineData("hello&nbsp;world", "hello world")]
    [InlineData("double&nbsp;&nbsp;entity", "double entity")]
    [InlineData("space &nbsp; around", "space around")]
    [InlineData("&nbsp;edges&nbsp;", "edges")]
    [InlineData("   ", "")]
    public void NormalizeCaption_NormalizesWhitespace(string input, string expected)
    {
        Assert.Equal(expected, Program.NormalizeCaption(input));
    }

    [Fact]
    public void GetVersion_ReturnsAThreePartPackageVersion()
    {
        Assert.Matches(new Regex(@"^\d+\.\d+\.\d+$"), Program.GetVersion());
    }

    [Fact]
    public void CreateOutput_MapsMetadataAndCanonicalUrl()
    {
        var video = new Video(
            VideoId.Parse("dQw4w9WgXcQ"), "Title",
            new Author("UCuAXFkgsw1L7xaCfnd5JJOw", "Channel"),
            new DateTimeOffset(2009, 10, 25, 6, 57, 33, TimeSpan.Zero), "Description",
            TimeSpan.FromSeconds(212.9), [], ["k1", "k2"], new Engagement(42, 1, 0));

        var output = Program.CreateOutput(video);

        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", output.url);
        Assert.Equal("dQw4w9WgXcQ", output.videoId);
        Assert.Equal("Channel", output.channel);
        Assert.Equal("UCuAXFkgsw1L7xaCfnd5JJOw", output.channelId);
        Assert.Equal("2009-10-25", output.uploadDate);
        Assert.Equal(212, output.durationSeconds);
        Assert.Equal(42, output.viewCount);
        Assert.Equal(["k1", "k2"], output.keywords);
        Assert.Equal(CaptionStatus.Skipped, output.captionStatus);
        Assert.Equal("", output.transcript);
    }

    [Fact]
    public void CreateOutput_LeavesLiveStreamDurationNull()
    {
        var video = new Video(
            VideoId.Parse("dQw4w9WgXcQ"), "Live", new Author("UCuAXFkgsw1L7xaCfnd5JJOw", "Channel"),
            DateTimeOffset.UnixEpoch, "", null, [], [], new Engagement(0, 0, 0));

        Assert.Null(Program.CreateOutput(video).durationSeconds);
    }

    public static TheoryData<Exception, string> CaptionErrors() => new()
    {
        { new RequestLimitExceededException("Too many requests"), CaptionStatus.Blocked },
        { new HttpRequestException("Forbidden", null, HttpStatusCode.Forbidden), CaptionStatus.Blocked },
        { new HttpRequestException("Too many", null, HttpStatusCode.TooManyRequests), CaptionStatus.Blocked },
        { new VideoUnplayableException("Sign in to confirm you're not a bot"), CaptionStatus.Blocked },
        { new HttpRequestException("Server error", null, HttpStatusCode.InternalServerError), CaptionStatus.Error },
        { new HttpRequestException("No route to host"), CaptionStatus.Error },
        { new TaskCanceledException("Timed out"), CaptionStatus.Error },
    };

    [Theory]
    [MemberData(nameof(CaptionErrors))]
    public void ClassifyCaptionError_SeparatesBlockingFromOtherFailures(Exception ex, string expected)
    {
        Assert.Equal(expected, Program.ClassifyCaptionError(ex));
    }

    private static Output TranscriptOutput() => new()
    {
        transcript = "- [00:01](link) first",
        segments = Program.BuildSegments([
            new ClosedCaption("first", TimeSpan.FromMilliseconds(1234.4), TimeSpan.FromSeconds(2), []),
            new ClosedCaption(" ", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), []),
            new ClosedCaption("a --> b", new TimeSpan(0, 101, 2, 3, 45), TimeSpan.FromMilliseconds(500), []),
        ]),
    };

    [Fact]
    public void BuildSegments_SkipsBlankCaptionsAndRoundsToMilliseconds()
    {
        var segments = TranscriptOutput().segments!;

        Assert.Equal(2, segments.Count);
        Assert.Equal(1.234, segments[0].start);
        Assert.Equal(3.234, segments[0].end);
        Assert.Equal(363723.045, segments[1].start);
    }

    [Fact]
    public void FormatTranscript_WritesSrt()
    {
        Assert.Equal(
            "1\n00:00:01,234 --> 00:00:03,234\nfirst\n\n2\n101:02:03,045 --> 101:02:03,545\na --> b\n\n",
            Program.FormatTranscript(OutputFormat.Srt, TranscriptOutput()));
    }

    [Fact]
    public void FormatTranscript_WritesVttWithoutCueArrowsInText()
    {
        Assert.Equal(
            "WEBVTT\n\n00:00:01.234 --> 00:00:03.234\nfirst\n\n101:02:03.045 --> 101:02:03.545\na -> b\n\n",
            Program.FormatTranscript(OutputFormat.Vtt, TranscriptOutput()));
    }

    [Fact]
    public void FormatTranscript_WritesTextAndMarkdown()
    {
        Assert.Equal("first\na --> b\n", Program.FormatTranscript(OutputFormat.Text, TranscriptOutput()));
        Assert.Equal("- [00:01](link) first" + Environment.NewLine,
            Program.FormatTranscript(OutputFormat.Markdown, TranscriptOutput()));
    }

    [Fact]
    public void CreateLanguagesOutput_ListsEveryTrack()
    {
        var tracks = new[]
        {
            new ClosedCaptionTrackInfo("https://example.com/en", new Language("en", "English"), false),
            new ClosedCaptionTrackInfo("https://example.com/a", new Language("en", "English (auto-generated)"), true),
        };

        var languages = Program.CreateLanguagesOutput(VideoId.Parse("dQw4w9WgXcQ"), tracks);

        Assert.Equal("dQw4w9WgXcQ", languages.videoId);
        Assert.Collection(languages.tracks,
            t => Assert.Equal(("en", "English", false), (t.code, t.name, t.isAutoGenerated)),
            t => Assert.Equal(("en", "English (auto-generated)", true), (t.code, t.name, t.isAutoGenerated)));
    }
}
