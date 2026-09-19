namespace DynamicControls.InputMapping;

/// <summary>
/// Follows a whole-input button onto whichever controls its directions end up driving.
///
/// <para>A platform button naming a whole control — MAME's <c>JOYSTICK</c>, a platform's
/// <c>Dpad-Any</c> — cannot be remapped by an emulator, because emulator configs bind individual
/// directions. So when a config moves <c>JOYSTICK_UP</c> and its three siblings onto the left
/// stick as well as the Dpad, <c>JOYSTICK</c> is left pointing at the Dpad alone and a label
/// written against it appears on only one of the two controls the player is actually using.</para>
///
/// <para>The pairing between a whole and its parts is discovered from
/// <see cref="ResolvedMapping.NaturalButtonToInput"/> — the state before the emulator's config was
/// applied, where the two are still aligned — rather than parsed out of button names, which vary
/// by platform (<c>JOYSTICK</c>, <c>Dpad-Any</c>, <c>L-Any</c>, <c>Stick-Any</c>) and cannot be
/// relied on.</para>
///
/// <para>Only layers that came from an emulator's configuration are derived over. A per-game
/// <c>InputMappings</c> file is written in the plugin's own vocabulary and can name a whole input
/// outright, so where its author could have said it and did not, the silence is the instruction.
/// That gate lives in <see cref="InputMappingService"/>, which decides whether to call this at
/// all — provenance is not visible here.</para>
/// </summary>
public static class WholeInputDeriver
{
    /// <summary>
    /// The generic inputs that are whole controls, each with the four direction inputs that make
    /// it up. Fixed vocabulary: these names are the plugin's own, not platform data.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> WholeToDirections =
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["ButtonDpad"] = ["ButtonDpadUp", "ButtonDpadDown", "ButtonDpadLeft", "ButtonDpadRight"],
            ["AxisLeftStick"] = ["AxisLeftStickUp", "AxisLeftStickDown", "AxisLeftStickLeft", "AxisLeftStickRight"],
            ["AxisRightStick"] = ["AxisRightStickUp", "AxisRightStickDown", "AxisRightStickLeft", "AxisRightStickRight"],
        };

    /// <summary>
    /// Returns <paramref name="mapping"/> with each whole-input button extended to the controls
    /// its direction siblings now drive, and the reverse lookup brought back into agreement.
    ///
    /// <para>A whole control is claimed only when <em>every one</em> of its four directions is
    /// covered. A config that binds only <c>UP</c> to the stick has not made the stick move the
    /// player, so the whole stick stays unclaimed and the label belongs to the Dpad alone.</para>
    ///
    /// <para>Derived inputs are <em>appended</em>, never prepended, so they sit after the
    /// button's own bindings. <c>InputLabelsService</c> ranks claims by position, so a button
    /// mapped directly to a control outranks one that merely reached it here — and for the same
    /// reason a derived binding never displaces an existing reverse-lookup entry.</para>
    /// </summary>
    public static ResolvedMapping Derive(ResolvedMapping mapping, ILogger logger)
    {
        var buttonToInput = mapping.ButtonToInput.ToDictionary(e => e.Key, e => e.Value);
        var inputToButton = mapping.InputToButton.ToDictionary(e => e.Key, e => e.Value);
        bool changed = false;

        foreach (KeyValuePair<string, IReadOnlyList<string>> entry in mapping.ButtonToInput)
        {
            IReadOnlyList<string> claimed = ClaimedWholes(entry.Key, mapping);
            List<string> additions = [.. claimed.Where(w => !entry.Value.Contains(w))];
            if (additions.Count == 0) continue;

            buttonToInput[entry.Key] = [.. entry.Value, .. additions];
            changed = true;
            logger.Debug($"Whole input: {entry.Key} follows its directions onto {string.Join(", ", additions)}");

            // First-seen-wins, as elsewhere: a button already on that control keeps it.
            foreach (string input in additions.Where(i => !inputToButton.ContainsKey(i)))
                inputToButton[input] = entry.Key;

            foreach (string input in additions.Where(i => inputToButton[i] != entry.Key))
                logger.Debug($"Whole input: {input} keeps {inputToButton[input]}, which is mapped to it directly");
        }

        return changed
            ? mapping with
            {
                ButtonToInput = buttonToInput.ToDictionary(e => e.Key, e => e.Value),
                InputToButton = inputToButton,
            }
            : mapping;
    }

    /// <summary>
    /// The whole controls that <paramref name="button"/>'s direction siblings collectively cover
    /// in the current mapping. Empty when the button names no whole control naturally, or when
    /// no whole control has all four of its directions accounted for.
    /// </summary>
    private static IReadOnlyList<string> ClaimedWholes(string button, ResolvedMapping mapping)
    {
        if (!mapping.NaturalButtonToInput.TryGetValue(button, out IReadOnlyList<string>? naturalInputs))
            return [];

        // The directions belonging to whichever whole controls this button names naturally.
        var ownDirections = new HashSet<string>(
            naturalInputs.Where(WholeToDirections.ContainsKey).SelectMany(w => WholeToDirections[w]));
        if (ownDirections.Count == 0) return [];

        // Its sibling buttons: the ones that drive those directions in the natural mapping.
        // Read from the *current* mapping, which is where the config moved them to.
        var reached = new HashSet<string>();
        foreach (KeyValuePair<string, IReadOnlyList<string>> sibling in mapping.NaturalButtonToInput)
        {
            if (sibling.Key == button || !sibling.Value.Any(ownDirections.Contains)) continue;
            if (mapping.ButtonToInput.TryGetValue(sibling.Key, out IReadOnlyList<string>? current))
                reached.UnionWith(current);
        }

        return [.. WholeToDirections
            .Where(w => w.Value.All(reached.Contains))
            .Select(w => w.Key)];
    }
}
