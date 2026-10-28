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
            IReadOnlyList<InputLabelsConfig> defaultLayers = _defaultLabelsLoader.LoadDefaultLabels(game.Platform);
            Dictionary<string, string> merged = MergeLayers(
                [(data, $"{game.RomName}'s labels"), .. DefaultLayers(defaultLayers, game.Platform)],
                mapping.ButtonAliases);
            ResolvedLabels resolved = TranslateToGeneric(merged, mapping) with { IsGameSpecific = true };
            if (resolved.LabelText.Count == 0) continue;
            return resolved;
        }

        return null;
    }

    /// <summary>
    /// Loads default labels and translates to generic input names.
    /// Returns null if the platform has no default labels.
    /// </summary>
    private ResolvedLabels? LoadDefaultLabels(string platform, ResolvedMapping mapping)
    {
        IReadOnlyList<InputLabelsConfig> defaultLayers = _defaultLabelsLoader.LoadDefaultLabels(platform);
        if (defaultLayers.Count == 0) return null;

        return TranslateToGeneric(MergeLayers(DefaultLayers(defaultLayers, platform), mapping.ButtonAliases), mapping);
    }

    private static IEnumerable<(InputLabelsConfig Labels, string Source)> DefaultLayers(
        IReadOnlyList<InputLabelsConfig> layers, string platform) =>
        layers.Select(layer => (layer, $"{platform} <Defaults>"));

    /// <summary>
    /// Merges layers of labels, the one that wins first: the game's own, then each block of
    /// defaults. Each layer is keyed by <see cref="ByButtonSet"/> on its own, and a later layer
    /// adds only the sets of buttons no earlier one named — compared once each side names its
    /// buttons one way, so a game or User entry naming a port by another name of it still
    /// overrides the default for that port. Overriding is what layers are for, so only a clash
    /// within one layer is reported.
    /// </summary>
    private Dictionary<string, string> MergeLayers(
        IEnumerable<(InputLabelsConfig Labels, string Source)> layers, IReadOnlyDictionary<string, string> aliases)
    {
        var merged = new Dictionary<string, string>();
        bool first = true;
        foreach ((InputLabelsConfig labels, string source) in layers)
        {
            foreach ((string buttons, string text) in ByButtonSet(labels.Labels, aliases, source))
            {
                if (merged.TryAdd(buttons, text) && !first) _logger.Debug($"Inherited from {source}: {buttons} -> {text}");
            }
            first = false;
        }
        return merged;
    }

    /// <summary>
    /// Translates platform button labels, keyed by <see cref="ByButtonSet"/>, to generic input
    /// names using the input mapping. An individual label is treated as a combination of one, so
    /// both resolve through the same rule: a generic takes a label's text only when some entry
    /// names exactly the buttons that reach it, no more and no fewer.
    ///
    /// <para>For each generic, the candidates are the buttons some label actually mentions, and
    /// its full driver set is every one of them that reaches it at all, regardless of how far
    /// down its own list the generic sits — reaching it together is what a combination describes,
    /// whichever button's list happens to name it first. If an entry's button-name set matches
    /// that exactly, it's the label.</para>
    ///
    /// <para>Failing that, a button can still reach a generic only weakly, through something
    /// derived rather than its own binding — an <c>analogToDigital</c> mirror, a control reaching
    /// what its members reach, a nudge reaching its half of its port's axis (see
    /// <see cref="ResolvedMapping.DerivedButtonToInput"/>). So when the full set has no match,
    /// it's retried after dropping every button whose claim here isn't the strongest, in case
    /// that was masking a real match underneath. An own binding outranks a derived one; between
    /// two of the same kind, the one earlier in its button's list is stronger.</para>
    ///
    /// <para>When neither set matches and more than one button remains, the contenders are asked
    /// whether they disagree at all: where each carries its own text and all of it is the same,
    /// that text is taken, because there is nothing to choose between. Only a real disagreement
    /// — those buttons fire together and say different things about it — is logged and left
    /// unlabelled rather than guessed at.</para>
    /// </summary>
    private ResolvedLabels TranslateToGeneric(
        Dictionary<string, string> labelsByButtonSet,
        ResolvedMapping mapping)
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> inputMapping = mapping.ButtonToInput;

        // Only buttons some label mentions can contend for a generic -- a button nobody labelled
        // is not a candidate driver even if the mapping happens to reach it too.
        var relevantButtons = new HashSet<string>(
            labelsByButtonSet.Keys.SelectMany(name => name.Split(' ', StringSplitOptions.RemoveEmptyEntries)));

        // Record every relevant button that reaches each generic, and how directly: whether the
        // button binds it or only derives it, then where it sits in the button's list. Position
        // alone can't stand in for the first -- a button binding both halves of an axis has to
        // list one of them second, which would make it look as weak as a derived claim.
        var reachersByGeneric = new Dictionary<string, List<(string Button, (int Derived, int Position) Rank)>>();
        foreach (string button in relevantButtons)
        {
            if (!inputMapping.TryGetValue(button, out IReadOnlyList<string>? genericNames))
            {
                _logger.Debug($"Label: {button} has no input mapping");
                continue;
            }

            IReadOnlyList<string> derived = mapping.DerivedButtonToInput.GetValueOrDefault(button, []);
            for (int position = 0; position < genericNames.Count; position++)
            {
                string generic = genericNames[position];
                if (!reachersByGeneric.TryGetValue(generic, out List<(string, (int, int))>? buttons))
                    reachersByGeneric[generic] = buttons = [];
                buttons.Add((button, (derived.Contains(generic) ? 1 : 0, position)));
            }
        }

        var labelText = new Dictionary<string, string>();
        foreach ((string generic, List<(string Button, (int Derived, int Position) Rank)> reachers) in reachersByGeneric)
        {
            string[] all = [.. Buttons(reachers)];
            if (labelsByButtonSet.TryGetValue(string.Join(' ', all), out string? text))
            {
                labelText[generic] = text;
                _logger.Debug($"Label: '{string.Join(' ', all)}' -> generic: {generic} -> {text}");
                continue;
            }

            (int, int) strongest = reachers.Min(r => r.Rank);
            string[] strongestOnly = [.. Buttons(reachers.Where(r => r.Rank == strongest))];
            if (strongestOnly.Length != all.Length && labelsByButtonSet.TryGetValue(string.Join(' ', strongestOnly), out string? narrowedText))
            {
                labelText[generic] = narrowedText;
                _logger.Debug($"Label: '{string.Join(' ', strongestOnly)}' -> generic: {generic} -> {narrowedText}");
                continue;
            }

            if (strongestOnly.Length > 1)
            {
                // Before calling it ambiguous, see whether there is anything to be ambiguous
                // about. Two ports can describe one physical control -- MAME binds a driver's
                // lightgun and trackball on the same axis to the same joycode, since either
                // scheme might be the one the game reads -- and the labels then say the same
                // words about each. Choosing between them is a distinction without a difference,
                // so take what they agree on. Every contender must carry its own text: a button
                // known only from inside a combination entry has said nothing on its own account
                // to agree with.

                string?[] ownTexts = [.. strongestOnly.Select(b =>
                    labelsByButtonSet.TryGetValue(ButtonSetKey(b), out string? t) ? t : null)];
                if (ownTexts.All(t => t != null) && ownTexts.Distinct().Count() == 1)
                {
                    labelText[generic] = ownTexts[0]!;
                    _logger.Debug($"Label: '{string.Join(' ', strongestOnly)}' agree -> generic: {generic} -> {ownTexts[0]}");
                    continue;
                }

                // They genuinely disagree: these buttons fire together with nothing to tell them
                // apart, and no label names exactly that combination -- neither button's own text
                // was written to describe it, and nothing here can choose between them. Leave it
                // unlabelled; the log names them so a genuine simultaneous action can be given
                // its own text with <Input name="A B">.
                _logger.Error($"{generic} is driven by {string.Join(" and ", strongestOnly)} at once, with no \"{string.Join(' ', strongestOnly)}\" label to say what that means; leaving it unlabelled.");
            }
        }

        return new ResolvedLabels(LabelText: labelText);
    }

    /// <summary>
    /// Keys each label by the set of buttons it names (<see cref="ButtonSetKey"/>), with every
    /// button named by another name of it rewritten to the button it means, inside a combination
    /// too, so the rest of the matching sees one name per button. Two entries that then name the
    /// same buttons — exactly, in another order, or one by each of its names — are the same label
    /// written twice: identical text is no conflict, and different text keeps the entry written
    /// first and logs, naming <paramref name="source"/>.
    /// </summary>
    private Dictionary<string, string> ByButtonSet(
        IEnumerable<LabelEntry> entries, IReadOnlyDictionary<string, string> aliases, string source)
    {
        var resolved = new Dictionary<string, string>();
        var writtenAs = new Dictionary<string, string>();
        foreach ((string name, string text) in entries.Select(e => (e.Name, e.Label)))
        {
            string buttons = ButtonSetKey(string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(button => aliases.GetValueOrDefault(button, button))));
            if (resolved.TryGetValue(buttons, out string? earlier))
            {
                if (earlier != text)
                {
                    _logger.Error($"{source}: label \"{name}\" names the same buttons as \"{writtenAs[buttons]}\" but says \"{text}\" rather than \"{earlier}\"; keeping \"{earlier}\".");
                }
                continue;
            }
            resolved[buttons] = text;
            writtenAs[buttons] = name;
        }
        return resolved;
    }

    private static IEnumerable<string> Buttons(IEnumerable<(string Button, (int Derived, int Position) Rank)> reachers) =>
        reachers.Select(r => r.Button).Distinct().OrderBy(b => b, StringComparer.Ordinal);

    /// <summary>Normalizes a label's button name(s) into an order-independent lookup key.</summary>
    private static string ButtonSetKey(string name) =>
        string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().OrderBy(b => b, StringComparer.Ordinal));


}
