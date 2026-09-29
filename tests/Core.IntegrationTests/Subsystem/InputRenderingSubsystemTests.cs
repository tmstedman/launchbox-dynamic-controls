using DynamicControls.Composition;
using DynamicControls.InputMapping;
using DynamicControls.Labels;
using DynamicControls.Rendering;
using DynamicControls.Templates;
using DynamicControls.Core.TestHelpers.Templates;
using static DynamicControls.Core.TestHelpers.Labels.LabelsFixtures;
using static DynamicControls.Templates.ShowIfCondition;

namespace DynamicControls.Core.IntegrationTests.Subsystem;

/// <summary>
/// Verifies the input-rendering pipeline with its real internal wiring intact:
/// <see cref="LayoutFilter"/> + <see cref="VisibilityEvaluator"/> + <see cref="InputImageRenderer"/>
/// + <see cref="InputImageResolver"/> + <see cref="InputLabelRenderer"/> composed by
/// <see cref="InputRenderingFactory"/>. Covers a wider scenario range than the end-to-end tests
/// reach economically — visibility flowing into image-opacity selection, image-state classifier
/// branches, collapse adjustments, group-overlay flag aggregation. Template data, mapping, and
/// labels are constructed inline so each test reads end-to-end without chasing fixtures;
/// <see cref="ITemplateImageSource"/> is faked to keep image lookups in the same file as the
/// assertions and <see cref="ILogger"/> is silenced.
/// </summary>
public class InputRenderingSubsystemTests
{
    private const string Genesis = "Sega Genesis";
    private const string ThreeButton = "3-Button";

    private readonly FakeTemplateImageSource _images = new();
    private readonly InputRenderingService _service = InputRenderingFactory.Create(new NullLogger());

    [Fact]
    public void Render_GameSpecificLabelsAndMappedInput_RoutesPlatformImageAndLabelThroughVisibility()
    {
        // given a template with two inputs:
        //   ButtonA — image at (10,20) and a label at (50,60), both showIf="auto"
        //   ButtonB — image at (100,200), showIf="auto", no label slot
        // and a mapping that drives ButtonA (mapped) but leaves ButtonB unmapped
        var inputA = Input(
            name: "ButtonA",
            images: [new InputImageDefinition(X: 10, Y: 20, ImageFile: "ButtonA.png", ShowIf: Auto)],
            labels: [new LabelDefinition(X: 50, Y: 60, FontSize: 16)]);
        var inputB = Input(
            name: "ButtonB",
            images: [new InputImageDefinition(X: 100, Y: 200, ImageFile: "ButtonB.png", ShowIf: Auto)]);

        // the platform's 3-Button styling exists for A only — proves the classifier reaches into
        // the styled folder when mapped, and falls through to generic when no styled file exists.
        _images.With(src: "A.png", generic: "A.png", styled: @"Sega Genesis\A.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "ButtonB.png", generic: "ButtonB.png", platform: Genesis, controller: ThreeButton);

        Template template = TemplateOf([inputA, inputB]);
        ResolvedMapping mapping = MappingOf(("A", "ButtonA"));
        ResolvedLabels labels = LabelsOf(isGameSpecific: true, ("ButtonA", "Jump"));

        // when the service renders
        RenderResult result = _service.Render(template, mapping, labels);

        // then ButtonA is at full opacity using the platform-styled asset (MappedDefault chose
        // the platform-button name "A.png" over the generic "ButtonA.png")
        RenderedImage imageA = result.Images.Single(i => i.InputName == "ButtonA");
        imageA.Source.ShouldBe(@"Sega Genesis\A.png");
        imageA.Opacity.ShouldBe(1.0);
        imageA.Top.ShouldBe(20);

        // and ButtonB falls to the template's MinOpacity — showIf="auto" with game-specific labels
        // resolves to "HasLabel"; ButtonB has none, so its image is faded with the inactive blur.
        RenderedImage imageB = result.Images.Single(i => i.InputName == "ButtonB");
        imageB.Source.ShouldBe("ButtonB.png");
        imageB.Opacity.ShouldBe(0.3);
        imageB.BlurRadius.ShouldBe(8);

        // and the label rendered for ButtonA carries the input-slot identity and the label-renderer's
        // baseline shift (Top = Y - FontSize*0.75 = 60 - 12 = 48)
        RenderedLabel label = result.Labels.Single();
        label.InputName.ShouldBe("ButtonA");
        label.Text.ShouldBe("Jump");
        label.Top.ShouldBe(48);
    }

    [Fact]
    public void Render_RemappedInput_ResolvesImageFromPhysicalButtonNotLogicalInput()
    {
        // given a 3-Button controller where the natural mapping is A→ButtonA, B→ButtonB, but the
        // game-specific mapping swaps them so the physical B button now drives ButtonA. The image
        // for ButtonA should follow the physical button — the player sees "B" at the ButtonA slot.
        var inputA = Input(
            name: "ButtonA",
            images: [new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonA.png")]);

        _images.With(src: "B.png", generic: "B.png", styled: @"Sega Genesis\B.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "ButtonA.png", generic: "ButtonA.png", platform: Genesis, controller: ThreeButton);

        Template template = TemplateOf([inputA]);
        var mapping = new ResolvedMapping(
            Platform: Genesis,
            Controller: ThreeButton,
            ButtonToInput: Dict(("B", ["ButtonA"])),
            InputToButton: Dict(("ButtonA", "B")),
            NaturalButtonToInput: Dict(("A", ["ButtonA"]), ("B", ["ButtonB"])),
            NaturalInputToButton: Dict(("ButtonA", "A"), ("ButtonB", "B")),
            AnalogToDigital: null);

        // when the service renders
        RenderResult result = _service.Render(template, mapping, LabelsOf());

        // then the image at the ButtonA slot is the styled platform image for the physical B
        // button (Remapped state — image follows the cabinet button being pressed)
        result.Images.Single(i => i.InputName == "ButtonA").Source.ShouldBe(@"Sega Genesis\B.png");
    }

    [Fact]
    public void Render_CollapsingStackWithVacatedFirstSlot_ShiftsLaterSlotsUpByGap()
    {
        // given a Stack with two inputs and gap=10. The first input is unmapped and has no label,
        // and its image is showIf="mapped" with MinOpacity=0 — so it vacates its slot. The second
        // input is mapped and renders at full opacity, expected to shift up by 10.
        var first = Input(
            name: "ButtonY",
            images: [new InputImageDefinition(X: 0, Y: 100, ImageFile: "ButtonY.png", ShowIf: Mapped, MinOpacity: 0)]);
        var second = Input(
            name: "ButtonA",
            images: [new InputImageDefinition(X: 0, Y: 200, ImageFile: "ButtonA.png")]);
        var stack = new Container(Children: [first, second], Overlays: []);

        _images.With(src: "ButtonY.png", generic: "ButtonY.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "A.png", generic: "A.png", styled: @"Sega Genesis\A.png", platform: Genesis, controller: ThreeButton);

        var collapseInfo = new Dictionary<InputDefinition, CollapseInfo>();
        CollapseGroupBuilder.Build(stack.Children, 10, collapseInfo);
        Template template = TemplateOf([stack], collapseInfo);
        ResolvedMapping mapping = MappingOf(("A", "ButtonA"));

        // when the service renders
        RenderResult result = _service.Render(template, mapping, LabelsOf());

        // then only the second input rendered (first was zero-opacity → filtered out), and its
        // Top is the InputImageDefinition Y plus the stack's negative offset: 200 + (-10) = 190
        result.Images.Single().InputName.ShouldBe("ButtonA");
        result.Images.Single().Top.ShouldBe(190);
    }

    [Fact]
    public void Render_CollapsingStackWithVAlignBottom_VacatedFirstSlot_StaysAnchoredAtDeclaredY()
    {
        // given a bottom-aligned 3-slot stack (gap=100), Y values as LayoutResolver would have
        // resolved them for a stack anchored at Y=300: slot0=100, slot1=200, slot2=300. The first
        // input is unmapped with MinOpacity=0, so it vacates; the other two always render.
        var first = Input(
            name: "ButtonY",
            images: [new InputImageDefinition(X: 0, Y: 100, ImageFile: "ButtonY.png", ShowIf: Mapped, MinOpacity: 0)]);
        var second = Input(
            name: "ButtonA",
            images: [new InputImageDefinition(X: 0, Y: 200, ImageFile: "ButtonA.png")]);
        var third = Input(
            name: "ButtonB",
            images: [new InputImageDefinition(X: 0, Y: 300, ImageFile: "ButtonB.png")]);
        var stack = new Container(Children: [first, second, third], Overlays: []);

        _images.With(src: "ButtonY.png", generic: "ButtonY.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "A.png", generic: "A.png", styled: @"Sega Genesis\A.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "ButtonB.png", generic: "ButtonB.png", platform: Genesis, controller: ThreeButton);

        var collapseInfo = new Dictionary<InputDefinition, CollapseInfo>();
        CollapseGroupBuilder.Build(stack.Children, 100, collapseInfo, vAlign: "bottom");
        Template template = TemplateOf([stack], collapseInfo);
        ResolvedMapping mapping = MappingOf(("A", "ButtonA"), ("B", "ButtonB"));

        // when the service renders
        RenderResult result = _service.Render(template, mapping, LabelsOf());

        // then the first slot vacates as before, but the vAlign correction (one gap, since 3
        // nominal slots became 2 visible) keeps the last visible input exactly at the declared
        // Y=300 -- without the correction it would land at 200, drifting toward the top as if
        // vAlign had no effect.
        result.Images.Single(i => i.InputName == "ButtonA").Top.ShouldBe(200);
        result.Images.Single(i => i.InputName == "ButtonB").Top.ShouldBe(300);
    }

    [Fact]
    public void Render_ContainerOverlayWithShowIfMapped_VisibilityAggregatedAcrossMembers()
    {
        // given a Group with two inputs and a single group-level overlay (showIf=Mapped). Only
        // one of the two members is mapped — but because the overlay's visibility is OR-reduced
        // across the group, the overlay should still render at full opacity.
        var inputUp = Input(
            name: "ButtonDpadUp",
            images: [new InputImageDefinition(0, 0, "ButtonDpadUp.png")]);
        var inputDown = Input(
            name: "ButtonDpadDown",
            images: [new InputImageDefinition(0, 0, "ButtonDpadDown.png")]);
        var overlay = new OverlayDefinition(X: 5, Y: 5, Source: "dpad-lines.png", ShowIf: Mapped, MinOpacity: 0.2);
        var group = new Container(
            Children: [inputUp, inputDown],
            Overlays: [overlay]);

        _images.With(src: "ButtonDpadUp.png", generic: "ButtonDpadUp.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "ButtonDpadDown.png", generic: "ButtonDpadDown.png", platform: Genesis, controller: ThreeButton);

        Template template = TemplateOf([group]);
        // only Dpad-Up is mapped — Dpad-Down has no platform button driving it
        var mapping = new ResolvedMapping(
            Platform: Genesis,
            Controller: ThreeButton,
            ButtonToInput: Dict(("Dpad-Up", ["ButtonDpadUp"])),
            InputToButton: Dict(("ButtonDpadUp", "Dpad-Up")),
            NaturalButtonToInput: Dict(("Dpad-Up", ["ButtonDpadUp"])),
            NaturalInputToButton: Dict(("ButtonDpadUp", "Dpad-Up")),
            AnalogToDigital: null);

        // when the service renders
        RenderResult result = _service.Render(template, mapping, LabelsOf());

        // then the group overlay rendered at full opacity — the group's aggregate IsMapped flag
        // (OR of members) satisfies the overlay's showIf=Mapped, even though one member doesn't
        RenderedImage overlayImage = result.Images.Single(i => i.Source == "dpad-lines.png");
        overlayImage.Opacity.ShouldBe(1.0);
        overlayImage.BlurRadius.ShouldBe(0.0);
        overlayImage.InputName.ShouldBeNull(); // group-level overlays carry no input identity
    }

    [Fact]
    public void Render_DefaultLabelsWithAutoShowIf_UsesMappingNotLabelForVisibility()
    {
        // given two inputs both showIf="auto" with default (non-game-specific) labels.
        // ButtonB has a label but is unmapped — this proves auto resolves to IsMapped, not
        // HasLabel, when IsGameSpecific is false: a game-specific label would make ButtonB
        // visible, but a default label does not.
        var inputA = Input(
            name: "ButtonA",
            images: [new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonA.png", ShowIf: Auto)]);
        var inputB = Input(
            name: "ButtonB",
            images: [new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonB.png", ShowIf: Auto)]);

        _images.With(src: "A.png", generic: "A.png", styled: @"Sega Genesis\A.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "ButtonB.png", generic: "ButtonB.png", platform: Genesis, controller: ThreeButton);

        Template template = TemplateOf([inputA, inputB]);
        ResolvedMapping mapping = MappingOf(("A", "ButtonA"));
        ResolvedLabels labels = LabelsOf(isGameSpecific: false, ("ButtonB", "Press Start"));

        // when the service renders
        RenderResult result = _service.Render(template, mapping, labels);

        // then ButtonA renders at full opacity (IsMapped=true) and ButtonB fades despite
        // having a label — IsMapped=false is what drives visibility in default-label mode
        result.Images.Single(i => i.InputName == "ButtonA").Opacity.ShouldBe(1.0);
        RenderedImage imageB = result.Images.Single(i => i.InputName == "ButtonB");
        imageB.Opacity.ShouldBe(0.3);
        imageB.BlurRadius.ShouldBe(8);
    }

    [Fact]
    public void Render_ContainerWithNoVisibleMembers_StillRendersMembersFadedAndOverlayAtFullOpacity()
    {
        // given a Container whose members both have showIf="mapping" images, a container-level
        // overlay with no showIf of its own (defaults to Always), and an empty mapping — no
        // member satisfies IsMapped. Unlike the old <Group> this replaced, nothing here gates the
        // Container's own inclusion any more -- only an explicit wrapping Condition would.
        var inputUp = Input(
            name: "ButtonDpadUp",
            images: [new InputImageDefinition(0, 0, "ButtonDpadUp.png", ShowIf: Mapped)]);
        var inputDown = Input(
            name: "ButtonDpadDown",
            images: [new InputImageDefinition(0, 0, "ButtonDpadDown.png", ShowIf: Mapped)]);
        var overlay = new OverlayDefinition(X: 0, Y: 0, Source: "dpad-lines.png");
        var container = new Container(Children: [inputUp, inputDown], Overlays: [overlay]);

        _images.With(src: "ButtonDpadUp.png", generic: "ButtonDpadUp.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "ButtonDpadDown.png", generic: "ButtonDpadDown.png", platform: Genesis, controller: ThreeButton);

        Template template = TemplateOf([container]);

        // when the service renders
        RenderResult result = _service.Render(template, MappingOf(), LabelsOf());

        // then both members still render, faded to the template's default MinOpacity (0.3) since
        // their own showIf=mapping isn't satisfied, and the overlay renders at full opacity —
        // its own showIf=Always is never affected by the members' mapping state
        result.Images.Single(i => i.InputName == "ButtonDpadUp").Opacity.ShouldBe(0.3);
        result.Images.Single(i => i.InputName == "ButtonDpadDown").Opacity.ShouldBe(0.3);
        result.Images.Single(i => i.Source == "dpad-lines.png").Opacity.ShouldBe(1.0);
    }

    [Fact]
    public void Render_ContainerOverlayWhoseConditionNotMet_RendersAtMinOpacity()
    {
        // given a Group with a labelled member (showIf="label") making the group visible,
        // but no mapped members, and a group overlay showIf="mapping" — the group is included
        // because HasLabel is true, but the overlay's IsMapped check fails
        var input = Input("ButtonA", images: [new InputImageDefinition(0, 0, "ButtonA.png", ShowIf: Label)]);
        var overlay = new OverlayDefinition(X: 5, Y: 5, Source: "highlight.png", ShowIf: Mapped, MinOpacity: 0.15);
        var group = new Container(Children: [input], Overlays: [overlay]);

        _images.With(src: "ButtonA.png", generic: "ButtonA.png", platform: Genesis, controller: ThreeButton);

        Template template = TemplateOf([group]);
        ResolvedLabels labels = LabelsOf(isGameSpecific: true, ("ButtonA", "Jump"));

        // when the service renders
        RenderResult result = _service.Render(template, MappingOf(), labels);

        // then ButtonA renders at full opacity — showIf=label, HasLabel=true
        result.Images.Single(i => i.InputName == "ButtonA").Opacity.ShouldBe(1.0);

        // and the overlay is present but faded: IsMapped=false so showIf=mapping is not
        // satisfied; blur falls back to the template's DefaultInactiveBlurRadius
        RenderedImage overlayImage = result.Images.Single(i => i.Source == "highlight.png");
        overlayImage.Opacity.ShouldBe(0.15);
        overlayImage.BlurRadius.ShouldBe(8);
        overlayImage.InputName.ShouldBeNull();
    }

    [Fact]
    public void Render_PerInputOverlay_CarriesOwningInputName()
    {
        // given an input with a per-input overlay inside a group that also has a group-level
        // overlay — both showIf=Always so both render — the distinction being tested is that
        // per-input overlays carry their owning input's name while group overlays carry null
        var perInputOverlay = new OverlayDefinition(X: 0, Y: 0, Source: "input-highlight.png");
        var input = Input("ButtonA",
            images: [new InputImageDefinition(0, 0, "ButtonA.png")],
            overlays: [perInputOverlay]);
        var groupOverlay = new OverlayDefinition(X: 0, Y: 0, Source: "group-highlight.png");
        var group = new Container(Children: [input], Overlays: [groupOverlay]);

        _images.With(src: "ButtonA.png", generic: "ButtonA.png", platform: Genesis, controller: ThreeButton);

        Template template = TemplateOf([group]);

        // when the service renders
        RenderResult result = _service.Render(template, MappingOf(), LabelsOf());

        // then the per-input overlay carries the owning input's name and the group overlay carries null
        result.Images.Single(i => i.Source == "input-highlight.png").InputName.ShouldBe("ButtonA");
        result.Images.Single(i => i.Source == "group-highlight.png").InputName.ShouldBeNull();
    }

    [Fact]
    public void Render_StickOneOfWithNestedCondition_PartialDirectionAgreement_FallsThroughToMergedAlternative()
    {
        // Regression test for a real production bug (crusnexo's left stick on Arcade): a OneOf's
        // first alternative is an outer Condition ("the whole has a label") wrapping an inner one
        // ("all four directions individually agree") gating a single glyph -- the compound-AND
        // idiom used by AxisLeftStick/AxisRightStick in Xbox Series X's Layout.xml. Left/Right
        // agree on "Steering" (so InputLabelsService would write that onto AxisLeftStick's own
        // key), but Up/Down have no label at all -- the outer check alone passes, but the inner
        // "all four" check doesn't. Before the fix, AnyVisible only consulted the outer check, so
        // OneOf picked this alternative and rendered nothing once the inner check dropped
        // everything inside, stranding the whole stick with no image or label at all instead of
        // falling through to the merged per-direction alternative.
        var up = Input(name: "AxisLeftStickUp", images: [new InputImageDefinition(0, 0, "AxisLeftStickUp.png", ShowIf: Label)]);
        var down = Input(name: "AxisLeftStickDown", images: [new InputImageDefinition(0, 0, "AxisLeftStickDown.png", ShowIf: Label)]);
        var left = Input(name: "AxisLeftStickLeft", images: [new InputImageDefinition(0, 0, "AxisLeftStickLeft.png", ShowIf: Label)]);
        var right = Input(name: "AxisLeftStickRight", images: [new InputImageDefinition(0, 0, "AxisLeftStickRight.png", ShowIf: Label)]);
        var glyph = Input(name: "AxisLeftStick", images: [new InputImageDefinition(0, 0, "AxisLeftStick-glyph.png", ShowIf: Label)]);

        var singleGlyphAlternative = new ConditionElement(ConditionMode.Any, ["AxisLeftStick"], ConditionMatch.Label,
        [
            new ConditionElement(ConditionMode.All,
                ["AxisLeftStickUp", "AxisLeftStickDown", "AxisLeftStickLeft", "AxisLeftStickRight"],
                ConditionMatch.Label, [glyph])
        ]);
        var mergedAlternative = new ConditionElement(ConditionMode.Any, ["AxisLeftStick"], ConditionMatch.Label,
        [
            new Container(Children: [up, left, right, down], Overlays: [])
        ]);
        var oneOf = new OneOf([singleGlyphAlternative, mergedAlternative]);

        _images.With(src: "AxisLeftStickUp.png", generic: "AxisLeftStickUp.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "AxisLeftStickDown.png", generic: "AxisLeftStickDown.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "AxisLeftStickLeft.png", generic: "AxisLeftStickLeft.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "AxisLeftStickRight.png", generic: "AxisLeftStickRight.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "AxisLeftStick-glyph.png", generic: "AxisLeftStick-glyph.png", platform: Genesis, controller: ThreeButton);

        Template template = TemplateOf([oneOf]);
        ResolvedLabels labels = LabelsOf(isGameSpecific: true,
            ("AxisLeftStick", "Steering"), ("AxisLeftStickLeft", "Steering"), ("AxisLeftStickRight", "Steering"));

        // when the service renders
        RenderResult result = _service.Render(template, MappingOf(), labels);

        // then the merged alternative's four directions rendered -- not a total blackout, and not
        // the single glyph (whose inner "all four" check correctly never passed)
        result.Images.ShouldContain(i => i.InputName == "AxisLeftStickLeft");
        result.Images.ShouldContain(i => i.InputName == "AxisLeftStickRight");
        result.Images.ShouldContain(i => i.InputName == "AxisLeftStickUp");
        result.Images.ShouldContain(i => i.InputName == "AxisLeftStickDown");
        result.Images.ShouldNotContain(i => i.InputName == "AxisLeftStick");
        result.Images.Single(i => i.InputName == "AxisLeftStickLeft").Opacity.ShouldBe(1.0);
        result.Images.Single(i => i.InputName == "AxisLeftStickUp").Opacity.ShouldBe(0.3);
    }

    [Fact]
    public void Render_LooseLabelUnderCondition_AttachesToOwningInputWithoutADuplicateStrictSelfInput()
    {
        // The same crusnexo-shaped scenario as above (Left/Right agree on "Steering", Up/Down have
        // no label), but the shared label is expressed with the new capability instead of the
        // duplicate strict-self <Input name="AxisLeftStick"> workaround: a bare LabelElement
        // nested inside the Condition, attaching ambiently to AxisLeftStick's own top-level
        // InputDefinition. Confirms the merged branch renders identically either way.
        var up = Input(name: "AxisLeftStickUp", images: [new InputImageDefinition(0, 0, "AxisLeftStickUp.png", ShowIf: Label)]);
        var down = Input(name: "AxisLeftStickDown", images: [new InputImageDefinition(0, 0, "AxisLeftStickDown.png", ShowIf: Label)]);
        var left = Input(name: "AxisLeftStickLeft", images: [new InputImageDefinition(0, 0, "AxisLeftStickLeft.png", ShowIf: Label)]);
        var right = Input(name: "AxisLeftStickRight", images: [new InputImageDefinition(0, 0, "AxisLeftStickRight.png", ShowIf: Label)]);

        var mergedAlternative = new ConditionElement(ConditionMode.Any, ["AxisLeftStick"], ConditionMatch.Label,
        [
            new Container(Children: [up, left, right, down], Overlays: []),
            new LabelElement(new LabelDefinition(X: 0, Y: 0, Alignment: "right")),
        ]);
        var axisLeftStick = new InputDefinition(
            Name: "AxisLeftStick",
            InputImages: [],
            Overlays: [],
            Labels: [],
            Children: [mergedAlternative]);

        _images.With(src: "AxisLeftStickUp.png", generic: "AxisLeftStickUp.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "AxisLeftStickDown.png", generic: "AxisLeftStickDown.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "AxisLeftStickLeft.png", generic: "AxisLeftStickLeft.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "AxisLeftStickRight.png", generic: "AxisLeftStickRight.png", platform: Genesis, controller: ThreeButton);

        Template template = TemplateOf([axisLeftStick]);
        ResolvedLabels labels = LabelsOf(isGameSpecific: true,
            ("AxisLeftStick", "Steering"), ("AxisLeftStickLeft", "Steering"), ("AxisLeftStickRight", "Steering"));

        // when the service renders
        RenderResult result = _service.Render(template, MappingOf(), labels);

        // then all four direction icons render, and the shared label attached to AxisLeftStick's
        // own identity — reached only through the Condition, with no Input of its own
        result.Images.Select(i => i.InputName).ShouldContain("AxisLeftStickLeft");
        result.Images.Select(i => i.InputName).ShouldContain("AxisLeftStickUp");
        RenderedLabel label = result.Labels.Single();
        label.InputName.ShouldBe("AxisLeftStick");
        label.Text.ShouldBe("Steering");
    }

    [Fact]
    public void Render_CollapsingContainerWithTwoOfFourDirectionsLabelled_MergedLabelCentersOnSurvivorsAndKeepsItsOwnNudge()
    {
        // Real production shape (Templates/Xbox Series X/Layout.xml's AxisRightStick multi-label
        // branch): a collapsing Group with no vAlign attribute (defaults to "top"), four direction
        // Inputs (style="small-label-vacate" -- showIf="label", MinOpacity=0, so an unlabelled
        // direction fully vacates its slot instead of merely dimming), and a loose Label carrying
        // its own y="+15" hand-tuned nudge. Mirrors a MAME game whose right stick has two
        // directions sharing identical label text ("Steer" on Left and Right) with Up/Down
        // unlabelled -- Up and Down vacate, Left and Right survive. Drives the real
        // VisibilityEvaluator (not a mock) so both the direction icons' vacate/shift and the
        // merged label's render-time centering come from the same per-game visibility facts.
        var up = Input(name: "AxisRightStickUp",
            images: [new InputImageDefinition(0, 772, "AxisRightStickUp.png", ShowIf: Label, MinOpacity: 0)]);
        var left = Input(name: "AxisRightStickLeft",
            images: [new InputImageDefinition(0, 817, "AxisRightStickLeft.png", ShowIf: Label)]);
        var right = Input(name: "AxisRightStickRight",
            images: [new InputImageDefinition(0, 862, "AxisRightStickRight.png", ShowIf: Label)]);
        var down = Input(name: "AxisRightStickDown",
            images: [new InputImageDefinition(0, 907, "AxisRightStickDown.png", ShowIf: Label, MinOpacity: 0)]);
        // Y=787: what LayoutResolver bakes for a loose Label with y="+15" here -- the nominal
        // (uncollapsed, vAlign="top" never shifts) frame origin of 772, plus the +15 nudge.
        var looseLabel = new LabelElement(new LabelDefinition(X: 0, Y: 787, FontSize: 20));
        var group = new Container(
            Children: [up, left, right, down, looseLabel],
            Overlays: [],
            DeclaredOriginY: 772,
            Gap: 45,
            VAlign: "top",
            Collapse: true);
        var axisRightStick = new InputDefinition(
            Name: "AxisRightStick", InputImages: [], Overlays: [], Labels: [], Children: [group]);

        _images.With(src: "AxisRightStickUp.png", generic: "AxisRightStickUp.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "AxisRightStickLeft.png", generic: "AxisRightStickLeft.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "AxisRightStickRight.png", generic: "AxisRightStickRight.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "AxisRightStickDown.png", generic: "AxisRightStickDown.png", platform: Genesis, controller: ThreeButton);

        // The icon vacate/shift mechanism (ComputeCollapseAdjustments) reads the separate
        // CollapseInfo dictionary, not Container's own fields -- both must be built here for a
        // realistic scenario, same as the other collapsing-group tests in this file.
        var collapseInfo = new Dictionary<InputDefinition, CollapseInfo>();
        CollapseGroupBuilder.Build(group.Children, gap: 45, collapseInfo, vAlign: "top");
        Template template = TemplateOf([axisRightStick], collapseInfo);
        ResolvedLabels labels = LabelsOf(isGameSpecific: true,
            ("AxisRightStick", "Steer"), ("AxisRightStickLeft", "Steer"), ("AxisRightStickRight", "Steer"));

        // when the service renders
        RenderResult result = _service.Render(template, MappingOf(), labels);

        // then Up and Down vacated (no label, MinOpacity=0) -- only Left and Right rendered, each
        // shifted up by one gap (Up's vacancy) from their nominal Y: 817-45=772, 862-45=817
        result.Images.Select(i => i.InputName).ShouldBe(["AxisRightStickLeft", "AxisRightStickRight"]);
        result.Images.Single(i => i.InputName == "AxisRightStickLeft").Top.ShouldBe(772);
        result.Images.Single(i => i.InputName == "AxisRightStickRight").Top.ShouldBe(817);

        // and the merged label centers on the true midpoint of the surviving pair (772 and 817,
        // i.e. 794.5) -- not the nominal 4-slot center (which vAlign="top" would otherwise have
        // left it pinned to, 772) -- plus its own +15 nudge preserved on top (809.5),
        // baseline-adjusted for FontSize=20 (Top = Y - FontSize*0.75 = 809.5 - 15 = 794.5)
        RenderedLabel label = result.Labels.Single();
        label.InputName.ShouldBe("AxisRightStick");
        label.Text.ShouldBe("Steer");
        label.Top.ShouldBe(794.5);
    }

    [Fact]
    public void Render_ConditionGatedLooseOverlay_AttachesToEnclosingInputAndUsesItsOwnFlags()
    {
        // given an Input whose only child is a Condition wrapping a bare Overlay -- no wrapping
        // Input needed, unlike the "strict-self render position" pattern this generalizes away:
        // an extra decoration that should only render once this specific input is mapped
        var overlayElement = new OverlayElement(new OverlayDefinition(X: 5, Y: 5, Source: "extra.png", ShowIf: Mapped));
        var condition = new ConditionElement(ConditionMode.Any, ["ButtonA"], ConditionMatch.Mapped, [overlayElement]);
        var inputA = new InputDefinition(
            Name: "ButtonA",
            InputImages: [new InputImageDefinition(X: 0, Y: 0, ImageFile: "ButtonA.png", ShowIf: Mapped)],
            Overlays: [],
            Labels: [],
            Children: [condition]);

        _images.With(src: "ButtonA.png", generic: "ButtonA.png", platform: Genesis, controller: ThreeButton);

        Template template = TemplateOf([inputA]);
        ResolvedMapping mapping = MappingOf(("A", "ButtonA"));

        // when the service renders
        RenderResult result = _service.Render(template, mapping, LabelsOf());

        // then the loose overlay rendered with ButtonA's own identity and its own IsMapped flag,
        // exactly as if it had been a direct child of the Input
        RenderedImage overlayImage = result.Images.Single(i => i.Source == "extra.png");
        overlayImage.Opacity.ShouldBe(1.0);
        overlayImage.InputName.ShouldBe("ButtonA");
    }

    [Fact]
    public void Render_ConditionGatedLooseOverlay_WithNoAmbientOwner_RendersUnconditionally()
    {
        // given a top-level Condition (no enclosing Input or Container at all) wrapping a bare
        // Overlay -- there's no fold-in target, so it renders unconditionally rather than being
        // treated as a template-authoring error the way a loose Label would be
        var overlayElement = new OverlayElement(new OverlayDefinition(X: 0, Y: 0, Source: "background.png"));
        var condition = new ConditionElement(ConditionMode.Any, ["ButtonA"], ConditionMatch.Mapped, [overlayElement]);

        Template template = TemplateOf([condition]);

        // when the service renders with ButtonA mapped, so the wrapping Condition's own gate passes
        RenderResult result = _service.Render(template, MappingOf(("A", "ButtonA")), LabelsOf());

        // then the overlay rendered at full opacity, with no InputName of its own
        RenderedImage overlayImage = result.Images.Single(i => i.Source == "background.png");
        overlayImage.Opacity.ShouldBe(1.0);
        overlayImage.InputName.ShouldBeNull();
    }

    [Fact]
    public void Render_ConditionMatchAuto_NoLabelsAtAll_StillRendersMappedPair()
    {
        // Regression: a pair of Inputs wrapped in <Condition ... match="auto"> (the real
        // AxisTriggerLeft/ButtonLeftShoulder shape) used to strand both entirely for a ROM with
        // no labels at all -- "auto" wasn't a recognized match value, so it silently defaulted to
        // "label", which this Condition can never satisfy when there are no labels anywhere.
        // Each Input's own image is ShowIf=Always so the test isolates the Condition's own
        // include/drop decision from each Input's individual fade.
        var triggerLeft = Input(
            name: "AxisTriggerLeft",
            images: [new InputImageDefinition(X: 0, Y: 0, ImageFile: "AxisTriggerLeft.png")]);
        var shoulderLeft = Input(
            name: "ButtonLeftShoulder",
            images: [new InputImageDefinition(X: 0, Y: 100, ImageFile: "ButtonLeftShoulder.png")]);
        var condition = new ConditionElement(
            ConditionMode.Any, ["AxisTriggerLeft", "ButtonLeftShoulder"], ConditionMatch.Auto,
            [triggerLeft, shoulderLeft]);

        _images.With(src: "AxisTriggerLeft.png", generic: "AxisTriggerLeft.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "ButtonLeftShoulder.png", generic: "ButtonLeftShoulder.png", platform: Genesis, controller: ThreeButton);

        Template template = TemplateOf([condition]);
        ResolvedMapping mapping = MappingOf(("X", "ButtonLeftShoulder"));

        // when the service renders with default (non-game-specific) labels -- i.e. none at all
        RenderResult result = _service.Render(template, mapping, LabelsOf(isGameSpecific: false));

        // then both survive -- Auto fell back to checking IsMapped, which ButtonLeftShoulder
        // satisfies, so the Condition passes and neither Input is dropped
        result.Images.Select(i => i.InputName).ShouldBe(["AxisTriggerLeft", "ButtonLeftShoulder"]);
    }

    // ---- helpers ----

    private static InputDefinition Input(
        string name,
        InputImageDefinition[]? images = null,
        OverlayDefinition[]? overlays = null,
        LabelDefinition[]? labels = null)
    {
        return new(
            Name: name,
            InputImages: images ?? [],
            Overlays: overlays ?? [],
            Labels: labels ?? [],
            Children: []);
    }

    private Template TemplateOf(
        ILayoutElement[] elements,
        IReadOnlyDictionary<InputDefinition, CollapseInfo>? collapseInfo = null) =>
        TemplateFixtures.TemplateOf(
            elements: elements,
            imageSource: _images,
            defaultFontSize: 16,
            defaultMinOpacity: 0.3,
            defaultInactiveBlurRadius: 8,
            collapseInfo: collapseInfo);

    private static ResolvedMapping MappingOf(params (string PlatformButton, string Input)[] entries)
    {
        return new(
            Platform: Genesis,
            Controller: ThreeButton,
            ButtonToInput: entries.ToDictionary(
                e => e.PlatformButton,
                e => (IReadOnlyList<string>)[e.Input]),
            InputToButton: entries.ToDictionary(e => e.Input, e => e.PlatformButton),
            NaturalButtonToInput: entries.ToDictionary(
                e => e.PlatformButton,
                e => (IReadOnlyList<string>)[e.Input]),
            NaturalInputToButton: entries.ToDictionary(e => e.Input, e => e.PlatformButton),
            AnalogToDigital: null);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Dict(params (string Key, string[] Value)[] entries) =>
        entries.ToDictionary(e => e.Key, e => (IReadOnlyList<string>)e.Value);

    private static IReadOnlyDictionary<string, string> Dict(params (string Key, string Value)[] entries) =>
        entries.ToDictionary(e => e.Key, e => e.Value);
}
