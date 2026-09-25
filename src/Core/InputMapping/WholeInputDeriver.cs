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
/// <c>ControllerOverrides</c> file is written in the plugin's own vocabulary and can name a whole input
/// outright, so where its author could have said it and did not, the silence is the instruction.
/// That gate lives in <see cref="InputMappingService"/>, which decides whether to call this at
/// all — provenance is not visible here.</para>
///
/// <para>Alongside the whole-level claim this has always added, the button is also extended onto
/// each individual direction its siblings currently reach — the plugin's own vocabulary
/// (<see cref="WholeInputs.PartsOf"/>), not the platform's. That's what lets
/// <c>InputLabelsService</c>'s final collapse pass tell a genuine per-direction disagreement
/// (one direction swapped onto an ordinary button) from the ordinary case where all four still
/// agree, rather than this class's own whole-level rule silently smoothing either shape over. The
/// direction additions cost nothing when everything agrees — the labels layer collapses them
/// straight back down to the same result this class already produces on its own.</para>
///
/// <para>A real class rather than a static one, and injected rather than called directly, so the
/// logger arrives once through the constructor instead of being threaded down from a caller that
/// has no other use for it. It holds no state: <see cref="Derive"/> is a function of its
/// argument. Concrete rather than behind an interface, because nothing substitutes it — the
/// logger is the seam, as with <c>LayeredFileSystem</c>.</para>
/// </summary>
public class WholeInputDeriver(ILogger logger)
{
    private readonly ILogger _logger = logger;
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> WholeToDirections => WholeInputs.PartsOf;

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
    public ResolvedMapping Derive(ResolvedMapping mapping)
    {
        var buttonToInput = mapping.ButtonToInput.ToDictionary(e => e.Key, e => e.Value);
        var inputToButton = mapping.InputToButton.ToDictionary(e => e.Key, e => e.Value);
        var dropped = new HashSet<string>();
        bool changed = false;

        foreach (KeyValuePair<string, IReadOnlyList<string>> entry in mapping.ButtonToInput)
        {
            DirectionReach? reach = Reach(entry.Key, mapping);

            // Null means there was nothing to test this button against — it names no whole
            // control, or no sibling drives any of its directions. Leave it exactly as it is:
            // a button mapped straight to a whole control with no directions anywhere still
            // drives that control, and silence is not evidence against it.
            if (reach == null) continue;
            IReadOnlyCollection<string> claimed = reach.Claimed;

            // Keep everything that isn't a whole control, and every whole control that at least
            // one direction still reaches. Dropping needs the stronger evidence: a control some
            // directions still work on is a control the label is still true of, and plenty of
            // arcade cabinets have two-way joysticks that never drove all four to begin with.
            List<string> kept = [.. entry.Value.Where(i =>
                !WholeToDirections.ContainsKey(i) || WholeToDirections[i].Any(reach.Reached.Contains))];
            List<string> wholeAdditions = [.. claimed.Where(w => !entry.Value.Contains(w))];

            // Alongside the whole controls (above), also add each individual direction a sibling
            // currently reaches. This is what lets a label collapse pass (InputLabelsService)
            // detect a real per-direction disagreement -- e.g. one direction swapped onto an
            // ordinary button -- instead of the whole's own label silently smoothing it over. It
            // costs nothing when every direction agrees: the labels layer collapses them straight
            // back down to the same whole-level result this class already produces on its own.
            List<string> directionAdditions = [.. reach.Reached.Where(i => !entry.Value.Contains(i))];
            List<string> additions = [.. wholeAdditions, .. directionAdditions];
            List<string> updated = [.. kept, .. additions];
            if (updated.SequenceEqual(entry.Value)) continue;

            buttonToInput[entry.Key] = updated;
            changed = true;

            foreach (string stale in entry.Value.Where(i => !updated.Contains(i)))
            {
                dropped.Add(stale);
                _logger.Debug($"Whole input: {entry.Key} no longer drives {stale} — every one of its directions has moved away");
            }

            if (wholeAdditions.Count > 0)
                _logger.Debug($"Whole input: {entry.Key} follows its directions onto {string.Join(", ", wholeAdditions)}");

            // First-seen-wins, as elsewhere: a button already on that control keeps it.
            foreach (string input in additions.Where(i => !inputToButton.ContainsKey(i)))
                inputToButton[input] = entry.Key;

            foreach (string input in additions.Where(i => inputToButton[i] != entry.Key))
                _logger.Debug($"Whole input: {input} keeps {inputToButton[input]}, which is mapped to it directly");
        }

        // Bring the reverse lookup back into step for anything dropped. A control another button
        // still drives passes to that button; one nothing drives leaves the lookup altogether,
        // which is what makes it read as unmapped.
        foreach (string stale in dropped)
        {
            if (!inputToButton.ContainsKey(stale)) continue;

            string? survivor = buttonToInput
                .Where(e => e.Value.Contains(stale))
                .Select(e => e.Key)
                .FirstOrDefault();

            if (survivor == null) inputToButton.Remove(stale);
            else inputToButton[stale] = survivor;
        }

        return changed
            ? mapping with
            {
                ButtonToInput = buttonToInput.ToDictionary(e => e.Key, e => e.Value),
                InputToButton = inputToButton,
            }
            : mapping;
    }

    /// <summary>Where a button's direction siblings have ended up: every input they now drive,
    /// and the whole controls they cover completely.</summary>
    /// <param name="Reached">Every input the siblings drive in the current mapping. One of a
    /// whole control's directions appearing here is enough to keep an existing claim.</param>
    /// <param name="Claimed">The whole controls every one of whose directions is reached. Only
    /// these are added, because asserting a new claim takes the stronger evidence.</param>
    private sealed record DirectionReach(
        IReadOnlySet<string> Reached,
        IReadOnlyCollection<string> Claimed);

    /// <summary>
    /// Follows <paramref name="button"/>'s direction siblings to wherever the config has put them.
    ///
    /// <para>Returns <c>null</c> rather than an empty result when the question does not apply: the
    /// button names no whole control naturally, or no other button drives any of its directions.
    /// Those cases carry no evidence either way, and must not be read as evidence of absence.</para>
    /// </summary>
    private static DirectionReach? Reach(string button, ResolvedMapping mapping)
    {
        if (!mapping.NaturalButtonToInput.TryGetValue(button, out IReadOnlyList<string>? naturalInputs))
            return null;

        // The directions belonging to whichever whole controls this button names naturally.
        var ownDirections = new HashSet<string>(
            naturalInputs.Where(WholeToDirections.ContainsKey).SelectMany(w => WholeToDirections[w]));
        if (ownDirections.Count == 0) return null;

        // Its sibling buttons: the ones that drive those directions in the natural mapping.
        // Read from the *current* mapping, which is where the config moved them to.
        var reached = new HashSet<string>();
        bool anySibling = false;
        foreach (KeyValuePair<string, IReadOnlyList<string>> sibling in mapping.NaturalButtonToInput)
        {
            if (sibling.Key == button || !sibling.Value.Any(ownDirections.Contains)) continue;
            anySibling = true;
            if (mapping.ButtonToInput.TryGetValue(sibling.Key, out IReadOnlyList<string>? current))
                reached.UnionWith(current);
        }
        if (!anySibling) return null;

        return new DirectionReach(
            Reached: reached,
            Claimed: [.. WholeToDirections.Where(w => w.Value.All(reached.Contains)).Select(w => w.Key)]);
    }
}
