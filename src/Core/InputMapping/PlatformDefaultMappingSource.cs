using DynamicControls.Config;

namespace DynamicControls.InputMapping;

/// <summary>
/// Lowest-priority input mapping source: returns the platform's default controller (the one
/// flagged <c>default="true"</c>, or the first declared if none is flagged) from Controllers.xml.
/// Registered last so it only contributes when no higher-priority source applies. Returns null
/// when the platform has no Controllers.xml or it declares no controllers — the service then
/// falls back to an empty config.
/// </summary>
public class PlatformDefaultMappingSource(ILogger logger) : IInputMappingSource
{
    private readonly ILogger _logger = logger;

    public bool IsEnabled(GlobalConfig config) => true;

    public InputMappingConfig? Load(GameInfo game, PlatformControllersConfig? platform)
    {
        ControllerConfig? controller = platform?.Resolve(null);
        if (controller == null) return null;

        _logger.Debug($"Selected platform default controller: {controller.Name}");
        return new InputMappingConfig
        {
            Controller = controller.Name,
            AnalogToDigital = controller.AnalogToDigital,
            Mappings = [.. controller.Mappings]
        };
    }
}
