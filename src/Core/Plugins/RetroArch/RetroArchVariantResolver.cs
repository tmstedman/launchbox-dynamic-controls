namespace DynamicControls.Plugins.RetroArch;

public interface IRetroArchVariantResolver
{
    RetroArchControllerConfig? Resolve(
        RetroArchGameData data,
        RetroArchCoreConfig coreConfig,
        string coreDisplayName);
}

/// <summary>
/// Resolves the effective controller variant from <c>input_libretro_device_p1</c>, walking the
/// cascade levels from most specific to least (Game → ContentDir → Core → Global) and stopping
/// at the first level that contains the key. Device type 1 (RETRO_DEVICE_JOYPAD) is resolved
/// like any other ID when declared in the core XML; when absent from the core XML it is treated
/// silently as "no override" (no error) since it is RetroArch's implicit default. The
/// <paramref name="logSource"/> constructor parameter ("cfg" or "remap") distinguishes the two
/// in log output.
/// </summary>
public class RetroArchVariantResolver(ILogger logger, string logSource) : IRetroArchVariantResolver
{
    private readonly ILogger _logger = logger;
    private readonly string _logSource = logSource;

    public RetroArchControllerConfig? Resolve(
        RetroArchGameData data,
        RetroArchCoreConfig coreConfig,
        string coreDisplayName)
    {
        Dictionary<string, string>?[] cascade = [data.Game, data.ContentDir, data.Core, data.Global];

        foreach (Dictionary<string, string>? entries in cascade)
        {
            if (entries == null || !entries.TryGetValue("input_libretro_device_p1", out string? deviceTypeStr))
                continue;

            if (!int.TryParse(deviceTypeStr, out int deviceType))
            {
                _logger.Debug($"RetroArch {_logSource}: no device type override");
                return null;
            }

            RetroArchControllerConfig? variant = coreConfig.SelectController(deviceType);
            if (variant == null)
            {
                if (deviceType != 1)
                    _logger.Error($"RetroArch {_logSource}: device type {deviceType} not declared in core XML for '{coreDisplayName}'");
                else
                    _logger.Debug($"RetroArch {_logSource}: no device type override");
                return null;
            }
            _logger.Debug($"RetroArch {_logSource}: selected variant '{variant.Name}' for device type {deviceType}");
            return variant;
        }

        _logger.Debug($"RetroArch {_logSource}: no device type override");
        return null;
    }
}
