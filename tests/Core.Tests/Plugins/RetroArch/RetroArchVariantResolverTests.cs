using DynamicControls.Plugins.RetroArch;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Unit tests for <see cref="RetroArchVariantResolver"/>. The resolver is pure logic over a
/// <see cref="RetroArchGameData"/> and a <see cref="RetroArchCoreConfig"/> — no filesystem
/// dependencies. Focus: the null-return conditions (key absent, non-integer value, device type 1
/// absent from core config), the error path when a non-1 type is not declared in the core config,
/// correct selection when the type matches — including device type 1 when declared and selection
/// by a non-first id.
/// </summary>
public class RetroArchVariantResolverTests
{
    private const string Core = "Genesis Plus GX";

    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly RetroArchVariantResolver _underTest;

    public RetroArchVariantResolverTests()
    {
        _underTest = new RetroArchVariantResolver(_logger, "cfg");
    }

    // ---- fixtures ----

    private static RetroArchCoreConfig CoreConfig(params (string Name, int[] Ids)[] controllers) => new()
    {
        Controllers = [.. controllers.Select(c =>
            new RetroArchControllerConfig { Name = c.Name, RetropadIds = [.. c.Ids] })],
    };

    private static RetroArchGameData Data(Dictionary<string, string>? entries) =>
        new(null, null, null, entries);

    // ---- null-return conditions ----

    [Theory]
    [InlineData(null)]    // key absent
    [InlineData("1")]     // device type 1 not declared in core config — treated as no override
    [InlineData("auto")]  // non-integer value
    public void Resolve_KeyAbsentOrNonIntegerOrType1NotDeclared_ReturnsNull(string? deviceTypeValue)
    {
        var entries = deviceTypeValue == null
            ? []
            : new Dictionary<string, string> { ["input_libretro_device_p1"] = deviceTypeValue };

        var result = _underTest.Resolve(Data(entries), new RetroArchCoreConfig(), Core);

        result.ShouldBeNull();
        _logger.Received().Debug(Arg.Is<string>(s => s.Contains("no device type override")));
        _logger.DidNotReceive().Error(Arg.Any<string>());
    }

    // ---- error path ----

    [Fact]
    public void Resolve_DeviceTypeNotInCoreConfig_ReturnsNullAndLogsError()
    {
        // given device type 256 not declared by any controller
        var entries = new Dictionary<string, string> { ["input_libretro_device_p1"] = "256" };
        var coreConfig = CoreConfig(("Pad-3btn", [1]), ("Pad-6btn", [513, 769]));

        var result = _underTest.Resolve(Data(entries), coreConfig, Core);

        result.ShouldBeNull();
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("256") && s.Contains(Core)));
    }

    // ---- match ----

    [Fact]
    public void Resolve_DeviceType1_DeclaredInCoreConfig_ReturnsVariant()
    {
        var entries = new Dictionary<string, string> { ["input_libretro_device_p1"] = "1" };
        var coreConfig = CoreConfig(("Control Pad", [1]), ("3D Control Pad", [261]));

        var result = _underTest.Resolve(Data(entries), coreConfig, Core);

        result.ShouldNotBeNull();
        result.Name.ShouldBe("Control Pad");
        _logger.DidNotReceive().Error(Arg.Any<string>());
    }

    [Theory]
    [InlineData("513")] // first declared id for Pad-6btn
    [InlineData("769")] // second declared id — verifies SelectController checks all ids
    public void Resolve_DeviceTypeMatchesCoreConfig_ReturnsVariant(string deviceTypeStr)
    {
        // given a coreConfig where Pad-6btn declares two device-type ids
        var entries = new Dictionary<string, string> { ["input_libretro_device_p1"] = deviceTypeStr };
        var coreConfig = CoreConfig(("Pad-3btn", [1]), ("Pad-6btn", [513, 769]));

        var result = _underTest.Resolve(Data(entries), coreConfig, Core);

        result.ShouldNotBeNull();
        result.Name.ShouldBe("Pad-6btn");
        _logger.DidNotReceive().Error(Arg.Any<string>());
    }
}
