using DynamicControls.Config;
using DynamicControls.InputMapping;
using DynamicControls.Plugins.Mame;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;

namespace DynamicControls.Core.Tests.Plugins.Mame;

/// <summary>
/// Unit tests for <see cref="MameInputMappingSource"/>. The transform decides whether to apply
/// MAME cfg overrides (emulator-path gating, game.cfg → default.cfg cascade) and merges the
/// resulting override dict onto a baseline <see cref="InputMappingConfig"/>. The cfg loader is
/// substituted so each test controls exactly what the cascade sees.
/// </summary>
public class MameInputMappingSourceTests
{
    private static readonly string MameDir = Path.Combine("Emulators", "MAME");
    private static readonly string MameExe = Path.Combine(MameDir, "mame64.exe");
    private static readonly string GameCfgPath = Path.Combine(MameDir, "cfg", "OutRun.cfg");
    private static readonly string DefaultCfgPath = Path.Combine(MameDir, "cfg", "default.cfg");

    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IMameCfgLoader _cfgLoader = Substitute.For<IMameCfgLoader>();
    private readonly MameInputMappingSource _underTest;

    public MameInputMappingSourceTests()
    {
        _underTest = new MameInputMappingSource(_logger, _cfgLoader);
    }

    private static GameInfo MameGame(string romName = "OutRun", string emulator = null!) =>
        Game(romName) with { EmulatorPath = emulator ?? MameExe };

    // ---- IsEnabled ----

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEnabled_FollowsGlobalConfigFlag(bool enabled)
    {
        // given a plugin config that toggles MAME on or off
        var config = new GlobalConfig { EnableMame = enabled };

        // when the transform reports its enabled state
        // then it mirrors the config flag verbatim
        _underTest.IsEnabled(config).ShouldBe(enabled);
    }

    // ---- Transform: gating ----

    [Fact]
    public void Transform_NoEmulatorPath_ReturnsNullAndDoesNotProbeCfg()
    {
        // given a game with no EmulatorPath (LaunchBox didn't supply one)
        GameInfo game = Game() with { EmulatorPath = null };

        // when the transform runs
        var result = _underTest.Transform(game, MappingConfig());

        // then null is returned and the cfg loader is never consulted
        result.ShouldBeNull();
        _cfgLoader.DidNotReceive().Load(Arg.Any<string>());
    }

    [Fact]
    public void Transform_NonMameEmulator_ReturnsNullAndDoesNotProbeCfg()
    {
        // given a non-MAME emulator (RetroArch)
        GameInfo game = Game() with { EmulatorPath = Path.Combine("Emulators", "RetroArch", "retroarch.exe") };

        // when the transform runs
        var result = _underTest.Transform(game, MappingConfig());

        // then it bails out before touching the cfg loader
        result.ShouldBeNull();
        _cfgLoader.DidNotReceive().Load(Arg.Any<string>());
    }

    // ---- Transform: cfg cascade ----

    [Fact]
    public void Transform_GameCfgPresent_UsesItAndSkipsDefault()
    {
        // given a per-game cfg with overrides and a (would-be) default.cfg
        _cfgLoader.Load(GameCfgPath).Returns(new Dictionary<string, List<string>>
        {
            ["BUTTON1"] = ["ButtonA"],
        });

        // when the transform runs
        var result = _underTest.Transform(MameGame(), MappingConfig());

        // then the per-game cfg is consumed and default.cfg is never probed
        result.ShouldNotBeNull();
        _cfgLoader.Received(1).Load(GameCfgPath);
        _cfgLoader.DidNotReceive().Load(DefaultCfgPath);
    }

    [Fact]
    public void Transform_GameCfgMissing_FallsBackToDefault()
    {
        // given no per-game cfg but a default.cfg with overrides
        _cfgLoader.Load(GameCfgPath).ReturnsNull();
        _cfgLoader.Load(DefaultCfgPath).Returns(new Dictionary<string, List<string>>
        {
            ["BUTTON1"] = ["ButtonA"],
        });

        // when the transform runs
        var result = _underTest.Transform(MameGame(), MappingConfig());

        // then default.cfg supplies the overrides
        result.ShouldNotBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("BUTTON1", "ButtonA")]);
    }

    [Fact]
    public void Transform_GameCfgEmpty_FallsBackToDefault()
    {
        // given a per-game cfg that exists but contains no overrides (empty dict)
        _cfgLoader.Load(GameCfgPath).Returns([]);
        _cfgLoader.Load(DefaultCfgPath).Returns(new Dictionary<string, List<string>>
        {
            ["BUTTON1"] = ["ButtonA"],
        });

        // when the transform runs
        var result = _underTest.Transform(MameGame(), MappingConfig());

        // then the cascade treats an empty cfg the same as a missing one and falls back
        result.ShouldNotBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("BUTTON1", "ButtonA")]);
    }

    [Fact]
    public void Transform_BothCfgsMissingOrEmpty_ReturnsNull()
    {
        // given neither cfg has any overrides
        _cfgLoader.Load(GameCfgPath).ReturnsNull();
        _cfgLoader.Load(DefaultCfgPath).Returns([]);

        // when the transform runs
        var result = _underTest.Transform(MameGame(), MappingConfig());

        // then the transform produces no override and yields to the next pipeline stage
        result.ShouldBeNull();
    }

    [Fact]
    public void Transform_BothCfgsBothNull_ReturnsNull()
    {
        // given both loaders return null (files absent rather than empty)
        _cfgLoader.Load(GameCfgPath).ReturnsNull();
        _cfgLoader.Load(DefaultCfgPath).ReturnsNull();

        // when the transform runs
        var result = _underTest.Transform(MameGame(), MappingConfig());

        // then the result is the same — no overrides to apply
        result.ShouldBeNull();
    }

    [Fact]
    public void Transform_NullRomName_SkipsGameCfgAndTriesDefault()
    {
        // RomName is non-nullable on the GameInfo record but the source treats null defensively
        // — verify only default.cfg is probed when the game name can't be used
        GameInfo game = MameGame() with { RomName = null! };
        _cfgLoader.Load(DefaultCfgPath).Returns(new Dictionary<string, List<string>>
        {
            ["BUTTON1"] = ["ButtonA"],
        });

        // when the transform runs
        _underTest.Transform(game, MappingConfig());

        // then it never tries to build a per-game cfg path
        _cfgLoader.DidNotReceive().Load(GameCfgPath);
        _cfgLoader.Received(1).Load(DefaultCfgPath);
    }

    // ---- Transform: merge semantics ----

    [Fact]
    public void Transform_BaselineEntryNotOverridden_CopiedVerbatim()
    {
        // given a cfg that overrides one button but the baseline declares two
        _cfgLoader.Load(GameCfgPath).Returns(new Dictionary<string, List<string>>
        {
            ["BUTTON1"] = ["ButtonA"],
        });
        InputMappingConfig baseline = MappingConfig(mappings:
        [
            ("BUTTON1", "OldA"),
            ("BUTTON2", "OldB"),
        ]);

        // when the transform runs
        var result = _underTest.Transform(MameGame(), baseline);

        // then BUTTON1 is replaced and BUTTON2 (no override) is preserved as-is
        result.ShouldNotBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("BUTTON1", "ButtonA"),
            ("BUTTON2", "OldB"),
        ]);
    }

    [Fact]
    public void Transform_MultiInputOverride_ExpandsToOneEntryPerInput()
    {
        // given an override whose value lists two inputs (MAME OR-chained sequence)
        _cfgLoader.Load(GameCfgPath).Returns(new Dictionary<string, List<string>>
        {
            ["BUTTON1"] = ["ButtonA", "ButtonB"],
        });
        InputMappingConfig baseline = MappingConfig(mappings: [("BUTTON1", "OldA")]);

        // when the transform runs
        var result = _underTest.Transform(MameGame(), baseline);

        // then the single baseline entry expands into one MappingEntry per overridden input
        result.ShouldNotBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("BUTTON1", "ButtonA"),
            ("BUTTON1", "ButtonB"),
        ]);
    }

    [Fact]
    public void Transform_OverrideNameNotInBaseline_IsAppendedAtEnd()
    {
        // given an override for a name the baseline does not declare
        _cfgLoader.Load(GameCfgPath).Returns(new Dictionary<string, List<string>>
        {
            ["EXTRA"] = ["ButtonX"],
        });
        InputMappingConfig baseline = MappingConfig(mappings: [("BUTTON1", "OldA")]);

        // when the transform runs
        var result = _underTest.Transform(MameGame(), baseline);

        // then the unmatched override is appended after the baseline entries
        result.ShouldNotBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("BUTTON1", "OldA"),
            ("EXTRA", "ButtonX"),
        ]);
    }

    [Fact]
    public void Transform_DuplicateBaselineNameWithOverride_ExpandsOnce()
    {
        // given two baseline entries with the same Name and an override for that name
        _cfgLoader.Load(GameCfgPath).Returns(new Dictionary<string, List<string>>
        {
            ["BUTTON1"] = ["ButtonA"],
        });
        InputMappingConfig baseline = MappingConfig(mappings:
        [
            ("BUTTON1", "OldA1"),
            ("BUTTON1", "OldA2"),
        ]);

        // when the transform runs
        var result = _underTest.Transform(MameGame(), baseline);

        // then the override is applied once — the duplicate baseline entry does not re-expand
        // (a single overridden Name should not multiply across baseline duplicates)
        result.ShouldNotBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("BUTTON1", "ButtonA")]);
    }

    [Fact]
    public void Transform_CarriesControllerAndAnalogToDigitalFromBaseline()
    {
        // given a baseline with a controller selection and an A2D mode
        _cfgLoader.Load(GameCfgPath).Returns(new Dictionary<string, List<string>>
        {
            ["BUTTON1"] = ["ButtonA"],
        });
        InputMappingConfig baseline = MappingConfig(
            controller: "Pad-6btn",
            analogToDigital: AnalogToDigitalMode.Left,
            mappings: [("BUTTON1", "OldA")]);

        // when the transform runs
        var result = _underTest.Transform(MameGame(), baseline);

        // then Controller and AnalogToDigital pass through unchanged — MAME cfg shuffles button
        // bindings but does not change which controller is in play
        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-6btn");
        result.AnalogToDigital.ShouldBe(AnalogToDigitalMode.Left);
    }
}
