using DynamicControls.Config;

namespace DynamicControls.InputMapping;

/// <summary>
/// Highest-priority input mapping provider: looks for the game's &lt;Game&gt; entry in the platform's
/// Controllers.xml (<see cref="PlatformControllersConfig.FindGame"/>). When there is one, this
/// provider supplies the whole mapping — it resolves the controller the entry inheritsFrom
/// (falling back to the platform default when it names none or an unknown controller) and layers
/// the entry's mappings on top of that controller's baseline. The entry's mappings override the
/// baseline by Name — all baseline entries sharing that Name are replaced by the entry's. Names
/// the entry doesn't mention keep their baseline mappings unchanged.
///
/// <para>An entry that names no controller is read against the platform default, never the pad an
/// emulator selected: its button names come from some controller's vocabulary, and another pad may
/// not have them. Because this provider answers first and supplies everything, an emulator's
/// controller choice and swaps are not consulted for a game that has an entry — the entry stands
/// in for what the emulator providers do, for an emulator the plugin cannot read.</para>
///
/// AnalogToDigital is inherited from the controller if the entry doesn't set it. An entry that
/// only names a controller (no &lt;Mapping&gt; entries) effectively means "use this controller
/// as-is, no overrides."
///
/// <para>Unlike labels, a clone does not inherit its parent's entry. Labels describe the game, which
/// a clone shares; a mapping describes the player's setup, which MAME and RetroArch keep per rom, so
/// an entry written for the parent would otherwise switch off a configuration written for the clone.</para>
/// </summary>
public class PerGameXmlMappingProvider(ILogger logger) : IInputMappingProvider
{
    private readonly ILogger _logger = logger;

    public bool IsEnabled(GlobalConfig config) => true;

    public ProvidedMapping? Load(GameInfo game, PlatformControllersConfig? platform)
    {
        if (game.RomName == null) return null;

        GameMappingConfig? entry = platform?.FindGame(game);
        if (entry == null) return null;
        var config = new InputMappingConfig
        {
            Controller = entry.InheritFrom,
            AnalogToDigital = entry.AnalogToDigital,
            Mappings = [.. entry.Mappings],
            Unmaps = [.. entry.Unmaps],
        };

        // Nothing after this provider is asked, so say so: a user wondering why their RetroArch or
        // MAME configuration has no effect finds the answer here.
        _logger.Debug($"Per-game mapping for '{game.RomName}' supplies the whole mapping; RetroArch's and MAME's configuration is not read for this game");
        if (config.Controller == null)
            _logger.Debug($"Per-game mapping for '{game.RomName}' names no controller, reading it against the platform default");

        ControllerConfig? baseline = platform?.Resolve(config.Controller);
        if (config.Controller != null && baseline == null)
            _logger.Error($"Game selected unknown controller '{config.Controller}', falling back to platform default");
        baseline ??= platform?.Resolve(null);

        config.Controller = baseline?.Name;
        config.AnalogToDigital ??= baseline?.AnalogToDigital;

        // With no controller to read against there is nothing for the entry to remap.
        if (baseline == null) return ProvidedMapping.Unchanged(config);

        // The controller as Controllers.xml declares it is the natural state, so a button the entry
        // moves classifies as remapped, the same as one an emulator's config moves. The mirror is a
        // statement about the hardware, so the natural state takes the entry's value of it.
        InputMappingConfig natural = ControllerBaseline.Of(baseline) with { AnalogToDigital = config.AnalogToDigital };
        config.Mappings = MappingOverlay.Apply(baseline.Mappings, config.Mappings, config.Unmaps);

        return new ProvidedMapping(natural, config);
    }
}
