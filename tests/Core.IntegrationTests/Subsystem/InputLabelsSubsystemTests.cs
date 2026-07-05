using DynamicControls.Composition;
using DynamicControls.Config;
using DynamicControls.InputMapping;
using DynamicControls.Labels;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;
using static DynamicControls.Core.TestHelpers.InputMapping.MappingFixtures;

namespace DynamicControls.Core.IntegrationTests.Subsystem;

/// <summary>
/// Verifies the input-labels subsystem with its real internal wiring intact:
/// <see cref="InputLabelsLoader"/> (XML file parsing for game entries and <c>&lt;Defaults&gt;</c>
/// blocks from the per-platform <c>Labels/{platform}.xml</c> file) and
/// <see cref="Plugins.ControlsXml.MameControlsXmlSource"/> +
/// <see cref="Plugins.ControlsXml.ControlsXmlLoader"/> (BYOAC controls.xml parsing, MAME-gated)
/// composing through <see cref="InputLabelsPlugins"/> (enable-filtering + chain order) into
/// <see cref="InputLabelsService"/> (game-loader chain, inheritable-default merge,
/// platform-button → generic-input translation, and the <c>IsGameSpecific</c> flag). Clone-of
/// fallback is pure service control flow with no loader-wiring dependency, so it stays in the
/// unit tier (<c>InputLabelsServiceTests</c>) rather than being re-tested here.
///
/// The only faked seam is <see cref="IFileSystem"/> — label XML lives inline in each test and is
/// parsed for real. The <see cref="ResolvedMapping"/> that drives translation is supplied
/// directly (it is the input-mapping subsystem's output, exercised in its own suite). This tier
/// covers the loader-interaction scenarios that unit tests (which substitute the loaders, so no
/// XML or MAME gating runs) and E2E tests (one on-disk fixture set per game) can't reach
/// economically.
/// </summary>
public class InputLabelsSubsystemTests
{
    private const string Platform = "Sega Genesis";
    private const string Arcade = "Arcade";
    private static readonly string RootDir = Path.DirectorySeparatorChar + "dc";
    private static readonly string MamePath = Path.Combine(RootDir, "Emulators", "mame", "mame64.exe");

    private readonly MockDynamicControlsFilesystem _dc = new(RootDir);

    /// <summary>Uses the production factory so the loader/plugins/service wiring under test
    /// matches production. <see cref="InputLabelsLoader"/> is always the first loader; the
    /// MAME controls.xml source is appended and enable-filtered on <see cref="GlobalConfig.EnableMame"/>.</summary>
    private InputLabelsService Build(bool enableMame = false) =>
        InputLabelsFactory.Create(
            _dc.Lfs,
            new NullLogger(),
            config: new GlobalConfig { EnableMame = enableMame });

    /// <summary>Builds a <see cref="ResolvedMapping"/> from a platform-button → generic-inputs
    /// table; only <c>ButtonToInput</c> is read by the labels subsystem.</summary>
    private static ResolvedMapping Mapping(params (string Button, string[] Inputs)[] entries) =>
        MappingOf(buttonToInput: entries.ToDictionary(
            e => e.Button,
            e => (IReadOnlyList<string>)e.Inputs));

    // ---- game labels from a file, translated through the mapping ----

    [Fact]
    public void Load_GameLabelsFile_ParsedAndTranslatedToGenericInputs()
    {
        _dc.WriteGameLabels(Platform, "OutRun", """
            <InputLabels>
              <A>Accelerate</A>
              <B>Brake</B>
            </InputLabels>
            """);
        ResolvedMapping mapping = Mapping(("A", ["ButtonA"]), ("B", ["ButtonB"]));

        ResolvedLabels labels = Build().Load(Game(platform: Platform, romName: "OutRun"), mapping);

        labels.LabelText.ShouldBeDictionaryOf(
            ("ButtonA", "Accelerate"),
            ("ButtonB", "Brake"));
        labels.IsGameSpecific.ShouldBeTrue();
    }

    [Fact]
    public void Load_LabelForUnmappedButton_IsDiscardedDuringTranslation()
    {
        // C has a label but no entry in the mapping — it can't be placed on any input, so it drops.
        _dc.WriteGameLabels(Platform, "OutRun", """
            <InputLabels>
              <A>Accelerate</A>
              <C>Nitro</C>
            </InputLabels>
            """);
        ResolvedMapping mapping = Mapping(("A", ["ButtonA"]));

        ResolvedLabels labels = Build().Load(Game(platform: Platform, romName: "OutRun"), mapping);

        labels.LabelText.ShouldBeDictionaryOf(("ButtonA", "Accelerate"));
    }

    [Fact]
    public void Load_ButtonDrivingMultipleInputs_FansLabelOutToAll()
    {
        // A2D mirror: one Dpad button drives both the Dpad and the left stick generic.
        _dc.WriteGameLabels(Platform, "OutRun", """
            <InputLabels>
              <Dpad-Up>Look Up</Dpad-Up>
            </InputLabels>
            """);
        ResolvedMapping mapping = Mapping(("Dpad-Up", ["ButtonDpadUp", "AxisLeftStickUp"]));

        ResolvedLabels labels = Build().Load(Game(platform: Platform, romName: "OutRun"), mapping);

        labels.LabelText.ShouldBeDictionaryOf(
            ("ButtonDpadUp", "Look Up"),
            ("AxisLeftStickUp", "Look Up"));
    }

    // ---- inheritable-default merge ----

    [Fact]
    public void Load_GameLabelsPresent_MergesAllDefaults()
    {
        _dc.WriteGameLabels(Platform, "OutRun", """
            <InputLabels>
              <A>Accelerate</A>
            </InputLabels>
            """);
        _dc.WriteDefaultLabels(Platform, """
            <InputLabels>
              <Start>Pause</Start>
              <B>Brake</B>
            </InputLabels>
            """);
        ResolvedMapping mapping = Mapping(
            ("A", ["ButtonA"]),
            ("Start", ["ButtonStart"]),
            ("B", ["ButtonB"]));

        ResolvedLabels labels = Build().Load(Game(platform: Platform, romName: "OutRun"), mapping);

        // game label kept, all defaults merged in for buttons the game doesn't define
        labels.LabelText.ShouldBeDictionaryOf(
            ("ButtonA", "Accelerate"),
            ("ButtonStart", "Pause"),
            ("ButtonB", "Brake"));
        labels.IsGameSpecific.ShouldBeTrue();
    }

    [Fact]
    public void Load_GameLabelOverridesInheritableDefaultForSameButton()
    {
        _dc.WriteGameLabels(Platform, "OutRun", """
            <InputLabels>
              <Start>Resume Race</Start>
            </InputLabels>
            """);
        _dc.WriteDefaultLabels(Platform, """
            <InputLabels>
              <Start inherit="true">Pause</Start>
            </InputLabels>
            """);
        ResolvedMapping mapping = Mapping(("Start", ["ButtonStart"]));

        ResolvedLabels labels = Build().Load(Game(platform: Platform, romName: "OutRun"), mapping);

        // game's own Start wins; the inheritable default does not overwrite it
        labels.LabelText.ShouldBeDictionaryOf(("ButtonStart", "Resume Race"));
    }

    // ---- default-labels fallback ----

    [Fact]
    public void Load_NoGameLabels_FallsBackToAllDefaults_NotGameSpecific()
    {
        _dc.WriteDefaultLabels(Platform, """
            <InputLabels>
              <Start inherit="true">Pause</Start>
              <A>Accelerate</A>
            </InputLabels>
            """);
        ResolvedMapping mapping = Mapping(("Start", ["ButtonStart"]), ("A", ["ButtonA"]));

        ResolvedLabels labels = Build().Load(Game(platform: Platform, romName: "OutRun"), mapping);

        // defaults-only path takes every default regardless of inherit, and isn't game-specific
        labels.LabelText.ShouldBeDictionaryOf(
            ("ButtonStart", "Pause"),
            ("ButtonA", "Accelerate"));
        labels.IsGameSpecific.ShouldBeFalse();
    }

    [Fact]
    public void Load_NoLabelsAnywhere_ReturnsEmptyNonGameSpecific()
    {
        ResolvedLabels labels = Build().Load(Game(platform: Platform, romName: "OutRun"), Mapping());

        labels.LabelText.ShouldBeEmpty();
        labels.IsGameSpecific.ShouldBeFalse();
    }

    [Fact]
    public void Load_EmptyGameLabelsFile_TreatedAsMissing_FallsBackToDefaults()
    {
        _dc.WriteGameLabels(Platform, "OutRun", "<InputLabels></InputLabels>");
        _dc.WriteDefaultLabels(Platform, """
            <InputLabels>
              <A>Accelerate</A>
            </InputLabels>
            """);
        ResolvedMapping mapping = Mapping(("A", ["ButtonA"]));

        ResolvedLabels labels = Build().Load(Game(platform: Platform, romName: "OutRun"), mapping);

        // an empty game file doesn't count as "the game has its own labels"
        labels.LabelText.ShouldBeDictionaryOf(("ButtonA", "Accelerate"));
        labels.IsGameSpecific.ShouldBeFalse();
    }

    // ---- MAME controls.xml source: gating, parsing, priority ----

    [Fact]
    public void Load_MameEnabledAndMameEmulator_UsesControlsXmlWhenNoLabelFile()
    {
        _dc.WriteControlsXml("""
            <dat>
              <game romname="dkong">
                <player number="1">
                  <labels>
                    <label name="P1_BUTTON1" value="Jump"/>
                  </labels>
                </player>
              </game>
            </dat>
            """);
        ResolvedMapping mapping = Mapping(("BUTTON1", ["ButtonA"]));
        GameInfo game = Game(platform: Arcade, romName: "dkong", emulatorPath: MamePath);

        ResolvedLabels labels = Build(enableMame: true).Load(game, mapping);

        // controls.xml parsed (P1_ prefix stripped) and translated through the mapping
        labels.LabelText.ShouldBeDictionaryOf(("ButtonA", "Jump"));
        labels.IsGameSpecific.ShouldBeTrue();
    }

    [Fact]
    public void Load_MameDisabled_ControlsXmlNotIgnored()
    {
        _dc.WriteControlsXml("""
            <dat>
              <game romname="dkong">
                <player number="1">
                  <labels>
                    <label name="P1_BUTTON1" value="Jump"/>
                  </labels>
                </player>
              </game>
            </dat>
            """);
        ResolvedMapping mapping = Mapping(("BUTTON1", ["ButtonA"]));
        GameInfo game = Game(platform: Arcade, romName: "dkong", emulatorPath: MamePath);

        // EnableMame=false filters the controls.xml source out of the chain entirely
        ResolvedLabels labels = Build(enableMame: false).Load(game, mapping);

        // controls.xml parsed (P1_ prefix stripped) and translated through the mapping
        labels.LabelText.ShouldBeDictionaryOf(("ButtonA", "Jump"));
        labels.IsGameSpecific.ShouldBeTrue();
    }

    [Fact]
    public void Load_MameEnabledButNonMameEmulator_ControlsXmlNotConsulted()
    {
        _dc.WriteControlsXml("""
            <dat>
              <game romname="dkong">
                <player number="1">
                  <labels>
                    <label name="P1_BUTTON1" value="Jump"/>
                  </labels>
                </player>
              </game>
            </dat>
            """);
        ResolvedMapping mapping = Mapping(("BUTTON1", ["ButtonA"]));
        // Source is in the chain (EnableMame=true) but gates itself off: emulator isn't MAME.
        GameInfo game = Game(platform: Arcade, romName: "dkong",
            emulatorPath: Path.Combine(RootDir, "Emulators", "retroarch", "retroarch.exe"));

        ResolvedLabels labels = Build(enableMame: true).Load(game, mapping);

        labels.LabelText.ShouldBeEmpty();
        labels.IsGameSpecific.ShouldBeFalse();
    }

    [Fact]
    public void Load_LabelFileAndControlsXmlBothPresent_FileLoaderWins()
    {
        // The file-based loader is first in the chain, so it takes priority over controls.xml.
        _dc.WriteGameLabels(Arcade, "dkong", """
            <InputLabels>
              <BUTTON1>Leap</BUTTON1>
            </InputLabels>
            """);
        _dc.WriteControlsXml("""
            <dat>
              <game romname="dkong">
                <player number="1">
                  <labels>
                    <label name="P1_BUTTON1" value="Jump"/>
                  </labels>
                </player>
              </game>
            </dat>
            """);
        ResolvedMapping mapping = Mapping(("BUTTON1", ["ButtonA"]));
        GameInfo game = Game(platform: Arcade, romName: "dkong", emulatorPath: MamePath);

        ResolvedLabels labels = Build(enableMame: true).Load(game, mapping);

        labels.LabelText.ShouldBeDictionaryOf(("ButtonA", "Leap"));
    }

    [Fact]
    public void Load_UserLabelsFilePresent_WinsOverDefaultsFile()
    {
        // Defaults labels file has A=Accelerate; User labels file replaces it with A=Boost.
        _dc.WriteGameLabels(Platform, "OutRun", """
            <InputLabels>
              <A>Accelerate</A>
            </InputLabels>
            """);
        _dc.WriteUserGameLabels(Platform, "OutRun", """
            <InputLabels>
              <A>Boost</A>
            </InputLabels>
            """);
        ResolvedMapping mapping = Mapping(("A", ["ButtonA"]));

        ResolvedLabels labels = Build().Load(Game(platform: Platform, romName: "OutRun"), mapping);

        // The User file wins — Boost, not the Defaults Accelerate
        labels.LabelText.ShouldBeDictionaryOf(("ButtonA", "Boost"));
    }

    // ---- LaunchBoxId-based lookup ----

    [Fact]
    public void Load_GameMatchedByLaunchBoxId_WhenRomNameDoesNotMatchEntryName()
    {
        // The entry's name attribute is the canonical No-Intro name; the ROM on disk may differ.
        // Matching by id lets the entry be found regardless of the ROM filename.
        _dc.WriteGameLabels(Platform, "OutRun (USA, Europe)", """
            <InputLabels>
              <A>Brake</A>
            </InputLabels>
            """, launchBoxId: 42);
        ResolvedMapping mapping = Mapping(("A", ["ButtonA"]));
        GameInfo game = Game(platform: Platform, romName: "OutRun", launchBoxId: 42);

        ResolvedLabels labels = Build().Load(game, mapping);

        labels.LabelText.ShouldBeDictionaryOf(("ButtonA", "Brake"));
        labels.IsGameSpecific.ShouldBeTrue();
    }

    // ---- Fuzzy romName matching ----

    [Fact]
    public void Load_FuzzyMatch_BareEntryFoundByBracketedRomName()
    {
        // Entry uses the bare title; ROM on disk has a region tag. Fuzzy match strips the
        // ROM's brackets so "OutRun (USA, Europe)" finds the "OutRun" entry.
        _dc.WriteGameLabels(Platform, "OutRun", """
            <InputLabels>
              <A>Brake</A>
            </InputLabels>
            """);
        ResolvedMapping mapping = Mapping(("A", ["ButtonA"]));
        GameInfo game = Game(platform: Platform, romName: "OutRun (USA, Europe)");

        ResolvedLabels labels = Build().Load(game, mapping);

        labels.LabelText.ShouldBeDictionaryOf(("ButtonA", "Brake"));
        labels.IsGameSpecific.ShouldBeTrue();
    }

    [Fact]
    public void Load_FuzzyMatch_BracketedEntryFoundByBareRomName()
    {
        // Entry was written with a region tag; ROM on disk is bare. Fuzzy match strips the
        // entry's brackets so "OutRun" finds the "OutRun (USA, Europe)" entry.
        _dc.WriteGameLabels(Platform, "OutRun (USA, Europe)", """
            <InputLabels>
              <A>Brake</A>
            </InputLabels>
            """);
        ResolvedMapping mapping = Mapping(("A", ["ButtonA"]));
        GameInfo game = Game(platform: Platform, romName: "OutRun");

        ResolvedLabels labels = Build().Load(game, mapping);

        labels.LabelText.ShouldBeDictionaryOf(("ButtonA", "Brake"));
        labels.IsGameSpecific.ShouldBeTrue();
    }

    [Fact]
    public void Load_FuzzyMatch_BothBracketedDifferently_StillMatch()
    {
        // Entry and ROM both have region tags but different ones. Both normalize to the same
        // bare title, so the match succeeds.
        _dc.WriteGameLabels(Platform, "OutRun (USA, Europe)", """
            <InputLabels>
              <A>Brake</A>
            </InputLabels>
            """);
        ResolvedMapping mapping = Mapping(("A", ["ButtonA"]));
        GameInfo game = Game(platform: Platform, romName: "OutRun (USA)");

        ResolvedLabels labels = Build().Load(game, mapping);

        labels.LabelText.ShouldBeDictionaryOf(("ButtonA", "Brake"));
        labels.IsGameSpecific.ShouldBeTrue();
    }

    [Fact]
    public void Load_FuzzyMatch_NormalizationDoesNotOverMatch_UnrelatedTitles()
    {
        // Fuzzy match only strips brackets — it does not do substring or similarity matching.
        // "Sonic" should not match an "OutRun" entry.
        _dc.WriteGameLabels(Platform, "OutRun", """
            <InputLabels>
              <A>Brake</A>
            </InputLabels>
            """);
        ResolvedMapping mapping = Mapping(("A", ["ButtonA"]));
        GameInfo game = Game(platform: Platform, romName: "Sonic (USA)");

        ResolvedLabels labels = Build().Load(game, mapping);

        labels.LabelText.ShouldBeEmpty();
        labels.IsGameSpecific.ShouldBeFalse();
    }

    // ---- User <Defaults> block merging ----

    [Fact]
    public void Load_UserDefaultsBlockOverridesDefaultsBlock_PerButton()
    {
        // User <Defaults> wins per-button: Start is overridden; B is not present in User so
        // the Defaults entry is kept.
        _dc.WriteDefaultLabels(Platform, """
            <InputLabels>
              <Start>Pause</Start>
              <B>Cancel</B>
            </InputLabels>
            """);
        _dc.WriteUserDefaultLabels(Platform, """
            <InputLabels>
              <Start>Resume</Start>
            </InputLabels>
            """);
        ResolvedMapping mapping = Mapping(("Start", ["ButtonStart"]), ("B", ["ButtonB"]));

        ResolvedLabels labels = Build().Load(Game(platform: Platform, romName: "Unknown"), mapping);

        labels.LabelText.ShouldBeDictionaryOf(
            ("ButtonStart", "Resume"),
            ("ButtonB", "Cancel"));
        labels.IsGameSpecific.ShouldBeFalse();
    }
}
