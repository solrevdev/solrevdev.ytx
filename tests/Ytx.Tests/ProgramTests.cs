using System.Text.RegularExpressions;
using Xunit;

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
}
