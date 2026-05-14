using DynamicControls.Config;

namespace DynamicControls.InputMapping;

/// <summary>
/// A standalone source of per-game input mapping data. Implementations look for a per-game
/// configuration (XML file, RetroArch core file, ...) and produce a complete InputMappingConfig
/// — including the resolved controller name on <see cref="InputMappingConfig.Controller"/> —
/// that replaces the platform default when present.
///
/// Sources are tried in priority order; the first one to return a non-null result wins, and
/// lower-priority sources are not consulted. If every source returns null, the platform default
/// controller is used directly. Transformation-style modifiers (MAME cfg button swaps) are
/// represented separately by <see cref="IInputMappingTransform"/> and applied on top of whichever
/// source (or platform default) is selected.
/// </summary>
public interface IInputMappingSource
{
    /// <summary>
    /// Returns true when this source should participate in the chain for the current
    /// configuration. Always-on sources (per-game XML, platform default) return true
    /// unconditionally; plugin-gated sources (RetroArch) gate on their <c>EnableX</c> flag.
    /// Evaluated once at composition time — sources for which this returns false are filtered out
    /// before the service ever sees them.
    /// </summary>
    bool IsEnabled(GlobalConfig config);

    /// <summary>
    /// Returns an InputMappingConfig for the given game, or null if this source has no data for
    /// it. Implementations are responsible for resolving their own controller from
    /// <paramref name="platform"/> (the platform's Controllers.xml, or null when the platform
    /// declares no controllers) and must set <see cref="InputMappingConfig.Controller"/> on the
    /// returned config.
    /// </summary>
    InputMappingConfig? Load(GameInfo game, PlatformControllersConfig? platform);
}
