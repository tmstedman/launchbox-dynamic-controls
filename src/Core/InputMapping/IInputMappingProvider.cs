using DynamicControls.Config;

namespace DynamicControls.InputMapping;

/// <summary>
/// What a provider hands back for one game: the controller as the platform declares it
/// (<see cref="Natural"/>) and the mapping the player actually has (<see cref="Current"/>). The two
/// differ when the provider knows of a remap — an emulator's button swaps, or a per-game
/// <c>&lt;Mapping&gt;</c> — and the difference is what <c>InputImageResolver</c> reads to tell a
/// button in its usual place from one that has moved.
/// </summary>
public record ProvidedMapping(InputMappingConfig Natural, InputMappingConfig Current)
{
    /// <summary>A mapping with nothing remapped: the natural state is the current one.</summary>
    public static ProvidedMapping Unchanged(InputMappingConfig mapping) => new(mapping, mapping);
}

/// <summary>
/// A standalone source of one game's input mapping: the controller in play and any remap of it.
/// Providers are tried in priority order and the first to return a mapping supplies all of it —
/// the controller choice and the remap alike — so a provider that applies is the whole answer and
/// no other provider is consulted. A per-game XML override therefore replaces an emulator's
/// swaps and controller choice rather than stacking on them: the XML stands in for what the
/// emulator providers do for an emulator the plugin cannot read.
///
/// <para>Controls (<c>&lt;Control&gt;</c> entries) are not a provider's job: the cfg has to have
/// moved a control's members before it is read, so <c>InputMappingService</c> applies them last.</para>
/// </summary>
public interface IInputMappingProvider
{
    /// <summary>
    /// Returns true when this provider should participate in the chain for the current
    /// configuration. Always-on providers (per-game XML, platform default) return true
    /// unconditionally; emulator providers gate on their <c>EnableX</c> flag. Evaluated once at
    /// composition time — disabled providers are filtered out before the service ever sees them.
    /// </summary>
    bool IsEnabled(GlobalConfig config);

    /// <summary>
    /// Returns the mapping for <paramref name="game"/>, or null when this provider has nothing for
    /// it and the next should be tried. <paramref name="platform"/> is the platform's
    /// Controllers.xml, or null when it declares none. Both configs carry the same
    /// <see cref="InputMappingConfig.Controller"/> (null only when the platform declares no
    /// controller to name), and the same <see cref="InputMappingConfig.AnalogToDigital"/> — a
    /// statement about the hardware, so a stick mirror is never read as a remap.
    /// </summary>
    ProvidedMapping? Load(GameInfo game, PlatformControllersConfig? platform);
}
