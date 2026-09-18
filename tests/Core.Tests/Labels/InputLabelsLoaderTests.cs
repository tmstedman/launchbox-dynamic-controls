using DynamicControls.Config;
using DynamicControls.Labels;
using NSubstitute;

namespace DynamicControls.Core.Tests.Labels;

/// <summary>
/// Unit tests for <see cref="InputLabelsLoader"/>. The loader parses a per-platform
/// <Input name="c">Labels/{platform}.xml</Input> file that combines all game entries and a <Input name="c">&lt;Defaults&gt;</Input>
/// block. Both the <Input name="c">Defaults\</Input> and <Input name="c">User\</Input> tiers are loaded and merged: User game
/// entries override Defaults entries (by id, then by name); User <Input name="c">&lt;Defaults&gt;</Input> entries
/// override Defaults entries per button. Filesystem is a substitute; each test stubs only the
/// exact paths it exercises.
/// </summary>
public class InputLabelsLoaderTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IFileSystem _fs = TestFs.Create();
    private const string RootDir = @"C:\plugin";
    private static readonly string DefaultsLabels = Path.Combine(RootDir, "Defaults", "Labels");
    private static readonly string UserLabels = Path.Combine(RootDir, "User", "Labels");
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
    public void LoadDefaultLabels_NoPlatformFile_ReturnsNull()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(false);

        InputLabelsConfig? result = _underTest.LoadDefaultLabels("Sega Genesis");

        result.ShouldBeNull();
    }

    // --- Path construction ---

    [Fact]
    public void Load_ReadsDefaultsPlatformFile()
    {
        string expected = Path.Combine(DefaultsLabels, "Sega Genesis.xml");
        StubXml(expected, "<Labels />");

        _underTest.Load(Game("Sega Genesis", "OutRun"));

        _fs.Received().OpenRead(expected);
    }

    [Fact]
    public void Load_PlatformWithInvalidChars_IsSanitized()
    {
        string safePlatform = "Sega/Genesis".SafeFileName();
        string expected = Path.Combine(DefaultsLabels, safePlatform + ".xml");
        StubXml(expected, "<Labels />");

        _underTest.Load(Game("Sega/Genesis", "OutRun"));

        _fs.Received().OpenRead(expected);
    }

    // --- Name-based lookup ---

    [Fact]
    public void Load_GameEntryMatchedByName_ReturnsItsLabels()
    {
        string path = Path.Combine(DefaultsLabels, "Sega Genesis.xml");
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
        string path = Path.Combine(DefaultsLabels, "Sega Genesis.xml");
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
        string path = Path.Combine(DefaultsLabels, "Sega Genesis.xml");
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
        string path = Path.Combine(DefaultsLabels, "Sega Genesis.xml");
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
        string path = Path.Combine(DefaultsLabels, "Sega Genesis.xml");
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
        string path = Path.Combine(DefaultsLabels, "Sega Genesis.xml");
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
        string path = Path.Combine(DefaultsLabels, "Sega Genesis.xml");
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
        string path = Path.Combine(DefaultsLabels, "Sega Genesis.xml");
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
        string path = Path.Combine(DefaultsLabels, "Sega Genesis.xml");
        StubXml(path, """
            <Labels>
              <Defaults>
                <Input name="Start">Pause</Input>
              </Defaults>
            </Labels>
            """);

        InputLabelsConfig? result = _underTest.LoadDefaultLabels("Sega Genesis");

        result.ShouldNotBeNull();
        result.Labels.Select(e => (e.Name, e.Label)).ShouldBe([("Start", "Pause")]);
    }

    [Fact]
    public void LoadDefaultLabels_NoDefaultsBlock_ReturnsNull()
    {
        string path = Path.Combine(DefaultsLabels, "Sega Genesis.xml");
        StubXml(path, """
            <Labels>
              <Game romName="OutRun"><Input name="A">Brake</Input></Game>
            </Labels>
            """);

        InputLabelsConfig? result = _underTest.LoadDefaultLabels("Sega Genesis");

        result.ShouldBeNull();
    }

    // --- User / Defaults merge ---

    [Fact]
    public void Load_UserFileGameEntryOverridesDefaultsEntry_ByName()
    {
        string defaultsPath = Path.Combine(DefaultsLabels, "Sega Genesis.xml");
        string userPath = Path.Combine(UserLabels, "Sega Genesis.xml");
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
        string defaultsPath = Path.Combine(DefaultsLabels, "Sega Genesis.xml");
        string userPath = Path.Combine(UserLabels, "Sega Genesis.xml");
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
    public void LoadDefaultLabels_UserDefaultsOverrideByButton()
    {
        string defaultsPath = Path.Combine(DefaultsLabels, "Sega Genesis.xml");
        string userPath = Path.Combine(UserLabels, "Sega Genesis.xml");
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

        InputLabelsConfig? result = _underTest.LoadDefaultLabels("Sega Genesis");

        // User Start wins; Defaults A is kept (not present in User)
        result!.Labels.Select(e => (e.Name, e.Label))
            .ShouldBe([("Start", "Resume"), ("A", "Default A")]);
    }

    // --- User-only file (no Defaults counterpart) ---

    [Fact]
    public void Load_UserFileOnly_NoDefaultsFile_MatchedByName()
    {
        // Defaults file absent; only the User file exists. Covers the null branch of
        // `defaultsExists ? ParseFile(...) : null`, `defaults?.Games ?? []`, and
        // `defaults?.Defaults` in MergeDefaults.
        string userPath = Path.Combine(UserLabels, "Sega Genesis.xml");
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
        string path = Path.Combine(DefaultsLabels, "Sega Genesis.xml");
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
        string path = Path.Combine(DefaultsLabels, "Sega Genesis.xml");
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

    [Fact]
    public void Load_CombinationName_IsIgnoredAndLogged_OtherEntriesSurvive()
    {
        string path = Path.Combine(DefaultsLabels, "Arcade.xml");
        StubXml(path, """
            <Labels>
              <Game romName="3countb">
                <Input name="BUTTON1">Punch</Input>
                <Input name="BUTTON1 BUTTON2">Power Move</Input>
              </Game>
            </Labels>
            """);

        InputLabelsConfig result = _underTest.Load(Game("Arcade", "3countb"))!;

        // the combination parses without error but cannot be resolved yet, so it is dropped
        // rather than attributed to either button named in it
        result.Labels.Select(e => e.Name).ShouldBe(["BUTTON1"]);
        _logger.Received().Info(Arg.Is<string>(s => s.Contains("BUTTON1 BUTTON2") && s.Contains("not supported yet")));
    }

    [Fact]
    public void Load_UnknownElement_IsSkippedAndLogged()
    {
        string path = Path.Combine(DefaultsLabels, "Sega Genesis.xml");
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
        string path = Path.Combine(DefaultsLabels, "Sega Genesis.xml");
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
}
