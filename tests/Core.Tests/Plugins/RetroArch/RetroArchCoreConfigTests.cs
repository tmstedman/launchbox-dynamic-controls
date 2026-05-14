using DynamicControls.Plugins.RetroArch;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

public class RetroArchCoreConfigTests
{
    private static RetroArchControllerConfig Controller(string name, params int[] ids) =>
        new() { Name = name, RetropadIds = [.. ids] };

    [Fact]
    public void SelectController_NoMatch_ReturnsNull()
    {
        var config = new RetroArchCoreConfig { Controllers = [Controller("Pad", 1)] };

        config.SelectController(deviceType: 99).ShouldBeNull();
    }

    [Fact]
    public void SelectController_MatchesFirstId_ReturnsController()
    {
        var pad = Controller("Pad", 1, 2);
        var config = new RetroArchCoreConfig { Controllers = [pad] };

        config.SelectController(deviceType: 1).ShouldBe(pad);
    }

    [Fact]
    public void SelectController_MatchesSecondId_ReturnsController()
    {
        // RetropadIds is a list — SelectController must check all entries, not just the first
        var pad = Controller("Pad", 1, 2);
        var config = new RetroArchCoreConfig { Controllers = [pad] };

        config.SelectController(deviceType: 2).ShouldBe(pad);
    }

    [Fact]
    public void SelectController_MatchesSecondController_ReturnsCorrectOne()
    {
        var pad3 = Controller("Pad-3btn", 1);
        var pad6 = Controller("Pad-6btn", 513);
        var config = new RetroArchCoreConfig { Controllers = [pad3, pad6] };

        config.SelectController(deviceType: 513).ShouldBe(pad6);
    }
}
