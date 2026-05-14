using DynamicControls.InputMapping;
using NSubstitute;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;

namespace DynamicControls.Core.Tests.InputMapping;

/// <summary>
/// Unit tests for <see cref="InputMappingLoader"/>. The loader parses per-game InputMappings XML
/// and platform Controllers.xml into raw DTOs without applying any merging. Filesystem is a
/// substitute so each test supplies a literal XML string and a stubbed FileExists answer; path
/// construction is verified by the precise path the loader probes.
/// </summary>
public class InputMappingLoaderTests
{
    private const string RootDir = @"C:\plugin";
    private static readonly string DefaultsDir = Path.Combine(RootDir, "Defaults");

    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IFileSystem _fs = TestFs.Create();
    private readonly InputMappingLoader _underTest;

    public InputMappingLoaderTests()
    {
        _underTest = new InputMappingLoader(_logger, new LayeredFileSystem(RootDir, _fs));
    }

    private void StubXml(string path, string xml)
    {
        _fs.FileExists(path).Returns(true);
        _fs.OpenRead(path).Returns(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
    }

    // ---- LoadGameMapping: file presence + path construction ----

    [Fact]
    public void LoadGameMapping_FileMissing_ReturnsNullAndDoesNotParse()
    {
        // given no per-game mapping file exists
        _fs.FileExists(Arg.Any<string>()).Returns(false);

        // when the loader is asked for it
        var result = _underTest.LoadGameMapping(Game());

        // then null is returned and no XML is loaded
        result.ShouldBeNull();
        _fs.DidNotReceive().OpenRead(Arg.Any<string>());
    }

    [Fact]
    public void LoadGameMapping_BuildsPathFromPlatformAndRomName()
    {
        // given a per-game mapping at Config/InputMappings/{platform}/{romName}.xml
        string expected = Path.Combine(DefaultsDir, "InputMappings", "Sega Genesis", "OutRun.xml");
        StubXml(expected, "<InputMapping />");

        // when the loader runs
        var result = _underTest.LoadGameMapping(Game());

        // then the loader probes the exact constructed path
        result.ShouldNotBeNull();
        _fs.Received().OpenRead(expected);
    }

    [Fact]
    public void LoadGameMapping_PlatformWithInvalidChars_IsSanitized()
    {
        // given a platform name with chars not legal in file paths
        string safePlatform = "Sega/Genesis".SafeFileName();
        string expected = Path.Combine(DefaultsDir, "InputMappings", safePlatform, "OutRun.xml");
        StubXml(expected, "<InputMapping />");

        // when the loader runs
        _underTest.LoadGameMapping(Game() with { Platform = "Sega/Genesis" });

        // then the platform folder uses the sanitized name
        _fs.Received().OpenRead(expected);
    }

    // ---- LoadGameMapping: XML parsing ----

    [Fact]
    public void LoadGameMapping_ParsesControllerAttribute_AndMappingEntries()
    {
        // given a per-game XML with a controller selection and two mapping entries
        string path = Path.Combine(DefaultsDir, "InputMappings", "Sega Genesis", "OutRun.xml");
        StubXml(path, """
            <InputMapping controller='Zapper'>
              <Mapping name='Trigger' input='ButtonA' />
              <Mapping name='Start' input='ButtonStart' />
            </InputMapping>
            """);

        // when the loader runs
        var result = _underTest.LoadGameMapping(Game())!;

        // then the controller attribute and both entries are captured in document order
        result.Controller.ShouldBe("Zapper");
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("Trigger", "ButtonA"),
            ("Start", "ButtonStart"),
        ]);
    }

    [Theory]
    [InlineData("left", AnalogToDigitalMode.Left)]
    [InlineData("right", AnalogToDigitalMode.Right)]
    [InlineData("LEFT", AnalogToDigitalMode.Left)]
    [InlineData("Right", AnalogToDigitalMode.Right)]
    public void LoadGameMapping_AnalogToDigital_IsCaseInsensitive(string attrValue, AnalogToDigitalMode expected)
    {
        // given a per-game XML with analogToDigital in varying case
        string path = Path.Combine(DefaultsDir, "InputMappings", "Sega Genesis", "OutRun.xml");
        StubXml(path, $"<InputMapping analogToDigital='{attrValue}' />");

        // when the loader runs
        var result = _underTest.LoadGameMapping(Game())!;

        // then the mode is parsed to the matching enumerator regardless of case
        result.AnalogToDigital.ShouldBe(expected);
    }

    [Fact]
    public void LoadGameMapping_InvalidAnalogToDigital_LogsErrorAndLeavesUnset()
    {
        // given a per-game XML with an unrecognized analogToDigital value
        string path = Path.Combine(DefaultsDir, "InputMappings", "Sega Genesis", "OutRun.xml");
        StubXml(path, "<InputMapping analogToDigital='diagonal' />");

        // when the loader runs
        var result = _underTest.LoadGameMapping(Game())!;

        // then AnalogToDigital is left null and the invalid value is logged
        result.AnalogToDigital.ShouldBeNull();
        _logger.Received().Error(Arg.Is<string>(s =>
            s.Contains("diagonal") && s.Contains("analogToDigital")));
    }

    [Fact]
    public void LoadGameMapping_MappingEntryMissingNameOrInput_SkippedWithError()
    {
        // given a per-game XML with one valid entry, one missing name, and one missing input
        string path = Path.Combine(DefaultsDir, "InputMappings", "Sega Genesis", "OutRun.xml");
        StubXml(path, """
            <InputMapping>
              <Mapping name='A' input='ButtonA' />
              <Mapping input='ButtonB' />
              <Mapping name='C' />
            </InputMapping>
            """);

        // when the loader runs
        var result = _underTest.LoadGameMapping(Game())!;

        // then only the complete entry is kept; each malformed entry produced an error log
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonA")]);
        _logger.Received(2).Error(Arg.Is<string>(s => s.Contains("missing 'name' or 'input'")));
    }

    [Fact]
    public void LoadGameMapping_Unmap_ParsesNameIntoUnmapsList()
    {
        // given a per-game XML with a mix of <Mapping> and <Unmap> entries
        string path = Path.Combine(DefaultsDir, "InputMappings", "Sega Genesis", "OutRun.xml");
        StubXml(path, """
            <InputMapping>
              <Mapping name='A' input='ButtonA' />
              <Unmap name='B' />
              <Unmap name='Mode' />
            </InputMapping>
            """);

        // when the loader runs
        var result = _underTest.LoadGameMapping(Game())!;

        // then Unmaps collects the names; Mappings is unaffected
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonA")]);
        result.Unmaps.ShouldBe(["B", "Mode"]);
    }

    [Fact]
    public void LoadGameMapping_UnmapMissingName_SkippedWithError()
    {
        // given a per-game XML with an <Unmap> element that has no name attribute
        string path = Path.Combine(DefaultsDir, "InputMappings", "Sega Genesis", "OutRun.xml");
        StubXml(path, """
            <InputMapping>
              <Unmap name='B' />
              <Unmap />
            </InputMapping>
            """);

        // when the loader runs
        var result = _underTest.LoadGameMapping(Game())!;

        // then the malformed entry is dropped and the rest survive
        result.Unmaps.ShouldBe(["B"]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("<Unmap>") && s.Contains("missing 'name'")));
    }

    // ---- LoadPlatformMapping: file presence + path construction ----

    [Fact]
    public void LoadPlatformMapping_FileMissing_ReturnsNull()
    {
        // given no platform controllers file exists
        _fs.FileExists(Arg.Any<string>()).Returns(false);

        // when the loader is asked for it
        var result = _underTest.LoadPlatformMapping("Sega Genesis");

        // then null is returned
        result.ShouldBeNull();
    }

    [Fact]
    public void LoadPlatformMapping_BuildsPathFromPlatform_AndSanitizes()
    {
        // given a platform controllers file at Config/Controllers/{safePlatform}.xml
        string safePlatform = "Sega/Genesis".SafeFileName();
        string expected = Path.Combine(DefaultsDir, "Controllers", safePlatform + ".xml");
        StubXml(expected, "<Controllers />");

        // when the loader runs
        _underTest.LoadPlatformMapping("Sega/Genesis");

        // then the platform filename uses the sanitized name
        _fs.Received().OpenRead(expected);
    }

    // ---- LoadPlatformMapping: XML parsing ----

    [Fact]
    public void LoadPlatformMapping_ParsesControllers_WithDefaultFlagAndAnalogToDigital()
    {
        // given a Controllers.xml with two controllers — one default with analogToDigital, one not
        string path = Path.Combine(DefaultsDir, "Controllers", "Sega Genesis.xml");
        StubXml(path, """
            <Controllers>
              <Controller name='Pad-3btn'>
                <Mapping name='A' input='ButtonA' />
              </Controller>
              <Controller name='Pad-6btn' default='true' analogToDigital='left'>
                <Mapping name='A' input='ButtonA' />
                <Mapping name='B' input='ButtonB' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then both controllers are present in document order with their flags and mappings
        result.Controllers.Count.ShouldBe(2);

        result.Controllers[0].Name.ShouldBe("Pad-3btn");
        result.Controllers[0].IsDefault.ShouldBeFalse();
        result.Controllers[0].AnalogToDigital.ShouldBeNull();
        result.Controllers[0].Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonA")]);

        result.Controllers[1].Name.ShouldBe("Pad-6btn");
        result.Controllers[1].IsDefault.ShouldBeTrue();
        result.Controllers[1].AnalogToDigital.ShouldBe(AnalogToDigitalMode.Left);
        result.Controllers[1].Mappings.Select(m => (m.Name, m.Input))
            .ShouldBe([("A", "ButtonA"), ("B", "ButtonB")]);
    }

    [Fact]
    public void LoadPlatformMapping_RootLevelMappings_AreSharedAcrossAllControllers()
    {
        // given a Controllers.xml with a shared root-level baseline plus per-controller additions
        string path = Path.Combine(DefaultsDir, "Controllers", "Sega Genesis.xml");
        StubXml(path, """
            <Controllers>
              <Mapping name='A' input='ButtonX' />
              <Mapping name='Start' input='ButtonStart' />
              <Controller name='6-Button' default='true'>
                <Mapping name='Y' input='ButtonY' />
              </Controller>
              <Controller name='3-Button' />
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the shared mappings come first, followed by each controller's own entries
        result.Controllers[0].Name.ShouldBe("6-Button");
        result.Controllers[0].Mappings.Select(m => (m.Name, m.Input))
            .ShouldBe([("A", "ButtonX"), ("Start", "ButtonStart"), ("Y", "ButtonY")]);

        // a controller with no entries of its own still inherits the shared baseline
        result.Controllers[1].Name.ShouldBe("3-Button");
        result.Controllers[1].Mappings.Select(m => (m.Name, m.Input))
            .ShouldBe([("A", "ButtonX"), ("Start", "ButtonStart")]);
    }

    [Fact]
    public void LoadPlatformMapping_ControllerMissingName_SkippedWithError()
    {
        // given a Controllers.xml with one valid controller and one missing the name attribute
        string path = Path.Combine(DefaultsDir, "Controllers", "Sega Genesis.xml");
        StubXml(path, """
            <Controllers>
              <Controller name='Pad' />
              <Controller default='true' />
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then only the named controller survives and an error is logged for the nameless one
        result.Controllers.Select(c => c.Name).ShouldBe(["Pad"]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("missing 'name'")));
    }
}
