using Unbroken.LaunchBox.Plugins.Data;
using DynamicControls;
using DynamicControls.Rendering;
using DynamicControls.Plugins.RetroArch;

namespace DynamicControls.LaunchBox;

/// <summary>
/// Handles a single game-launching event from LaunchBox: validates inputs, builds a
/// <see cref="GameInfo"/>, runs the overlay service, and pushes the result onto the bound view model.
/// All of <see cref="DynamicControlsPlugin"/>'s per-launch work lives here so the plugin entry
/// point can stay a thin DI host — this class is unit-testable, the plugin isn't (its
/// parameterless constructor bootstraps the LaunchBox environment).
/// </summary>
public class OnGameLaunchHandler(
    IControllerOverlayService overlayService,
    IRetroArchCoreResolver retroArchCoreResolver,
    IDynamicControlsViewModel viewModel)
{
    private readonly IControllerOverlayService _overlayService = overlayService;
    private readonly IRetroArchCoreResolver _retroArchCoreResolver = retroArchCoreResolver;
    private readonly IDynamicControlsViewModel _viewModel = viewModel;

    /// <summary>
    /// Resolves the controller overlay for the given game and updates the bound view model.
    /// Skips silently if game or its path is missing. Resolution errors are handled and
    /// logged inside <see cref="ControllerOverlayService.Resolve"/>, which returns an empty
    /// model so the pause screen doesn't show stale overlay data.
    /// </summary>
    public void OnBeforeGameLaunching(IGame? game, IEmulator? emulator)
    {
        if (game?.ApplicationPath == null) return;

        string romName = Path.GetFileNameWithoutExtension(game.ApplicationPath);
        string platform = game.Platform;
        if (string.IsNullOrEmpty(romName) || string.IsNullOrEmpty(platform)) return;

        var gameInfo = new GameInfo(
            Platform: platform,
            RomName: romName,
            CloneOf: game.CloneOf,
            LaunchBoxId: game.LaunchBoxDbId?.ToString(),
            EmulatorPath: emulator?.ApplicationPath,
            RomDirectory: Path.GetDirectoryName(game.ApplicationPath),
            RetroArchCore: _retroArchCoreResolver.Resolve(emulator, platform));

        ControllerOverlayModel overlay = _overlayService.Resolve(gameInfo);
        _viewModel.ControlsImagePath = overlay.ImagePath;
        _viewModel.CanvasWidth = overlay.CanvasWidth;
        _viewModel.CanvasHeight = overlay.CanvasHeight;
        _viewModel.InputLabels = overlay.InputLabels;
        _viewModel.RenderedImages = overlay.RenderedImages;
    }
}
