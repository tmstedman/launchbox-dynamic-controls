using DynamicControls;
using DynamicControls.Config;
using DynamicControls.Labels;
using NSubstitute;

namespace DynamicControls.Core.Tests.Labels;

/// <summary>
/// Unit tests for <see cref="InputLabelsLoader"/>. The loader parses per-game and per-platform
/// label XML files into raw <see cref="InputLabelsConfig"/> DTOs without applying any input
/// mapping. Filesystem is a substitute so each test supplies a literal XML string and a stubbed
/// FileExists answer; path construction is verified by the precise path the loader probes.
/// </summary>
public class InputLabelsLoaderTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IFileSystem _fs = TestFs.Create();
    private const string RootDir = @"C:\plugin";
    private static readonly string LabelsDir = Path.Combine(RootDir, "Defaults", "Labels");
    private readonly InputLabelsLoader _underTest;

    public InputLabelsLoaderTests()
    {
        _underTest = new InputLabelsLoader(_logger, new LayeredFileSystem(RootDir, _fs));
    }

    private static GameInfo Game(string platform, string romName) => new(
        Platform: platform,
        RomName: romName,
        CloneOf: null,
        EmulatorPath: null,
        RomDirectory: null,
        RetroArchCore: null);

    private void StubXml(string path, string xml)
    {
        _fs.FileExists(path).Returns(true);
        _fs.OpenRead(path).Returns(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
    }

    // --- IsEnabled ---

    [Fact]
    public void IsEnabled_AlwaysReturnsTrue()
    {
        _underTest.IsEnabled(new GlobalConfig()).ShouldBeTrue();
    }

    // --- File presence ---

    [Fact]
    public void Load_FileMissing_ReturnsNullAndDoesNotParse()
    {
        // given the game labels file does not exist
        _fs.FileExists(Arg.Any<string>()).Returns(false);

        // when the loader is asked for it
        InputLabelsConfig? result = _underTest.Load(Game("Sega Genesis", "OutRun"));

        // then null is returned and no XML is loaded
        result.ShouldBeNull();
        _fs.DidNotReceive().OpenRead(Arg.Any<string>());
    }

    [Fact]
    public void LoadDefaultLabels_FileMissing_ReturnsNull()
    {
        // given no default labels file exists
        _fs.FileExists(Arg.Any<string>()).Returns(false);

        // when the loader is asked for defaults
        InputLabelsConfig? result = _underTest.LoadDefaultLabels("Sega Genesis");

        // then null is returned
        result.ShouldBeNull();
    }

    // --- Path construction ---

    [Fact]
    public void Load_BuildsPathFromPlatformAndRomName()
    {
        // given a game labels file at Data/Labels/{platform}/{romName}.xml
        string expected = Path.Combine(LabelsDir, "Sega Genesis", "OutRun.xml");
        StubXml(expected, "<labels />");

        // when the loader is asked for it
        InputLabelsConfig? result = _underTest.Load(Game("Sega Genesis", "OutRun"));

        // then it probes the exact constructed path
        result.ShouldNotBeNull();
        _fs.Received().OpenRead(expected);
    }

    [Fact]
    public void LoadDefaultLabels_UsesDefaultLabelsFilename()
    {
        // given a defaults file at Data/Labels/{platform}/_DefaultLabels.xml
        string expected = Path.Combine(LabelsDir, "Sega Genesis", "_DefaultLabels.xml");
        StubXml(expected, "<labels />");

        // when the loader is asked for defaults
        _underTest.LoadDefaultLabels("Sega Genesis");

        // then the loader probes the _DefaultLabels.xml file
        _fs.Received().OpenRead(expected);
    }

    [Fact]
    public void Load_PlatformWithInvalidChars_IsSanitized()
    {
        // given a platform name with invalid filename chars
        string safePlatform = "Sega/Genesis".SafeFileName();
        string expected = Path.Combine(LabelsDir, safePlatform, "OutRun.xml");
        StubXml(expected, "<labels />");

        // when the loader is asked for the file
        _underTest.Load(Game("Sega/Genesis", "OutRun"));

        // then the platform folder uses the sanitized name
        _fs.Received().OpenRead(expected);
    }

    // --- XML parsing ---

    [Fact]
    public void Load_EmptyRoot_ReturnsEmptyConfig()
    {
        // given a labels file with a root element and no children
        string path = Path.Combine(LabelsDir, "Sega Genesis", "OutRun.xml");
        StubXml(path, "<labels />");

        // when the loader runs
        InputLabelsConfig? result = _underTest.Load(Game("Sega Genesis", "OutRun"));

        // then a default (but non-null) config is returned
        result.ShouldNotBeNull();
        result.Labels.ShouldBeEmpty();
    }

    [Fact]
    public void Load_ParsesEntriesIntoLabelsList()
    {
        // given a labels file with two entries
        string path = Path.Combine(LabelsDir, "Sega Genesis", "OutRun.xml");
        StubXml(path, """
            <labels>
              <A>Brake</A>
              <B>Accelerate</B>
            </labels>
            """);

        // when the loader runs
        InputLabelsConfig result = _underTest.Load(Game("Sega Genesis", "OutRun"))!;

        // then entries are kept in document order, mapping element name → Name and text → Label
        result.Labels.Select(e => (e.Name, e.Label))
            .ShouldBe([("A", "Brake"), ("B", "Accelerate")]);
    }

    [Fact]
    public void Load_EmptyInnerText_IsSkippedAndLogged()
    {
        // given a labels file with one valid entry and one empty entry
        string path = Path.Combine(LabelsDir, "Sega Genesis", "OutRun.xml");
        StubXml(path, """
            <labels>
              <A>Brake</A>
              <B></B>
            </labels>
            """);

        // when the loader runs
        InputLabelsConfig result = _underTest.Load(Game("Sega Genesis", "OutRun"))!;

        // then only the non-empty entry is kept and an error is logged for the empty one
        result.Labels.Select(e => e.Name).ShouldBe(["A"]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("<B>") && s.Contains("no text value")));
    }

}
