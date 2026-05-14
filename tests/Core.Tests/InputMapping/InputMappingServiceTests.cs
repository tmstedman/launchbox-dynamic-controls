using DynamicControls.InputMapping;
using NSubstitute;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;

namespace DynamicControls.Core.Tests.InputMapping;

/// <summary>
/// Unit tests for <see cref="InputMappingService"/>. All collaborators are substituted —
/// <see cref="IInputMappingPlugins"/> stands in for the source/transform iteration (covered by
/// <see cref="InputMappingPluginsTests"/>) so each test states the chosen baseline and transformed
/// config directly. Focus: the pipeline shape (loader → SelectSource → resolver → SelectTransform
/// → re-splice naturals), with special attention to the natural-snapshot invariant when a
/// transform applies.
/// </summary>
public class InputMappingServiceTests
{
    private readonly IInputMappingLoader _loader = Substitute.For<IInputMappingLoader>();
    private readonly IInputMappingResolver _resolver = Substitute.For<IInputMappingResolver>();
    private readonly IInputMappingPlugins _plugins = Substitute.For<IInputMappingPlugins>();
    private readonly InputMappingService _underTest;

    public InputMappingServiceTests()
    {
        _underTest = new(_loader, _resolver, _plugins);
    }

    // ---- fixture builders ----

    private static ResolvedMapping Resolved(
        string controller,
        Dictionary<string, IReadOnlyList<string>>? buttonToInput = null,
        Dictionary<string, string>? inputToButton = null) =>
        new(
            Platform: Platform,
            Controller: controller,
            ButtonToInput: buttonToInput ?? [],
            InputToButton: inputToButton ?? [],
            NaturalButtonToInput: buttonToInput ?? [],
            NaturalInputToButton: inputToButton ?? [],
            AnalogToDigital: null);

    private void StubSelection(InputMappingConfig baseline, InputMappingConfig? transformed = null)
    {
        _plugins.SelectSource(Arg.Any<GameInfo>(), Arg.Any<PlatformControllersConfig?>())
            .Returns(baseline);
        _plugins.SelectTransform(Arg.Any<GameInfo>(), Arg.Any<InputMappingConfig>())
            .Returns(transformed);
    }

    private void ResolverReturns(ResolvedMapping mapping) =>
        _resolver.Resolve(Arg.Any<string>(), Arg.Any<InputMappingConfig>(), Arg.Any<string?>()).Returns(mapping);

    // ---- tests ----

    [Fact]
    public void Load_WithNoTransform_ReturnsBaselineMappingFromSingleResolverCall()
    {
        // given the plugins yield a baseline and no transform
        StubSelection(baseline: MappingConfig(controller: "Pad", mappings: [("A", "ButtonA")]));
        var canned = Resolved(controller: "Pad");
        ResolverReturns(canned);

        // when the service loads
        var result = _underTest.Load(Game());

        // then the resolver's single result is returned unchanged — no second resolution because
        // there is no transform
        result.ShouldBeSameAs(canned);
        _resolver.Received(1).Resolve(Arg.Any<string>(), Arg.Any<InputMappingConfig>(), Arg.Any<string?>());
    }

    /// <summary>
    /// Pins the natural-snapshot invariant: when a transform applies, the returned mapping must
    /// carry the *baseline's* NaturalButtonToInput / NaturalInputToButton even though its active
    /// ButtonToInput / InputToButton come from the transformed mapping. This is what lets the
    /// renderer detect "this button has been remapped" — the natural state is the comparison
    /// point. Each role uses a distinct dictionary instance so reference identity proves which
    /// mapping sourced which field.
    /// </summary>
    [Fact]
    public void Load_WithTransform_KeepsNaturalMapsFromBaseline()
    {
        // given the plugins yield a baseline and a transformed config that swaps A/B
        var baseline = MappingConfig(controller: "Pad", mappings: [("A", "ButtonA"), ("B", "ButtonB")]);
        var transformed = MappingConfig(controller: "Pad", mappings: [("A", "ButtonB"), ("B", "ButtonA")]);
        StubSelection(baseline, transformed);

        // each resolver call returns distinct dictionary instances so we can prove by reference
        // identity which mapping sourced each field on the final result
        var baselineButtonToInput = new Dictionary<string, IReadOnlyList<string>>
        {
            ["A"] = ["ButtonA"],
            ["B"] = ["ButtonB"],
        };
        var baselineInputToButton = new Dictionary<string, string>
        {
            ["ButtonA"] = "A",
            ["ButtonB"] = "B",
        };
        var transformedButtonToInput = new Dictionary<string, IReadOnlyList<string>>
        {
            ["A"] = ["ButtonB"],
            ["B"] = ["ButtonA"],
        };
        var transformedInputToButton = new Dictionary<string, string>
        {
            ["ButtonB"] = "A",
            ["ButtonA"] = "B",
        };
        _resolver.Resolve(Platform, baseline, "Pad")
            .Returns(Resolved("Pad", baselineButtonToInput, baselineInputToButton));
        _resolver.Resolve(Platform, transformed, "Pad")
            .Returns(Resolved("Pad", transformedButtonToInput, transformedInputToButton));

        // when the service loads
        var result = _underTest.Load(Game());

        // then the active maps come from the transformed mapping
        result.ButtonToInput.ShouldBeSameAs(transformedButtonToInput);
        result.InputToButton.ShouldBeSameAs(transformedInputToButton);

        // and the natural snapshots come from the baseline — the invariant under test
        result.NaturalButtonToInput.ShouldBeSameAs(baselineButtonToInput);
        result.NaturalInputToButton.ShouldBeSameAs(baselineInputToButton);
    }

    [Fact]
    public void Load_TransformWithoutController_InheritsControllerFromBaseline()
    {
        // given the plugins yield a baseline with a controller and a transformed config without
        var baseline = MappingConfig(controller: "Pad", mappings: [("A", "ButtonA")]);
        var transformed = MappingConfig(controller: null, mappings: [("A", "ButtonB")]);
        StubSelection(baseline, transformed);
        ResolverReturns(Resolved("Pad"));

        // when the service loads
        _underTest.Load(Game());

        // then the resolver is invoked for the transformed config using the baseline's controller
        _resolver.Received(1).Resolve(Platform, transformed, "Pad");
    }

    [Fact]
    public void Load_ForwardsLoaderPlatformControllersToSelectSource()
    {
        // given the loader produces a platform controllers config
        var platform = new PlatformControllersConfig
        {
            Controllers = [new ControllerConfig { Name = "Pad", IsDefault = true }],
        };
        _loader.LoadPlatformMapping(Platform).Returns(platform);
        StubSelection(baseline: MappingConfig(controller: "Pad"));
        ResolverReturns(Resolved("Pad"));

        // when the service loads
        _underTest.Load(Game());

        // then the same platform instance is forwarded to plugins.SelectSource
        _plugins.Received(1).SelectSource(Arg.Any<GameInfo>(), platform);
    }
}
