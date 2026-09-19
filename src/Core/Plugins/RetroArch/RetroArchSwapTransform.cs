using DynamicControls.Config;
using DynamicControls.InputMapping;

namespace DynamicControls.Plugins.RetroArch;

/// <summary>
/// Applies the player's RetroArch button swaps on top of whichever mapping the source chain
/// produced. The cfg cascade's swaps go on first, then the remap file's, so the remap wins where
/// the two disagree.
///
/// <para>This used to happen inside <see cref="RetroArchMappingSource"/>. It is a transform
/// because it amends a mapping rather than building one, and separating it matters beyond
/// tidiness: <c>InputMappingService</c> snapshots the mapping between the source and the
/// transform, so with the swaps inside the source they were baked into the "before" state. That
/// made a swap invisible as a remap, and left a whole-control label unable to follow its
/// directions — the same shape MAME's cfg has always had.</para>
///
/// <para>Returns null when RetroArch's configuration contributes no swaps, leaving the source's
/// mapping as it stands.</para>
/// </summary>
public class RetroArchSwapTransform(
    ILogger logger,
    IRetroArchGameOverridesResolver overridesResolver,
    IRetroArchSwapApplier swapApplier) : IInputMappingTransform
{
    private readonly ILogger _logger = logger;
    private readonly IRetroArchGameOverridesResolver _overridesResolver = overridesResolver;
    private readonly IRetroArchSwapApplier _swapApplier = swapApplier;

    public bool IsEnabled(GlobalConfig config) => config.EnableRetroArch;

    public InputMappingConfig? Transform(GameInfo game, InputMappingConfig baseline)
    {
        RetroArchGameOverrides? overrides = _overridesResolver.Resolve(game);
        if (overrides == null) return null;

        int cfgSwaps = overrides.Cfg?.Swaps.Count ?? 0;
        int rmpSwaps = overrides.Rmp?.Swaps.Count ?? 0;
        if (cfgSwaps == 0 && rmpSwaps == 0) return null;

        // cfg first, then rmp on top — each call produces a new config that feeds the next layer.
        InputMappingConfig current = baseline;
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

        return current;
    }
}
