using DynamicControls.Composition;
using static DynamicControls.Core.IntegrationTests.TestHelpers.TestPaths;

namespace DynamicControls.Core.IntegrationTests.EndToEnd;

/// <summary>
/// Full-pipeline test for a platform with no labels file at any level: no game entry and no
/// <c>&lt;Defaults&gt;</c>. The labels service returns an empty
/// <see cref="Labels.ResolvedLabels"/> with <c>IsGameSpecific=false</c>, so
/// <c>showIf="auto"</c> resolves to <c>IsMapped</c> (default-labels rendering mode). Every
/// mapped input renders active; label-only clusters (face buttons, Start/Back) drop out entirely
/// because nothing in them is labelled.
/// </summary>
public class SonyPlaystationEndToEndTests
{
    private readonly ControllerOverlayService _service = ControllerOverlayFactory.Create(FixturesRoot);

    /// <summary>
    /// DualShock (the default controller), no labels at any level: mapped inputs render active
    /// with the generic art (the fixture template carries no PlayStation images), and every
    /// label-driven cluster and alternative drops out.
    /// </summary>
    [Fact]
    public void SonyPlaystation_NoLabels()
    {
        var game = new GameInfo(
            Platform: "Sony Playstation",
            RomName: "Ape Escape (USA)",   // no labels file exists for this ROM (or the platform)
            CloneOf: null,
            LaunchBoxId: null,
            EmulatorPath: null,
            RomDirectory: null,
            RetroArchCore: null);

        ControllerOverlayModel overlay = _service.Resolve(game);

        var t = overlay.InTemplate(@"Templates\Xbox Series X");
        t.ShouldHaveBaseImage("BaseImage.png", width: 1600, height: 1000);
        t.ShouldHaveImages(
                #pragma warning disable format
                // Face buttons, shoulders and triggers mapped → active, generic art
                new(Input: "ButtonY",             Src: "ButtonY.png",             W: 64,  H: 64),
                new(Input: "ButtonX",             Src: "ButtonX.png",             W: 64,  H: 64),
                new(Input: "ButtonA",             Src: "ButtonA.png",             W: 64,  H: 64),
                new(Input: "ButtonB",             Src: "ButtonB.png",             W: 64,  H: 64),
                new(Input: "ButtonLeftShoulder",  Src: "ButtonLeftShoulder.png",  W: 64,  H: 42),
                new(Input: "ButtonLeftShoulder",  Src: "Line_ButtonLeftShoulder.png"),
                new(Input: "ButtonRightShoulder", Src: "ButtonRightShoulder.png", W: 64,  H: 42),
                new(Input: "ButtonRightShoulder", Src: "Line_ButtonRightShoulder.png"),
                new(Input: "AxisTriggerLeft",     Src: "AxisTriggerLeft.png",     W: 65,  H: 65),
                new(Input: "AxisTriggerLeft",     Src: "Line_AxisTriggerLeft.png"),
                new(Input: "AxisTriggerRight",    Src: "AxisTriggerRight.png",    W: 65,  H: 65),
                new(Input: "AxisTriggerRight",    Src: "Line_AxisTriggerRight.png"),
                // Both sticks and the D-pad mapped; their label-driven alternatives drop out
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",       W: 124, H: 124),
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",      W: 124, H: 124),
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",          W: 135, H: 135),
                // Start/Select mapped — top-level mapped-input-blur passes showIf=mapping → active.
                // The meta-buttons cluster below them drops entirely (nothing labelled)
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 44,  H: 44),
                // Guide isn't on a DualShock → unmapped, dim
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",         W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0));
                #pragma warning restore format

        // No labels anywhere — nothing should be emitted at all
        overlay.InputLabels.ShouldBeEmpty();
    }
}
