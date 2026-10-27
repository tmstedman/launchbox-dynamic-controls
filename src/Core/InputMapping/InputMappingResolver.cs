namespace DynamicControls.InputMapping;

/// <summary>
/// Converts a raw <see cref="InputMappingConfig"/> into a <see cref="ResolvedMapping"/>: builds
/// the platform-button → generic-input dictionary, applies AnalogToDigital mirroring, builds the
/// reverse lookup, and snapshots the natural state for the renderer's remap detection. Pure
/// transformation — no file I/O, no caching. Callers that need a transformed-vs-baseline
/// distinction (e.g. MAME cfg overlays) resolve both configs separately and combine via
/// <c>with</c>.
/// </summary>
public interface IInputMappingResolver
{
    /// <summary>
    /// Materializes a <see cref="ResolvedMapping"/> from <paramref name="xml"/>: copies entries,
    /// applies AnalogToDigital mirroring, builds the reverse lookup, and uses the result as the
    /// natural snapshot.
    /// </summary>
    /// <param name="platform">Platform name carried through to the returned mapping.</param>
    /// <param name="xml">Raw mapping config to materialize.</param>
    /// <param name="controller">Resolved controller name carried through to the returned mapping.</param>
    ResolvedMapping Resolve(string platform, InputMappingConfig xml, string? controller);
}

/// <summary>
/// Production implementation: dedup is by case-sensitive string comparison, reverse lookup is
/// first-seen-wins so mirror-added entries don't displace natural ones, and a single ILogger is
/// used for diagnostic counts.
/// </summary>
public class InputMappingResolver(ILogger logger) : IInputMappingResolver
{
    private readonly ILogger _logger = logger;

    /// <inheritdoc />
    public ResolvedMapping Resolve(string platform, InputMappingConfig xml, string? controller)
    {
        var buttonToInput = new Dictionary<string, List<string>>();
        var derived = new Dictionary<string, List<string>>();
        var bound = new HashSet<(string Name, string Input)>();
        foreach (MappingEntry entry in xml.Mappings)
        {
            if (!buttonToInput.TryGetValue(entry.Name, out List<string>? list))
            {
                list = [];
                buttonToInput[entry.Name] = list;
            }
            if (!list.Contains(entry.Input))
                list.Add(entry.Input);

            // A button's own binding of an input is the stronger claim, wherever it came
            // in the file, so one entry binding it outright overrides any deriving it.
            if (!entry.Derived) bound.Add((entry.Name, entry.Input));
        }
        foreach (MappingEntry entry in xml.Mappings.Where(e => e.Derived && !bound.Contains((e.Name, e.Input))))
            AddDerived(derived, entry.Name, entry.Input);
        _logger.Debug($"Mapping entries: {buttonToInput.Count}");

        if (xml.AnalogToDigital is AnalogToDigitalMode mode)
        {
            var ownCounts = buttonToInput.ToDictionary(e => e.Key, e => e.Value.Count);
            AnalogToDigitalMirror.Mirror(buttonToInput, mode);
            foreach ((string button, List<string> inputs) in buttonToInput)
                foreach (string mirrored in inputs.Skip(ownCounts[button]))
                    AddDerived(derived, button, mirrored);
            _logger.Debug($"Applied AnalogToDigital mirror (Dpad -> {mode.ToString().ToLowerInvariant()} stick)");
        }

        Dictionary<string, string> inputToButton = BuildReverse(buttonToInput);
        var readOnlyButtonToInput = buttonToInput.ToDictionary(
            e => e.Key,
            e => (IReadOnlyList<string>)e.Value);

        _logger.Debug($"Input mapping count: {buttonToInput.Count}, AnalogToDigital: {xml.AnalogToDigital}");

        return new ResolvedMapping(
            Platform: platform,
            Controller: controller,
            ButtonToInput: readOnlyButtonToInput,
            InputToButton: inputToButton,
            NaturalButtonToInput: readOnlyButtonToInput,
            NaturalInputToButton: inputToButton,
            AnalogToDigital: xml.AnalogToDigital)
        {
            DerivedButtonToInput = derived.ToDictionary(e => e.Key, e => (IReadOnlyList<string>)e.Value),
        };
    }

    private static void AddDerived(Dictionary<string, List<string>> derived, string button, string input)
    {
        if (!derived.TryGetValue(button, out List<string>? inputs)) derived[button] = inputs = [];
        if (!inputs.Contains(input)) inputs.Add(input);
    }

    /// <summary>
    /// Builds the reverse lookup (generic input → primary platform button) from
    /// <paramref name="buttonToInput"/>. First-seen-wins: subsequent platform buttons claiming
    /// the same generic (e.g. from AnalogToDigital mirroring) are recorded in ButtonToInput but
    /// not exposed as the primary platform button for that generic.
    /// </summary>
    private static Dictionary<string, string> BuildReverse(Dictionary<string, List<string>> buttonToInput)
    {
        var reverse = new Dictionary<string, string>();
        foreach (KeyValuePair<string, List<string>> entry in buttonToInput)
        {
            foreach (string generic in entry.Value)
            {
                if (!reverse.ContainsKey(generic))
                    reverse[generic] = entry.Key;
            }
        }
        return reverse;
    }
}
