using DynamicControls.InputMapping;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;
using static DynamicControls.InputMapping.AnalogToDigitalMode;

namespace DynamicControls.Core.Tests.InputMapping;

/// <summary>
/// Unit tests for <see cref="PerGameXmlMappingProvider"/>. The loader is a substitute so each test
/// supplies the exact per-game <see cref="InputMappingConfig"/> the provider should see — no XML,
/// no filesystem. Covers the controller-resolution chain (named → unknown-falls-back → null →
/// platform-default), the per-game-over-baseline merge by Name, and AnalogToDigital inheritance.
/// </summary>
public class PerGameXmlMappingProviderTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IInputMappingLoader _loader = Substitute.For<IInputMappingLoader>();
    private readonly PerGameXmlMappingProvider _underTest;

    public PerGameXmlMappingProviderTests()
    {
        _underTest = new PerGameXmlMappingProvider(_logger, _loader);
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

        // then the provider contributes nothing and the loader is never called
        result.ShouldBeNull();
        _loader.DidNotReceive().LoadGameMapping(Arg.Any<GameInfo>());
    }

    [Fact]
    public void Load_NoPerGameXml_ReturnsNull()
    {
        // given no per-game mapping file exists for this rom
        var game = Game();
        _loader.LoadGameMapping(game).ReturnsNull();

        // when the provider loads
        var result = _underTest.Load(game, PlatformConfig(ControllerDef("Pad", isDefault: true)))?.Current;

        // then the provider contributes nothing
        result.ShouldBeNull();
    }

    [Fact]
    public void Load_ExplicitController_ResolvesNamedControllerFromPlatform()
    {
        // given a per-game XML selecting "Zapper" and a platform with that controller
        var game = Game();
        _loader.LoadGameMapping(game).Returns(
            MappingConfig(controller: "Zapper", mappings: [("Trigger", "ButtonA")]));
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings: [("A", "ButtonA")]),
            ControllerDef(name: "Zapper", mappings: [("Trigger", "ButtonA")]));

        // when the provider loads
        var result = _underTest.Load(game, platform)?.Current;

        // then the named controller is selected as the baseline
        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Zapper");
    }

    [Fact]
    public void Load_UnknownController_LogsErrorAndFallsBackToPlatformDefault()
    {
        // given a per-game XML selecting a controller the platform doesn't declare
        var game = Game();
        _loader.LoadGameMapping(game).Returns(
            MappingConfig(controller: "Unknown", mappings: [("A", "ButtonA")]));
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings: [("A", "ButtonA")]));

        // when the provider loads
        var result = _underTest.Load(game, platform)?.Current;

        // then the result falls back to the platform default and an error was logged
        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad");
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Unknown") && s.Contains("falling back")));
    }

    [Fact]
    public void Load_NoControllerInXml_UsesPlatformDefault()
    {
        // given a per-game XML with no controller= attribute
        var game = Game();
        _loader.LoadGameMapping(game).Returns(
            MappingConfig(mappings: [("A", "ButtonA")]));
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings: [("A", "ButtonA")]));

        // when the provider loads
        var result = _underTest.Load(game, platform)?.Current;

        // then the platform default fills in as baseline
        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad");
    }

    [Fact]
    public void Load_NoControllerInXml_LogsThatItIsReadAgainstThePlatformDefault()
    {
        // given a per-game XML with no controller= attribute, over a platform with a default
        var game = Game();
        _loader.LoadGameMapping(game).Returns(MappingConfig(mappings: [("A", "ButtonA")]));
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings: [("A", "ButtonA")]));

        // when the provider loads
        _underTest.Load(game, platform);

        // then the log says which pad the file's button names were resolved against
        _logger.Received().Debug(Arg.Is<string>(s => s.Contains("names no controller") && s.Contains("platform default")));
    }

    [Fact]
    public void Load_XmlPresent_LogsThatTheEmulatorsConfigurationIsNotRead()
    {
        // given a per-game XML for the game
        var game = Game(romName: "sonic");
        _loader.LoadGameMapping(game).Returns(MappingConfig(controller: "Pad", mappings: [("A", "ButtonA")]));
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings: [("A", "ButtonA")]));

        // when the provider loads
        _underTest.Load(game, platform);

        // then the log says the file is the whole mapping, so an emulator's ignored config is explained
        _logger.Received(1).Debug(Arg.Is<string>(s => s.Contains("'sonic' supplies the whole mapping") && s.Contains("not read")));
    }

    [Fact]
    public void Load_NoXml_DoesNotLogThatItSuppliesTheMapping()
    {
        // given no per-game XML (and no clone parent)
        _loader.LoadGameMapping(Arg.Any<GameInfo>()).ReturnsNull();

        // when the provider loads
        _underTest.Load(Game(), PlatformConfig(ControllerDef(name: "Pad", isDefault: true)));

        // then the provider declines silently, leaving the emulator providers to answer
        _logger.DidNotReceive().Debug(Arg.Is<string>(s => s.Contains("supplies the whole mapping")));
    }

    [Fact]
    public void Load_ExplicitController_DoesNotLogTheDefaultNotice()
    {
        var game = Game();
        _loader.LoadGameMapping(game).Returns(MappingConfig(controller: "Pad", mappings: [("A", "ButtonA")]));
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings: [("A", "ButtonA")]));

        _underTest.Load(game, platform);

        _logger.DidNotReceive().Debug(Arg.Is<string>(s => s.Contains("names no controller")));
    }

    [Fact]
    public void Load_NullPlatform_ReturnsConfigWithNullController()
    {
        // given a per-game XML and no platform controllers file
        var game = Game();
        _loader.LoadGameMapping(game).Returns(
            MappingConfig(controller: "Pad", mappings: [("A", "ButtonA")]));

        // when the provider loads with no platform
        var result = _underTest.Load(game, platform: null)?.Current;

        // then the result keeps the per-game mappings but has no controller resolution
        result.ShouldNotBeNull();
        result.Controller.ShouldBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonA")]);
    }

    [Fact]
    public void Load_LayersGameOverBaseline_ReplacesByName_AndAppendsNew()
    {
        // given a baseline with A, B, Start and a per-game XML that overrides B and adds Select
        var game = Game();
        _loader.LoadGameMapping(game).Returns(
            MappingConfig(mappings:
            [
                ("B", "ButtonY"),     // override baseline's B
                ("Select", "ButtonSelect"), // new — not in baseline
            ]));
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings:
            [
                ("A", "ButtonA"),
                ("B", "ButtonB"),     // will be replaced
                ("Start", "ButtonStart"),
            ]));

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
        // given a per-game XML that exchanges the controls A and B sit on
        var game = Game();
        _loader.LoadGameMapping(game).Returns(
            MappingConfig(mappings:
            [
                ("A", "ButtonB"),
                ("B", "ButtonA"),
            ]));
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings:
            [
                ("A", "ButtonA"),
                ("B", "ButtonB"),
                ("Start", "ButtonStart"),
            ]));

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
        // given a per-game XML that moves A onto B's control without saying what becomes of B
        var game = Game();
        _loader.LoadGameMapping(game).Returns(
            MappingConfig(mappings: [("A", "ButtonB")]));
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings:
            [
                ("A", "ButtonA"),
                ("B", "ButtonB"),
            ]));

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
        // given a baseline with A, B, Start and a per-game XML that <Unmap>s B
        var game = Game();
        _loader.LoadGameMapping(game).Returns(
            MappingConfig(unmaps: ["B"]));
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings:
            [
                ("A", "ButtonA"),
                ("B", "ButtonB"),
                ("Start", "ButtonStart"),
            ]));

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
        // given a per-game XML that both unmaps and remaps B — the <Mapping> entry wins because
        // the override-Names set is shared, but Mapping then appends the new value while Unmap
        // contributes nothing back
        var game = Game();
        _loader.LoadGameMapping(game).Returns(
            MappingConfig(
                mappings: [("B", "ButtonY")],
                unmaps: ["B"]));
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings:
            [
                ("A", "ButtonA"),
                ("B", "ButtonB"),
            ]));

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
    public void Load_AnalogToDigitalNullInXml_InheritsFromBaseline()
    {
        // given a per-game XML without AnalogToDigital and a controller that has one
        var game = Game();
        _loader.LoadGameMapping(game).Returns(
            MappingConfig(controller: "Pad", mappings: [("A", "ButtonA")]));
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, analogToDigital: Right,
                mappings: [("A", "ButtonA")]));

        // when the provider loads
        var result = _underTest.Load(game, platform)?.Current;

        // then the baseline's AnalogToDigital flows through
        result.ShouldNotBeNull();
        result.AnalogToDigital.ShouldBe(Right);
    }

    /// <summary>
    /// Failing test demonstrating the CloneOf inheritance gap: <see cref="PerGameXmlMappingProvider"/>
    /// only looks up the rom by exact name and never retries with <see cref="GameInfo.CloneOf"/>,
    /// even though the labels pipeline does (<c>InputLabelsService.LoadGameLabels</c> retries on
    /// null). A clone ROM with no per-game XML of its own should inherit its parent's mappings.
    /// </summary>
    [Fact]
    public void Load_NoGameXml_CloneOfHasGameXml_FallsBackToCloneOfMapping()
    {
        // given a clone with no per-game XML; the parent ROM has one selecting "3-Button"
        var game = Game(romName: "OutRun (Beta)") with { CloneOf = "OutRun (USA, Europe)" };
        _loader.LoadGameMapping(Arg.Is<GameInfo>(g => g.RomName == "OutRun (Beta)"))
            .ReturnsNull();
        _loader.LoadGameMapping(Arg.Is<GameInfo>(g => g.RomName == "OutRun (USA, Europe)"))
            .Returns(MappingConfig(controller: "3-Button", mappings: [("A", "ButtonX")]));
        var platform = PlatformConfig(
            ControllerDef(name: "6-Button", isDefault: true, mappings: [("A", "ButtonX")]),
            ControllerDef(name: "3-Button", mappings: [("A", "ButtonX")]));

        // when the provider loads
        var result = _underTest.Load(game, platform)?.Current;

        // then the parent's mapping is used (matches InputLabelsService.LoadGameLabels CloneOf behavior)
        result.ShouldNotBeNull();
        result.Controller.ShouldBe("3-Button");
    }

    [Fact]
    public void Load_AnalogToDigitalExplicitlySetInXml_OverridesBaseline()
    {
        // given a per-game XML that explicitly sets AnalogToDigital and a controller with a
        // different value
        var game = Game();
        _loader.LoadGameMapping(game).Returns(
            MappingConfig(controller: "Pad", analogToDigital: Left,
                mappings: [("A", "ButtonA")]));
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, analogToDigital: Right,
                mappings: [("A", "ButtonA")]));

        // when the provider loads
        var result = _underTest.Load(game, platform)?.Current;

        // then the per-game value wins — `??=` only fills in nulls
        result.ShouldNotBeNull();
        result.AnalogToDigital.ShouldBe(Left);
    }

    // ---- natural vs current ----

    [Fact]
    public void Load_MappingRemapsAButton_NaturalIsTheControllerAsDeclared()
    {
        // given a file moving A onto ButtonY, over a controller that declares A as ButtonA
        var game = Game();
        _loader.LoadGameMapping(game).Returns(MappingConfig(mappings: [("A", "ButtonY")]));
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings: [("A", "ButtonA"), ("B", "ButtonB")]));

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
    public void Load_FileNamesANonDefaultController_NaturalIsThatController()
    {
        // given a file naming "Zapper" and nothing else, where Zapper's button drives a different
        // input from the default pad's
        var game = Game();
        _loader.LoadGameMapping(game).Returns(MappingConfig(controller: "Zapper"));
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, mappings: [("A", "ButtonA")]),
            ControllerDef(name: "Zapper", mappings: [("Trigger", "AxisTriggerRight")]));

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
    public void Load_FileSetsAnalogToDigital_NaturalTakesTheSameValue()
    {
        // the mirror is a statement about the hardware, so a stick mirror is never read as a remap
        var game = Game();
        _loader.LoadGameMapping(game).Returns(
            MappingConfig(analogToDigital: Right, mappings: [("A", "ButtonY")]));
        var platform = PlatformConfig(
            ControllerDef(name: "Pad", isDefault: true, analogToDigital: Left, mappings: [("A", "ButtonA")]));

        ProvidedMapping? result = _underTest.Load(game, platform);

        result.ShouldNotBeNull();
        result.Current.AnalogToDigital.ShouldBe(Right);
        result.Natural.AnalogToDigital.ShouldBe(Right);
    }

    [Fact]
    public void Load_NoPlatform_ReportsNothingRemapped()
    {
        // with no controller to read against there is nothing for the file to remap
        var game = Game();
        _loader.LoadGameMapping(game).Returns(MappingConfig(mappings: [("A", "ButtonY")]));

        ProvidedMapping? result = _underTest.Load(game, platform: null);

        result.ShouldNotBeNull();
        result.Natural.ShouldBeSameAs(result.Current);
    }
}
