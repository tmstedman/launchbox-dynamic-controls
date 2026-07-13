using DynamicControls.Composition;
using static DynamicControls.Core.IntegrationTests.TestHelpers.TestPaths;

namespace DynamicControls.Core.IntegrationTests.EndToEnd;

/// <summary>
/// Full-pipeline test exercising transitive <c>inheritFrom</c>: NEC PC Engine's Controllers.xml
/// declares a three-level chain — 2-Button (base) → 3-Button (adds III) → 6-Button (adds IV/V/VI).
/// A per-game XML selects 6-Button, so a correct resolution must accumulate every ancestor's
/// mappings. The labels then land on the generic input each platform button drives, and the slot
/// each label reaches is the discriminator:
/// <list type="bullet">
///   <item>I → ButtonB, II → ButtonA — inherited from the <b>grandparent</b> (2-Button)</item>
///   <item>III → ButtonX — inherited from the <b>parent</b> (3-Button)</item>
///   <item>IV → ButtonY — the leaf's <b>own</b> mapping (6-Button)</item>
///   <item>Run → ButtonStart — an inheritable default, also from the grandparent (2-Button)</item>
/// </list>
/// If inheritance were only one level deep, the grandparent's I/II/Run mappings would be missing
/// and those labels would have nowhere to land.
/// </summary>
public class NecPcEngineEndToEndTests
{
    private readonly ControllerOverlayService _service = ControllerOverlayFactory.Create(FixturesRoot);

    [Fact]
    public void NecPcEngine_StreetFighter_SixButton_ResolvesTransitiveInheritance()
    {
        var game = new GameInfo(
            Platform: "NEC PC Engine",
            RomName: "Street Fighter II Dash (Japan)",   // InputMappings picks the 6-Button controller
            CloneOf: null,
            LaunchBoxId: null,
            EmulatorPath: null,
            RomDirectory: null,
            RetroArchCore: null);

        ControllerOverlayModel overlay = _service.Resolve(game);

        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "ButtonB",     Text: "Light Punch"),   // I   — grandparent (2-Button)
            new(Input: "ButtonA",     Text: "Medium Punch"),  // II  — grandparent (2-Button)
            new(Input: "ButtonX",     Text: "Heavy Punch"),   // III — parent (3-Button)
            new(Input: "ButtonY",     Text: "Light Kick"),    // IV  — own (6-Button)
            new(Input: "ButtonStart", Text: "Pause"));        // Run — inherited default, grandparent
            #pragma warning restore format
    }
}
