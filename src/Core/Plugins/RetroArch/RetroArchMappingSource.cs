using DynamicControls.Config;
using DynamicControls.InputMapping;

namespace DynamicControls.Plugins.RetroArch;

/// <summary>
/// Input mapping source for RetroArch games. Answers one question: which controller is the player
/// using? It reads the <c>input_libretro_device_p1</c> override from the cfg and remap cascades
/// (the remap wins) and loads that variant's buttons from the platform's Controllers.xml. When
/// neither picks a variant, the platform default is used instead.
///
/// <para>Building a mapping from nothing is what makes this a source. The player's button swaps
/// are a separate job and live in <see cref="RetroArchSwapTransform"/> — they amend a mapping
/// that already exists, which is what a transform is for. Keeping them apart is what lets
/// <c>ResolvedMapping</c>'s pre-config snapshot be taken between the two, so a swap is visible as
/// a remap and a whole-control label can follow its directions.</para>
///
/// <para>Returns null when RetroArch's configuration says nothing about this game, or when the
/// selected variant has no matching <c>&lt;Controller&gt;</c> — the source chain then falls
/// through to the platform default.</para>
/// </summary>
public class RetroArchMappingSource(
    ILogger logger,
    IRetroArchGameOverridesResolver overridesResolver) : IInputMappingSource
{
    private readonly ILogger _logger = logger;
    private readonly IRetroArchGameOverridesResolver _overridesResolver = overridesResolver;

    public bool IsEnabled(GlobalConfig config) => config.EnableRetroArch;

    public InputMappingConfig? Load(GameInfo game, PlatformControllersConfig? platform)
    {
        if (platform == null) return null;

        RetroArchGameOverrides? overrides = _overridesResolver.Resolve(game);
        if (overrides == null) return null;

        // The remap file wins over the cfg cascade for variant selection. A null variant means
        // neither picked one, and platform.Resolve falls back to the platform default.
        string? variant = (overrides.Rmp?.Variant ?? overrides.Cfg?.Variant)?.Name;
        if (variant != null) _logger.Debug($"RetroArch: effective variant '{variant}'");

        ControllerConfig? baseConfig = platform.Resolve(variant);
        if (baseConfig == null)
        {
            _logger.Error($"RetroArch: variant '{variant}' not found in '{game.Platform}/Controllers.xml'");
            return null;
        }

        return new InputMappingConfig
        {
            Controller = baseConfig.Name,
            AnalogToDigital = baseConfig.AnalogToDigital,
            Mappings = [.. baseConfig.Mappings]
        };
    }
}
