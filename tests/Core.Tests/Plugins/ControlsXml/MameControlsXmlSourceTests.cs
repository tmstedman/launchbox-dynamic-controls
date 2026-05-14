using DynamicControls.Config;
using DynamicControls.Labels;
using DynamicControls.Plugins.ControlsXml;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace DynamicControls.Core.Tests.Plugins.ControlsXml;

/// <summary>
/// Unit tests for <see cref="MameControlsXmlSource"/>. The source gates label lookups on the
/// emulator being a MAME variant and delegates the actual lookup to an <see cref="IControlsXmlLoader"/>.
/// All filesystem and parsing concerns live in the loader and are covered separately —
/// here we only verify the gating and pass-through behavior.
/// </summary>
public class MameControlsXmlSourceTests
{
    private static readonly string MameDir = Path.Combine("Emulators", "MAME");
    private static readonly string MameExe = Path.Combine(MameDir, "mame64.exe");
    private static readonly string RetroArchExe = Path.Combine("Emulators", "RetroArch", "retroarch.exe");

    private readonly IControlsXmlLoader _loader = Substitute.For<IControlsXmlLoader>();
    private readonly MameControlsXmlSource _underTest;

    public MameControlsXmlSourceTests()
    {
        _underTest = new MameControlsXmlSource(_loader);
    }

    private static GameInfo MameGame(string romName = "galaga", string? emulator = null) => new(
        Platform: "Arcade",
        RomName: romName,
        CloneOf: null,
        EmulatorPath: emulator ?? MameExe,
        RomDirectory: null,
        RetroArchCore: null);

    // ---- IsEnabled ----

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEnabled_IsAlwaysTrue(bool enabled)
    {
        // given a plugin config that toggles MAME on or off
        var config = new GlobalConfig { EnableMame = enabled };

        // when the source reports its enabled state
        // then it should be true regardless
        _underTest.IsEnabled(config).ShouldBeTrue();
    }

    // ---- LoadDefaultLabels ----

    [Fact]
    public void LoadDefaultLabels_AlwaysReturnsNull()
    {
        // controls.xml is per-ROM only; this source never supplies platform defaults
        _underTest.LoadDefaultLabels("Arcade").ShouldBeNull();
        _underTest.LoadDefaultLabels("Sega Genesis").ShouldBeNull();
    }

    // ---- Load: gating ----

    [Fact]
    public void Load_NoEmulatorPath_ReturnsNullAndDoesNotConsultLoader()
    {
        // given a game with no EmulatorPath
        GameInfo game = MameGame() with { EmulatorPath = null };

        // when the source is asked for labels
        var result = _underTest.Load(game);

        // then null is returned and the loader is never consulted
        result.ShouldBeNull();
        _loader.DidNotReceive().Lookup(Arg.Any<string>());
    }

    [Fact]
    public void Load_NonMameEmulator_ReturnsNullAndDoesNotConsultLoader()
    {
        // given a non-MAME emulator (RetroArch)
        GameInfo game = MameGame(emulator: RetroArchExe);

        // when the source is asked for labels
        var result = _underTest.Load(game);

        // then it bails out before touching the loader — controls.xml only applies to MAME
        result.ShouldBeNull();
        _loader.DidNotReceive().Lookup(Arg.Any<string>());
    }

    // ---- Load: delegation ----

    [Fact]
    public void Load_MameEmulator_DelegatesToLoaderWithRomName()
    {
        // given the loader has labels for this ROM
        InputLabelsConfig labels = new()
        {
            Labels =
            [
                new LabelEntry { Name = "BUTTON1", Label = "Fire" },
                new LabelEntry { Name = "START", Label = "Start" },
            ],
        };
        _loader.Lookup("galaga").Returns(labels);

        // when the source is asked for labels for that ROM
        var result = _underTest.Load(MameGame(romName: "galaga"));

        // then the loader is consulted with the ROM name and its result is returned verbatim
        result.ShouldBe(labels);
        _loader.Received(1).Lookup("galaga");
    }

    [Fact]
    public void Load_MameEmulator_PropagatesLoaderNull()
    {
        // given the loader has no entry for this ROM
        _loader.Lookup("notagame").ReturnsNull();

        // when the source is asked for labels for that ROM
        var result = _underTest.Load(MameGame(romName: "notagame"));

        // then the null pass-through reaches the caller — no fallback in this layer
        result.ShouldBeNull();
        _loader.Received(1).Lookup("notagame");
    }
}
