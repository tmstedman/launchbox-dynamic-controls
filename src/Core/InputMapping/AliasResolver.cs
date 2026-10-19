namespace DynamicControls.InputMapping;

/// <summary>
/// Resolves <see cref="AliasEntry"/> declarations against a mapping: a platform button standing
/// for several others reaches whatever those others currently reach.
///
/// <para>Run last, after any emulator transform, because that is the whole point of the
/// indirection. A cfg binds directions one at a time and has no way to name a control as a unit,
/// so a button that does name one has to be re-read against wherever its members actually ended
/// up. Writing the generic inputs into the alias instead would fix them when the file was
/// written: measured over the rom pack, that cost thousands of games a label, the stick losing
/// its text wherever a cfg had moved the directions beneath it.</para>
///
/// <para>This replaces <c>WholeInputDeriver</c>, which did the same job for a fixed set of names
/// Core knew — <c>ButtonDpad</c>, <c>AxisLeftStick</c>, <c>AxisRightStick</c> — discovering the
/// whole-and-parts pairing from the natural mapping. Saying it in the platform's own file makes
/// the relationship data, and lets a platform declare whatever its hardware actually has.</para>
/// </summary>
public class AliasResolver(ILogger logger)
{
    private readonly ILogger _logger = logger;

    /// <summary>
    /// Returns <paramref name="mapping"/> with each alias in <paramref name="aliases"/> reaching
    /// the union of its members' current targets. The natural snapshot is resolved the same way
    /// against its own members, since <c>IsMapped</c> reads it and an aliased button would
    /// otherwise look unmapped however plainly the controller declares it.
    /// </summary>
    public ResolvedMapping Apply(ResolvedMapping mapping, IReadOnlyList<AliasEntry> aliases)
    {
        if (aliases.Count == 0) return mapping;

        (IReadOnlyDictionary<string, IReadOnlyList<string>> buttonToInput,
         IReadOnlyDictionary<string, string> inputToButton) =
            Expand(mapping.ButtonToInput, mapping.InputToButton, aliases, "current");

        (IReadOnlyDictionary<string, IReadOnlyList<string>> naturalButtonToInput,
         IReadOnlyDictionary<string, string> naturalInputToButton) =
            Expand(mapping.NaturalButtonToInput, mapping.NaturalInputToButton, aliases, "natural");

        return mapping with
        {
            ButtonToInput = buttonToInput,
            InputToButton = inputToButton,
            NaturalButtonToInput = naturalButtonToInput,
            NaturalInputToButton = naturalInputToButton,
        };
    }

    private (IReadOnlyDictionary<string, IReadOnlyList<string>>, IReadOnlyDictionary<string, string>) Expand(
        IReadOnlyDictionary<string, IReadOnlyList<string>> buttonToInput,
        IReadOnlyDictionary<string, string> inputToButton,
        IReadOnlyList<AliasEntry> aliases,
        string which)
    {
        var buttons = buttonToInput.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
        var inputs = inputToButton.ToDictionary(kv => kv.Key, kv => kv.Value);

        foreach (AliasEntry alias in aliases)
        {
            // Distinct, in member order: an input two members both reach is still one target,
            // and the order is the author's rather than a dictionary's.
            var reached = new List<string>();
            foreach (string member in alias.Mappings)
            {
                foreach (string input in buttonToInput.GetValueOrDefault(member, []))
                {
                    if (!reached.Contains(input)) reached.Add(input);
                }
            }

            if (reached.Count == 0)
            {
                _logger.Debug($"Alias ({which}): {alias.Name} reaches nothing — no member is bound");
                continue;
            }

            // Appended, never displacing. A button that drives an input directly outranks one
            // that merely stands for buttons which reach it, so the direct binding keeps the
            // reverse lookup and decides which image the player is shown.
            List<string> own = buttons.TryGetValue(alias.Name, out List<string>? existing) ? existing : [];
            foreach (string input in reached)
                if (!own.Contains(input)) own.Add(input);
            buttons[alias.Name] = own;

            foreach (string input in reached)
                inputs.TryAdd(input, alias.Name);

            _logger.Debug($"Alias ({which}): {alias.Name} -> {string.Join(", ", reached)}");
        }

        return (buttons.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value), inputs);
    }
}
