using DynamicControls.InputMapping;
using NSubstitute;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;
using static DynamicControls.InputMapping.AnalogToDigitalMode;

namespace DynamicControls.Core.Tests.InputMapping;

/// <summary>
/// Unit tests for <see cref="PerGameXmlMappingProvider"/>. Each test supplies the platform's
/// controllers and the game's <see cref="GameMappingConfig"/> entry directly — no XML, no
/// filesystem. Covers the controller-resolution chain (named → unknown-falls-back → null →
/// platform-default), the per-game-over-baseline merge by Name, and AnalogToDigital inheritance.
/// </summary>
public class PerGameXmlMappingProviderTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly PerGameXmlMappingProvider _underTest;

    public PerGameXmlMappingProviderTests()
    {
        _underTest = new PerGameXmlMappingProvider(_logger);
    }

    [Fact]
    public void IsEnabled_AlwaysTrue()
    {
        _underTest.IsEnabled(new()).ShouldBeTrue();
    }

    [Fact]
    public void Load_NullRomName_ReturnsNull()
    {
        // given a game with no rom name
        var game = Game(romName: null!);

        // when the provider loads
        var result = _underTest.Load(game, platform: null)?.Current;

        // then the provider contributes nothing
        result.ShouldBeNull();
    }

    [Fact]
    public void Load_NoGameEntry_ReturnsNull()
    {
        // given no entry for this rom in the platform's file
        var game = Game();

        // when the provider loads
        var result = _underTest.Load(game, PlatformConfig(ControllerDef("Pad", isDefault: true)))?.Current;

        // then the provider contributes nothing
        result.ShouldBeNull();
    }

    [Fact]
    public void Load_ExplicitController_ResolvesNamedControllerFromPlatform()
    {
        // given a game entry selecting "Zapper" and a platform with that controller
        var game = Game();
        GameMappingConfig entry = GameEntry(inheritFrom: "Zapper", mappings: [("Trigger", "ButtonA")]);
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings: [("A", "ButtonA")]),
            ControllerDef(name: "Zapper", mappings: [("Trigger", "ButtonA")])).WithGames(entry);

        // when the provider loads
        var result = _underTest.Load(game, platform)?.Current;

        // then the named controller is selected as the baseline
        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Zapper");
    }

    [Fact]
    public void Load_UnknownController_LogsErrorAndFallsBackToPlatformDefault()
    {
        // given a game entry selecting a controller the platform doesn't declare
        var game = Game();
        GameMappingConfig entry = GameEntry(inheritFrom: "Unknown", mappings: [("A", "ButtonA")]);
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings: [("A", "ButtonA")])).WithGames(entry);

        // when the provider loads
        var result = _underTest.Load(game, platform)?.Current;

        // then the result falls back to the platform default and an error was logged
        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad");
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Unknown") && s.Contains("falling back")));
    }

    [Fact]
    public void Load_EntryNamesNoController_UsesPlatformDefault()
    {
        // given a game entry with no inheritFrom
        var game = Game();
        GameMappingConfig entry = GameEntry(mappings: [("A", "ButtonA")]);
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings: [("A", "ButtonA")])).WithGames(entry);

        // when the provider loads
        var result = _underTest.Load(game, platform)?.Current;

        // then the platform default fills in as baseline
        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad");
    }

    [Fact]
    public void Load_EntryNamesNoController_LogsThatItIsReadAgainstThePlatformDefault()
    {
        // given a game entry with no inheritFrom, over a platform with a default
        var game = Game();
        GameMappingConfig entry = GameEntry(mappings: [("A", "ButtonA")]);
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings: [("A", "ButtonA")])).WithGames(entry);

        // when the provider loads
        _underTest.Load(game, platform);

        // then the log says which pad the entry's button names were resolved against
        _logger.Received().Debug(Arg.Is<string>(s => s.Contains("names no controller") && s.Contains("platform default")));
    }

    [Fact]
    public void Load_EntryPresent_LogsThatTheEmulatorsConfigurationIsNotRead()
    {
        // given a game entry for the game
        var game = Game(romName: "sonic");
        GameMappingConfig entry = GameEntry(romName: "sonic", inheritFrom: "Pad", mappings: [("A", "ButtonA")]);
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings: [("A", "ButtonA")])).WithGames(entry);

        // when the provider loads
        _underTest.Load(game, platform);

        // then the log says the entry is the whole mapping, so an emulator's ignored config is explained
        _logger.Received(1).Debug(Arg.Is<string>(s => s.Contains("'sonic' supplies the whole mapping") && s.Contains("not read")));
    }

    [Fact]
    public void Load_NoGameEntry_DoesNotLogThatItSuppliesTheMapping()
    {
        // given no game entry (and no clone parent)
        // when the provider loads
        _underTest.Load(Game(), PlatformConfig(ControllerDef(name: "Pad", isDefault: true)));

        // then the provider declines silently, leaving the emulator providers to answer
        _logger.DidNotReceive().Debug(Arg.Is<string>(s => s.Contains("supplies the whole mapping")));
    }

    [Fact]
    public void Load_ExplicitController_DoesNotLogTheDefaultNotice()
    {
        var game = Game();
        GameMappingConfig entry = GameEntry(inheritFrom: "Pad", mappings: [("A", "ButtonA")]);
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings: [("A", "ButtonA")])).WithGames(entry);

        _underTest.Load(game, platform);

        _logger.DidNotReceive().Debug(Arg.Is<string>(s => s.Contains("names no controller")));
    }

    [Fact]
    public void Load_PlatformWithNoControllers_ReturnsConfigWithNullController()
    {
        // given a game entry in a file that declares no controllers
        var game = Game();
        GameMappingConfig entry = GameEntry(inheritFrom: "Pad", mappings: [("A", "ButtonA")]);

        // when the provider loads
        var result = _underTest.Load(game, PlatformConfig().WithGames(entry))?.Current;

        // then the result keeps the per-game mappings but has no controller resolution
        result.ShouldNotBeNull();
        result.Controller.ShouldBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonA")]);
    }

    [Fact]
    public void Load_LayersGameOverBaseline_ReplacesByName_AndAppendsNew()
    {
        // given a baseline with A, B, Start and a game entry that overrides B and adds Select
        var game = Game();
        GameMappingConfig entry = GameEntry(mappings:
            [
                ("B", "ButtonY"),     // override baseline's B
                ("Select", "ButtonSelect"), // new — not in baseline
            ]);
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings:
            [
                ("A", "ButtonA"),
                ("B", "ButtonB"),     // will be replaced
                ("Start", "ButtonStart"),
            ])).WithGames(entry);

        // when the provider loads
        var result = _underTest.Load(game, platform)?.Current;

        // then baseline-only entries (A, Start) remain, B is replaced by the game value, and the
        // new Select entry is appended
        result.ShouldNotBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("A", "ButtonA"),         // baseline preserved
            ("Start", "ButtonStart"), // baseline preserved
            ("B", "ButtonY"),         // game override
            ("Select", "ButtonSelect"), // game new
        ]);
    }

    [Fact]
    public void Load_SwapsTwoButtons_BothOldBindingsCleared()
    {
        // given a game entry that exchanges the controls A and B sit on
        var game = Game();
        GameMappingConfig entry = GameEntry(mappings:
            [
                ("A", "ButtonB"),
                ("B", "ButtonA"),
            ]);
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings:
            [
                ("A", "ButtonA"),
                ("B", "ButtonB"),
                ("Start", "ButtonStart"),
            ])).WithGames(entry);

        // when the provider loads
        var result = _underTest.Load(game, platform)?.Current;

        // then each button sits on the other's control, with no leftover baseline entry keeping
        // either control double-bound — naming both buttons is what makes a swap expressible
        result.ShouldNotBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("Start", "ButtonStart"),
            ("A", "ButtonB"),
            ("B", "ButtonA"),
        ]);
    }

    [Fact]
    public void Load_MovesOneButtonOntoAnOccupiedControl_LeavesTheOccupantInPlace()
    {
        // given a game entry that moves A onto B's control without saying what becomes of B
        var game = Game();
        GameMappingConfig entry = GameEntry(mappings: [("A", "ButtonB")]);
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings:
            [
                ("A", "ButtonA"),
                ("B", "ButtonB"),
            ])).WithGames(entry);

        // when the provider loads
        var result = _underTest.Load(game, platform)?.Current;

        // then B keeps ButtonB and A joins it there: the overlay drops baseline entries by button
        // name, never by the control they occupy. Both now drive ButtonB, which InputLabelsService
        // reports as a collision it cannot resolve. See issue #8.
        result.ShouldNotBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("B", "ButtonB"),
            ("A", "ButtonB"),
        ]);
    }

    [Fact]
    public void Load_Unmap_DropsBaselineEntryWithoutReplacement()
    {
        // given a baseline with A, B, Start and a game entry that <Unmap>s B
        var game = Game();
        GameMappingConfig entry = GameEntry(unmaps: ["B"]);
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings:
            [
                ("A", "ButtonA"),
                ("B", "ButtonB"),
                ("Start", "ButtonStart"),
            ])).WithGames(entry);

        // when the provider loads
        var result = _underTest.Load(game, platform)?.Current;

        // then B is gone entirely; A and Start survive from the baseline
        result.ShouldNotBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("A", "ButtonA"),
            ("Start", "ButtonStart"),
        ]);
    }

    [Fact]
    public void Load_UnmapAndMappingForSameName_MappingWins()
    {
        // given a game entry that both unmaps and remaps B — the <Mapping> entry wins because
        // the override-Names set is shared, but Mapping then appends the new value while Unmap
        // contributes nothing back
        var game = Game();
        GameMappingConfig entry = GameEntry(
                mappings: [("B", "ButtonY")],
                unmaps: ["B"]);
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings:
            [
                ("A", "ButtonA"),
                ("B", "ButtonB"),
            ])).WithGames(entry);

        // when the provider loads
        var result = _underTest.Load(game, platform)?.Current;

        // then B is present at the remapped target, not dropped
        result.ShouldNotBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("A", "ButtonA"),
            ("B", "ButtonY"),
        ]);
    }

    [Fact]
    public void Load_AnalogToDigitalNullInEntry_InheritsFromBaseline()
    {
        // given a game entry without AnalogToDigital and a controller that has one
        var game = Game();
        GameMappingConfig entry = GameEntry(inheritFrom: "Pad", mappings: [("A", "ButtonA")]);
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, analogToDigital: Right,
                mappings: [("A", "ButtonA")])).WithGames(entry);

        // when the provider loads
        var result = _underTest.Load(game, platform)?.Current;

        // then the baseline's AnalogToDigital flows through
        result.ShouldNotBeNull();
        result.AnalogToDigital.ShouldBe(Right);
    }

    [Fact]
    public void Load_NoGameEntry_CloneOfHasOne_ReturnsNull_SoTheClonesOwnConfigurationIsRead()
    {
        // given a clone with no entry of its own; the parent ROM has one selecting "3-Button"
        var game = Game(romName: "OutRun (Beta)") with { CloneOf = "OutRun (USA, Europe)" };
        GameMappingConfig parents = GameEntry(romName: "OutRun (USA, Europe)", inheritFrom: "3-Button", mappings: [("A", "ButtonX")]);
        var platform = PlatformConfig(
            ControllerDef(name: "6-Button", isDefault: true, mappings: [("A", "ButtonX")]),
            ControllerDef(name: "3-Button", mappings: [("A", "ButtonX")])).WithGames(parents);

        // when the provider loads
        var result = _underTest.Load(game, platform);

        // then it has nothing for the clone: unlike labels, a mapping is per rom, and the parent's
        // would switch off the clone's own MAME or RetroArch configuration
        result.ShouldBeNull();
    }

    [Fact]
    public void Load_AnalogToDigitalExplicitlySetInEntry_OverridesBaseline()
    {
        // given a game entry that explicitly sets AnalogToDigital and a controller with a
        // different value
        var game = Game();
        GameMappingConfig entry = GameEntry(inheritFrom: "Pad", analogToDigital: Left,
                mappings: [("A", "ButtonA")]);
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, analogToDigital: Right,
                mappings: [("A", "ButtonA")])).WithGames(entry);

        // when the provider loads
        var result = _underTest.Load(game, platform)?.Current;

        // then the per-game value wins — `??=` only fills in nulls
        result.ShouldNotBeNull();
        result.AnalogToDigital.ShouldBe(Left);
    }

    [Fact]
    public void Load_AnalogToDigitalNoneInEntry_SwitchesOffTheControllersMirror()
    {
        // given an entry saying analogToDigital="none" over a controller that mirrors right
        var game = Game();
        GameMappingConfig entry = GameEntry(inheritFrom: "Pad", statesAnalogToDigital: true);
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, analogToDigital: Right)).WithGames(entry);

        // when the provider loads
        ProvidedMapping? result = _underTest.Load(game, platform);

        // then neither the mapping nor its natural state mirrors
        result.ShouldNotBeNull();
        result.Current.AnalogToDigital.ShouldBeNull();
        result.Natural.AnalogToDigital.ShouldBeNull();
    }

    // ---- natural vs current ----

    [Fact]
    public void Load_MappingRemapsAButton_NaturalIsTheControllerAsDeclared()
    {
        // given an entry moving A onto ButtonY, over a controller that declares A as ButtonA
        var game = Game();
        GameMappingConfig entry = GameEntry(mappings: [("A", "ButtonY")]);
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings: [("A", "ButtonA"), ("B", "ButtonB")])).WithGames(entry);

        // when the provider loads
        ProvidedMapping? result = _underTest.Load(game, platform);

        // then the mapping the player has carries the remap, and the natural state is the
        // controller as declared, so the remap is told apart from a button in its usual place
        result.ShouldNotBeNull();
        result.Current.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("B", "ButtonB"), ("A", "ButtonY")]);
        result.Natural.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonA"), ("B", "ButtonB")]);
        result.Natural.Controller.ShouldBe("Pad");
    }

    [Fact]
    public void Load_EntryNamesANonDefaultController_NaturalIsThatController()
    {
        // given an entry naming "Zapper" and nothing else, where Zapper's button drives a different
        // input from the default pad's
        var game = Game();
        GameMappingConfig entry = GameEntry(inheritFrom: "Zapper");
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings: [("A", "ButtonA")]),
            ControllerDef(name: "Zapper", mappings: [("Trigger", "AxisTriggerRight")])).WithGames(entry);

        // when the provider loads
        ProvidedMapping? result = _underTest.Load(game, platform);

        // then the natural state is the named controller, not the platform default, so choosing a
        // controller reports nothing remapped
        result.ShouldNotBeNull();
        result.Natural.Controller.ShouldBe("Zapper");
        result.Natural.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("Trigger", "AxisTriggerRight")]);
        result.Current.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("Trigger", "AxisTriggerRight")]);
    }

    [Fact]
    public void Load_EntrySetsAnalogToDigital_NaturalTakesTheSameValue()
    {
        // the mirror is a statement about the hardware, so a stick mirror is never read as a remap
        var game = Game();
        GameMappingConfig entry = GameEntry(analogToDigital: Right, mappings: [("A", "ButtonY")]);
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, analogToDigital: Left, mappings: [("A", "ButtonA")])).WithGames(entry);

        ProvidedMapping? result = _underTest.Load(game, platform);

        result.ShouldNotBeNull();
        result.Current.AnalogToDigital.ShouldBe(Right);
        result.Natural.AnalogToDigital.ShouldBe(Right);
    }

    [Fact]
    public void Load_NoControllers_ReportsNothingRemapped()
    {
        // with no controller to read against there is nothing for the entry to remap
        var game = Game();
        GameMappingConfig entry = GameEntry(mappings: [("A", "ButtonY")]);

        ProvidedMapping? result = _underTest.Load(game, PlatformConfig().WithGames(entry));

        result.ShouldNotBeNull();
        result.Natural.ShouldBeSameAs(result.Current);
    }
}
