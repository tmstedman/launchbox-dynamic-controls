using DynamicControls.Composition;
using static DynamicControls.Core.IntegrationTests.TestHelpers.TestPaths;

namespace DynamicControls.Core.IntegrationTests.EndToEnd;

/// <summary>
/// Full-pipeline tests for the Arcade platform. MAME is the emulator, so both plugins are
/// active: ControlsXml supplies labels from controls.xml, and MameInputMappingSource can apply
/// cfg-based button remaps. No analogToDigital on the Cabinet controller — AxisLeftStick
/// directions never inherit Joystick labels, in contrast to every Sega Genesis test.
/// </summary>
public class ArcadeEndToEndTests
{
    private readonly ControllerOverlayService _service = ControllerOverlayFactory.Create(FixturesRoot);

    private static string MameEmulatorPath => Path.Combine(FixturesRoot, "Emulators", "mame", "mame.exe");
    private static string MameDefaultEmulatorPath => Path.Combine(FixturesRoot, "Emulators", "mame-default", "mame.exe");

    /// <summary>
    /// Cabinet controller, labels from controls.xml. The Cabinet has no analogToDigital, so
    /// AxisLeftStick never inherits the Joystick labels and stays dim — unlike every Genesis test.
    /// </summary>
    [Fact]
    public void Arcade_DonkeyKong()
    {
        var game = new GameInfo(
            Platform: "Arcade",
            RomName: "dkong",
            CloneOf: null,
            EmulatorPath: MameEmulatorPath,
            RomDirectory: null,
            RetroArchCore: null);

        ControllerOverlayModel overlay = _service.Resolve(game);

        var t = overlay.InTemplate(@"Templates\Xbox Series X");
        t.ShouldHaveBaseImage("BaseImage.png", width: 1600, height: 1000);
        t.ShouldHaveImages(
                #pragma warning disable format
                // only BUTTON1 (Jump) is labeled — no platform-specific images for Arcade,
                // so all buttons fall back to generic ButtonA.png / ButtonB.png / etc.
                new(Input: "ButtonA",             Src: "ButtonA.png",             W: 64,  H: 64),              // Jump — active
                new(Input: "ButtonA",             Src: "ButtonA.png",             W: 44,  H: 44),              // top-level + Stack
                new(Input: "ButtonA",             Src: "LineA.png"),
                new(Input: "ButtonB",             Src: "ButtonB.png",             W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonB",             Src: "ButtonB.png",             W: 44,  H: 44,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonB",             Src: "LineB.png",                               Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonX",             Src: "ButtonX.png",             W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonX",             Src: "ButtonX.png",             W: 44,  H: 44,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonX",             Src: "LineX.png",                               Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonY",             Src: "ButtonY.png",             W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonY",             Src: "ButtonY.png",             W: 44,  H: 44,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonY",             Src: "LineY.png",                               Opacity: 0.3, BlurRadius: 8.0),
                // Start and Insert Coin — both active (inherited defaults supply labels)
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 44,  H: 44),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 34,  H: 34),          // top-level + Stack
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 34,  H: 34),           // Stack entry active — first time in any test
                // Joystick directions all labeled — no analogToDigital, so AxisLeftStick stays dim
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",          W: 135, H: 135),
                new(Input: "ButtonDpadUp",        Src: "ButtonDpadUp.png",        W: 34,  H: 34),
                new(Input: "ButtonDpadLeft",      Src: "ButtonDpadLeft.png",      W: 34,  H: 34),
                new(Input: "ButtonDpadRight",     Src: "ButtonDpadRight.png",     W: 34,  H: 34),
                new(Input: "ButtonDpadDown",      Src: "ButtonDpadDown.png",      W: 34,  H: 34),
                // AxisLeftStick dim — no JOYSTICKLEFT labels and no analogToDigital mirror
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",       W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                // disabled inputs — no labels, not in Cabinet mapping, or beyond BUTTON4
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",         W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "AxisTriggerLeft.png",     W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "LineLT.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder",  Src: "ButtonLeftShoulder.png",  W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder",  Src: "LineLB.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "AxisTriggerRight.png",    W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "LineRT.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: "ButtonRightShoulder.png", W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: "LineRB.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",      W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                // group-level decorations — LineL_Multi.png absent (no AxisLeftStick labels)
                new(Input: null,                  Src: "LineStart.png"),
                new(Input: null,                  Src: "LineDpad_Multi.png"));
                #pragma warning restore format

        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "ButtonA",        Text: "Jump"),
            new(Input: "ButtonDpadUp",   Text: "Climb Up Ladder"),
            new(Input: "ButtonDpadDown", Text: "Climb Down Ladder"),
            new(Input: "ButtonDpadLeft", Text: "Run Left"),
            new(Input: "ButtonDpadRight", Text: "Run Right"),
            // inherited Arcade defaults — no analogToDigital, so no stick mirrors
            new(Input: "ButtonStart",    Text: "Start"),
            new(Input: "ButtonBack",     Text: "Insert Coin"));
            #pragma warning restore format
    }

    /// <summary>
    /// Dual 8-way joystick (JOYSTICKLEFT=Move, JOYSTICKRIGHT=Fire): both sticks fully active with
    /// directional labels, the right-stick directions reusing left-stick images via useImage.
    /// </summary>
    [Fact]
    public void Arcade_Robotron()
    {
        var game = new GameInfo(
            Platform: "Arcade",
            RomName: "robotron",
            CloneOf: null,
            EmulatorPath: MameEmulatorPath,
            RomDirectory: null,
            RetroArchCore: null);

        ControllerOverlayModel overlay = _service.Resolve(game);

        var t = overlay.InTemplate(@"Templates\Xbox Series X");
        t.ShouldHaveBaseImage("BaseImage.png", width: 1600, height: 1000);
        t.ShouldHaveImages(
                #pragma warning disable format
                // no BUTTON labels — face buttons render once each (top-level only, no Stack/line decorations)
                new(Input: "ButtonA",             Src: "ButtonA.png",             W: 64,  H: 64, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonB",             Src: "ButtonB.png",             W: 64,  H: 64, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonX",             Src: "ButtonX.png",             W: 64,  H: 64, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonY",             Src: "ButtonY.png",             W: 64,  H: 64, Opacity: 0.3, BlurRadius: 8.0),
                // Start and Insert Coin — both active (inherited defaults)
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 44,  H: 44),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 34,  H: 34), // top-level + Stack
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 34,  H: 34), // Stack
                // left stick — Move directions fully active (JOYSTICKLEFT_*)
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",       W: 124, H: 124),
                new(Input: "AxisLeftStickUp",     Src: "AxisLeftStickUp.png",     W: 34,  H: 34),
                new(Input: "AxisLeftStickLeft",   Src: "AxisLeftStickLeft.png",   W: 34,  H: 34),
                new(Input: "AxisLeftStickRight",  Src: "AxisLeftStickRight.png",  W: 34,  H: 34),
                new(Input: "AxisLeftStickDown",   Src: "AxisLeftStickDown.png",   W: 34,  H: 34),
                // right stick — Fire directions fully active for the first time (JOYSTICKRIGHT_*);
                // directional renders reuse AxisLeftStick images via useImage
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",      W: 124, H: 124),
                new(Input: "AxisRightStickUp",    Src: "AxisLeftStickUp.png",     W: 34,  H: 34),
                new(Input: "AxisRightStickLeft",  Src: "AxisLeftStickLeft.png",   W: 34,  H: 34),
                new(Input: "AxisRightStickRight", Src: "AxisLeftStickRight.png",  W: 34,  H: 34),
                new(Input: "AxisRightStickDown",  Src: "AxisLeftStickDown.png",   W: 34,  H: 34),
                // Dpad dim — no JOYSTICK labels (Robotron has no standard joystick)
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",          W: 135, H: 135, Opacity: 0.3, BlurRadius: 8.0),
                // disabled inputs
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",         W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "AxisTriggerLeft.png",     W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "LineLT.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder",  Src: "ButtonLeftShoulder.png",  W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder",  Src: "LineLB.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "AxisTriggerRight.png",    W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "LineRT.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: "ButtonRightShoulder.png", W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: "LineRB.png",                              Opacity: 0.3, BlurRadius: 8.0),
                // group-level decorations — both stick multi-line overlays appear; no LineDpad_Multi
                new(Input: null,                  Src: "LineStart.png"),
                new(Input: null,                  Src: "LineL_Multi.png"),
                new(Input: null,                  Src: "LineR_Multi.png"));
                #pragma warning restore format

        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "AxisLeftStickUp",     Text: "Move Up"),
            new(Input: "AxisLeftStickDown",   Text: "Move Down"),
            new(Input: "AxisLeftStickLeft",   Text: "Move Left"),
            new(Input: "AxisLeftStickRight",  Text: "Move Right"),
            new(Input: "AxisRightStickUp",    Text: "Fire Up"),
            new(Input: "AxisRightStickDown",  Text: "Fire Down"),
            new(Input: "AxisRightStickLeft",  Text: "Fire Left"),
            new(Input: "AxisRightStickRight", Text: "Fire Right"),
            new(Input: "ButtonStart",         Text: "Start"),
            new(Input: "ButtonBack",          Text: "Insert Coin"));
            #pragma warning restore format
    }

    /// <summary>
    /// Per-game InputMappings remap BUTTON1/2 onto ButtonX/A (file labels win over controls.xml);
    /// a whole-Joystick label fires the OneOf second alt and mirrors Move onto AxisLeftStick.
    /// </summary>
    [Fact]
    public void Arcade_1942()
    {
        var game = new GameInfo(
            Platform: "Arcade",
            RomName: "1942",
            CloneOf: null,
            EmulatorPath: MameEmulatorPath,
            RomDirectory: null,
            RetroArchCore: null);

        ControllerOverlayModel overlay = _service.Resolve(game);

        var t = overlay.InTemplate(@"Templates\Xbox Series X");
        t.ShouldHaveBaseImage("BaseImage.png", width: 1600, height: 1000);
        t.ShouldHaveImages(
                #pragma warning disable format
                // ButtonX (Fire) — active; BUTTON1 remapped from ButtonA to ButtonX by InputMappings
                new(Input: "ButtonX",             Src: "ButtonX.png",              W: 64,  H: 64),
                new(Input: "ButtonX",             Src: "ButtonX.png",              W: 44,  H: 44),  // top-level + Stack
                new(Input: "ButtonX",             Src: "LineX.png"),
                // ButtonA (Loop) — active; BUTTON2 remapped from ButtonB to ButtonA by InputMappings
                new(Input: "ButtonA",             Src: "ButtonA.png",              W: 64,  H: 64),
                new(Input: "ButtonA",             Src: "ButtonA.png",              W: 44,  H: 44),
                new(Input: "ButtonA",             Src: "LineA.png"),
                // ButtonB now dim — Loop remapped away, nothing lands here
                new(Input: "ButtonB",             Src: "ButtonB.png",              W: 64,  H: 64, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonB",             Src: "ButtonB.png",              W: 44,  H: 44, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonB",             Src: "LineB.png",                               Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonY",             Src: "ButtonY.png",              W: 64,  H: 64, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonY",             Src: "ButtonY.png",              W: 44,  H: 44, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonY",             Src: "LineY.png",                               Opacity: 0.3, BlurRadius: 8.0),
                // Start and Insert Coin — both active (inherited defaults)
                new(Input: "ButtonStart",         Src: "ButtonStart.png",          W: 44,  H: 44),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",          W: 34,  H: 34),  // top-level + Stack
                new(Input: "ButtonBack",          Src: "ButtonBack.png",           W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",           W: 34,  H: 34),  // Stack
                // ButtonDpad active (JOYSTICK→Move); second OneOf alternative fires — all images carry
                // InputName="ButtonDpad" (not individual direction names)
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",           W: 135, H: 135),
                new(Input: "ButtonDpad",          Src: "ButtonDpadUp.png",         W: 34,  H: 34),  // useImage render, InputName="ButtonDpad"
                new(Input: "ButtonDpad",          Src: "ButtonDpadLeft.png",       W: 34,  H: 34),
                new(Input: "ButtonDpad",          Src: "ButtonDpadRight.png",      W: 34,  H: 34),
                new(Input: "ButtonDpad",          Src: "ButtonDpadDown.png",       W: 34,  H: 34),
                new(Input: "ButtonDpad",          Src: "LineDpad_Multi.png"),       // overlay inside Input → InputName="ButtonDpad"
                // AxisLeftStick active — analogToDigital mirrors ButtonDpad→Move to AxisLeftStick (whole stick);
                // no direction labels exist (no JOYSTICK_UP/DOWN labels), so AxisLeftStickUp etc. stay absent;
                // OneOf second alt fires (show-if-label, has Move label): renders AxisLeftStick.png + LineL.png
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",        W: 124, H: 124), // top-level auto-blur: active (hasLabel)
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",        W: 64,  H: 64),  // OneOf second alt render
                new(Input: "AxisLeftStick",       Src: "LineL.png"),                // LineL (single-direction), not LineL_Multi
                // disabled inputs
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",          W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "AxisTriggerLeft.png",      W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "LineLT.png",                               Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder",  Src: "ButtonLeftShoulder.png",   W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder",  Src: "LineLB.png",                               Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "AxisTriggerRight.png",     W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "LineRT.png",                               Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: "ButtonRightShoulder.png",  W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: "LineRB.png",                               Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",       W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                // group-level decoration
                new(Input: null,                  Src: "LineStart.png"));
                #pragma warning restore format

        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "ButtonX",        Text: "Fire"),              // BUTTON1 remapped to ButtonX
            new(Input: "ButtonA",        Text: "Loop"),              // BUTTON2 remapped to ButtonA
            new(Input: "ButtonDpad",     Text: "Move"),              // whole-joystick label
            new(Input: "AxisLeftStick",  Text: "Move"),              // analogToDigital mirror of ButtonDpad
            // inherited Arcade defaults
            new(Input: "ButtonStart",    Text: "Start"),
            new(Input: "ButtonBack",     Text: "Insert Coin"));
            #pragma warning restore format
    }

    /// <summary>
    /// MAME cfg remaps four buttons and OR-joins the Joystick directions with the left-stick axes,
    /// so all six face buttons are labeled and both Dpad and AxisLeftStick activate together.
    /// </summary>
    [Fact]
    public void Arcade_StreetFighterIICE()
    {
        var game = new GameInfo(
            Platform: "Arcade",
            RomName: "sf2ce",
            CloneOf: null,
            EmulatorPath: MameEmulatorPath,
            RomDirectory: null,
            RetroArchCore: null);

        ControllerOverlayModel overlay = _service.Resolve(game);

        var t = overlay.InTemplate(@"Templates\Xbox Series X");
        t.ShouldHaveBaseImage("BaseImage.png", width: 1600, height: 1000);
        t.ShouldHaveImages(
                #pragma warning disable format
                // face buttons — all active; labels follow the remapped layout
                new(Input: "ButtonA",             Src: "ButtonA.png",             W: 64,  H: 64), // Light Punch (unmoved)
                new(Input: "ButtonA",             Src: "ButtonA.png",             W: 44,  H: 44), // top-level + Stack
                new(Input: "ButtonA",             Src: "LineA.png"),
                new(Input: "ButtonB",             Src: "ButtonB.png",             W: 64,  H: 64), // Middle Punch (unmoved)
                new(Input: "ButtonB",             Src: "ButtonB.png",             W: 44,  H: 44),
                new(Input: "ButtonB",             Src: "LineB.png"),
                new(Input: "ButtonX",             Src: "ButtonX.png",             W: 64,  H: 64), // Light Kick (was Heavy Punch)
                new(Input: "ButtonX",             Src: "ButtonX.png",             W: 44,  H: 44),
                new(Input: "ButtonX",             Src: "LineX.png"),
                new(Input: "ButtonY",             Src: "ButtonY.png",             W: 64,  H: 64), // Middle Kick (was Light Kick)
                new(Input: "ButtonY",             Src: "ButtonY.png",             W: 44,  H: 44),
                new(Input: "ButtonY",             Src: "LineY.png"),
                // ButtonLeftShoulder dim — Middle Kick remapped away; HasLabel=false in game-specific mode
                new(Input: "ButtonLeftShoulder",  Src: "ButtonLeftShoulder.png",  W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder",  Src: "LineLB.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: "ButtonRightShoulder.png", W: 64,  H: 42), // Heavy Punch (was Heavy Kick)
                new(Input: "ButtonRightShoulder", Src: "LineRB.png"),
                // AxisTriggerRight active for the first time — Heavy Kick remapped onto it
                new(Input: "AxisTriggerRight",    Src: "AxisTriggerRight.png",    W: 65,  H: 65),
                new(Input: "AxisTriggerRight",    Src: "LineRT.png"),
                // Start and Insert Coin
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 44,  H: 44),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 34,  H: 34),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 34,  H: 34),
                // Joystick directions OR'd with left-stick axes — both Dpad and AxisLeftStick active
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",          W: 135, H: 135),
                new(Input: "ButtonDpadUp",        Src: "ButtonDpadUp.png",        W: 34,  H: 34),
                new(Input: "ButtonDpadLeft",      Src: "ButtonDpadLeft.png",      W: 34,  H: 34),
                new(Input: "ButtonDpadRight",     Src: "ButtonDpadRight.png",     W: 34,  H: 34),
                new(Input: "ButtonDpadDown",      Src: "ButtonDpadDown.png",      W: 34,  H: 34),
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",       W: 124, H: 124),
                new(Input: "AxisLeftStickUp",     Src: "AxisLeftStickUp.png",     W: 34,  H: 34),
                new(Input: "AxisLeftStickLeft",   Src: "AxisLeftStickLeft.png",   W: 34,  H: 34),
                new(Input: "AxisLeftStickRight",  Src: "AxisLeftStickRight.png",  W: 34,  H: 34),
                new(Input: "AxisLeftStickDown",   Src: "AxisLeftStickDown.png",   W: 34,  H: 34),
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",      W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                // disabled inputs
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",         W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "AxisTriggerLeft.png",     W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "LineLT.png",                              Opacity: 0.3, BlurRadius: 8.0),
                // group-level decorations — LineL_Multi.png now appears (AxisLeftStick active)
                new(Input: null,                  Src: "LineStart.png"),
                new(Input: null,                  Src: "LineL_Multi.png"),
                new(Input: null,                  Src: "LineDpad_Multi.png"));
                #pragma warning restore format

        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "ButtonA",             Text: "Light Punch"),
            new(Input: "ButtonB",             Text: "Middle Punch"),
            new(Input: "ButtonRightShoulder", Text: "Heavy Punch"),   // remapped from ButtonX
            new(Input: "ButtonX",             Text: "Light Kick"),    // remapped from ButtonY
            new(Input: "ButtonY",             Text: "Middle Kick"),   // remapped from ButtonLeftShoulder
            new(Input: "AxisTriggerRight",    Text: "Heavy Kick"),    // remapped from ButtonRightShoulder
            new(Input: "ButtonDpadUp",        Text: "Jump"),
            new(Input: "ButtonDpadDown",      Text: "Crouch"),
            new(Input: "ButtonDpadLeft",      Text: "Left"),
            new(Input: "ButtonDpadRight",     Text: "Right"),
            // OR sequences produce the same label on the left-stick inputs simultaneously
            new(Input: "AxisLeftStickUp",     Text: "Jump"),
            new(Input: "AxisLeftStickDown",   Text: "Crouch"),
            new(Input: "AxisLeftStickLeft",   Text: "Left"),
            new(Input: "AxisLeftStickRight",  Text: "Right"),
            new(Input: "ButtonStart",         Text: "Start"),
            new(Input: "ButtonBack",          Text: "Insert Coin"));
            #pragma warning restore format
    }

    /// <summary>
    /// Driving game with analog controls (pedals → triggers, paddle → left-stick L/R): the
    /// AxisLeftStick whole-stick label fans out to its directionals, firing the OneOf first alt
    /// with Left/Right active and Up/Down dim.
    /// </summary>
    [Fact]
    public void Arcade_OutRun()
    {
        var game = new GameInfo(
            Platform: "Arcade",
            RomName: "outrun",
            CloneOf: null,
            EmulatorPath: MameEmulatorPath,
            RomDirectory: null,
            RetroArchCore: null);

        ControllerOverlayModel overlay = _service.Resolve(game);

        var t = overlay.InTemplate(@"Templates\Xbox Series X");
        t.ShouldHaveBaseImage("BaseImage.png", width: 1600, height: 1000);
        t.ShouldHaveImages(
                #pragma warning disable format
                // disabled — no label and not OutRun controls
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",                          W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0),
                // Brake (PEDAL2) and Accelerate (PEDAL) — active with line decorations
                new(Input: "AxisTriggerLeft",     Src: "AxisTriggerLeft.png",                      W: 65,  H: 65),
                new(Input: "AxisTriggerLeft",     Src: "LineLT.png"),
                new(Input: "ButtonLeftShoulder",  Src: "ButtonLeftShoulder.png",                   W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder",  Src: "LineLB.png",                                               Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "AxisTriggerRight.png",                     W: 65,  H: 65),
                new(Input: "AxisTriggerRight",    Src: "LineRT.png"),
                new(Input: "ButtonRightShoulder", Src: "ButtonRightShoulder.png",                  W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: "LineRB.png",                                               Opacity: 0.3, BlurRadius: 8.0),
                // BUTTON1 (High/Low) active; other face buttons dim
                new(Input: "ButtonY",             Src: "ButtonY.png",                              W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonX",             Src: "ButtonX.png",                              W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonA",             Src: "ButtonA.png",                              W: 64,  H: 64),
                new(Input: "ButtonB",             Src: "ButtonB.png",                              W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                // Start and Insert Coin — active (mapped + inherited labels)
                new(Input: "ButtonStart",         Src: "ButtonStart.png",                          W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",                           W: 44,  H: 44),
                new(Input: null,                  Src: "LineStart.png"),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",                          W: 34,  H: 34),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",                           W: 34,  H: 34),
                // face-button Stack renders (AnyVisible=true — ButtonA labeled)
                new(Input: "ButtonY",             Src: "ButtonY.png",                              W: 44,  H: 44,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonY",             Src: "LineY.png",                                                Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonB",             Src: "ButtonB.png",                              W: 44,  H: 44,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonB",             Src: "LineB.png",                                                Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonA",             Src: "ButtonA.png",                              W: 44,  H: 44),
                new(Input: "ButtonA",             Src: "LineA.png"),
                new(Input: "ButtonX",             Src: "ButtonX.png",                              W: 44,  H: 44,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonX",             Src: "LineX.png",                                                Opacity: 0.3, BlurRadius: 8.0),
                // AxisLeftStick top-level active — HasLabel fans out to directional descendants
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",                        W: 124, H: 124),
                // first OneOf alt fires — Left/Right labeled; Up/Down dim
                new(Input: null,                  Src: "LineL_Multi.png"),
                new(Input: "AxisLeftStickUp",     Src: "AxisLeftStickUp.png",                      W: 34,  H: 34,  Opacity: 0.3, BlurRadius: 6.0),
                new(Input: "AxisLeftStickLeft",   Src: "AxisLeftStickLeft.png",                    W: 34,  H: 34),
                new(Input: "AxisLeftStickRight",  Src: "AxisLeftStickRight.png",                   W: 34,  H: 34),
                new(Input: "AxisLeftStickDown",   Src: "AxisLeftStickDown.png",                    W: 34,  H: 34,  Opacity: 0.3, BlurRadius: 6.0),
                // disabled — not in OutRun controls
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",                       W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",                           W: 135, H: 135, Opacity: 0.3, BlurRadius: 8.0));
                #pragma warning restore format

        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "ButtonA",              Text: "High / Low"),
            new(Input: "AxisTriggerLeft",      Text: "Brake"),
            new(Input: "AxisTriggerRight",     Text: "Accelerate"),
            new(Input: "AxisLeftStickLeft",    Text: "Left"),
            new(Input: "AxisLeftStickRight",   Text: "Right"),
            new(Input: "ButtonStart",          Text: "Start"),
            new(Input: "ButtonBack",           Text: "Insert Coin"));
            #pragma warning restore format
    }

    /// <summary>
    /// Clone-of label fallback: a MAME ROM with no controls.xml entry ("mk3r10") inherits its
    /// CloneOf parent's ("mk3") labels via InputLabelsService's retry.
    /// </summary>
    [Fact]
    public void Arcade_MortalKombat3_CloneOfFallback()
    {
        var game = new GameInfo(
            Platform: "Arcade",
            RomName: "mk3r10",            // no controls.xml entry
            CloneOf: "mk3",                // controls.xml has the parent entry
            EmulatorPath: MameEmulatorPath,
            RomDirectory: null,
            RetroArchCore: null);

        ControllerOverlayModel overlay = _service.Resolve(game);

        // Spot-check parent's labels arrived via the CloneOf retry in InputLabelsService.LoadGameLabels
        #pragma warning disable format
        overlay.ShouldHaveLabel("ButtonA",             "High Punch");   // BUTTON1
        overlay.ShouldHaveLabel("ButtonB",             "Block");        // BUTTON2
        overlay.ShouldHaveLabel("ButtonX",             "High Kick");    // BUTTON3
        overlay.ShouldHaveLabel("ButtonY",             "Low Punch");    // BUTTON4
        overlay.ShouldHaveLabel("ButtonLeftShoulder",  "Low Kick");     // BUTTON5
        overlay.ShouldHaveLabel("ButtonRightShoulder", "Run");          // BUTTON6
        overlay.ShouldHaveLabel("ButtonDpadUp",        "Jump");         // JOYSTICK_UP
        overlay.ShouldHaveLabel("ButtonDpadDown",      "Duck");         // JOYSTICK_DOWN
        overlay.ShouldHaveLabel("ButtonStart",         "Start");        // inherited default
        overlay.ShouldHaveLabel("ButtonBack",          "Insert Coin");  // inherited default
        #pragma warning restore format
    }

    /// <summary>
    /// MAME default.cfg fallback: a ROM with no per-rom cfg picks up cfg/default.cfg, whose remap
    /// moves BUTTON7 onto ButtonRightShoulder — proven by the "Attack 7" label landing there
    /// rather than on AxisTriggerLeft (its baseline binding).
    /// </summary>
    [Fact]
    public void Arcade_MoleAttack_MameDefaultCfgFallback()
    {
        var game = new GameInfo(
            Platform: "Arcade",
            RomName: "mole",
            CloneOf: null,
            EmulatorPath: MameDefaultEmulatorPath,
            RomDirectory: null,
            RetroArchCore: null);

        ControllerOverlayModel overlay = _service.Resolve(game);

        // Discriminators that prove default.cfg was applied:
        // (1) ButtonRightShoulder ends with BUTTON7's "Attack 7" label (not BUTTON6's "Attack 6")
        overlay.ShouldHaveLabel("ButtonRightShoulder", "Attack 7");
        // (2) AxisTriggerLeft has no label — without default.cfg it would carry BUTTON7's "Attack 7"
        overlay.InputLabels.ShouldNotContain(l => l.InputName == "AxisTriggerLeft");

        // Sanity: baseline mole labels still flow through for the untouched ports
        #pragma warning disable format
        overlay.ShouldHaveLabel("ButtonA",            "Attack 1");      // BUTTON1
        overlay.ShouldHaveLabel("ButtonB",            "Attack 2");      // BUTTON2
        overlay.ShouldHaveLabel("ButtonX",            "Attack 3");      // BUTTON3
        overlay.ShouldHaveLabel("ButtonY",            "Attack 4");      // BUTTON4
        overlay.ShouldHaveLabel("ButtonLeftShoulder", "Attack 5");      // BUTTON5
        overlay.ShouldHaveLabel("AxisTriggerRight",   "Attack 8");      // BUTTON8
        overlay.ShouldHaveLabel("ButtonLeftStick",    "Attack 9");      // BUTTON9
        overlay.ShouldHaveLabel("ButtonStart",        "Start");         // inherited default
        overlay.ShouldHaveLabel("ButtonBack",         "Insert Coin");   // inherited default
        #pragma warning restore format
    }
}
