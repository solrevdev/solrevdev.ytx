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
}
