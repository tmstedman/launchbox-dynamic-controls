using DynamicControls.Config;
using DynamicControls.InputMapping;
using NSubstitute;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;

namespace DynamicControls.Core.Tests.InputMapping;

/// <summary>
/// Unit tests for <see cref="InputMappingPlugins"/>. Two responsibilities:
/// (1) IsEnabled filtering at construction — disabled underTest are dropped from the chains and
/// never consulted at runtime; (2) priority-order selection at call time — SelectSource and
/// SelectTransform iterate their respective filtered chains and return the first non-null
/// contribution, falling back to an empty config (sources) or null (transforms) when none apply.
/// Substitutes stand in for the source/transform implementations so each test states the
/// IsEnabled and Load/Transform return values directly.
/// </summary>
public class InputMappingPluginsTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly GlobalConfig _config = new();

    // ---- helpers ----

    private static IInputMappingSource Source(bool enabled, InputMappingConfig? returns)
    {
        var source = Substitute.For<IInputMappingSource>();
        source.IsEnabled(Arg.Any<GlobalConfig>()).Returns(enabled);
        source.Load(Arg.Any<GameInfo>(), Arg.Any<PlatformControllersConfig?>()).Returns(returns);
        return source;
    }

    private static IInputMappingTransform Transform(bool enabled, InputMappingConfig? returns)
    {
        var transform = Substitute.For<IInputMappingTransform>();
        transform.IsEnabled(Arg.Any<GlobalConfig>()).Returns(enabled);
        transform.Transform(Arg.Any<GameInfo>(), Arg.Any<InputMappingConfig>()).Returns(returns);
        return transform;
    }

    private InputMappingPlugins Build(
        IReadOnlyList<IInputMappingSource>? sources = null,
        IReadOnlyList<IInputMappingTransform>? transforms = null) =>
        new(_logger, sources ?? [], transforms ?? [], _config);

    // ---- SelectSource ----

    [Fact]
    public void SelectSource_NoSources_ReturnsEmptyConfig()
    {
        // given a underTest instance with no sources registered
        var underTest = Build();

        // when SelectSource runs
        InputMappingConfig result = underTest.SelectSource(Game(), platform: null);

        // then a default-constructed config falls out (no controller, no mappings)
        result.Controller.ShouldBeNull();
        result.Mappings.ShouldBeEmpty();
    }

    [Fact]
    public void SelectSource_FirstNonNullWins_LaterSourcesNotConsulted()
    {
        // given three sources: first returns null (skipped), second wins, third must never be
        // consulted because iteration stops at the first non-null
        var nulled = Source(enabled: true, returns: null);
        var winner = Source(enabled: true, returns: MappingConfig(controller: "Pad"));
        var unreached = Source(enabled: true, returns: MappingConfig(controller: "Should Not Be Used"));
        var underTest = Build(sources: [nulled, winner, unreached]);

        // when SelectSource runs
        InputMappingConfig result = underTest.SelectSource(Game(), platform: null);

        // then the null source was tried, the winner was tried, the third was skipped
        nulled.Received(1).Load(Arg.Any<GameInfo>(), Arg.Any<PlatformControllersConfig?>());
        winner.Received(1).Load(Arg.Any<GameInfo>(), Arg.Any<PlatformControllersConfig?>());
        unreached.DidNotReceive().Load(Arg.Any<GameInfo>(), Arg.Any<PlatformControllersConfig?>());
        result.Controller.ShouldBe("Pad");
    }

    [Fact]
    public void SelectSource_DisabledSource_IsNeverConsulted()
    {
        // given two sources: the first is disabled (would otherwise win), the second is enabled
        var disabled = Source(enabled: false, returns: MappingConfig(controller: "Disabled"));
        var enabled = Source(enabled: true, returns: MappingConfig(controller: "Enabled"));
        var underTest = Build(sources: [disabled, enabled]);

        // when SelectSource runs
        InputMappingConfig result = underTest.SelectSource(Game(), platform: null);

        // then the disabled source is filtered out at construction; the enabled source wins
        disabled.DidNotReceive().Load(Arg.Any<GameInfo>(), Arg.Any<PlatformControllersConfig?>());
        result.Controller.ShouldBe("Enabled");
    }

    [Fact]
    public void SelectSource_ForwardsPlatformControllers()
    {
        // given a platform controllers config
        var platform = new PlatformControllersConfig
        {
            Controllers = [new ControllerConfig { Name = "Pad", IsDefault = true }],
        };
        var source = Source(enabled: true, returns: MappingConfig(controller: "Pad"));
        var underTest = Build(sources: [source]);

        // when SelectSource runs with the platform argument
        underTest.SelectSource(Game(), platform);

        // then the same platform instance is forwarded to the source
        source.Received(1).Load(Arg.Any<GameInfo>(), platform);
    }

    // ---- SelectTransform ----

    [Fact]
    public void SelectTransform_NoTransforms_ReturnsNull()
    {
        // given no transforms registered
        var underTest = Build();

        // when SelectTransform runs
        InputMappingConfig? result = underTest.SelectTransform(Game(), MappingConfig(controller: "Pad"));

        // then null falls out — there is no transform to apply
        result.ShouldBeNull();
    }

    [Fact]
    public void SelectTransform_FirstNonNullWins_LaterTransformsNotConsulted()
    {
        // given three transforms: first returns null, second wins, third must never be consulted
        var nulled = Transform(enabled: true, returns: null);
        var winner = Transform(enabled: true, returns: MappingConfig(controller: "Pad", mappings: [("A", "ButtonB")]));
        var unreached = Transform(enabled: true, returns: MappingConfig(controller: "Pad", mappings: [("A", "ButtonC")]));
        var underTest = Build(transforms: [nulled, winner, unreached]);

        // when SelectTransform runs
        InputMappingConfig? result = underTest.SelectTransform(Game(), MappingConfig(controller: "Pad"));

        // then the null transform was tried, the winner was tried, the third was skipped
        nulled.Received(1).Transform(Arg.Any<GameInfo>(), Arg.Any<InputMappingConfig>());
        winner.Received(1).Transform(Arg.Any<GameInfo>(), Arg.Any<InputMappingConfig>());
        unreached.DidNotReceive().Transform(Arg.Any<GameInfo>(), Arg.Any<InputMappingConfig>());
        result.ShouldNotBeNull();
        result.Mappings.Single().Input.ShouldBe("ButtonB");
    }

    [Fact]
    public void SelectTransform_DisabledTransform_IsNeverConsulted()
    {
        // given two transforms: the first is disabled (would otherwise win), the second enabled
        var disabled = Transform(enabled: false, returns: MappingConfig(controller: "Pad", mappings: [("A", "ButtonZ")]));
        var enabled = Transform(enabled: true, returns: MappingConfig(controller: "Pad", mappings: [("A", "ButtonY")]));
        var underTest = Build(transforms: [disabled, enabled]);

        // when SelectTransform runs
        InputMappingConfig? result = underTest.SelectTransform(Game(), MappingConfig(controller: "Pad"));

        // then the disabled transform is filtered out at construction; the enabled one wins
        disabled.DidNotReceive().Transform(Arg.Any<GameInfo>(), Arg.Any<InputMappingConfig>());
        result.ShouldNotBeNull();
        result.Mappings.Single().Input.ShouldBe("ButtonY");
    }
}
