using DynamicControls.InputMapping;
using NSubstitute;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;

namespace DynamicControls.Core.Tests.InputMapping;

/// <summary>
/// Unit tests for <see cref="InputMappingService"/>. All collaborators are substituted —
/// <see cref="IInputMappingProviders"/> stands in for the provider iteration (covered by
/// <see cref="InputMappingProvidersTests"/>) so each test states the natural and current config
/// directly. Focus: the pipeline shape (loader → Resolve → resolver → re-splice naturals), with
/// special attention to the natural-snapshot invariant when a provider reports a remap.
/// </summary>
public class InputMappingServiceTests
{
    private readonly IInputMappingLoader _loader = Substitute.For<IInputMappingLoader>();
    private readonly IInputMappingResolver _resolver = Substitute.For<IInputMappingResolver>();
    private readonly IInputMappingProviders _providers = Substitute.For<IInputMappingProviders>();
    private readonly InputMappingService _underTest;

    public InputMappingServiceTests()
    {
        _underTest = new(_loader, _resolver, _providers, new ControlResolver(Substitute.For<ILogger>()));
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

    /// <summary>The providers supply <paramref name="natural"/>, with <paramref name="current"/>
    /// as the player's mapping when it differs (a remap), or nothing remapped when it is null.</summary>
    private void StubSelection(InputMappingConfig natural, InputMappingConfig? current = null) =>
        _providers.Resolve(Arg.Any<GameInfo>(), Arg.Any<PlatformControllersConfig?>())
            .Returns(current == null ? ProvidedMapping.Unchanged(natural) : new ProvidedMapping(natural, current));

    private void ResolverReturns(ResolvedMapping mapping) =>
        _resolver.Resolve(Arg.Any<string>(), Arg.Any<InputMappingConfig>(), Arg.Any<string?>()).Returns(mapping);

    // ---- tests ----

    [Fact]
    public void Load_WithNothingRemapped_ReturnsTheMappingFromSingleResolverCall()
    {
        // given the providers yield one mapping, nothing remapped
        StubSelection(natural: MappingConfig(controller: "Pad", mappings: [("A", "ButtonA")]));
        var canned = Resolved(controller: "Pad");
        ResolverReturns(canned);

        // when the service loads
        var result = _underTest.Load(Game());

        // then the resolver's single result is returned unchanged — no second resolution because
        // there is no remap
        result.ShouldBeSameAs(canned);
        _resolver.Received(1).Resolve(Arg.Any<string>(), Arg.Any<InputMappingConfig>(), Arg.Any<string?>());
    }

    /// <summary>
    /// Pins the natural-snapshot invariant: when a provider reports a remap, the returned mapping
    /// must carry the *natural* state's NaturalButtonToInput / NaturalInputToButton even though its
    /// active ButtonToInput / InputToButton come from the current mapping. This is what lets the
    /// renderer detect "this button has been remapped" — the natural state is the comparison
    /// point. Each role uses a distinct dictionary instance so reference identity proves which
    /// mapping sourced which field.
    /// </summary>
    [Fact]
    public void Load_WithARemap_KeepsNaturalMapsFromTheNaturalState()
    {
        // given the providers yield a natural config and a current one that swaps A/B
        var baseline = MappingConfig(controller: "Pad", mappings: [("A", "ButtonA"), ("B", "ButtonB")]);
        var remapped = MappingConfig(controller: "Pad", mappings: [("A", "ButtonB"), ("B", "ButtonA")]);
        StubSelection(baseline, remapped);

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
        var remappedButtonToInput = new Dictionary<string, IReadOnlyList<string>>
        {
            ["A"] = ["ButtonB"],
            ["B"] = ["ButtonA"],
        };
        var remappedInputToButton = new Dictionary<string, string>
        {
            ["ButtonB"] = "A",
            ["ButtonA"] = "B",
        };
        _resolver.Resolve(Platform, baseline, "Pad")
            .Returns(Resolved("Pad", baselineButtonToInput, baselineInputToButton));
        _resolver.Resolve(Platform, remapped, "Pad")
            .Returns(Resolved("Pad", remappedButtonToInput, remappedInputToButton));

        // when the service loads
        var result = _underTest.Load(Game());

        // then the active maps come from the current mapping
        result.ButtonToInput.ShouldBeSameAs(remappedButtonToInput);
        result.InputToButton.ShouldBeSameAs(remappedInputToButton);

        // and the natural snapshots come from the natural state — the invariant under test
        result.NaturalButtonToInput.ShouldBeSameAs(baselineButtonToInput);
        result.NaturalInputToButton.ShouldBeSameAs(baselineInputToButton);
    }

    [Fact]
    public void Load_ForwardsLoaderPlatformControllersToTheProviders()
    {
        // given the loader produces a platform controllers config
        var platform = new PlatformControllersConfig
        {
            Controllers = [new ControllerConfig { Name = "Pad", IsDefault = true }],
        };
        _loader.LoadPlatformMapping(Platform).Returns(platform);
        StubSelection(natural: MappingConfig(controller: "Pad"));
        ResolverReturns(Resolved("Pad"));

        // when the service loads
        _underTest.Load(Game());

        // then the same platform instance is forwarded to the providers
        _providers.Received(1).Resolve(Arg.Any<GameInfo>(), platform);
    }
}
