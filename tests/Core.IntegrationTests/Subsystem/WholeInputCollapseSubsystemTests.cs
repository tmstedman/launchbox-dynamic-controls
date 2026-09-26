using DynamicControls.Composition;
using DynamicControls.Config;
using DynamicControls.InputMapping;
using DynamicControls.Labels;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;

namespace DynamicControls.Core.IntegrationTests.Subsystem;

/// <summary>
/// Verifies the collapse interaction between two subsystems that each have their own suite in
/// isolation: <see cref="WholeInputDeriver"/> (extends a whole-naming platform button onto each
/// individual direction its siblings currently reach, alongside its existing whole-level claim)
/// feeding into <see cref="InputLabelsService"/>'s final collapse pass (folds four agreeing
/// direction labels back onto their whole; leaves a genuine disagreement rendered per-direction).
///
/// <see cref="InputMappingSubsystemTests"/> covers <see cref="WholeInputDeriver"/>'s own output
/// shape; <see cref="InputLabelsSubsystemTests"/> deliberately supplies its
/// <see cref="ResolvedMapping"/> directly rather than wiring a real
/// <see cref="InputMappingService"/> in. Neither exercises what the real mapping service's output
/// does once it reaches the real labels service — specifically the case
/// <see cref="WholeInputDeriver"/> alone could never resolve: a config that moves one direction
/// off the whole control entirely and onto an ordinary button, which needs the labels layer's
/// own per-direction rank comparison, not anything the mapping layer computes.
/// </summary>
public class WholeInputCollapseSubsystemTests
{
    private static readonly string RootDir = Path.DirectorySeparatorChar + "dc";
    private readonly MockDynamicControlsFilesystem _dc = new(RootDir);

    private ResolvedMapping LoadMapping(IInputMappingTransform? transform, GameInfo game) =>
        InputMappingFactory.Create(_dc.Lfs, _dc.Fs, new NullLogger(), transforms: transform == null ? [] : [transform])
            .Load(game);

    private ResolvedLabels LoadLabels(GameInfo game, ResolvedMapping mapping) =>
        InputLabelsFactory.Create(_dc.Lfs, new NullLogger(), config: new GlobalConfig())
            .Load(game, mapping);

    [Fact]
    public void OneDirectionSwappedOntoAnOrdinaryButton_ThatWholeRendersPerDirection()
    {
        // The MAME shape: every P1_JOYSTICK_* port bound to the hat OR the matching axis, so
        // normally all four directions would drive the Dpad and the stick together and JOYSTICK's
        // "Move" label would collapse onto both wholes -- WholeInputDeriver's own suite already
        // covers that. Here the config instead moves JOYSTICK_UP off the Dpad entirely and onto
        // an ordinary button's generic input, the same shape a swapped BUTTON2 binding produces.
        // WholeInputDeriver has no way to see this -- it only ever asks "did every direction
        // leave," never "does the direction that stayed still agree with the others' text." That
        // question belongs to the labels layer's own rank comparison and final collapse.
        _dc.WritePlatform("Arcade", """
            <Controllers>
              <Controller name="Cabinet" default="true">
                <Mapping name="JOYSTICK" input="ButtonDpad" />
                <Mapping name="JOYSTICK_UP" input="ButtonDpadUp" />
                <Mapping name="JOYSTICK_DOWN" input="ButtonDpadDown" />
                <Mapping name="JOYSTICK_LEFT" input="ButtonDpadLeft" />
                <Mapping name="JOYSTICK_RIGHT" input="ButtonDpadRight" />
                <Mapping name="BUTTON2" input="ButtonA" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteGameLabels("Arcade", "3on3dunk", """
            <InputLabels>
              <Input name="JOYSTICK">Move</Input>
              <Input name="BUTTON2">Jump</Input>
            </InputLabels>
            """);
        var transform = new StubMappingTransform((_, baseline) => MappingConfig(
            controller: baseline.Controller,
            analogToDigital: baseline.AnalogToDigital,
            mappings:
            [
                .. baseline.Mappings.Where(m => m.Name is not ("JOYSTICK_UP" or "BUTTON2"))
                    .Select(m => (m.Name, m.Input)),
                ("JOYSTICK_DOWN", "AxisLeftStickDown"),
                ("JOYSTICK_LEFT", "AxisLeftStickLeft"),
                ("JOYSTICK_RIGHT", "AxisLeftStickRight"),
                // UP's Dpad target is swapped away -- BUTTON2 (a real, independently labelled
                // button) takes over that exact position -- but its stick target is untouched,
                // same as the other three directions: the swap only ever displaces the digital
                // (hat) half of the OR-chain, never the axis half.
                ("JOYSTICK_UP", "AxisLeftStickUp"),
                ("BUTTON2", "ButtonDpadUp"),
            ]));
        GameInfo game = Game(platform: "Arcade", romName: "3on3dunk");

        ResolvedMapping mapping = LoadMapping(transform, game);
        ResolvedLabels labels = LoadLabels(game, mapping);

        // BUTTON2's direct claim on ButtonDpadUp outranks whatever JOYSTICK's derivation reached
        // there, so the four Dpad directions no longer all agree -- collapse refuses to fire and
        // each direction renders its own, correct text instead of "Move" papering over "Jump".
        labels.LabelText["ButtonDpadUp"].ShouldBe("Jump");
        labels.LabelText["ButtonDpadDown"].ShouldBe("Move");
        labels.LabelText["ButtonDpadLeft"].ShouldBe("Move");
        labels.LabelText["ButtonDpadRight"].ShouldBe("Move");

        // The left stick is untouched by the swap -- all four of its directions still agree, so
        // the whole also reads "Move" exactly as WholeInputDeriver's own suite expects, alongside
        // each individual direction (kept, not removed, so the layout's Condition can still tell
        // "all four agree" apart from a partial subset).
        labels.LabelText["AxisLeftStick"].ShouldBe("Move");
        labels.LabelText["AxisLeftStickUp"].ShouldBe("Move");
    }

    [Fact]
    public void EveryDirectionAgrees_CollapsesOntoBothWholes_ThroughTheRealPipeline()
    {
        // The ordinary case, through the same two real services, as a baseline against the swap
        // scenario above -- confirms the collapse fires when nothing has disturbed any direction.
        _dc.WritePlatform("Arcade", """
            <Controllers>
              <Controller name="Cabinet" default="true">
                <Mapping name="JOYSTICK" input="ButtonDpad" />
                <Mapping name="JOYSTICK_UP" input="ButtonDpadUp" />
                <Mapping name="JOYSTICK_DOWN" input="ButtonDpadDown" />
                <Mapping name="JOYSTICK_LEFT" input="ButtonDpadLeft" />
                <Mapping name="JOYSTICK_RIGHT" input="ButtonDpadRight" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteGameLabels("Arcade", "3on3dunk", """
            <InputLabels>
              <Input name="JOYSTICK">Move</Input>
            </InputLabels>
            """);
        var transform = new StubMappingTransform((_, baseline) => MappingConfig(
            controller: baseline.Controller,
            analogToDigital: baseline.AnalogToDigital,
            mappings:
            [
                .. baseline.Mappings.Select(m => (m.Name, m.Input)),
                ("JOYSTICK_UP", "AxisLeftStickUp"),
                ("JOYSTICK_DOWN", "AxisLeftStickDown"),
                ("JOYSTICK_LEFT", "AxisLeftStickLeft"),
                ("JOYSTICK_RIGHT", "AxisLeftStickRight"),
            ]));
        GameInfo game = Game(platform: "Arcade", romName: "3on3dunk");

        ResolvedMapping mapping = LoadMapping(transform, game);
        ResolvedLabels labels = LoadLabels(game, mapping);

        labels.LabelText.ShouldBeDictionaryOf(
            ("ButtonDpad", "Move"), ("AxisLeftStick", "Move"),
            ("ButtonDpadUp", "Move"), ("AxisLeftStickUp", "Move"),
            ("ButtonDpadDown", "Move"), ("AxisLeftStickDown", "Move"),
            ("ButtonDpadLeft", "Move"), ("AxisLeftStickLeft", "Move"),
            ("ButtonDpadRight", "Move"), ("AxisLeftStickRight", "Move"));
    }
}
