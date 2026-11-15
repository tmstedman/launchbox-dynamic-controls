using DynamicControls.Config;
using DynamicControls.InputMapping;
using DynamicControls.Plugins.RetroArch;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Unit tests for <see cref="RetroArchMappingProvider"/>, which answers two questions: which
/// controller is the player using, and which of its buttons have they swapped? Reading RetroArch's
/// configuration is substituted out, so each test states directly what the cfg and remap layers
/// said. Focus: (1) variant selection — the remap wins over the cfg, no variant falls back to the
/// platform default, an unrecognised variant falls back to it too, with an error; (2) result
/// shape — the controller name and analogToDigital come from the platform's controller, not from
/// RetroArch, and that controller is the natural state; (3) order — the cfg cascade's swaps go on
/// first and the remap's are applied to that result, so the remap wins where they disagree;
/// (4) declining — RetroArch saying nothing about the game returns null, and a game with no swaps
/// reports nothing remapped.
/// </summary>
public class RetroArchMappingProviderTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IRetroArchGameOverridesResolver _overrides =
        Substitute.For<IRetroArchGameOverridesResolver>();
    private readonly IRetroArchSwapApplier _swapApplier = Substitute.For<IRetroArchSwapApplier>();
    private readonly RetroArchMappingProvider _underTest;

    public RetroArchMappingProviderTests()
    {
        _underTest = new RetroArchMappingProvider(_logger, _overrides, _swapApplier);

        // transparent by default, so tests focused elsewhere need not stub every Apply call
        _swapApplier
            .Apply(Arg.Any<InputMappingConfig>(), Arg.Any<Dictionary<string, int>>())
            .Returns(ci => ci.Arg<InputMappingConfig>());
    }

    // ---- fixtures ----

    private static GameInfo Game() => new(
        Platform: "Sega Genesis",
        RomName: "OutRun",
        CloneOf: null,
        LaunchBoxId: null,
        EmulatorPath: Path.Combine("Emulators", "RetroArch", "retroarch.exe"),
        RomDirectory: @"C:\Games\Sega Genesis",
        RetroArchCore: "genesis_plus_gx_libretro");

    private static PlatformControllersConfig PlatformOf(params ControllerConfig[] controllers) => new()
    {
        Controllers = [.. controllers],
    };

    private static ControllerConfig Controller(
        string name,
        bool isDefault = false,
        AnalogToDigitalMode? analogToDigital = null) => new()
        {
            Name = name,
            IsDefault = isDefault,
            AnalogToDigital = analogToDigital,
            Mappings = [new MappingEntry { Name = "A", Input = "ButtonA" }],
        };

    private static RetroArchInputOverrides Overrides(
        RetroArchControllerConfig? variant = null, Dictionary<string, int>? swaps = null) =>
        new(variant, swaps ?? []);

    private static RetroArchControllerConfig Variant(string name) =>
        new() { Name = name, RetropadIds = [] };

    /// <summary>The mapping the player has, which is what reaches the screen; the natural state
    /// is asserted where a test is about it.</summary>
    private InputMappingConfig? Load(GameInfo game, PlatformControllersConfig? platform) =>
        _underTest.Load(game, platform)?.Current;

    private void StubOverrides(RetroArchInputOverrides? cfg, RetroArchInputOverrides? rmp) =>
        _overrides.Resolve(Arg.Any<GameInfo>()).Returns(new RetroArchGameOverrides(cfg, rmp));

    // ---- IsEnabled ----

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEnabled_MirrorsRetroArchConfigFlag(bool enabled)
    {
        _underTest.IsEnabled(new GlobalConfig { EnableRetroArch = enabled }).ShouldBe(enabled);
    }

    // ---- gating ----

    [Fact]
    public void Load_NullPlatform_ReturnsNullWithoutReadingRetroArchConfig()
    {
        // without a Controllers.xml there is nothing to select a variant from
        ProvidedMapping? result = _underTest.Load(Game(), platform: null);

        result.ShouldBeNull();
        _overrides.DidNotReceive().Resolve(Arg.Any<GameInfo>());
    }

    [Fact]
    public void Load_RetroArchSaysNothingAboutThisGame_ReturnsNull()
    {
        // given the game isn't a RetroArch game, or its core has no definition file
        _overrides.Resolve(Arg.Any<GameInfo>()).Returns((RetroArchGameOverrides?)null);

        InputMappingConfig? result = Load(Game(), PlatformOf(Controller("Pad", isDefault: true)));

        // the provider chain then falls through to the platform default
        result.ShouldBeNull();
    }

    // ---- variant selection ----

    [Fact]
    public void Load_NoVariantSelected_UsesPlatformDefault()
    {
        StubOverrides(cfg: Overrides(variant: null), rmp: Overrides(variant: null));

        InputMappingConfig? result = Load(Game(), PlatformOf(Controller("Pad-3btn", isDefault: true)));

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-3btn");
    }

    [Fact]
    public void Load_OnlyTheCfgPicksAVariant_ThatVariantIsUsed()
    {
        StubOverrides(cfg: Overrides(Variant("Pad-6btn")), rmp: null);

        InputMappingConfig? result = Load(
            Game(), PlatformOf(Controller("Pad-3btn", isDefault: true), Controller("Pad-6btn")));

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-6btn");
    }

    [Fact]
    public void Load_OnlyTheRemapPicksAVariant_ThatVariantIsUsed()
    {
        StubOverrides(cfg: null, rmp: Overrides(Variant("Pad-6btn")));

        InputMappingConfig? result = Load(
            Game(), PlatformOf(Controller("Pad-3btn", isDefault: true), Controller("Pad-6btn")));

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-6btn");
    }

    [Fact]
    public void Load_BothPickAVariant_TheRemapWins()
    {
        StubOverrides(cfg: Overrides(Variant("CfgVariant")), rmp: Overrides(Variant("RmpVariant")));

        InputMappingConfig? result = Load(
            Game(), PlatformOf(Controller("CfgVariant"), Controller("RmpVariant", isDefault: true)));

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("RmpVariant");
    }

    [Fact]
    public void Load_RemapExistsButPicksNoVariant_TheCfgVariantStillApplies()
    {
        // the selection reads `rmp?.Variant ?? cfg?.Variant`, so a remap that contributes only
        // swaps must not suppress the cfg's variant by merely existing
        StubOverrides(cfg: Overrides(Variant("Pad-6btn")), rmp: Overrides(variant: null));

        InputMappingConfig? result = Load(
            Game(), PlatformOf(Controller("Pad-3btn", isDefault: true), Controller("Pad-6btn")));

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-6btn");
    }

    [Fact]
    public void Load_SelectedVariantIsNotInTheControllersFile_FallsBackToThePlatformDefaultAndLogsError()
    {
        // the swaps apply to the platform default controller
        StubOverrides(cfg: Overrides(Variant("Pad-6btn")), rmp: null);

        InputMappingConfig? result = Load(Game(), PlatformOf(Controller("Pad-3btn", isDefault: true)));

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-3btn");
        _logger.Received().Error(
            Arg.Is<string>(s => s.Contains("Pad-6btn") && s.Contains("Controllers.xml")));
    }

    // ---- result shape ----

    [Fact]
    public void Load_ControllerNameAndAnalogToDigitalComeFromThePlatformController()
    {
        StubOverrides(cfg: Overrides(), rmp: null);

        InputMappingConfig? result = Load(
            Game(), PlatformOf(Controller("Pad-6btn", isDefault: true, analogToDigital: AnalogToDigitalMode.Left)));

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-6btn");
        result.AnalogToDigital.ShouldBe(AnalogToDigitalMode.Left);
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonA")]);
    }

    // ---- the natural state ----

    [Fact]
    public void Load_NoSwaps_ReportsNothingRemapped()
    {
        // a cfg that only picked a controller variant has nothing to swap: the natural state is the
        // current one, so the service has no second mapping to resolve
        StubOverrides(cfg: Overrides(Variant("Pad-6btn")), rmp: Overrides());

        ProvidedMapping? result = _underTest.Load(
            Game(), PlatformOf(Controller("Pad-3btn", isDefault: true), Controller("Pad-6btn")));

        result.ShouldNotBeNull();
        result.Natural.ShouldBeSameAs(result.Current);
        _swapApplier.DidNotReceive().Apply(Arg.Any<InputMappingConfig>(), Arg.Any<Dictionary<string, int>>());
    }

    [Fact]
    public void Load_WithSwaps_NaturalIsTheControllerAsDeclaredAndCurrentHasTheSwaps()
    {
        var swapped = new InputMappingConfig { Controller = "Pad-3btn", Mappings = [new MappingEntry { Name = "A", Input = "ButtonB" }] };
        var swaps = new Dictionary<string, int> { ["a"] = 0 };
        StubOverrides(cfg: Overrides(swaps: swaps), rmp: null);
        _swapApplier.Apply(Arg.Any<InputMappingConfig>(), swaps).Returns(swapped);

        ProvidedMapping? result = _underTest.Load(Game(), PlatformOf(Controller("Pad-3btn", isDefault: true)));

        result.ShouldNotBeNull();
        result.Natural.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonA")]);
        result.Current.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonB")]);
    }

    [Fact]
    public void Load_SelectedVariantIsNotInTheControllersFile_TheSwapsApplyToThePlatformDefault()
    {
        // the fallback is the controller the swaps are read against, and its declared state the
        // natural one
        var cfgSwaps = new Dictionary<string, int> { ["a"] = 0 };
        StubOverrides(cfg: Overrides(Variant("Pad-6btn"), cfgSwaps), rmp: null);

        ProvidedMapping? result = _underTest.Load(Game(), PlatformOf(Controller("Pad-3btn", isDefault: true)));

        result.ShouldNotBeNull();
        result.Natural.Controller.ShouldBe("Pad-3btn");
        _swapApplier.Received(1).Apply(Arg.Is<InputMappingConfig>(c => c.Controller == "Pad-3btn"), cfgSwaps);
    }

    [Fact]
    public void Load_PlatformDeclaresNoController_NaturalIsEmpty_AndTheSwapsStillApply()
    {
        var cfgSwaps = new Dictionary<string, int> { ["a"] = 0 };
        StubOverrides(cfg: Overrides(swaps: cfgSwaps), rmp: null);

        ProvidedMapping? result = _underTest.Load(Game(), PlatformOf());

        result.ShouldNotBeNull();
        result.Natural.Controller.ShouldBeNull();
        result.Natural.Mappings.ShouldBeEmpty();
        _swapApplier.Received(1).Apply(result.Natural, cfgSwaps);
    }

    // ---- applying swaps ----

    [Fact]
    public void Load_OnlyTheCfgHasSwaps_AppliesThemOnce()
    {
        var cfgSwaps = new Dictionary<string, int> { ["a"] = 0 };
        StubOverrides(cfg: Overrides(swaps: cfgSwaps), rmp: null);

        Load(Game(), PlatformOf(Controller("Pad-3btn", isDefault: true)));

        _swapApplier.Received(1).Apply(Arg.Any<InputMappingConfig>(), cfgSwaps);
    }

    [Fact]
    public void Load_OnlyTheRemapHasSwaps_AppliesThemOnce()
    {
        var rmpSwaps = new Dictionary<string, int> { ["b"] = 8 };
        StubOverrides(cfg: null, rmp: Overrides(swaps: rmpSwaps));

        Load(Game(), PlatformOf(Controller("Pad-3btn", isDefault: true)));

        _swapApplier.Received(1).Apply(Arg.Any<InputMappingConfig>(), rmpSwaps);
    }

    [Fact]
    public void Load_BothHaveSwaps_TheRemapIsAppliedToTheCfgResult()
    {
        var cfgSwaps = new Dictionary<string, int> { ["a"] = 0 };
        var rmpSwaps = new Dictionary<string, int> { ["b"] = 8 };
        StubOverrides(cfg: Overrides(swaps: cfgSwaps), rmp: Overrides(swaps: rmpSwaps));

        // a sentinel from the cfg call proves the two are chained rather than independent
        var afterCfg = new InputMappingConfig { Controller = "after-cfg" };
        _swapApplier.Apply(Arg.Any<InputMappingConfig>(), cfgSwaps).Returns(afterCfg);

        Load(Game(), PlatformOf(Controller("Pad-3btn", isDefault: true)));

        _swapApplier.Received(1).Apply(afterCfg, rmpSwaps);
    }

    [Fact]
    public void Load_AppliesTheCfgSwapsToTheSelectedControllersMappings()
    {
        // the swaps amend the controller the player chose; they must not rebuild a mapping of their own
        var cfgSwaps = new Dictionary<string, int> { ["a"] = 0 };
        StubOverrides(cfg: Overrides(Variant("Pad-6btn"), cfgSwaps), rmp: null);

        Load(Game(), PlatformOf(Controller("Pad-3btn", isDefault: true), Controller("Pad-6btn")));

        _swapApplier.Received(1).Apply(Arg.Is<InputMappingConfig>(c => c.Controller == "Pad-6btn"), cfgSwaps);
    }

    [Fact]
    public void Load_NamesTheNaturalMappingsAsTheDpadTheStickPresses()
    {
        // RetroArch works out a stick's Dpad presses after remapping, so a stick standing in for
        // the Dpad keeps pressing the one the controller started with
        StubOverrides(cfg: null, rmp: Overrides(swaps: new Dictionary<string, int> { ["up"] = 0 }));

        ProvidedMapping? result = _underTest.Load(Game(), PlatformOf(Controller("Pad-3btn", isDefault: true)));

        result.ShouldNotBeNull();
        result.Current.DpadMirrorSource.ShouldBeSameAs(result.Natural.Mappings);
    }
}
