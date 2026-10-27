using System.Text.RegularExpressions;

namespace DynamicControls.Plugins.Mame;

/// <summary>
/// One platform button a MAME port states, and what it reaches. <see cref="Own"/> is what the
/// button binds; <see cref="Derived"/> what it reaches only as part of something larger — its
/// port's axis, or the joystick a direction belongs to (see
/// <see cref="MamePortBinding"/>), carried on to
/// <see cref="InputMapping.ResolvedMapping.DerivedButtonToInput"/>. Both empty is a button the
/// cfg states but leaves unbound, which still drops Controllers.xml's baseline for it.
/// <see cref="From"/> says which sequence it came from, for the log.
/// </summary>
public record MamePortReach(string Name, IReadOnlyList<string> Own, IReadOnlyList<string> Derived, string From)
{
    /// <summary>Everything the button reaches, own bindings first.</summary>
    public IReadOnlyList<string> Inputs => [.. Own, .. Derived];
}

/// <summary>
/// What one MAME port reaches, from the sequences it declares: the platform buttons it states and
/// each one's own and derived reach. Pure — the cfg has already been read, and the joycode
/// vocabulary is given.
/// </summary>
public static class MamePortBinding
{
    /// <summary>
    /// A true analogue port (DIAL, PADDLE, PEDAL, TRACKBALL_X/Y, ...) carries its "standard"
    /// joycode from real analogue hardware, but MAME also lets a player nudge the same value
    /// with two ordinary buttons instead — "increment" and "decrement" — for whichever direction
    /// analogue hardware isn't present. "NONE" means that sequence isn't bound.
    /// </summary>
    public static readonly IReadOnlyList<string> SequenceTypes = ["standard", "increment", "decrement"];

    /// <summary>
    /// Each sequence becomes its own platform button: the port's own name for the analogue
    /// binding, and a <c>.inc</c>/<c>.dec</c> sibling for each nudge direction.
    ///
    /// <para>They were unioned onto the one name until it proved lossy. The nudge buttons are
    /// routinely a different physical control from the axis — Air Combat 22 steers its throttle
    /// with the right stick and nudges it with the two triggers — and the artwork labels them
    /// separately ("Throttle", "Throttle Up", "Throttle Down"). One name carries one label, so
    /// unioning them made the port say one word about three controls.</para>
    ///
    /// <para>The suffix is MAME's own sequence type rather than a direction, because which way
    /// a nudge goes is the game's business: Air Combat 22's increment is its throttle
    /// <em>down</em>. A platform button standing for the pair is declared in Controllers.xml, so
    /// a game that draws no distinction still labels all three by naming the port alone.</para>
    /// </summary>
    /// <summary>One direction of a joystick, in MAME's port naming: <c>P1_JOYSTICK_UP</c>,
    /// <c>P1_JOYSTICKLEFT_DOWN</c>.</summary>
    private static readonly Regex JoystickDirection = new(@"_JOYSTICK[A-Z]*_(UP|DOWN|LEFT|RIGHT)$");

    private static readonly (string SeqType, string Suffix)[] SequenceButtons =
        [("standard", ""), ("increment", ".inc"), ("decrement", ".dec")];

    /// <summary>
    /// Resolves <paramref name="port"/>'s buttons. <paramref name="declared"/> holds each sequence
    /// the port declares, <see cref="MameSequence.Unbound"/> where it is <c>NONE</c> — which ones
    /// it declares is how an analogue port announces itself: MAME writes increment and decrement
    /// on anything nudgeable, as NONE where unbound, and neither on an ordinary button. Returns
    /// nothing at all when no bound sequence translates.
    /// </summary>
    public static IReadOnlyList<MamePortReach> Resolve(
        string port, IReadOnlyDictionary<string, MameSequence> declared, JoycodeMapping joycodes)
    {
        MameSequence standard = declared.GetValueOrDefault("standard", MameSequence.Unbound);
        MameSequence increment = declared.GetValueOrDefault("increment", MameSequence.Unbound);
        MameSequence decrement = declared.GetValueOrDefault("decrement", MameSequence.Unbound);

        // A switch at each end of one axis, one in each nudge, is a stick standing in for the
        // port's analogue binding — bound as switches so it keeps turning while held. It is the
        // port's own reach, as if the axis were bound to standard; the nudges reach their half of
        // it only because the port does.
        (MameSequence incEnd, MameSequence decEnd) = increment.AxisEndsSharedWith(decrement);
        MameSequence axisPair = incEnd.Concat(decEnd);

        var bySeqType = new Dictionary<string, (List<string> Own, List<string> Derived)>
        {
            ["standard"] = SplitJoystickDirection(port, joycodes, standard, axisPair),
            ["increment"] = Split(joycodes, increment.Without(incEnd), incEnd),
            ["decrement"] = Split(joycodes, decrement.Without(decEnd), decEnd),
        };
        if (bySeqType.Values.All(r => r.Own.Count + r.Derived.Count == 0)) return [];

        // A bound port states all three of its names, empty where that sequence isn't bound.
        // Stating them is what drops Controllers.xml's baseline for the ones this cfg doesn't use:
        // the baseline describes the axis as the cabinet has it, and a cfg that moves the axis
        // elsewhere, or drives it by nudges alone, would otherwise leave the unused names still
        // pointing at the old guess — the port then reaching both where the cfg put it and where
        // the baseline assumed.
        var reach = new List<MamePortReach>();
        foreach ((string seqType, string suffix) in SequenceButtons)
        {
            // The port's own name is always stated; a nudge name only when the port declares
            // that sequence, so an ordinary button never grows one.
            if (suffix.Length > 0 && !declared.ContainsKey(seqType)) continue;

            (List<string> own, List<string> derived) = bySeqType[seqType];
            MameSequence sequence = declared.GetValueOrDefault(seqType, MameSequence.Unbound);
            string from = sequence.IsBound ? sequence.ToString()
                : suffix.Length == 0 && axisPair.IsBound ? $"{axisPair}, from the nudges"
                : "unbound";

            // A nudge sequence the cfg leaves unbound still names a real half of the axis, when
            // the axis itself is bound: the hardware drives the value both ways, and the joycode
            // says which end is which. Reading the half off the axis is what lets a label written
            // for one direction land on a cabinet whose cfg has no nudge buttons at all — the
            // common case by far, since nudges exist for cabinets lacking the analogue hardware.
            // It is derived, like the stick half a nudge reaches above, so the port's own label
            // outranks it there. The baseline is still dropped: these come from the axis this cfg
            // names, never from Controllers.xml's guess about which axis the port would use.
            if (suffix.Length > 0 && !sequence.IsBound && standard.IsBound)
            {
                string half = suffix[1..];
                derived = [.. joycodes.TranslateHalf(standard.ToString(), half)];
                if (derived.Count > 0) from = $"{standard}, {half} half";
            }

            reach.Add(new MamePortReach(port + suffix, own, derived, from));
        }
        return reach;
    }

    /// <summary>
    /// The port's own sequence. A joystick direction port carrying a button alongside its hat and
    /// stick is two controls: the joystick's direction, which the port reaches only because the
    /// joystick does, and a button that is the port's alone — gberet binds its jump button onto
    /// up. So the direction is derived, and a label of the whole joystick's outranks the port's
    /// there, while the port's own label keeps to the button. A port of directions alone is
    /// simply that direction and keeps it all, so whether MAME happened to write the port into
    /// the cfg never decides who labels it.
    /// </summary>
    private static (List<string> Own, List<string> Derived) SplitJoystickDirection(
        string port, JoycodeMapping joycodes, MameSequence standard, MameSequence axisPair)
    {
        List<string> inputs = [.. Translate(joycodes, standard).Concat(Translate(joycodes, axisPair)).Distinct()];
        if (!JoystickDirection.IsMatch(port)) return (inputs, []);

        MameSequence directions = standard.Directions();
        (List<string> own, List<string> derived) = Split(joycodes, standard.Without(directions), directions);
        return own.Count > 0 && derived.Count > 0 ? (own, derived) : (inputs, []);
    }

    /// <summary>A nudge's own reach, and what it derives from the axis end it holds.</summary>
    private static (List<string> Own, List<string> Derived) Split(
        JoycodeMapping joycodes, MameSequence own, MameSequence axisEnd)
    {
        List<string> ownInputs = Translate(joycodes, own);
        return (ownInputs, [.. Translate(joycodes, axisEnd).Where(i => !ownInputs.Contains(i))]);
    }

    private static List<string> Translate(JoycodeMapping joycodes, MameSequence sequence) =>
        sequence.IsBound ? [.. joycodes.Translate(sequence.ToString()).Distinct()] : [];
}
