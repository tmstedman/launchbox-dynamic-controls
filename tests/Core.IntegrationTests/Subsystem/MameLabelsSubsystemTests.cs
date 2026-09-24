using DynamicControls.Composition;
using DynamicControls.Config;
using DynamicControls.InputMapping;
using DynamicControls.Labels;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;

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

        // All four right-stick directions agree, so they collapse onto the whole control.
        labels.LabelText["AxisLeftStick"].ShouldBe("Move Crosshair");
        // Nothing claims the left stick at all -- a static JOYSTICKLEFT fallback would have shown
        // "Move" here regardless, which is exactly the stale label this cfg no longer produces.
        labels.LabelText.ContainsKey("AxisRightStick").ShouldBeFalse();
        labels.LabelText.ContainsKey("AxisRightStickLeft").ShouldBeFalse();
        labels.LabelText.ContainsKey("AxisRightStickUp").ShouldBeFalse();
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

        labels.LabelText["AxisLeftStick"].ShouldBe("TrackBall Movement");
    }
}
