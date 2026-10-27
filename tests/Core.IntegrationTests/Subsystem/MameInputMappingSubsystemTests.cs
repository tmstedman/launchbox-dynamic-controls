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
            _dc.Fs,
            new NullLogger(),
            config: new GlobalConfig { EnableMame = enableMame },
            sources: []);

    private GameInfo MameGame(string romName) =>
        Game(platform: Platform, romName: romName, emulatorPath: MamePath);

    /// <summary>Trimmed Arcade "Cabinet" baseline: just the ports these tests touch. Note BUTTON4
    /// (→ButtonY) is intentionally absent so a swap onto it has a conflict-free reverse lookup.
    /// Mapping names carry the "P1_" prefix, matching the raw cfg port types verbatim (#16); only
    /// START1 keeps its cfg name, like every other port -- nothing is rewritten on the way in.</summary>
    private void WriteBaseline() => _dc.WritePlatform(Platform, """
        <Controllers>
          <Controller name="Cabinet" default="true">
            <Mapping name="P1_BUTTON1" input="ButtonA" />
            <Mapping name="P1_BUTTON2" input="ButtonB" />
            <Mapping name="P1_BUTTON3" input="ButtonX" />
            <Mapping name="P1_BUTTON6" input="ButtonRightShoulder" />
            <Mapping name="P1_JOYSTICK_UP" input="ButtonDpadUp" />
            <Mapping name="START1" input="ButtonStart" />
            <Control name="P1_PADDLE" mappings="P1_PADDLE.inc P1_PADDLE.dec" />
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
          <Mapping joycode="JOYCODE_1_HAT1UP" axis="JOYCODE_1_HAT1Y" half="dec" input="ButtonDpadUp" />
          <Mapping joycode="JOYCODE_1_YAXIS_UP_SWITCH" axis="JOYCODE_1_YAXIS" half="dec" input="AxisLeftStickUp" />
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

        mapping.ButtonToInput["P1_BUTTON3"].ShouldBe(["ButtonY"]);          // matched → replaced
        mapping.ButtonToInput["P1_BUTTON5"].ShouldBe(["ButtonRightShoulder"]); // not in baseline → appended
        mapping.ButtonToInput["P1_BUTTON1"].ShouldBe(["ButtonA"]);          // untouched → verbatim
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

        mapping.ButtonToInput["P1_JOYSTICK_UP"].ShouldBe(["ButtonDpadUp", "AxisLeftStickUp"]);
    }

    [Fact]
    public void Load_PortTypeNormalization_StartNormalized_P2KeepsItsOwnDistinctKey()
    {
        WriteBaseline();
        WriteJoycodeMapping();
        // START1 keeps its name. P2_BUTTON1 is a distinct key from P1_BUTTON1 (#16), so its
        // own override lands under its own name rather than smuggling onto P1_BUTTON1.
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

        mapping.ButtonToInput["P1_BUTTON1"].ShouldBe(["ButtonB"]);              // P1's own override
        mapping.ButtonToInput["P2_BUTTON1"].ShouldBe(["ButtonRightShoulder"]);  // P2's own, distinct key
        mapping.ButtonToInput["START1"].ShouldBe(["ButtonStart"]);              // verbatim from the cfg
    }

    // ---- true analogue ports: standard, increment and decrement newseqs ----

    [Fact]
    public void Load_AnalogueStandardUnbound_DigitalIncrementAndDecrementBindingsApply()
    {
        WriteBaseline();
        WriteJoycodeMapping();
        // adillor's shape: a trackball/paddle axis with no analogue hardware assigned, driven
        // instead by two ordinary buttons nudging the value each way. Each nudge direction gets
        // its own button now, so the two can carry different labels; the <Control> on the port is
        // what still lets a game that draws no distinction reach both by naming PADDLE alone.
        _dc.WriteMameCfg("dkong.cfg", """
            <mameconfig>
              <system name="dkong">
                <input>
                  <port type="P1_PADDLE">
                    <newseq type="standard">NONE</newseq>
                    <newseq type="increment">JOYCODE_1_BUTTON1</newseq>
                    <newseq type="decrement">JOYCODE_1_BUTTON2</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        ResolvedMapping mapping = Build().Load(MameGame("dkong"));

        // each direction addressable on its own...
        mapping.ButtonToInput["P1_PADDLE.inc"].ShouldBe(["ButtonA"]);
        mapping.ButtonToInput["P1_PADDLE.dec"].ShouldBe(["ButtonB"]);
        // ...and the port itself still reaching both, via the control rather than a union
        mapping.ButtonToInput["P1_PADDLE"].ShouldBe(["ButtonA", "ButtonB"]);
    }

    [Fact]
    public void Load_PaddleNudgedByTheXAxisEnds_ThePortOwnsTheStick()
    {
        // spacwalk's shape, and MAME's own default for a nudged paddle: the stick's left and right
        // switches, one in each nudge. The port binds the stick, so it owns it -- in the reverse
        // lookup that picks the image as much as in the labels -- and the nudges reach their half
        // only derivedly, ahead of the port in Controllers.xml though they are
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Cabinet" default="true">
                <Control name="P1_PADDLE" mappings="P1_PADDLE.dec P1_PADDLE.inc" />
                <Mapping name="P1_PADDLE.dec" input="AxisLeftStickLeft" />
                <Mapping name="P1_PADDLE.inc" input="AxisLeftStickRight" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteMameMapping("""
            <JoycodeMapping>
              <Mapping joycode="JOYCODE_1_XAXIS_LEFT_SWITCH" axis="JOYCODE_1_XAXIS" half="dec" input="AxisLeftStickLeft" />
              <Mapping joycode="JOYCODE_1_XAXIS_RIGHT_SWITCH" axis="JOYCODE_1_XAXIS" half="inc" input="AxisLeftStickRight" />
            </JoycodeMapping>
            """);
        _dc.WriteMameCfg("spacwalk.cfg", """
            <mameconfig>
              <system name="spacwalk">
                <input>
                  <port type="P1_PADDLE">
                    <newseq type="standard">NONE</newseq>
                    <newseq type="increment">JOYCODE_1_XAXIS_RIGHT_SWITCH</newseq>
                    <newseq type="decrement">JOYCODE_1_XAXIS_LEFT_SWITCH</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        ResolvedMapping mapping = Build().Load(MameGame("spacwalk"));

        mapping.ButtonToInput["P1_PADDLE"].ShouldBe(["AxisLeftStickRight", "AxisLeftStickLeft"]);
        mapping.DerivedButtonToInput.ShouldBeDictionaryOf(
            ("P1_PADDLE.inc", ["AxisLeftStickRight"]),
            ("P1_PADDLE.dec", ["AxisLeftStickLeft"]));
        mapping.InputToButton["AxisLeftStickLeft"].ShouldBe("P1_PADDLE");
        mapping.InputToButton["AxisLeftStickRight"].ShouldBe("P1_PADDLE");
    }

    [Theory]
    // the plain axis: decrement is the negative end, which this file calls Up
    [InlineData("JOYCODE_1_YAXIS", "AxisLeftStickUp", "AxisLeftStickDown")]
    // and the reversed one declares the opposite halves, so the ends swap
    [InlineData("JOYCODE_1_YAXIS_REVERSE", "AxisLeftStickDown", "AxisLeftStickUp")]
    public void Load_AxisBoundWithNoNudges_EachHalfStillReachesItsOwnEndOfTheAxis(
        string axisJoycode, string expectedDec, string expectedInc)
    {
        // bm1stmix's shape again, but with the halves declared. A cabinet with real analogue
        // hardware binds no nudge buttons at all -- that is what nudges are for, the cabinets
        // lacking it -- so without reading the half off the axis, a label written for one
        // direction could only ever land on the minority of cfgs that nudge.
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Cabinet" default="true">
                <Control name="P1_DIAL" mappings="P1_DIAL.dec P1_DIAL.inc" />
                <Mapping name="P1_DIAL.dec" input="AxisLeftStickLeft" />
                <Mapping name="P1_DIAL.inc" input="AxisLeftStickRight" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteMameMapping($"""
            <JoycodeMapping>
              <Mapping joycode="{axisJoycode}" half="{(axisJoycode.EndsWith("_REVERSE") ? "inc" : "dec")}" input="AxisLeftStickUp" />
              <Mapping joycode="{axisJoycode}" half="{(axisJoycode.EndsWith("_REVERSE") ? "dec" : "inc")}" input="AxisLeftStickDown" />
            </JoycodeMapping>
            """);
        _dc.WriteMameCfg("bm1stmix.cfg", $"""
            <mameconfig>
              <system name="bm1stmix">
                <input>
                  <port type="P1_DIAL">
                    <newseq type="standard">{axisJoycode}</newseq>
                    <newseq type="increment">NONE</newseq>
                    <newseq type="decrement">NONE</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        ResolvedMapping mapping = Build().Load(MameGame("bm1stmix"));

        // each half lands on its own end of the axis the cfg actually named...
        mapping.ButtonToInput["P1_DIAL.dec"].ShouldBe([expectedDec]);
        mapping.ButtonToInput["P1_DIAL.inc"].ShouldBe([expectedInc]);
        // ...the port itself still reaching the whole axis
        mapping.ButtonToInput["P1_DIAL"].ShouldBe(["AxisLeftStickUp", "AxisLeftStickDown"]);
        // ...and the baseline's horizontal guess is still gone: the halves come from the axis
        // this cfg names, never from the one Controllers.xml assumed
        mapping.InputToButton.ContainsKey("AxisLeftStickLeft").ShouldBeFalse();
        mapping.InputToButton.ContainsKey("AxisLeftStickRight").ShouldBeFalse();
    }

    [Fact]
    public void Load_CfgMovesTheAxisAndBindsNoNudges_BaselineHalvesDoNotSurvive()
    {
        // the theory above, with the one thing it relies on taken away: this mapping states no
        // halves, so there is no end of the axis to read the unbound nudges off. The baseline
        // must still be dropped -- that is the part that does not depend on knowing the sign.
        // Without it the port would reach the axis the cfg gave it AND the one Controllers.xml
        // guessed, lighting all four directions.
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Cabinet" default="true">
                <Control name="P1_DIAL" mappings="P1_DIAL.dec P1_DIAL.inc" />
                <Mapping name="P1_DIAL.dec" input="AxisLeftStickLeft" />
                <Mapping name="P1_DIAL.inc" input="AxisLeftStickRight" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteMameMapping("""
            <JoycodeMapping>
              <Mapping joycode="JOYCODE_1_YAXIS" input="AxisLeftStickUp" />
              <Mapping joycode="JOYCODE_1_YAXIS" input="AxisLeftStickDown" />
            </JoycodeMapping>
            """);
        _dc.WriteMameCfg("bm1stmix.cfg", """
            <mameconfig>
              <system name="bm1stmix">
                <input>
                  <port type="P1_DIAL">
                    <newseq type="standard">JOYCODE_1_YAXIS</newseq>
                    <newseq type="increment">NONE</newseq>
                    <newseq type="decrement">NONE</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        ResolvedMapping mapping = Build().Load(MameGame("bm1stmix"));

        // the vertical axis the cfg actually named, and nothing else
        mapping.ButtonToInput["P1_DIAL"].ShouldBe(["AxisLeftStickUp", "AxisLeftStickDown"]);
        // the baseline's horizontal guess is gone from the halves entirely -- an override with
        // nothing in it produces no entry at all, rather than an empty one -- so the control
        // finds no bound member and adds nothing
        mapping.ButtonToInput.ContainsKey("P1_DIAL.dec").ShouldBeFalse();
        mapping.ButtonToInput.ContainsKey("P1_DIAL.inc").ShouldBeFalse();
        mapping.InputToButton.ContainsKey("AxisLeftStickLeft").ShouldBeFalse();
        mapping.InputToButton.ContainsKey("AxisLeftStickRight").ShouldBeFalse();
    }

    // ---- single-player games borrowing a P2 slot for a second P1 axis (#16) ----

    [Fact]
    public void Scenario_Hwchamp_BorrowedP2SlotForASecondLever_RecognizedUnderItsOwnKey()
    {
        // hwchamp's real cfg shape: a single-player boxing game with two analogue levers, one per
        // fist. MAME represents the second lever as a P2_AD_STICK_Z port, but the joycode is still
        // player 1's own controller (JOYCODE_1_RZAXIS) -- there is no second player anywhere in
        // this cfg. P1_AD_STICK_X/Z stay distinct from P2_AD_STICK_Z, so recognizing the borrowed
        // slot doesn't collide with or overwrite either genuine P1 lever.
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Cabinet" default="true">
                <Mapping name="P1_AD_STICK_X" input="AxisLeftStickLeft" />
                <Mapping name="P1_AD_STICK_Z" input="AxisRightStickUp" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteMameMapping("""
            <JoycodeMapping>
              <Mapping joycode="JOYCODE_1_XAXIS" input="AxisLeftStickLeft" />
              <Mapping joycode="JOYCODE_1_XAXIS" input="AxisLeftStickRight" />
              <Mapping joycode="JOYCODE_1_YAXIS" input="AxisLeftStickUp" />
              <Mapping joycode="JOYCODE_1_YAXIS" input="AxisLeftStickDown" />
              <Mapping joycode="JOYCODE_1_RZAXIS" input="AxisRightStickUp" />
              <Mapping joycode="JOYCODE_1_RZAXIS" input="AxisRightStickDown" />
            </JoycodeMapping>
            """);
        _dc.WriteMameCfg("hwchamp.cfg", """
            <mameconfig>
              <system name="hwchamp">
                <input>
                  <port type="P1_AD_STICK_Z"><newseq type="standard">JOYCODE_1_YAXIS</newseq></port>
                  <port type="P1_AD_STICK_X"><newseq type="standard">JOYCODE_1_XAXIS</newseq></port>
                  <port type="P2_AD_STICK_Z"><newseq type="standard">JOYCODE_1_RZAXIS</newseq></port>
                </input>
              </system>
            </mameconfig>
            """);

        ResolvedMapping mapping = Build().Load(MameGame("hwchamp"));

        mapping.ButtonToInput["P1_AD_STICK_X"].ShouldBe(["AxisLeftStickLeft", "AxisLeftStickRight"]);
        mapping.ButtonToInput["P1_AD_STICK_Z"].ShouldBe(["AxisLeftStickUp", "AxisLeftStickDown"]);
        // The second (borrowed-slot) lever -- previously dropped entirely, now its own key.
        mapping.ButtonToInput["P2_AD_STICK_Z"].ShouldBe(["AxisRightStickUp", "AxisRightStickDown"]);
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

        mapping.ButtonToInput["P1_BUTTON1"].ShouldBe(["ButtonB"]);
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
        mapping.ButtonToInput["P1_BUTTON1"].ShouldBe(["ButtonX"]);
    }

    // ---- no overrides → baseline passes through unchanged ----

    [Fact]
    public void Load_NoCfgFiles_TransformReturnsNull_BaselineUnchanged()
    {
        WriteBaseline();
        WriteJoycodeMapping();

        ResolvedMapping mapping = Build().Load(MameGame("dkong"));

        // No cfg → no transform → active map equals the natural baseline. Compared entry by
        // entry rather than whole: resolving a control rebuilds both maps, so their lists are
        // equal in content but no longer the same instances, and List<string> equality is by
        // reference.
        mapping.ButtonToInput["P1_BUTTON3"].ShouldBe(["ButtonX"]);
        mapping.ButtonToInput.Keys.ShouldBe(mapping.NaturalButtonToInput.Keys, ignoreOrder: true);
        foreach ((string button, IReadOnlyList<string> inputs) in mapping.ButtonToInput)
            inputs.ShouldBe(mapping.NaturalButtonToInput[button]);
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

        mapping.ButtonToInput["P1_BUTTON3"].ShouldBe(["ButtonX"]);
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

        mapping.ButtonToInput["P1_BUTTON3"].ShouldBe(["ButtonX"]);
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
        mapping.ButtonToInput["P1_BUTTON1"].ShouldBe(["ButtonB"]);
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
        mapping.ButtonToInput["P1_BUTTON3"].ShouldBe(["ButtonY"]);
        mapping.InputToButton["ButtonY"].ShouldBe("P1_BUTTON3");
        // ...but the Naturals re-spliced from the pre-transform baseline still record ButtonX,
        // so IsMapped/remap detection can tell ButtonX's physical button is no longer in play.
        mapping.NaturalButtonToInput["P1_BUTTON3"].ShouldBe(["ButtonX"]);
        mapping.NaturalInputToButton["ButtonX"].ShouldBe("P1_BUTTON3");
        mapping.InputToButton.ContainsKey("ButtonX").ShouldBeFalse();
    }
}
