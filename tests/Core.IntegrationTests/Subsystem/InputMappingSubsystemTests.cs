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

    /// <summary>Wraps a delegate as an <see cref="IInputMappingTransform"/> so tests can describe
    /// their own per-game overlay without dragging in the MAME plugin internals.</summary>
    private sealed class StubMappingTransform(Func<GameInfo, InputMappingConfig, InputMappingConfig?> fn)
        : IInputMappingTransform
    {
        public bool IsEnabled(GlobalConfig config) => true;
        public InputMappingConfig? Transform(GameInfo game, InputMappingConfig baseline) => fn(game, baseline);
    }
}
