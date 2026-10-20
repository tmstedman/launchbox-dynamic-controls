using DynamicControls.Composition;
using DynamicControls.Config;
using DynamicControls.InputMapping;
using DynamicControls.Labels;
using DynamicControls.Rendering;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;
using static DynamicControls.Core.TestHelpers.Rendering.RenderingFixtures;

namespace DynamicControls.Core.IntegrationTests.Subsystem;

/// <summary>
/// Verifies the collapse interaction across three subsystems that each have their own suite in
/// isolation: <see cref="ControlResolver"/> (a platform button standing for several reaches
/// whatever they currently reach), feeding <see cref="InputLabelsService"/>'s per-direction rank
/// comparison, feeding <see cref="WholeLabelCollapser"/> (folds agreeing direction labels back
/// onto their composite; leaves a genuine disagreement rendered per-direction).
///
/// <see cref="InputMappingSubsystemTests"/> covers the control resolver's own output shape;
/// <see cref="InputLabelsSubsystemTests"/> deliberately supplies its
/// <see cref="ResolvedMapping"/> directly rather than wiring a real
/// <see cref="InputMappingService"/> in. Neither exercises what the real mapping service's output
/// does once it reaches the real labels service — specifically the case an control alone could
/// never resolve: a config that moves one direction off the composite entirely and onto an
/// ordinary button, which needs the labels layer's own per-direction rank comparison, not
/// anything the mapping layer computes.
///
/// The composites are passed to the collapser here the way a template declares them in its
/// <c>&lt;Head&gt;</c>, since that is where the relation lives now.
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

    /// <summary>Applies the real collapser with the composites the shipped template declares.</summary>
    private static IReadOnlyDictionary<string, string> Collapse(ResolvedLabels labels) =>
        new WholeLabelCollapser(new NullLogger()).Collapse(labels.LabelText, StandardWholes);

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
                <Control name="JOYSTICK" mappings="JOYSTICK_UP JOYSTICK_DOWN JOYSTICK_LEFT JOYSTICK_RIGHT" />
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
        IReadOnlyDictionary<string, string> collapsed = Collapse(labels);

        // BUTTON2's direct claim on ButtonDpadUp outranks whatever the control reached there, so
        // the four Dpad directions no longer all agree -- collapse refuses to fire and each
        // direction renders its own, correct text instead of "Move" papering over "Jump".
        collapsed["ButtonDpadUp"].ShouldBe("Jump");
        collapsed["ButtonDpadDown"].ShouldBe("Move");
        collapsed["ButtonDpadLeft"].ShouldBe("Move");
        collapsed["ButtonDpadRight"].ShouldBe("Move");
        collapsed.ContainsKey("ButtonDpad").ShouldBeFalse();

        // The left stick is untouched by the swap -- all four of its directions still agree, so
        // the composite also reads "Move", alongside each individual direction (kept, not
        // removed, so the layout's Condition can still tell "all four agree" apart from a
        // partial subset).
        collapsed["AxisLeftStick"].ShouldBe("Move");
        collapsed["AxisLeftStickUp"].ShouldBe("Move");
    }

    [Fact]
    public void EveryDirectionAgrees_CollapsesOntoBothWholes_ThroughTheRealPipeline()
    {
        // The ordinary case, through the same two real services, as a baseline against the swap
        // scenario above -- confirms the collapse fires when nothing has disturbed any direction.
        _dc.WritePlatform("Arcade", """
            <Controllers>
              <Controller name="Cabinet" default="true">
                <Control name="JOYSTICK" mappings="JOYSTICK_UP JOYSTICK_DOWN JOYSTICK_LEFT JOYSTICK_RIGHT" />
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

        Collapse(labels).ShouldBeDictionaryOf(
            ("ButtonDpad", "Move"), ("AxisLeftStick", "Move"),
            ("ButtonDpadUp", "Move"), ("AxisLeftStickUp", "Move"),
            ("ButtonDpadDown", "Move"), ("AxisLeftStickDown", "Move"),
            ("ButtonDpadLeft", "Move"), ("AxisLeftStickLeft", "Move"),
            ("ButtonDpadRight", "Move"), ("AxisLeftStickRight", "Move"));
    }
}
