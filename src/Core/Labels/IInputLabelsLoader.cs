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
    /// Loads game labels for the given <paramref name="game"/>. Lookup order: database ID
    /// (<see cref="GameInfo.LaunchBoxId"/>), then ROM name (<see cref="GameInfo.RomName"/>).
    /// Returns null when this loader has no entry for the game.
    /// </summary>
    InputLabelsConfig? Load(GameInfo game);

    /// <summary>
    /// Loads the platform-default labels. For the file-based loader this comes from the
    /// <c>&lt;Defaults&gt;</c> block of <c>Labels/{platform}.xml</c>; plugin loaders (e.g. MAME
    /// controls.xml) return null as they have no concept of platform defaults. All entries are
    /// merged into game-specific labels by the service, with game-specific labels taking
    /// precedence for any button defined in both.
    /// </summary>
    InputLabelsConfig? LoadDefaultLabels(string platform);
}
