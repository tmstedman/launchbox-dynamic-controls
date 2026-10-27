namespace DynamicControls.InputMapping;

/// <summary>
/// Resolves <see cref="ControlEntry"/> declarations against a mapping: a platform button standing
/// for several others reaches whatever those others currently reach.
///
/// <para>Run last, after any emulator transform, because that is the whole point of the
/// indirection. A cfg binds directions one at a time and has no way to name a control as a unit,
/// so a button that does name one has to be re-read against wherever its members actually ended
/// up. Writing the generic inputs into the control instead would fix them when the file was
/// written: measured over the rom pack, that cost thousands of games a label, the stick losing
/// its text wherever a cfg had moved the directions beneath it.</para>
///
/// <para>This replaces <c>WholeInputDeriver</c>, which did the same job for a fixed set of names
/// Core knew — <c>ButtonDpad</c>, <c>AxisLeftStick</c>, <c>AxisRightStick</c> — discovering the
/// whole-and-parts pairing from the natural mapping. Saying it in the platform's own file makes
/// the relationship data, and lets a platform declare whatever its hardware actually has.</para>
/// </summary>
public class ControlResolver(ILogger logger)
{
    private readonly ILogger _logger = logger;

    /// <summary>
    /// Returns <paramref name="mapping"/> with each control in <paramref name="controls"/> reaching
    /// the union of its members' current targets. The natural snapshot is resolved the same way
    /// against its own members, since <c>IsMapped</c> reads it and a button reached only
    /// through one would otherwise look unmapped however plainly the controller declares it.
    /// </summary>
    public ResolvedMapping Apply(ResolvedMapping mapping, IReadOnlyList<ControlEntry> controls)
    {
        if (controls.Count == 0) return mapping;

        (IReadOnlyDictionary<string, IReadOnlyList<string>> buttonToInput,
         IReadOnlyDictionary<string, string> inputToButton,
         IReadOnlyDictionary<string, IReadOnlyList<string>> derived) =
            Expand(mapping.ButtonToInput, mapping.InputToButton, mapping.DerivedButtonToInput,
                mapping.ControlledButtonToInput, controls, "current");

        // Only the current view's derived reach is read — by the labels — so the natural
        // snapshot's is discarded.
        (IReadOnlyDictionary<string, IReadOnlyList<string>> naturalButtonToInput,
         IReadOnlyDictionary<string, string> naturalInputToButton, _) =
            Expand(mapping.NaturalButtonToInput, mapping.NaturalInputToButton, mapping.DerivedButtonToInput,
                mapping.ControlledButtonToInput, controls, "natural");

        return mapping with
        {
            ButtonToInput = buttonToInput,
            InputToButton = inputToButton,
            NaturalButtonToInput = naturalButtonToInput,
            NaturalInputToButton = naturalInputToButton,
            DerivedButtonToInput = derived,
        };
    }

    private (IReadOnlyDictionary<string, IReadOnlyList<string>>, IReadOnlyDictionary<string, string>, IReadOnlyDictionary<string, IReadOnlyList<string>>) Expand(
        IReadOnlyDictionary<string, IReadOnlyList<string>> buttonToInput,
        IReadOnlyDictionary<string, string> inputToButton,
        IReadOnlyDictionary<string, IReadOnlyList<string>> derivedButtonToInput,
        IReadOnlyDictionary<string, IReadOnlyList<string>> controlledButtonToInput,
        IReadOnlyList<ControlEntry> controls,
        string which)
    {
        var buttons = buttonToInput.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
        var inputs = inputToButton.ToDictionary(kv => kv.Key, kv => kv.Value);
        var derived = derivedButtonToInput.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());

        foreach (ControlEntry control in controls)
        {
            // Distinct, in member order: an input two members both reach is still one target,
            // and the order is the author's rather than a dictionary's.
            var reached = new List<string>();
            foreach (string member in control.Mappings)
            {
                foreach (string input in buttonToInput.GetValueOrDefault(member, []))
                {
                    if (!reached.Contains(input)) reached.Add(input);
                }
            }

            if (reached.Count == 0)
            {
                _logger.Debug($"Control ({which}): {control.Name} reaches nothing — no member is bound");
                continue;
            }

            // Appended, never displacing. A button that drives an input directly outranks one
            // that merely stands for buttons which reach it, so the direct binding keeps the
            // reverse lookup and decides which image the player is shown.
            // What it appends is derived — reached only because a member reaches it — so a
            // label of the control's own outranks a member's only where the control binds it too,
            // or where a member reaches it only on the control's behalf: gberet's up port reaches
            // the stick because the joystick does, so the joystick's label is the stick's.
            var heldForIt = new HashSet<string>(control.Mappings.SelectMany(m => controlledButtonToInput.GetValueOrDefault(m, [])));
            List<string> own = buttons.TryGetValue(control.Name, out List<string>? existing) ? existing : [];
            List<string> appended = derived.TryGetValue(control.Name, out List<string>? soFar) ? soFar : [];
            foreach (string input in reached)
            {
                if (own.Contains(input)) continue;
                own.Add(input);
                if (!heldForIt.Contains(input)) appended.Add(input);
            }
            buttons[control.Name] = own;
            if (appended.Count > 0) derived[control.Name] = appended;

            foreach (string input in reached)
                inputs.TryAdd(input, control.Name);

            _logger.Debug($"Control ({which}): {control.Name} -> {string.Join(", ", reached)}");
        }

        return (buttons.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value),
                inputs,
                derived.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value));
    }
}
