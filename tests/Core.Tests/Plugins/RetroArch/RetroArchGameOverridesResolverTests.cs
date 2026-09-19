using DynamicControls.InputMapping;
using DynamicControls.Plugins.RetroArch;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Unit tests for <see cref="RetroArchGameOverridesResolver"/> — the reading of RetroArch's own
/// configuration, shared by the source that picks a controller and the transform that applies
/// swaps. Focus: (1) gating, so a game that isn't a RetroArch game costs nothing; (2) resolving
/// the core's display name, with the DLL name as fallback when the .info file is missing;
/// (3) returning null when neither the cfg cascade nor the remap file says anything.
/// </summary>
public class RetroArchGameOverridesResolverTests
{
    // Path.Combine so Path.GetDirectoryName(RetroArchExe) == RetroArchDir on every platform.
    private static readonly string RetroArchDir = Path.Combine("Emulators", "RetroArch");
    private static readonly string RetroArchExe = Path.Combine(RetroArchDir, "retroarch.exe");
    private const string CoreDll = "genesis_plus_gx_libretro";
    private const string CoreDisplayName = "Genesis Plus GX";

    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IRetroArchCoreInfo _coreInfo = Substitute.For<IRetroArchCoreInfo>();
    private readonly IRetroArchCoreLoader _coreLoader = Substitute.For<IRetroArchCoreLoader>();
    private readonly IRetroArchOverridesResolver _cfgParser = Substitute.For<IRetroArchOverridesResolver>();
    private readonly IRetroArchOverridesResolver _remapParser = Substitute.For<IRetroArchOverridesResolver>();
    private readonly RetroArchGameOverridesResolver _underTest;

    public RetroArchGameOverridesResolverTests()
    {
        _underTest = new RetroArchGameOverridesResolver(
            _logger, _coreInfo, _coreLoader, _cfgParser, _remapParser);

        // happy-path defaults — individual tests override what they need
        _coreInfo.ReadDisplayName(RetroArchDir, CoreDll).Returns(CoreDisplayName);
        _coreLoader.Load(CoreDisplayName).Returns(CoreConfig());
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

    private static RetroArchInputOverrides Overrides(
        RetroArchControllerConfig? variant = null,
        Dictionary<string, int>? swaps = null) =>
        new(variant, swaps ?? []);

    private void StubCfg(RetroArchInputOverrides? result) =>
        _cfgParser
            .Parse(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RetroArchCoreConfig>(), Arg.Any<GameInfo>())
            .Returns(result);

    private void StubRmp(RetroArchInputOverrides? result) =>
        _remapParser
            .Parse(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RetroArchCoreConfig>(), Arg.Any<GameInfo>())
            .Returns(result);

    // ---- gating ----

    [Fact]
    public void Resolve_NullEmulatorPath_ReturnsNullWithoutProbingCore()
    {
        // given the game has no recorded emulator path — there is no RetroArch directory to read
        GameInfo game = Game() with { EmulatorPath = null };

        RetroArchGameOverrides? result = _underTest.Resolve(game);

        result.ShouldBeNull();
        _coreLoader.DidNotReceive().Load(Arg.Any<string>());
    }

    [Fact]
    public void Resolve_NullRomName_ReturnsNull()
    {
        // given LaunchBox metadata is incomplete
        GameInfo game = Game() with { RomName = null! };

        _underTest.Resolve(game).ShouldBeNull();
    }

    [Fact]
    public void Resolve_NullRetroArchCore_ReturnsNull()
    {
        // given LaunchBox supplied no -L core flag
        GameInfo game = Game() with { RetroArchCore = null };

        _underTest.Resolve(game).ShouldBeNull();
    }

    [Fact]
    public void Resolve_NonRetroArchEmulator_ReturnsNullWithoutProbingCore()
    {
        // given a MAME executable rather than RetroArch
        GameInfo game = Game() with { EmulatorPath = Path.Combine("Emulators", "MAME", "mame64.exe") };

        RetroArchGameOverrides? result = _underTest.Resolve(game);

        // the emulator check gates out before the filesystem is touched
        result.ShouldBeNull();
        _coreLoader.DidNotReceive().Load(Arg.Any<string>());
    }

    [Fact]
    public void Resolve_NoCoreDefinitionFile_ReturnsNullWithoutCallingParsers()
    {
        // given the plugin ships no definition for this core, so there is no variant catalogue
        _coreLoader.Load(CoreDisplayName).Returns((RetroArchCoreConfig?)null);

        RetroArchGameOverrides? result = _underTest.Resolve(Game());

        result.ShouldBeNull();
        _cfgParser.DidNotReceive().Parse(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RetroArchCoreConfig>(), Arg.Any<GameInfo>());
    }

    [Fact]
    public void Resolve_NeitherCfgNorRemapContributes_ReturnsNull()
    {
        // NSubstitute already returns null for these, so the stubs document the intent
        StubCfg(null);
        StubRmp(null);

        _underTest.Resolve(Game()).ShouldBeNull();
    }

    // ---- core display name ----

    [Fact]
    public void Resolve_CoreInfoFileMissing_FallsBackToTheDllName()
    {
        // given no .info file, so the human-readable name cannot be read
        _coreInfo.ReadDisplayName(RetroArchDir, CoreDll).Returns((string?)null);
        _coreLoader.Load(CoreDll).Returns(CoreConfig());
        StubCfg(Overrides());

        _underTest.Resolve(Game());

        // then every downstream lookup uses the DLL name instead
        _coreLoader.Received(1).Load(CoreDll);
        _coreLoader.DidNotReceive().Load(CoreDisplayName);
    }

    // ---- what it hands back ----

    [Fact]
    public void Resolve_BothLayersContribute_ReturnsBothUntouched()
    {
        // the resolver reads and reports; interpreting what it found is the callers' job
        RetroArchInputOverrides cfg = Overrides(swaps: new Dictionary<string, int> { ["a"] = 0 });
        RetroArchInputOverrides rmp = Overrides(
            variant: new RetroArchControllerConfig { Name = "Pad-6btn", RetropadIds = [] });
        StubCfg(cfg);
        StubRmp(rmp);

        RetroArchGameOverrides? result = _underTest.Resolve(Game());

        result.ShouldNotBeNull();
        result.Cfg.ShouldBeSameAs(cfg);
        result.Rmp.ShouldBeSameAs(rmp);
    }

    [Fact]
    public void Resolve_OnlyOneLayerContributes_TheOtherIsNull()
    {
        RetroArchInputOverrides cfg = Overrides(swaps: new Dictionary<string, int> { ["a"] = 0 });
        StubCfg(cfg);
        StubRmp(null);

        RetroArchGameOverrides? result = _underTest.Resolve(Game());

        result.ShouldNotBeNull();
        result.Cfg.ShouldBeSameAs(cfg);
        result.Rmp.ShouldBeNull();
    }
}
