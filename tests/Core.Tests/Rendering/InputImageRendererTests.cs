using DynamicControls.InputMapping;
using DynamicControls.Rendering;
using DynamicControls.Templates;
using NSubstitute;
using static DynamicControls.Core.TestHelpers.InputMapping.MappingFixtures;
using static DynamicControls.Core.TestHelpers.Templates.LayoutElements;
using static DynamicControls.Core.TestHelpers.Templates.TemplateFixtures;

namespace DynamicControls.Core.Tests.Rendering;

/// <summary>
/// Unit tests for <see cref="InputImageRenderer"/>. Pins the visibility/opacity/blur logic for
/// both the images path (Source resolved per render via <see cref="IInputImageResolver"/>) and
/// the overlays path (Source already resolved at template build time). Also pins that YOffset
/// is baked into Top here, that zero-effective-opacity entries are excluded, and that images
/// are emitted before overlays.
/// </summary>
public class InputImageRendererTests
{
    private readonly IInputImageResolver _imageResolver = Substitute.For<IInputImageResolver>();
    private readonly InputImageRenderer _underTest;

    public InputImageRendererTests()
    {
        _underTest = new InputImageRenderer(_imageResolver);
        // Default resolver answer so unrelated tests don't have to stub it explicitly.
        _imageResolver.Resolve(
                Arg.Any<InputImageDefinition>(),
                Arg.Any<InputDefinition>(),
                Arg.Any<ResolvedMapping>(),
                Arg.Any<Template>())
            .Returns("resolved.png");
    }

    // ---- fixtures ----

    private static LayoutInput LayoutInputOf(
        InputDefinition input,
        double yOffset = 0,
        bool hasLabel = false,
        bool isMapped = false) =>
        new(input, yOffset, new VisibilityFlags(hasLabel, isMapped));

    // ---- tests ----

    [Fact]
    public void Render_NoImagesOrOverlays_ReturnsEmpty()
    {
        // given an input with neither images nor overlays
        var input = Input("ButtonA");
        var li = LayoutInputOf(input);

        // when the renderer runs
        var result = _underTest.Render(li, TemplateOf(), EmptyMapping(), isGameSpecific: false).ToList();

        // then nothing is emitted
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_VisibleImage_FullOpacityNoBlur_AndResolverSuppliesSource()
    {
        // given a visible image (ShowIf=Mapped + flags IsMapped) at known coordinates
        var image = new InputImageDefinition(X: 10, Y: 20, ImageFile: "x.png",
            ShowIf: ShowIfCondition.Mapped);
        var input = Input("ButtonA") with { InputImages = [image] };
        var li = LayoutInputOf(input, isMapped: true);
        _imageResolver.Resolve(image, input, Arg.Any<ResolvedMapping>(), Arg.Any<Template>())
            .Returns("/resolved/x.png");

        // when the renderer runs
        var rendered = _underTest.Render(li, TemplateOf(), EmptyMapping(), isGameSpecific: false).Single();

        // then the rendered image is emitted at full opacity, no blur, and the resolver-supplied
        // source. InputName flows through from the owning input.
        rendered.Source.ShouldBe("/resolved/x.png");
        rendered.Opacity.ShouldBe(1.0);
        rendered.BlurRadius.ShouldBe(0.0);
        rendered.Left.ShouldBe(10);
        rendered.Top.ShouldBe(20);
        rendered.InputName.ShouldBe("ButtonA");
    }

    [Fact]
    public void Render_InvisibleImage_UsesOwnMinOpacityAndBlur_OverTemplateDefaults()
    {
        // given an invisible image with its own MinOpacity / InactiveBlurRadius
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "x.png",
            ShowIf: ShowIfCondition.Mapped,
            MinOpacity: 0.4,
            InactiveBlurRadius: 7);
        var input = Input("ButtonA") with { InputImages = [image] };
        var li = LayoutInputOf(input, isMapped: false);

        // when the renderer runs (template defaults set high so we can prove they aren't used)
        var rendered = _underTest.Render(li,
            TemplateOf(defaultMinOpacity: 0.9, defaultInactiveBlurRadius: 99),
            EmptyMapping(), isGameSpecific: false).Single();

        // then the image's own values win
        rendered.Opacity.ShouldBe(0.4);
        rendered.BlurRadius.ShouldBe(7);
    }

    [Fact]
    public void Render_InvisibleImage_FallsBackToTemplateDefaults_WhenOwnIsNull()
    {
        // given an invisible image with no MinOpacity / InactiveBlurRadius of its own
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "x.png",
            ShowIf: ShowIfCondition.Mapped);
        var input = Input("ButtonA") with { InputImages = [image] };
        var li = LayoutInputOf(input, isMapped: false);

        // when the renderer runs with template defaults
        var rendered = _underTest.Render(li,
            TemplateOf(defaultMinOpacity: 0.3, defaultInactiveBlurRadius: 4),
            EmptyMapping(), isGameSpecific: false).Single();

        // then the template defaults flow through
        rendered.Opacity.ShouldBe(0.3);
        rendered.BlurRadius.ShouldBe(4);
    }

    [Fact]
    public void Render_ImageWithZeroEffectiveOpacity_IsExcluded()
    {
        // given an invisible image with MinOpacity=0 (authored to disappear entirely)
        var image = new InputImageDefinition(X: 0, Y: 0, ImageFile: "x.png",
            ShowIf: ShowIfCondition.Mapped,
            MinOpacity: 0);
        var input = Input("ButtonA") with { InputImages = [image] };
        var li = LayoutInputOf(input, isMapped: false);

        // when the renderer runs
        var result = _underTest.Render(li, TemplateOf(), EmptyMapping(), isGameSpecific: false);

        // then the image is dropped — no point emitting a fully-transparent image
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_BakesYOffsetIntoTop_ForBothImagesAndOverlays()
    {
        // given an input with one image and one overlay, both at Y=100, with YOffset=-40
        var image = new InputImageDefinition(X: 0, Y: 100, ImageFile: "x.png");
        var overlay = new OverlayDefinition(X: 0, Y: 100, Source: "overlay.png");
        var input = Input("ButtonA") with { InputImages = [image], Overlays = [overlay] };
        var li = LayoutInputOf(input, yOffset: -40);

        // when the renderer runs
        var rendered = _underTest.Render(li, TemplateOf(), EmptyMapping(), isGameSpecific: false).ToList();

        // then both elements have YOffset added to their Top — the renderer bakes the shift here
        // so the service downstream doesn't have to
        rendered.ShouldAllBe(r => r.Top == 60);
    }

    [Fact]
    public void Render_OverlayUsesPreResolvedSource_AndSameVisibilityRules()
    {
        // given an invisible overlay with its own MinOpacity — Source comes from the
        // OverlayDefinition itself (resolved at template build time), so the resolver shouldn't
        // be consulted
        var overlay = new OverlayDefinition(X: 0, Y: 0, Source: "/templates/dpad.png",
            ShowIf: ShowIfCondition.Mapped,
            MinOpacity: 0.5);
        var input = Input("ButtonA") with { Overlays = [overlay] };
        var li = LayoutInputOf(input, isMapped: false);

        // when the renderer runs
        var rendered = _underTest.Render(li, TemplateOf(), EmptyMapping(), isGameSpecific: false).Single();

        // then Source is the overlay's own value (no resolver call for overlays) and the same
        // visibility rules from the images path apply
        rendered.Source.ShouldBe("/templates/dpad.png");
        rendered.Opacity.ShouldBe(0.5);
        _imageResolver.DidNotReceive().Resolve(
            Arg.Any<InputImageDefinition>(),
            Arg.Any<InputDefinition>(),
            Arg.Any<ResolvedMapping>(),
            Arg.Any<Template>());
    }

    [Fact]
    public void Render_InvisibleOverlay_UsesOwnInactiveBlurRadius_OverTemplateDefault()
    {
        // given an invisible overlay with its own InactiveBlurRadius
        var overlay = new OverlayDefinition(X: 0, Y: 0, Source: "dpad.png",
            ShowIf: ShowIfCondition.Mapped,
            MinOpacity: 0.5,
            InactiveBlurRadius: 7);
        var input = Input("ButtonA") with { Overlays = [overlay] };
        var li = LayoutInputOf(input, isMapped: false);

        // when the renderer runs (template defaults set high so we can prove they aren't used)
        var rendered = _underTest.Render(li,
            TemplateOf(defaultInactiveBlurRadius: 99),
            EmptyMapping(), isGameSpecific: false).Single();

        // then the overlay's own InactiveBlurRadius wins over the template default
        rendered.BlurRadius.ShouldBe(7);
    }

    [Fact]
    public void Render_InvisibleOverlay_FallsBackToTemplateDefaults_WhenOwnIsNull()
    {
        // given an invisible overlay with no MinOpacity / InactiveBlurRadius of its own
        var overlay = new OverlayDefinition(X: 0, Y: 0, Source: "dpad.png",
            ShowIf: ShowIfCondition.Mapped);
        var input = Input("ButtonA") with { Overlays = [overlay] };
        var li = LayoutInputOf(input, isMapped: false);

        // when the renderer runs with template defaults
        var rendered = _underTest.Render(li,
            TemplateOf(defaultMinOpacity: 0.3, defaultInactiveBlurRadius: 4),
            EmptyMapping(), isGameSpecific: false).Single();

        // then the template defaults flow through for both opacity and blur
        rendered.Opacity.ShouldBe(0.3);
        rendered.BlurRadius.ShouldBe(4);
    }

    [Fact]
    public void Render_ImagesEmittedBeforeOverlays_InDocumentOrder()
    {
        // given an input with two images and two overlays, all visible
        var images = new[]
        {
            new InputImageDefinition(X: 1, Y: 0, ImageFile: "a.png"),
            new InputImageDefinition(X: 2, Y: 0, ImageFile: "b.png"),
        };
        var overlays = new[]
        {
            new OverlayDefinition(X: 3, Y: 0, Source: "ov1.png"),
            new OverlayDefinition(X: 4, Y: 0, Source: "ov2.png"),
        };
        var input = Input("ButtonA") with { InputImages = images, Overlays = overlays };
        var li = LayoutInputOf(input);

        // when the renderer runs
        var rendered = _underTest.Render(li, TemplateOf(), EmptyMapping(), isGameSpecific: false).ToList();

        // then all images come first (in source order), then all overlays (in source order)
        rendered.Select(r => r.Left).ShouldBe([1.0, 2.0, 3.0, 4.0]);
    }
}
