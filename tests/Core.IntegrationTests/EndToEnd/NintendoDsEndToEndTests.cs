using DynamicControls.Composition;
using static DynamicControls.Core.IntegrationTests.TestHelpers.TestPaths;

namespace DynamicControls.Core.IntegrationTests.EndToEnd;

/// <summary>
/// Full-pipeline tests for the Nintendo DS, whose <c>&lt;Defaults&gt;</c> label the emulator's own
/// functions on controls the console doesn't have: the microphone and screen swap on the
/// triggers, the lid on the left stick click, and a touch-screen pointer on the right stick with
/// its toggle on the right stick click. Every DS game therefore carries labels on those controls,
/// whether or not it labels anything itself.
/// </summary>
public class NintendoDsEndToEndTests
{
    private readonly ControllerOverlayService _service = ControllerOverlayFactory.Create(FixturesRoot);

    /// <summary>
    /// NDS Pad controller and no game entry, so the platform's defaults are the only labels and
    /// <c>showIf="auto"</c> means mapped: every mapped input renders active with NDS art where it
    /// exists. The face-button column drops out, as no face button is labelled.
    /// </summary>
    [Fact]
    public void NintendoDs_DefaultLabelsOnly()
    {
        var game = new GameInfo(
            Platform: "Nintendo DS",
            RomName: "Mario Kart DS (USA)",   // no game entry; the platform's defaults only
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
                // Face buttons mapped (NDS swaps A/B and X/Y vs. Xbox) → MappedDefault, platform images
                new(Input: "ButtonY",             Src: @"Nintendo DS\X.png",      W: 64,  H: 64),
                new(Input: "ButtonX",             Src: @"Nintendo DS\Y.png",      W: 64,  H: 64),
                new(Input: "ButtonA",             Src: @"Nintendo DS\B.png",      W: 64,  H: 64),
                new(Input: "ButtonB",             Src: @"Nintendo DS\A.png",      W: 64,  H: 64),
                // Shoulder buttons — NDS platform L/R images
                new(Input: "ButtonLeftShoulder",  Src: @"Nintendo DS\L.png",      W: 64,  H: 42),
                new(Input: "ButtonLeftShoulder",  Src: "Line_ButtonLeftShoulder.png"),
                new(Input: "ButtonRightShoulder", Src: @"Nintendo DS\R.png",      W: 64,  H: 42),
                new(Input: "ButtonRightShoulder", Src: "Line_ButtonRightShoulder.png"),
                // Microphone and screen swap on the triggers — no NDS art, so generic
                new(Input: "AxisTriggerLeft",     Src: "AxisTriggerLeft.png",     W: 65,  H: 65),
                new(Input: "AxisTriggerLeft",     Src: "Line_AxisTriggerLeft.png"),
                new(Input: "AxisTriggerRight",    Src: "AxisTriggerRight.png",    W: 65,  H: 65),
                new(Input: "AxisTriggerRight",    Src: "Line_AxisTriggerRight.png"),
                // Close lid on the left stick click, and the touch-stick toggle on the right one,
                // which sits below the controller
                new(Input: "ButtonLeftStick",     Src: "ButtonLeftStick.png",     W: 44,  H: 44),
                new(Input: "ButtonLeftStick",     Src: "Line_ButtonLeftStick.png"),
                new(Input: "ButtonRightStick",    Src: "ButtonRightStick.png",    W: 44,  H: 44),
                new(Input: "ButtonRightStick",    Src: "Line_ButtonRightStick.png"),
                // The touch pointer's four directions share one label: the single glyph in the
                // right-hand column
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",      W: 124, H: 124),
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",      W: 64,  H: 64),
                new(Input: "AxisRightStick",      Src: "Line_AxisRightStick.png"),
                // Left stick and D-pad mapped but unlabelled
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",       W: 124, H: 124),
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",          W: 135, H: 135),
                // Start/Select mapped and labelled by default, so the meta-buttons cluster draws too
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 44,  H: 44),
                new(Input: null,                  Src: "Line_MetaButtons.png"),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 34,  H: 34),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 34,  H: 34),
                // Guide is the only top-level input not in NDS Pad → unmapped, dim
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",         W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0));
                #pragma warning restore format

        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "AxisTriggerLeft",     Text: "Make microphone noise"),
            new(Input: "AxisTriggerRight",    Text: "Swap screens"),
            new(Input: "ButtonLeftStick",     Text: "Close lid"),
            new(Input: "ButtonRightStick",    Text: "Toggle touch joystick"),
            new(Input: "AxisRightStick",      Text: "Move touch joystick"),
            new(Input: "ButtonStart",         Text: "Start"),
            new(Input: "ButtonBack",          Text: "Select"));
            #pragma warning restore format
    }

    /// <summary>
    /// A game that labels the D-pad and L but no face button, over the platform's defaults. The
    /// touch stick's label is inherited, not the game's, so it must not draw the face-button
    /// column that a game's own right-stick label does (<c>match="game-label"</c>) — every DS
    /// game would otherwise show four dim face buttons beside its touch stick.
    /// </summary>
    [Fact]
    public void NintendoDs_GameLabels_InheritedTouchStickDrawsNoFaceButtonColumn()
    {
        var game = new GameInfo(
            Platform: "Nintendo DS",
            RomName: "Metroid Prime Hunters (USA)",
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
                // No face button labelled — dim on the controller, and no face-button column
                new(Input: "ButtonY",             Src: @"Nintendo DS\X.png",      W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonX",             Src: @"Nintendo DS\Y.png",      W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonA",             Src: @"Nintendo DS\B.png",      W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonB",             Src: @"Nintendo DS\A.png",      W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                // L=Fire active; R unlabelled, dim
                new(Input: "ButtonLeftShoulder",  Src: @"Nintendo DS\L.png",      W: 64,  H: 42),
                new(Input: "ButtonLeftShoulder",  Src: "Line_ButtonLeftShoulder.png"),
                new(Input: "ButtonRightShoulder", Src: @"Nintendo DS\R.png",      W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: "Line_ButtonRightShoulder.png",                          Opacity: 0.3, BlurRadius: 8.0),
                // The emulator functions, labelled by the inherited defaults
                new(Input: "AxisTriggerLeft",     Src: "AxisTriggerLeft.png",     W: 65,  H: 65),
                new(Input: "AxisTriggerLeft",     Src: "Line_AxisTriggerLeft.png"),
                new(Input: "AxisTriggerRight",    Src: "AxisTriggerRight.png",    W: 65,  H: 65),
                new(Input: "AxisTriggerRight",    Src: "Line_AxisTriggerRight.png"),
                new(Input: "ButtonLeftStick",     Src: "ButtonLeftStick.png",     W: 44,  H: 44),
                new(Input: "ButtonLeftStick",     Src: "Line_ButtonLeftStick.png"),
                new(Input: "ButtonRightStick",    Src: "ButtonRightStick.png",    W: 44,  H: 44),
                new(Input: "ButtonRightStick",    Src: "Line_ButtonRightStick.png"),
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",      W: 124, H: 124),
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",      W: 64,  H: 64),
                new(Input: "AxisRightStick",      Src: "Line_AxisRightStick.png"),
                // Dpad-Any=Move: all four directions agree, so the merged D-pad cluster draws, and
                // analogToDigital mirrors Move onto the left stick's single glyph
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",          W: 135, H: 135),
                new(Input: "ButtonDpadLeft",      Src: "ButtonDpadLeft.png",      W: 34,  H: 34),
                new(Input: "ButtonDpadRight",     Src: "ButtonDpadRight.png",     W: 34,  H: 34),
                new(Input: "ButtonDpadUp",        Src: "ButtonDpadUp.png",        W: 34,  H: 34),
                new(Input: "ButtonDpadDown",      Src: "ButtonDpadDown.png",      W: 34,  H: 34),
                new(Input: null,                  Src: "Line_ButtonDpad_Multi.png"),
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",       W: 124, H: 124),
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",       W: 64,  H: 64),
                new(Input: "AxisLeftStick",       Src: "Line_AxisLeftStick.png"),
                // Start/Select labelled by default
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 44,  H: 44),
                new(Input: null,                  Src: "Line_MetaButtons.png"),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 34,  H: 34),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 34,  H: 34),
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",         W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0));
                #pragma warning restore format

        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "ButtonDpad",          Text: "Move"),
            new(Input: "AxisLeftStick",       Text: "Move"),
            new(Input: "ButtonLeftShoulder",  Text: "Fire"),
            // inherited from the platform's defaults
            new(Input: "AxisTriggerLeft",     Text: "Make microphone noise"),
            new(Input: "AxisTriggerRight",    Text: "Swap screens"),
            new(Input: "ButtonLeftStick",     Text: "Close lid"),
            new(Input: "ButtonRightStick",    Text: "Toggle touch joystick"),
            new(Input: "AxisRightStick",      Text: "Move touch joystick"),
            new(Input: "ButtonStart",         Text: "Start"),
            new(Input: "ButtonBack",          Text: "Select"));
            #pragma warning restore format
    }
}
