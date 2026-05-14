using DynamicControls.Composition;
using static DynamicControls.Core.IntegrationTests.TestHelpers.TestPaths;

namespace DynamicControls.Core.IntegrationTests.EndToEnd;

/// <summary>
/// Full-pipeline tests: build ControllerOverlayService via the production factory, point it at
/// the Fixtures tree, resolve the overlay for a known game, and assert that each label/image
/// landed in the correct slot via <c>InputName</c> metadata. Cover the two main rendering modes
/// (game-specific vs. default) and two Sega Genesis controllers (3-Button vs. 6-Button).
/// </summary>
public class SegaGenesisEndToEndTests
{
    private readonly ControllerOverlayService _service = ControllerOverlayFactory.Create(FixturesRoot);

    /// <summary>
    /// 3-Button controller (per-game XML) with game-specific labels — the game-specific rendering
    /// mode where showIf="auto" resolves to HasLabel, so unlabeled and unmapped inputs render dim.
    /// </summary>
    [Fact]
    public void SegaGenesis_OutRun()
    {
        // given the OutRun game on Sega Genesis (the fixture's InputMappings file picks 3-Button)
        var game = new GameInfo(
            Platform: "Sega Genesis",
            RomName: "OutRun (USA, Europe)",
            CloneOf: null,
            EmulatorPath: null,
            RomDirectory: null,
            RetroArchCore: null);

        // when the service resolves the overlay for the game
        ControllerOverlayModel overlay = _service.Resolve(game);

        // then the overlay carries the template's base image, the canvas matches its pixel
        // dimensions (1600x1000, proving it was probed via IImageHeader.ReadDimensions), and
        // images land on the correct slots with the correct style:
        var t = overlay.InTemplate(@"Templates\Xbox Series X");
        t.ShouldHaveBaseImage("BaseImage.png", width: 1600, height: 1000);
        t.ShouldHaveImages(
                #pragma warning disable format
                // platform inputs rendered twice each: once top-level, once in the face-button
                // Stack group. Labels for A/B in OutRun's labels file make ButtonX/ButtonA
                // visible under showIf="auto" in game-specific mode (auto -> HasLabel).
                new(Input: "ButtonX",             Src: @"Sega Genesis\A.png",     W: 64,  H: 64),
                new(Input: "ButtonX",             Src: @"Sega Genesis\A.png",     W: 44,  H: 44),
                new(Input: "ButtonA",             Src: @"Sega Genesis\B.png",     W: 64,  H: 64),
                new(Input: "ButtonA",             Src: @"Sega Genesis\B.png",     W: 44,  H: 44),
                // disabled platform input — C is mapped to ButtonB but OutRun has no C label
                new(Input: "ButtonB",             Src: @"Sega Genesis\C.png",     W: 64,  H: 64, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonB",             Src: @"Sega Genesis\C.png",     W: 44,  H: 44, Opacity: 0.3, BlurRadius: 8.0),
                // ButtonY isn't on 3-Button at all — rendered twice (top-level + Stack group)
                new(Input: "ButtonY",             Src: "ButtonY.png",             W: 64,  H: 64, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonY",             Src: "ButtonY.png",             W: 44,  H: 44, Opacity: 0.3, BlurRadius: 8.0),
                // ButtonStart and ButtonBack also appear in both top-level and Stack group
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 44,  H: 44),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 34,  H: 34),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 44,  H: 44, Opacity: 0.3, BlurRadius: 6.0),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 34,  H: 34, Opacity: 0.3, BlurRadius: 6.0),
                // active generic inputs (single occurrence each)
                new(Input: "ButtonDpadUp",        Src: "ButtonDpadUp.png",        W: 34,  H: 34),
                new(Input: "ButtonDpadLeft",      Src: "ButtonDpadLeft.png",      W: 34,  H: 34),
                new(Input: "ButtonDpadRight",     Src: "ButtonDpadRight.png",     W: 34,  H: 34),
                new(Input: "ButtonDpadDown",      Src: "ButtonDpadDown.png",      W: 34,  H: 34),
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",          W: 135, H: 135),
                new(Input: "AxisLeftStickUp",     Src: "AxisLeftStickUp.png",     W: 34,  H: 34),
                new(Input: "AxisLeftStickLeft",   Src: "AxisLeftStickLeft.png",   W: 34,  H: 34),
                new(Input: "AxisLeftStickRight",  Src: "AxisLeftStickRight.png",  W: 34,  H: 34),
                new(Input: "AxisLeftStickDown",   Src: "AxisLeftStickDown.png",   W: 34,  H: 34),
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",       W: 124, H: 124),
                // disabled generic inputs — not on 3-Button or no label in game-specific mode
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",         W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "AxisTriggerLeft.png",     W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder",  Src: "ButtonLeftShoulder.png",  W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "AxisTriggerRight.png",    W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: "ButtonRightShoulder.png", W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",      W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                // per-input line decorations
                new(Input: "ButtonX",             Src: "LineX.png"                                                ),
                new(Input: "ButtonA",             Src: "LineA.png"                                                ),
                new(Input: "ButtonY",             Src: "LineY.png",                               Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonB",             Src: "LineB.png",                               Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "LineLT.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder",  Src: "LineLB.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "LineRT.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonRightShoulder", Src: "LineRB.png",                              Opacity: 0.3, BlurRadius: 8.0),
                // group-level decorations with no associated input
                new(Input: null,                  Src: "LineStart.png"),
                new(Input: null,                  Src: "LineL_Multi.png"),
                new(Input: null,                  Src: "LineDpad_Multi.png"));
                #pragma warning restore format

        // and game-specific labels from OutRun's labels file land on the input slots their
        // platform buttons drive (3-Button maps A->ButtonX, B->ButtonA, etc.); the inherited
        // default Start=Pause from _DefaultLabels.xml lands on ButtonStart
        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "ButtonX",            Text: "Brake"),
            new(Input: "ButtonA",            Text: "Accelerate"),
            new(Input: "ButtonDpadUp",       Text: "Low gear"),
            new(Input: "ButtonDpadDown",     Text: "High gear"),
            new(Input: "ButtonDpadLeft",     Text: "Steer"),
            new(Input: "ButtonDpadRight",    Text: "Steer"),
            new(Input: "ButtonStart",        Text: "Pause"),
            // analogToDigital="left" mirrors each Dpad label onto its left-stick equivalent
            new(Input: "AxisLeftStickUp",    Text: "Low gear"),
            new(Input: "AxisLeftStickDown",  Text: "High gear"),
            new(Input: "AxisLeftStickLeft",  Text: "Steer"),
            new(Input: "AxisLeftStickRight", Text: "Steer"));
            #pragma warning restore format
    }

    /// <summary>
    /// 6-Button default, no game-specific labels — the default rendering mode where showIf="auto"
    /// resolves to IsMapped, so all mapped inputs are active and the label-only Stack/OneOf groups drop.
    /// </summary>
    [Fact]
    public void SegaGenesis_StreetFighterII()
    {
        var game = new GameInfo(
            Platform: "Sega Genesis",
            RomName: "Street Fighter II' - Special Champion Edition (USA)",
            CloneOf: null,
            EmulatorPath: null,
            RomDirectory: null,
            RetroArchCore: null);

        ControllerOverlayModel overlay = _service.Resolve(game);

        var t = overlay.InTemplate(@"Templates\Xbox Series X");
        t.ShouldHaveBaseImage("BaseImage.png", width: 1600, height: 1000);
        t.ShouldHaveImages(
                #pragma warning disable format
                // 6-Button face buttons — all active (IsMapped=true); controller subfolder wins over platform folder
                new(Input: "ButtonX",             Src: @"Sega Genesis\6-Button\A.png", W: 64,  H: 64),
                new(Input: "ButtonA",             Src: @"Sega Genesis\6-Button\B.png", W: 64,  H: 64),
                new(Input: "ButtonB",             Src: @"Sega Genesis\6-Button\C.png", W: 64,  H: 64),
                new(Input: "ButtonY",             Src: @"Sega Genesis\6-Button\Y.png", W: 64,  H: 64),
                new(Input: "ButtonLeftShoulder",  Src: @"Sega Genesis\6-Button\X.png", W: 64,  H: 42),
                new(Input: "ButtonLeftShoulder",  Src: "LineLB.png"),
                new(Input: "ButtonRightShoulder", Src: @"Sega Genesis\6-Button\Z.png", W: 64,  H: 42),
                new(Input: "ButtonRightShoulder", Src: "LineRB.png"),
                // Start and Mode buttons — active (showIf="mapping", both mapped in 6-Button)
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 44,  H: 44),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 34,  H: 34),  // top-level + Stack
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 34,  H: 34,  Opacity: 0.3, BlurRadius: 6.0),
                // Dpad and left stick — active (IsMapped, analogToDigital mirrors Dpad→AxisLeftStick)
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",          W: 135, H: 135),
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",       W: 124, H: 124),
                // disabled inputs — not in 6-Button mapping
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",         W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "AxisTriggerLeft.png",     W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "LineLT.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "AxisTriggerRight.png",    W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "LineRT.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",      W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                // group-level decoration (always rendered)
                new(Input: null,                  Src: "LineStart.png"));
                #pragma warning restore format

        // no game-specific labels — only the inherited default Start=Pause from _DefaultLabels.xml
        overlay.ShouldHaveLabels(
            new ExpectedLabel(Input: "ButtonStart", Text: "Pause"));
    }

    /// <summary>
    /// 6-Button default with full game-specific labels — every face button, shoulder, and Dpad
    /// direction labeled, so the whole face-button Stack and the directional renders are active.
    /// </summary>
    [Fact]
    public void SegaGenesis_MortalKombatII()
    {
        var game = new GameInfo(
            Platform: "Sega Genesis",
            RomName: "Mortal Kombat II (USA)",
            CloneOf: null,
            EmulatorPath: null,
            RomDirectory: null,
            RetroArchCore: null);

        ControllerOverlayModel overlay = _service.Resolve(game);

        var t = overlay.InTemplate(@"Templates\Xbox Series X");
        t.ShouldHaveBaseImage("BaseImage.png", width: 1600, height: 1000);
        t.ShouldHaveImages(
                #pragma warning disable format
                // platform face buttons — all active; controller subfolder wins over platform folder
                new(Input: "ButtonX",             Src: @"Sega Genesis\6-Button\A.png", W: 64,  H: 64),
                new(Input: "ButtonX",             Src: @"Sega Genesis\6-Button\A.png", W: 44,  H: 44),  // top-level + Stack
                new(Input: "ButtonA",             Src: @"Sega Genesis\6-Button\B.png", W: 64,  H: 64),
                new(Input: "ButtonA",             Src: @"Sega Genesis\6-Button\B.png", W: 44,  H: 44),
                new(Input: "ButtonB",             Src: @"Sega Genesis\6-Button\C.png", W: 64,  H: 64),  // active (Low Kick) — contrast OutRun
                new(Input: "ButtonB",             Src: @"Sega Genesis\6-Button\C.png", W: 44,  H: 44),
                new(Input: "ButtonY",             Src: @"Sega Genesis\6-Button\Y.png", W: 64,  H: 64),  // active (Block) — contrast OutRun
                new(Input: "ButtonY",             Src: @"Sega Genesis\6-Button\Y.png", W: 44,  H: 44),
                // shoulder buttons — active for the first time (High Punch / High Kick)
                new(Input: "ButtonLeftShoulder",  Src: @"Sega Genesis\6-Button\X.png", W: 64,  H: 42),
                new(Input: "ButtonLeftShoulder",  Src: "LineLB.png"),
                new(Input: "ButtonRightShoulder", Src: @"Sega Genesis\6-Button\Z.png", W: 64,  H: 42),
                new(Input: "ButtonRightShoulder", Src: "LineRB.png"),
                // per-input line decorations in the face-button Stack — all active
                new(Input: "ButtonX",             Src: "LineX.png"),
                new(Input: "ButtonA",             Src: "LineA.png"),
                new(Input: "ButtonB",             Src: "LineB.png"),  // active — contrast OutRun
                new(Input: "ButtonY",             Src: "LineY.png"),  // active — contrast OutRun
                // Start (mapped + labeled) and Mode (mapped, no label → Stack entry still dimmed)
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 44,  H: 44),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 34,  H: 34),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 44,  H: 44),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 34,  H: 34, Opacity: 0.3, BlurRadius: 6.0),
                // Dpad directionals — all active (game-specific Dpad labels)
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
                // disabled inputs — not in 6-Button mapping or no label
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

        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "ButtonX",              Text: "Low Punch"),
            new(Input: "ButtonLeftShoulder",   Text: "High Punch"),
            new(Input: "ButtonB",              Text: "Low Kick"),
            new(Input: "ButtonRightShoulder",  Text: "High Kick"),
            new(Input: "ButtonA",              Text: "Block"),
            new(Input: "ButtonY",              Text: "Block"),
            new(Input: "ButtonStart",          Text: "Pause"),
            new(Input: "ButtonDpadUp",         Text: "Jump"),
            new(Input: "ButtonDpadDown",       Text: "Crouch"),
            new(Input: "ButtonDpadLeft",       Text: "Back"),
            new(Input: "ButtonDpadRight",      Text: "Forward"),
            // analogToDigital="left" mirrors each Dpad label onto its left-stick equivalent
            new(Input: "AxisLeftStickUp",      Text: "Jump"),
            new(Input: "AxisLeftStickDown",    Text: "Crouch"),
            new(Input: "AxisLeftStickLeft",    Text: "Back"),
            new(Input: "AxisLeftStickRight",   Text: "Forward"));
            #pragma warning restore format
    }

    /// <summary>
    /// Per-game XML that both selects a non-default controller (3-Button) and remaps
    /// A → ButtonRightShoulder — verifies controller selection and a Remapped input compose:
    /// A drives ButtonRightShoulder while ButtonX, left without a mapping, renders Unmapped and dim.
    /// </summary>
    [Fact]
    public void SegaGenesis_Aladdin_PerGameControllerWithOverride()
    {
        var game = new GameInfo(
            Platform: "Sega Genesis",
            RomName: "Aladdin (USA)",
            CloneOf: null,
            EmulatorPath: null,
            RomDirectory: null,
            RetroArchCore: null);

        ControllerOverlayModel overlay = _service.Resolve(game);

        var t = overlay.InTemplate(@"Templates\Xbox Series X");
        t.ShouldHaveBaseImage("BaseImage.png", width: 1600, height: 1000);
        t.ShouldHaveImages(
                #pragma warning disable format
                // Per-game override: A → ButtonRightShoulder. Remapped(A) → Sega Genesis/A.png.
                new(Input: "ButtonRightShoulder", Src: @"Sega Genesis\A.png",     W: 64,  H: 42),
                new(Input: "ButtonRightShoulder", Src: "LineRB.png"),
                // 3-Button defaults: B → ButtonA (Jump), C → ButtonB (Throw) — MappedDefault, Genesis art
                new(Input: "ButtonA",             Src: @"Sega Genesis\B.png",     W: 64,  H: 64),
                new(Input: "ButtonA",             Src: @"Sega Genesis\B.png",     W: 44,  H: 44),
                new(Input: "ButtonA",             Src: "LineA.png"),
                new(Input: "ButtonB",             Src: @"Sega Genesis\C.png",     W: 64,  H: 64),
                new(Input: "ButtonB",             Src: @"Sega Genesis\C.png",     W: 44,  H: 44),
                new(Input: "ButtonB",             Src: "LineB.png"),
                // ButtonX: no current mapping (override moved A away); Unmapped → generic ButtonX.png dim
                new(Input: "ButtonX",             Src: "ButtonX.png",             W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonX",             Src: "ButtonX.png",             W: 44,  H: 44,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonX",             Src: "LineX.png",                               Opacity: 0.3, BlurRadius: 8.0),
                // ButtonY: not in 3-Button at all → Unmapped, dim
                new(Input: "ButtonY",             Src: "ButtonY.png",             W: 64,  H: 64,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonY",             Src: "ButtonY.png",             W: 44,  H: 44,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonY",             Src: "LineY.png",                               Opacity: 0.3, BlurRadius: 8.0),
                // Start active (Pause inherited); Back not in 3-Button → dim
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 44,  H: 44),
                new(Input: "ButtonStart",         Src: "ButtonStart.png",         W: 34,  H: 34),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 44,  H: 44,  Opacity: 0.3, BlurRadius: 6.0),
                new(Input: "ButtonBack",          Src: "ButtonBack.png",          W: 34,  H: 34,  Opacity: 0.3, BlurRadius: 6.0),
                // Dpad/sticks mapped but no directional labels → dim (game-specific mode → auto resolves to HasLabel)
                new(Input: "ButtonDpad",          Src: "ButtonDpad.png",          W: 135, H: 135, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisLeftStick",       Src: "AxisLeftStick.png",       W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisRightStick",      Src: "AxisRightStick.png",      W: 124, H: 124, Opacity: 0.3, BlurRadius: 8.0),
                // Inputs absent from 3-Button entirely → Unmapped, generic art, dim
                new(Input: "ButtonGuide",         Src: "ButtonGuide.png",         W: 68,  H: 68,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "AxisTriggerLeft.png",     W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerLeft",     Src: "LineLT.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "AxisTriggerRight.png",    W: 65,  H: 65,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "AxisTriggerRight",    Src: "LineRT.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder",  Src: "ButtonLeftShoulder.png",  W: 64,  H: 42,  Opacity: 0.3, BlurRadius: 8.0),
                new(Input: "ButtonLeftShoulder",  Src: "LineLB.png",                              Opacity: 0.3, BlurRadius: 8.0),
                new(Input: null,                  Src: "LineStart.png"));
                #pragma warning restore format

        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "ButtonRightShoulder", Text: "Sword"),     // A remapped here
            new(Input: "ButtonA",             Text: "Jump"),      // B (3-Button default)
            new(Input: "ButtonB",             Text: "Throw"),     // C (3-Button default)
            new(Input: "ButtonStart",         Text: "Pause"));    // inherited default
            #pragma warning restore format
    }

    /// <summary>
    /// CloneOf fallback: a clone with no files of its own inherits the parent's 3-Button selection
    /// and labels via both pipelines' CloneOf retry (<see cref="Labels.InputLabelsService"/> and
    /// <see cref="InputMapping.PerGameXmlMappingSource"/>). ButtonBack dim is the discriminator —
    /// 3-Button has no Mode mapping, where the 6-Button default would render it active.
    /// </summary>
    [Fact]
    public void SegaGenesis_OutRunBeta_CloneOfFallback_PerGameMapping()
    {
        var game = new GameInfo(
            Platform: "Sega Genesis",
            RomName: "OutRun (Beta)",                  // no InputMappings or Labels file
            CloneOf: "OutRun (USA, Europe)",           // parent has both
            EmulatorPath: null,
            RomDirectory: null,
            RetroArchCore: null);

        ControllerOverlayModel overlay = _service.Resolve(game);

        // Labels inherit via InputLabelsService's CloneOf retry.
        overlay.ShouldHaveLabel("ButtonX", "Brake");          // A → ButtonX (same in 3- and 6-Button)
        overlay.ShouldHaveLabel("ButtonA", "Accelerate");     // B → ButtonA

        // Mapping inherits via PerGameXmlMappingSource's CloneOf retry: no XML for the Beta, so it
        // retries the parent, which selects controller="3-Button". 3-Button has no Mode mapping,
        // so ButtonBack is dim (0.3, 6.0) — contrast the 6-Button default where Mode → ButtonBack
        // would be active at full opacity.
        overlay.ShouldHaveImage(
            "ButtonBack",
            @"Templates\Xbox Series X\ButtonBack.png".AsFixturePath(),
            expectedOpacity: 0.3,
            expectedBlurRadius: 6.0);
    }
}
