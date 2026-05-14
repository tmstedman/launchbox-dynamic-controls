using DynamicControls.Config;

namespace DynamicControls.InputMapping;

/// <summary>
/// The ordered, enabled chain of input-mapping sources and transforms for the current
/// configuration. Encapsulates "which plugins are active and how they're consulted": the
/// constructor filters by each plugin's <see cref="IInputMappingSource.IsEnabled"/> /
/// <see cref="IInputMappingTransform.IsEnabled"/>, and <see cref="SelectSource"/> /
/// <see cref="SelectTransform"/> walk the filtered set in priority order picking the first
/// applicable. <see cref="InputMappingService"/> orchestrates the pipeline against this surface
/// without touching the underlying lists.
/// </summary>
public interface IInputMappingPlugins
{
    /// <summary>
    /// Returns the first source whose <see cref="IInputMappingSource.Load"/> contributes a
    /// non-null config for <paramref name="game"/>, or an empty config if every source returns
    /// null.
    /// </summary>
    InputMappingConfig SelectSource(GameInfo game, PlatformControllersConfig? platform);

    /// <summary>
    /// Returns the first transform whose <see cref="IInputMappingTransform.Transform"/>
    /// contributes a non-null config for <paramref name="game"/> on top of
    /// <paramref name="baseline"/>, or null if every transform returns null.
    /// </summary>
    InputMappingConfig? SelectTransform(GameInfo game, InputMappingConfig baseline);
}

/// <summary>
/// Production implementation. Constructor filters the supplied source/transform lists through
/// each plugin's <c>IsEnabled</c>; the resulting chains are walked in input order on each
/// selection call. Logs the winning plugin's type at debug so operators can trace which source
/// or transform won for a given game.
/// </summary>
public class InputMappingPlugins(
    ILogger logger,
    IReadOnlyList<IInputMappingSource> allSources,
    IReadOnlyList<IInputMappingTransform> allTransforms,
    GlobalConfig config) : IInputMappingPlugins
{
    private readonly ILogger _logger = logger;
    private readonly IReadOnlyList<IInputMappingSource> _sources = [.. allSources.Where(s => s.IsEnabled(config))];
    private readonly IReadOnlyList<IInputMappingTransform> _transforms = [.. allTransforms.Where(t => t.IsEnabled(config))];

    /// <inheritdoc />
    public InputMappingConfig SelectSource(GameInfo game, PlatformControllersConfig? platform)
    {
        foreach (IInputMappingSource source in _sources)
        {
            InputMappingConfig? config = source.Load(game, platform);
            if (config != null)
            {
                _logger.Debug($"Using input mapping source: {source.GetType().Name}, controller: {config.Controller}");
                return config;
            }
        }
        return new InputMappingConfig();
    }

    /// <inheritdoc />
    public InputMappingConfig? SelectTransform(GameInfo game, InputMappingConfig baseline)
    {
        foreach (IInputMappingTransform transform in _transforms)
        {
            InputMappingConfig? config = transform.Transform(game, baseline);
            if (config != null)
            {
                _logger.Debug($"Using input mapping transform: {transform.GetType().Name}");
                return config;
            }
        }
        return null;
    }
}
