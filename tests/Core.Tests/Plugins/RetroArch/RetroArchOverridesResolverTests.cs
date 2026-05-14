using DynamicControls.Plugins.RetroArch;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Unit tests for <see cref="RetroArchOverridesResolver"/>. All three collaborators are substituted.
/// Focus: (1) null gate — loader returning null short-circuits the resolvers; (2) forwarding —
/// the loader receives the expected args; the variant resolver receives the merged view; the swap
/// resolver receives the full RetroArchGameData; (3) result assembly — all four combinations of
/// variant/swaps present or absent.
/// </summary>
public class RetroArchOverridesResolverTests
{
    private static readonly string RetroArchDir = Path.Combine("Emulators", "RetroArch");
    private static readonly string RomDirectory = Path.Combine("Games", "Sega Genesis");
    private const string Core = "Genesis Plus GX";
    private const string Rom = "OutRun";

    private readonly IRetroArchGameLoader _loader = Substitute.For<IRetroArchGameLoader>();
    private readonly IRetroArchVariantResolver _variantResolver = Substitute.For<IRetroArchVariantResolver>();
    private readonly IRetroArchSwapResolver _swapResolver = Substitute.For<IRetroArchSwapResolver>();
    private readonly RetroArchOverridesResolver _underTest;

    public RetroArchOverridesResolverTests()
    {
        _underTest = new RetroArchOverridesResolver(_loader, _variantResolver, _swapResolver);

        // defaults: non-null empty data; resolvers return null/empty
        _loader.Load(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<GameInfo>())
               .Returns(new RetroArchGameData(null, null, null, null));
        _swapResolver.ResolveSwaps(Arg.Any<RetroArchGameData>())
                     .Returns([]);
    }

    private static GameInfo Game() => new(
        Platform: "Sega Genesis",
        RomName: Rom,
        CloneOf: null,
        EmulatorPath: null,
        RomDirectory: RomDirectory,
        RetroArchCore: null);

    // ---- null gate ----

    [Fact]
    public void Parse_LoaderReturnsNull_ReturnsNullWithoutCallingResolvers()
    {
        _loader.Load(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<GameInfo>())
               .Returns((RetroArchGameData?)null);

        var result = _underTest.Parse(RetroArchDir, Core, new RetroArchCoreConfig(), Game());

        result.ShouldBeNull();
        _variantResolver.DidNotReceive().Resolve(Arg.Any<RetroArchGameData>(), Arg.Any<RetroArchCoreConfig>(), Arg.Any<string>());
        _swapResolver.DidNotReceive().ResolveSwaps(Arg.Any<RetroArchGameData>());
    }

    // ---- forwarding ----

    [Fact]
    public void Parse_ForwardsArgsToLoader()
    {
        _underTest.Parse(RetroArchDir, Core, new RetroArchCoreConfig(), Game());

        _loader.Received(1).Load(RetroArchDir, Core, Game());
    }

    [Fact]
    public void Parse_ForwardsDataAndCoreConfigToVariantResolver()
    {
        var data = new RetroArchGameData(null, null, null, new Dictionary<string, string> { ["input_libretro_device_p1"] = "260" });
        var coreConfig = new RetroArchCoreConfig();
        _loader.Load(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<GameInfo>()).Returns(data);
        _swapResolver.ResolveSwaps(Arg.Any<RetroArchGameData>())
                     .Returns(new Dictionary<string, int> { ["a"] = 0 });

        _underTest.Parse(RetroArchDir, Core, coreConfig, Game());

        _variantResolver.Received(1).Resolve(data, coreConfig, Core);
    }

    [Fact]
    public void Parse_ForwardsDataToSwapResolver()
    {
        var data = new RetroArchGameData(null, null, null, new Dictionary<string, string> { ["input_player1_a_btn"] = "0" });
        _loader.Load(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<GameInfo>()).Returns(data);
        _variantResolver.Resolve(Arg.Any<RetroArchGameData>(), Arg.Any<RetroArchCoreConfig>(), Arg.Any<string>())
                        .Returns(new RetroArchControllerConfig { Name = "Pad" });

        _underTest.Parse(RetroArchDir, Core, new RetroArchCoreConfig(), Game());

        // the swap resolver receives the full RetroArchGameData from the loader
        _swapResolver.Received(1).ResolveSwaps(data);
    }

    // ---- result assembly ----

    [Fact]
    public void Parse_NeitherVariantNorSwaps_ReturnsNull()
    {
        var result = _underTest.Parse(RetroArchDir, Core, new RetroArchCoreConfig(), Game());

        result.ShouldBeNull();
    }

    [Fact]
    public void Parse_VariantOnly_ReturnsOverridesWithVariant()
    {
        var variant = new RetroArchControllerConfig { Name = "Pad-6btn" };
        _variantResolver.Resolve(Arg.Any<RetroArchGameData>(), Arg.Any<RetroArchCoreConfig>(), Arg.Any<string>())
                        .Returns(variant);

        var result = _underTest.Parse(RetroArchDir, Core, new RetroArchCoreConfig(), Game());

        result.ShouldNotBeNull();
        result.Variant.ShouldBeSameAs(variant);
        result.Swaps.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_SwapsOnly_ReturnsOverridesWithSwaps()
    {
        var swaps = new Dictionary<string, int> { ["a"] = 0 };
        _swapResolver.ResolveSwaps(Arg.Any<RetroArchGameData>()).Returns(swaps);

        var result = _underTest.Parse(RetroArchDir, Core, new RetroArchCoreConfig(), Game());

        result.ShouldNotBeNull();
        result.Variant.ShouldBeNull();
        result.Swaps.ShouldBeSameAs(swaps);
    }

    [Fact]
    public void Parse_BothVariantAndSwaps_ReturnsBoth()
    {
        var variant = new RetroArchControllerConfig { Name = "Pad-6btn" };
        var swaps = new Dictionary<string, int> { ["a"] = 0, ["up"] = 5 };
        _variantResolver.Resolve(Arg.Any<RetroArchGameData>(), Arg.Any<RetroArchCoreConfig>(), Arg.Any<string>())
                        .Returns(variant);
        _swapResolver.ResolveSwaps(Arg.Any<RetroArchGameData>()).Returns(swaps);

        var result = _underTest.Parse(RetroArchDir, Core, new RetroArchCoreConfig(), Game());

        result.ShouldNotBeNull();
        result.Variant.ShouldBeSameAs(variant);
        result.Swaps.ShouldBeSameAs(swaps);
    }
}
