using DynamicControls.Config;
using DynamicControls.InputMapping;
using NSubstitute;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;

namespace DynamicControls.Core.Tests.InputMapping;

/// <summary>
/// Unit tests for <see cref="InputMappingProviders"/>. Two responsibilities:
/// (1) IsEnabled filtering at construction — disabled providers are dropped from the chain and
/// never consulted at runtime; (2) priority order at call time — Resolve iterates the filtered
/// chain and returns what the first non-null provider supplies, falling back to an empty mapping
/// when none answer. Substitutes stand in for the provider implementations so each test states the
/// IsEnabled and Load return values directly.
/// </summary>
public class InputMappingProvidersTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly GlobalConfig _config = new();

    // ---- helpers ----

    private static IInputMappingProvider Provider(bool enabled, ProvidedMapping? returns)
    {
        var provider = Substitute.For<IInputMappingProvider>();
        provider.IsEnabled(Arg.Any<GlobalConfig>()).Returns(enabled);
        provider.Load(Arg.Any<GameInfo>(), Arg.Any<PlatformControllersConfig?>()).Returns(returns);
        return provider;
    }

    private static ProvidedMapping Supplies(string controller) =>
        ProvidedMapping.Unchanged(MappingConfig(controller: controller));

    private InputMappingProviders Build(IReadOnlyList<IInputMappingProvider>? providers = null) =>
        new(_logger, providers ?? [], _config);

    // ---- Resolve ----

    [Fact]
    public void Resolve_NoProviders_ReturnsAnEmptyMapping()
    {
        // given a chain with no providers registered
        var underTest = Build();

        // when Resolve runs
        ProvidedMapping result = underTest.Resolve(Game(), platform: null);

        // then an empty mapping falls out (no controller, no mappings), nothing remapped
        result.Current.Controller.ShouldBeNull();
        result.Current.Mappings.ShouldBeEmpty();
        result.Natural.ShouldBeSameAs(result.Current);
    }

    [Fact]
    public void Resolve_FirstNonNullWins_LaterProvidersNotConsulted()
    {
        // given three providers: first returns null (skipped), second wins, third must never be
        // consulted because iteration stops at the first non-null
        var nulled = Provider(enabled: true, returns: null);
        var winner = Provider(enabled: true, returns: Supplies("Pad"));
        var unreached = Provider(enabled: true, returns: Supplies("Should Not Be Used"));
        var underTest = Build([nulled, winner, unreached]);

        // when Resolve runs
        ProvidedMapping result = underTest.Resolve(Game(), platform: null);

        // then the null provider was tried, the winner was tried, the third was skipped
        nulled.Received(1).Load(Arg.Any<GameInfo>(), Arg.Any<PlatformControllersConfig?>());
        winner.Received(1).Load(Arg.Any<GameInfo>(), Arg.Any<PlatformControllersConfig?>());
        unreached.DidNotReceive().Load(Arg.Any<GameInfo>(), Arg.Any<PlatformControllersConfig?>());
        result.Current.Controller.ShouldBe("Pad");
    }

    [Fact]
    public void Resolve_ReturnsBothTheNaturalAndTheCurrentMappingTheWinnerSupplied()
    {
        // given a provider that reports a remap: the controller as declared, and the player's mapping
        var natural = MappingConfig(controller: "Pad", mappings: [("A", "ButtonA")]);
        var current = MappingConfig(controller: "Pad", mappings: [("A", "ButtonB")]);
        var underTest = Build([Provider(enabled: true, returns: new ProvidedMapping(natural, current))]);

        // when Resolve runs
        ProvidedMapping result = underTest.Resolve(Game(), platform: null);

        // then both reach the caller, unmerged, so the natural state can be spliced in
        result.Natural.ShouldBeSameAs(natural);
        result.Current.ShouldBeSameAs(current);
    }

    [Fact]
    public void Resolve_DisabledProvider_IsNeverConsulted()
    {
        // given two providers: the first is disabled (would otherwise win), the second is enabled
        var disabled = Provider(enabled: false, returns: Supplies("Disabled"));
        var enabled = Provider(enabled: true, returns: Supplies("Enabled"));
        var underTest = Build([disabled, enabled]);

        // when Resolve runs
        ProvidedMapping result = underTest.Resolve(Game(), platform: null);

        // then the disabled provider is filtered out at construction; the enabled one wins
        disabled.DidNotReceive().Load(Arg.Any<GameInfo>(), Arg.Any<PlatformControllersConfig?>());
        result.Current.Controller.ShouldBe("Enabled");
    }

    [Fact]
    public void Resolve_ForwardsPlatformControllers()
    {
        // given a platform controllers config
        var platform = new PlatformControllersConfig
        {
            Controllers = [new ControllerConfig { Name = "Pad", IsDefault = true }],
        };
        var provider = Provider(enabled: true, returns: Supplies("Pad"));
        var underTest = Build([provider]);

        // when Resolve runs with the platform argument
        underTest.Resolve(Game(), platform);

        // then the same platform instance is forwarded to the provider
        provider.Received(1).Load(Arg.Any<GameInfo>(), platform);
    }
}
