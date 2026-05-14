using DynamicControls.InputMapping;

namespace DynamicControls.Core.Tests.InputMapping;

public class PlatformControllersConfigTests
{
    private static ControllerConfig Controller(string name, bool isDefault = false) =>
        new() { Name = name, IsDefault = isDefault };

    [Fact]
    public void Resolve_NoControllers_ReturnsNull()
    {
        new PlatformControllersConfig().Resolve(preferredName: null).ShouldBeNull();
    }

    [Fact]
    public void Resolve_PreferredNameMatches_ReturnsThatController()
    {
        var pad = Controller("Pad");
        var config = new PlatformControllersConfig { Controllers = [pad, Controller("Zapper")] };

        config.Resolve(preferredName: "Pad").ShouldBe(pad);
    }

    [Fact]
    public void Resolve_PreferredNameMissing_ReturnsNull()
    {
        var config = new PlatformControllersConfig { Controllers = [Controller("Pad")] };

        config.Resolve(preferredName: "Zapper").ShouldBeNull();
    }

    [Fact]
    public void Resolve_NullPreferredName_ReturnsDefaultFlaggedController()
    {
        var pad = Controller("Pad");
        var zapper = Controller("Zapper", isDefault: true);
        var config = new PlatformControllersConfig { Controllers = [pad, zapper] };

        config.Resolve(preferredName: null).ShouldBe(zapper);
    }

    [Fact]
    public void Resolve_NullPreferredNameNoDefault_ReturnsFirstController()
    {
        var pad = Controller("Pad");
        var config = new PlatformControllersConfig { Controllers = [pad, Controller("Zapper")] };

        config.Resolve(preferredName: null).ShouldBe(pad);
    }
}
