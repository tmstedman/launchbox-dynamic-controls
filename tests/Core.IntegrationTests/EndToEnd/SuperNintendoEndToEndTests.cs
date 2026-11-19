using DynamicControls.Composition;
using static DynamicControls.Core.IntegrationTests.TestHelpers.TestPaths;

namespace DynamicControls.Core.IntegrationTests.EndToEnd;

/// <summary>
/// Full-pipeline tests for Super Nintendo games against the Fixtures tree. Pilotwings is the
/// first game labelled with control sets: its manual gives one controller diagram per vehicle,
/// and only Turn Left and Turn Right hold in all five.
/// </summary>
public class SuperNintendoEndToEndTests
{
    private readonly ControllerOverlayService _service = ControllerOverlayFactory.Create(FixturesRoot);

    private static GameInfo Pilotwings => new(
        Platform: "Super Nintendo Entertainment System",
        RomName: "Pilotwings",
        CloneOf: null,
        LaunchBoxId: 2418,
        EmulatorPath: null,
        RomDirectory: null,
        RetroArchCore: null);

    [Fact]
    public void SuperNintendo_Pilotwings_TheDefaultHoldsOnlyWhatEveryVehicleShares()
    {
        // when the service resolves the overlay for Pilotwings
        ControllerOverlayModel overlay = _service.Resolve(Pilotwings);

        // then the overlay itself is the default (the pause screen shows the sets instead): the turns every vehicle shares,
        // and none of any one vehicle's controls
        overlay.SetName.ShouldBeNull();
        overlay.ShouldHaveLabel("ButtonDpadLeft", "Turn Left");
        overlay.ShouldHaveLabel("ButtonDpadRight", "Turn Right");
        overlay.InputLabels.ShouldNotContain(l => l.InputName == "ButtonB" || l.InputName == "ButtonDpadUp");
    }

    [Fact]
    public void SuperNintendo_Pilotwings_EachVehicleIsAnOverlayOfItsOwn()
    {
        ControllerOverlayModel overlay = _service.Resolve(Pilotwings);

        // each vehicle is its own overlay, in the manual's chapter order
        overlay.ControlSets.Select(s => s.SetName)
            .ShouldBe(["Light Plane", "Sky Diving", "Rocket Belt", "Hang Glider", "Helicopter"]);

        // and each is labelled in full and on its own: the Rocket Belt's A and Up are its own
        // (SNES A drives the template's ButtonB), its shoulders change the view, and nothing
        // carries over from the Light Plane before it
        ControllerOverlayModel rocketBelt = overlay.ControlSets[2];
        rocketBelt.ShouldHaveLabel("ButtonB", "Fast Jets Throttle");
        rocketBelt.ShouldHaveLabel("ButtonA", "Slow Jets Throttle");
        rocketBelt.ShouldHaveLabel("ButtonDpadUp", "Point Jets Forward");
        rocketBelt.ShouldHaveLabel("ButtonLeftShoulder", "Change View Point");
        rocketBelt.ShouldHaveLabel("ButtonDpadLeft", "Turn Left");
        rocketBelt.InputLabels.ShouldNotContain(l => l.FullText == "Increase Throttle");

        // a set still takes the platform's <Defaults>, as the default does
        rocketBelt.ShouldHaveLabel("ButtonStart", "Start");

        // and a vehicle without a B binding leaves it unlabelled rather than borrowing another's
        overlay.ControlSets[1].InputLabels.ShouldNotContain(l => l.InputName == "ButtonA");
    }

    [Fact]
    public void SuperNintendo_Pilotwings_EachSetLightsOnlyTheButtonsItLabels()
    {
        ControllerOverlayModel overlay = _service.Resolve(Pilotwings);
        string buttonA = @"Templates\Xbox Series X\ButtonA.png".AsFixturePath();
        string buttonB = @"Templates\Xbox Series X\ButtonB.png".AsFixturePath();

        // the default labels no face button, so what it draws of them is dimmed
        overlay.ShouldOnlyHaveImageAtOpacity("ButtonA", buttonA, 0.3);
        overlay.ShouldOnlyHaveImageAtOpacity("ButtonB", buttonB, 0.3);

        // the Light Plane labels both throttles (SNES A and B drive the template's ButtonB and
        // ButtonA), so both are lit
        ControllerOverlayModel lightPlane = overlay.ControlSets[0];
        lightPlane.ShouldOnlyHaveImageAtOpacity("ButtonA", buttonA, 1.0);
        lightPlane.ShouldOnlyHaveImageAtOpacity("ButtonB", buttonB, 1.0);

        // Sky Diving labels only SNES A, the template's ButtonB: its ButtonA is dimmed
        ControllerOverlayModel skyDiving = overlay.ControlSets[1];
        skyDiving.ShouldOnlyHaveImageAtOpacity("ButtonA", buttonA, 0.3);
        skyDiving.ShouldOnlyHaveImageAtOpacity("ButtonB", buttonB, 1.0);
    }

    [Fact]
    public void SuperNintendo_Pilotwings_EverySetIsDrawnOnTheDefaultsBaseImageAndCanvas()
    {
        ControllerOverlayModel overlay = _service.Resolve(Pilotwings);

        // the sets differ in what they label, never in the picture they are drawn on
        overlay.ControlSets.ShouldNotBeEmpty();
        overlay.ControlSets.ShouldAllBe(set =>
            set.ImagePath == overlay.ImagePath
            && set.CanvasWidth == overlay.CanvasWidth
            && set.CanvasHeight == overlay.CanvasHeight);
    }
}
