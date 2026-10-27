using DynamicControls.Composition;
using DynamicControls.Config;
using DynamicControls.InputMapping;
using DynamicControls.Labels;
using DynamicControls.Rendering;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;
using static DynamicControls.Core.TestHelpers.Rendering.RenderingFixtures;

namespace DynamicControls.Core.IntegrationTests.Subsystem;

/// <summary>
/// Verifies that a label named after an analog-stick platform button (<c>AD_STICK_X</c>,
/// <c>AD_STICK_Y</c>, ...) follows wherever the game's own MAME cfg sends it, rather than sitting
/// on that button's static <c>Controllers.xml</c> default. <see cref="MameInputMappingSubsystemTests"/>
/// already proves the mapping layer does this; this suite is the one place the real MAME cfg
/// pipeline (<see cref="Plugins.Mame.MameInputMappingSource"/> → <see cref="Plugins.Mame.MameCfgLoader"/>
/// → <see cref="Plugins.Mame.JoycodeMappingLoader"/>) is wired into a real <see cref="InputLabelsService"/>
/// — <see cref="WholeInputCollapseSubsystemTests"/> covers the same seam but with a
/// <c>StubMappingTransform</c> standing in for MAME.
///
/// This is the case that motivated dropping the redundant <c>JOYSTICKLEFT</c>/<c>JOYSTICKRIGHT</c>
/// fallback entries from the Arcade label pack: that fallback is a static baseline claim on
/// <c>AxisLeftStick</c>/<c>AxisRightStick</c> that never changes, so it would keep showing a label on
/// whichever stick <em>this pack's own reference cfg</em> happened to use — wrong for any other
/// player whose cfg sends the same physical control somewhere else. Naming the real port
/// (<c>AD_STICK_X</c>/<c>AD_STICK_Y</c>) instead means the label tracks the actual override.
/// </summary>
public class MameLabelsSubsystemTests
{
    private const string Platform = "Arcade";
    private static readonly string RootDir = Path.DirectorySeparatorChar + "dc";
    private static readonly string MamePath = Path.Combine(RootDir, "Emulators", "mame", "mame64.exe");

    private readonly MockDynamicControlsFilesystem _dc = new(RootDir);

    private ResolvedMapping LoadMapping(GameInfo game) =>
        InputMappingFactory.Create(_dc.Lfs, _dc.Fs, new NullLogger(),
            config: new GlobalConfig { EnableMame = true }, sources: []).Load(game);

    private ResolvedLabels LoadLabels(GameInfo game, ResolvedMapping mapping) =>
        InputLabelsFactory.Create(_dc.Lfs, new NullLogger(), config: new GlobalConfig()).Load(game, mapping);

    /// <summary>Applies the real collapser with the composites the shipped template declares —
    /// folding agreeing directions onto their composite is a render-time concern now.</summary>
    private static IReadOnlyDictionary<string, string> Collapse(ResolvedLabels labels) =>
        new WholeLabelCollapser(new NullLogger()).Collapse(labels.LabelText, StandardWholes);

    [Fact]
    public void Load_CfgRemapsAdStickToTheRightStick_LabelFollowsItThereNotTheStaticLeftStickDefault()
    {
        // Baseline: AD_STICK_X/Y default to the LEFT stick, same as production Controllers.xml.
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Cabinet" default="true">
                <Mapping name="P1_AD_STICK_X" input="AxisRightStickLeft" />
                <Mapping name="P1_AD_STICK_X" input="AxisRightStickRight" />
                <Mapping name="P1_AD_STICK_Y" input="AxisRightStickUp" />
                <Mapping name="P1_AD_STICK_Y" input="AxisRightStickDown" />
              </Controller>
            </Controllers>
            """);
        // This player's cfg reads the same bare axis codes as RIGHT-stick input instead --
        // a different physical assignment than whatever the reference cfg used to transcribe it.
        _dc.WriteMameMapping("""
            <JoycodeMapping>
              <Mapping joycode="JOYCODE_1_XAXIS" input="AxisLeftStickLeft" />
              <Mapping joycode="JOYCODE_1_XAXIS" input="AxisLeftStickRight" />
              <Mapping joycode="JOYCODE_1_YAXIS" input="AxisLeftStickUp" />
              <Mapping joycode="JOYCODE_1_YAXIS" input="AxisLeftStickDown" />
            </JoycodeMapping>
            """);
        _dc.WriteGameLabels(Platform, "revx", """
            <InputLabels>
              <Input name="P1_AD_STICK_X">Move Crosshair</Input>
              <Input name="P1_AD_STICK_Y">Move Crosshair</Input>
            </InputLabels>
            """);
        _dc.WriteMameCfg("revx.cfg", """
            <mameconfig>
              <system name="revx">
                <input>
                  <port type="P1_AD_STICK_X"><newseq type="standard">JOYCODE_1_XAXIS</newseq></port>
                  <port type="P1_AD_STICK_Y"><newseq type="standard">JOYCODE_1_YAXIS</newseq></port>
                </input>
              </system>
            </mameconfig>
            """);
        GameInfo game = Game(platform: Platform, romName: "revx", emulatorPath: MamePath);

        ResolvedMapping mapping = LoadMapping(game);
        ResolvedLabels labels = LoadLabels(game, mapping);
        IReadOnlyDictionary<string, string> collapsed = Collapse(labels);

        // All four right-stick directions agree, so they collapse onto the whole control.
        collapsed["AxisLeftStick"].ShouldBe("Move Crosshair");
        // Nothing claims the left stick at all -- a static JOYSTICKLEFT fallback would have shown
        // "Move" here regardless, which is exactly the stale label this cfg no longer produces.
        collapsed.ContainsKey("AxisRightStick").ShouldBeFalse();
        collapsed.ContainsKey("AxisRightStickLeft").ShouldBeFalse();
        collapsed.ContainsKey("AxisRightStickUp").ShouldBeFalse();
    }

    [Fact]
    public void Load_PortTypeRepeatedOnOneTag_EachPortIsLabelledByItsOwnName()
    {
        // escounts: three OTHER ports on one tag, told apart only by mask. The last keeps the bare
        // name, and the earlier two are labelled under their qualified names
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Cabinet" default="true" />
            </Controllers>
            """);
        _dc.WriteMameMapping("""
            <JoycodeMapping>
              <Mapping joycode="JOYCODE_1_BUTTON5" input="ButtonLeftShoulder" />
              <Mapping joycode="JOYCODE_1_BUTTON6" input="ButtonRightShoulder" />
              <Mapping joycode="JOYCODE_1_SLIDER2_NEG_SWITCH" input="AxisTriggerRight" />
            </JoycodeMapping>
            """);
        _dc.WriteGameLabels(Platform, "escounts", """
            <InputLabels>
              <Input name="OTHER:STROBE1:4">Collect</Input>
              <Input name="OTHER:STROBE1:2">Continue</Input>
              <Input name="OTHER">Bonus</Input>
            </InputLabels>
            """);
        _dc.WriteMameCfg("escounts.cfg", """
            <mameconfig>
              <system name="escounts">
                <input>
                  <port tag=":STROBE1" type="OTHER" mask="2"><newseq type="standard">JOYCODE_1_BUTTON6</newseq></port>
                  <port tag=":STROBE1" type="OTHER" mask="4"><newseq type="standard">JOYCODE_1_BUTTON5</newseq></port>
                  <port tag=":STROBE1" type="OTHER" mask="16"><newseq type="standard">JOYCODE_1_SLIDER2_NEG_SWITCH</newseq></port>
                </input>
              </system>
            </mameconfig>
            """);
        GameInfo game = Game(platform: Platform, romName: "escounts", emulatorPath: MamePath);

        ResolvedLabels labels = LoadLabels(game, LoadMapping(game));

        labels.LabelText["ButtonLeftShoulder"].ShouldBe("Collect");
        labels.LabelText["ButtonRightShoulder"].ShouldBe("Continue");
        labels.LabelText["AxisTriggerRight"].ShouldBe("Bonus");
    }

    [Fact]
    public void Load_PortNudgedByOtherControls_EachSequenceCarriesItsOwnLabel()
    {
        // airco22b's shape, and the reason the cfg loader stopped unioning a port's sequences:
        // the throttle is read from an analogue axis AND nudged by the two triggers, which are a
        // different physical control. The artwork names all three separately, and one platform
        // button carries one label, so unioning them made the port say one word about three
        // controls.
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Cabinet" default="true">
                <Control name="P1_AD_STICK_Z" mappings="P1_AD_STICK_Z.dec P1_AD_STICK_Z.inc" />
                <Mapping name="P1_AD_STICK_Z.dec" input="AxisRightStickUp" />
                <Mapping name="P1_AD_STICK_Z.inc" input="AxisRightStickDown" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteMameMapping("""
            <JoycodeMapping>
              <Mapping joycode="JOYCODE_1_RZAXIS" input="AxisRightStickUp" />
              <Mapping joycode="JOYCODE_1_RZAXIS" input="AxisRightStickDown" />
              <Mapping joycode="JOYCODE_1_SLIDER1_NEG_SWITCH" input="AxisTriggerLeft" />
              <Mapping joycode="JOYCODE_1_SLIDER2_NEG_SWITCH" input="AxisTriggerRight" />
            </JoycodeMapping>
            """);
        _dc.WriteGameLabels(Platform, "airco22b", """
            <InputLabels>
              <Input name="P1_AD_STICK_Z">Throttle</Input>
              <Input name="P1_AD_STICK_Z.inc">Throttle Down</Input>
              <Input name="P1_AD_STICK_Z.dec">Throttle Up</Input>
            </InputLabels>
            """);
        _dc.WriteMameCfg("airco22b.cfg", """
            <mameconfig>
              <system name="airco22b">
                <input>
                  <port type="P1_AD_STICK_Z">
                    <newseq type="standard">JOYCODE_1_RZAXIS</newseq>
                    <newseq type="increment">JOYCODE_1_SLIDER1_NEG_SWITCH</newseq>
                    <newseq type="decrement">JOYCODE_1_SLIDER2_NEG_SWITCH</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);
        GameInfo game = Game(platform: Platform, romName: "airco22b", emulatorPath: MamePath);

        ResolvedLabels labels = LoadLabels(game, LoadMapping(game));

        // The nudges reach the triggers and say their own words -- the port's own label does not
        // smear across them, even though the control makes the port reach them too. Each nudge
        // claims its generic more directly than the port does, which is what settles it.
        labels.LabelText["AxisTriggerLeft"].ShouldBe("Throttle Down");
        labels.LabelText["AxisTriggerRight"].ShouldBe("Throttle Up");
        // ...and the axis itself still carries the port's own word.
        Collapse(labels)["AxisRightStick"].ShouldBe("Throttle");
    }

    [Fact]
    public void Load_PortNudgedButOnlyThePortLabelled_ThatLabelStillReachesTheNudges()
    {
        // The common case, and what the <Control> over the pair is for: a game that draws no
        // distinction names only the port, and must still label everything the port drives --
        // otherwise splitting the sequences would have cost a label on every game that never
        // wanted the halves separated.
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
              <Mapping joycode="JOYCODE_1_XAXIS" input="AxisLeftStickLeft" />
              <Mapping joycode="JOYCODE_1_XAXIS" input="AxisLeftStickRight" />
              <Mapping joycode="JOYCODE_1_HAT1LEFT" input="ButtonDpadLeft" />
              <Mapping joycode="JOYCODE_1_HAT1RIGHT" input="ButtonDpadRight" />
            </JoycodeMapping>
            """);
        _dc.WriteGameLabels(Platform, "arknoid2", """
            <InputLabels>
              <Input name="P1_DIAL">Move</Input>
            </InputLabels>
            """);
        _dc.WriteMameCfg("arknoid2.cfg", """
            <mameconfig>
              <system name="arknoid2">
                <input>
                  <port type="P1_DIAL">
                    <newseq type="standard">JOYCODE_1_XAXIS</newseq>
                    <newseq type="increment">JOYCODE_1_HAT1RIGHT</newseq>
                    <newseq type="decrement">JOYCODE_1_HAT1LEFT</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);
        GameInfo game = Game(platform: Platform, romName: "arknoid2", emulatorPath: MamePath);

        ResolvedLabels labels = LoadLabels(game, LoadMapping(game));

        // the axis the port is read from...
        labels.LabelText["AxisLeftStickLeft"].ShouldBe("Move");
        labels.LabelText["AxisLeftStickRight"].ShouldBe("Move");
        // ...and the d-pad it is nudged by, reached only through the control
        labels.LabelText["ButtonDpadLeft"].ShouldBe("Move");
        labels.LabelText["ButtonDpadRight"].ShouldBe("Move");
    }

    [Fact]
    public void Load_LabelNamesTheRealPort_NotTheStaleCfgCapture_RemapStillFollowsIt()
    {
        // adillor's real control is a trackball, read through TRACKBALL_X/Y ports -- the
        // reference cfg this pack shipped with was simply captured wrong (it showed AD_STICK_X/Y
        // instead), and that wrong capture is why the label named AD_STICK_X/Y before both the
        // cfg and the label got corrected to TRACKBALL_X/Y. This is the regression guard for the
        // corrected data: labelling the real port tracks a remap correctly.
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Cabinet" default="true">
                <Mapping name="P1_TRACKBALL_X" input="AxisLeftStickLeft" />
                <Mapping name="P1_TRACKBALL_Y" input="AxisLeftStickUp" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteMameMapping("""
            <JoycodeMapping>
              <Mapping joycode="JOYCODE_2_XAXIS" input="AxisLeftStickLeft" />
              <Mapping joycode="JOYCODE_2_XAXIS" input="AxisLeftStickRight" />
              <Mapping joycode="JOYCODE_2_YAXIS" input="AxisLeftStickUp" />
              <Mapping joycode="JOYCODE_2_YAXIS" input="AxisLeftStickDown" />
            </JoycodeMapping>
            """);
        _dc.WriteGameLabels(Platform, "adillor", """
            <InputLabels>
              <Input name="P1_TRACKBALL_X">TrackBall Movement</Input>
              <Input name="P1_TRACKBALL_Y">TrackBall Movement</Input>
            </InputLabels>
            """);
        _dc.WriteMameCfg("adillor.cfg", """
            <mameconfig>
              <system name="adillor">
                <input>
                  <port type="P1_TRACKBALL_X"><newseq type="standard">JOYCODE_2_XAXIS</newseq></port>
                  <port type="P1_TRACKBALL_Y"><newseq type="standard">JOYCODE_2_YAXIS</newseq></port>
                </input>
              </system>
            </mameconfig>
            """);
        GameInfo game = Game(platform: Platform, romName: "adillor", emulatorPath: MamePath);

        ResolvedMapping mapping = LoadMapping(game);
        ResolvedLabels labels = LoadLabels(game, mapping);
        IReadOnlyDictionary<string, string> collapsed = Collapse(labels);

        collapsed["AxisLeftStick"].ShouldBe("TrackBall Movement");
    }

    /// <summary>
    /// downtown's rotary: the right stick turns it by a switch at each end of its axis, so it
    /// keeps turning while held, and the triggers nudge it too. Both sit in the port's two nudge
    /// sequences together, so naming the sequences alone can't tell the stick from the triggers.
    /// </summary>
    private void WriteRotary(string labels)
    {
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Cabinet" default="true">
                <Control name="P1_POSITIONAL" mappings="P1_POSITIONAL.dec P1_POSITIONAL.inc" />
                <Mapping name="P1_POSITIONAL.dec" input="AxisLeftStickLeft" />
                <Mapping name="P1_POSITIONAL.inc" input="AxisLeftStickRight" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteMameMapping("""
            <JoycodeMapping>
              <Mapping joycode="JOYCODE_1_ZAXIS_NEG_SWITCH" axis="JOYCODE_1_ZAXIS" end="dec" input="AxisRightStickLeft" />
              <Mapping joycode="JOYCODE_1_ZAXIS_POS_SWITCH" axis="JOYCODE_1_ZAXIS" end="inc" input="AxisRightStickRight" />
              <Mapping joycode="JOYCODE_1_SLIDER1_NEG_SWITCH" input="AxisTriggerLeft" />
              <Mapping joycode="JOYCODE_1_SLIDER2_NEG_SWITCH" input="AxisTriggerRight" />
            </JoycodeMapping>
            """);
        _dc.WriteGameLabels(Platform, "downtown", labels);
        _dc.WriteMameCfg("downtown.cfg", """
            <mameconfig>
              <system name="downtown">
                <input>
                  <port type="P1_POSITIONAL">
                    <newseq type="standard">NONE</newseq>
                    <newseq type="increment">JOYCODE_1_ZAXIS_POS_SWITCH OR JOYCODE_1_SLIDER2_NEG_SWITCH</newseq>
                    <newseq type="decrement">JOYCODE_1_ZAXIS_NEG_SWITCH OR JOYCODE_1_SLIDER1_NEG_SWITCH</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);
    }

    [Fact]
    public void Load_StickNudgingBothWaysBesideTriggers_PortLabelsTheStick_NudgesLabelTheTriggers()
    {
        // The card says "Rotate" on the stick and names each trigger's turn. A switch at each end
        // of one axis is the stick standing in for the port's analogue binding, so the port's own
        // label is the stick's, and the nudge labels keep to the triggers.
        WriteRotary("""
            <InputLabels>
              <Input name="P1_POSITIONAL">Rotate</Input>
              <Input name="P1_POSITIONAL.dec">Rotate Left</Input>
              <Input name="P1_POSITIONAL.inc">Rotate Right</Input>
            </InputLabels>
            """);
        GameInfo game = Game(platform: Platform, romName: "downtown", emulatorPath: MamePath);

        ResolvedLabels labels = LoadLabels(game, LoadMapping(game));

        labels.LabelText["AxisTriggerLeft"].ShouldBe("Rotate Left");
        labels.LabelText["AxisTriggerRight"].ShouldBe("Rotate Right");
        Collapse(labels)["AxisRightStick"].ShouldBe("Rotate");
    }

    [Fact]
    public void Load_StickNudgingBothWaysBesideTriggers_OnlyNudgesLabelled_StickKeepsTheirLabels()
    {
        // With no label on the port there is nothing for the stick to take instead, so it keeps
        // what the nudges say about each half -- recognising the axis never costs a game a label.
        WriteRotary("""
            <InputLabels>
              <Input name="P1_POSITIONAL.dec">Rotate Left</Input>
              <Input name="P1_POSITIONAL.inc">Rotate Right</Input>
            </InputLabels>
            """);
        GameInfo game = Game(platform: Platform, romName: "downtown", emulatorPath: MamePath);

        ResolvedLabels labels = LoadLabels(game, LoadMapping(game));

        labels.LabelText.ShouldBeDictionaryOf(
            ("AxisRightStickLeft", "Rotate Left"),
            ("AxisRightStickRight", "Rotate Right"),
            ("AxisTriggerLeft", "Rotate Left"),
            ("AxisTriggerRight", "Rotate Right"));
    }
}
