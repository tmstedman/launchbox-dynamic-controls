using DynamicControls.Config;
using DynamicControls.Labels;
using NSubstitute;

namespace DynamicControls.Core.Tests.Labels;

/// <summary>
/// Unit tests for <see cref="InputLabelsLoader"/>. The loader parses a per-platform
/// <c>Platforms/{platform}/Labels.xml</c> file that combines all game entries and a <c>&lt;Defaults&gt;</c>
/// block. Both the <c>Defaults\</c> and <c>User\</c> tiers are loaded and merged: User game
/// entries replace Defaults entries whole (by id, then by name), <c>&lt;ControlSet&gt;</c>s
/// included; User <c>&lt;Defaults&gt;</c> entries override Defaults entries per button. Filesystem
/// is a substitute; each test stubs only the exact paths it exercises.
/// </summary>
public class InputLabelsLoaderTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IFileSystem _fs = TestFs.Create();
    private const string RootDir = @"C:\plugin";
    private static readonly string DefaultsPlatforms = Path.Combine(RootDir, "Defaults", "Platforms");
    private static readonly string UserPlatforms = Path.Combine(RootDir, "User", "Platforms");
    private readonly InputLabelsLoader _underTest;

    public InputLabelsLoaderTests()
    {
        _underTest = new InputLabelsLoader(_logger, new LayeredFileSystem(RootDir, _fs));
    }

    private static GameInfo Game(string platform, string romName, int? launchBoxId = null) => new(
        Platform: platform,
        RomName: romName,
        CloneOf: null,
        LaunchBoxId: launchBoxId,
        EmulatorPath: null,
        RomDirectory: null,
        RetroArchCore: null);

    private void StubXml(string path, string xml)
    {
        _fs.FileExists(path).Returns(true);
        _fs.OpenRead(path).Returns(_ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));
    }

    /// <summary>A set as one comparable line, "Name: button=label, …", in file order.</summary>
    private static string Describe(ControlSetConfig set) =>
        $"{set.Name}: {string.Join(", ", set.Labels.Select(e => $"{e.Name}={e.Label}"))}";

    // --- IsEnabled ---

    [Fact]
    public void IsEnabled_AlwaysReturnsTrue()
    {
        _underTest.IsEnabled(new GlobalConfig()).ShouldBeTrue();
    }

    // --- File presence ---

    [Fact]
    public void Load_NoPlatformFile_ReturnsNull()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(false);

        InputLabelsConfig? result = _underTest.Load(Game("Sega Genesis", "OutRun"));

        result.ShouldBeNull();
        _fs.DidNotReceive().OpenRead(Arg.Any<string>());
    }

    [Fact]
    public void LoadDefaultLabels_NoPlatformFile_ReturnsNoLayers()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(false);

        IReadOnlyList<InputLabelsConfig> result = _underTest.LoadDefaultLabels("Sega Genesis");

        result.ShouldBeEmpty();
    }

    // --- Path construction ---

    [Fact]
    public void Load_ReadsDefaultsPlatformFile()
    {
        string expected = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(expected, "<Labels />");

        _underTest.Load(Game("Sega Genesis", "OutRun"));

        _fs.Received().OpenRead(expected);
    }

    [Fact]
    public void Load_PlatformWithInvalidChars_IsSanitized()
    {
        string safePlatform = "Sega/Genesis".SafeFileName();
        string expected = Path.Combine(DefaultsPlatforms, safePlatform, "Labels.xml");
        StubXml(expected, "<Labels />");

        _underTest.Load(Game("Sega/Genesis", "OutRun"));

        _fs.Received().OpenRead(expected);
    }

    // --- Name-based lookup ---

    [Fact]
    public void Load_GameEntryMatchedByName_ReturnsItsLabels()
    {
        string path = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game romName="OutRun">
                <Input name="A">Brake</Input>
                <Input name="B">Accelerate</Input>
              </Game>
            </Labels>
            """);

        InputLabelsConfig? result = _underTest.Load(Game("Sega Genesis", "OutRun"));

        result.ShouldNotBeNull();
        result.Labels.Select(e => (e.Name, e.Label))
            .ShouldBe([("A", "Brake"), ("B", "Accelerate")]);
    }

    [Fact]
    public void Load_NoMatchingGameEntry_ReturnsNull()
    {
        string path = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game romName="Sonic">
                <Input name="A">Jump</Input>
              </Game>
            </Labels>
            """);

        InputLabelsConfig? result = _underTest.Load(Game("Sega Genesis", "OutRun"));

        result.ShouldBeNull();
    }

    [Fact]
    public void Load_GameEntryMatchedByName_CaseInsensitive()
    {
        string path = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game romName="OutRun">
                <Input name="A">Brake</Input>
              </Game>
            </Labels>
            """);

        InputLabelsConfig? result = _underTest.Load(Game("Sega Genesis", "outrun"));

        result.ShouldNotBeNull();
        result.Labels.Select(e => e.Name).ShouldBe(["A"]);
    }

    // --- Fuzzy name lookup ---

    [Fact]
    public void Load_GameEntryMatchedByFuzzyName_WhenExactNameDoesNotMatch()
    {
        string path = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game romName="OutRun">
                <Input name="A">Brake</Input>
              </Game>
            </Labels>
            """);

        // ROM name lacks the region tag that the entry has — fuzzy match strips both
        InputLabelsConfig? result = _underTest.Load(Game("Sega Genesis", "OutRun (USA, Europe)"));

        result.ShouldNotBeNull();
        result.Labels.Select(e => e.Name).ShouldBe(["A"]);
    }

    [Fact]
    public void Load_ExactNameMatchTakesPriorityOverFuzzyMatch()
    {
        string path = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game romName="OutRun">
                <Input name="A">A1</Input>
              </Game>
              <Game romName="OutRun (USA, Europe)">
                <Input name="A">A2</Input>
              </Game>
            </Labels>
            """);

        // "OutRun" matches exactly, so the fuzzy-only entry should not win
        InputLabelsConfig? result1 = _underTest.Load(Game("Sega Genesis", "OutRun"));
        result1.ShouldNotBeNull();
        result1.Labels[0].Label.ShouldBe("A1");

        // "OutRun" matches exactly, so the fuzzy-only entry should not win
        InputLabelsConfig? result2 = _underTest.Load(Game("Sega Genesis", "OutRun (USA, Europe)"));
        result2.ShouldNotBeNull();
        result2.Labels[0].Label.ShouldBe("A2");
    }

    [Fact]
    public void Load_GameEntryMatchedByFuzzyName_CaseInsensitive()
    {
        string path = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game romName="OutRun">
                <Input name="A">Brake</Input>
              </Game>
            </Labels>
            """);

        // ROM name has different casing and lacks the region tag — fuzzy match handles both
        InputLabelsConfig? result = _underTest.Load(Game("Sega Genesis", "OUTRUN (USA, Europe)"));

        result.ShouldNotBeNull();
        result.Labels.Select(e => e.Name).ShouldBe(["A"]);
    }

    // --- ID-based lookup ---

    [Fact]
    public void Load_GameEntryMatchedById_ReturnsItsLabels()
    {
        string path = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game launchBoxId="42" romName="OutRun (USA, Europe)">
                <Input name="A">Brake</Input>
              </Game>
            </Labels>
            """);

        // matches by id even though RomName differs from the entry's name attribute
        InputLabelsConfig? result = _underTest.Load(Game("Sega Genesis", "OutRun (EUR)", launchBoxId: 42));

        result.ShouldNotBeNull();
        result.Labels.Select(e => e.Name).ShouldBe(["A"]);
    }

    [Fact]
    public void Load_IdMatchTakesPriorityOverNameMatch()
    {
        string path = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game launchBoxId="42" romName="OutRun">
                <Input name="A">By-Id</Input>
              </Game>
              <Game romName="OutRun">
                <Input name="A">By-Name</Input>
              </Game>
            </Labels>
            """);

        InputLabelsConfig? result = _underTest.Load(Game("Sega Genesis", "OutRun", launchBoxId: 42));

        result!.Labels[0].Label.ShouldBe("By-Id");
    }

    // --- Defaults block ---

    [Fact]
    public void LoadDefaultLabels_ReturnsDefaultsBlock()
    {
        string path = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Defaults>
                <Input name="Start">Pause</Input>
              </Defaults>
            </Labels>
            """);

        IReadOnlyList<InputLabelsConfig> result = _underTest.LoadDefaultLabels("Sega Genesis");

        result.Single().Labels.Select(e => (e.Name, e.Label)).ShouldBe([("Start", "Pause")]);
    }

    [Fact]
    public void LoadDefaultLabels_NoDefaultsBlock_ReturnsNoLayers()
    {
        string path = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game romName="OutRun"><Input name="A">Brake</Input></Game>
            </Labels>
            """);

        IReadOnlyList<InputLabelsConfig> result = _underTest.LoadDefaultLabels("Sega Genesis");

        result.ShouldBeEmpty();
    }

    // --- User / Defaults merge ---

    [Fact]
    public void Load_UserFileGameEntryOverridesDefaultsEntry_ByName()
    {
        string defaultsPath = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        string userPath = Path.Combine(UserPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(defaultsPath, """
            <Labels>
              <Game romName="OutRun"><Input name="A">Default Brake</Input></Game>
            </Labels>
            """);
        StubXml(userPath, """
            <Labels>
              <Game romName="OutRun"><Input name="A">User Brake</Input></Game>
            </Labels>
            """);

        InputLabelsConfig? result = _underTest.Load(Game("Sega Genesis", "OutRun"));

        result!.Labels[0].Label.ShouldBe("User Brake");
    }

    [Fact]
    public void Load_UserFileGameEntryOverridesDefaultsEntry_ById()
    {
        string defaultsPath = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        string userPath = Path.Combine(UserPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(defaultsPath, """
            <Labels>
              <Game launchBoxId="42" romName="OutRun (USA, Europe)"><Input name="A">Default</Input></Game>
            </Labels>
            """);
        StubXml(userPath, """
            <Labels>
              <Game launchBoxId="42" romName="OutRun (USA, Europe)"><Input name="A">User</Input></Game>
            </Labels>
            """);

        InputLabelsConfig? result = _underTest.Load(Game("Sega Genesis", "OutRun (USA, Europe)", launchBoxId: 42));

        result!.Labels[0].Label.ShouldBe("User");
    }

    [Fact]
    public void LoadDefaultLabels_UserDefaultsComeFirst_EachTierWhole()
    {
        string defaultsPath = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        string userPath = Path.Combine(UserPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(defaultsPath, """
            <Labels>
              <Defaults><Input name="Start">Pause</Input><Input name="A">Default A</Input></Defaults>
            </Labels>
            """);
        StubXml(userPath, """
            <Labels>
              <Defaults><Input name="Start">Resume</Input></Defaults>
            </Labels>
            """);

        IReadOnlyList<InputLabelsConfig> result = _underTest.LoadDefaultLabels("Sega Genesis");

        // each tier is its own layer, User first: which entries name the same buttons depends on
        // the mapping, so the service merges them
        result.Select(layer => layer.Labels.Select(e => (e.Name, e.Label)).ToList()).ShouldBe(
        [
            [("Start", "Resume")],
            [("Start", "Pause"), ("A", "Default A")],
        ]);
    }

    // --- User-only file (no Defaults counterpart) ---

    [Fact]
    public void Load_UserFileOnly_NoDefaultsFile_MatchedByName()
    {
        // Defaults file absent; only the User file exists. Covers the null branch of
        // `defaultsExists ? ParseFile(...) : null`, `defaults?.Games ?? []`, and
        // `defaults?.Defaults` in MergeDefaults.
        string userPath = Path.Combine(UserPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(userPath, """
            <Labels>
              <Game romName="OutRun"><Input name="A">User Only</Input></Game>
            </Labels>
            """);

        InputLabelsConfig? result = _underTest.Load(Game("Sega Genesis", "OutRun"));

        result.ShouldNotBeNull();
        result.Labels.Select(e => (e.Name, e.Label)).ShouldBe([("A", "User Only")]);
    }

    // --- Game element with no name attribute ---

    [Fact]
    public void Load_GameElementWithNoNameAttribute_MatchedById_NameNotIndexed()
    {
        // A <Game launchBoxId="42"> with no name attribute is still found by launchBoxId. Covers the null
        // branch of `node.Attributes["romName"]?.Value` and the `e.Name != null` guard in Merge.
        string path = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game launchBoxId="42">
                <Input name="A">Brake</Input>
              </Game>
            </Labels>
            """);

        InputLabelsConfig? byId = _underTest.Load(Game("Sega Genesis", "OutRun", launchBoxId: 42));
        InputLabelsConfig? byName = _underTest.Load(Game("Sega Genesis", "OutRun"));

        byId.ShouldNotBeNull();
        byId.Labels.Select(e => e.Name).ShouldBe(["A"]);
        byName.ShouldBeNull();
    }

    // --- Parse errors ---

    [Fact]
    public void Load_EmptyLabelText_IsSkippedAndLogged()
    {
        string path = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game romName="OutRun">
                <Input name="A">Brake</Input>
                <Input name="B"></Input>
              </Game>
            </Labels>
            """);

        InputLabelsConfig result = _underTest.Load(Game("Sega Genesis", "OutRun"))!;

        result.Labels.Select(e => e.Name).ShouldBe(["A"]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("name=\"B\"") && s.Contains("no text value")));
    }

    [Theory]
    [InlineData("<Input>Brake</Input>")]
    [InlineData("<Input name=\" \">Brake</Input>")]
    public void Load_InputWithoutAName_IsSkippedAndLogged(string input)
    {
        string path = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(path, $"""
            <Labels>
              <Game romName="OutRun">
                <Input name="A">Accelerate</Input>
                {input}
              </Game>
            </Labels>
            """);

        InputLabelsConfig result = _underTest.Load(Game("Sega Genesis", "OutRun"))!;

        result.Labels.Select(e => e.Name).ShouldBe(["A"]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("missing 'name' attribute")));
    }

    [Fact]
    public void Load_CombinationName_IsCarriedThroughVerbatim()
    {
        string path = Path.Combine(DefaultsPlatforms, "Arcade", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game romName="3countb">
                <Input name="BUTTON1">Punch</Input>
                <Input name="BUTTON1 BUTTON2">Power Move</Input>
              </Game>
            </Labels>
            """);

        InputLabelsConfig result = _underTest.Load(Game("Arcade", "3countb"))!;

        // the loader does not interpret the name — resolving it against the mapping is the
        // service's job, so the entry arrives unchanged
        result.Labels.Select(e => e.Name).ShouldBe(["BUTTON1", "BUTTON1 BUTTON2"]);
    }

    [Fact]
    public void Load_UnknownElement_IsSkippedAndLogged()
    {
        string path = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game romName="OutRun">
                <A>Brake</A>
                <Input name="B">Accelerate</Input>
              </Game>
            </Labels>
            """);

        InputLabelsConfig result = _underTest.Load(Game("Sega Genesis", "OutRun"))!;

        // the retired element-name-as-data form is rejected rather than silently half-read
        result.Labels.Select(e => e.Name).ShouldBe(["B"]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("<A>") && s.Contains("expected")));
    }

    [Fact]
    public void Load_LabelTextIsTrimmed()
    {
        string path = Path.Combine(DefaultsPlatforms, "Sega Genesis", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game romName="OutRun">
                <Input name="A">
                    Brake
                </Input>
              </Game>
            </Labels>
            """);

        InputLabelsConfig result = _underTest.Load(Game("Sega Genesis", "OutRun"))!;

        result.Labels.Single().Label.ShouldBe("Brake");
    }

    [Theory]
    #pragma warning disable format
    [InlineData("Punch&#10;Block")]             // the documented form
    [InlineData("Punch &#10; Block")]           // spaced either side of the break
    [InlineData("Punch\r\nBlock")]              // typed into the file on Windows
    [InlineData("Punch\r\n            Block")]  // typed, and indented to match the file
    #pragma warning restore format
    public void Load_LineBreakInsideLabelText_IsKeptInOneForm(string written)
    {
        string path = Path.Combine(DefaultsPlatforms, "Arcade", "Labels.xml");
        StubXml(path, $"""
            <Labels>
              <Game romName="buriki">
                <Input name="P1_JOYSTICK">{written}</Input>
              </Game>
            </Labels>
            """);

        InputLabelsConfig result = _underTest.Load(Game("Arcade", "buriki"))!;

        // the break survives to be drawn as a second line, and every way of writing it reads the
        // same: a plain \n, with each line trimmed, so equal labels compare equal
        result.Labels.Single().Label.ShouldBe("Punch\nBlock");
    }

    // --- Control sets ---

    [Fact]
    public void Load_ControlSets_AreReadInOrder_BesideTheDefault()
    {
        string path = Path.Combine(DefaultsPlatforms, "Super Nintendo Entertainment System", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game romName="Pilotwings">
                <Input name="Dpad-Left">Turn Left</Input>
                <ControlSet name="Light Plane">
                  <Input name="Dpad-Left">Turn Left</Input>
                  <Input name="B">Throttle</Input>
                </ControlSet>
                <ControlSet name="Rocket Belt">
                  <Input name="A">Thrust</Input>
                </ControlSet>
              </Game>
            </Labels>
            """);

        InputLabelsConfig result = _underTest.Load(Game("Super Nintendo Entertainment System", "Pilotwings"))!;

        // the direct inputs are the default, and each set is held whole beside it, not merged in
        result.Labels.Select(e => (e.Name, e.Label)).ShouldBe([("Dpad-Left", "Turn Left")]);
        result.ControlSets.Select(Describe).ShouldBe(
        [
            "Light Plane: Dpad-Left=Turn Left, B=Throttle",
            "Rocket Belt: A=Thrust",
        ]);
    }

    [Fact]
    public void Load_ControlSetsWithoutADefault_LeaveTheDefaultEmpty()
    {
        string path = Path.Combine(DefaultsPlatforms, "Super Nintendo Entertainment System", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game romName="Pilotwings">
                <ControlSet name="Rocket Belt"><Input name="A">Thrust</Input></ControlSet>
              </Game>
            </Labels>
            """);

        InputLabelsConfig result = _underTest.Load(Game("Super Nintendo Entertainment System", "Pilotwings"))!;

        result.Labels.ShouldBeEmpty();
        result.ControlSets.Select(s => s.Name).ShouldBe(["Rocket Belt"]);
    }

    [Fact]
    public void Load_ControlSetWithoutAName_IsSkippedAndLogged()
    {
        string path = Path.Combine(DefaultsPlatforms, "Super Nintendo Entertainment System", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game romName="Pilotwings">
                <ControlSet><Input name="A">Thrust</Input></ControlSet>
                <ControlSet name=" "><Input name="A">Thrust</Input></ControlSet>
              </Game>
            </Labels>
            """);

        InputLabelsConfig result = _underTest.Load(Game("Super Nintendo Entertainment System", "Pilotwings"))!;

        // a set is shown under its name, so one without a name has nothing to be shown as
        result.ControlSets.ShouldBeEmpty();
        _logger.Received(2).Error(Arg.Is<string>(s => s.Contains("<ControlSet>") && s.Contains("'name'")));
    }

    [Fact]
    public void Load_ControlSetNameRepeated_KeepsTheFirstAndLogs()
    {
        string path = Path.Combine(DefaultsPlatforms, "Super Nintendo Entertainment System", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game romName="Pilotwings">
                <ControlSet name="Rocket Belt"><Input name="A">Thrust</Input></ControlSet>
                <ControlSet name="rocket belt"><Input name="A">Lift</Input></ControlSet>
              </Game>
            </Labels>
            """);

        InputLabelsConfig result = _underTest.Load(Game("Super Nintendo Entertainment System", "Pilotwings"))!;

        // names match case-insensitively, since two that differ only in case read as one
        result.ControlSets.Single().Labels.Single().Label.ShouldBe("Thrust");
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("rocket belt") && s.Contains("already")));
    }

    [Fact]
    public void Load_ControlSetName_IsTrimmed_SoStraySpacesNeitherShowNorHideADuplicate()
    {
        string path = Path.Combine(DefaultsPlatforms, "Super Nintendo Entertainment System", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game romName="Pilotwings">
                <ControlSet name=" Rocket Belt "><Input name="A">Thrust</Input></ControlSet>
                <ControlSet name="Rocket Belt"><Input name="A">Lift</Input></ControlSet>
              </Game>
            </Labels>
            """);

        InputLabelsConfig result = _underTest.Load(Game("Super Nintendo Entertainment System", "Pilotwings"))!;

        // the name is shown to the player, so it is kept as written less the stray spaces, and
        // the second set is the first's duplicate
        result.ControlSets.Select(s => s.Name).ShouldBe(["Rocket Belt"]);
        result.ControlSets.Single().Labels.Single().Label.ShouldBe("Thrust");
    }

    [Fact]
    public void Load_ControlSetWithNoInputs_IsSkippedAndLogged()
    {
        string path = Path.Combine(DefaultsPlatforms, "Super Nintendo Entertainment System", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game romName="Pilotwings">
                <Input name="Dpad-Left">Turn Left</Input>
                <ControlSet name="Rocket Belt" />
              </Game>
            </Labels>
            """);

        InputLabelsConfig result = _underTest.Load(Game("Super Nintendo Entertainment System", "Pilotwings"))!;

        result.ControlSets.ShouldBeEmpty();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Rocket Belt") && s.Contains("no <Input>")));
    }

    [Fact]
    public void Load_ControlSetInDefaults_IsSkippedAndLogged()
    {
        string path = Path.Combine(DefaultsPlatforms, "Super Nintendo Entertainment System", "Labels.xml");
        StubXml(path, """<Labels><Defaults><ControlSet name="X"><Input name="A">Jump</Input></ControlSet></Defaults></Labels>""");

        _underTest.Load(Game("Super Nintendo Entertainment System", "Pilotwings"));
        IReadOnlyList<InputLabelsConfig> defaults = _underTest.LoadDefaultLabels("Super Nintendo Entertainment System");

        // in <Defaults> a set would belong to every game
        defaults.ShouldBeEmpty();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("<ControlSet>") && s.Contains("directly inside <Game>")));
    }

    [Fact]
    public void Load_ControlSetOutsideAnyGame_IsSkippedAndLogged()
    {
        string path = Path.Combine(DefaultsPlatforms, "Super Nintendo Entertainment System", "Labels.xml");
        StubXml(path, """
            <Labels>
              <ControlSet name="Light Plane"><Input name="A">Throttle</Input></ControlSet>
              <Game romName="Pilotwings"><Input name="B">Turn Left</Input></Game>
            </Labels>
            """);

        InputLabelsConfig result = _underTest.Load(Game("Super Nintendo Entertainment System", "Pilotwings"))!;

        // directly under <Labels> a set belongs to no game: dropped, and the game beside it stays
        result.Labels.Select(e => e.Name).ShouldBe(["B"]);
        result.ControlSets.ShouldBeEmpty();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("<ControlSet>") && s.Contains("directly inside <Game>")));
    }

    [Fact]
    public void Load_UnknownRootElement_IsSkippedAndLogged()
    {
        string path = Path.Combine(DefaultsPlatforms, "Super Nintendo Entertainment System", "Labels.xml");
        StubXml(path, """<Labels><game romName="Pilotwings"><Input name="B">Turn Left</Input></game></Labels>""");

        InputLabelsConfig? result = _underTest.Load(Game("Super Nintendo Entertainment System", "Pilotwings"));

        // a misspelt <Game> says why the game has no labels rather than leaving it silently unlabelled
        result.ShouldBeNull();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("<game>") && s.Contains("expected <Defaults> or <Game>")));
    }

    [Fact]
    public void Load_ControlSetInsideAControlSet_IsSkippedAndLogged_TheOuterSetKept()
    {
        string path = Path.Combine(DefaultsPlatforms, "Super Nintendo Entertainment System", "Labels.xml");
        StubXml(path, """
            <Labels>
              <Game romName="Pilotwings">
                <ControlSet name="X">
                  <Input name="B">Run</Input>
                  <ControlSet name="Y"><Input name="A">Jump</Input></ControlSet>
                </ControlSet>
              </Game>
            </Labels>
            """);

        InputLabelsConfig result = _underTest.Load(Game("Super Nintendo Entertainment System", "Pilotwings"))!;

        // inside another set it would be a set of a set: dropped, and the set around it stays
        result.ControlSets.Select(Describe).ShouldBe(["X: B=Run"]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("<ControlSet>") && s.Contains("directly inside <Game>")));
    }

    [Fact]
    public void Load_UserEntryWithSets_ReplacesTheShippedEntryWhole()
    {
        string defaultsPath = Path.Combine(DefaultsPlatforms, "Super Nintendo Entertainment System", "Labels.xml");
        string userPath = Path.Combine(UserPlatforms, "Super Nintendo Entertainment System", "Labels.xml");
        StubXml(defaultsPath, """
            <Labels>
              <Game romName="Pilotwings">
                <Input name="Dpad-Left">Turn Left</Input>
                <ControlSet name="Light Plane"><Input name="B">Throttle</Input></ControlSet>
                <ControlSet name="Rocket Belt"><Input name="A">Thrust</Input><Input name="B">Hover</Input></ControlSet>
              </Game>
            </Labels>
            """);
        StubXml(userPath, """
            <Labels>
              <Game romName="Pilotwings">
                <ControlSet name="Rocket Belt"><Input name="A">Lift</Input></ControlSet>
              </Game>
            </Labels>
            """);

        InputLabelsConfig result = _underTest.Load(Game("Super Nintendo Entertainment System", "Pilotwings"))!;

        // the User entry is the whole entry, as one without sets always was: the shipped default
        // and every shipped set it leaves out are gone, so writing an entry is how a set is removed
        result.Labels.ShouldBeEmpty();
        result.ControlSets.Select(Describe).ShouldBe(["Rocket Belt: A=Lift"]);
    }

    [Fact]
    public void Load_UserEntryWithoutSets_DropsTheShippedSets()
    {
        string defaultsPath = Path.Combine(DefaultsPlatforms, "Super Nintendo Entertainment System", "Labels.xml");
        string userPath = Path.Combine(UserPlatforms, "Super Nintendo Entertainment System", "Labels.xml");
        StubXml(defaultsPath, """
            <Labels>
              <Game launchBoxId="2418" romName="Pilotwings">
                <Input name="Dpad-Left">Turn Left</Input>
                <Input name="Dpad-Right">Turn Right</Input>
                <ControlSet name="Rocket Belt"><Input name="A">Thrust</Input></ControlSet>
              </Game>
            </Labels>
            """);
        StubXml(userPath, """
            <Labels>
              <Game launchBoxId="2418"><Input name="Dpad-Left">Bank Left</Input></Game>
            </Labels>
            """);

        InputLabelsConfig result = _underTest.Load(Game("Super Nintendo Entertainment System", "Pilotwings", launchBoxId: 2418))!;

        result.Labels.Select(e => (e.Name, e.Label)).ShouldBe([("Dpad-Left", "Bank Left")]);
        result.ControlSets.ShouldBeEmpty();
    }

    [Fact]
    public void Load_EmptyUserEntry_LeavesNothingOfTheShippedEntry()
    {
        string defaultsPath = Path.Combine(DefaultsPlatforms, "Super Nintendo Entertainment System", "Labels.xml");
        string userPath = Path.Combine(UserPlatforms, "Super Nintendo Entertainment System", "Labels.xml");
        StubXml(defaultsPath, """
            <Labels>
              <Game romName="Pilotwings">
                <Input name="Dpad-Left">Turn Left</Input>
                <ControlSet name="Rocket Belt"><Input name="A">Thrust</Input></ControlSet>
              </Game>
            </Labels>
            """);
        StubXml(userPath, """<Labels><Game romName="Pilotwings" /></Labels>""");

        InputLabelsConfig result = _underTest.Load(Game("Super Nintendo Entertainment System", "Pilotwings"))!;

        // nothing left, so the game falls through to the next source as it would with no entry
        result.Labels.ShouldBeEmpty();
        result.ControlSets.ShouldBeEmpty();
    }
}
