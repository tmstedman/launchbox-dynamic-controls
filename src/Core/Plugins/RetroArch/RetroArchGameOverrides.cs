using DynamicControls.InputMapping;

namespace DynamicControls.Plugins.RetroArch;

/// <summary>
/// What RetroArch's own configuration says about one game: the overrides read from the cfg
/// cascade and from the remap file, either of which may be absent.
/// </summary>
/// <param name="Cfg">Overrides from the cfg cascade, or null when nothing in it applies.</param>
/// <param name="Rmp">Overrides from the .rmp remap file, or null when there isn't one.</param>
public record RetroArchGameOverrides(
    RetroArchInputOverrides? Cfg,
    RetroArchInputOverrides? Rmp);

/// <summary>
/// Reads RetroArch's configuration for a game and hands back what it found.
///
/// <para>Two plugins need this same answer — <see cref="RetroArchMappingSource"/> to learn which
/// controller the player selected, and <see cref="RetroArchSwapTransform"/> to learn which
/// buttons they swapped — so the reading lives here rather than in either of them.</para>
/// </summary>
public interface IRetroArchGameOverridesResolver
{
    /// <summary>
    /// Returns what RetroArch's configuration says about <paramref name="game"/>, or null when it
    /// says nothing: the game isn't running under RetroArch, the core has no definition file
    /// shipped with the plugin, or neither the cfg cascade nor a remap file contributes anything.
    /// </summary>
    RetroArchGameOverrides? Resolve(GameInfo game);
}

/// <summary>
/// Production implementation. Resolves the core's display name from its .info file (the directory
/// name RetroArch itself uses under <c>config/</c>, e.g. "Genesis Plus GX" rather than
/// "genesis_plus_gx_libretro"), loads the plugin's per-core definition, then parses the cfg and
/// remap cascades through the two resolvers.
///
/// <para>This is read afresh per call. Both plugins that use it run once per game launch, so the
/// files are read twice where they were previously read once — see the note on
/// <see cref="RetroArchSwapTransform"/>.</para>
/// </summary>
public class RetroArchGameOverridesResolver(
    ILogger logger,
    IRetroArchCoreInfo coreInfo,
    IRetroArchCoreLoader coreLoader,
    IRetroArchOverridesResolver cfgResolver,
    IRetroArchOverridesResolver remapResolver) : IRetroArchGameOverridesResolver
{
    private readonly ILogger _logger = logger;
    private readonly IRetroArchCoreInfo _coreInfo = coreInfo;
    private readonly IRetroArchCoreLoader _coreLoader = coreLoader;
    private readonly IRetroArchOverridesResolver _cfgResolver = cfgResolver;
    private readonly IRetroArchOverridesResolver _remapResolver = remapResolver;

    /// <inheritdoc />
    public RetroArchGameOverrides? Resolve(GameInfo game)
    {
        if (game.EmulatorPath == null || game.RomName == null || game.RetroArchCore == null) return null;
        if (!RetroArchEmulator.IsRetroArchExecutable(game.EmulatorPath)) return null;

        string retroArchDir = Path.GetDirectoryName(game.EmulatorPath)!;

        // The .info file carries the display name RetroArch uses for its config subdirectories.
        // Fall back to the DLL name when the file is missing.
        string coreDisplayName = _coreInfo.ReadDisplayName(retroArchDir, game.RetroArchCore) ?? game.RetroArchCore;

        // The plugin's own per-core file declares which controller variants the core offers and
        // which RetroArch device-type IDs select each one. Without it there is nothing to match against.
        RetroArchCoreConfig? coreConfig = _coreLoader.Load(coreDisplayName);
        if (coreConfig == null) return null;

        RetroArchInputOverrides? cfg = _cfgResolver.Parse(retroArchDir, coreDisplayName, coreConfig, game);
        RetroArchInputOverrides? rmp = _remapResolver.Parse(retroArchDir, coreDisplayName, coreConfig, game);
        if (cfg == null && rmp == null) return null;

        _logger.Debug(
            $"RetroArch overrides for core '{coreDisplayName}': " +
            $"cfg {(cfg == null ? "none" : $"{cfg.Swaps.Count} swaps, variant '{cfg.Variant?.Name ?? "none"}'")}; " +
            $"remap {(rmp == null ? "none" : $"{rmp.Swaps.Count} swaps, variant '{rmp.Variant?.Name ?? "none"}'")}");

        return new RetroArchGameOverrides(cfg, rmp);
    }
}
