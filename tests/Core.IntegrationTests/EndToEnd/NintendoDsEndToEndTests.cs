using DynamicControls.Composition;
using static DynamicControls.Core.IntegrationTests.TestHelpers.TestPaths;

namespace DynamicControls.Core.IntegrationTests.EndToEnd;

/// <summary>
/// Full-pipeline test for a platform with no labels file at any level: no game-specific labels
/// XML and no <c>_DefaultLabels.xml</c>. The labels service returns an empty
/// <see cref="Labels.ResolvedLabels"/> with <c>IsGameSpecific=false</c>, so
/// <c>showIf="auto"</c> resolves to <c>IsMapped</c> (default-labels rendering mode). Every
/// mapped input renders active with its platform-specific image; label-only Groups (face-button
/// Stack, Start/Back Stack) drop out entirely because no descendants are labelled.
/// </summary>
public class NintendoDsEndToEndTests
{
    private readonly ControllerOverlayService _service = ControllerOverlayFactory.Create(FixturesRoot);

    /// <summary>
    /// NDS Pad controller, no labels at any level — default rendering mode: mapped inputs render
    /// active (NDS platform art where available), and every label-driven Group/OneOf drops out.
    /// </summary>
    [Fact]
    public void NintendoDs_NoLabels()
    {
        var game = new GameInfo(
            Platform: "Nintendo DS",
            RomName: "Mario Kart DS (USA)",   // no labels file exists for this ROM (or the platform)
            CloneOf: null,
            EmulatorPath: null,
            RomDirectory: null,
            RetroArchCore: null);

        ControllerOverlayModel overlay = _service.Resolve(game);

        var t = overlay.InTemplate(@"Templates\Xbox Series X");
        t.ShouldHaveBaseImage("BaseImage.png", width: 1600, height: 1000);
        t.ShouldHaveImages(
                #pragma warning disable format
                // Face buttons mapped (NDS swaps A/B and X/Y vs. Xbox) → MappedDefault, platform images
                new(Input: "ButtonY",             Src: @"Nintendo DS\X.png",      W: 64,  H: 64),
                new(Input: "ButtonX",             Src: @"Nintendo DS\Y.png",      W: 64,  H: 64),
                new(Input: "ButtonA",             Src: @"Nintendo DS\B.png",      W: 64,  H: 64),
                new(Input: "ButtonB",             Src: @"Nintendo DS\A.png",      W: 64,  H: 64),
                // Shoulder buttons — NDS platform L/R images
                new(Input: "ButtonLeftShoulder",  Src: @"Nintendo DS\L.png",      W: 64,  H: 42),
                new(Input: "ButtonLeftShoulder",  Src: "LineLB.png"),
                new(Input: "ButtonRightShoulder", Src: @"Nintendo DS\R.png",      W: 64,  H: 42),
                new(Input: "ButtonRightShoulder", Src: "LineRB.png"),
                // Triggers/sticks/Dpad — mapped via emulator-specific entries in NDS Controllers.xml,
                // no NDS-specific art so fall back to generic
                new(Input: "AxisTriggerLeft",     Src: "AxisTriggerLeft.png",     W: 65,  H: 65),
                new(Input: "AxisTriggerLeft",     Src: "LineLT.png"),
                new(Input: "AxisTriggerRight",    Src: "AxisTriggerRight.png",    W: 65,  H: 65),
                new(Input: "AxisTriggerRight",    Src: "LineRT.png"),
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",       W: 124, H: 124),
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",      W: 124, H: 124),
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",          W: 135, H: 135),
                // Start/Select mapped — top-level mapped-input-blur passes showIf=mapping → active.
                // Stack Group below them drops entirely (showIf=label inputs, nothing labeled)
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 44,  H: 44),
                // Guide is the only top-level input not in NDS Pad → unmapped, dim
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",         W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0));
                #pragma warning restore format

        // No labels anywhere — nothing should be emitted at all
        overlay.InputLabels.ShouldBeEmpty();
    }
}
