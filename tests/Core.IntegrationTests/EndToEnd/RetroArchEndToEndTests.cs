using DynamicControls.Composition;
using static DynamicControls.Core.IntegrationTests.TestHelpers.TestPaths;

namespace DynamicControls.Core.IntegrationTests.EndToEnd;

/// <summary>
/// Full-pipeline tests for RetroArch games. The RetroArch plugin reads the cfg cascade to
/// select a controller variant, which may differ from the platform default. Tests here verify
/// that the cfg-driven variant selection, label resolution, and rendering all wire together
/// correctly end-to-end.
/// </summary>
public class RetroArchEndToEndTests
{
    private readonly ControllerOverlayService _service = ControllerOverlayFactory.Create(FixturesRoot);

    private static string RetroArchEmulatorPath =>
        Path.Combine(FixturesRoot, "Emulators", "retroarch", "retroarch.exe");

    /// <summary>
    /// 6-Button (platform default, no game cfg) with an rmp Sega A↔B swap — ButtonX and ButtonA
    /// take each other's image and label; all other inputs unaffected.
    /// </summary>
    [Fact]
    public void SegaGenesis_MortalKombatII_RetroArchRmpButtonSwap()
    {
        var game = new GameInfo(
            Platform: "Sega Genesis",
            RomName: "Mortal Kombat II (USA)",
            CloneOf: null,
            LaunchBoxId: null,
            EmulatorPath: RetroArchEmulatorPath,
            RomDirectory: null,
            RetroArchCore: "genesis_plus_gx_libretro");

        ControllerOverlayModel overlay = _service.Resolve(game);

        var t = overlay.InTemplate(@"Templates\Xbox Series X");
        t.ShouldHaveBaseImage("BaseImage.png", width: 1600, height: 1000);
        t.ShouldHaveImages(
                #pragma warning disable format
                // A↔B rmp swap: ButtonX now carries Sega B (Remapped → B.png); ButtonA carries Sega A (Remapped → A.png).
                // 6-Button controller art exists under Sega Genesis\6-Button\, so it wins over the flat platform images.
                new(Input: "ButtonX",             Src: @"Sega Genesis\6-Button\B.png",     W: 64,  H: 64),
                new(Input: "ButtonX",             Src: @"Sega Genesis\6-Button\B.png",     W: 44,  H: 44),   // top-level + Stack
                new(Input: "ButtonA",             Src: @"Sega Genesis\6-Button\A.png",     W: 64,  H: 64),
                new(Input: "ButtonA",             Src: @"Sega Genesis\6-Button\A.png",     W: 44,  H: 44),
                // ButtonB (Sega C) and ButtonY (Sega Y) are unaffected by the swap
                new(Input: "ButtonB",             Src: @"Sega Genesis\6-Button\C.png",     W: 64,  H: 64),
                new(Input: "ButtonB",             Src: @"Sega Genesis\6-Button\C.png",     W: 44,  H: 44),
                new(Input: "ButtonY",             Src: @"Sega Genesis\6-Button\Y.png",     W: 64,  H: 64),
                new(Input: "ButtonY",             Src: @"Sega Genesis\6-Button\Y.png",     W: 44,  H: 44),
                // shoulder buttons — active (High Punch / High Kick), unaffected; Genesis X/Z art (6-Button row)
                new(Input: "ButtonLeftShoulder",  Src: @"Sega Genesis\6-Button\X.png",     W: 64,  H: 42),
                new(Input: "ButtonLeftShoulder",  Src: "LineLB.png"),
                new(Input: "ButtonRightShoulder", Src: @"Sega Genesis\6-Button\Z.png",     W: 64,  H: 42),
                new(Input: "ButtonRightShoulder", Src: "LineRB.png"),
                // per-input line decorations in the face-button Stack — all active
                new(Input: "ButtonX",             Src: "LineX.png"),
                new(Input: "ButtonA",             Src: "LineA.png"),
                new(Input: "ButtonB",             Src: "LineB.png"),
                new(Input: "ButtonY",             Src: "LineY.png"),
                // Start active (inherited Pause label); Mode mapped but not labeled → Stack entry dim
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 44,  H: 44),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 34,  H: 34),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 34,  H: 34,  Opacity: 0.3, BlurRadius: 6.0),
                // Dpad directionals — all active (game-specific labels), unaffected by face-button swap
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",          W: 135, H: 135),
                new(Input: "ButtonDpadUp",        Src: "ButtonDpadUp.png",        W: 34,  H: 34),
                new(Input: "ButtonDpadLeft",      Src: "ButtonDpadLeft.png",      W: 34,  H: 34),
                new(Input: "ButtonDpadRight",     Src: "ButtonDpadRight.png",     W: 34,  H: 34),
                new(Input: "ButtonDpadDown",      Src: "ButtonDpadDown.png",      W: 34,  H: 34),
                // AxisLeftStick — active; directionals via analogToDigital mirror of Dpad labels
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",       W: 124, H: 124),
                new(Input: "AxisLeftStickUp",     Src: "AxisLeftStickUp.png",     W: 34,  H: 34),
                new(Input: "AxisLeftStickLeft",   Src: "AxisLeftStickLeft.png",   W: 34,  H: 34),
                new(Input: "AxisLeftStickRight",  Src: "AxisLeftStickRight.png",  W: 34,  H: 34),
                new(Input: "AxisLeftStickDown",   Src: "AxisLeftStickDown.png",   W: 34,  H: 34),
                // disabled inputs — not in 6-Button mapping
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",         W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "AxisTriggerLeft.png",     W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "LineLT.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "AxisTriggerRight.png",    W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "LineRT.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",      W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                // group-level decorations
                new(Input: null,                  Src: "LineStart.png"),
                new(Input: null,                  Src: "LineL_Multi.png"),
                new(Input: null,                  Src: "LineDpad_Multi.png"));
                #pragma warning restore format

        // A↔B swap moves labels: ButtonX carries B's label ("Block"), ButtonA carries A's label ("Low Punch")
        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "ButtonX",              Text: "Block"),         // Sega B (remapped here) → "Block"
            new(Input: "ButtonLeftShoulder",   Text: "High Punch"),
            new(Input: "ButtonB",              Text: "Low Kick"),
            new(Input: "ButtonRightShoulder",  Text: "High Kick"),
            new(Input: "ButtonA",              Text: "Low Punch"),     // Sega A (remapped here) → "Low Punch"
            new(Input: "ButtonY",              Text: "Block"),
            new(Input: "ButtonStart",          Text: "Pause"),
            new(Input: "ButtonDpadUp",         Text: "Jump"),
            new(Input: "ButtonDpadDown",       Text: "Crouch"),
            new(Input: "ButtonDpadLeft",       Text: "Back"),
            new(Input: "ButtonDpadRight",      Text: "Forward"),
            new(Input: "AxisLeftStickUp",      Text: "Jump"),
            new(Input: "AxisLeftStickDown",    Text: "Crouch"),
            new(Input: "AxisLeftStickLeft",    Text: "Back"),
            new(Input: "AxisLeftStickRight",   Text: "Forward"));
            #pragma warning restore format
    }

    /// <summary>
    /// 3-Button variant selected by <c>input_libretro_device_p1 = 257</c> in the game cfg —
    /// verifies cfg-driven controller selection (vs. the 6-Button platform default).
    /// </summary>
    [Fact]
    public void SegaGenesis_SonicTheHedgehog_RetroArch3Button()
    {
        var game = new GameInfo(
            Platform: "Sega Genesis",
            RomName: "Sonic the Hedgehog (USA)",
            CloneOf: null,
            LaunchBoxId: null,
            EmulatorPath: RetroArchEmulatorPath,
            RomDirectory: null,
            RetroArchCore: "genesis_plus_gx_libretro");

        ControllerOverlayModel overlay = _service.Resolve(game);

        var t = overlay.InTemplate(@"Templates\Xbox Series X");
        t.ShouldHaveBaseImage("BaseImage.png", width: 1600, height: 1000);
        t.ShouldHaveImages(
                #pragma warning disable format
                // A→ButtonX, B→ButtonA, C→ButtonB — all labeled "Jump"; face-button Stack renders
                new(Input: "ButtonX",             Src: @"Sega Genesis\A.png",     W: 64,  H: 64),
                new(Input: "ButtonX",             Src: @"Sega Genesis\A.png",     W: 44,  H: 44),  // top-level + Stack
                new(Input: "ButtonX",             Src: "LineX.png"),
                new(Input: "ButtonA",             Src: @"Sega Genesis\B.png",     W: 64,  H: 64),
                new(Input: "ButtonA",             Src: @"Sega Genesis\B.png",     W: 44,  H: 44),
                new(Input: "ButtonA",             Src: "LineA.png"),
                new(Input: "ButtonB",             Src: @"Sega Genesis\C.png",     W: 64,  H: 64),
                new(Input: "ButtonB",             Src: @"Sega Genesis\C.png",     W: 44,  H: 44),
                new(Input: "ButtonB",             Src: "LineB.png"),
                // ButtonY not in 3-Button mapping — dim; Stack group visible (A/B/C labeled), so
                // ButtonY renders twice (top-level + Stack) and its line decoration also renders dim
                new(Input: "ButtonY",             Src: "ButtonY.png",             W: 64,  H: 64, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonY",             Src: "ButtonY.png",             W: 44,  H: 44, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonY",             Src: "LineY.png",                              Opacity: 0.3, BlurRadius: 8.0),
                // Start active (inherited Pause label); ButtonBack not in 3-Button → dim
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 44,  H: 44),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 34,  H: 34),  // top-level + Stack
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 44,  H: 44, Opacity: 0.3, BlurRadius: 6.0),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 34,  H: 34, Opacity: 0.3, BlurRadius: 6.0),
                // Dpad "Move" — whole-stick label; OneOf second alt fires (no per-direction labels)
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",          W: 135, H: 135),
                new(Input: "ButtonDpad",          Src: "ButtonDpadUp.png",        W: 34,  H: 34),  // useImage renders, InputName="ButtonDpad"
                new(Input: "ButtonDpad",          Src: "ButtonDpadLeft.png",      W: 34,  H: 34),
                new(Input: "ButtonDpad",          Src: "ButtonDpadRight.png",     W: 34,  H: 34),
                new(Input: "ButtonDpad",          Src: "ButtonDpadDown.png",      W: 34,  H: 34),
                new(Input: "ButtonDpad",          Src: "LineDpad_Multi.png"),                               // overlay on Input, InputName="ButtonDpad"
                // AxisLeftStick "Move" via analogToDigital; no directional mirrors; OneOf second alt fires
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",       W: 124, H: 124), // top-level auto-blur: active (hasLabel)
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",       W: 64,  H: 64),  // OneOf second alt render
                new(Input: "AxisLeftStick",       Src: "LineL.png"),
                // shoulder/trigger buttons — not in 3-Button mapping → dim
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
                // group-level decoration; no LineL_Multi — Dpad label is whole-stick only, no direction
                // labels exist, so AxisLeftStick OneOf fires its second alt (LineL.png), not the first
                new(Input: null,                  Src: "LineStart.png"));
                #pragma warning restore format

        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "ButtonX",        Text: "Jump"),          // A
            new(Input: "ButtonA",        Text: "Jump"),          // B
            new(Input: "ButtonB",        Text: "Jump"),          // C
            new(Input: "ButtonDpad",     Text: "Move"),
            new(Input: "AxisLeftStick",  Text: "Move"),          // analogToDigital mirror
            new(Input: "ButtonStart",    Text: "Pause"));        // inherited default
            #pragma warning restore format
    }

    /// <summary>
    /// PS2 game-cfg Cross↔Circle button swap: ButtonA/ButtonB take each other's image and label
    /// (Remapped); Square/Triangle on ButtonX/ButtonY are unaffected.
    /// </summary>
    [Fact]
    public void SonyPlaystation2_Tekken5_RetroArchCfgCrossCircleSwap()
    {
        var game = new GameInfo(
            Platform: "Sony Playstation 2",
            RomName: "Tekken 5 (USA)",
            CloneOf: null,
            LaunchBoxId: null,
            EmulatorPath: RetroArchEmulatorPath,
            RomDirectory: null,
            RetroArchCore: "pcsx2_libretro");

        ControllerOverlayModel overlay = _service.Resolve(game);

        var t = overlay.InTemplate(@"Templates\Xbox Series X");
        t.ShouldHaveBaseImage("BaseImage.png", width: 1600, height: 1000);
        t.ShouldHaveImages(
                #pragma warning disable format
                // Face buttons after Cross↔Circle cfg swap: ButtonA carries Circle (Remapped → Circle.png),
                // ButtonB carries Cross (Remapped → Cross.png); Square/Triangle on ButtonX/ButtonY unaffected
                new(Input: "ButtonA",             Src: @"Sony Playstation 2\Circle.png",   W: 64,  H: 64),
                new(Input: "ButtonA",             Src: @"Sony Playstation 2\Circle.png",   W: 44,  H: 44),   // top-level + Stack
                new(Input: "ButtonB",             Src: @"Sony Playstation 2\Cross.png",    W: 64,  H: 64),
                new(Input: "ButtonB",             Src: @"Sony Playstation 2\Cross.png",    W: 44,  H: 44),
                new(Input: "ButtonX",             Src: @"Sony Playstation 2\Square.png",   W: 64,  H: 64),
                new(Input: "ButtonX",             Src: @"Sony Playstation 2\Square.png",   W: 44,  H: 44),
                new(Input: "ButtonY",             Src: @"Sony Playstation 2\Triangle.png", W: 64,  H: 64),
                new(Input: "ButtonY",             Src: @"Sony Playstation 2\Triangle.png", W: 44,  H: 44),
                // per-input line decorations in the face-button Stack — all active (all face buttons labeled)
                new(Input: "ButtonA",             Src: "LineA.png"),
                new(Input: "ButtonB",             Src: "LineB.png"),
                new(Input: "ButtonX",             Src: "LineX.png"),
                new(Input: "ButtonY",             Src: "LineY.png"),
                // Start active (inherited Pause label); Back mapped (Select) but unlabeled — top-level
                // mapped-input-blur passes showIf=mapping so it's active, Stack small-label-blur fails so dim
                new(Input: "ButtonStart",         Src: "ButtonStart.png",                  W: 44,  H: 44),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",                  W: 34,  H: 34),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",                   W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",                   W: 34,  H: 34,  Opacity: 0.3, BlurRadius: 6.0),
                // shoulder/trigger buttons — mapped (L1/R1/L2/R2) but unlabeled → dim with platform images
                new(Input: "AxisTriggerLeft",     Src: @"Sony Playstation 2\L2.png",       W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "LineLT.png",                                       Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder",  Src: @"Sony Playstation 2\L1.png",       W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder",  Src: "LineLB.png",                                       Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: @"Sony Playstation 2\R2.png",       W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "LineRT.png",                                       Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: @"Sony Playstation 2\R1.png",       W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: "LineRB.png",                                       Opacity: 0.3, BlurRadius: 8.0),
                // Guide not in DualShock mapping — fully unmapped, falls back to generic image
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",                  W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0),
                // Sticks + Dpad mapped but unlabeled; no L/R/Dpad PS2 art → fall back to generic; OneOf
                // alternatives all need labels somewhere, so only top-level dim render appears
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",                W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",               W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",                   W: 135, H: 135, Opacity: 0.3, BlurRadius: 8.0),
                // group-level decoration above the Start/Back stack (group is visible because Start is labeled)
                new(Input: null,                  Src: "LineStart.png"));
                #pragma warning restore format

        // Cross↔Circle swap moves face-button labels: ButtonA carries Circle's "Right Kick",
        // ButtonB carries Cross's "Left Kick"; Square/Triangle labels unaffected; Start inherited
        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "ButtonA",     Text: "Right Kick"),     // Circle (remapped here)
            new(Input: "ButtonB",     Text: "Left Kick"),      // Cross (remapped here)
            new(Input: "ButtonX",     Text: "Left Punch"),     // Square (unaffected)
            new(Input: "ButtonY",     Text: "Right Punch"),    // Triangle (unaffected)
            new(Input: "ButtonStart", Text: "Pause"));         // inherited default
            #pragma warning restore format
    }

    /// <summary>
    /// PS2 cfg-level analog trigger swap (L2↔R2 axes): AxisTriggerLeft/Right take each other's
    /// image and label (Remapped); face buttons, shoulders, and sticks are unchanged.
    /// </summary>
    [Fact]
    public void SonyPlaystation2_NeedForSpeed_RetroArchCfgTriggerSwap()
    {
        var game = new GameInfo(
            Platform: "Sony Playstation 2",
            RomName: "Need for Speed Underground 2 (USA)",
            CloneOf: null,
            LaunchBoxId: null,
            EmulatorPath: RetroArchEmulatorPath,
            RomDirectory: null,
            RetroArchCore: "pcsx2_libretro");

        ControllerOverlayModel overlay = _service.Resolve(game);

        var t = overlay.InTemplate(@"Templates\Xbox Series X");
        t.ShouldHaveBaseImage("BaseImage.png", width: 1600, height: 1000);
        t.ShouldHaveImages(
                #pragma warning disable format
                // L2↔R2 trigger swap: AxisTriggerLeft now carries R2 (Remapped → R2.png),
                // AxisTriggerRight now carries L2 (Remapped → L2.png)
                new(Input: "AxisTriggerLeft",     Src: @"Sony Playstation 2\R2.png",       W: 65,  H: 65),
                new(Input: "AxisTriggerLeft",     Src: "LineLT.png"),
                new(Input: "AxisTriggerRight",    Src: @"Sony Playstation 2\L2.png",       W: 65,  H: 65),
                new(Input: "AxisTriggerRight",    Src: "LineRT.png"),
                // shoulders mapped (L1/R1) but unlabeled — dim, MappedDefault → platform images
                new(Input: "ButtonLeftShoulder",  Src: @"Sony Playstation 2\L1.png",       W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder",  Src: "LineLB.png",                                       Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: @"Sony Playstation 2\R1.png",       W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: "LineRB.png",                                       Opacity: 0.3, BlurRadius: 8.0),
                // face buttons mapped but unlabeled — dim, MappedDefault, platform images
                new(Input: "ButtonY",             Src: @"Sony Playstation 2\Triangle.png", W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonX",             Src: @"Sony Playstation 2\Square.png",   W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonA",             Src: @"Sony Playstation 2\Cross.png",    W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonB",             Src: @"Sony Playstation 2\Circle.png",   W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                // Start active (inherited Pause); Back top-level active via mapped-input-blur, Stack dim
                new(Input: "ButtonStart",         Src: "ButtonStart.png",                  W: 44,  H: 44),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",                  W: 34,  H: 34),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",                   W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",                   W: 34,  H: 34,  Opacity: 0.3, BlurRadius: 6.0),
                // Guide not in DualShock — unmapped, dim generic
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",                  W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0),
                // sticks + Dpad mapped but unlabeled, no PS2-specific art → generic dim
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",                W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",               W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",                   W: 135, H: 135, Opacity: 0.3, BlurRadius: 8.0),
                // group-level decoration above the Start/Back stack
                new(Input: null,                  Src: "LineStart.png"));
                #pragma warning restore format

        // Trigger swap moves only the trigger labels; Start inherited
        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "AxisTriggerLeft",  Text: "Accelerate"),  // R2 (remapped here)
            new(Input: "AxisTriggerRight", Text: "Brake"),       // L2 (remapped here)
            new(Input: "ButtonStart",      Text: "Pause"));      // inherited default
            #pragma warning restore format
    }

    /// <summary>
    /// PS2 cfg-level d-pad hat swap (up↔down): the Jump/Crouch labels land on the opposite Dpad
    /// direction. No PS2 Dpad art exists, so images are unchanged — the swap shows only via labels.
    /// </summary>
    [Fact]
    public void SonyPlaystation2_CrashBandicoot_RetroArchCfgDpadHatSwap()
    {
        var game = new GameInfo(
            Platform: "Sony Playstation 2",
            RomName: "Crash Bandicoot (USA)",
            CloneOf: null,
            LaunchBoxId: null,
            EmulatorPath: RetroArchEmulatorPath,
            RomDirectory: null,
            RetroArchCore: "pcsx2_libretro");

        ControllerOverlayModel overlay = _service.Resolve(game);

        var t = overlay.InTemplate(@"Templates\Xbox Series X");
        t.ShouldHaveBaseImage("BaseImage.png", width: 1600, height: 1000);
        t.ShouldHaveImages(
                #pragma warning disable format
                // ButtonDpad top-level active because descendants have labels (auto-blur → HasLabel
                // fans out across the directional children)
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",                   W: 135, H: 135),
                // Dpad OneOf first alt fires (directional Stack visible). Up/Down active (labels remapped here),
                // Left/Right dim (no labels). Image: Remapped lookup tries "Dpad-Down.png"/"Dpad-Up.png" in PS2
                // folder — neither exists, so falls back to the render's own generic
                new(Input: "ButtonDpadUp",        Src: "ButtonDpadUp.png",                 W: 34,  H: 34),
                new(Input: "ButtonDpadDown",      Src: "ButtonDpadDown.png",               W: 34,  H: 34),
                new(Input: "ButtonDpadLeft",      Src: "ButtonDpadLeft.png",               W: 34,  H: 34,  Opacity: 0.3, BlurRadius: 6.0),
                new(Input: "ButtonDpadRight",     Src: "ButtonDpadRight.png",              W: 34,  H: 34,  Opacity: 0.3, BlurRadius: 6.0),
                new(Input: null,                  Src: "LineDpad_Multi.png"),
                // shoulders + triggers + face buttons + sticks all mapped but unlabeled → dim
                new(Input: "AxisTriggerLeft",     Src: @"Sony Playstation 2\L2.png",       W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "LineLT.png",                                       Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder",  Src: @"Sony Playstation 2\L1.png",       W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder", Src: "LineLB.png",                                        Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: @"Sony Playstation 2\R2.png",       W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "LineRT.png",                                       Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: @"Sony Playstation 2\R1.png",       W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: "LineRB.png",                                       Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonY",             Src: @"Sony Playstation 2\Triangle.png", W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonX",             Src: @"Sony Playstation 2\Square.png",   W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonA",             Src: @"Sony Playstation 2\Cross.png",    W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonB",             Src: @"Sony Playstation 2\Circle.png",   W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",                  W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",                W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",               W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                // Start active (inherited Pause); Back active top-level (mapped-input-blur), dim in Stack
                new(Input: "ButtonStart",         Src: "ButtonStart.png",                  W: 44,  H: 44),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",                  W: 34,  H: 34),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",                   W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",                   W: 34,  H: 34,  Opacity: 0.3, BlurRadius: 6.0),
                new(Input: null,                  Src: "LineStart.png"));
                #pragma warning restore format

        // Hat swap moves Up↔Down labels to the opposite physical input
        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "ButtonDpadUp",   Text: "Crouch"),    // Dpad-Down (remapped here)
            new(Input: "ButtonDpadDown", Text: "Jump"),      // Dpad-Up (remapped here)
            new(Input: "ButtonStart",    Text: "Pause"));    // inherited default
            #pragma warning restore format
    }

    /// <summary>
    /// PS2 cfg slot disable (<c>input_player1_b_btn = "-1"</c> removes Cross): ButtonA goes Unmapped
    /// (generic image, dim) and Cross's label is dropped; Circle/Square/Triangle stay MappedDefault.
    /// </summary>
    [Fact]
    public void SonyPlaystation2_Burnout3_RetroArchCfgDisabledSlot()
    {
        var game = new GameInfo(
            Platform: "Sony Playstation 2",
            RomName: "Burnout 3 (USA)",
            CloneOf: null,
            LaunchBoxId: null,
            EmulatorPath: RetroArchEmulatorPath,
            RomDirectory: null,
            RetroArchCore: "pcsx2_libretro");

        ControllerOverlayModel overlay = _service.Resolve(game);

        var t = overlay.InTemplate(@"Templates\Xbox Series X");
        t.ShouldHaveBaseImage("BaseImage.png", width: 1600, height: 1000);
        t.ShouldHaveImages(
                #pragma warning disable format
                // Cross disabled → ButtonA is Unmapped: uses generic ButtonA.png (NOT PS2/Cross.png),
                // dim because auto-blur falls through to HasLabel=false in game-specific mode
                new(Input: "ButtonA",             Src: "ButtonA.png",                      W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonA",             Src: "ButtonA.png",                      W: 44,  H: 44,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonA",             Src: "LineA.png",                                        Opacity: 0.3, BlurRadius: 8.0),
                // Circle/Square/Triangle unaffected: MappedDefault → PS2 platform images, active
                new(Input: "ButtonB",             Src: @"Sony Playstation 2\Circle.png",   W: 64,  H: 64),
                new(Input: "ButtonB",             Src: @"Sony Playstation 2\Circle.png",   W: 44,  H: 44),
                new(Input: "ButtonB",             Src: "LineB.png"),
                new(Input: "ButtonX",             Src: @"Sony Playstation 2\Square.png",   W: 64,  H: 64),
                new(Input: "ButtonX",             Src: @"Sony Playstation 2\Square.png",   W: 44,  H: 44),
                new(Input: "ButtonX",             Src: "LineX.png"),
                new(Input: "ButtonY",             Src: @"Sony Playstation 2\Triangle.png", W: 64,  H: 64),
                new(Input: "ButtonY",             Src: @"Sony Playstation 2\Triangle.png", W: 44,  H: 44),
                new(Input: "ButtonY",             Src: "LineY.png"),
                // Start/Back same as other PS2 tests
                new(Input: "ButtonStart",         Src: "ButtonStart.png",                  W: 44,  H: 44),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",                  W: 34,  H: 34),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",                   W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",                   W: 34,  H: 34,  Opacity: 0.3, BlurRadius: 6.0),
                // remaining inputs all mapped but unlabeled → dim with platform images
                new(Input: "AxisTriggerLeft",     Src: @"Sony Playstation 2\L2.png",       W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "LineLT.png",                                       Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder",  Src: @"Sony Playstation 2\L1.png",       W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder", Src: "LineLB.png",                                        Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: @"Sony Playstation 2\R2.png",       W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "LineRT.png",                                       Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: @"Sony Playstation 2\R1.png",       W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: "LineRB.png",                                       Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",                  W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",                W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",               W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",                   W: 135, H: 135, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: null,                  Src: "LineStart.png"));
                #pragma warning restore format

        // Cross's "Boost" label is silently dropped — Cross is no longer in ButtonToInput so
        // the translator finds no input slot. Other face-button labels and inherited Start remain.
        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "ButtonB",     Text: "Brake"),       // Circle
            new(Input: "ButtonX",     Text: "Handbrake"),   // Square
            new(Input: "ButtonY",     Text: "Camera"),      // Triangle
            new(Input: "ButtonStart", Text: "Pause"));      // inherited default
            #pragma warning restore format
    }

    /// <summary>
    /// PS2 layered remaps: cfg swaps Cross↔Circle, then rmp swaps Square↔Triangle on top of the
    /// cfg-mutated mapping — so all four face buttons end up Remapped onto the opposite slot.
    /// </summary>
    [Fact]
    public void SonyPlaystation2_ResidentEvil4_RetroArchCfgPlusRmpLayered()
    {
        var game = new GameInfo(
            Platform: "Sony Playstation 2",
            RomName: "Resident Evil 4 (USA)",
            CloneOf: null,
            LaunchBoxId: null,
            EmulatorPath: RetroArchEmulatorPath,
            RomDirectory: null,
            RetroArchCore: "pcsx2_libretro");

        ControllerOverlayModel overlay = _service.Resolve(game);

        var t = overlay.InTemplate(@"Templates\Xbox Series X");
        t.ShouldHaveBaseImage("BaseImage.png", width: 1600, height: 1000);
        t.ShouldHaveImages(
                #pragma warning disable format
                // Cfg Cross↔Circle: ButtonA carries Circle, ButtonB carries Cross
                new(Input: "ButtonA",             Src: @"Sony Playstation 2\Circle.png",   W: 64,  H: 64),
                new(Input: "ButtonA",             Src: @"Sony Playstation 2\Circle.png",   W: 44,  H: 44),
                new(Input: "ButtonA",             Src: "LineA.png"),
                new(Input: "ButtonB",             Src: @"Sony Playstation 2\Cross.png",    W: 64,  H: 64),
                new(Input: "ButtonB",             Src: @"Sony Playstation 2\Cross.png",    W: 44,  H: 44),
                new(Input: "ButtonB",             Src: "LineB.png"),
                // Rmp Square↔Triangle: ButtonX carries Triangle, ButtonY carries Square
                new(Input: "ButtonX",             Src: @"Sony Playstation 2\Triangle.png", W: 64,  H: 64),
                new(Input: "ButtonX",             Src: @"Sony Playstation 2\Triangle.png", W: 44,  H: 44),
                new(Input: "ButtonX",             Src: "LineX.png"),
                new(Input: "ButtonY",             Src: @"Sony Playstation 2\Square.png",   W: 64,  H: 64),
                new(Input: "ButtonY",             Src: @"Sony Playstation 2\Square.png",   W: 44,  H: 44),
                new(Input: "ButtonY",             Src: "LineY.png"),
                // Start/Back same as other PS2 tests
                new(Input: "ButtonStart",         Src: "ButtonStart.png",                  W: 44,  H: 44),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",                  W: 34,  H: 34),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",                   W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",                   W: 34,  H: 34,  Opacity: 0.3, BlurRadius: 6.0),
                // unaffected mapped-but-unlabeled inputs
                new(Input: "AxisTriggerLeft",     Src: @"Sony Playstation 2\L2.png",       W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "LineLT.png",                                       Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder",  Src: @"Sony Playstation 2\L1.png",       W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder", Src: "LineLB.png",                                        Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: @"Sony Playstation 2\R2.png",       W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "LineRT.png",                                       Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: @"Sony Playstation 2\R1.png",       W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: "LineRB.png",                                       Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",                  W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",                W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",               W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",                   W: 135, H: 135, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: null,                  Src: "LineStart.png"));
                #pragma warning restore format

        // Cfg moves Cross/Circle labels; rmp moves Square/Triangle labels — all 4 visible on their new slots
        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "ButtonA",     Text: "Cancel"),      // Circle (cfg swap)
            new(Input: "ButtonB",     Text: "Attack"),      // Cross  (cfg swap)
            new(Input: "ButtonX",     Text: "Inventory"),   // Triangle (rmp swap)
            new(Input: "ButtonY",     Text: "Reload"),      // Square (rmp swap)
            new(Input: "ButtonStart", Text: "Pause"));      // inherited default
            #pragma warning restore format
    }
}
