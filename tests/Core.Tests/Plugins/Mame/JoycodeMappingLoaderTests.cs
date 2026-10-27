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
    public void Load_DuplicateJoycode_EntriesAccumulate()
    {
        // given two entries for the same joycode — a bare analogue axis has no sign, so both
        // halves are declared against the same joycode rather than a directional variant
        StubXml("""
            <JoycodeMapping>
              <Mapping joycode='JOYCODE_1_XAXIS' input='AxisLeftStickLeft' />
              <Mapping joycode='JOYCODE_1_XAXIS' input='AxisLeftStickRight' />
            </JoycodeMapping>
            """);

        // when the loader runs
        var result = _underTest.Load();

        // then both entries are kept, in declaration order, rather than the second overwriting
        // the first
        result.Translate("JOYCODE_1_XAXIS").ShouldBe(["AxisLeftStickLeft", "AxisLeftStickRight"]);
    }

    [Fact]
    public void Load_MappingStatesWhichEndOfItsAxisAnInputIs_EachEndTranslatesAlone()
    {
        // given both halves of a bare analogue axis, each stating which end it lies on
        StubXml("""
            <JoycodeMapping>
              <Mapping joycode='JOYCODE_1_XAXIS' half='dec' input='AxisLeftStickLeft' />
              <Mapping joycode='JOYCODE_1_XAXIS' half='inc' input='AxisLeftStickRight' />
            </JoycodeMapping>
            """);

        // when the loader runs
        var result = _underTest.Load();

        // then each end is reachable on its own, and the axis still reaches both
        result.TranslateHalf("JOYCODE_1_XAXIS", "dec").ShouldBe(["AxisLeftStickLeft"]);
        result.TranslateHalf("JOYCODE_1_XAXIS", "inc").ShouldBe(["AxisLeftStickRight"]);
        result.Translate("JOYCODE_1_XAXIS").ShouldBe(["AxisLeftStickLeft", "AxisLeftStickRight"]);
    }

    [Fact]
    public void Load_HalfNamesNeitherEnd_InputIsKeptWithoutOneAndLogsError()
    {
        // given an entry whose half names something MAME has no sequence type for
        StubXml("""
            <JoycodeMapping>
              <Mapping joycode='JOYCODE_1_XAXIS' half='left' input='AxisLeftStickLeft' />
            </JoycodeMapping>
            """);

        // when the loader runs
        var result = _underTest.Load();

        // then the input still translates and only the unusable claim is dropped, so a typo
        // costs the axis its ends rather than costing the port its control altogether
        result.Translate("JOYCODE_1_XAXIS").ShouldBe(["AxisLeftStickLeft"]);
        result.TranslateHalf("JOYCODE_1_XAXIS", "dec").ShouldBeEmpty();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("expected \"dec\" or \"inc\"")));
    }

    [Fact]
    public void Load_SwitchStatesTheAxisItIsAnEndOf_ItsEndIsKnown()
    {
        // given a stick axis read as a switch, declaring which axis it is an end of and which end
        StubXml("""
            <JoycodeMapping>
              <Mapping joycode='JOYCODE_1_ZAXIS_POS_SWITCH' axis='JOYCODE_1_ZAXIS' end='inc' input='AxisRightStickRight' />
              <Mapping joycode='JOYCODE_1_SLIDER1_NEG_SWITCH' input='AxisTriggerLeft' />
            </JoycodeMapping>
            """);

        // when the loader runs
        var result = _underTest.Load();

        // then the switch is that end of that axis but no half of it, and a joycode declaring no
        // axis is no end
        result.EndOf("JOYCODE_1_ZAXIS_POS_SWITCH").ShouldBe(new AxisEnd("JOYCODE_1_ZAXIS", "inc"));
        result.TranslateHalf("JOYCODE_1_ZAXIS_POS_SWITCH", "inc").ShouldBeEmpty();
        result.EndOf("JOYCODE_1_SLIDER1_NEG_SWITCH").ShouldBeNull();
        result.Translate("JOYCODE_1_ZAXIS_POS_SWITCH").ShouldBe(["AxisRightStickRight"]);
    }

    [Theory]
    [InlineData("axis='JOYCODE_1_ZAXIS'")]
    [InlineData("end='inc'")]
    public void Load_HalfAnEnd_IsNoEndAndLogsError(string attributes)
    {
        // given a switch naming its axis without which end, or which end without its axis
        StubXml($"""
            <JoycodeMapping>
              <Mapping joycode='JOYCODE_1_ZAXIS_POS_SWITCH' {attributes} input='AxisRightStickRight' />
            </JoycodeMapping>
            """);

        // when the loader runs
        var result = _underTest.Load();

        // then it still translates, but an end of no known side pairs with nothing
        result.Translate("JOYCODE_1_ZAXIS_POS_SWITCH").ShouldBe(["AxisRightStickRight"]);
        result.EndOf("JOYCODE_1_ZAXIS_POS_SWITCH").ShouldBeNull();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("an end needs both")));
    }

    [Fact]
    public void Load_EndNeitherDecNorInc_IsNoEndAndLogsError()
    {
        // given a switch naming its end in words other than MAME's own sequence types
        StubXml("""
            <JoycodeMapping>
              <Mapping joycode='JOYCODE_1_ZAXIS_POS_SWITCH' axis='JOYCODE_1_ZAXIS' end='pos' input='AxisRightStickRight' />
            </JoycodeMapping>
            """);

        // when the loader runs
        var result = _underTest.Load();

        // then it still translates, but is no end, and the bad value is reported
        result.Translate("JOYCODE_1_ZAXIS_POS_SWITCH").ShouldBe(["AxisRightStickRight"]);
        result.EndOf("JOYCODE_1_ZAXIS_POS_SWITCH").ShouldBeNull();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("end=\"pos\"") && s.Contains("end=\"dec\" or end=\"inc\"")));
    }

    [Fact]
    public void Load_OneJoycodeDeclaredTwoDifferentEnds_KeepsTheFirstAndLogsError()
    {
        // given one switch declared twice, as opposite ends
        StubXml("""
            <JoycodeMapping>
              <Mapping joycode='JOYCODE_1_ZAXIS_POS_SWITCH' axis='JOYCODE_1_ZAXIS' end='inc' input='AxisRightStickRight' />
              <Mapping joycode='JOYCODE_1_ZAXIS_POS_SWITCH' axis='JOYCODE_1_ZAXIS' end='dec' input='ButtonDpadRight' />
            </JoycodeMapping>
            """);

        // when the loader runs
        var result = _underTest.Load();

        // then the first end stands, both inputs still translate, and the conflict is reported
        result.EndOf("JOYCODE_1_ZAXIS_POS_SWITCH").ShouldBe(new AxisEnd("JOYCODE_1_ZAXIS", "inc"));
        result.Translate("JOYCODE_1_ZAXIS_POS_SWITCH").ShouldBe(["AxisRightStickRight", "ButtonDpadRight"]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("already declares it a different end")));
    }

    [Fact]
    public void Load_MappingDeclaringNoEnds_LogsError()
    {
        // given a copy made before ends were declared: switches, but no axis= on any of them
        StubXml("""
            <JoycodeMapping>
              <Mapping joycode='JOYCODE_1_ZAXIS_NEG_SWITCH' input='AxisRightStickLeft' />
              <Mapping joycode='JOYCODE_1_ZAXIS_POS_SWITCH' input='AxisRightStickRight' />
            </JoycodeMapping>
            """);

        // when the loader runs
        _underTest.Load();

        // then the silent loss of axis pairing is reported
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("declares no axis ends")));
    }

    [Fact]
    public void Load_MappingDeclaringAnEnd_DoesNotReportMissingEnds()
    {
        // given a mapping declaring one end beside an ordinary button
        StubXml("""
            <JoycodeMapping>
              <Mapping joycode='JOYCODE_1_BUTTON1' input='ButtonA' />
              <Mapping joycode='JOYCODE_1_ZAXIS_POS_SWITCH' axis='JOYCODE_1_ZAXIS' end='inc' input='AxisRightStickRight' />
            </JoycodeMapping>
            """);

        // when the loader runs
        _underTest.Load();

        // then nothing is reported: one end is enough to show the file knows about them
        _logger.DidNotReceive().Error(Arg.Any<string>());
    }
}
