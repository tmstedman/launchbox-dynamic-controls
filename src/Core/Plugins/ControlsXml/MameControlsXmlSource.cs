using DynamicControls.Config;
using DynamicControls.Labels;
using DynamicControls.Plugins.Mame;

namespace DynamicControls.Plugins.ControlsXml;

/// <summary>
/// MAME-gated <see cref="IInputLabelsLoader"/> backed by the BYOAC controls.xml file.
/// Filters out non-MAME games at the source level and delegates the actual file read and
/// lookup to <see cref="ControlsXmlLoader"/>.
/// </summary>
public class MameControlsXmlSource(IControlsXmlLoader loader) : IInputLabelsLoader
{
    private readonly IControlsXmlLoader _loader = loader;

    public bool IsEnabled(GlobalConfig config) => true;

    public InputLabelsConfig? LoadDefaultLabels(string platform) => null;

    public InputLabelsConfig? Load(GameInfo game)
    {
        if (game.EmulatorPath == null || !MameEmulator.IsMameExecutable(game.EmulatorPath)) return null;
        return _loader.Lookup(game.RomName);
    }
}
