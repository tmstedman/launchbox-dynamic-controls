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

    [Fact]
    public void Resolve_ControlSets_EachRenderedIntoItsOwnModel_BesideTheDefault()
    {
        // given labels whose default carries two control sets, each rendering its own labels
        ResolvedLabels plane = EmptyLabels() with { SetName = "Light Plane" };
        ResolvedLabels belt = EmptyLabels() with { SetName = "Rocket Belt" };
        ResolvedLabels labels = EmptyLabels() with { ControlSets = [plane, belt] };
        StubPipeline(
            baseImage: new BaseImage(@"C:\template\BaseImage.png", Width: 800, Height: 500),
            rendered: new RenderResult(Labels: [Label("Turn Left")], Images: []));
        _inputLabelsService.Load(Arg.Any<GameInfo>(), Arg.Any<ResolvedMapping>()).Returns(labels);
        _inputRenderingService.Render(Arg.Any<Template>(), Arg.Any<ResolvedMapping>(), plane)
            .Returns(new RenderResult(Labels: [Label("Increase Throttle")], Images: []));
        _inputRenderingService.Render(Arg.Any<Template>(), Arg.Any<ResolvedMapping>(), belt)
            .Returns(new RenderResult(Labels: [Label("Fast Jets Throttle")], Images: []));

        // when the service resolves
        var result = BuildTestFixture().Resolve(Game());

        // then the model is the default, and each set is a model of its own, in order, drawn on
        // the same base image and canvas, with the mapping resolved once for all of them
        result.SetName.ShouldBeNull();
        result.InputLabels.Select(l => l.Text).ShouldBe(["Turn Left"]);
        result.ControlSets.Select(s => (s.SetName, s.InputLabels.Single().Text, s.ImagePath, s.CanvasWidth)).ShouldBe(
        [
            ("Light Plane", "Increase Throttle", @"C:\template\BaseImage.png", 800),
            ("Rocket Belt", "Fast Jets Throttle", @"C:\template\BaseImage.png", 800),
        ]);
        result.ControlSets.ShouldAllBe(s => s.ControlSets.Count == 0);
        _inputMappingService.Received(1).Load(Arg.Any<GameInfo>());
    }

    [Fact]
    public void Resolve_HasGameLabels_IsTrueOnlyWhereTheGameSpokeForAnInput()
    {
        // given a default that is the platform's alone (no input is the game's own), and a set
        // that names one
        ResolvedLabels belt = EmptyLabels() with { SetName = "Rocket Belt", GameLabelled = new HashSet<string> { "ButtonA" } };
        ResolvedLabels labels = EmptyLabels() with { ControlSets = [belt] };
        StubPipeline(baseImage: null, rendered: new RenderResult(Labels: [Label("Pause")], Images: []));
        _inputLabelsService.Load(Arg.Any<GameInfo>(), Arg.Any<ResolvedMapping>()).Returns(labels);

        // when the service resolves
        var result = BuildTestFixture().Resolve(Game());

        // then the default says it has none of the game's own, and the set that does says so
        result.HasGameLabels.ShouldBeFalse();
        result.ControlSets.Single().HasGameLabels.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_HasGameLabels_IsTrueForADefaultTheGameWrote()
    {
        // given a default with an input of the game's own
        ResolvedLabels labels = EmptyLabels() with { GameLabelled = new HashSet<string> { "ButtonA" } };
        StubPipeline(baseImage: null, rendered: new RenderResult(Labels: [Label("Jump")], Images: []));
        _inputLabelsService.Load(Arg.Any<GameInfo>(), Arg.Any<ResolvedMapping>()).Returns(labels);

        // when the service resolves
        // then the overlay says so
        BuildTestFixture().Resolve(Game()).HasGameLabels.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_ControlSetThrows_IsLoggedAndLeftEmptyUnderItsName_TheRestStillDrawn()
    {
        // given two control sets, the first of which fails to render
        ResolvedLabels plane = EmptyLabels() with { SetName = "Light Plane" };
        ResolvedLabels belt = EmptyLabels() with { SetName = "Rocket Belt" };
        ResolvedLabels labels = EmptyLabels() with { ControlSets = [plane, belt] };
        StubPipeline(
            baseImage: new BaseImage(@"C:\template\BaseImage.png", Width: 800, Height: 500),
            rendered: new RenderResult(Labels: [Label("Turn Left")], Images: []));
        _inputLabelsService.Load(Arg.Any<GameInfo>(), Arg.Any<ResolvedMapping>()).Returns(labels);
        _inputRenderingService.Render(Arg.Any<Template>(), Arg.Any<ResolvedMapping>(), plane)
            .Throws(new InvalidOperationException("boom"));
        _inputRenderingService.Render(Arg.Any<Template>(), Arg.Any<ResolvedMapping>(), belt)
            .Returns(new RenderResult(Labels: [Label("Fast Jets Throttle")], Images: []));

        // when the service resolves
        var result = BuildTestFixture().Resolve(Game());

        // then the default and the other set are drawn as ever, and the failed set stays in its
        // place under its name with nothing on it, so the sets keep their order and count
        result.InputLabels.Select(l => l.Text).ShouldBe(["Turn Left"]);
        result.ControlSets.Select(s => (s.SetName, s.InputLabels.Count, s.RenderedImages.Count, s.ImagePath)).ShouldBe(
        [
            ("Light Plane", 0, 0, @"C:\template\BaseImage.png"),
            ("Rocket Belt", 1, 0, @"C:\template\BaseImage.png"),
        ]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("Light Plane")));
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("boom")));
    }

    [Fact]
    public void Resolve_DefaultThrowsOnAGameWithSets_IsLoggedAndLeftEmpty_TheSetsStillDrawn()
    {
        // given a default that fails to render, and a control set that renders
        ResolvedLabels belt = EmptyLabels() with { SetName = "Rocket Belt" };
        ResolvedLabels labels = EmptyLabels() with { ControlSets = [belt] };
        StubPipeline(
            baseImage: new BaseImage(@"C:\template\BaseImage.png", Width: 800, Height: 500),
            rendered: new RenderResult(Labels: [], Images: []));
        _inputLabelsService.Load(Arg.Any<GameInfo>(), Arg.Any<ResolvedMapping>()).Returns(labels);
        _inputRenderingService.Render(Arg.Any<Template>(), Arg.Any<ResolvedMapping>(), labels)
            .Throws(new InvalidOperationException("boom"));
        _inputRenderingService.Render(Arg.Any<Template>(), Arg.Any<ResolvedMapping>(), belt)
            .Returns(new RenderResult(Labels: [Label("Fast Jets Throttle")], Images: []));

        // when the service resolves
        var result = BuildTestFixture().Resolve(Game());

        // then the default is left empty on the base image, and the set, which is what the pause
        // screen shows, is drawn as ever
        result.ImagePath.ShouldBe(@"C:\template\BaseImage.png");
        result.InputLabels.ShouldBeEmpty();
        result.ControlSets.Select(s => (s.SetName, s.InputLabels.Single().Text)).ShouldBe([("Rocket Belt", "Fast Jets Throttle")]);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("The default")));
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("boom")));
    }

    private static RenderedLabel Label(string text) =>
        new(Left: 0, Top: 0, Text: text, Alignment: "Left", FontSize: 14);
}
