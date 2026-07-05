using DynamicControls.Config;
using DynamicControls.InputMapping;
using DynamicControls.Plugins.RetroArch;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Unit tests for <see cref="RetroArchMappingSource"/>. All five collaborators are substituted so
/// each test controls exactly what the cfg/rmp parsers return and what the swap applier produces.
/// Focus: (1) gating — the source bails before touching the filesystem when mandatory game fields
/// are absent; (2) display-name resolution — coreInfo null falls back to the DLL name; (3) variant
/// selection — rmp wins over cfg, no-variant falls through to platform default, unrecognised variant
/// yields null + error; (4) swap application order — cfg is applied first and its result feeds
/// into the rmp Apply call; (5) result shape — Controller and AnalogToDigital come from the
/// platform controller, not the parsers.
/// </summary>
public class RetroArchMappingSourceTests
{
    // Use Path.Combine so Path.GetDirectoryName(RetroArchExe) == RetroArchDir on all platforms.
    private static readonly string RetroArchDir = Path.Combine("Emulators", "RetroArch");
    private static readonly string RetroArchExe = Path.Combine(RetroArchDir, "retroarch.exe");
    private const string CoreDll = "genesis_plus_gx_libretro";
    private const string CoreDisplayName = "Genesis Plus GX";

    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IRetroArchCoreInfo _coreInfo = Substitute.For<IRetroArchCoreInfo>();
    private readonly IRetroArchCoreLoader _coreLoader = Substitute.For<IRetroArchCoreLoader>();
    private readonly IRetroArchOverridesResolver _cfgParser = Substitute.For<IRetroArchOverridesResolver>();
    private readonly IRetroArchOverridesResolver _remapParser = Substitute.For<IRetroArchOverridesResolver>();
    private readonly IRetroArchSwapApplier _swapApplier = Substitute.For<IRetroArchSwapApplier>();
    private readonly RetroArchMappingSource _underTest;

    public RetroArchMappingSourceTests()
    {
        _underTest = new RetroArchMappingSource(
            _logger, _coreInfo, _coreLoader, _cfgParser, _remapParser, _swapApplier);

        // happy-path defaults — individual tests override what they need
        _coreInfo.ReadDisplayName(RetroArchDir, CoreDll).Returns(CoreDisplayName);
        _coreLoader.Load(CoreDisplayName).Returns(CoreConfig());

        // swap applier is transparent by default so that tests focused elsewhere
        // can check the returned config without needing to stub every Apply call
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
        EmulatorPath: RetroArchExe,
        RomDirectory: @"C:\Games\Sega Genesis",
        RetroArchCore: CoreDll);

    private static RetroArchCoreConfig CoreConfig() => new()
    {
        Controllers = [new RetroArchControllerConfig { Name = "Pad-3btn", RetropadIds = [1] }],
    };

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
        RetroArchControllerConfig? variant = null,
        Dictionary<string, int>? swaps = null) =>
        new(variant, swaps ?? []);

    private static RetroArchControllerConfig Variant(string name) =>
        new() { Name = name, RetropadIds = [] };

    private void StubCfg(RetroArchInputOverrides? result) =>
        _cfgParser
            .Parse(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RetroArchCoreConfig>(), Arg.Any<GameInfo>())
            .Returns(result);

    private void StubRmp(RetroArchInputOverrides? result) =>
        _remapParser
            .Parse(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RetroArchCoreConfig>(), Arg.Any<GameInfo>())
            .Returns(result);

    // ---- IsEnabled ----

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEnabled_MirrorsRetroArchConfigFlag(bool enabled)
    {
        _underTest.IsEnabled(new GlobalConfig { EnableRetroArch = enabled }).ShouldBe(enabled);
    }

    // ---- Load: gating ----

    [Fact]
    public void Load_NullPlatform_ReturnsNullWithoutProbingCore()
    {
        // given no platform controllers file
        // when the source loads
        var result = _underTest.Load(Game(), platform: null);

        // then null is returned and no core XML lookup is attempted
        result.ShouldBeNull();
        _coreLoader.DidNotReceive().Load(Arg.Any<string>());
    }

    [Fact]
    public void Load_NullEmulatorPath_ReturnsNullWithoutProbingCore()
    {
        // given the game has no recorded emulator path
        GameInfo game = Game() with { EmulatorPath = null };

        // when the source loads
        var result = _underTest.Load(game, PlatformOf(Controller("Pad", isDefault: true)));

        // then null is returned — no emulator means we cannot determine the RetroArch directory
        result.ShouldBeNull();
        _coreLoader.DidNotReceive().Load(Arg.Any<string>());
    }

    [Fact]
    public void Load_NullRomName_ReturnsNull()
    {
        // given the game record has no ROM name (can happen when LaunchBox metadata is incomplete)
        GameInfo game = Game() with { RomName = null! };

        var result = _underTest.Load(game, PlatformOf(Controller("Pad", isDefault: true)));

        result.ShouldBeNull();
    }

    [Fact]
    public void Load_NullRetroArchCore_ReturnsNull()
    {
        // given LaunchBox supplied no -L core flag
        GameInfo game = Game() with { RetroArchCore = null };

        var result = _underTest.Load(game, PlatformOf(Controller("Pad", isDefault: true)));

        result.ShouldBeNull();
    }

    [Fact]
    public void Load_NonRetroArchEmulator_ReturnsNullWithoutProbingCore()
    {
        // given a MAME executable rather than RetroArch
        GameInfo game = Game() with { EmulatorPath = Path.Combine("Emulators", "MAME", "mame64.exe") };

        var result = _underTest.Load(game, PlatformOf(Controller("Pad", isDefault: true)));

        // then the emulator-type check gates the source out before the filesystem is touched
        result.ShouldBeNull();
        _coreLoader.DidNotReceive().Load(Arg.Any<string>());
    }

    [Fact]
    public void Load_NoCoreXml_ReturnsNullWithoutCallingParsers()
    {
        // given no per-core XML exists in rootDir/Data/RetroArch/
        _coreLoader.Load(CoreDisplayName).Returns((RetroArchCoreConfig?)null);

        var result = _underTest.Load(Game(), PlatformOf(Controller("Pad", isDefault: true)));

        // then null is returned and neither parser is consulted — without the core XML we have
        // no variant catalog to match device types against
        result.ShouldBeNull();
        _cfgParser.DidNotReceive().Parse(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RetroArchCoreConfig>(), Arg.Any<GameInfo>());
    }

    [Fact]
    public void Load_BothParsersReturnNull_ReturnsNull()
    {
        // given no cfg cascade and no rmp file contribute anything (both return null)
        // NSubstitute's default for nullable reference types is already null,
        // so no explicit stub is needed — this documents the intent
        StubCfg(null);
        StubRmp(null);

        var result = _underTest.Load(Game(), PlatformOf(Controller("Pad", isDefault: true)));

        // then the source has nothing to contribute
        result.ShouldBeNull();
    }

    // ---- Load: core display name ----

    [Fact]
    public void Load_CoreInfoReturnsNull_FallsBackToCoreDllAsDisplayName()
    {
        // given the .info file is missing and coreInfo can't resolve the human-readable name
        _coreInfo.ReadDisplayName(RetroArchDir, CoreDll).Returns((string?)null);
        // coreLoader must be reachable via the DLL name (not the human-readable name)
        _coreLoader.Load(CoreDll).Returns(CoreConfig());
        StubCfg(Overrides());

        // when the source loads
        _underTest.Load(Game(), PlatformOf(Controller("Pad-3btn", isDefault: true)));

        // then the DLL name is used as the fallback display name for all downstream lookups
        _coreLoader.Received(1).Load(CoreDll);
        _coreLoader.DidNotReceive().Load(CoreDisplayName);
    }

    // ---- Load: variant selection ----

    [Fact]
    public void Load_NoVariant_UsesPlatformDefault()
    {
        // given both parsers return overrides but neither selects a variant
        StubCfg(Overrides(variant: null));
        StubRmp(Overrides(variant: null));
        var platform = PlatformOf(Controller("Pad-3btn", isDefault: true));

        // when the source loads
        var result = _underTest.Load(Game(), platform);

        // then the platform default controller is used and its name surfaces on the result
        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-3btn");
    }

    [Fact]
    public void Load_CfgHasVariant_RmpNull_UsesCfgVariant()
    {
        // given the cfg cascade picks a 6-button variant, rmp does not exist
        StubCfg(Overrides(variant: Variant("Pad-6btn")));
        StubRmp(null);
        var platform = PlatformOf(Controller("Pad-3btn", isDefault: true), Controller("Pad-6btn"));

        var result = _underTest.Load(Game(), platform);

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-6btn");
    }

    [Fact]
    public void Load_RmpHasVariant_CfgNull_UsesRmpVariant()
    {
        // given the .rmp file picks a 6-button variant, cfg does not exist
        StubCfg(null);
        StubRmp(Overrides(variant: Variant("Pad-6btn")));
        var platform = PlatformOf(Controller("Pad-3btn", isDefault: true), Controller("Pad-6btn"));

        var result = _underTest.Load(Game(), platform);

        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-6btn");
    }

    [Fact]
    public void Load_BothHaveVariant_RmpWinsOverCfg()
    {
        // given cfg selects one variant and rmp selects a different one
        StubCfg(Overrides(variant: Variant("CfgVariant")));
        StubRmp(Overrides(variant: Variant("RmpVariant")));
        var platform = PlatformOf(Controller("CfgVariant"), Controller("RmpVariant", isDefault: true));

        var result = _underTest.Load(Game(), platform);

        // then rmp has the final say — cfg's variant is ignored
        result.ShouldNotBeNull();
        result.Controller.ShouldBe("RmpVariant");
    }

    [Fact]
    public void Load_RmpPresentButHasNoVariant_CfgVariantIsUsed()
    {
        // given rmp contributes swaps but no variant selection (rmp.Variant == null), while cfg
        // does select a variant — the variant expression is `rmp?.Variant ?? cfg?.Variant`, so a
        // null rmp.Variant on a non-null rmp should fall through to cfg's variant, not suppress it
        StubCfg(Overrides(variant: Variant("Pad-6btn")));
        StubRmp(Overrides(variant: null, swaps: new Dictionary<string, int> { ["a"] = 0 }));
        var platform = PlatformOf(Controller("Pad-3btn", isDefault: true), Controller("Pad-6btn"));

        var result = _underTest.Load(Game(), platform);

        // then cfg's variant is used — rmp's null variant does not suppress it
        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-6btn");
    }

    [Fact]
    public void Load_VariantNotInPlatform_ReturnsNullAndLogsError()
    {
        // given the selected variant is not declared in the platform's Controllers.xml
        StubCfg(Overrides(variant: Variant("Pad-6btn")));
        var platform = PlatformOf(Controller("Pad-3btn", isDefault: true)); // Pad-6btn absent

        var result = _underTest.Load(Game(), platform);

        // then null is returned and the missing variant is logged as an error
        result.ShouldBeNull();
        _logger.Received().Error(
            Arg.Is<string>(s => s.Contains("Pad-6btn") && s.Contains("Controllers.xml")));
    }

    // ---- Load: swap application order ----

    [Fact]
    public void Load_OnlyCfg_AppliesCfgSwapsOnce()
    {
        // given only the cfg cascade contributes swaps
        var cfgSwaps = new Dictionary<string, int> { ["a"] = 0 };
        StubCfg(Overrides(swaps: cfgSwaps));
        StubRmp(null);

        _underTest.Load(Game(), PlatformOf(Controller("Pad-3btn", isDefault: true)));

        // then the swap applier is called exactly once with the cfg swaps
        _swapApplier.Received(1).Apply(Arg.Any<InputMappingConfig>(), cfgSwaps);
    }

    [Fact]
    public void Load_OnlyRmp_AppliesRmpSwapsOnce()
    {
        // given only the .rmp file contributes swaps
        var rmpSwaps = new Dictionary<string, int> { ["b"] = 8 };
        StubCfg(null);
        StubRmp(Overrides(swaps: rmpSwaps));

        _underTest.Load(Game(), PlatformOf(Controller("Pad-3btn", isDefault: true)));

        _swapApplier.Received(1).Apply(Arg.Any<InputMappingConfig>(), rmpSwaps);
    }

    [Fact]
    public void Load_BothCfgAndRmp_AppliesCfgFirstThenRmpOnCfgResult()
    {
        // given both sources contribute swaps
        var cfgSwaps = new Dictionary<string, int> { ["a"] = 0 };
        var rmpSwaps = new Dictionary<string, int> { ["b"] = 8 };
        StubCfg(Overrides(swaps: cfgSwaps));
        StubRmp(Overrides(swaps: rmpSwaps));

        // make the cfg Apply call return a sentinel so we can prove the rmp Apply call
        // receives it — this is what verifies the two calls are chained, not independent
        var afterCfg = new InputMappingConfig { Controller = "after-cfg" };
        _swapApplier.Apply(Arg.Any<InputMappingConfig>(), cfgSwaps).Returns(afterCfg);

        _underTest.Load(Game(), PlatformOf(Controller("Pad-3btn", isDefault: true)));

        // then the rmp Apply received the cfg Apply result as its base — rmp operates on top of cfg
        _swapApplier.Received(1).Apply(afterCfg, rmpSwaps);
    }

    // ---- Load: result shape ----

    [Fact]
    public void Load_ResultCarriesControllerNameAndAnalogToDigitalFromPlatformController()
    {
        // given a platform controller with a specific name and AnalogToDigital mode
        StubCfg(Overrides());
        var platform = PlatformOf(new ControllerConfig
        {
            Name = "Pad-6btn",
            IsDefault = true,
            AnalogToDigital = AnalogToDigitalMode.Left,
            Mappings = [new MappingEntry { Name = "A", Input = "ButtonA" }],
        });

        var result = _underTest.Load(Game(), platform);

        // then the resolved controller's identity fields surface on the returned config unchanged
        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-6btn");
        result.AnalogToDigital.ShouldBe(AnalogToDigitalMode.Left);
    }
}
