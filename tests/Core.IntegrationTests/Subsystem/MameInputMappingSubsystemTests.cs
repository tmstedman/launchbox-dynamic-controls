using DynamicControls.Composition;
using DynamicControls.Config;
using DynamicControls.InputMapping;
using DynamicControls.Plugins.Mame;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;

namespace DynamicControls.Core.IntegrationTests.Subsystem;

/// <summary>
/// Verifies the MAME input-mapping transform with its real internal wiring intact:
/// <see cref="MameInputMappingSource"/> (emulator gate → cfg cascade → merge-onto-baseline) →
/// <see cref="MameCfgLoader"/> (real cfg XML parse + port-type normalization) →
/// <see cref="JoycodeMappingLoader"/> (real <c>JoycodeMapping.xml</c> parse) →
/// <see cref="JoycodeMapping"/> (OR-split translation), composed through the full
/// <see cref="InputMappingService"/> pipeline so the <c>Natural*</c> re-splice is exercised too.
///
/// The only faked seam is <see cref="IFileSystem"/>: the baseline <c>Controllers.xml</c>, the cfg
/// files, and <c>JoycodeMapping.xml</c> all live inline and are parsed for real. The sibling
/// <see cref="InputMappingSubsystemTests"/> deliberately injects a stub transform to keep MAME
/// internals out; this suite is the MAME half of that split. Unit tests
/// (<c>MameInputMappingSourceTests</c>, <c>MameCfgLoaderTests</c>) mock the cfg loader, and the
/// only full-stack coverage is <c>ArcadeEndToEndTests</c> (one on-disk fixture set per game) — so
/// the cfg cascade, real JOYCODE translation, merge, and natural-map composition are only
/// verified together here.
/// </summary>
public class MameInputMappingSubsystemTests
{
    private const string Platform = "Arcade";
    private static readonly string RootDir = Path.DirectorySeparatorChar + "dc";
    private static readonly string MamePath = Path.Combine(RootDir, "Emulators", "mame", "mame64.exe");
    private static readonly string NonMamePath =
        Path.Combine(RootDir, "Emulators", "retroarch", "retroarch.exe");

    private readonly MockDynamicControlsFilesystem _dc = new(RootDir);

    /// <summary>Wires the production mapping subsystem with the real MAME transform: leaving
    /// <c>transforms</c> defaulted lets the factory build
    /// <see cref="JoycodeMappingLoader"/> + <see cref="MameCfgLoader"/> +
    /// <see cref="MameInputMappingSource"/>, while <c>sources: []</c> drops the RetroArch middle
    /// source so the baseline comes purely from the platform <c>Controllers.xml</c>.</summary>
    private InputMappingService Build(bool enableMame = true) =>
        InputMappingFactory.Create(
            _dc.Lfs,
            new NullLogger(),
            config: new GlobalConfig { EnableMame = enableMame },
            sources: []);

    private GameInfo MameGame(string romName) =>
        Game(platform: Platform, romName: romName, emulatorPath: MamePath);

    /// <summary>Trimmed Arcade "Cabinet" baseline: just the ports these tests touch. Note BUTTON4
    /// (→ButtonY) is intentionally absent so a swap onto it has a conflict-free reverse lookup.</summary>
    private void WriteBaseline() => _dc.WritePlatform(Platform, """
        <Controllers>
          <Controller name="Cabinet" default="true">
            <Mapping name="BUTTON1" input="ButtonA" />
            <Mapping name="BUTTON2" input="ButtonB" />
            <Mapping name="BUTTON3" input="ButtonX" />
            <Mapping name="BUTTON6" input="ButtonRightShoulder" />
            <Mapping name="JOYSTICK_UP" input="ButtonDpadUp" />
            <Mapping name="START" input="ButtonStart" />
          </Controller>
        </Controllers>
        """);

    /// <summary>Minimal JOYCODE → generic vocabulary covering the codes referenced below.</summary>
    private void WriteJoycodeMapping() => _dc.WriteMameMapping("""
        <JoycodeMapping>
          <Mapping joycode="JOYCODE_1_BUTTON1" input="ButtonA" />
          <Mapping joycode="JOYCODE_1_BUTTON2" input="ButtonB" />
          <Mapping joycode="JOYCODE_1_BUTTON3" input="ButtonX" />
          <Mapping joycode="JOYCODE_1_BUTTON4" input="ButtonY" />
          <Mapping joycode="JOYCODE_1_BUTTON6" input="ButtonRightShoulder" />
          <Mapping joycode="JOYCODE_1_HAT1UP" input="ButtonDpadUp" />
          <Mapping joycode="JOYCODE_1_YAXIS_UP_SWITCH" input="AxisLeftStickUp" />
          <Mapping joycode="JOYCODE_1_START" input="ButtonStart" />
        </JoycodeMapping>
        """);

    // ---- cfg overrides merged onto the baseline ----

    [Fact]
    public void Load_PerGameCfg_OverridesMatchingPorts_AndAppendsPortsNotInBaseline()
    {
        WriteBaseline();
        WriteJoycodeMapping();
        // BUTTON3 is remapped onto physical button 4 (→ButtonY); BUTTON5 isn't in the baseline so
        // it is appended; BUTTON1 is untouched and must pass through verbatim.
        _dc.WriteMameCfg("dkong.cfg", """
            <mameconfig>
              <system name="dkong">
                <input>
                  <port type="P1_BUTTON3"><newseq type="standard">JOYCODE_1_BUTTON4</newseq></port>
                  <port type="P1_BUTTON5"><newseq type="standard">JOYCODE_1_BUTTON6</newseq></port>
                </input>
              </system>
            </mameconfig>
            """);

        ResolvedMapping mapping = Build().Load(MameGame("dkong"));

        mapping.ButtonToInput["BUTTON3"].ShouldBe(["ButtonY"]);          // matched → replaced
        mapping.ButtonToInput["BUTTON5"].ShouldBe(["ButtonRightShoulder"]); // not in baseline → appended
        mapping.ButtonToInput["BUTTON1"].ShouldBe(["ButtonA"]);          // untouched → verbatim
    }

    [Fact]
    public void Load_MultiJoycodeOrSequence_FansPortOutToEveryGeneric()
    {
        WriteBaseline();
        WriteJoycodeMapping();
        // A single port whose sequence ORs two JOYCODEs drives both generics at once.
        _dc.WriteMameCfg("dkong.cfg", """
            <mameconfig>
              <system name="dkong">
                <input>
                  <port type="P1_JOYSTICK_UP">
                    <newseq type="standard">JOYCODE_1_HAT1UP OR JOYCODE_1_YAXIS_UP_SWITCH</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        ResolvedMapping mapping = Build().Load(MameGame("dkong"));

        mapping.ButtonToInput["JOYSTICK_UP"].ShouldBe(["ButtonDpadUp", "AxisLeftStickUp"]);
    }

    [Fact]
    public void Load_PortTypeNormalization_StartNormalized_OtherPlayersIgnored()
    {
        WriteBaseline();
        WriteJoycodeMapping();
        // START1 normalizes to START; the P2 port is not a player-1 input and must be dropped, so
        // it can't smuggle an override onto BUTTON1.
        _dc.WriteMameCfg("dkong.cfg", """
            <mameconfig>
              <system name="dkong">
                <input>
                  <port type="P1_BUTTON1"><newseq type="standard">JOYCODE_1_BUTTON2</newseq></port>
                  <port type="START1"><newseq type="standard">JOYCODE_1_START</newseq></port>
                  <port type="P2_BUTTON1"><newseq type="standard">JOYCODE_1_BUTTON6</newseq></port>
                </input>
              </system>
            </mameconfig>
            """);

        ResolvedMapping mapping = Build().Load(MameGame("dkong"));

        mapping.ButtonToInput["BUTTON1"].ShouldBe(["ButtonB"]);   // only P1's override applied
        mapping.ButtonToInput["START"].ShouldBe(["ButtonStart"]); // START1 → START
    }

    // ---- cfg cascade: {rom}.cfg first, then default.cfg ----

    [Fact]
    public void Load_NoRomCfg_FallsBackToDefaultCfg()
    {
        WriteBaseline();
        WriteJoycodeMapping();
        // No dkong.cfg; default.cfg supplies the override.
        _dc.WriteMameCfg("default.cfg", """
            <mameconfig>
              <system name="default">
                <input>
                  <port type="P1_BUTTON1"><newseq type="standard">JOYCODE_1_BUTTON2</newseq></port>
                </input>
              </system>
            </mameconfig>
            """);

        ResolvedMapping mapping = Build().Load(MameGame("dkong"));

        mapping.ButtonToInput["BUTTON1"].ShouldBe(["ButtonB"]);
    }

    [Fact]
    public void Load_RomCfgPresent_WinsOverDefaultCfg()
    {
        WriteBaseline();
        WriteJoycodeMapping();
        _dc.WriteMameCfg("dkong.cfg", """
            <mameconfig>
              <system name="dkong">
                <input>
                  <port type="P1_BUTTON1"><newseq type="standard">JOYCODE_1_BUTTON3</newseq></port>
                </input>
              </system>
            </mameconfig>
            """);
        _dc.WriteMameCfg("default.cfg", """
            <mameconfig>
              <system name="default">
                <input>
                  <port type="P1_BUTTON1"><newseq type="standard">JOYCODE_1_BUTTON2</newseq></port>
                </input>
              </system>
            </mameconfig>
            """);

        ResolvedMapping mapping = Build().Load(MameGame("dkong"));

        // dkong.cfg short-circuits the cascade, so BUTTON1 follows it (ButtonX), not default (ButtonB).
        mapping.ButtonToInput["BUTTON1"].ShouldBe(["ButtonX"]);
    }

    // ---- no overrides → baseline passes through unchanged ----

    [Fact]
    public void Load_NoCfgFiles_TransformReturnsNull_BaselineUnchanged()
    {
        WriteBaseline();
        WriteJoycodeMapping();

        ResolvedMapping mapping = Build().Load(MameGame("dkong"));

        // No cfg → no transform → active map equals the natural baseline.
        mapping.ButtonToInput["BUTTON3"].ShouldBe(["ButtonX"]);
        mapping.ButtonToInput.ShouldBe(mapping.NaturalButtonToInput);
        mapping.InputToButton.ShouldBe(mapping.NaturalInputToButton);
    }

    // ---- gating ----

    [Fact]
    public void Load_MameDisabled_TransformFilteredOut_CfgIgnored()
    {
        WriteBaseline();
        WriteJoycodeMapping();
        _dc.WriteMameCfg("dkong.cfg", """
            <mameconfig>
              <system name="dkong">
                <input>
                  <port type="P1_BUTTON3"><newseq type="standard">JOYCODE_1_BUTTON4</newseq></port>
                </input>
              </system>
            </mameconfig>
            """);

        // EnableMame=false removes the transform from the pipeline entirely.
        ResolvedMapping mapping = Build(enableMame: false).Load(MameGame("dkong"));

        mapping.ButtonToInput["BUTTON3"].ShouldBe(["ButtonX"]);
    }

    [Fact]
    public void Load_NonMameEmulator_TransformSelfGates_CfgIgnored()
    {
        WriteBaseline();
        WriteJoycodeMapping();
        _dc.WriteMameCfg("dkong.cfg", """
            <mameconfig>
              <system name="dkong">
                <input>
                  <port type="P1_BUTTON3"><newseq type="standard">JOYCODE_1_BUTTON4</newseq></port>
                </input>
              </system>
            </mameconfig>
            """);

        // Transform is enabled but gates itself off because the emulator isn't MAME.
        ResolvedMapping mapping = Build().Load(
            Game(platform: Platform, romName: "dkong", emulatorPath: NonMamePath));

        mapping.ButtonToInput["BUTTON3"].ShouldBe(["ButtonX"]);
    }

    // ---- user-layer override ----

    [Fact]
    public void Load_UserMameMappingFilePresent_WinsOverDefaultsFile()
    {
        // Defaults JoycodeMapping.xml: JOYCODE_1_BUTTON1 → ButtonA
        // User JoycodeMapping.xml:     JOYCODE_1_BUTTON1 → ButtonB
        // MAME cfg maps P1_BUTTON1 → JOYCODE_1_BUTTON1, so whichever mapping wins determines
        // which generic input BUTTON1 ends up driving.
        WriteBaseline();
        _dc.WriteMameMapping("""
            <JoycodeMapping>
              <Mapping joycode="JOYCODE_1_BUTTON1" input="ButtonA" />
            </JoycodeMapping>
            """);
        _dc.WriteUserMameMapping("""
            <JoycodeMapping>
              <Mapping joycode="JOYCODE_1_BUTTON1" input="ButtonB" />
            </JoycodeMapping>
            """);
        _dc.WriteMameCfg("dkong.cfg", """
            <mameconfig>
              <system name="dkong">
                <input>
                  <port type="P1_BUTTON1"><newseq type="standard">JOYCODE_1_BUTTON1</newseq></port>
                </input>
              </system>
            </mameconfig>
            """);

        ResolvedMapping mapping = Build().Load(MameGame("dkong"));

        // The User MameMapping wins — ButtonB, not the Defaults ButtonA
        mapping.ButtonToInput["BUTTON1"].ShouldBe(["ButtonB"]);
    }

    // ---- composition: natural-map re-splice survives a real JOYCODE swap ----

    [Fact]
    public void Scenario_RealJoycodeSwap_ActiveMapTransformed_NaturalsRetainBaseline()
    {
        WriteBaseline();
        WriteJoycodeMapping();
        // BUTTON3 (naturally ButtonX) is physically remapped onto button 4 (→ButtonY).
        _dc.WriteMameCfg("dkong.cfg", """
            <mameconfig>
              <system name="dkong">
                <input>
                  <port type="P1_BUTTON3"><newseq type="standard">JOYCODE_1_BUTTON4</newseq></port>
                </input>
              </system>
            </mameconfig>
            """);

        ResolvedMapping mapping = Build().Load(MameGame("dkong"));

        // Active view reflects the swap...
        mapping.ButtonToInput["BUTTON3"].ShouldBe(["ButtonY"]);
        mapping.InputToButton["ButtonY"].ShouldBe("BUTTON3");
        // ...but the Naturals re-spliced from the pre-transform baseline still record ButtonX,
        // so IsMapped/remap detection can tell ButtonX's physical button is no longer in play.
        mapping.NaturalButtonToInput["BUTTON3"].ShouldBe(["ButtonX"]);
        mapping.NaturalInputToButton["ButtonX"].ShouldBe("BUTTON3");
        mapping.InputToButton.ContainsKey("ButtonX").ShouldBeFalse();
    }
}
