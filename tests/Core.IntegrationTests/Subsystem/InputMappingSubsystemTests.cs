using DynamicControls.Composition;
using DynamicControls.Config;
using DynamicControls.InputMapping;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;

namespace DynamicControls.Core.IntegrationTests.Subsystem;

/// <summary>
/// Verifies the input-mapping pipeline with its real internal wiring intact:
/// <see cref="InputMappingLoader"/> (XML parsing) → <see cref="PerGameXmlMappingSource"/> /
/// <see cref="PlatformDefaultMappingSource"/> (source chain) → <see cref="InputMappingPlugins"/>
/// (priority + filtering) → <see cref="InputMappingResolver"/> (forward/reverse build +
/// <see cref="AnalogToDigitalMirror"/>) → <see cref="InputMappingService"/>
/// (orchestration + natural-map splicing). Each test stages Controllers.xml and per-game XMLs in
/// an in-memory <see cref="MockFileSystem"/> and asserts on the fully-resolved
/// <see cref="ResolvedMapping"/>. Stub <see cref="IInputMappingSource"/> /
/// <see cref="IInputMappingTransform"/> instances are injected when a test needs to exercise
/// the middle-priority position or transform-on-baseline behaviour without dragging in the
/// RetroArch or MAME plugin internals (each of which warrants its own subsystem suite).
/// </summary>
public class InputMappingSubsystemTests
{
    private const string Platform = "Sega Genesis";
    private static readonly string RootDir = Path.DirectorySeparatorChar + "dc";

    private readonly MockDynamicControlsFilesystem _dc = new(RootDir);

    // ---- factory helpers ----

    /// <summary>Uses the production factory so loader/resolver/plugins/service wiring under test
    /// matches what the rest of the system uses. <see cref="PerGameXmlMappingSource"/> and
    /// <see cref="PlatformDefaultMappingSource"/> are framework-fixed — the factory always wires
    /// them — so the suite only controls the middle (where RetroArch lives in production) and
    /// any transforms (where MAME lives in production).</summary>
    private InputMappingService Build(
        IInputMappingSource? source = null,
        IInputMappingTransform? transform = null) =>
        InputMappingFactory.Create(
            _dc.Lfs,
            _dc.Fs,
            new NullLogger(),
            sources: source == null ? [] : [source],
            transforms: transform == null ? [] : [transform]);

    // ---- source priority ----

    [Fact]
    public void Load_NoPlatformAndNoGame_ReturnsEmptyMapping()
    {
        // No Controllers.xml, no per-game XML, no middle source. Every source returns null;
        // the service falls back to an empty config rather than throwing.
        ResolvedMapping mapping = Build().Load(Game(platform: Platform));

        mapping.Platform.ShouldBe(Platform);
        mapping.Controller.ShouldBeNull();
        mapping.ButtonToInput.ShouldBeEmpty();
        mapping.InputToButton.ShouldBeEmpty();
    }

    [Fact]
    public void Load_PerGameXmlPresent_WinsOverMiddleAndPlatformDefault()
    {
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Pad" default="true">
                <Mapping name="A" input="ButtonA" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteGameMapping(Platform, "OutRun", """
            <GameMapping>
              <Mapping name="A" input="ButtonStart" />
            </GameMapping>
            """);
        var middle = new StubMappingSource(MappingConfig(mappings: [("A", "ButtonY")]));

        ResolvedMapping mapping = Build(middle).Load(Game(romName: "OutRun"));

        // Per-game override wins — A drives ButtonStart, not the platform default's ButtonA
        // nor the middle source's ButtonY.
        mapping.ButtonToInput["A"].ShouldBe(["ButtonStart"]);
    }

    [Fact]
    public void Load_PerGameXmlMissing_MiddleSourcePresent_WinsOverPlatformDefault()
    {
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Pad" default="true">
                <Mapping name="A" input="ButtonA" />
              </Controller>
            </Controllers>
            """);
        var middle = new StubMappingSource(MappingConfig(mappings: [("A", "ButtonY")]));

        ResolvedMapping mapping = Build(middle).Load(Game(romName: "OutRun"));

        mapping.ButtonToInput["A"].ShouldBe(["ButtonY"]);
    }

    [Fact]
    public void Load_PerGameXmlMissing_MiddleSourceNull_FallsThroughToPlatformDefault()
    {
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Pad" default="true">
                <Mapping name="A" input="ButtonA" />
              </Controller>
            </Controllers>
            """);
        var middle = new StubMappingSource(returns: null);

        ResolvedMapping mapping = Build(middle).Load(Game(romName: "OutRun"));

        mapping.ButtonToInput["A"].ShouldBe(["ButtonA"]);
        mapping.Controller.ShouldBe("Pad");
    }

    // ---- per-game overlay onto baseline controller ----

    [Fact]
    public void Load_PerGameMapping_ReplacesBaselineEntriesByName_PreservesUnrelatedNames()
    {
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Pad" default="true">
                <Mapping name="A" input="ButtonA" />
                <Mapping name="B" input="ButtonB" />
                <Mapping name="Start" input="ButtonStart" />
              </Controller>
            </Controllers>
            """);
        // Per-game overrides A; B and Start should survive from the baseline.
        _dc.WriteGameMapping(Platform, "OutRun", """
            <GameMapping>
              <Mapping name="A" input="ButtonY" />
            </GameMapping>
            """);

        ResolvedMapping mapping = Build().Load(Game(romName: "OutRun"));

        mapping.ButtonToInput.ShouldBeDictionaryOf(
            ("A", ["ButtonY"]),
            ("B", ["ButtonB"]),
            ("Start", ["ButtonStart"]));
    }

    [Fact]
    public void Load_PerGameUnmap_DropsBaselineEntryWithoutReplacement()
    {
        // <Unmap name="A" /> in the per-game XML removes A from the baseline mapping entirely —
        // parity with RetroArch's -1 sentinel. Other Names survive.
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Pad" default="true">
                <Mapping name="A" input="ButtonA" />
                <Mapping name="B" input="ButtonB" />
                <Mapping name="Start" input="ButtonStart" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteGameMapping(Platform, "OutRun", """
            <GameMapping>
              <Unmap name="A" />
            </GameMapping>
            """);

        ResolvedMapping mapping = Build().Load(Game(romName: "OutRun"));

        mapping.ButtonToInput.ShouldBeDictionaryOf(
            ("B", ["ButtonB"]),
            ("Start", ["ButtonStart"]));
        mapping.InputToButton.ShouldBeDictionaryOf(
            ("ButtonB", "B"),
            ("ButtonStart", "Start"));
    }

    // ---- AnalogToDigital cascade ----

    [Fact]
    public void Load_PerGameOmitsAnalogToDigital_InheritsFromBaselineController_AndMirrorApplied()
    {
        // Baseline controller declares analogToDigital="left"; per-game XML inherits it,
        // and the resolver's AnalogToDigitalMirror appends AxisLeftStickUp to the same
        // platform button that drives ButtonDpadUp.
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Pad" default="true" analogToDigital="left">
                <Mapping name="Up" input="ButtonDpadUp" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteGameMapping(Platform, "OutRun", """
            <GameMapping>
              <Mapping name="A" input="ButtonA" />
            </GameMapping>
            """);

        ResolvedMapping mapping = Build().Load(Game(romName: "OutRun"));

        mapping.AnalogToDigital.ShouldBe(AnalogToDigitalMode.Left);
        mapping.ButtonToInput["Up"].ShouldBe(["ButtonDpadUp", "AxisLeftStickUp"]);
    }

    // ---- controller selection ----

    [Fact]
    public void Load_PerGameSelectsNonDefaultController_OverlaysOnThatBaseline()
    {
        // 6-Button uses inheritFrom="Pad" so it prepends Pad's A mapping, then adds X.
        // The per-game override replaces A; X survives from the inherited baseline.
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Pad" default="true">
                <Mapping name="A" input="ButtonA" />
              </Controller>
              <Controller name="6-Button" inheritFrom="Pad">
                <Mapping name="X" input="ButtonX" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteGameMapping(Platform, "OutRun", """
            <GameMapping controller="6-Button">
              <Mapping name="A" input="ButtonY" />
            </GameMapping>
            """);

        ResolvedMapping mapping = Build().Load(Game(romName: "OutRun"));

        mapping.Controller.ShouldBe("6-Button");
        // 6-Button's resolved baseline (A from Pad + X own) is overlaid by the per-game mapping.
        // The exact-dict assertion also catches any accidental cross-controller bleed.
        mapping.ButtonToInput.ShouldBeDictionaryOf(
            ("A", ["ButtonY"]),
            ("X", ["ButtonX"]));
    }

    [Fact]
    public void Load_PerGameSelectsTransitivelyInheritingController_AccumulatesWholeChain()
    {
        // 6-Button inheritFrom 3-Button inheritFrom 2-Button. Selecting 6-Button must resolve the
        // whole chain through the real loader: I/II from 2-Button (grandparent), III from 3-Button
        // (parent), IV from 6-Button's own. This is the discriminator against one-level inheritance,
        // where the grandparent (2-Button) entries would be missing.
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="2-Button" default="true">
                <Mapping name="I" input="ButtonB" />
                <Mapping name="II" input="ButtonA" />
              </Controller>
              <Controller name="3-Button" inheritFrom="2-Button">
                <Mapping name="III" input="ButtonX" />
              </Controller>
              <Controller name="6-Button" inheritFrom="3-Button">
                <Mapping name="IV" input="ButtonY" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteGameMapping(Platform, "Fatal Fury", """
            <GameMapping controller="6-Button" />
            """);

        ResolvedMapping mapping = Build().Load(Game(romName: "Fatal Fury"));

        mapping.Controller.ShouldBe("6-Button");
        mapping.ButtonToInput.ShouldBeDictionaryOf(
            ("I", ["ButtonB"]),      // grandparent (2-Button)
            ("II", ["ButtonA"]),     // grandparent (2-Button)
            ("III", ["ButtonX"]),    // parent (3-Button)
            ("IV", ["ButtonY"]));    // own (6-Button)
    }

    [Fact]
    public void Load_PlatformInheritsSharedBaseFile_ResolvesThroughRealLoader()
    {
        // The platform's Controllers file is a one-line pointer at a shared base file; the base
        // holds the actual 2/3/6-Button chain. Selecting 6-Button must resolve root-level file
        // inheritFrom AND controller-level transitive inheritFrom (which crosses the file boundary)
        // through the real loader, accumulating every ancestor's mappings.
        _dc.WritePlatform("_NEC Pad", """
            <Controllers>
              <Controller name="2-Button" default="true">
                <Mapping name="I" input="ButtonB" />
                <Mapping name="II" input="ButtonA" />
              </Controller>
              <Controller name="3-Button" inheritFrom="2-Button">
                <Mapping name="III" input="ButtonX" />
              </Controller>
              <Controller name="6-Button" inheritFrom="3-Button">
                <Mapping name="IV" input="ButtonY" />
              </Controller>
            </Controllers>
            """);
        _dc.WritePlatform(Platform, """<Controllers inheritFrom="_NEC Pad" />""");
        _dc.WriteGameMapping(Platform, "Fatal Fury", """
            <GameMapping controller="6-Button" />
            """);

        ResolvedMapping mapping = Build().Load(Game(romName: "Fatal Fury"));

        mapping.Controller.ShouldBe("6-Button");
        mapping.ButtonToInput.ShouldBeDictionaryOf(
            ("I", ["ButtonB"]),
            ("II", ["ButtonA"]),
            ("III", ["ButtonX"]),
            ("IV", ["ButtonY"]));
    }

    [Fact]
    public void Load_PlatformAddsNewDefaultControllerOverBaseFile_OverridesInheritedDefault()
    {
        // The platform's own Controllers file inherits a shared base whose "Pad" is the default,
        // but also declares its own new controller also marked default (e.g. a CD add-on platform
        // introducing a controller the base platform never had). No per-game mapping selects a
        // controller explicitly, so PlatformDefaultMappingSource falls back to whichever controller
        // resolves as the platform default — this must be the platform file's own, not the base's.
        _dc.WritePlatform("_SharedBase", """
            <Controllers>
              <Controller name="Pad" default="true">
                <Mapping name="A" input="ButtonA" />
              </Controller>
            </Controllers>
            """);
        _dc.WritePlatform(Platform, """
            <Controllers inheritFrom="_SharedBase">
              <Controller name="CD-Pad" default="true">
                <Mapping name="A" input="ButtonB" />
              </Controller>
            </Controllers>
            """);

        ResolvedMapping mapping = Build().Load(Game());

        mapping.Controller.ShouldBe("CD-Pad");
        mapping.ButtonToInput.ShouldBeDictionaryOf(("A", ["ButtonB"]));
    }

    [Fact]
    public void Load_PerGameSelectsUnknownController_FallsBackToPlatformDefault()
    {
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Pad" default="true">
                <Mapping name="A" input="ButtonA" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteGameMapping(Platform, "OutRun", """
            <GameMapping controller="DoesNotExist">
              <Mapping name="A" input="ButtonY" />
            </GameMapping>
            """);

        ResolvedMapping mapping = Build().Load(Game(romName: "OutRun"));

        mapping.Controller.ShouldBe("Pad");
        mapping.ButtonToInput["A"].ShouldBe(["ButtonY"]);
    }

    // ---- CloneOf fallback ----

    [Fact]
    public void Load_PerGameMissingButCloneOfPresent_LoadsFromCloneOf()
    {
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Pad" default="true">
                <Mapping name="A" input="ButtonA" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteGameMapping(Platform, "Parent", """
            <GameMapping>
              <Mapping name="A" input="ButtonY" />
            </GameMapping>
            """);

        ResolvedMapping mapping = Build().Load(Game(romName: "Clone", cloneOf: "Parent"));

        mapping.ButtonToInput["A"].ShouldBe(["ButtonY"]);
    }

    // ---- transform application + natural-map splicing ----

    [Fact]
    public void Load_TransformApplies_NaturalMapsRetainSourceBaselineNotTransformedMapping()
    {
        // Source baseline: A→ButtonA. Transform swaps A→ButtonStart. NaturalButtonToInput must
        // reflect the pre-transform baseline ("A is naturally ButtonA"), even though the active
        // ButtonToInput shows the transformed view ("A is now ButtonStart"). This lets
        // VisibilityEvaluator detect that ButtonA's natural physical button is still present in
        // the mapping (via Naturals) after its action has been remapped away.
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Pad" default="true">
                <Mapping name="A" input="ButtonA" />
              </Controller>
            </Controllers>
            """);
        var transform = new StubMappingTransform((_, baseline) => MappingConfig(
            controller: baseline.Controller,
            mappings: [("A", "ButtonStart")]));

        ResolvedMapping mapping = Build(transform: transform).Load(Game(romName: "OutRun"));

        mapping.ButtonToInput["A"].ShouldBe(["ButtonStart"]);
        mapping.NaturalButtonToInput["A"].ShouldBe(["ButtonA"]);
        mapping.NaturalInputToButton["ButtonA"].ShouldBe("A");
        mapping.InputToButton.ContainsKey("ButtonA").ShouldBeFalse();
    }

    [Fact]
    public void Load_TransformReturnsNull_NoTransformApplied_NaturalsEqualActive()
    {
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Pad" default="true">
                <Mapping name="A" input="ButtonA" />
              </Controller>
            </Controllers>
            """);
        var transform = new StubMappingTransform((_, _) => null);

        ResolvedMapping mapping = Build(transform: transform).Load(Game(romName: "OutRun"));

        mapping.ButtonToInput.ShouldBe(mapping.NaturalButtonToInput);
        mapping.InputToButton.ShouldBe(mapping.NaturalInputToButton);
    }

    // ---- interaction scenarios ----
    // The cross-cutting cases — multiple features composing through the full pipeline at once.

    [Fact]
    public void Scenario_PerGameSelectsAlternateController_WithA2D_AndTransformOnTop()
    {
        // Per-game XML selects the non-default "6-Button" controller (which declares A2D=Right),
        // adds an override mapping, and a transform overlays a swap on top. Verify:
        //   (1) Controller selection threaded through to ResolvedMapping.Controller
        //   (2) A2D inherited from the chosen baseline controller, mirror applied to active map
        //   (3) Transform replaces the active button-to-input for Y, but Naturals still record Y's
        //       pre-transform identity
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Pad" default="true">
                <Mapping name="A" input="ButtonA" />
              </Controller>
              <Controller name="6-Button" analogToDigital="right">
                <Mapping name="A" input="ButtonA" />
                <Mapping name="Y" input="ButtonY" />
                <Mapping name="Up" input="ButtonDpadUp" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteGameMapping(Platform, "OutRun", """
            <GameMapping controller="6-Button">
              <Mapping name="A" input="ButtonStart" />
            </GameMapping>
            """);
        // Transform: swap Y onto ButtonZ (e.g. simulating a MAME-style cfg overlay).
        var transform = new StubMappingTransform((_, baseline) =>
        {
            var copy = MappingConfig(
                controller: baseline.Controller,
                analogToDigital: baseline.AnalogToDigital,
                mappings: [.. baseline.Mappings.Select(m =>
                    m.Name == "Y" ? (m.Name, Input: "ButtonZ") : (m.Name, m.Input))]);
            return copy;
        });

        ResolvedMapping mapping = Build(transform: transform).Load(Game(romName: "OutRun"));

        // (1) controller selection
        mapping.Controller.ShouldBe("6-Button");

        // (2) A2D inherited from 6-Button, mirror applied to active map (Up gains stick generic)
        mapping.AnalogToDigital.ShouldBe(AnalogToDigitalMode.Right);
        mapping.ButtonToInput["Up"].ShouldBe(["ButtonDpadUp", "AxisRightStickUp"]);

        // Per-game override survived the transform layering for A
        mapping.ButtonToInput["A"].ShouldBe(["ButtonStart"]);

        // (3) transform's swap on Y is the active mapping; naturals retain pre-transform ButtonY
        mapping.ButtonToInput["Y"].ShouldBe(["ButtonZ"]);
        mapping.NaturalButtonToInput["Y"].ShouldBe(["ButtonY"]);
        mapping.NaturalInputToButton["ButtonY"].ShouldBe("Y");
    }

    [Fact]
    public void Scenario_TransformMovesJoystickDirectionsOntoASecondControl_WholeJoystickFollows()
    {
        // The MAME shape, through the real pipeline. A cfg binds each P1_JOYSTICK_* port to the
        // hat OR the matching axis, so every direction drives a Dpad control and a left-stick
        // control at once. JOYSTICK itself cannot be moved by a cfg — MAME has no whole-joystick
        // port — so without the derivation it stays on ButtonDpad and a label written against it
        // reaches only half the controls the player is using.
        _dc.WritePlatform("Arcade", """
            <Controllers>
              <Controller name="Cabinet" default="true">
                <Mapping name="BUTTON1" input="ButtonA" />
                <Mapping name="JOYSTICK" input="ButtonDpad" />
                <Mapping name="JOYSTICK_UP" input="ButtonDpadUp" />
                <Mapping name="JOYSTICK_DOWN" input="ButtonDpadDown" />
                <Mapping name="JOYSTICK_LEFT" input="ButtonDpadLeft" />
                <Mapping name="JOYSTICK_RIGHT" input="ButtonDpadRight" />
              </Controller>
            </Controllers>
            """);
        // Each direction gains its stick equivalent, exactly as an OR'd JOYCODE sequence does.
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

        ResolvedMapping mapping = Build(transform: transform)
            .Load(Game(platform: "Arcade", romName: "3on3dunk"));

        // The whole joystick now drives both controls, with the derived one appended -- and,
        // alongside it, each individual direction its siblings currently reach (what lets the
        // labels layer's own collapse pass tell a genuine per-direction disagreement apart from
        // the ordinary case, later).
        mapping.ButtonToInput["JOYSTICK"].ShouldBe([
            "ButtonDpad", "AxisLeftStick",
            "ButtonDpadUp", "AxisLeftStickUp", "ButtonDpadDown", "AxisLeftStickDown",
            "ButtonDpadLeft", "AxisLeftStickLeft", "ButtonDpadRight", "AxisLeftStickRight",
        ]);

        // The directions are untouched by the derivation, and an ordinary button is unaffected.
        mapping.ButtonToInput["JOYSTICK_UP"].ShouldBe(["ButtonDpadUp", "AxisLeftStickUp"]);
        mapping.ButtonToInput["BUTTON1"].ShouldBe(["ButtonA"]);

        // Naturals still record the pre-transform state, so remap detection is unaffected.
        mapping.NaturalButtonToInput["JOYSTICK"].ShouldBe(["ButtonDpad"]);
        mapping.NaturalButtonToInput["JOYSTICK_UP"].ShouldBe(["ButtonDpadUp"]);
    }

    [Fact]
    public void Scenario_TransformMovesOnlyOneJoystickDirection_WholeJoystickStaysPut()
    {
        // Only UP reaches the stick. Pushing the stick up moves the player, but the stick as a
        // whole does not, so the whole-joystick binding stays on the Dpad alone.
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
        var transform = new StubMappingTransform((_, baseline) => MappingConfig(
            controller: baseline.Controller,
            analogToDigital: baseline.AnalogToDigital,
            mappings: [.. baseline.Mappings.Select(m => (m.Name, m.Input)), ("JOYSTICK_UP", "AxisLeftStickUp")]));

        ResolvedMapping mapping = Build(transform: transform)
            .Load(Game(platform: "Arcade", romName: "3on3dunk"));

        // No new whole is added -- but the directions siblings currently reach still are, same
        // as any other case, including the one that moved onto the stick.
        mapping.ButtonToInput["JOYSTICK"].ShouldBe([
            "ButtonDpad", "ButtonDpadUp", "AxisLeftStickUp", "ButtonDpadDown", "ButtonDpadLeft", "ButtonDpadRight",
        ]);
    }

    [Fact]
    public void Scenario_PerGameXmlMovesJoystickDirections_WholeJoystickIsLeftAlone()
    {
        // The gate, stated as a test. A per-game file moves every direction onto the stick and
        // says nothing about JOYSTICK — and JOYSTICK must stay on the Dpad alone.
        //
        // This is the one layer that is NOT derived over. Its author writes in the plugin's own
        // vocabulary and could have said `<Mapping name="JOYSTICK" input="AxisLeftStick" />`
        // outright; an emulator cfg cannot, which is the whole reason the derivation exists.
        // Where an author could have spoken and did not, the silence is the instruction.
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
        _dc.WriteGameMapping("Arcade", "handmade", """
            <GameMapping>
              <Mapping name="JOYSTICK_UP" input="AxisLeftStickUp" />
              <Mapping name="JOYSTICK_DOWN" input="AxisLeftStickDown" />
              <Mapping name="JOYSTICK_LEFT" input="AxisLeftStickLeft" />
              <Mapping name="JOYSTICK_RIGHT" input="AxisLeftStickRight" />
            </GameMapping>
            """);

        ResolvedMapping mapping = Build().Load(Game(platform: "Arcade", romName: "handmade"));

        mapping.ButtonToInput["JOYSTICK_UP"].ShouldBe(["AxisLeftStickUp"]);
        mapping.ButtonToInput["JOYSTICK"].ShouldBe(["ButtonDpad"]);
    }

    [Fact]
    public void Scenario_WholeJoystickFollowsTheCfg_ReverseLookupAgrees()
    {
        // ButtonToInput and InputToButton are two views of one mapping, and the derivation must
        // leave them agreeing. VisibilityEvaluator asks the reverse map whether an input is
        // driven at all, and InputImageResolver asks it which physical button to draw — so a
        // stick that gained a binding in the forward map but not the reverse would render dim
        // and fall back to a generic image while being fully playable.
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

        ResolvedMapping mapping = Build(transform: transform)
            .Load(Game(platform: "Arcade", romName: "3on3dunk"));

        mapping.ButtonToInput["JOYSTICK"].ShouldBe([
            "ButtonDpad", "AxisLeftStick",
            "ButtonDpadUp", "AxisLeftStickUp", "ButtonDpadDown", "AxisLeftStickDown",
            "ButtonDpadLeft", "AxisLeftStickLeft", "ButtonDpadRight", "AxisLeftStickRight",
        ]);
        mapping.InputToButton["AxisLeftStick"].ShouldBe("JOYSTICK");
    }

    [Fact]
    public void Scenario_DerivedBindingDoesNotDisplaceAButtonAlreadyOnThatControl()
    {
        // The cabinet has a real JOYSTICKLEFT on the stick, so when JOYSTICK derives its way
        // there too, two buttons drive it. Reverse lookup is first-seen-wins and the derived
        // binding is appended, so the button genuinely mapped to the stick keeps it — a name
        // that means "the left stick" outranks one that merely turned out to reach it.
        _dc.WritePlatform("Arcade", """
            <Controllers>
              <Controller name="Cabinet" default="true">
                <Mapping name="JOYSTICK" input="ButtonDpad" />
                <Mapping name="JOYSTICK_UP" input="ButtonDpadUp" />
                <Mapping name="JOYSTICK_DOWN" input="ButtonDpadDown" />
                <Mapping name="JOYSTICK_LEFT" input="ButtonDpadLeft" />
                <Mapping name="JOYSTICK_RIGHT" input="ButtonDpadRight" />
                <Mapping name="JOYSTICKLEFT" input="AxisLeftStick" />
              </Controller>
            </Controllers>
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

        ResolvedMapping mapping = Build(transform: transform)
            .Load(Game(platform: "Arcade", romName: "3on3dunk"));

        mapping.ButtonToInput["JOYSTICK"].ShouldBe([
            "ButtonDpad", "AxisLeftStick",
            "ButtonDpadUp", "AxisLeftStickUp", "ButtonDpadDown", "AxisLeftStickDown",
            "ButtonDpadLeft", "AxisLeftStickLeft", "ButtonDpadRight", "AxisLeftStickRight",
        ]);
        mapping.InputToButton["AxisLeftStick"].ShouldBe("JOYSTICKLEFT");
    }

    [Fact]
    public void Scenario_WholeInputFollowsDirections_WithAnalogToDigitalMirrorAlreadyApplied()
    {
        // Two derivations meeting. AnalogToDigitalMirror runs inside InputMappingResolver, so by
        // the time the whole-input derivation sees either mapping, Dpad-Any is already on the
        // left stick and every direction already carries its stick equivalent — in the reference
        // as well as the current mapping. The derivation must reach the same conclusion the
        // mirror did without restating it, and must still pick up the genuinely new control.
        //
        // Deliberately not MAME's vocabulary: Sega Genesis names its whole input Dpad-Any, and
        // the pairing is discovered from the mapping rather than parsed out of the name, so the
        // derivation has to work here identically.
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Pad" default="true" analogToDigital="left">
                <Mapping name="Dpad-Any" input="ButtonDpad" />
                <Mapping name="Dpad-Up" input="ButtonDpadUp" />
                <Mapping name="Dpad-Down" input="ButtonDpadDown" />
                <Mapping name="Dpad-Left" input="ButtonDpadLeft" />
                <Mapping name="Dpad-Right" input="ButtonDpadRight" />
              </Controller>
            </Controllers>
            """);
        // The config moves every direction onto the right stick as well.
        var transform = new StubMappingTransform((_, baseline) => MappingConfig(
            controller: baseline.Controller,
            analogToDigital: baseline.AnalogToDigital,
            mappings:
            [
                .. baseline.Mappings.Select(m => (m.Name, m.Input)),
                ("Dpad-Up", "AxisRightStickUp"),
                ("Dpad-Down", "AxisRightStickDown"),
                ("Dpad-Left", "AxisRightStickLeft"),
                ("Dpad-Right", "AxisRightStickRight"),
            ]));

        ResolvedMapping mapping = Build(transform: transform).Load(Game());

        // AxisLeftStick came from the mirror and is not restated; AxisRightStick is derived --
        // and, alongside all three wholes, every individual direction the siblings currently
        // reach (each direction now carries three targets: its own Dpad part, the mirror's left-
        // stick part, and this transform's right-stick part).
        mapping.ButtonToInput["Dpad-Any"].ShouldBe([
            "ButtonDpad", "AxisLeftStick", "AxisRightStick",
            "ButtonDpadUp", "AxisRightStickUp", "AxisLeftStickUp",
            "ButtonDpadDown", "AxisRightStickDown", "AxisLeftStickDown",
            "ButtonDpadLeft", "AxisRightStickLeft", "AxisLeftStickLeft",
            "ButtonDpadRight", "AxisRightStickRight", "AxisLeftStickRight",
        ]);
    }

    [Fact]
    public void Scenario_PerGameXmlRemapsButtons_ThenCfgMovesDirections_BothSurvive()
    {
        // The real 3on3dunk shape, which the end-to-end fixture deliberately simplifies away:
        // the companion cfg pack rotates the face buttons *and* ORs the joystick onto the axes.
        // Here the button rotation arrives from the authored per-game file and the direction
        // move from the config, so the derivation reads a reference that already carries
        // someone else's edits. The whole-input pairing must still be found in it.
        _dc.WritePlatform("Arcade", """
            <Controllers>
              <Controller name="Cabinet" default="true">
                <Mapping name="BUTTON1" input="ButtonA" />
                <Mapping name="BUTTON2" input="ButtonB" />
                <Mapping name="JOYSTICK" input="ButtonDpad" />
                <Mapping name="JOYSTICK_UP" input="ButtonDpadUp" />
                <Mapping name="JOYSTICK_DOWN" input="ButtonDpadDown" />
                <Mapping name="JOYSTICK_LEFT" input="ButtonDpadLeft" />
                <Mapping name="JOYSTICK_RIGHT" input="ButtonDpadRight" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteGameMapping("Arcade", "3on3dunk", """
            <GameMapping>
              <Mapping name="BUTTON1" input="ButtonX" />
              <Mapping name="BUTTON2" input="ButtonY" />
            </GameMapping>
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

        ResolvedMapping mapping = Build(transform: transform)
            .Load(Game(platform: "Arcade", romName: "3on3dunk"));

        // The authored button rotation is intact and is the natural state, not a remap.
        mapping.ButtonToInput["BUTTON1"].ShouldBe(["ButtonX"]);
        mapping.NaturalButtonToInput["BUTTON1"].ShouldBe(["ButtonX"]);

        // The derivation still found the joystick pairing in that edited reference.
        mapping.ButtonToInput["JOYSTICK"].ShouldBe([
            "ButtonDpad", "AxisLeftStick",
            "ButtonDpadUp", "AxisLeftStickUp", "ButtonDpadDown", "AxisLeftStickDown",
            "ButtonDpadLeft", "AxisLeftStickLeft", "ButtonDpadRight", "AxisLeftStickRight",
        ]);
    }

    [Fact]
    public void Scenario_EveryDirectionMovedOffTheDpad_WholeJoystickStopsClaimingIt()
    {
        // The other half of the whole-input problem, and the one still open. A config that binds
        // each joystick direction to a stick axis *alone* — no hat, which is what a player on a
        // gamepad would write — moves every direction off the D-pad. JOYSTICK follows onto the
        // stick correctly, but it also keeps its original claim on the D-pad, which nothing
        // drives any more.
        //
        // The player sees "Move" printed on a D-pad that does nothing, beside a correctly
        // labelled stick. A stranded label is worse than a missing one: it actively misdirects.
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
        // Each direction is *replaced*, not added to — the axis only, no hat.
        var transform = new StubMappingTransform((_, baseline) => MappingConfig(
            controller: baseline.Controller,
            analogToDigital: baseline.AnalogToDigital,
            mappings:
            [
                ("JOYSTICK", "ButtonDpad"),
                ("JOYSTICK_UP", "AxisLeftStickUp"),
                ("JOYSTICK_DOWN", "AxisLeftStickDown"),
                ("JOYSTICK_LEFT", "AxisLeftStickLeft"),
                ("JOYSTICK_RIGHT", "AxisLeftStickRight"),
            ]));

        ResolvedMapping mapping = Build(transform: transform)
            .Load(Game(platform: "Arcade", romName: "mslug"));

        // Following onto the stick is right and already works.
        mapping.ButtonToInput["JOYSTICK"].ShouldContain("AxisLeftStick");

        // Keeping the D-pad is the bug: no direction of it is driven any more.
        mapping.ButtonToInput["JOYSTICK"].ShouldNotContain("ButtonDpad");
    }

    // ---- user-layer override ----

    [Fact]
    public void Load_UserControllerFilePresent_WinsOverDefaultsFile()
    {
        // Defaults file has A→ButtonA; User file replaces the whole controller with A→ButtonB.
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Pad" default="true">
                <Mapping name="A" input="ButtonA" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteUserPlatform(Platform, """
            <Controllers>
              <Controller name="Pad" default="true">
                <Mapping name="A" input="ButtonB" />
              </Controller>
            </Controllers>
            """);

        ResolvedMapping mapping = Build().Load(Game(romName: "OutRun"));

        // The User file wins — ButtonB, not the Defaults ButtonA
        mapping.ButtonToInput["A"].ShouldBe(["ButtonB"]);
    }

    [Fact]
    public void Load_UserGameMappingFilePresent_WinsOverDefaultsFile()
    {
        // Defaults per-game mapping has A→ButtonA; User per-game mapping remaps A→ButtonB.
        _dc.WritePlatform(Platform, """
            <Controllers>
              <Controller name="Pad" default="true">
                <Mapping name="A" input="ButtonA" />
              </Controller>
            </Controllers>
            """);
        _dc.WriteGameMapping(Platform, "OutRun", """
            <GameMapping>
              <Mapping name="A" input="ButtonA" />
            </GameMapping>
            """);
        _dc.WriteUserGameMapping(Platform, "OutRun", """
            <GameMapping>
              <Mapping name="A" input="ButtonB" />
            </GameMapping>
            """);

        ResolvedMapping mapping = Build().Load(Game(romName: "OutRun"));

        // The User per-game mapping wins — ButtonB, not the Defaults per-game ButtonA
        mapping.ButtonToInput["A"].ShouldBe(["ButtonB"]);
    }

    // ---- stubs and helpers ----

    /// <summary>Always returns the supplied config (or null) — used to occupy the middle source slot
    /// without dragging in the RetroArch plugin internals.</summary>
    private sealed class StubMappingSource(InputMappingConfig? returns) : IInputMappingSource
    {
        public bool IsEnabled(GlobalConfig config) => true;
        public InputMappingConfig? Load(GameInfo game, PlatformControllersConfig? platform) => returns;
    }

}
