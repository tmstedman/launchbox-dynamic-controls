namespace DynamicControls.Plugins.RetroArch;

public interface IRetroArchOverridesResolver
{
    /// <summary>
    /// Resolves input overrides for a game from one source layer (cfg cascade or remap cascade).
    /// Returns null when the loader finds no applicable file or when neither a variant override
    /// nor any swaps result.
    /// </summary>
    RetroArchInputOverrides? Parse(
        string retroArchDir,
        string coreDisplayName,
        RetroArchCoreConfig coreConfig,
        GameInfo game);
}

/// <summary>
/// Loads game-specific data via <see cref="IRetroArchGameLoader"/>, resolves the controller variant
/// override via <see cref="IRetroArchVariantResolver"/>, and resolves slot swaps via
/// <see cref="IRetroArchSwapResolver"/> (game-level only). Returns null when the loader finds no
/// applicable file or when neither a variant override nor any swaps result. Two instances are
/// constructed with different collaborators: one for the per-core cfg cascade, one for the .rmp
/// remap file.
/// </summary>
public class RetroArchOverridesResolver(
    IRetroArchGameLoader loader,
    IRetroArchVariantResolver variantResolver,
    IRetroArchSwapResolver swapResolver) : IRetroArchOverridesResolver
{
    private readonly IRetroArchGameLoader _loader = loader;
    private readonly IRetroArchVariantResolver _variantResolver = variantResolver;
    private readonly IRetroArchSwapResolver _swapResolver = swapResolver;

    public RetroArchInputOverrides? Parse(
        string retroArchDir,
        string coreDisplayName,
        RetroArchCoreConfig coreConfig,
        GameInfo game)
    {
        RetroArchGameData? data = _loader.Load(retroArchDir, coreDisplayName, game);
        if (data == null) return null;

        RetroArchControllerConfig? variant = _variantResolver.Resolve(data, coreConfig, coreDisplayName);
        Dictionary<string, int> swaps = _swapResolver.ResolveSwaps(data);

        return variant == null && swaps.Count == 0 ? null : new RetroArchInputOverrides(variant, swaps);
    }
}
