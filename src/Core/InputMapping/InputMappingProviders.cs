using DynamicControls.Config;

namespace DynamicControls.InputMapping;

/// <summary>
/// The ordered, enabled chain of input-mapping providers for the current configuration.
/// Encapsulates "which providers are active and how they're consulted": the constructor filters by
/// each provider's <see cref="IInputMappingProvider.IsEnabled"/>, and <see cref="Resolve"/> walks the
/// filtered set in priority order, returning what the first one to answer supplies. The winner is
/// logged and then discarded, so nothing downstream can tell which provider produced a binding.
/// <see cref="InputMappingService"/> orchestrates the pipeline against this surface without
/// touching the underlying list.
/// </summary>
public interface IInputMappingProviders
{
    /// <summary>
    /// Returns what the first provider that answers supplies for <paramref name="game"/>, or an
    /// empty mapping when every provider declines. Always returns something: an empty mapping is
    /// the floor the rest of the pipeline builds on.
    /// </summary>
    ProvidedMapping Resolve(GameInfo game, PlatformControllersConfig? platform);
}

/// <summary>
/// Production implementation. The constructor filters the supplied providers through each one's
/// <c>IsEnabled</c>; the resulting chain is walked in input order on each call. Logs the winning
/// provider's type at debug so operators can trace which one won for a given game — that log line
/// is the only surviving record of provenance.
/// </summary>
public class InputMappingProviders(
    ILogger logger,
    IReadOnlyList<IInputMappingProvider> allProviders,
    GlobalConfig config) : IInputMappingProviders
{
    private readonly ILogger _logger = logger;
    private readonly IReadOnlyList<IInputMappingProvider> _providers = [.. allProviders.Where(p => p.IsEnabled(config))];

    /// <inheritdoc />
    public ProvidedMapping Resolve(GameInfo game, PlatformControllersConfig? platform)
    {
        foreach (IInputMappingProvider provider in _providers)
        {
            ProvidedMapping? provided = provider.Load(game, platform);
            if (provided != null)
            {
                _logger.Debug($"Using input mapping provider: {provider.GetType().Name}, controller: {provided.Current.Controller}");
                return provided;
            }
        }
        return ProvidedMapping.Unchanged(new InputMappingConfig());
    }
}
