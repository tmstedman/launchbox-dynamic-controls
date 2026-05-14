using DynamicControls.Config;

namespace DynamicControls.Labels;

/// <summary>
/// Holds the input-label loaders that participate in the current configuration. The default
/// loader is structurally mandatory (used both as the first loader in the chain and as the sole
/// source of inheritable defaults); additional loaders are filtered by their
/// <see cref="IInputLabelsLoader.IsEnabled"/>. <see cref="InputLabelsService"/> reads only the
/// filtered chain plus the default loader, so it never needs to know about plugin gating or
/// <see cref="GlobalConfig"/>.
///
/// Unlike <see cref="InputMapping.IInputMappingPlugins"/> (which owns
/// <c>Select*</c> iteration methods), this class is a passive holder — the service's loader loop
/// is interleaved with per-loop default-labels fetching, mapping-driven translation, and a
/// fall-through-on-empty-translation rule, so pulling iteration out would either lose behaviour
/// or couple this class to <c>ResolvedMapping</c>/translation concerns that don't belong here.
/// </summary>
public class InputLabelsPlugins(
    IInputLabelsLoader defaultLoader,
    IReadOnlyList<IInputLabelsLoader> additionalLoaders,
    GlobalConfig config)
{
    public IInputLabelsLoader DefaultLoader { get; } = defaultLoader;

    public IReadOnlyList<IInputLabelsLoader> Loaders { get; } =
        [defaultLoader, .. additionalLoaders.Where(l => l.IsEnabled(config))];
}
