using DynamicControls.Config;
using DynamicControls.InputMapping;
using DynamicControls.Plugins.Mame;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;

namespace DynamicControls.Core.Tests.Plugins.Mame;

/// <summary>
/// Unit tests for <see cref="MameMappingProvider"/>. The provider decides whether to apply
/// MAME cfg overrides (emulator-path gating, game.cfg → default.cfg cascade) and merges the
/// resulting override dict onto the platform's default controller, which is the natural state.
/// The cfg loader is substituted so each test controls exactly what the cascade sees; a test that
/// names a baseline has it declared as the platform's default controller.
/// </summary>
public class MameMappingProviderTests
{
    private static readonly string MameDir = Path.Combine("Emulators", "MAME");
    private static readonly string MameExe = Path.Combine(MameDir, "mame64.exe");
    private static readonly string GameCfgPath = Path.Combine(MameDir, "cfg", "OutRun.cfg");
    private static readonly string DefaultCfgPath = Path.Combine(MameDir, "cfg", "default.cfg");

    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IMameCfgLoader _cfgLoader = Substitute.For<IMameCfgLoader>();
    private readonly MameMappingProvider _underTest;

    public MameMappingProviderTests()
    {
        _underTest = new MameMappingProvider(_logger, _cfgLoader);
    }

    private static MamePortReach Reach(string name, params string[] inputs) => new(name, inputs, [], From: "");

    /// <summary>Runs the provider over a platform whose default controller is
    /// <paramref name="baseline"/>, and returns the mapping the player has.</summary>
    private InputMappingConfig? LoadOver(GameInfo game, InputMappingConfig baseline) =>
        _underTest.Load(game, PlatformConfig(ControllerDef(
            baseline.Controller ?? "Cabinet",
            isDefault: true,
            analogToDigital: baseline.AnalogToDigital,
            mappings: [.. baseline.Mappings.Select(m => (m.Name, m.Input))])))?.Current;

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

        // when the provider reports its enabled state
        // then it mirrors the config flag verbatim
        _underTest.IsEnabled(config).ShouldBe(enabled);
    }

    // ---- Load: gating ----

    [Fact]
    public void Load_NoEmulatorPath_ReturnsNullAndDoesNotProbeCfg()
    {
        // given a game with no EmulatorPath (LaunchBox didn't supply one)
        GameInfo game = Game() with { EmulatorPath = null };

        // when the provider runs
        var result = LoadOver(game, MappingConfig());

        // then null is returned and the cfg loader is never consulted
        result.ShouldBeNull();
        _cfgLoader.DidNotReceive().Load(Arg.Any<string>());
    }

    [Fact]
    public void Load_NonMameEmulator_ReturnsNullAndDoesNotProbeCfg()
    {
        // given a non-MAME emulator (RetroArch)
        GameInfo game = Game() with { EmulatorPath = Path.Combine("Emulators", "RetroArch", "retroarch.exe") };

        // when the provider runs
        var result = LoadOver(game, MappingConfig());

        // then it bails out before touching the cfg loader
        result.ShouldBeNull();
        _cfgLoader.DidNotReceive().Load(Arg.Any<string>());
    }

    // ---- Load: cfg cascade ----

    [Fact]
    public void Load_GameCfgPresent_UsesItAndSkipsDefault()
    {
        // given a per-game cfg with overrides and a (would-be) default.cfg
        _cfgLoader.Load(GameCfgPath).Returns([Reach("BUTTON1", "ButtonA")]);

        // when the provider runs
        var result = LoadOver(MameGame(), MappingConfig());

        // then the per-game cfg is consumed and default.cfg is never probed
        result.ShouldNotBeNull();
        _cfgLoader.Received(1).Load(GameCfgPath);
        _cfgLoader.DidNotReceive().Load(DefaultCfgPath);
    }

    [Fact]
    public void Load_GameCfgMissing_FallsBackToDefault()
    {
        // given no per-game cfg but a default.cfg with overrides
        _cfgLoader.Load(GameCfgPath).ReturnsNull();
        _cfgLoader.Load(DefaultCfgPath).Returns([Reach("BUTTON1", "ButtonA")]);

        // when the provider runs
        var result = LoadOver(MameGame(), MappingConfig());

        // then default.cfg supplies the overrides
        result.ShouldNotBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("BUTTON1", "ButtonA")]);
    }

    [Fact]
    public void Load_GameCfgEmpty_FallsBackToDefault()
    {
        // given a per-game cfg that exists but contains no overrides (empty dict)
        _cfgLoader.Load(GameCfgPath).Returns([]);
        _cfgLoader.Load(DefaultCfgPath).Returns([Reach("BUTTON1", "ButtonA")]);

        // when the provider runs
        var result = LoadOver(MameGame(), MappingConfig());

        // then the cascade treats an empty cfg the same as a missing one and falls back
        result.ShouldNotBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("BUTTON1", "ButtonA")]);
    }

    [Fact]
    public void Load_BothCfgsMissingOrEmpty_ReturnsNull()
    {
        // given neither cfg has any overrides
        _cfgLoader.Load(GameCfgPath).ReturnsNull();
        _cfgLoader.Load(DefaultCfgPath).Returns([]);

        // when the provider runs
        var result = LoadOver(MameGame(), MappingConfig());

        // then the provider declines and the chain falls through
        result.ShouldBeNull();
    }

    [Fact]
    public void Load_BothCfgsBothNull_ReturnsNull()
    {
        // given both loaders return null (files absent rather than empty)
        _cfgLoader.Load(GameCfgPath).ReturnsNull();
        _cfgLoader.Load(DefaultCfgPath).ReturnsNull();

        // when the provider runs
        var result = LoadOver(MameGame(), MappingConfig());

        // then the result is the same — no overrides to apply
        result.ShouldBeNull();
    }

    [Fact]
    public void Load_NullRomName_SkipsGameCfgAndTriesDefault()
    {
        // RomName is non-nullable on the GameInfo record but the provider treats null defensively
        // — verify only default.cfg is probed when the game name can't be used
        GameInfo game = MameGame() with { RomName = null! };
        _cfgLoader.Load(DefaultCfgPath).Returns([Reach("BUTTON1", "ButtonA")]);

        // when the provider runs
        LoadOver(game, MappingConfig());

        // then it never tries to build a per-game cfg path
        _cfgLoader.DidNotReceive().Load(GameCfgPath);
        _cfgLoader.Received(1).Load(DefaultCfgPath);
    }

    // ---- Load: natural state ----

    [Fact]
    public void Load_NaturalIsThePlatformDefaultController_NotAnotherOne()
    {
        // given a cfg that moves BUTTON1, over a platform declaring a non-default controller first
        // and the default after it
        _cfgLoader.Load(GameCfgPath).Returns([Reach("BUTTON1", "ButtonB")]);
        var platform = PlatformConfig(
            ControllerDef("Gun", mappings: [("TRIGGER", "AxisTriggerRight")]),
            ControllerDef("Cabinet", isDefault: true, mappings: [("BUTTON1", "ButtonA")]));

        // when the provider runs
        ProvidedMapping? result = _underTest.Load(MameGame(), platform);

        // then the natural state is the default controller as declared, untouched by the cfg — a
        // MAME cfg moves buttons, it never picks a controller — and the cfg's move is the current
        result.ShouldNotBeNull();
        result.Natural.Controller.ShouldBe("Cabinet");
        result.Natural.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("BUTTON1", "ButtonA")]);
        result.Current.Controller.ShouldBe("Cabinet");
        result.Current.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("BUTTON1", "ButtonB")]);
    }

    [Fact]
    public void Load_NoPlatformControllers_NaturalIsEmpty_AndTheCfgIsStillApplied()
    {
        // given a cfg with a binding, on a platform with no Controllers.xml
        _cfgLoader.Load(GameCfgPath).Returns([Reach("BUTTON1", "ButtonB")]);

        // when the provider runs
        ProvidedMapping? result = _underTest.Load(MameGame(), platform: null);

        // then there is no controller to start from, so the natural state is empty and the cfg's
        // bindings are the whole current mapping
        result.ShouldNotBeNull();
        result.Natural.Controller.ShouldBeNull();
        result.Natural.Mappings.ShouldBeEmpty();
        result.Current.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("BUTTON1", "ButtonB")]);
    }

    // ---- Load: merge semantics ----

    [Fact]
    public void Load_BaselineEntryNotOverridden_CopiedVerbatim()
    {
        // given a cfg that overrides one button but the baseline declares two
        _cfgLoader.Load(GameCfgPath).Returns([Reach("BUTTON1", "ButtonA")]);
        InputMappingConfig baseline = MappingConfig(mappings:
        [
            ("BUTTON1", "OldA"),
            ("BUTTON2", "OldB"),
        ]);

        // when the provider runs
        var result = LoadOver(MameGame(), baseline);

        // then BUTTON1 is replaced and BUTTON2 (no override) is preserved as-is
        result.ShouldNotBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("BUTTON1", "ButtonA"),
            ("BUTTON2", "OldB"),
        ]);
    }

    [Fact]
    public void Load_MultiInputOverride_ExpandsToOneEntryPerInput()
    {
        // given an override whose value lists two inputs (MAME OR-chained sequence)
        _cfgLoader.Load(GameCfgPath).Returns([Reach("BUTTON1", "ButtonA", "ButtonB")]);
        InputMappingConfig baseline = MappingConfig(mappings: [("BUTTON1", "OldA")]);

        // when the provider runs
        var result = LoadOver(MameGame(), baseline);

        // then the single baseline entry expands into one MappingEntry per overridden input
        result.ShouldNotBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("BUTTON1", "ButtonA"),
            ("BUTTON1", "ButtonB"),
        ]);
    }

    [Fact]
    public void Load_OverrideNameNotInBaseline_IsAppendedAtEnd()
    {
        // given an override for a name the baseline does not declare
        _cfgLoader.Load(GameCfgPath).Returns([Reach("EXTRA", "ButtonX")]);
        InputMappingConfig baseline = MappingConfig(mappings: [("BUTTON1", "OldA")]);

        // when the provider runs
        var result = LoadOver(MameGame(), baseline);

        // then the unmatched override is appended after the baseline entries
        result.ShouldNotBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("BUTTON1", "OldA"),
            ("EXTRA", "ButtonX"),
        ]);
    }

    [Fact]
    public void Load_DuplicateBaselineNameWithOverride_ExpandsOnce()
    {
        // given two baseline entries with the same Name and an override for that name
        _cfgLoader.Load(GameCfgPath).Returns([Reach("BUTTON1", "ButtonA")]);
        InputMappingConfig baseline = MappingConfig(mappings:
        [
            ("BUTTON1", "OldA1"),
            ("BUTTON1", "OldA2"),
        ]);

        // when the provider runs
        var result = LoadOver(MameGame(), baseline);

        // then the override is applied once — the duplicate baseline entry does not re-expand
        // (a single overridden Name should not multiply across baseline duplicates)
        result.ShouldNotBeNull();
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe([("BUTTON1", "ButtonA")]);
    }

    [Fact]
    public void Load_CarriesControllerAndAnalogToDigitalFromBaseline()
    {
        // given a baseline with a controller selection and an A2D mode
        _cfgLoader.Load(GameCfgPath).Returns([Reach("BUTTON1", "ButtonA")]);
        InputMappingConfig baseline = MappingConfig(
            controller: "Pad-6btn",
            analogToDigital: AnalogToDigitalMode.Left,
            mappings: [("BUTTON1", "OldA")]);

        // when the provider runs
        var result = LoadOver(MameGame(), baseline);

        // then Controller and AnalogToDigital pass through unchanged — MAME cfg shuffles button
        // bindings but does not change which controller is in play
        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-6btn");
        result.AnalogToDigital.ShouldBe(AnalogToDigitalMode.Left);
    }

    [Theory]
    // an override replacing a baseline entry, and one the baseline never declared
    [InlineData(true)]
    [InlineData(false)]
    public void Load_DerivedReach_IsMarkedOnTheMergedEntries(bool inBaseline)
    {
        // given a nudge whose stick half is derived from the port's own axis
        _cfgLoader.Load(GameCfgPath).Returns(
            [new MamePortReach("P1_POSITIONAL.dec", Own: ["AxisTriggerLeft"], Derived: ["AxisRightStickLeft"], From: "")]);
        InputMappingConfig baseline = MappingConfig(mappings: inBaseline
            ? [("P1_POSITIONAL.dec", "AxisLeftStickLeft")]
            : [("BUTTON1", "ButtonA")]);

        // when the provider runs
        var result = LoadOver(MameGame(), baseline);

        // then the derived half is marked as such, and the nudge's own binding is not
        result.ShouldNotBeNull();
        result.Mappings.Where(m => m.Name == "P1_POSITIONAL.dec").Select(m => (m.Input, m.Derived)).ShouldBe(
        [
            ("AxisTriggerLeft", false),
            ("AxisRightStickLeft", true),
        ]);
    }

    [Theory]
    // a name the baseline declares, and one it doesn't
    [InlineData("BUTTON1")]
    [InlineData("EXTRA")]
    public void Load_SameNameTwice_TheLaterStands_AndNothingIsLost(string name)
    {
        // nothing in the loader contract promises unique names, and a throw here is swallowed
        // into an empty overlay -- losing every label the game has, not just this button's
        _cfgLoader.Load(GameCfgPath).Returns([Reach(name, "ButtonA"), Reach("BUTTON2", "ButtonB"), Reach(name, "ButtonC")]);
        InputMappingConfig baseline = MappingConfig(mappings: [("BUTTON1", "OldA")]);

        var result = LoadOver(MameGame(), baseline);

        result.ShouldNotBeNull();
        result.Mappings.Where(m => m.Name == name).Select(m => m.Input).ShouldBe(["ButtonC"]);
        result.Mappings.Where(m => m.Name == "BUTTON2").Select(m => m.Input).ShouldBe(["ButtonB"]);
    }

    [Fact]
    public void Load_ButtonAlsoNamed_CarriesEachOtherNameToTheButton()
    {
        // given a cfg button that also answers to its port's tag and mask
        _cfgLoader.Load(GameCfgPath).Returns([Reach("OTHER", "AxisTriggerRight") with { AlsoNamed = ["OTHER:STROBE1:16"] }]);

        // when the provider runs
        var result = LoadOver(MameGame(), MappingConfig());

        // then the config says which button the other name means
        result.ShouldNotBeNull();
        result.ButtonAliases.ShouldBe(new Dictionary<string, string> { ["OTHER:STROBE1:16"] = "OTHER" });
    }
}
