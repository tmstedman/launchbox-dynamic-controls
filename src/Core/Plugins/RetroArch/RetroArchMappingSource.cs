using DynamicControls.Config;
using DynamicControls.InputMapping;

namespace DynamicControls.Plugins.RetroArch;

/// <summary>
/// Input mapping source for RetroArch games. When a per-core file at
/// <c>{rootDir}/Data/RetroArch/{coreDisplayName}.xml</c> exists, this source resolves the effective
/// controller variant by reading the cfg/rmp cascade for an <c>input_libretro_device_p1</c>
/// override (rmp wins over cfg) and then loads the variant's button mappings from the platform's
/// Controllers.xml. Game-level cfg button swaps are applied to the base first, then rmp swaps on
/// top (rmp wins on conflicts). When neither cfg nor rmp pick a variant, the platform default
/// (Controllers.xml's <c>default="true"</c>) is used as the base; cfg and rmp swaps still apply.
/// Returns null when no per-core file exists, when nothing in the chain contributes, or when the
/// selected variant has no matching <c>&lt;Controller&gt;</c> in Controllers.xml — the source
/// chain then falls through to the platform default.
/// </summary>
public class RetroArchMappingSource(
    ILogger logger,
    IRetroArchCoreInfo coreInfo,
    IRetroArchCoreLoader coreLoader,
    IRetroArchOverridesResolver cfgResolver,
    IRetroArchOverridesResolver remapResolver,
    IRetroArchSwapApplier swapApplier) : IInputMappingSource
{
    private readonly ILogger _logger = logger;
    private readonly IRetroArchCoreInfo _coreInfo = coreInfo;
    private readonly IRetroArchCoreLoader _coreLoader = coreLoader;
    private readonly IRetroArchOverridesResolver _cfgResolver = cfgResolver;
    private readonly IRetroArchOverridesResolver _remapResolver = remapResolver;
    private readonly IRetroArchSwapApplier _swapApplier = swapApplier;

    public bool IsEnabled(GlobalConfig config) => config.EnableRetroArch;

    public InputMappingConfig? Load(GameInfo game, PlatformControllersConfig? platform)
    {
        if (platform == null) return null;

        // Gate: only applies to RetroArch games that have a per-core XML file in rootDir/Data/RetroArch/.
        if (game.EmulatorPath == null || game.RomName == null || game.RetroArchCore == null) return null;
        if (!RetroArchEmulator.IsRetroArchExecutable(game.EmulatorPath)) return null;

        string retroArchDir = Path.GetDirectoryName(game.EmulatorPath)!;

        // Resolve the core's display name (from the .info file) — this is the directory name
        // RetroArch uses under config/ and config/remaps/, e.g. "Genesis Plus GX" not "genesis_plus_gx_libretro".
        // Fall back to the DLL name if the .info file is missing.
        string coreDisplayName = _coreInfo.ReadDisplayName(retroArchDir, game.RetroArchCore) ?? game.RetroArchCore;

        // Load the per-core XML (rootDir/Data/RetroArch/{coreDisplayName}.xml). This declares which
        // controller variants the core supports and which RetroArch device-type IDs select each one.
        // Null means no per-core file exists — this source has nothing to contribute.
        RetroArchCoreConfig? coreConfig = _coreLoader.Load(coreDisplayName);
        if (coreConfig == null) return null;

        // Read both layers of input in parallel:
        //   cfg — variant override (input_libretro_device_p1) and game-level button swaps
        //   rmp — variant override and slot swaps from the .rmp file
        RetroArchInputOverrides? cfg = _cfgResolver.Parse(retroArchDir, coreDisplayName, coreConfig, game);
        RetroArchInputOverrides? rmp = _remapResolver.Parse(retroArchDir, coreDisplayName, coreConfig, game);
        if (cfg == null && rmp == null) return null;

        // rmp wins over cfg for variant selection. If neither picked a variant, we'll use the
        // platform default as the base and still apply any swaps on top.
        string? variant = (rmp?.Variant ?? cfg?.Variant)?.Name;
        if (variant != null) _logger.Debug($"RetroArch: effective variant '{variant}'");

        // Resolve the base mapping from Controllers.xml using the selected variant name, or fall
        // back to the platform default when no variant was selected.
        ControllerConfig? baseConfig = platform.Resolve(variant);
        if (baseConfig == null)
        {
            _logger.Error($"RetroArch: variant '{variant}' not found in '{game.Platform}/Controllers.xml'");
            return null;
        }

        // Apply swaps in priority order: cfg first (game-level cfg is the baseline rmp operates on),
        // then rmp on top. Each call to ApplyRemap produces a new config; the previous result feeds
        // into the next layer as the base.
        InputMappingConfig current = new()
        {
            Controller = baseConfig.Name,
            AnalogToDigital = baseConfig.AnalogToDigital,
            Mappings = [.. baseConfig.Mappings]
        };
        if (cfg != null)
        {
            current = _swapApplier.Apply(current, cfg.Swaps);
            _logger.Debug($"RetroArch cfg swaps applied: {cfg.Swaps.Count} swaps");
        }
        if (rmp != null)
        {
            current = _swapApplier.Apply(current, rmp.Swaps);
            _logger.Debug($"RetroArch remap applied: {rmp.Swaps.Count} swaps");
        }
        return current;
    }
}
