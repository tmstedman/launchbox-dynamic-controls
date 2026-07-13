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
        byte[] bytes = Encoding.UTF8.GetBytes(xml);
        _fs.FileExists(path).Returns(true);
        // Fresh stream per call — a file may be opened more than once (e.g. root inheritFrom that
        // re-reads a file before a cycle is detected), and a single MemoryStream would be disposed.
        _fs.OpenRead(path).Returns(_ => new MemoryStream(bytes));
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
    public void LoadPlatformMapping_InheritFrom_PrependBaseControllerMappingsBeforeOwn()
    {
        // given a Controllers.xml where 6-Button inherits from 3-Button (which appears before it)
        string path = Path.Combine(DefaultsDir, "Controllers", "Sega Genesis.xml");
        StubXml(path, """
            <Controllers>
              <Controller name='3-Button'>
                <Mapping name='A' input='ButtonX' />
                <Mapping name='Start' input='ButtonStart' />
              </Controller>
              <Controller name='6-Button' default='true' inheritFrom='3-Button'>
                <Mapping name='Y' input='ButtonY' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the base mappings come first, followed by the inheriting controller's own entries
        result.Controllers[0].Name.ShouldBe("3-Button");
        result.Controllers[0].Mappings.Select(m => (m.Name, m.Input))
            .ShouldBe([("A", "ButtonX"), ("Start", "ButtonStart")]);

        result.Controllers[1].Name.ShouldBe("6-Button");
        result.Controllers[1].Mappings.Select(m => (m.Name, m.Input))
            .ShouldBe([("A", "ButtonX"), ("Start", "ButtonStart"), ("Y", "ButtonY")]);
    }

    [Fact]
    public void LoadPlatformMapping_InheritFrom_SameNameInChild_ReplacesParentEntry()
    {
        // given a controller that inherits a mapping and redeclares the same button Name
        string path = Path.Combine(DefaultsDir, "Controllers", "Sega Genesis.xml");
        StubXml(path, """
            <Controllers>
              <Controller name='3-Button'>
                <Mapping name='A' input='ButtonX' />
                <Mapping name='Start' input='ButtonStart' />
              </Controller>
              <Controller name='6-Button' default='true' inheritFrom='3-Button'>
                <Mapping name='A' input='ButtonRightShoulder' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then 6-Button's A replaces 3-Button's A — not duplicated — and Start is inherited
        result.Controllers[1].Mappings.Select(m => (m.Name, m.Input))
            .ShouldBe([("Start", "ButtonStart"), ("A", "ButtonRightShoulder")]);
    }

    [Fact]
    public void LoadPlatformMapping_InheritFrom_SameNameTwiceInParent_ChildSingleEntryReplacesAll()
    {
        // given a parent controller with the same button Name declared twice, and a child that
        // redeclares that Name once
        string path = Path.Combine(DefaultsDir, "Controllers", "Sega Genesis.xml");
        StubXml(path, """
            <Controllers>
              <Controller name='3-Button'>
                <Mapping name='A' input='ButtonX' />
                <Mapping name='A' input='ButtonRightShoulder' />
                <Mapping name='Start' input='ButtonStart' />
              </Controller>
              <Controller name='6-Button' default='true' inheritFrom='3-Button'>
                <Mapping name='A' input='ButtonY' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then both parent A entries are dropped and replaced by the child's single entry
        result.Controllers[1].Mappings.Select(m => (m.Name, m.Input))
            .ShouldBe([("Start", "ButtonStart"), ("A", "ButtonY")]);
    }

    [Fact]
    public void LoadPlatformMapping_InheritFrom_SameNameTwiceInChild_BothEntriesSurvive()
    {
        // given a controller that declares the same button Name twice in its own mappings
        string path = Path.Combine(DefaultsDir, "Controllers", "Sega Genesis.xml");
        StubXml(path, """
            <Controllers>
              <Controller name='3-Button'>
                <Mapping name='A' input='ButtonX' />
                <Mapping name='Start' input='ButtonStart' />
              </Controller>
              <Controller name='6-Button' default='true' inheritFrom='3-Button'>
                <Mapping name='A' input='ButtonRightShoulder' />
                <Mapping name='A' input='ButtonY' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the parent's A is replaced (not duplicated), and both child A entries survive —
        // the resolver will later accumulate them into buttonToInput["A"] = ["ButtonRightShoulder", "ButtonY"]
        result.Controllers[1].Mappings.Select(m => (m.Name, m.Input))
            .ShouldBe([("Start", "ButtonStart"), ("A", "ButtonRightShoulder"), ("A", "ButtonY")]);
    }

    [Fact]
    public void LoadPlatformMapping_InheritFrom_ForwardReference_ResolvesCorrectly()
    {
        // given a Controllers.xml where the inheriting controller appears before the base in document order
        string path = Path.Combine(DefaultsDir, "Controllers", "Sega Genesis.xml");
        StubXml(path, """
            <Controllers>
              <Controller name='6-Button' inheritFrom='3-Button'>
                <Mapping name='Y' input='ButtonY' />
              </Controller>
              <Controller name='3-Button'>
                <Mapping name='A' input='ButtonX' />
                <Mapping name='Start' input='ButtonStart' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs (two-pass: all parsed first, then inheritFrom resolved)
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the forward reference resolves: base mappings prepended before 6-Button's own
        result.Controllers[0].Name.ShouldBe("6-Button");
        result.Controllers[0].Mappings.Select(m => (m.Name, m.Input))
            .ShouldBe([("A", "ButtonX"), ("Start", "ButtonStart"), ("Y", "ButtonY")]);
    }

    [Fact]
    public void LoadPlatformMapping_InheritFrom_UnknownController_LogsErrorAndUsesOwnMappingsOnly()
    {
        // given a Controllers.xml where inheritFrom names a controller that doesn't exist
        string path = Path.Combine(DefaultsDir, "Controllers", "Sega Genesis.xml");
        StubXml(path, """
            <Controllers>
              <Controller name='6-Button' inheritFrom='DoesNotExist'>
                <Mapping name='Y' input='ButtonY' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then only the controller's own mappings are kept and an error is logged
        result.Controllers[0].Mappings.Select(m => (m.Name, m.Input))
            .ShouldBe([("Y", "ButtonY")]);
        _logger.Received().Error(Arg.Is<string>(s =>
            s.Contains("6-Button") && s.Contains("DoesNotExist")));
    }

    [Fact]
    public void LoadPlatformMapping_NoInheritFrom_ControllerUnaffected()
    {
        // given a Controllers.xml with a controller that has no inheritFrom attribute
        string path = Path.Combine(DefaultsDir, "Controllers", "Sega Genesis.xml");
        StubXml(path, """
            <Controllers>
              <Controller name='Pad'>
                <Mapping name='A' input='ButtonA' />
                <Mapping name='B' input='ButtonB' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the controller's mappings are unchanged — no base prepended
        result.Controllers[0].Mappings.Select(m => (m.Name, m.Input))
            .ShouldBe([("A", "ButtonA"), ("B", "ButtonB")]);
    }

    [Fact]
    public void LoadPlatformMapping_InheritFrom_IsTransitive()
    {
        // given A inherits from B which inherits from C — A merges the whole chain root-first,
        // so C's own mappings come before B's, which come before A's.
        string path = Path.Combine(DefaultsDir, "Controllers", "Sega Genesis.xml");
        StubXml(path, """
            <Controllers>
              <Controller name='C'>
                <Mapping name='X' input='ButtonX' />
              </Controller>
              <Controller name='B' inheritFrom='C'>
                <Mapping name='Y' input='ButtonY' />
              </Controller>
              <Controller name='A' inheritFrom='B'>
                <Mapping name='Z' input='ButtonZ' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then A gets C's mapping (X) and B's mapping (Y) prepended before its own (Z), in that order
        result.Controllers[2].Name.ShouldBe("A");
        result.Controllers[2].Mappings.Select(m => (m.Name, m.Input))
            .ShouldBe([("X", "ButtonX"), ("Y", "ButtonY"), ("Z", "ButtonZ")]);

        // and the intermediate controller B is itself fully resolved (C's mapping before its own)
        result.Controllers[1].Name.ShouldBe("B");
        result.Controllers[1].Mappings.Select(m => (m.Name, m.Input))
            .ShouldBe([("X", "ButtonX"), ("Y", "ButtonY")]);
    }

    [Fact]
    public void LoadPlatformMapping_InheritFrom_Cycle_LogsErrorAndStopsWithoutLooping()
    {
        // given a cycle A -> B -> A in the inheritFrom chain
        string path = Path.Combine(DefaultsDir, "Controllers", "Sega Genesis.xml");
        StubXml(path, """
            <Controllers>
              <Controller name='A' inheritFrom='B'>
                <Mapping name='Za' input='ButtonZ' />
              </Controller>
              <Controller name='B' inheritFrom='A'>
                <Mapping name='Yb' input='ButtonY' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then resolution terminates (no infinite loop): A accumulates B's own then its own,
        // and the cycle is reported. Each mapping name appears once — no duplicate re-entry.
        result.Controllers[0].Name.ShouldBe("A");
        result.Controllers[0].Mappings.Select(m => (m.Name, m.Input))
            .ShouldBe([("Yb", "ButtonY"), ("Za", "ButtonZ")]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("cycle")));
    }

    // ---- LoadPlatformMapping: root-level (cross-file) inheritFrom ----

    [Fact]
    public void LoadPlatformMapping_RootInheritFrom_PullsInBaseFileControllers()
    {
        // given a platform file that only points at a shared base file (no own controllers)
        string platformPath = Path.Combine(DefaultsDir, "Controllers", "Sega Genesis.xml");
        string basePath = Path.Combine(DefaultsDir, "Controllers", "_SharedBase.xml");
        StubXml(platformPath, """<Controllers inheritFrom="_SharedBase" />""");
        StubXml(basePath, """
            <Controllers>
              <Controller name='Pad' default='true'>
                <Mapping name='A' input='ButtonA' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the base file's controllers are used wholesale
        result.Controllers.Select(c => c.Name).ShouldBe(["Pad"]);
        result.Controllers[0].IsDefault.ShouldBeTrue();
        result.Controllers[0].Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonA")]);
    }

    [Fact]
    public void LoadPlatformMapping_RootInheritFrom_OwnControllersOverrideByNameAndAppendNew()
    {
        // given a base with Pad+Extra and a platform that overrides Pad and adds a new controller
        string platformPath = Path.Combine(DefaultsDir, "Controllers", "Sega Genesis.xml");
        string basePath = Path.Combine(DefaultsDir, "Controllers", "_SharedBase.xml");
        StubXml(basePath, """
            <Controllers>
              <Controller name='Pad' default='true'>
                <Mapping name='A' input='ButtonA' />
              </Controller>
              <Controller name='Extra'>
                <Mapping name='E' input='ButtonE' />
              </Controller>
            </Controllers>
            """);
        StubXml(platformPath, """
            <Controllers inheritFrom="_SharedBase">
              <Controller name='Pad' default='true'>
                <Mapping name='A' input='ButtonB' />
              </Controller>
              <Controller name='Special'>
                <Mapping name='S' input='ButtonS' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then Pad is overridden in place (ButtonB), Extra survives from the base, and the new
        // Special controller is appended after the base entries
        result.Controllers.Select(c => c.Name).ShouldBe(["Pad", "Extra", "Special"]);
        result.Controllers[0].Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonB")]);
        result.Controllers[1].Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("E", "ButtonE")]);
        result.Controllers[2].Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("S", "ButtonS")]);
    }

    [Fact]
    public void LoadPlatformMapping_RootInheritFrom_ControllerLevelInheritanceCrossesFileBoundary()
    {
        // given a base that defines 3-Button and a platform that adds 6-Button inheritFrom="3-Button".
        // The controller-level inheritFrom must resolve against the merged (base + own) set.
        string platformPath = Path.Combine(DefaultsDir, "Controllers", "Sega Genesis.xml");
        string basePath = Path.Combine(DefaultsDir, "Controllers", "_SharedBase.xml");
        StubXml(basePath, """
            <Controllers>
              <Controller name='3-Button'>
                <Mapping name='A' input='ButtonX' />
                <Mapping name='Start' input='ButtonStart' />
              </Controller>
            </Controllers>
            """);
        StubXml(platformPath, """
            <Controllers inheritFrom="_SharedBase">
              <Controller name='6-Button' inheritFrom='3-Button' default='true'>
                <Mapping name='Y' input='ButtonY' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then 6-Button inherits the base file's 3-Button mappings before its own
        result.Controllers.Select(c => c.Name).ShouldBe(["3-Button", "6-Button"]);
        result.Controllers[1].Mappings.Select(m => (m.Name, m.Input))
            .ShouldBe([("A", "ButtonX"), ("Start", "ButtonStart"), ("Y", "ButtonY")]);
    }

    [Fact]
    public void LoadPlatformMapping_RootInheritFrom_IsTransitiveAcrossFiles()
    {
        // given a platform -> _Mid -> _Root chain of files
        string platformPath = Path.Combine(DefaultsDir, "Controllers", "Sega Genesis.xml");
        string midPath = Path.Combine(DefaultsDir, "Controllers", "_Mid.xml");
        string rootPath = Path.Combine(DefaultsDir, "Controllers", "_Root.xml");
        StubXml(platformPath, """<Controllers inheritFrom="_Mid" />""");
        StubXml(midPath, """<Controllers inheritFrom="_Root" />""");
        StubXml(rootPath, """
            <Controllers>
              <Controller name='Pad' default='true'>
                <Mapping name='A' input='ButtonA' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the root file's controllers resolve through both hops
        result.Controllers.Select(c => c.Name).ShouldBe(["Pad"]);
        result.Controllers[0].Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonA")]);
    }

    [Fact]
    public void LoadPlatformMapping_RootInheritFrom_MissingBaseFile_LogsErrorAndUsesOwn()
    {
        // given a platform file that points at a base file which does not exist
        string platformPath = Path.Combine(DefaultsDir, "Controllers", "Sega Genesis.xml");
        StubXml(platformPath, """
            <Controllers inheritFrom="_DoesNotExist">
              <Controller name='Pad'>
                <Mapping name='A' input='ButtonA' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then only the platform's own controllers are used and the missing base is logged
        result.Controllers.Select(c => c.Name).ShouldBe(["Pad"]);
        result.Controllers[0].Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonA")]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("_DoesNotExist") && s.Contains("does not exist")));
    }

    [Fact]
    public void LoadPlatformMapping_RootInheritFrom_Cycle_LogsErrorAndStopsWithoutLooping()
    {
        // given two files that inheritFrom each other
        string platformPath = Path.Combine(DefaultsDir, "Controllers", "Sega Genesis.xml");
        string otherPath = Path.Combine(DefaultsDir, "Controllers", "_Other.xml");
        StubXml(platformPath, """
            <Controllers inheritFrom="_Other">
              <Controller name='Pad'>
                <Mapping name='A' input='ButtonA' />
              </Controller>
            </Controllers>
            """);
        StubXml(otherPath, """
            <Controllers inheritFrom="Sega Genesis">
              <Controller name='OtherPad'>
                <Mapping name='B' input='ButtonB' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then resolution terminates and the cycle is reported; the merged set is finite
        result.Controllers.Select(c => c.Name).ShouldBe(["Pad", "OtherPad"]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("cycle")));
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
