using DynamicControls.Config;

namespace DynamicControls.InputMapping;

/// <summary>
/// Highest-priority input mapping provider: looks for a per-game XML at
/// Platforms/&lt;Platform&gt;/ControllerOverrides/&lt;RomName&gt;.xml. When present, this provider supplies
/// the whole mapping — it resolves the controller via the XML's `controller="..."` attribute
/// (falling back to the platform default when the attribute is absent or names an unknown
/// controller) and layers the XML's mappings on top of that controller's baseline. Per-game
/// entries override the baseline by Name — all baseline entries sharing that Name are replaced by
/// the per-game entries. Names not mentioned in the per-game XML keep their baseline mappings
/// unchanged.
///
/// <para>A file that names no controller is read against the platform default, never the pad an
/// emulator selected: its button names come from some controller's vocabulary, and another pad may
/// not have them. Because this provider answers first and supplies everything, an emulator's
/// controller choice and swaps are not consulted for a game that has an override — the file stands
/// in for what the emulator providers do, for an emulator the plugin cannot read.</para>
///
/// AnalogToDigital is inherited from the controller if the per-game XML doesn't set it. A
/// per-game XML that contains only `controller="..."` (no &lt;Mapping&gt; entries) effectively
/// means "use this controller as-is, no overrides."
///
/// <para>Unlike labels, a clone does not inherit its parent's file. Labels describe the game, which
/// a clone shares; a mapping describes the player's setup, which MAME and RetroArch keep per rom, so
/// a file written for the parent would otherwise switch off a configuration written for the clone.</para>
/// </summary>
public class PerGameXmlMappingProvider(ILogger logger, IInputMappingLoader loader) : IInputMappingProvider
{
    private readonly ILogger _logger = logger;
    private readonly IInputMappingLoader _loader = loader;

    public bool IsEnabled(GlobalConfig config) => true;

    public ProvidedMapping? Load(GameInfo game, PlatformControllersConfig? platform)
    {
        if (game.RomName == null) return null;

        InputMappingConfig? config = _loader.LoadGameMapping(game);
        if (config == null) return null;

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

        // With no controller to read against there is nothing for the file to remap.
        if (baseline == null) return ProvidedMapping.Unchanged(config);

        // The controller as Controllers.xml declares it is the natural state, so a button the file
        // moves classifies as remapped, the same as one an emulator's config moves. The mirror is a
        // statement about the hardware, so the natural state takes the file's value of it.
        InputMappingConfig natural = ControllerBaseline.Of(baseline) with { AnalogToDigital = config.AnalogToDigital };
        config.Mappings = MappingOverlay.Apply(baseline.Mappings, config.Mappings, config.Unmaps);

        return new ProvidedMapping(natural, config);
    }
}
