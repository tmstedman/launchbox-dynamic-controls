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
    ///
    /// <para>A label whose name is space-separated describes an action performed by pressing
    /// those buttons together. It resolves to the inputs every named button drives — the
    /// intersection — and outranks the individual labels of the buttons involved, because a
    /// control that fires several buttons at once is doing the combined action rather than any
    /// one of them.</para>
    ///
    /// <para>Where buttons contend for an input and no combination covers it, the input is left
    /// unlabelled. Pressing it does more than one thing, so no single label is true, and showing
    /// an arbitrary one would be worse than showing none.</para>
    /// </summary>
    private ResolvedLabels TranslateToGeneric(
        Dictionary<string, string> platformLabels,
        IReadOnlyDictionary<string, IReadOnlyList<string>> inputMapping)
    {
        var labelText = new Dictionary<string, string>();
        var claimedByCombination = new HashSet<string>();

        // Combinations first, so their claim is already recorded when individual labels are placed.
        foreach (KeyValuePair<string, string> entry in platformLabels.Where(e => IsCombination(e.Key)))
        {
            IReadOnlyCollection<string> shared = SharedInputs(entry.Key, inputMapping);
            if (shared.Count == 0)
            {
                _logger.Debug($"Label: '{entry.Key}' shares no input in this mapping, nothing to label");
                continue;
            }

            foreach (string input in shared)
            {
                labelText[input] = entry.Value;
                claimedByCombination.Add(input);
                _logger.Debug($"Label: '{entry.Key}' -> generic: {input} -> {entry.Value}");
            }
        }

        // Record which buttons claim each input, and how directly. A button's own bindings come
        // first in its list and anything derived — an analogToDigital mirror — is appended after,
        // so a lower position means a stronger claim on that input.
        var claims = new Dictionary<string, List<(string Button, int Rank)>>();
        foreach (KeyValuePair<string, string> entry in platformLabels.Where(e => !IsCombination(e.Key)))
        {
            if (!inputMapping.TryGetValue(entry.Key, out IReadOnlyList<string>? genericNames))
            {
                _logger.Debug($"Label: {entry.Key} has no input mapping");
                continue;
            }

            for (int rank = 0; rank < genericNames.Count; rank++)
            {
                if (!claims.TryGetValue(genericNames[rank], out List<(string, int)>? buttons))
                    claims[genericNames[rank]] = buttons = [];
                buttons.Add((entry.Key, rank));
            }
        }

        foreach ((string input, List<(string Button, int Rank)> buttons) in claims)
        {
            if (claimedByCombination.Contains(input))
            {
                _logger.Debug($"Label: {string.Join(", ", buttons.Select(b => b.Button))} -> {input} superseded by a combination label");
                continue;
            }

            int strongest = buttons.Min(b => b.Rank);
            List<string> contenders = [.. buttons.Where(b => b.Rank == strongest).Select(b => b.Button)];

            if (contenders.Count > 1)
            {
                // Equally direct claims, so nothing here can choose between them. The last still
                // wins as it always has; the log says so because the result is wrong for whichever
                // button lost, and a genuine simultaneous action should say so with
                // <Input name="A B"> instead.
                _logger.Error($"{input} is driven by {string.Join(" and ", contenders)} at once; showing '{platformLabels[contenders[^1]]}'. If these fire together, label them with <Input name=\"{string.Join(" ", contenders)}\">.");
            }

            labelText[input] = platformLabels[contenders[^1]];
            _logger.Debug($"Label: {contenders[^1]} -> generic: {input} -> {labelText[input]}");
        }

        CollapseWholeDirections(labelText);
        return new ResolvedLabels(LabelText: labelText);
    }

    /// <summary>
    /// Final pass: where all four direction inputs of a whole control (<see cref="WholeInputs.PartsOf"/>)
    /// currently carry the same text, fold them onto the whole and drop the four -- that's what
    /// makes the layout's OneOf pick its single-render alternative instead of four redundant
    /// per-direction ones. Left alone whenever they disagree (or aren't all present), so a genuine
    /// per-direction remap -- one direction swapped onto an ordinary button, say -- renders each
    /// direction with its own, correct text instead of being smoothed over by whichever whole-level
    /// claim <see cref="WholeInputDeriver"/> may separately have added.
    /// </summary>
    private void CollapseWholeDirections(Dictionary<string, string> labelText)
    {
        foreach ((string whole, IReadOnlyList<string> parts) in WholeInputs.PartsOf)
        {
            if (!parts.All(labelText.ContainsKey)) continue;

            List<string> distinct = [.. parts.Select(p => labelText[p]).Distinct()];
            if (distinct.Count != 1) continue;

            foreach (string part in parts) labelText.Remove(part);
            labelText[whole] = distinct[0];
            _logger.Debug($"Label: {string.Join(", ", parts)} agree on '{distinct[0]}' -- collapsed onto {whole}");
        }
    }

    /// <summary>True when the entry names several buttons pressed together.</summary>
    private static bool IsCombination(string name) => name.Contains(' ');

    /// <summary>
    /// The generic inputs driven by every button in a combination. Empty when the buttons share no
    /// input, or when any of them is absent from the mapping — in both cases the player's
    /// configuration has no single control performing the combined action.
    /// </summary>
    private static IReadOnlyCollection<string> SharedInputs(
        string combinationName,
        IReadOnlyDictionary<string, IReadOnlyList<string>> inputMapping)
    {
        string[] buttons = [.. combinationName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct()];

        HashSet<string>? shared = null;
        foreach (string button in buttons)
        {
            if (!inputMapping.TryGetValue(button, out IReadOnlyList<string>? inputs))
                return [];

            if (shared == null) shared = [.. inputs];
            else shared.IntersectWith(inputs);

            if (shared.Count == 0) return [];
        }
        return shared ?? (IReadOnlyCollection<string>)[];
    }
}
