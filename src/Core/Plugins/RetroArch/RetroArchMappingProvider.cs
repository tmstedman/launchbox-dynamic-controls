using DynamicControls.Config;
using DynamicControls.InputMapping;

namespace DynamicControls.Plugins.RetroArch;

/// <summary>
/// Input mapping provider for RetroArch games. Answers two questions: which controller is the
/// player using, and which of its buttons have they swapped?
///
/// <para>The controller comes from the <c>input_libretro_device_p1</c> override in the cfg and
/// remap cascades (the remap wins), loaded from that variant's buttons in the platform's
/// Controllers.xml; with neither picking a variant, or a variant the platform doesn't declare (an
/// error is logged), the platform default is used instead. That controller is the natural state.
/// The player's swaps are then applied to a copy of it, the cfg cascade's first and the remap
/// file's on top so the remap wins where the two disagree. Keeping the two states apart is what
/// lets a swap be seen as a remap, and a whole-control label follow its directions.</para>
///
/// <para>It names the natural mapping as the Dpad a stick standing in for the Dpad presses
/// (<see cref="InputMappingConfig.DpadMirrorSource"/>): RetroArch works out that stick's Dpad
/// presses from the stick itself, after remapping, so a swap of the Dpad's buttons never reaches
/// it.</para>
///
/// <para>Returns null when RetroArch's configuration says nothing about this game (or the platform
/// declares no controllers file), leaving the chain to fall through to the platform default.</para>
/// </summary>
public class RetroArchMappingProvider(
    ILogger logger,
    IRetroArchGameOverridesResolver overridesResolver,
    IRetroArchSwapApplier swapApplier) : IInputMappingProvider
{
    private readonly ILogger _logger = logger;
    private readonly IRetroArchGameOverridesResolver _overridesResolver = overridesResolver;
    private readonly IRetroArchSwapApplier _swapApplier = swapApplier;

    public bool IsEnabled(GlobalConfig config) => config.EnableRetroArch;

    public ProvidedMapping? Load(GameInfo game, PlatformControllersConfig? platform)
    {
        if (platform == null) return null;

        RetroArchGameOverrides? overrides = _overridesResolver.Resolve(game);
        if (overrides == null) return null;

        // The remap file wins over the cfg cascade for variant selection. A null variant means
        // neither picked one, and platform.Resolve falls back to the platform default.
        string? variant = (overrides.Rmp?.Variant ?? overrides.Cfg?.Variant)?.Name;
        if (variant != null) _logger.Debug($"RetroArch: effective variant '{variant}'");

        ControllerConfig? controller = platform.Resolve(variant);
        if (controller == null)
        {
            _logger.Error($"RetroArch: variant '{variant}' not found in '{game.Platform}/Controllers.xml'");
            controller = platform.Resolve(null);
        }

        InputMappingConfig natural = controller != null ? ControllerBaseline.Of(controller) : new InputMappingConfig();

        int cfgSwaps = overrides.Cfg?.Swaps.Count ?? 0;
        int rmpSwaps = overrides.Rmp?.Swaps.Count ?? 0;
        if (cfgSwaps == 0 && rmpSwaps == 0) return ProvidedMapping.Unchanged(natural);

        // cfg first, then rmp on top — each call produces a new config that feeds the next layer.
        InputMappingConfig current = natural;
        if (overrides.Cfg != null && cfgSwaps > 0)
        {
            current = _swapApplier.Apply(current, overrides.Cfg.Swaps);
            _logger.Debug($"RetroArch cfg swaps applied: {cfgSwaps} swaps");
        }
        if (overrides.Rmp != null && rmpSwaps > 0)
        {
            current = _swapApplier.Apply(current, overrides.Rmp.Swaps);
            _logger.Debug($"RetroArch remap applied: {rmpSwaps} swaps");
        }

        return new ProvidedMapping(natural, current with { DpadMirrorSource = natural.Mappings });
    }
}
