using DynamicControls.Composition;
using DynamicControls.Infrastructure;
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
        // Derived from this assembly rather than the entry assembly: the plugin DLL always has a
        // location, where GetEntryAssembly can be null depending on how the host loaded us, and a
        // throw here takes the whole plugin down before anything can report why.
        string pluginDir = Path.GetDirectoryName(typeof(DynamicControlsPlugin).Assembly.Location)!;
        string launchBoxDir = Path.GetFullPath(Path.Combine(pluginDir, "..", ".."));
        string rootDir = Path.Combine(launchBoxDir, "Data", "Dynamic Controls");

        ILogger logger = Logger.ForRoot(new SystemFileSystem(), rootDir);
        try
        {
            ControllerOverlayService overlayService = ControllerOverlayFactory.Create(rootDir);

            _handler = new OnGameLaunchHandler(
                overlayService,
                new RetroArchCoreResolver(),
                DynamicControlsViewModel.Instance,
                logger);

            logger.Info($"Plugin loaded. Plugin dir: {pluginDir}");
        }
        catch (Exception ex)
        {
            // Without this the plugin dies during construction and leaves no trace anywhere.
            logger.Error($"Plugin failed to load: {ex}");
            throw;
        }
    }

    public void OnBeforeGameLaunching(IGame game, IAdditionalApplication app, IEmulator emulator) =>
        _handler.OnBeforeGameLaunching(game, emulator);

    public void OnAfterGameLaunched(IGame game, IAdditionalApplication app, IEmulator emulator) { }

    public void OnGameExited() { }
}
