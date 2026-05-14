using DynamicControls.Composition;
using static DynamicControls.Core.IntegrationTests.TestHelpers.TestPaths;

namespace DynamicControls.Core.IntegrationTests.EndToEnd;

/// <summary>
/// Full-pipeline tests for the two-tier Defaults/User config layering. Each test exercises one
/// layer seam end-to-end: the User file is resolved first and wins; the Defaults file is the
/// fallback when no User counterpart exists. The existing platform test suites cover all the
/// rendering scenarios; these tests focus only on which file was read.
/// </summary>
public class LayeredConfigEndToEndTests
{
    private readonly ControllerOverlayService _service = ControllerOverlayFactory.Create(FixturesRoot);

    /// <summary>
    /// User/InputMappings shadows Defaults/InputMappings: the Defaults file selects 3-Button but
    /// the User file selects 6-Button. The 6-Button-only Y button maps to ButtonY, so the
    /// Defaults labels file's Y="Special Attack" lands on ButtonY only when the User mapping wins.
    /// If the Defaults mapping (3-Button) were used instead, Y has no mapping and the label drops.
    /// </summary>
    [Fact]
    public void SegaGenesis_UserInputMappingsFileWinsOverDefaults()
    {
        // given a game whose Defaults mapping selects 3-Button but User mapping selects 6-Button
        var game = new GameInfo(
            Platform: "Sega Genesis",
            RomName: "UserMappingTest",
            CloneOf: null,
            EmulatorPath: null,
            RomDirectory: null,
            RetroArchCore: null);

        // when the service resolves the overlay
        ControllerOverlayModel overlay = _service.Resolve(game);

        // then ButtonY carries the label — proving the User mapping (6-Button, which has Y) was used,
        // not the Defaults mapping (3-Button, which has no Y and would drop the label entirely)
        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "ButtonY",     Text: "Special Attack"),
            new(Input: "ButtonStart", Text: "Pause"));      // inherited from _DefaultLabels.xml
            #pragma warning restore format
    }

    /// <summary>
    /// User/Labels shadows Defaults/Labels: the Defaults labels file has A="Defaults Brake" but
    /// the User labels file has A="User Brake". Since A maps to ButtonX on the 6-Button default
    /// controller, the label text on ButtonX reveals which file was read.
    /// </summary>
    [Fact]
    public void SegaGenesis_UserLabelsFileWinsOverDefaults()
    {
        // given a game with no InputMappings override (falls back to 6-Button default), a Defaults
        // labels file, and a User labels file that overrides the same button with different text
        var game = new GameInfo(
            Platform: "Sega Genesis",
            RomName: "UserLabelsTest",
            CloneOf: null,
            EmulatorPath: null,
            RomDirectory: null,
            RetroArchCore: null);

        // when the service resolves the overlay
        ControllerOverlayModel overlay = _service.Resolve(game);

        // then the label on ButtonX comes from the User file ("User Brake"), not the Defaults file
        // ("Defaults Brake") — proving the User labels file was resolved and the Defaults file was
        // never opened
        overlay.ShouldHaveLabels(
            #pragma warning disable format
            new(Input: "ButtonX",     Text: "User Brake"),
            new(Input: "ButtonStart", Text: "Pause"));      // inherited from _DefaultLabels.xml
            #pragma warning restore format
    }
}
