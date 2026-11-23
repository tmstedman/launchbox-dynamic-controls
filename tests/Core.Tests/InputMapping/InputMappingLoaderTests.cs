using DynamicControls.InputMapping;
using NSubstitute;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;

namespace DynamicControls.Core.Tests.InputMapping;

/// <summary>
/// Unit tests for <see cref="InputMappingLoader"/>. The loader parses a platform's Controllers.xml —
/// its controllers and its per-game entries — into raw DTOs. Filesystem is a
/// substitute so each test supplies a literal XML string and a stubbed FileExists answer; path
/// construction is verified by the precise path the loader probes.
/// </summary>
public class InputMappingLoaderTests
{
    private const string RootDir = @"C:\plugin";
    private static readonly string DefaultsDir = Path.Combine(RootDir, "Defaults");
    private static readonly string UserDir = Path.Combine(RootDir, "User");

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

    // ---- LoadPlatformMapping: <Game> entries ----

    private static readonly string GenesisControllers = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");

    private GameMappingConfig? LoadGame(string gameXml, GameInfo? game = null)
    {
        StubXml(GenesisControllers, $"""
            <Controllers default='Pad'>
              <Controller name='Pad' />
              {gameXml}
            </Controllers>
            """);
        return _underTest.LoadPlatformMapping("Sega Genesis")!.FindGame(game ?? Game());
    }

    [Fact]
    public void LoadPlatformMapping_Game_ParsesInheritFromAndMappingEntries()
    {
        // given an entry selecting a controller and remapping two buttons
        var result = LoadGame("""
            <Game romName='OutRun' inheritFrom='Zapper'>
              <Mapping name='Trigger' input='ButtonA' />
              <Mapping name='Start' input='ButtonStart' />
            </Game>
            """)!;

        // then the controller and both entries are captured in document order
        result.InheritFrom.ShouldBe("Zapper");
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
    public void LoadPlatformMapping_GameAnalogToDigital_IsCaseInsensitive(string attrValue, AnalogToDigitalMode expected)
    {
        // given an entry with analogToDigital in varying case
        var result = LoadGame($"<Game romName='OutRun' analogToDigital='{attrValue}' />")!;

        // then the mode is parsed to the matching enumerator regardless of case
        result.AnalogToDigital.ShouldBe(expected);
    }

    [Fact]
    public void LoadPlatformMapping_GameAnalogToDigitalNone_IsStatedWithNoMode()
    {
        // given an entry switching the mirror off
        var result = LoadGame("<Game romName='OutRun' analogToDigital='none' />")!;

        // then it states a value, which is no mirroring, rather than inheriting the controller's
        result.StatesAnalogToDigital.ShouldBeTrue();
        result.AnalogToDigital.ShouldBeNull();
    }

    [Fact]
    public void LoadPlatformMapping_GameInvalidAnalogToDigital_LogsErrorAndLeavesUnset()
    {
        // given an entry with an unrecognized analogToDigital value
        var result = LoadGame("<Game romName='OutRun' analogToDigital='diagonal' />")!;

        // then AnalogToDigital is left null and the invalid value is logged
        result.AnalogToDigital.ShouldBeNull();
        _logger.Received().Error(Arg.Is<string>(s =>
            s.Contains("diagonal") && s.Contains("analogToDigital")));
    }

    [Fact]
    public void LoadPlatformMapping_GameMappingEntryMissingNameOrInput_SkippedWithError()
    {
        // given an entry with one valid mapping, one missing name, and one missing input
        var result = LoadGame("""
            <Game romName='OutRun'>
              <Mapping name='A' input='ButtonA' />
              <Mapping input='ButtonB' />
              <Mapping name='C' />
            </Game>
            """)!;

        // then only the complete entry is kept; each malformed entry produced an error log
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonA")]);
        _logger.Received(2).Error(Arg.Is<string>(s => s.Contains("missing 'name' or 'input'")));
    }

    [Fact]
    public void LoadPlatformMapping_GameUnmap_ParsesNameIntoUnmapsList()
    {
        // given an entry with a mix of <Mapping> and <Unmap> elements
        var result = LoadGame("""
            <Game romName='OutRun'>
              <Mapping name='A' input='ButtonA' />
              <Unmap name='B' />
              <Unmap name='Mode' />
            </Game>
            """)!;

        // then Unmaps collects the names; Mappings is unaffected
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonA")]);
        result.Unmaps.ShouldBe(["B", "Mode"]);
    }

    [Fact]
    public void LoadPlatformMapping_GameUnmapMissingName_SkippedWithError()
    {
        // given an entry with an <Unmap> that has no name attribute
        var result = LoadGame("""
            <Game romName='OutRun'>
              <Unmap name='B' />
              <Unmap />
            </Game>
            """)!;

        // then the malformed element is dropped and the rest survive
        result.Unmaps.ShouldBe(["B"]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("<Unmap>") && s.Contains("missing 'name'")));
    }

    [Fact]
    public void LoadPlatformMapping_GameControl_IsParsedAlongsideMappings()
    {
        // given an entry declaring a control of its own
        var result = LoadGame("""
            <Game romName='OutRun'>
              <Control name='Pedals' mappings='A B' />
              <Mapping name='A' input='ButtonX' />
            </Game>
            """)!;

        // then the control is read as a controller's would be
        result.Controls.Select(c => (c.Name, string.Join(" ", c.Mappings))).ShouldBe([("Pedals", "A B")]);
        _logger.DidNotReceive().Error(Arg.Any<string>());
    }

    [Fact]
    public void LoadPlatformMapping_GameUnrecognisedElement_SkippedWithError()
    {
        // given an entry with a typo'd element
        var result = LoadGame("""
            <Game romName='OutRun'>
              <Mappin name='A' input='ButtonA' />
            </Game>
            """)!;

        // then it is logged rather than silently dropped
        result.Mappings.ShouldBeEmpty();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("<Mappin>") && s.Contains("romName=\"OutRun\"")));
    }

    [Fact]
    public void LoadPlatformMapping_GameWithNeitherKey_SkippedWithError()
    {
        // given an entry with no launchBoxId and no romName
        StubXml(GenesisControllers, """
            <Controllers>
              <Game inheritFrom='Pad' />
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the entry, which nothing could find, is dropped and logged
        result.Games.ShouldBeEmpty();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("<Game>") && s.Contains("launchBoxId or a romName")));
    }

    [Fact]
    public void LoadPlatformMapping_Game_IsFoundByLaunchBoxIdBeforeRomName()
    {
        // given one entry keyed by the game's LaunchBox ID and another by its ROM name
        StubXml(GenesisControllers, """
            <Controllers>
              <Game romName='OutRun' inheritFrom='ByName' />
              <Game launchBoxId='42' inheritFrom='ById' />
            </Controllers>
            """);

        // when the game is looked up
        var platform = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then its ID finds it first, and its ROM name only when no ID matches
        platform.FindGame(Game(launchBoxId: 42))!.InheritFrom.ShouldBe("ById");
        platform.FindGame(Game(launchBoxId: 7))!.InheritFrom.ShouldBe("ByName");
    }

    [Fact]
    public void LoadPlatformMapping_Game_RomNameMatchesIgnoringCaseButNotTags()
    {
        // given an entry for one dump of a game
        StubXml(GenesisControllers, """
            <Controllers>
              <Game romName='OutRun (USA, Europe)' />
            </Controllers>
            """);

        // when the game is looked up
        var platform = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then a difference of case still finds it, but another region's dump does not: a mapping
        // is one ROM's setup, unlike a label
        platform.FindGame(Game(romName: "outrun (usa, europe)")).ShouldNotBeNull();
        platform.FindGame(Game(romName: "OutRun (Japan)")).ShouldBeNull();
    }

    [Fact]
    public void LoadPlatformMapping_TwoGamesWithTheSameKey_KeepsTheFirstAndLogs()
    {
        // given two entries in one file naming the same ROM
        StubXml(GenesisControllers, """
            <Controllers>
              <Game romName='OutRun' inheritFrom='First' />
              <Game romName='OUTRUN' inheritFrom='Second' />
            </Controllers>
            """);

        // when the loader runs
        var platform = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the first stands and the second is reported
        platform.Games.Select(g => g.InheritFrom).ShouldBe(["First"]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("OUTRUN") && s.Contains("already names that game")));
    }

    [Fact]
    public void LoadPlatformMapping_UserGame_ReplacesTheShippedEntryItSharesEitherKeyWith()
    {
        // given shipped entries for two games, and a User entry naming one of them by ROM name only
        // where the shipped entry also has an ID
        StubXml(GenesisControllers, """
            <Controllers>
              <Game launchBoxId='42' romName='OutRun' inheritFrom='Shipped'>
                <Mapping name='A' input='ButtonA' />
              </Game>
              <Game romName='Aladdin' inheritFrom='Shipped' />
            </Controllers>
            """);
        StubXml(Path.Combine(UserDir, "Platforms", "Sega Genesis", "Controllers.xml"), """
            <Controllers>
              <Game romName='OutRun' inheritFrom='Mine' />
              <Game romName='Sonic' inheritFrom='Mine' />
            </Controllers>
            """);

        // when the loader runs
        var platform = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the user's entry replaces the shipped one whole, so the game's ID no longer finds the
        // shipped entry; the other shipped game is kept, and the user's new game is added
        platform.FindGame(Game(launchBoxId: 42))!.InheritFrom.ShouldBe("Mine");
        platform.FindGame(Game(launchBoxId: 42))!.Mappings.ShouldBeEmpty();
        platform.Games.Select(g => (g.RomName, g.InheritFrom)).ShouldBe(
        [
            ("OutRun", "Mine"),
            ("Aladdin", "Shipped"),
            ("Sonic", "Mine"),
        ]);
    }

    [Fact]
    public void LoadPlatformMapping_UserGame_IsFoundBeforeAShippedOneKeyedDifferently()
    {
        // given a shipped entry keyed by the game's ID alone, and a User entry by its ROM name alone,
        // which share no key for the merge to replace one with the other
        StubXml(GenesisControllers, """
            <Controllers>
              <Game launchBoxId='42' inheritFrom='Shipped' />
            </Controllers>
            """);
        StubXml(Path.Combine(UserDir, "Platforms", "Sega Genesis", "Controllers.xml"), """
            <Controllers>
              <Game romName='OutRun' inheritFrom='Mine' />
            </Controllers>
            """);

        // when the game is looked up by both
        var platform = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the user's entry wins, though the shipped one matches by the key tried first
        platform.FindGame(Game(romName: "OutRun", launchBoxId: 42))!.InheritFrom.ShouldBe("Mine");
        platform.FindGame(Game(romName: "Other", launchBoxId: 42))!.InheritFrom.ShouldBe("Shipped");
    }

    [Fact]
    public void LoadPlatformMapping_RootInheritFrom_DoesNotBringInTheBaseFilesGames()
    {
        // given a base platform with a game entry, and a platform pointing at it
        StubXml(GenesisControllers, """<Controllers inheritFrom="_SharedBase" />""");
        StubXml(Path.Combine(DefaultsDir, "Platforms", "_SharedBase", "Controllers.xml"), """
            <Controllers default='Pad'>
              <Controller name='Pad' />
              <Game romName='OutRun' inheritFrom='Pad' />
            </Controllers>
            """);

        // when the loader runs
        var platform = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the controllers arrive but the game, which belongs to the base platform, doesn't
        platform.Controllers.Select(c => c.Name).ShouldBe(["Pad"]);
        platform.Games.ShouldBeEmpty();
    }

    [Fact]
    public void LoadPlatformMapping_UnrecognisedRootElement_SkippedWithError()
    {
        // given a file with a typo'd root child
        StubXml(GenesisControllers, """
            <Controllers>
              <Controler name='Pad' />
            </Controllers>
            """);

        // when the loader runs
        _underTest.LoadPlatformMapping("Sega Genesis");

        // then it is logged rather than silently dropped
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("<Controler>") && s.Contains("expected <Controller> or <Game>")));
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
        // given a platform controllers file at Platforms/{safePlatform}/Controllers.xml
        string safePlatform = "Sega/Genesis".SafeFileName();
        string expected = Path.Combine(DefaultsDir, "Platforms", safePlatform, "Controllers.xml");
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
        string path = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
        StubXml(path, """
            <Controllers default='Pad-6btn'>
              <Controller name='Pad-3btn'>
                <Mapping name='A' input='ButtonA' />
              </Controller>
              <Controller name='Pad-6btn' analogToDigital='left'>
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
        string path = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
        StubXml(path, """
            <Controllers default='6-Button'>
              <Controller name='3-Button'>
                <Mapping name='A' input='ButtonX' />
                <Mapping name='Start' input='ButtonStart' />
              </Controller>
              <Controller name='6-Button' inheritFrom='3-Button'>
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

    // --- <Control>: one platform button standing for several ---

    [Fact]
    public void LoadPlatformMapping_Control_IsParsedAlongsideMappings()
    {
        string path = Path.Combine(DefaultsDir, "Platforms", "Nintendo 64", "Controllers.xml");
        StubXml(path, """
            <Controllers default='Pad'>
              <Controller name='Pad'>
                <Mapping name='C-Up' input='AxisRightStickUp' />
                <Mapping name='C-Down' input='AxisRightStickDown' />
                <Control name='C-Any' mappings='C-Up C-Down' />
              </Controller>
            </Controllers>
            """);

        var result = _underTest.LoadPlatformMapping("Nintendo 64")!;

        ControlEntry control = result.Controllers[0].Controls.ShouldHaveSingleItem();
        control.Name.ShouldBe("C-Any");
        control.Mappings.ShouldBe(["C-Up", "C-Down"]);
    }

    [Fact]
    public void LoadPlatformMapping_UnrecognisedElementInController_SkippedWithError()
    {
        // A typo'd element used to be dropped without a word, which is how a whole button goes
        // missing with nothing to read afterwards.
        string path = Path.Combine(DefaultsDir, "Platforms", "Nintendo 64", "Controllers.xml");
        StubXml(path, """
            <Controllers default='Pad'>
              <Controller name='Pad'>
                <Mapping name='C-Up' input='AxisRightStickUp' />
                <Mappping name='C-Down' input='AxisRightStickDown' />
              </Controller>
            </Controllers>
            """);

        var result = _underTest.LoadPlatformMapping("Nintendo 64")!;

        result.Controllers[0].Mappings.ShouldHaveSingleItem().Name.ShouldBe("C-Up");
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Mappping") && s.Contains("Pad")));
    }

    [Fact]
    public void LoadPlatformMapping_Control_InheritsAndIsReplacedByNameLikeAMapping()
    {
        // A descendant redeclaring a control replaces it outright rather than adding members, so
        // what a name stands for is always stated in one place.
        string path = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
        StubXml(path, """
            <Controllers default='6-Button'>
              <Controller name='3-Button'>
                <Control name='Dpad-Any' mappings='Dpad-Up Dpad-Down' />
                <Control name='Stick-Any' mappings='L-Up L-Down' />
              </Controller>
              <Controller name='6-Button' inheritFrom='3-Button'>
                <Control name='Dpad-Any' mappings='Dpad-Up Dpad-Down Dpad-Left Dpad-Right' />
              </Controller>
            </Controllers>
            """);

        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        result.Controllers[1].Controls.Select(a => (a.Name, string.Join(" ", a.Mappings)))
            .ShouldBe([("Dpad-Any", "Dpad-Up Dpad-Down Dpad-Left Dpad-Right"), ("Stick-Any", "L-Up L-Down")]);
    }

    [Theory]
    [InlineData("<Control mappings='A B' />", "name")]
    [InlineData("<Control name='X' />", "mappings")]
    [InlineData("<Control name='X' mappings='A' />", "mappings")]
    public void LoadPlatformMapping_IncompleteControl_IsLoggedAndSkipped(string control, string expected)
    {
        // A single member is that button under another name, which the mappings already express.
        string path = Path.Combine(DefaultsDir, "Platforms", "Arcade", "Controllers.xml");
        StubXml(path, $"""
            <Controllers default='Pad'>
              <Controller name='Pad'>
                <Mapping name='A' input='ButtonA' />
                {control}
              </Controller>
            </Controllers>
            """);

        var result = _underTest.LoadPlatformMapping("Arcade")!;

        result.Controllers[0].Controls.ShouldBeEmpty();
        result.Controllers[0].Mappings.ShouldHaveSingleItem();   // parsing carried on
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Control") && s.Contains(expected)));
    }

    [Fact]
    public void LoadPlatformMapping_InheritFrom_SameNameInChild_ReplacesParentEntry()
    {
        // given a controller that inherits a mapping and redeclares the same button Name
        string path = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
        StubXml(path, """
            <Controllers default='6-Button'>
              <Controller name='3-Button'>
                <Mapping name='A' input='ButtonX' />
                <Mapping name='Start' input='ButtonStart' />
              </Controller>
              <Controller name='6-Button' inheritFrom='3-Button'>
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
        string path = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
        StubXml(path, """
            <Controllers default='6-Button'>
              <Controller name='3-Button'>
                <Mapping name='A' input='ButtonX' />
                <Mapping name='A' input='ButtonRightShoulder' />
                <Mapping name='Start' input='ButtonStart' />
              </Controller>
              <Controller name='6-Button' inheritFrom='3-Button'>
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
        string path = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
        StubXml(path, """
            <Controllers default='6-Button'>
              <Controller name='3-Button'>
                <Mapping name='A' input='ButtonX' />
                <Mapping name='Start' input='ButtonStart' />
              </Controller>
              <Controller name='6-Button' inheritFrom='3-Button'>
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
        string path = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
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
        string path = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
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
        string path = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
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
    public void LoadPlatformMapping_InheritFrom_InheritsAnalogToDigitalThroughTheChain()
    {
        // given A inherits from B which inherits from C, and only C sets analogToDigital
        StubXml(Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml"), """
            <Controllers>
              <Controller name='C' analogToDigital='left' />
              <Controller name='B' inheritFrom='C' />
              <Controller name='A' inheritFrom='B' />
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then every level mirrors as C does, since none of them says otherwise
        result.Controllers.Select(c => (c.Name, c.AnalogToDigital)).ShouldBe(
        [
            ("C", AnalogToDigitalMode.Left),
            ("B", AnalogToDigitalMode.Left),
            ("A", AnalogToDigitalMode.Left),
        ]);
    }

    [Fact]
    public void LoadPlatformMapping_InheritFrom_OwnAnalogToDigitalWins_AndNoneSwitchesItOff()
    {
        // given a base mirroring left, one descendant mirroring right, and one with a stick of
        // its own saying none
        StubXml(Path.Combine(DefaultsDir, "Platforms", "Sega Saturn", "Controllers.xml"), """
            <Controllers>
              <Controller name='Control Pad' analogToDigital='left' />
              <Controller name='Right' inheritFrom='Control Pad' analogToDigital='right' />
              <Controller name='3D Control Pad' inheritFrom='Control Pad' analogToDigital='None' />
              <Controller name='Below 3D' inheritFrom='3D Control Pad' />
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Saturn")!;

        // then each descendant's own statement wins, and none is inherited like any other value
        result.Controllers.Select(c => (c.Name, c.AnalogToDigital)).ShouldBe(
        [
            ("Control Pad", AnalogToDigitalMode.Left),
            ("Right", AnalogToDigitalMode.Right),
            ("3D Control Pad", null),
            ("Below 3D", null),
        ]);
        _logger.DidNotReceive().Error(Arg.Any<string>());
    }

    [Fact]
    public void LoadPlatformMapping_InheritFrom_UnmapDropsABaseButton()
    {
        // given a base with A, B and C, and a descendant without B
        StubXml(Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml"), """
            <Controllers>
              <Controller name='Base'>
                <Mapping name='A' input='ButtonA' />
                <Mapping name='B' input='ButtonB' />
                <Mapping name='C' input='ButtonX' />
              </Controller>
              <Controller name='Fewer' inheritFrom='Base'>
                <Unmap name='B' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the descendant has the base's buttons but B
        result.Controllers[1].Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonA"), ("C", "ButtonX")]);
    }

    [Fact]
    public void LoadPlatformMapping_InheritFrom_UnmapDropsABaseControl()
    {
        // given a base with a control, and a descendant that doesn't have it
        StubXml(Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml"), """
            <Controllers>
              <Controller name='Base'>
                <Control name='Dpad-Any' mappings='Dpad-Up Dpad-Down' />
                <Mapping name='Dpad-Up' input='ButtonDpadUp' />
                <Mapping name='Dpad-Down' input='ButtonDpadDown' />
              </Controller>
              <Controller name='Child' inheritFrom='Base'>
                <Unmap name='Dpad-Any' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the control is gone from the descendant, as an unmapped button would be
        result.Controllers[1].Controls.ShouldBeEmpty();
        result.Controllers[1].Mappings.Select(m => m.Name).ShouldBe(["Dpad-Up", "Dpad-Down"]);
    }

    [Fact]
    public void LoadPlatformMapping_InheritFrom_IsTransitive()
    {
        // given A inherits from B which inherits from C — A merges the whole chain root-first,
        // so C's own mappings come before B's, which come before A's.
        string path = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
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
        string path = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
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
        string platformPath = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
        string basePath = Path.Combine(DefaultsDir, "Platforms", "_SharedBase", "Controllers.xml");
        StubXml(platformPath, """<Controllers inheritFrom="_SharedBase" />""");
        StubXml(basePath, """
            <Controllers default='Pad'>
              <Controller name='Pad'>
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
        string platformPath = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
        string basePath = Path.Combine(DefaultsDir, "Platforms", "_SharedBase", "Controllers.xml");
        StubXml(basePath, """
            <Controllers default='Pad'>
              <Controller name='Pad'>
                <Mapping name='A' input='ButtonA' />
              </Controller>
              <Controller name='Extra'>
                <Mapping name='E' input='ButtonE' />
              </Controller>
            </Controllers>
            """);
        StubXml(platformPath, """
            <Controllers default='Pad' inheritFrom="_SharedBase">
              <Controller name='Pad'>
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
    public void LoadPlatformMapping_RootInheritFrom_NewControllerMarkedDefault_OverridesBaseDefault()
    {
        // given a base with a default "Pad", and a platform that adds a new, unrelated controller
        // also marked default (e.g. a CD add-on introducing a controller the base platform lacks)
        string platformPath = Path.Combine(DefaultsDir, "Platforms", "NEC PC Engine-CD", "Controllers.xml");
        string basePath = Path.Combine(DefaultsDir, "Platforms", "_SharedBase", "Controllers.xml");
        StubXml(basePath, """
            <Controllers default='Pad'>
              <Controller name='Pad'>
                <Mapping name='A' input='ButtonA' />
              </Controller>
            </Controllers>
            """);
        StubXml(platformPath, """
            <Controllers default='CD-Pad' inheritFrom="_SharedBase">
              <Controller name='CD-Pad'>
                <Mapping name='A' input='ButtonB' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("NEC PC Engine-CD")!;

        // then the new controller's default wins — Pad's is cleared, not left as a second default
        result.Controllers.Select(c => (c.Name, c.IsDefault)).ShouldBe(
        [
            ("Pad", false),
            ("CD-Pad", true),
        ]);
        result.Resolve(null)!.Name.ShouldBe("CD-Pad");
    }

    [Fact]
    public void LoadPlatformMapping_DefaultOnAController_IsLoggedAndIgnored()
    {
        // given a file in the old form, marking its default on a controller
        StubXml(Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml"), """
            <Controllers>
              <Controller name='3-Button' />
              <Controller name='6-Button' default='true' />
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the attribute is reported with where the default belongs, and nothing is default
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("6-Button") && s.Contains("<Controllers default=")));
        result.Controllers.ShouldAllBe(c => !c.IsDefault);
        result.Resolve(null)!.Name.ShouldBe("3-Button");
    }

    [Fact]
    public void LoadPlatformMapping_RootDefaultNamingNoController_IsLoggedAndTheNextOneApplies()
    {
        // given a user file whose default is misspelt, over a shipped one with a default of its own
        StubXml(Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml"), """
            <Controllers default='6-Button'>
              <Controller name='3-Button' />
              <Controller name='6-Button' />
            </Controllers>
            """);
        StubXml(Path.Combine(UserDir, "Platforms", "Sega Genesis", "Controllers.xml"),
            """<Controllers default='6-Buton' />""");

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the typo is reported and costs nothing but its own line: the shipped default stands
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("6-Buton") && s.Contains("names no controller")));
        result.Resolve(null)!.Name.ShouldBe("6-Button");
    }

    [Fact]
    public void LoadPlatformMapping_RootDefaultNamingNoController_FallsThroughToTheBasePlatforms()
    {
        // given a platform pointing at a base, with a default naming a controller neither has
        StubXml(Path.Combine(DefaultsDir, "Platforms", "_SharedBase", "Controllers.xml"), """
            <Controllers default='Pad'>
              <Controller name='Stick' />
              <Controller name='Pad' />
            </Controllers>
            """);
        StubXml(Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml"),
            """<Controllers default='Missing' inheritFrom='_SharedBase' />""");

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the base's default applies, not the first controller the merge happens to list
        result.Resolve(null)!.Name.ShouldBe("Pad");
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Missing")));
    }

    [Fact]
    public void LoadPlatformMapping_NoRootDefault_TheFirstControllerIsUsed()
    {
        // given a file naming no default
        StubXml(Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml"), """
            <Controllers>
              <Controller name='3-Button' />
              <Controller name='6-Button' />
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then none is marked, and resolution takes the first in document order
        result.Controllers.ShouldAllBe(c => !c.IsDefault);
        result.Resolve(null)!.Name.ShouldBe("3-Button");
        _logger.DidNotReceive().Error(Arg.Any<string>());
    }

    [Fact]
    public void LoadPlatformMapping_RootInheritFrom_OwnHasNoDefault_BaseDefaultIsUnaffected()
    {
        // given a base with a default "Pad", and a platform that adds a new controller with no
        // default attribute at all
        string platformPath = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
        string basePath = Path.Combine(DefaultsDir, "Platforms", "_SharedBase", "Controllers.xml");
        StubXml(basePath, """
            <Controllers default='Pad'>
              <Controller name='Pad'>
                <Mapping name='A' input='ButtonA' />
              </Controller>
            </Controllers>
            """);
        StubXml(platformPath, """
            <Controllers inheritFrom="_SharedBase">
              <Controller name='Special'>
                <Mapping name='S' input='ButtonS' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the base's default is untouched — the fix only intervenes when own declares a default
        result.Resolve(null)!.Name.ShouldBe("Pad");
    }

    [Fact]
    public void LoadPlatformMapping_RootInheritFrom_ControllerLevelInheritanceCrossesFileBoundary()
    {
        // given a base that defines 3-Button and a platform that adds 6-Button inheritFrom="3-Button".
        // The controller-level inheritFrom must resolve against the merged (base + own) set.
        string platformPath = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
        string basePath = Path.Combine(DefaultsDir, "Platforms", "_SharedBase", "Controllers.xml");
        StubXml(basePath, """
            <Controllers>
              <Controller name='3-Button'>
                <Mapping name='A' input='ButtonX' />
                <Mapping name='Start' input='ButtonStart' />
              </Controller>
            </Controllers>
            """);
        StubXml(platformPath, """
            <Controllers default='6-Button' inheritFrom="_SharedBase">
              <Controller name='6-Button' inheritFrom='3-Button'>
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
        string platformPath = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
        string midPath = Path.Combine(DefaultsDir, "Platforms", "_Mid", "Controllers.xml");
        string rootPath = Path.Combine(DefaultsDir, "Platforms", "_Root", "Controllers.xml");
        StubXml(platformPath, """<Controllers inheritFrom="_Mid" />""");
        StubXml(midPath, """<Controllers inheritFrom="_Root" />""");
        StubXml(rootPath, """
            <Controllers default='Pad'>
              <Controller name='Pad'>
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
        string platformPath = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
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
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("_DoesNotExist") && s.Contains("found in neither")));
    }

    [Fact]
    public void LoadPlatformMapping_RootInheritFrom_Cycle_LogsErrorAndStopsWithoutLooping()
    {
        // given two files that inheritFrom each other
        string platformPath = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
        string otherPath = Path.Combine(DefaultsDir, "Platforms", "_Other", "Controllers.xml");
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

        // then resolution terminates and the cycle is reported at the file it started from; the
        // merged set is finite, the base's controller first
        result.Controllers.Select(c => c.Name).ShouldBe(["OtherPad", "Pad"]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("cycle")));
    }

    // ---- LoadPlatformMapping: a User file merged over the shipped one ----

    [Fact]
    public void LoadPlatformMapping_UserFileOnly_IsLoaded()
    {
        // given a controllers file in the User layer and none shipped
        StubXml(Path.Combine(UserDir, "Platforms", "Sega Genesis", "Controllers.xml"), """
            <Controllers>
              <Controller name='Pad'>
                <Mapping name='A' input='ButtonA' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the user's controllers are the platform's
        result.Controllers.Select(c => c.Name).ShouldBe(["Pad"]);
    }

    [Fact]
    public void LoadPlatformMapping_UserController_ReplacesTheShippedOneByName_AndKeepsTheRest()
    {
        // given a shipped file with two controllers, and a User file redefining one of them and
        // adding a third
        StubXml(Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml"), """
            <Controllers default='6-Button'>
              <Controller name='3-Button'>
                <Mapping name='A' input='ButtonX' />
              </Controller>
              <Controller name='6-Button'>
                <Mapping name='A' input='ButtonX' />
                <Mapping name='X' input='ButtonLeftShoulder' />
              </Controller>
            </Controllers>
            """);
        StubXml(Path.Combine(UserDir, "Platforms", "Sega Genesis", "Controllers.xml"), """
            <Controllers>
              <Controller name='3-Button'>
                <Mapping name='A' input='ButtonA' />
              </Controller>
              <Controller name='Mouse'>
                <Mapping name='Left' input='ButtonA' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the user's 3-Button replaces the shipped one in place, the shipped 6-Button is kept
        // and stays the default, and the new controller is appended
        result.Controllers.Select(c => c.Name).ShouldBe(["3-Button", "6-Button", "Mouse"]);
        result.Controllers[0].Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonA")]);
        result.Resolve(null)!.Name.ShouldBe("6-Button");
    }

    [Fact]
    public void LoadPlatformMapping_UserRootDefault_WinsOverTheShippedOne()
    {
        // given a shipped default and a User file naming another controller default
        StubXml(Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml"), """
            <Controllers default='6-Button'>
              <Controller name='3-Button' />
              <Controller name='6-Button' />
            </Controllers>
            """);
        StubXml(Path.Combine(UserDir, "Platforms", "Sega Genesis", "Controllers.xml"), """
            <Controllers default='3-Button'>
              <Controller name='3-Button' />
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the user's choice is the only default
        result.Controllers.Select(c => (c.Name, c.IsDefault)).ShouldBe([("3-Button", true), ("6-Button", false)]);
    }

    [Fact]
    public void LoadPlatformMapping_UserCopyOfTheDefaultController_KeepsItTheDefault()
    {
        // given the shipped default, and a User copy of it changing a button
        StubXml(Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml"), """
            <Controllers default='6-Button'>
              <Controller name='3-Button' />
              <Controller name='6-Button' inheritFrom='3-Button'>
                <Mapping name='X' input='ButtonLeftShoulder' />
              </Controller>
            </Controllers>
            """);
        StubXml(Path.Combine(UserDir, "Platforms", "Sega Genesis", "Controllers.xml"), """
            <Controllers>
              <Controller name='6-Button' inheritFrom='3-Button'>
                <Mapping name='X' input='ButtonY' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the copy is still the default: which one is the default is the root's to say, and
        // the user's file doesn't say otherwise
        result.Resolve(null)!.Name.ShouldBe("6-Button");
        result.Resolve(null)!.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("X", "ButtonY")]);
    }

    [Fact]
    public void LoadPlatformMapping_UserFileStatingNoRootInheritFrom_KeepsTheShippedOne()
    {
        // given a shipped one-line pointer at a base platform, and a User file for the same
        // platform that adds a controller without restating the pointer
        StubXml(Path.Combine(DefaultsDir, "Platforms", "Nintendo Famicom", "Controllers.xml"),
            """<Controllers inheritFrom="Nintendo Entertainment System" />""");
        StubXml(Path.Combine(DefaultsDir, "Platforms", "Nintendo Entertainment System", "Controllers.xml"), """
            <Controllers default='Pad'>
              <Controller name='Pad'>
                <Mapping name='A' input='ButtonA' />
              </Controller>
            </Controllers>
            """);
        StubXml(Path.Combine(UserDir, "Platforms", "Nintendo Famicom", "Controllers.xml"), """
            <Controllers>
              <Controller name='Zapper'>
                <Mapping name='Trigger' input='ButtonA' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Nintendo Famicom")!;

        // then the base platform's controllers still arrive, with the user's added after them
        result.Controllers.Select(c => c.Name).ShouldBe(["Pad", "Zapper"]);
    }

    [Fact]
    public void LoadPlatformMapping_UserFileStatingARootInheritFrom_ReplacesTheShippedOne()
    {
        // given a shipped pointer at one base, and a User file pointing at another
        StubXml(Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml"),
            """<Controllers inheritFrom="_Shipped" />""");
        StubXml(Path.Combine(DefaultsDir, "Platforms", "_Shipped", "Controllers.xml"),
            """<Controllers><Controller name='ShippedPad' /></Controllers>""");
        StubXml(Path.Combine(DefaultsDir, "Platforms", "_Mine", "Controllers.xml"),
            """<Controllers><Controller name='MyPad' /></Controllers>""");
        StubXml(Path.Combine(UserDir, "Platforms", "Sega Genesis", "Controllers.xml"),
            """<Controllers inheritFrom="_Mine" />""");

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then the user's pointer is the one followed
        result.Controllers.Select(c => c.Name).ShouldBe(["MyPad"]);
    }

    [Fact]
    public void LoadPlatformMapping_BasePlatformsUserFile_ReachesThePlatformsInheritingFromIt()
    {
        // given a shipped pointer from Famicom to NES, and a User NES file changing the pad
        StubXml(Path.Combine(DefaultsDir, "Platforms", "Nintendo Famicom", "Controllers.xml"),
            """<Controllers inheritFrom="Nintendo Entertainment System" />""");
        StubXml(Path.Combine(DefaultsDir, "Platforms", "Nintendo Entertainment System", "Controllers.xml"), """
            <Controllers default='Pad'>
              <Controller name='Pad'>
                <Mapping name='A' input='ButtonA' />
              </Controller>
            </Controllers>
            """);
        StubXml(Path.Combine(UserDir, "Platforms", "Nintendo Entertainment System", "Controllers.xml"), """
            <Controllers default='Pad'>
              <Controller name='Pad'>
                <Mapping name='A' input='ButtonB' />
              </Controller>
            </Controllers>
            """);

        // when the Famicom is loaded
        var result = _underTest.LoadPlatformMapping("Nintendo Famicom")!;

        // then it inherits the NES pad as the user changed it
        result.Controllers.Single().Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonB")]);
    }

    [Fact]
    public void LoadPlatformMapping_TwoControllersWithOneName_KeepsTheFirstAndLogs()
    {
        // given a file declaring the same controller twice
        StubXml(Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml"), """
            <Controllers>
              <Controller name='Pad'>
                <Mapping name='A' input='ButtonA' />
              </Controller>
              <Controller name='Pad'>
                <Mapping name='A' input='ButtonB' />
              </Controller>
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis");

        // then the platform still loads, from the first, and the second is reported
        result.ShouldNotBeNull();
        result.Controllers.Single().Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonA")]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("second <Controller name=\"Pad\">")));
    }

    [Fact]
    public void LoadPlatformMapping_ControllerMissingName_SkippedWithError()
    {
        // given a Controllers.xml with one valid controller and one missing the name attribute
        string path = Path.Combine(DefaultsDir, "Platforms", "Sega Genesis", "Controllers.xml");
        StubXml(path, """
            <Controllers>
              <Controller name='Pad' />
              <Controller />
            </Controllers>
            """);

        // when the loader runs
        var result = _underTest.LoadPlatformMapping("Sega Genesis")!;

        // then only the named controller survives and an error is logged for the nameless one
        result.Controllers.Select(c => c.Name).ShouldBe(["Pad"]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("missing 'name'")));
    }
}
