using DynamicControls.Plugins.Mame;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.Mame;

/// <summary>
/// Unit tests for <see cref="JoycodeMappingLoader"/>. The loader reads JoycodeMapping.xml from
/// {rootDir}/Emulators/MAME and returns a <see cref="JoycodeMapping"/>. Filesystem is substituted so each
/// test supplies a literal XML string. The resulting mapping is verified via <c>Translate</c>
/// since the underlying dictionary is not exposed.
/// </summary>
public class JoycodeMappingLoaderTests
{
    private const string RootDir = @"C:\plugin";
    private static readonly string MappingPath = Path.Combine(RootDir, "Defaults", "Emulators", "MAME", "JoycodeMapping.xml");

    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IFileSystem _fs = TestFs.Create();
    private readonly JoycodeMappingLoader _underTest;

    public JoycodeMappingLoaderTests()
    {
        _underTest = new JoycodeMappingLoader(_logger, new LayeredFileSystem(RootDir, _fs));
    }

    private void StubXml(string xml)
    {
        _fs.FileExists(MappingPath).Returns(true);
        _fs.OpenRead(MappingPath).Returns(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
    }

    [Fact]
    public void Load_FileMissing_ReturnsEmptyMappingAndLogsError()
    {
        // given no JoycodeMapping.xml on disk
        _fs.FileExists(MappingPath).Returns(false);

        // when the loader runs
        var result = _underTest.Load();

        // then an empty mapping is returned, no XML is parsed, and the absence is logged
        result.Translate("JOYCODE_1_BUTTON1").ShouldBeEmpty();
        _fs.DidNotReceive().OpenRead(Arg.Any<string>());
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("JoycodeMapping.xml not found")));
    }

    [Fact]
    public void Load_ParsesMappingEntries()
    {
        // given a JoycodeMapping.xml with two well-formed entries
        StubXml("""
            <JoycodeMapping>
              <Mapping joycode='JOYCODE_1_BUTTON1' input='ButtonA' />
              <Mapping joycode='JOYCODE_1_BUTTON2' input='ButtonB' />
            </JoycodeMapping>
            """);

        // when the loader runs
        var result = _underTest.Load();

        // then both joycodes translate to their configured inputs
        result.Translate("JOYCODE_1_BUTTON1").ShouldBe(["ButtonA"]);
        result.Translate("JOYCODE_1_BUTTON2").ShouldBe(["ButtonB"]);
    }

    [Fact]
    public void Load_EntryMissingJoycodeOrInput_SkippedWithError()
    {
        // given one valid entry, one missing joycode, and one missing input
        StubXml("""
            <JoycodeMapping>
              <Mapping joycode='JOYCODE_1_BUTTON1' input='ButtonA' />
              <Mapping input='ButtonB' />
              <Mapping joycode='JOYCODE_1_BUTTON3' />
            </JoycodeMapping>
            """);

        // when the loader runs
        var result = _underTest.Load();

        // then only the complete entry is kept; each malformed entry produced an error log
        result.Translate("JOYCODE_1_BUTTON1").ShouldBe(["ButtonA"]);
        result.Translate("JOYCODE_1_BUTTON3").ShouldBeEmpty();
        _logger.Received(2).Error(Arg.Is<string>(s => s.Contains("missing 'joycode' or 'input'")));
    }

    [Fact]
    public void Load_IgnoresNonMappingChildren()
    {
        // given a file mixing comments, whitespace, and unrelated elements with one Mapping
        StubXml("""
            <JoycodeMapping>
              <!-- a comment -->
              <Other joycode='JOYCODE_X' input='ButtonX' />
              <Mapping joycode='JOYCODE_1_BUTTON1' input='ButtonA' />
            </JoycodeMapping>
            """);

        // when the loader runs
        var result = _underTest.Load();

        // then only the Mapping element contributes; the unrelated element is ignored silently
        result.Translate("JOYCODE_1_BUTTON1").ShouldBe(["ButtonA"]);
        result.Translate("JOYCODE_X").ShouldBeEmpty();
        _logger.DidNotReceive().Error(Arg.Is<string>(s => s.Contains("missing")));
    }

    [Fact]
    public void Load_DuplicateJoycode_LastEntryWins()
    {
        // given two entries for the same joycode
        StubXml("""
            <JoycodeMapping>
              <Mapping joycode='JOYCODE_1_BUTTON1' input='ButtonA' />
              <Mapping joycode='JOYCODE_1_BUTTON1' input='ButtonOverride' />
            </JoycodeMapping>
            """);

        // when the loader runs
        var result = _underTest.Load();

        // then the later entry replaces the earlier one (dictionary assignment semantics)
        result.Translate("JOYCODE_1_BUTTON1").ShouldBe(["ButtonOverride"]);
    }
}
