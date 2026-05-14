using DynamicControls;
using DynamicControls.Templates;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace DynamicControls.Core.Tests.Templates;

/// <summary>
/// Unit tests for <see cref="TemplateService"/>. The service is a thin coordinator over three
/// collaborators (loader, image resolver, layout resolver), so the tests focus on how it wires their
/// outputs onto <see cref="Template"/> and on its caching behavior.
/// </summary>
public class TemplateServiceTests
{
    private readonly ITemplateLoader _loader = Substitute.For<ITemplateLoader>();
    private readonly ITemplateImageResolver _imageResolver = Substitute.For<ITemplateImageResolver>();
    private readonly ILayoutResolver _layoutResolver = Substitute.For<ILayoutResolver>();
    private readonly TemplateService _underTest;

    public TemplateServiceTests()
    {
        _underTest = new TemplateService(_loader, _imageResolver, _layoutResolver);
    }

    private static ResolvedLayout EmptyResolvedLayout() => new(
        Elements: [],
        InputDescendants: new Dictionary<InputDefinition, IReadOnlyList<InputDefinition>>(),
        CollapseInfo: new Dictionary<InputDefinition, CollapseInfo>(),
        DefaultFontSize: 28,
        DefaultMinOpacity: 0,
        DefaultInactiveBlurRadius: 0);

    [Fact]
    public void Load_BuildsTemplateFromCollaboratorOutputs()
    {
        // given a layout, layout-resolver result, and base image all stubbed
        var layout = new LayoutConfig();
        var input = new InputDefinition(
            Name: "ButtonA",
            InputImages: [],
            Overlays: [],
            Labels: [],
            Children: []);
        var resolvedLayout = new ResolvedLayout(
            Elements: [input],
            InputDescendants: new Dictionary<InputDefinition, IReadOnlyList<InputDefinition>>
            {
                [input] = [],
            },
            CollapseInfo: new Dictionary<InputDefinition, CollapseInfo>(),
            DefaultFontSize: 22,
            DefaultMinOpacity: 0.4,
            DefaultInactiveBlurRadius: 3);
        var baseImage = new BaseImage(Path: "Templates/x/BaseImage.png", Width: 800, Height: 600);

        _loader.LoadLayout("x").Returns(layout);
        _layoutResolver.Resolve(layout, Arg.Any<ITemplateImageSource>()).Returns(resolvedLayout);
        _imageResolver.FindBaseImage("x").Returns(baseImage);

        // when the service loads the template
        Template template = _underTest.Load("x");

        // then every collaborator's output surfaces on the returned Template
        template.Name.ShouldBe("x");
        template.Layout.ShouldBe(resolvedLayout);
        template.BaseImage.ShouldBe(baseImage);
        template.ImageSource.ShouldNotBeNull();
    }

    [Fact]
    public void Load_NoBaseImage_LeavesBaseImageNull()
    {
        // given the image resolver cannot find a base image
        _loader.LoadLayout("x").Returns(new LayoutConfig());
        _layoutResolver.Resolve(Arg.Any<LayoutConfig>(), Arg.Any<ITemplateImageSource>())
            .Returns(EmptyResolvedLayout());
        _imageResolver.FindBaseImage("x").ReturnsNull();

        // when the service loads the template
        Template template = _underTest.Load("x");

        // then BaseImage surfaces as null — the canvas-default fallback is the consumer's job,
        // not the template's
        template.BaseImage.ShouldBeNull();
    }

    [Fact]
    public void Load_NullLayoutFromLoader_PassesEmptyConfigToResolver()
    {
        // given the loader returns null (no Layout.xml on disk)
        _loader.LoadLayout("x").ReturnsNull();
        _layoutResolver.Resolve(Arg.Any<LayoutConfig>(), Arg.Any<ITemplateImageSource>())
            .Returns(EmptyResolvedLayout());

        // when the service loads the template
        _underTest.Load("x");

        // then the resolver is invoked with a fresh empty config (no elements, no head)
        _layoutResolver.Received(1).Resolve(
            Arg.Is<LayoutConfig>(c => c.Elements.Count == 0),
            Arg.Any<ITemplateImageSource>());
    }

    [Fact]
    public void Load_Twice_ReturnsCachedInstanceAndDoesNotReinvokeCollaborators()
    {
        // given a template name that has been loaded once
        _loader.LoadLayout("x").Returns(new LayoutConfig());
        _layoutResolver.Resolve(Arg.Any<LayoutConfig>(), Arg.Any<ITemplateImageSource>())
            .Returns(EmptyResolvedLayout());
        Template first = _underTest.Load("x");

        // when the service is asked for the same name again
        Template second = _underTest.Load("x");

        // then the same instance is returned and no collaborator is invoked a second time
        second.ShouldBeSameAs(first);
        _loader.Received(1).LoadLayout("x");
        _layoutResolver.Received(1).Resolve(Arg.Any<LayoutConfig>(), Arg.Any<ITemplateImageSource>());
        _imageResolver.Received(1).FindBaseImage("x");
    }

    [Fact]
    public void Load_DifferentNames_AreCachedSeparately()
    {
        // given two distinct template names with distinct stubbed outputs
        _loader.LoadLayout(Arg.Any<string>()).Returns(new LayoutConfig());
        _layoutResolver.Resolve(Arg.Any<LayoutConfig>(), Arg.Any<ITemplateImageSource>())
            .Returns(EmptyResolvedLayout());

        // when the service loads each name
        Template x = _underTest.Load("x");
        Template y = _underTest.Load("y");

        // then they are distinct Template instances carrying their own names
        x.ShouldNotBeSameAs(y);
        x.Name.ShouldBe("x");
        y.Name.ShouldBe("y");
        _loader.Received(1).LoadLayout("x");
        _loader.Received(1).LoadLayout("y");
    }
}
