using DynamicControls.InputMapping;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;
using static DynamicControls.InputMapping.AnalogToDigitalMode;

namespace DynamicControls.Core.Tests.InputMapping;

/// <summary>
/// Unit tests for <see cref="PerGameXmlMappingSource"/>. The loader is a substitute so each test
/// supplies the exact per-game <see cref="InputMappingConfig"/> the source should see — no XML,
/// no filesystem. Covers the controller-resolution chain (named → unknown-falls-back → null →
/// platform-default), the per-game-over-baseline merge by Name, and AnalogToDigital inheritance.
/// </summary>
public class PerGameXmlMappingSourceTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IInputMappingLoader _loader = Substitute.For<IInputMappingLoader>();
    private readonly PerGameXmlMappingSource _underTest;

    public PerGameXmlMappingSourceTests()
    {
        _underTest = new PerGameXmlMappingSource(_logger, _loader);
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

        // when the source loads
        var result = _underTest.Load(game, platform: null);

        // then the source contributes nothing and the loader is never called
        result.ShouldBeNull();
        _loader.DidNotReceive().LoadGameMapping(Arg.Any<GameInfo>());
    }

    [Fact]
    public void Load_NoPerGameXml_ReturnsNull()
    {
        // given no per-game mapping file exists for this rom
        var game = Game();
        _loader.LoadGameMapping(game).ReturnsNull();

        // when the source loads
        var result = _underTest.Load(game, PlatformConfig(ControllerDef("Pad", isDefault: true)));

        // then the source contributes nothing
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

        // when the source loads
        var result = _underTest.Load(game, platform);

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

        // when the source loads
        var result = _underTest.Load(game, platform);

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

        // when the source loads
        var result = _underTest.Load(game, platform);

        // then the platform default fills in as baseline
        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad");
    }

    [Fact]
    public void Load_NullPlatform_ReturnsConfigWithNullController()
    {
        // given a per-game XML and no platform controllers file
        var game = Game();
        _loader.LoadGameMapping(game).Returns(
            MappingConfig(controller: "Pad", mappings: [("A", "ButtonA")]));

        // when the source loads with no platform
        var result = _underTest.Load(game, platform: null);

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

        // when the source loads
        var result = _underTest.Load(game, platform);

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

        // when the source loads
        var result = _underTest.Load(game, platform);

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

        // when the source loads
        var result = _underTest.Load(game, platform);

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

        // when the source loads
        var result = _underTest.Load(game, platform);

        // then the baseline's AnalogToDigital flows through
        result.ShouldNotBeNull();
        result.AnalogToDigital.ShouldBe(Right);
    }

    /// <summary>
    /// Failing test demonstrating the CloneOf inheritance gap: <see cref="PerGameXmlMappingSource"/>
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

        // when the source loads
        var result = _underTest.Load(game, platform);

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

        // when the source loads
        var result = _underTest.Load(game, platform);

        // then the per-game value wins — `??=` only fills in nulls
        result.ShouldNotBeNull();
        result.AnalogToDigital.ShouldBe(Left);
    }
}
