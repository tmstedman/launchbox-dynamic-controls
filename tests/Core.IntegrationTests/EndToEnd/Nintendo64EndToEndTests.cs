using DynamicControls.Composition;
using DynamicControls.Core.IntegrationTests.TestHelpers;
using static DynamicControls.Core.IntegrationTests.TestHelpers.TestPaths;

namespace DynamicControls.Core.IntegrationTests.EndToEnd;

/// <summary>
/// Full-pipeline tests for the Nintendo 64 platform. Pad controller: analogToDigital="left", C-buttons mapped to AxisRightStick.
/// </summary>
public class Nintendo64EndToEndTests
{
    private readonly ControllerOverlayService _service = ControllerOverlayFactory.Create(FixturesRoot);

    /// <summary>
    /// Pad controller with full game-specific labels. The N64 has its own analog stick, so the pad
    /// carries no analogToDigital mirror: AxisLeftStick is Stick-Any's and keeps "Look", while the
    /// Dpad keeps "Move" on ButtonDpad.
    /// </summary>
    [Fact]
    public void Nintendo64_Goldeneye007()
    {
        var game = new GameInfo(
            Platform: "Nintendo 64",
            RomName: "Goldeneye 007 (USA)",
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
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",        W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0),
                // AxisTriggerLeft not in Pad mapping, no label — dim
                new(Input: "AxisTriggerLeft",     Src: "AxisTriggerLeft.png",    W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "Line_AxisTriggerLeft.png",                             Opacity: 0.3, BlurRadius: 8.0),
                // L=Aim → ButtonLeftShoulder, Z=Fire → AxisTriggerRight, R=Aim → ButtonRightShoulder — all active
                new(Input: "ButtonLeftShoulder",  Src: @"Nintendo 64\L.png",     W: 64,  H: 42),
                new(Input: "ButtonLeftShoulder",  Src: "Line_ButtonLeftShoulder.png"),
                new(Input: "AxisTriggerRight",    Src: @"Nintendo 64\Z.png",     W: 65,  H: 65),
                new(Input: "AxisTriggerRight",    Src: "Line_AxisTriggerRight.png"),
                new(Input: "ButtonRightShoulder", Src: @"Nintendo 64\R.png",     W: 64,  H: 42),
                new(Input: "ButtonRightShoulder", Src: "Line_ButtonRightShoulder.png"),
                // ButtonY and ButtonX not in Pad mapping — dim; face-button Stack group fires
                // (ButtonA/ButtonB labeled), so each also gets a Stack entry
                new(Input: "ButtonY",             Src: "ButtonY.png",            W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonX",             Src: "ButtonX.png",            W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                // B=Weapon → ButtonA, A=Action → ButtonB — active with platform images
                new(Input: "ButtonA",             Src: @"Nintendo 64\B.png",     W: 64,  H: 64),
                new(Input: "ButtonB",             Src: @"Nintendo 64\A.png",     W: 64,  H: 64),
                // ButtonStart active (mapped); ButtonBack dim (not in Pad mapping)
                new(Input: "ButtonStart",         Src: "ButtonStart.png",        W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",         W: 44,  H: 44,  Opacity: 0.3, BlurRadius: 6.0),
                // LineStart group: ButtonStart has inherited Pause → group fires; ButtonBack dim (no label)
                new(Input: null,                  Src: "Line_MetaButtons.png"),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",        W: 34,  H: 34),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",         W: 34,  H: 34,  Opacity: 0.3, BlurRadius: 6.0),
                // Face-button Stack — group fires (ButtonA/ButtonB labeled); ButtonY/ButtonX dim in Stack
                new(Input: "ButtonY",             Src: "ButtonY.png",            W: 44,  H: 44,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonY",             Src: "Line_ButtonY.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonB",             Src: @"Nintendo 64\A.png",     W: 44,  H: 44),
                new(Input: "ButtonB",             Src: "Line_ButtonB.png"),
                new(Input: "ButtonA",             Src: @"Nintendo 64\B.png",     W: 44,  H: 44),
                new(Input: "ButtonA",             Src: "Line_ButtonA.png"),
                new(Input: "ButtonX",             Src: "ButtonX.png",            W: 44,  H: 44,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonX",             Src: "Line_ButtonX.png",                              Opacity: 0.3, BlurRadius: 8.0),
                // Stick-Any=Look → AxisLeftStick; whole-stick label fires alt 2
                // (Line_AxisLeftStick.png), not the directionals
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",      W: 124, H: 124),
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",      W: 64,  H: 64),
                new(Input: "AxisLeftStick",       Src: "Line_AxisLeftStick.png"),
                // C-Any=Move → AxisRightStick whole-stick label; alt 2 fires (show-if-label, has Move)
                new(Input: "AxisRightStick",      Src: @"Nintendo 64\C-Any.png", W: 124, H: 124),
                new(Input: "AxisRightStick",      Src: @"Nintendo 64\C-Any.png", W: 64,  H: 64),
                new(Input: "AxisRightStick",      Src: "Line_AxisRightStick.png"),
                // Dpad-Any=Move → ButtonDpad active with no per-direction breakdown; the merged
                // Condition alternative fires, copying "Move" onto all four directions so each
                // renders its own icon (InputName is the individual direction)
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",         W: 135, H: 135),
                new(Input: "ButtonDpadUp",        Src: "ButtonDpadUp.png",       W: 34,  H: 34),
                new(Input: "ButtonDpadLeft",      Src: "ButtonDpadLeft.png",     W: 34,  H: 34),
                new(Input: "ButtonDpadRight",     Src: "ButtonDpadRight.png",    W: 34,  H: 34),
                new(Input: "ButtonDpadDown",      Src: "ButtonDpadDown.png",     W: 34,  H: 34),
                new(Input: null,                  Src: "Line_ButtonDpad_Multi.png"));
                #pragma warning restore format

        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "ButtonA",             Text: "Weapon"),            // B → ButtonA
            new(Input: "ButtonB",             Text: "Action"),            // A → ButtonB
            new(Input: "ButtonLeftShoulder",  Text: "Aim"),               // L → ButtonLeftShoulder
            new(Input: "ButtonRightShoulder", Text: "Aim"),               // R → ButtonRightShoulder
            new(Input: "AxisTriggerRight",    Text: "Fire"),              // Z → AxisTriggerRight
            new(Input: "AxisLeftStick",       Text: "Look"),              // Stick-Any → AxisLeftStick
            new(Input: "ButtonDpad",          Text: "Move"),              // Dpad-Any → ButtonDpad
            new(Input: "AxisRightStick",      Text: "Move"),              // C-Any → AxisRightStick
            new(Input: "ButtonStart",         Text: "Pause"));            // inherited default
            #pragma warning restore format
    }

    /// <summary>
    /// Pad controller, no game-specific labels — default rendering mode (showIf="auto" → IsMapped):
    /// all mapped inputs active with platform images, and every label-only Group/OneOf drops out.
    /// </summary>
    [Fact]
    public void Nintendo64_NoLabels()
    {
        var game = new GameInfo(
            Platform: "Nintendo 64",
            RomName: "Super Mario 64 (USA)",
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
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",        W: 68,  H: 68, Opacity: 0.3, BlurRadius: 8.0),
                // AxisTriggerLeft not in Pad mapping — dim
                new(Input: "AxisTriggerLeft",     Src: "AxisTriggerLeft.png",    W: 65,  H: 65, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "Line_AxisTriggerLeft.png",                            Opacity: 0.3, BlurRadius: 8.0),
                // L → ButtonLeftShoulder, R → ButtonRightShoulder, Z → AxisTriggerRight — all active with platform images
                new(Input: "ButtonLeftShoulder",  Src: @"Nintendo 64\L.png",     W: 64,  H: 42),
                new(Input: "ButtonLeftShoulder",  Src: "Line_ButtonLeftShoulder.png"),
                new(Input: "AxisTriggerRight",    Src: @"Nintendo 64\Z.png",     W: 65,  H: 65),
                new(Input: "AxisTriggerRight",    Src: "Line_AxisTriggerRight.png"),
                new(Input: "ButtonRightShoulder", Src: @"Nintendo 64\R.png",     W: 64,  H: 42),
                new(Input: "ButtonRightShoulder", Src: "Line_ButtonRightShoulder.png"),
                // ButtonY and ButtonX — not in Pad mapping; face-button Stack group absent (showIf=label,
                // no game-specific labels) so each appears once only, not twice as in Sega Genesis tests
                new(Input: "ButtonY",             Src: "ButtonY.png",            W: 64,  H: 64, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonX",             Src: "ButtonX.png",            W: 64,  H: 64, Opacity: 0.3, BlurRadius: 8.0),
                // B → ButtonA, A → ButtonB — active with platform images; single occurrence (no Stack group)
                new(Input: "ButtonA",             Src: @"Nintendo 64\B.png",     W: 64,  H: 64),
                new(Input: "ButtonB",             Src: @"Nintendo 64\A.png",     W: 64,  H: 64),
                // ButtonStart active (mapped); ButtonBack dim (not in Pad mapping)
                new(Input: "ButtonStart",         Src: "ButtonStart.png",        W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",         W: 44,  H: 44, Opacity: 0.3, BlurRadius: 6.0),
                // LineStart group fires — Start=Pause inherited from _DefaultLabels.xml; ButtonBack dim (no label)
                new(Input: null,                  Src: "Line_MetaButtons.png"),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",        W: 34,  H: 34),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",         W: 34,  H: 34, Opacity: 0.3, BlurRadius: 6.0),
                // AxisLeftStick active (L-Any mapped) — falls back to generic; no L-Any.png in the platform folder;
                // OneOf directionals absent (small-label-blur, no labels); analogToDigital inert
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",      W: 124, H: 124),
                // C-Any → AxisRightStick — active with platform image; directionals absent
                new(Input: "AxisRightStick",      Src: @"Nintendo 64\C-Any.png", W: 124, H: 124),
                // Dpad active (Dpad mapped) — falls back to generic; no Dpad.png in the platform folder;
                // directional OneOf absent (no labels)
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",         W: 135, H: 135));
                #pragma warning restore format

        overlay.ShouldHaveLabels(
            new ExpectedLabel(Input: "ButtonStart", Text: "Pause"));
    }
}
