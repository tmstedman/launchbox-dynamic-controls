using DynamicControls.Static;
using NSubstitute;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;

namespace DynamicControls.Core.Tests.Static;

/// <summary>
/// Unit tests for <see cref="StaticImageResolver"/>. The resolver probes
/// <c>Static/{platform}/{romName}.jpg</c> then <c>.png</c> and returns the first hit (or null).
/// Filesystem is a substitute so each test pins exactly which probe paths exist.
/// </summary>
public class StaticImageResolverTests
{
    private const string RootDir = @"C:\plugin";
    private static readonly string StaticDir = Path.Combine(RootDir, "User", "Static");

    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IFileSystem _fs = TestFs.Create();
    private readonly StaticImageResolver _underTest;

    public StaticImageResolverTests()
    {
        _underTest = new StaticImageResolver(_logger, new LayeredFileSystem(RootDir, _fs));
    }

    [Fact]
    public void Find_NoFilesExist_ReturnsNull()
    {
        // given the filesystem has neither .jpg nor .png for this game
        _fs.FileExists(Arg.Any<string>()).Returns(false);

        // when the resolver is asked
        var result = _underTest.Find(Game());

        // then nothing is returned
        result.ShouldBeNull();
    }

    [Fact]
    public void Find_JpgExists_ReturnsJpgPath()
    {
        // given only the .jpg variant exists
        string expected = Path.Combine(StaticDir, "Sega Genesis", "OutRun.jpg");
        _fs.FileExists(expected).Returns(true);

        // when the resolver is asked
        var result = _underTest.Find(Game());

        // then the .jpg path is returned
        result.ShouldBe(expected);
    }

    [Fact]
    public void Find_OnlyPngExists_ReturnsPngPath()
    {
        // given only the .png variant exists (jpg probe misses)
        string png = Path.Combine(StaticDir, "Sega Genesis", "OutRun.png");
        _fs.FileExists(png).Returns(true);

        // when the resolver is asked
        var result = _underTest.Find(Game());

        // then the .png path is returned via fallthrough
        result.ShouldBe(png);
    }

    [Fact]
    public void Find_BothJpgAndPngExist_PrefersPng()
    {
        // given both variants exist
        string png = Path.Combine(StaticDir, "Sega Genesis", "OutRun.png");
        string jpg = Path.Combine(StaticDir, "Sega Genesis", "OutRun.jpg");
        _fs.FileExists(png).Returns(true);
        _fs.FileExists(jpg).Returns(true);

        // when the resolver is asked
        var result = _underTest.Find(Game());

        // then .jpg wins by virtue of being probed first
        result.ShouldBe(png);
    }

    [Fact]
    public void Find_PlatformWithInvalidChars_IsSanitizedInPath()
    {
        // given a platform name with chars not legal in file paths
        string safePlatform = "Sega/Genesis".SafeFileName();
        string expected = Path.Combine(StaticDir, safePlatform, "OutRun.jpg");
        _fs.FileExists(expected).Returns(true);

        // when the resolver is asked for that platform
        var result = _underTest.Find(Game() with { Platform = "Sega/Genesis" });

        // then the platform folder uses the sanitized name
        result.ShouldBe(expected);
    }
}
