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
        var stack = new InputGroup(AlwaysInclude: true, Children: [first, second], Overlays: []);

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
    public void Render_GroupOverlayWithShowIfMapped_VisibilityAggregatedAcrossMembers()
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
        var group = new InputGroup(
            AlwaysInclude: false,
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
    public void Render_GroupWithNoVisibleMembers_DropsGroupAndOverlay()
    {
        // given a Group (AlwaysInclude=false) whose members both have showIf="mapping" images,
        // a group-level overlay, and an empty mapping — no member satisfies IsMapped
        var inputUp = Input(
            name: "ButtonDpadUp",
            images: [new InputImageDefinition(0, 0, "ButtonDpadUp.png", ShowIf: Mapped)]);
        var inputDown = Input(
            name: "ButtonDpadDown",
            images: [new InputImageDefinition(0, 0, "ButtonDpadDown.png", ShowIf: Mapped)]);
        var overlay = new OverlayDefinition(X: 0, Y: 0, Source: "dpad-lines.png");
        var group = new InputGroup(AlwaysInclude: false, Children: [inputUp, inputDown], Overlays: [overlay]);

        _images.With(src: "ButtonDpadUp.png", generic: "ButtonDpadUp.png", platform: Genesis, controller: ThreeButton);
        _images.With(src: "ButtonDpadDown.png", generic: "ButtonDpadDown.png", platform: Genesis, controller: ThreeButton);

        Template template = TemplateOf([group]);

        // when the service renders
        RenderResult result = _service.Render(template, MappingOf(), LabelsOf());

        // then the group is dropped entirely — no member images and no overlay
        result.Images.ShouldBeEmpty();
    }

    [Fact]
    public void Render_GroupOverlayWhoseConditionNotMet_RendersAtMinOpacity()
    {
        // given a Group with a labelled member (showIf="label") making the group visible,
        // but no mapped members, and a group overlay showIf="mapping" — the group is included
        // because HasLabel is true, but the overlay's IsMapped check fails
        var input = Input("ButtonA", images: [new InputImageDefinition(0, 0, "ButtonA.png", ShowIf: Label)]);
        var overlay = new OverlayDefinition(X: 5, Y: 5, Source: "highlight.png", ShowIf: Mapped, MinOpacity: 0.15);
        var group = new InputGroup(AlwaysInclude: false, Children: [input], Overlays: [overlay]);

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
        var group = new InputGroup(AlwaysInclude: true, Children: [input], Overlays: [groupOverlay]);

        _images.With(src: "ButtonA.png", generic: "ButtonA.png", platform: Genesis, controller: ThreeButton);

        Template template = TemplateOf([group]);

        // when the service renders
        RenderResult result = _service.Render(template, MappingOf(), LabelsOf());

        // then the per-input overlay carries the owning input's name and the group overlay carries null
        result.Images.Single(i => i.Source == "input-highlight.png").InputName.ShouldBe("ButtonA");
        result.Images.Single(i => i.Source == "group-highlight.png").InputName.ShouldBeNull();
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
