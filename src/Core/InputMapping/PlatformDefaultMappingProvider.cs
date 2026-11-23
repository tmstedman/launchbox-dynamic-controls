using DynamicControls.Config;

namespace DynamicControls.InputMapping;

/// <summary>
/// Lowest-priority input mapping provider: returns the platform's default controller (the one
/// the root names with <c>default="..."</c>, or the first declared if none) from Controllers.xml,
/// nothing remapped. Registered last so it only contributes when no higher-priority provider
/// applies. Returns null when the platform has no Controllers.xml or it declares no controllers —
/// the chain then falls back to an empty mapping.
/// </summary>
public class PlatformDefaultMappingProvider(ILogger logger) : IInputMappingProvider
{
    private readonly ILogger _logger = logger;

    public bool IsEnabled(GlobalConfig config) => true;

    public ProvidedMapping? Load(GameInfo game, PlatformControllersConfig? platform)
    {
        ControllerConfig? controller = platform?.Resolve(null);
        if (controller == null) return null;

        _logger.Debug($"Selected platform default controller: {controller.Name}");
        return ProvidedMapping.Unchanged(ControllerBaseline.Of(controller));
    }
}
