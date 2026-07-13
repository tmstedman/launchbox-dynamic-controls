using DynamicControls.Config;

namespace DynamicControls.InputMapping;

/// <summary>
/// Highest-priority input mapping source: looks for a per-game XML at
/// Platforms/&lt;Platform&gt;/InputMappings/&lt;RomName&gt;.xml. When present, this source resolves
/// the controller via the XML's `controller="..."` attribute (falling back to the platform
/// default when the attribute is absent or names an unknown controller) and layers the XML's
/// mappings on top of that controller's baseline. Per-game entries override the baseline by
/// Name — all baseline entries sharing that Name are replaced by the per-game entries. Names
/// not mentioned in the per-game XML keep their baseline mappings unchanged.
///
/// AnalogToDigital is inherited from the controller if the per-game XML doesn't set it. A
/// per-game XML that contains only `controller="..."` (no &lt;Mapping&gt; entries) effectively
/// means "use this controller as-is, no overrides."
///
/// When no per-game XML exists for the rom and <see cref="GameInfo.CloneOf"/> is set, the
/// lookup retries with the parent rom — matching the same CloneOf retry that
/// <c>InputLabelsService.LoadGameLabels</c> performs.
/// </summary>
public class PerGameXmlMappingSource(ILogger logger, IInputMappingLoader loader) : IInputMappingSource
{
    private readonly ILogger _logger = logger;
    private readonly IInputMappingLoader _loader = loader;

    public bool IsEnabled(GlobalConfig config) => true;

    public InputMappingConfig? Load(GameInfo game, PlatformControllersConfig? platform)
    {
        if (game.RomName == null) return null;

        InputMappingConfig? config = _loader.LoadGameMapping(game);
        if (config == null && !string.IsNullOrEmpty(game.CloneOf))
            config = _loader.LoadGameMapping(game with { RomName = game.CloneOf });
        if (config == null) return null;

        ControllerConfig? baseline = platform?.Resolve(config.Controller);
        if (config.Controller != null && baseline == null)
            _logger.Error($"Game selected unknown controller '{config.Controller}', falling back to platform default");
        baseline ??= platform?.Resolve(null);

        config.Controller = baseline?.Name;
        config.AnalogToDigital ??= baseline?.AnalogToDigital;

        if (baseline != null)
            config.Mappings = MappingOverlay.Apply(baseline.Mappings, config.Mappings, config.Unmaps);

        return config;
    }
}
