using DynamicControls.Config;
using DynamicControls.InputMapping;
using NSubstitute;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;
using static DynamicControls.InputMapping.AnalogToDigitalMode;

namespace DynamicControls.Core.Tests.InputMapping;

/// <summary>
/// Unit tests for <see cref="PlatformDefaultMappingSource"/>. The source is a thin wrapper around
/// <see cref="PlatformControllersConfig.Resolve"/> — it picks the platform default controller and
/// copies its name, AnalogToDigital, and Mappings into a fresh <see cref="InputMappingConfig"/>.
/// </summary>
public class PlatformDefaultMappingSourceTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly PlatformDefaultMappingSource _underTest;

    public PlatformDefaultMappingSourceTests()
    {
        _underTest = new PlatformDefaultMappingSource(_logger);
    }

    [Fact]
    public void IsEnabled_AlwaysReturnsTrue()
    {
        _underTest.IsEnabled(new GlobalConfig()).ShouldBeTrue();
    }

    [Fact]
    public void Load_NullPlatform_ReturnsNull()
    {
        // given no platform controllers file exists
        // when the source loads
        var result = _underTest.Load(Game(), platform: null);

        // then the source contributes nothing
        result.ShouldBeNull();
    }

    [Fact]
    public void Load_PlatformWithNoControllers_ReturnsNull()
    {
        // given a platform config with an empty controllers list (Resolve returns null)
        var platform = PlatformConfig();

        // when the source loads
        var result = _underTest.Load(Game(), platform);

        // then the source contributes nothing
        result.ShouldBeNull();
    }

    [Fact]
    public void Load_ReturnsDefaultFlaggedController_WithFieldsCopied()
    {
        // given a platform with multiple controllers, one flagged default, with AnalogToDigital
        // and mapping entries on the default
        var platform = PlatformConfig(
            ControllerDef("Pad-3btn", mappings: [("A", "ButtonA")]),
            ControllerDef("Pad-6btn", isDefault: true, analogToDigital: Left,
                mappings: [("A", "ButtonA"), ("B", "ButtonB"), ("Start", "ButtonStart")]));

        // when the source loads
        var result = _underTest.Load(Game(), platform);

        // then the default-flagged controller is selected and its fields are copied
        result.ShouldNotBeNull();
        result.Controller.ShouldBe("Pad-6btn");
        result.AnalogToDigital.ShouldBe(Left);
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("A", "ButtonA"),
            ("B", "ButtonB"),
            ("Start", "ButtonStart"),
        ]);
    }

    [Fact]
    public void Load_NoControllerFlaggedDefault_FallsBackToFirstDeclared()
    {
        // given a platform with multiple controllers and none flagged default
        var platform = PlatformConfig(
            ControllerDef("FirstDeclared", mappings: [("A", "ButtonA")]),
            ControllerDef("Second", mappings: [("B", "ButtonB")]));

        // when the source loads
        var result = _underTest.Load(Game(), platform);

        // then the first declared controller is used as the implicit default
        result.ShouldNotBeNull();
        result.Controller.ShouldBe("FirstDeclared");
    }
}
