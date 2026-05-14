using DynamicControls.Config;

namespace DynamicControls.Labels;

/// <summary>
/// Provides game-specific and platform-default labels from various sources (XML files, MAME
/// Controls.dat, etc). Clone-of fallback is handled by the caller (<c>InputLabelsService</c>),
/// which retries with the parent ROM name when the original returns null.
/// </summary>
public interface IInputLabelsLoader
{
    /// <summary>
    /// Returns true when this loader should participate in the chain for the current
    /// configuration. Always-on loaders (the default XML loader) return true unconditionally;
    /// plugin-gated loaders (MAME) gate on their <c>EnableX</c> flag. Evaluated once at
    /// composition time — disabled loaders are filtered out before the service ever sees them.
    /// </summary>
    bool IsEnabled(GlobalConfig config);

    /// <summary>
    /// Loads game labels for the given <paramref name="game"/>. Returns null when this loader
    /// has no data for the ROM.
    /// </summary>
    InputLabelsConfig? Load(GameInfo game);

    /// <summary>
    /// Loads the platform-default labels file. Returns null when no defaults exist for
    /// <paramref name="platform"/>. All entries are merged into game-specific labels by the service,
    /// with game-specific labels taking precedence for any button defined in both.
    /// </summary>
    InputLabelsConfig? LoadDefaultLabels(string platform);
}
