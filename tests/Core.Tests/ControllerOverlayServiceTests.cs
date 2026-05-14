using DynamicControls.InputMapping;
using DynamicControls.Labels;
using DynamicControls.Rendering;
using DynamicControls.Static;
using DynamicControls.Templates;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;
using static DynamicControls.Core.TestHelpers.InputMapping.MappingFixtures;
using static DynamicControls.Core.TestHelpers.Labels.LabelsFixtures;
using static DynamicControls.Core.TestHelpers.Templates.TemplateFixtures;

namespace DynamicControls.Core.Tests;

/// <summary>
/// Unit tests for <see cref="ControllerOverlayService"/>. The five collaborators are substitutes
/// so each test controls exactly which arm of the pipeline runs. Covers the no-template short
/// circuit, the static-image short circuit (which must skip every other service), the normal
/// rendering pipeline, the BaseImage-null canvas-defaults path, the exception-to-empty-model
/// safety net, and the Distinct() dedup on the rendered images list.
/// </summary>
public class ControllerOverlayServiceTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IInputLabelsService _inputLabelsService = Substitute.For<IInputLabelsService>();
    private readonly ITemplateService _templateService = Substitute.For<ITemplateService>();
    private readonly IStaticImageResolver _staticImageResolver = Substitute.For<IStaticImageResolver>();
    private readonly IInputMappingService _inputMappingService = Substitute.For<IInputMappingService>();
    private readonly IInputRenderingService _inputRenderingService = Substitute.For<IInputRenderingService>();

    private ControllerOverlayService BuildTestFixture(string? defaultTemplate = "Default") => new(
        _logger,
        _inputLabelsService,
        _templateService,
        _staticImageResolver,
        _inputMappingService,
        _inputRenderingService,
        defaultTemplate);

    // ---- minimal fixtures for the collaborators' outputs ----

    private static Template TemplateWith(BaseImage? baseImage) =>
        TemplateOf() with { Name = "Default", BaseImage = baseImage };

    private void StubPipeline(BaseImage? baseImage, RenderResult rendered)
    {
        _staticImageResolver.Find(Arg.Any<GameInfo>()).ReturnsNull();
        _inputMappingService.Load(Arg.Any<GameInfo>()).Returns(EmptyMapping());
        _inputLabelsService.Load(Arg.Any<GameInfo>(), Arg.Any<ResolvedMapping>()).Returns(EmptyLabels());
        _templateService.Load("Default").Returns(TemplateWith(baseImage));
        _inputRenderingService
            .Render(Arg.Any<Template>(), Arg.Any<ResolvedMapping>(), Arg.Any<ResolvedLabels>())
            .Returns(rendered);
    }

    // ---- tests ----

    [Fact]
    public void Resolve_NoDefaultTemplate_ReturnsEmptyModel_AndSkipsEntirePipeline()
    {
        // given a service configured without a default template name
        var underTest = BuildTestFixture(defaultTemplate: null);

        // when the service resolves
        var result = underTest.Resolve(Game());

        // then an empty model is returned and no collaborator is consulted
        result.ImagePath.ShouldBeNull();
        _staticImageResolver.DidNotReceive().Find(Arg.Any<GameInfo>());
        _inputMappingService.DidNotReceive().Load(Arg.Any<GameInfo>());
        _templateService.DidNotReceive().Load(Arg.Any<string>());
    }

    [Fact]
    public void Resolve_StaticImageFound_ShortCircuitsBeforeRendering()
    {
        // given a static image resolver that returns a path
        _staticImageResolver.Find(Arg.Any<GameInfo>()).Returns(@"C:\images\game.png");
        var underTest = BuildTestFixture();

        // when the service resolves
        var result = underTest.Resolve(Game());

        // then the model carries the static image path and no other pipeline runs
        result.ImagePath.ShouldBe(@"C:\images\game.png");
        _inputMappingService.DidNotReceive().Load(Arg.Any<GameInfo>());
        _inputLabelsService.DidNotReceive().Load(Arg.Any<GameInfo>(), Arg.Any<ResolvedMapping>());
        _templateService.DidNotReceive().Load(Arg.Any<string>());
        _inputRenderingService.DidNotReceive().Render(
            Arg.Any<Template>(), Arg.Any<ResolvedMapping>(), Arg.Any<ResolvedLabels>());
    }

    [Fact]
    public void Resolve_NoStaticImage_RunsPipelineAndPopulatesModel()
    {
        // given a full pipeline: no static image, a template with a base image, a render result
        var label = new RenderedLabel(Left: 10, Top: 20, Text: "Jump", Alignment: "Left", FontSize: 14);
        var image = new RenderedImage(Source: @"C:\template\ButtonA.png", Left: 5, Top: 6);
        StubPipeline(
            baseImage: new BaseImage(@"C:\template\BaseImage.png", Width: 800, Height: 500),
            rendered: new RenderResult(Labels: [label], Images: [image]));

        // when the service resolves
        var result = BuildTestFixture().Resolve(Game());

        // then every field is populated from the corresponding pipeline output
        result.ImagePath.ShouldBe(@"C:\template\BaseImage.png");
        result.CanvasWidth.ShouldBe(800);
        result.CanvasHeight.ShouldBe(500);
        result.InputLabels.ShouldBe([label]);
        result.RenderedImages.ShouldBe([image]);
    }

    [Fact]
    public void Resolve_TemplateWithoutBaseImage_FallsBackToRenderingDefaults()
    {
        // given a template with no BaseImage discovered
        StubPipeline(
            baseImage: null,
            rendered: new RenderResult(Labels: [], Images: []));

        // when the service resolves
        var result = BuildTestFixture().Resolve(Game());

        // then ImagePath is null and the canvas falls back to the rendering defaults
        result.ImagePath.ShouldBeNull();
        result.CanvasWidth.ShouldBe(RenderingDefaults.CanvasWidth);
        result.CanvasHeight.ShouldBe(RenderingDefaults.CanvasHeight);
    }

    [Fact]
    public void Resolve_PipelineThrows_LogsAndReturnsEmptyModel()
    {
        // given the mapping service throws unexpectedly mid-pipeline
        _staticImageResolver.Find(Arg.Any<GameInfo>()).ReturnsNull();
        _inputMappingService.Load(Arg.Any<GameInfo>()).Throws(new InvalidOperationException("boom"));

        // when the service resolves
        var result = BuildTestFixture().Resolve(Game());

        // then the empty model is returned and the exception is logged (not propagated)
        result.ImagePath.ShouldBeNull();
        result.InputLabels.ShouldBeEmpty();
        result.RenderedImages.ShouldBeEmpty();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("boom")));
    }

    [Fact]
    public void Resolve_PipelineThrows_WithInnerException_LogsInnerExceptionDetails()
    {
        // given the pipeline throws an exception that wraps an inner exception
        _staticImageResolver.Find(Arg.Any<GameInfo>()).ReturnsNull();
        _inputMappingService.Load(Arg.Any<GameInfo>())
            .Throws(new InvalidOperationException("outer", new Exception("inner detail")));

        // when the service resolves
        var result = BuildTestFixture().Resolve(Game());

        // then the empty model is returned and the inner exception message is logged
        result.ImagePath.ShouldBeNull();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("inner detail")));
    }

    [Fact]
    public void Resolve_GameWithCloneOf_IncludesCloneOfInLog()
    {
        // given a game that is a clone of another (e.g. a regional variant)
        GameInfo game = Game() with { CloneOf = "sf2" };
        StubPipeline(baseImage: null, rendered: new RenderResult(Labels: [], Images: []));

        // when the service resolves
        BuildTestFixture().Resolve(game);

        // then the CloneOf value appears in the platform/ROM log line
        _logger.Received().Debug(Arg.Is<string>(s => s.Contains("sf2")));
    }

    [Fact]
    public void Resolve_DuplicateRenderedImages_DedupedByStructuralEquality()
    {
        // given a render result with two structurally identical images
        var duplicate = new RenderedImage(Source: @"C:\template\ButtonA.png", Left: 5, Top: 6);
        var distinct = new RenderedImage(Source: @"C:\template\ButtonB.png", Left: 7, Top: 8);
        StubPipeline(
            baseImage: new BaseImage(@"C:\template\BaseImage.png", 800, 500),
            rendered: new RenderResult(
                Labels: [],
                Images: [duplicate, distinct, duplicate]));

        // when the service resolves
        var result = BuildTestFixture().Resolve(Game());

        // then `Distinct()` collapses the structurally-equal duplicates — RenderedImage is a
        // record so structural equality determines the dedup
        result.RenderedImages.ShouldBe([duplicate, distinct]);
    }
}
