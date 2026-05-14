using DynamicControls.Composition;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace DynamicControls.LaunchBox;

/// <summary>
/// LaunchBox plugin entry point. Derives the root directory from the LaunchBox install path,
/// delegates all Core wiring to <see cref="ControllerOverlayFactory"/>, and wires the
/// LaunchBox-specific pieces on top.
/// </summary>
public class DynamicControlsPlugin : IGameLaunchingPlugin
{
    private readonly OnGameLaunchHandler _handler;

    public DynamicControlsPlugin()
    {
        string launchBoxDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()!.Location)!;
        string rootDir = Path.Combine(launchBoxDir, "Data", "Dynamic Controls");

        ControllerOverlayService overlayService = ControllerOverlayFactory.Create(rootDir);

        _handler = new OnGameLaunchHandler(
            overlayService,
            new RetroArchCoreResolver(),
            DynamicControlsViewModel.Instance);
    }

    public void OnBeforeGameLaunching(IGame game, IAdditionalApplication app, IEmulator emulator) =>
        _handler.OnBeforeGameLaunching(game, emulator);

    public void OnAfterGameLaunched(IGame game, IAdditionalApplication app, IEmulator emulator) { }

    public void OnGameExited() { }
}
