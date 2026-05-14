using System.Diagnostics.CodeAnalysis;
using DynamicControls.Config;
using DynamicControls.Labels;
using DynamicControls.Plugins.ControlsXml;
using DynamicControls.Plugins.Mame;

namespace DynamicControls.Composition;

/// <summary>
/// Wires the input-labels subsystem. Builds every loader, hands them to
/// <see cref="InputLabelsPlugins"/> for enable-filtering, and injects the plugins object into the
/// service. No logic lives here — gating is the plugin's own concern; filtering is
/// <see cref="InputLabelsPlugins"/>' concern.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class InputLabelsFactory
{
    public static InputLabelsService Create(
        LayeredFileSystem lfs,
        ILogger logger,
        GlobalConfig? config = null)
    {
        config ??= new GlobalConfig();

        var defaultLoader = new InputLabelsLoader(logger, lfs);
        var controlsXmlLoader = new ControlsXmlLoader(logger, lfs);
        IInputLabelsLoader[] additional =
        [
            new MameControlsXmlSource(controlsXmlLoader),
        ];

        var plugins = new InputLabelsPlugins(defaultLoader, additional, config);
        return new InputLabelsService(logger, plugins);
    }
}
