using DynamicControls.Config;
using DynamicControls.InputMapping;
using DynamicControls.Plugins.RetroArch;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Unit tests for <see cref="RetroArchMappingSource"/>, which answers one question: which
/// controller is the player using? Reading RetroArch's configuration is substituted out, so each
/// test states directly what the cfg and remap layers said. Focus: (1) variant selection — the
/// remap wins over the cfg, no variant falls back to the platform default, an unrecognised
/// variant yields null plus an error; (2) result shape — the controller name and analogToDigital
/// come from the platform's controller, not from RetroArch.
///
/// <para>Applying the player's swaps is <see cref="RetroArchSwapTransform"/>'s job and is tested
/// there.</para>
/// </summary>
public class RetroArchMappingSourceTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IRetroArchGameOverridesResolver _overrides =
        Substitute.For<IRetroArchGameOverridesResolver>();
    private readonly RetroArchMappingSource _underTest;

    public RetroArchMappingSourceTests() => _underTest = new RetroArchMappingSource(_logger, _overrides);

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

    private static RetroArchInputOverrides Overrides(RetroArchControllerConfig? variant = null) =>
        new(variant, []);

    private static RetroArchControllerConfig Variant(string name) =>
        new() { Name = name, RetropadIds = [] };

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
        InputMappingConfig? result = _underTest.Load(Game(), platform: null);

        result.ShouldBeNull();
        _overrides.DidNotReceive().Resolve(Arg.Any<GameInfo>());
    }

    [Fact]
    public void Load_RetroArchSaysNothingAboutThisGame_ReturnsNull()
    {
        // given the game isn't a RetroArch game, or its core has no definition file
        _overrides.Resolve(Arg.Any<GameInfo>()).Returns((RetroArchGameOverrides?)null);

        InputMappingConfig? result = _underTest.Load(Game(), PlatformOf(Controller("Pad", isDefault: true)));

        // the source chain then falls through to the platform default
        result.ShouldBeNull();
    }

    // ---- variant selection ----

    [Fact]
    public void Load_NoVariantSelected_UsesPlatformDefault()
    {
        StubOverrides(cfg: Overrides(variant: null), rmp: Overrides(variant: null));

        InputMappingConfig? result = _underTest.Load(Game(), PlatformOf(Controller("Pad-3btn", isDefault: true)));

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-3btn");
    }

    [Fact]
    public void Load_OnlyTheCfgPicksAVariant_ThatVariantIsUsed()
    {
        StubOverrides(cfg: Overrides(Variant("Pad-6btn")), rmp: null);

        InputMappingConfig? result = _underTest.Load(
            Game(), PlatformOf(Controller("Pad-3btn", isDefault: true), Controller("Pad-6btn")));

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-6btn");
    }

    [Fact]
    public void Load_OnlyTheRemapPicksAVariant_ThatVariantIsUsed()
    {
        StubOverrides(cfg: null, rmp: Overrides(Variant("Pad-6btn")));

        InputMappingConfig? result = _underTest.Load(
            Game(), PlatformOf(Controller("Pad-3btn", isDefault: true), Controller("Pad-6btn")));

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-6btn");
    }

    [Fact]
    public void Load_BothPickAVariant_TheRemapWins()
    {
        StubOverrides(cfg: Overrides(Variant("CfgVariant")), rmp: Overrides(Variant("RmpVariant")));

        InputMappingConfig? result = _underTest.Load(
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

        InputMappingConfig? result = _underTest.Load(
            Game(), PlatformOf(Controller("Pad-3btn", isDefault: true), Controller("Pad-6btn")));

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-6btn");
    }

    [Fact]
    public void Load_SelectedVariantIsNotInTheControllersFile_ReturnsNullAndLogsError()
    {
        StubOverrides(cfg: Overrides(Variant("Pad-6btn")), rmp: null);

        InputMappingConfig? result = _underTest.Load(Game(), PlatformOf(Controller("Pad-3btn", isDefault: true)));

        result.ShouldBeNull();
        _logger.Received().Error(
            Arg.Is<string>(s => s.Contains("Pad-6btn") && s.Contains("Controllers.xml")));
    }

    // ---- result shape ----

    [Fact]
    public void Load_ControllerNameAndAnalogToDigitalComeFromThePlatformController()
    {
        StubOverrides(cfg: Overrides(), rmp: null);

        InputMappingConfig? result = _underTest.Load(
            Game(), PlatformOf(Controller("Pad-6btn", isDefault: true, analogToDigital: AnalogToDigitalMode.Left)));

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-6btn");
        result.AnalogToDigital.ShouldBe(AnalogToDigitalMode.Left);
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("A", "ButtonA")]);
    }
}
