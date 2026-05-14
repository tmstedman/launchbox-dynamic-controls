using DynamicControls.InputMapping;

namespace DynamicControls.Labels;

/// <summary>
/// Resolves display labels for a game's controller inputs. Tries registered loaders in
/// registration order for game-specific labels (with clone-of fallback), then merges in the
/// platform's inheritable defaults, then translates platform button names to generic input
/// names via the supplied mapping. Returns an empty result with <c>IsGameSpecific=false</c>
/// when no labels are found.
/// </summary>
public interface IInputLabelsService
{
    /// <summary>
    /// Resolves labels for <paramref name="game"/>: tries game-specific labels first, falls
    /// back to platform defaults, returns an empty set if neither exists.
    /// </summary>
    ResolvedLabels Load(GameInfo game, ResolvedMapping mapping);
}

/// <summary>
/// Production implementation: holds the default-labels loader separately (so it can be
/// consulted for inheritable defaults regardless of the game-loader chain) and merges results
/// via private helpers that route inheritance, clone-of retry, and platform-to-generic
/// translation. The <see cref="InputLabelsPlugins"/> dependency carries the already-filtered
/// chain in priority order plus the mandatory default loader — the service never sees disabled
/// loaders or <see cref="Config.GlobalConfig"/>.
/// </summary>
public class InputLabelsService(ILogger logger, InputLabelsPlugins plugins) : IInputLabelsService
{
    private readonly ILogger _logger = logger;
    private readonly IInputLabelsLoader _defaultLabelsLoader = plugins.DefaultLoader;
    private readonly IReadOnlyList<IInputLabelsLoader> _loaders = plugins.Loaders;

    /// <inheritdoc />
    public ResolvedLabels Load(GameInfo game, ResolvedMapping mapping)
    {
        ResolvedLabels? labels = LoadGameLabels(game, mapping);
        if (labels != null)
        {
            _logger.Debug($"Game label count: {labels.LabelText.Count}");
            return labels;
        }

        labels = LoadDefaultLabels(game.Platform, mapping);
        if (labels != null)
        {
            _logger.Debug($"Using default labels, count: {labels.LabelText.Count}");
            return labels;
        }

        _logger.Debug("No labels found, showing template only");
        return new ResolvedLabels(LabelText: new Dictionary<string, string>());
    }

    /// <summary>
    /// Loads game labels from the first loader that returns data. Each loader is tried for the
    /// game's ROM and, if that misses, for its clone parent — clones inherit their parent's
    /// labels. A loader that returns a labels file with zero entries is treated as if it had
    /// returned null — an empty game labels XML doesn't count as "the game has its own labels,"
    /// so we fall through to the next loader (and ultimately to LoadDefaultLabels at the
    /// caller). Returns null if no loader has labels for this game.
    /// </summary>
    private ResolvedLabels? LoadGameLabels(GameInfo game, ResolvedMapping mapping)
    {
        foreach (IInputLabelsLoader loader in _loaders)
        {
            InputLabelsConfig? data = loader.Load(game);
            if (data == null && !string.IsNullOrEmpty(game.CloneOf))
                data = loader.Load(game with { RomName = game.CloneOf });
            if (data == null || data.Labels.Count == 0) continue;

            _logger.Debug($"Game labels from {loader.GetType().Name}: {data.Labels.Count}");
            InputLabelsConfig? defaultData = _defaultLabelsLoader.LoadDefaultLabels(game.Platform);
            Dictionary<string, string> merged = MergeWithDefaults(data, defaultData);
            ResolvedLabels resolved = TranslateToGeneric(merged, mapping.ButtonToInput) with { IsGameSpecific = true };
            if (resolved.LabelText.Count == 0) continue;
            return resolved;
        }

        return null;
    }

    /// <summary>
    /// Loads default labels and translates to generic input names.
    /// Returns null if no default labels file exists.
    /// </summary>
    private ResolvedLabels? LoadDefaultLabels(string platform, ResolvedMapping mapping)
    {
        InputLabelsConfig? defaultData = _defaultLabelsLoader.LoadDefaultLabels(platform);
        if (defaultData == null) return null;

        _logger.Debug($"Default labels count: {defaultData.Labels.Count}");
        return TranslateToGeneric(ToLabelDict(defaultData), mapping.ButtonToInput);
    }

    /// <summary>
    /// Merges game labels with default labels. Default entries are added for any platform button
    /// not already present in the game labels.
    /// </summary>
    private Dictionary<string, string> MergeWithDefaults(InputLabelsConfig gameData, InputLabelsConfig? defaultData)
    {
        Dictionary<string, string> labels = ToLabelDict(gameData);
        if (defaultData == null) return labels;

        foreach (LabelEntry entry in defaultData.Labels.Where(e => !labels.ContainsKey(e.Name)))
        {
            labels[entry.Name] = entry.Label;
            _logger.Debug($"Inherited default: {entry.Name} -> {entry.Label}");
        }
        _logger.Debug($"Default labels count: {defaultData.Labels.Count}");

        return labels;
    }

    /// <summary>
    /// Converts an InputLabelsConfig into a platform button name to label text dictionary.
    /// </summary>
    private static Dictionary<string, string> ToLabelDict(InputLabelsConfig data)
    {
        var dict = new Dictionary<string, string>();
        foreach (LabelEntry e in data.Labels)
        {
            dict[e.Name] = e.Label;
        }
        return dict;
    }

    /// <summary>
    /// Translates platform button labels to generic input names using the input mapping.
    /// Entries with no mapping are logged and discarded.
    /// </summary>
    private ResolvedLabels TranslateToGeneric(
        Dictionary<string, string> platformLabels,
        IReadOnlyDictionary<string, IReadOnlyList<string>> inputMapping)
    {
        var labelText = new Dictionary<string, string>();
        foreach (KeyValuePair<string, string> entry in platformLabels)
        {
            if (!inputMapping.TryGetValue(entry.Key, out IReadOnlyList<string>? genericNames))
            {
                _logger.Debug($"Label: {entry.Key} has no input mapping");
                continue;
            }

            foreach (string genericName in genericNames)
            {
                labelText[genericName] = entry.Value;
                _logger.Debug($"Label: {entry.Key} -> generic: {genericName} -> {entry.Value}");
            }
        }
        return new ResolvedLabels(LabelText: labelText);
    }
}
