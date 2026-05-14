using DynamicControls.InputMapping;
using DynamicControls.Labels;
using DynamicControls.Rendering;
using DynamicControls.Templates;
using NSubstitute;
using static DynamicControls.Core.TestHelpers.InputMapping.MappingFixtures;
using static DynamicControls.Core.TestHelpers.Labels.LabelsFixtures;
using static DynamicControls.Core.TestHelpers.Templates.LayoutElements;
using static DynamicControls.Core.TestHelpers.Templates.TemplateFixtures;

namespace DynamicControls.Core.Tests.Rendering;

/// <summary>
/// Unit tests for <see cref="InputRenderingService"/>. The three collaborators
/// (<see cref="ILayoutFilter"/>, <see cref="IInputImageRenderer"/>,
/// <see cref="IInputLabelRenderer"/>) are substitutes so each test pins exactly what each stage
/// emits. Focus: that the service forwards the right values to each collaborator and that the
/// per-input label-YOffset patch is applied to labels (image YOffset is baked in upstream).
/// </summary>
public class InputRenderingServiceTests
{
    private readonly ILayoutFilter _layoutFilter = Substitute.For<ILayoutFilter>();
    private readonly IInputImageRenderer _imageRenderer = Substitute.For<IInputImageRenderer>();
    private readonly IInputLabelRenderer _labelRenderer = Substitute.For<IInputLabelRenderer>();
    private readonly InputRenderingService _underTest;

    public InputRenderingServiceTests()
    {
        _underTest = new InputRenderingService(_layoutFilter, _labelRenderer, _imageRenderer);

        // Default to empty enumerables so tests that only care about what gets called don't have
        // to stub explicit returns to keep the pipeline from null-deref-ing on .Select().
        _labelRenderer.Render(Arg.Any<InputDefinition>(), Arg.Any<string?>())
            .Returns(_ => []);
        _imageRenderer.Render(Arg.Any<LayoutInput>(), Arg.Any<Template>(), Arg.Any<ResolvedMapping>(), Arg.Any<bool>())
            .Returns(_ => []);
    }

    // ---- fixtures ----

    private void StubFilter(params LayoutInput[] inputs) => StubFilter(inputs, []);

    private void StubFilter(IReadOnlyList<LayoutInput> inputs, IReadOnlyList<LayoutGroupOverlay> overlays) =>
        _layoutFilter.Filter(Arg.Any<Template>(), Arg.Any<VisibilityContext>())
            .Returns(new FilteredLayout(inputs, overlays));

    // ---- tests ----

    [Fact]
    public void Render_EmptyFilteredLayout_ReturnsEmptyResult()
    {
        // given the filter returns no inputs and no group overlays
        StubFilter();

        // when the service renders
        var result = _underTest.Render(TemplateOf(), EmptyMapping(), LabelsOf());

        // then both output lists are empty
        result.Labels.ShouldBeEmpty();
        result.Images.ShouldBeEmpty();
    }

    [Fact]
    public void Render_AggregatesImagesAndLabels_AcrossInputs()
    {
        // given two inputs, each emitting an image and a label from their respective renderers
        var inputA = Input("ButtonA");
        var inputB = Input("ButtonB");
        var liA = new LayoutInput(inputA, YOffset: 0, Flags: VisibilityFlags.None);
        var liB = new LayoutInput(inputB, YOffset: 0, Flags: VisibilityFlags.None);
        StubFilter(liA, liB);

        var imageA = new RenderedImage(Source: "a.png");
        var imageB = new RenderedImage(Source: "b.png");
        var labelA = new RenderedLabel(0, 0, "A-text", "Left", 12);
        var labelB = new RenderedLabel(0, 0, "B-text", "Left", 12);
        _imageRenderer.Render(liA, Arg.Any<Template>(), Arg.Any<ResolvedMapping>(), Arg.Any<bool>()).Returns([imageA]);
        _imageRenderer.Render(liB, Arg.Any<Template>(), Arg.Any<ResolvedMapping>(), Arg.Any<bool>()).Returns([imageB]);
        _labelRenderer.Render(inputA, Arg.Any<string?>()).Returns([labelA]);
        _labelRenderer.Render(inputB, Arg.Any<string?>()).Returns([labelB]);

        // when the service renders
        var result = _underTest.Render(TemplateOf(), EmptyMapping(), LabelsOf());

        // then the per-input outputs are concatenated in iteration order
        result.Images.ShouldBe([imageA, imageB]);
        result.Labels.ShouldBe([labelA, labelB]);
    }

    [Fact]
    public void Render_PassesLabelTextLookupToLabelRenderer()
    {
        // given a label text dictionary that only covers one of the two inputs
        var inputA = Input("ButtonA");
        var inputB = Input("ButtonB");
        var liA = new LayoutInput(inputA, YOffset: 0, Flags: VisibilityFlags.None);
        var liB = new LayoutInput(inputB, YOffset: 0, Flags: VisibilityFlags.None);
        StubFilter(liA, liB);

        // when the service renders with labels for ButtonA only
        _underTest.Render(TemplateOf(), EmptyMapping(), LabelsOf(entries: ("ButtonA", "Jump")));

        // then the label renderer receives the matched text for A and null for B
        _labelRenderer.Received(1).Render(inputA, "Jump");
        _labelRenderer.Received(1).Render(inputB, null);
    }

    [Fact]
    public void Render_NonZeroYOffset_ShiftsLabelTop_ButNotImageTop()
    {
        // given an input with YOffset = -40 that emits a label at Top=100
        // (the service shifts labels here; the image renderer is expected to have already baked
        // YOffset into its image positions, so the service does not touch image Top)
        var input = Input("ButtonA");
        var li = new LayoutInput(input, YOffset: -40, Flags: VisibilityFlags.None);
        StubFilter(li);

        var label = new RenderedLabel(Left: 0, Top: 100, Text: "A", Alignment: "Left", FontSize: 12);
        var image = new RenderedImage(Source: "a.png", Top: 50); // pretend image renderer already baked YOffset
        _labelRenderer.Render(input, Arg.Any<string?>()).Returns([label]);
        _imageRenderer.Render(li, Arg.Any<Template>(), Arg.Any<ResolvedMapping>(), Arg.Any<bool>()).Returns([image]);

        // when the service renders
        var result = _underTest.Render(TemplateOf(), EmptyMapping(), LabelsOf());

        // then the label's Top is shifted by YOffset, but the image's Top is unchanged
        result.Labels.Single().Top.ShouldBe(60); // 100 + (-40)
        result.Images.Single().Top.ShouldBe(50); // untouched
    }

    [Fact]
    public void Render_VisibleGroupOverlay_EmittedAtFullOpacityAndNoBlur()
    {
        // given a group overlay whose aggregated flags satisfy its ShowIf
        var overlay = new OverlayDefinition(X: 10, Y: 20, Source: "dpad.png");
        var group = new LayoutGroupOverlay(overlay, Flags: new VisibilityFlags(HasLabel: true, IsMapped: true));
        StubFilter([], [group]);

        // when the service renders
        var result = _underTest.Render(TemplateOf(), EmptyMapping(), LabelsOf());

        // then the overlay is rendered with Opacity=1.0 and no blur
        var rendered = result.Images.Single();
        rendered.Source.ShouldBe("dpad.png");
        rendered.Opacity.ShouldBe(1.0);
        rendered.BlurRadius.ShouldBe(0.0);
    }

    [Fact]
    public void Render_InvisibleGroupOverlay_UsesOverlayMinOpacityOverTemplateDefault()
    {
        // given an overlay whose ShowIf=Label isn't satisfied and that sets its own MinOpacity
        var overlay = new OverlayDefinition(
            X: 0, Y: 0, Source: "dpad.png",
            ShowIf: ShowIfCondition.Label,
            MinOpacity: 0.3,
            InactiveBlurRadius: 5);
        var group = new LayoutGroupOverlay(overlay, Flags: VisibilityFlags.None); // hasLabel=false
        StubFilter([], [group]);

        // when the service renders (template defaults set to 0.9 / 99 to prove the overlay's
        // own values win)
        var result = _underTest.Render(TemplateOf(defaultMinOpacity: 0.9, defaultInactiveBlurRadius: 99),
            EmptyMapping(), LabelsOf());

        // then the overlay's own MinOpacity / InactiveBlurRadius are used
        var rendered = result.Images.Single();
        rendered.Opacity.ShouldBe(0.3);
        rendered.BlurRadius.ShouldBe(5);
    }

    [Fact]
    public void Render_InvisibleGroupOverlayWithoutMinOpacity_FallsBackToTemplateDefault()
    {
        // given an overlay with no MinOpacity / InactiveBlurRadius of its own
        var overlay = new OverlayDefinition(
            X: 0, Y: 0, Source: "dpad.png",
            ShowIf: ShowIfCondition.Label); // MinOpacity defaults to null
        var group = new LayoutGroupOverlay(overlay, Flags: VisibilityFlags.None);
        StubFilter([], [group]);

        // when the service renders with template defaults
        var result = _underTest.Render(TemplateOf(defaultMinOpacity: 0.25, defaultInactiveBlurRadius: 8),
            EmptyMapping(), LabelsOf());

        // then the template defaults flow through
        var rendered = result.Images.Single();
        rendered.Opacity.ShouldBe(0.25);
        rendered.BlurRadius.ShouldBe(8);
    }

    [Fact]
    public void Render_GroupOverlayWithZeroEffectiveOpacity_IsExcluded()
    {
        // given an invisible overlay with MinOpacity=0 (e.g. authored to disappear entirely)
        var overlay = new OverlayDefinition(
            X: 0, Y: 0, Source: "dpad.png",
            ShowIf: ShowIfCondition.Label,
            MinOpacity: 0);
        var group = new LayoutGroupOverlay(overlay, Flags: VisibilityFlags.None);
        StubFilter([], [group]);

        // when the service renders
        var result = _underTest.Render(TemplateOf(), EmptyMapping(), LabelsOf());

        // then the overlay is dropped — no point emitting a fully-transparent image
        result.Images.ShouldBeEmpty();
    }
}
