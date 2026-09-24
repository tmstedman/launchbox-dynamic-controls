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
    /// Translates platform button labels to generic input names using the input mapping. An
    /// individual label is treated as a combination of one, so both resolve through the same
    /// rule: a generic takes a label's text only when some entry names exactly the buttons that
    /// reach it, no more and no fewer.
    ///
    /// <para>For each generic, the candidates are the buttons some label actually mentions, and
    /// its full driver set is every one of them that reaches it at all, regardless of how far
    /// down its own list the generic sits — reaching it together is what a combination describes,
    /// whichever button's list happens to name it first. If an entry's button-name set matches
    /// that exactly, it's the label.</para>
    ///
    /// <para>Failing that, a button can still reach a generic only weakly, through something
    /// derived rather than its own binding — an <c>analogToDigital</c> mirror appends the stick
    /// onto the Dpad's list well after the Dpad's own targets. So when the full set has no match,
    /// it's retried after dropping every button whose reach here isn't at its strongest (lowest)
    /// rank, in case that was masking a real match underneath. When neither the full set nor the
    /// strongest-only set matches, and more than one button remains, that's a genuine ambiguity —
    /// those buttons fire together but nothing says what that means — so it's logged and the
    /// generic is left unlabelled rather than guessing.</para>
    /// </summary>
    private ResolvedLabels TranslateToGeneric(
        Dictionary<string, string> platformLabels,
        IReadOnlyDictionary<string, IReadOnlyList<string>> inputMapping)
    {
        var labelsByButtonSet = new Dictionary<string, string>();
        foreach (KeyValuePair<string, string> entry in platformLabels)
        {
            labelsByButtonSet[ButtonSetKey(entry.Key)] = entry.Value;
        }

        // Only buttons some label mentions can contend for a generic -- a button nobody labelled
        // is not a candidate driver even if the mapping happens to reach it too.
        var relevantButtons = new HashSet<string>(
            platformLabels.Keys.SelectMany(name => name.Split(' ', StringSplitOptions.RemoveEmptyEntries)));

        // Record every relevant button that reaches each generic, and how directly. A button's
        // own bindings come first in its list and anything derived -- an analogToDigital mirror
        // -- is appended after, so a lower position is a stronger claim.
        var reachersByGeneric = new Dictionary<string, List<(string Button, int Rank)>>();
        foreach (string button in relevantButtons)
        {
            if (!inputMapping.TryGetValue(button, out IReadOnlyList<string>? genericNames))
            {
                _logger.Debug($"Label: {button} has no input mapping");
                continue;
            }

            for (int rank = 0; rank < genericNames.Count; rank++)
            {
                if (!reachersByGeneric.TryGetValue(genericNames[rank], out List<(string, int)>? buttons))
                    reachersByGeneric[genericNames[rank]] = buttons = [];
                buttons.Add((button, rank));
            }
        }

        var labelText = new Dictionary<string, string>();
        foreach ((string generic, List<(string Button, int Rank)> reachers) in reachersByGeneric)
        {
            string[] all = [.. Buttons(reachers)];
            if (labelsByButtonSet.TryGetValue(string.Join(' ', all), out string? text))
            {
                labelText[generic] = text;
                _logger.Debug($"Label: '{string.Join(' ', all)}' -> generic: {generic} -> {text}");
                continue;
            }

            int strongest = reachers.Min(r => r.Rank);
            string[] strongestOnly = [.. Buttons(reachers.Where(r => r.Rank == strongest))];
            if (strongestOnly.Length != all.Length && labelsByButtonSet.TryGetValue(string.Join(' ', strongestOnly), out string? narrowedText))
            {
                labelText[generic] = narrowedText;
                _logger.Debug($"Label: '{string.Join(' ', strongestOnly)}' -> generic: {generic} -> {narrowedText}");
                continue;
            }

            if (strongestOnly.Length > 1)
            {
                // These buttons fire together with nothing to tell them apart, but no label names
                // exactly that combination -- neither button's own text was written to describe
                // it, and nothing here can choose between them. Leave it unlabelled; the log
                // names them so a genuine simultaneous action can be given its own text with
                // <Input name="A B">.
                _logger.Error($"{generic} is driven by {string.Join(" and ", strongestOnly)} at once, with no \"{string.Join(' ', strongestOnly)}\" label to say what that means; leaving it unlabelled.");
            }
        }

        CollapseWholeDirections(labelText);
        return new ResolvedLabels(LabelText: labelText);
    }

    private static IEnumerable<string> Buttons(IEnumerable<(string Button, int Rank)> reachers) =>
        reachers.Select(r => r.Button).Distinct().OrderBy(b => b, StringComparer.Ordinal);

    /// <summary>Normalizes a label's button name(s) into an order-independent lookup key.</summary>
    private static string ButtonSetKey(string name) =>
        string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().OrderBy(b => b, StringComparer.Ordinal));

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

}
