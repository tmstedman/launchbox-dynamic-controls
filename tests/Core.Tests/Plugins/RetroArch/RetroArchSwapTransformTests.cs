using DynamicControls.Config;
using DynamicControls.InputMapping;
using DynamicControls.Plugins.RetroArch;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Unit tests for <see cref="RetroArchSwapTransform"/>, which applies the player's button swaps
/// on top of whichever mapping the source chain produced. Focus: (1) order — the cfg cascade's
/// swaps go on first and the remap's are applied to that result, so the remap wins where they
/// disagree; (2) declining — nothing to apply means returning null and leaving the baseline
/// untouched, which is what keeps it out of the pre-config snapshot.
/// </summary>
public class RetroArchSwapTransformTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IRetroArchGameOverridesResolver _overrides =
        Substitute.For<IRetroArchGameOverridesResolver>();
    private readonly IRetroArchSwapApplier _swapApplier = Substitute.For<IRetroArchSwapApplier>();
    private readonly RetroArchSwapTransform _underTest;

    public RetroArchSwapTransformTests()
    {
        _underTest = new RetroArchSwapTransform(_logger, _overrides, _swapApplier);

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

    private static InputMappingConfig Baseline() => new()
    {
        Controller = "Pad-3btn",
        Mappings = [new MappingEntry { Name = "A", Input = "ButtonA" }],
    };

    private static RetroArchInputOverrides Overrides(Dictionary<string, int>? swaps = null) =>
        new(null, swaps ?? []);

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

    // ---- declining ----

    [Fact]
    public void Transform_RetroArchSaysNothingAboutThisGame_ReturnsNull()
    {
        _overrides.Resolve(Arg.Any<GameInfo>()).Returns((RetroArchGameOverrides?)null);

        _underTest.Transform(Game(), Baseline()).ShouldBeNull();
        _swapApplier.DidNotReceive().Apply(Arg.Any<InputMappingConfig>(), Arg.Any<Dictionary<string, int>>());
    }

    [Fact]
    public void Transform_ConfigExistsButCarriesNoSwaps_ReturnsNullAndLeavesTheBaselineAlone()
    {
        // a cfg that only picked a controller variant has nothing for this transform to do — the
        // source has already acted on it. Returning null rather than a copy is what keeps the
        // baseline identical to the pre-config snapshot.
        StubOverrides(cfg: Overrides(), rmp: Overrides());

        _underTest.Transform(Game(), Baseline()).ShouldBeNull();
        _swapApplier.DidNotReceive().Apply(Arg.Any<InputMappingConfig>(), Arg.Any<Dictionary<string, int>>());
    }

    // ---- applying ----

    [Fact]
    public void Transform_OnlyTheCfgHasSwaps_AppliesThemOnce()
    {
        var cfgSwaps = new Dictionary<string, int> { ["a"] = 0 };
        StubOverrides(cfg: Overrides(cfgSwaps), rmp: null);

        _underTest.Transform(Game(), Baseline());

        _swapApplier.Received(1).Apply(Arg.Any<InputMappingConfig>(), cfgSwaps);
    }

    [Fact]
    public void Transform_OnlyTheRemapHasSwaps_AppliesThemOnce()
    {
        var rmpSwaps = new Dictionary<string, int> { ["b"] = 8 };
        StubOverrides(cfg: null, rmp: Overrides(rmpSwaps));

        _underTest.Transform(Game(), Baseline());

        _swapApplier.Received(1).Apply(Arg.Any<InputMappingConfig>(), rmpSwaps);
    }

    [Fact]
    public void Transform_BothHaveSwaps_TheRemapIsAppliedToTheCfgResult()
    {
        var cfgSwaps = new Dictionary<string, int> { ["a"] = 0 };
        var rmpSwaps = new Dictionary<string, int> { ["b"] = 8 };
        StubOverrides(cfg: Overrides(cfgSwaps), rmp: Overrides(rmpSwaps));

        // a sentinel from the cfg call proves the two are chained rather than independent
        var afterCfg = new InputMappingConfig { Controller = "after-cfg" };
        _swapApplier.Apply(Arg.Any<InputMappingConfig>(), cfgSwaps).Returns(afterCfg);

        _underTest.Transform(Game(), Baseline());

        _swapApplier.Received(1).Apply(afterCfg, rmpSwaps);
    }

    [Fact]
    public void Transform_AppliesTheFirstSwapsToTheBaselineItWasGiven()
    {
        // the transform amends what the source produced; it must not rebuild a mapping of its own
        var cfgSwaps = new Dictionary<string, int> { ["a"] = 0 };
        StubOverrides(cfg: Overrides(cfgSwaps), rmp: null);
        InputMappingConfig baseline = Baseline();

        _underTest.Transform(Game(), baseline);

        _swapApplier.Received(1).Apply(baseline, cfgSwaps);
    }
}
