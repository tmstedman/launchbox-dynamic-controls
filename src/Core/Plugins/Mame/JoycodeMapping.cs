namespace DynamicControls.Plugins.Mame;

/// <summary>
/// Provides the JOYCODE-to-generic-input mapping parsed from JoycodeMapping.xml. The result is
/// cached on first call; later calls are O(1). Exists primarily to let consumers (and tests)
/// substitute the loader cleanly.
/// </summary>
public interface IJoycodeMappingLoader
{
    JoycodeMapping Load();
}

/// <summary>
/// One generic input a joycode reaches, which half of a bare axis it is, and which end of an axis
/// the joycode is as a switch.
///
/// <para><see cref="Half"/> is <c>"dec"</c> or <c>"inc"</c> on the two halves of a bare analogue
/// axis, and null everywhere else — an ordinary button, a trigger or a switch has no half of
/// itself to offer a nudge. It names MAME's own sequence types rather than a direction, because
/// which way a nudge goes is the game's business, not the hardware's.</para>
///
/// <para><see cref="End"/> is set on a stick axis read as a switch, naming which end of which
/// axis it is, so two switches can be recognised as one axis's two ends. It is kept apart from
/// <see cref="Half"/> because a switch is not half of anything: a dial bound to one switch must
/// not hand a nudge that switch's direction.</para>
/// </summary>
public record JoycodeTarget(string Input, string? Half, AxisEnd? End = null);

/// <summary>
/// Which end of which axis a joycode is, when it is a stick axis read as a switch.
/// <see cref="Axis"/> is only a name to match on — two joycodes are one axis's two ends when they
/// name the same axis and opposite sides — <c>"dec"</c> and <c>"inc"</c>, as a bare axis names its halves.
/// </summary>
public record AxisEnd(string Axis, string Side)
{
    public AxisEnd Opposite => this with { Side = Side == "dec" ? "inc" : "dec" };
}

/// <summary>
/// Translates MAME JOYCODE values to generic input names.
/// Loaded from JoycodeMapping.xml by JoycodeMappingLoader.
/// </summary>
public class JoycodeMapping(Dictionary<string, List<JoycodeTarget>> data)
{
    private readonly Dictionary<string, List<JoycodeTarget>> _data = data;

    /// <summary>
    /// A mapping whose joycodes state no axis halves. Every ordinary button is this shape — only
    /// the two halves of a bare analogue axis have an end to name.
    /// </summary>
    public JoycodeMapping(Dictionary<string, List<string>> data)
        : this(data.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Select(input => new JoycodeTarget(input, null)).ToList()))
    { }

    /// <summary>
    /// Translates a MAME JOYCODE sequence into one or more generic input names.
    /// MAME sequences can chain multiple codes with "OR" (e.g. "JOYCODE_1_BUTTON3 OR JOYCODE_1_BUTTON4"),
    /// meaning either physical input triggers the same in-game function. A single JOYCODE can also map
    /// to more than one generic input on its own — a bare analogue axis (e.g. JOYCODE_1_XAXIS) has no
    /// sign in the token itself, so it stands for both halves of the axis and both are recorded.
    /// Each recognized JOYCODE contributes every generic input it maps to; the results are returned
    /// in source order with duplicates removed. Non-JOYCODE tokens (KEYCODE_*, MOUSECODE_*, etc.) and
    /// unrecognized JOYCODEs are skipped.
    /// </summary>
    public IReadOnlyList<string> Translate(string? joycode) => Resolve(joycode, half: null);

    /// <summary>
    /// The generic inputs this joycode reaches on one half of its axis — the <c>"dec"</c> or
    /// <c>"inc"</c> end — or none where the joycode doesn't state halves at all.
    ///
    /// <para>Which end is which is a fact about the joycode, declared alongside the input in
    /// JoycodeMapping.xml, and the reversed form of an axis simply declares the opposite halves.
    /// That keeps <c>_REVERSE</c> a spelling this code never has to recognise: nothing here knows
    /// the suffix exists, and a platform that inverts some other way says so the same way.</para>
    /// </summary>
    public IReadOnlyList<string> TranslateHalf(string? joycode, string half) => Resolve(joycode, half);

    /// <summary>
    /// Which end of which axis <paramref name="joycode"/> is, or null for a joycode declaring no
    /// end — a button, a trigger, a bare axis (both ends at once), a hat, a chord, or a joycode the
    /// mapping doesn't know. Declared in JoycodeMapping.xml rather than read off the joycode's
    /// name, so a device whose codes this file has never seen needs only a line of data.
    /// </summary>
    public AxisEnd? EndOf(string joycode) =>
        _data.TryGetValue(joycode, out List<JoycodeTarget>? targets)
            ? targets.Select(t => t.End).FirstOrDefault(end => end != null)
            : null;

    /// <summary>Whether any joycode declares an end — false for a mapping written before ends were.</summary>
    internal bool DeclaresAnyEnd => _data.Values.Any(targets => targets.Any(t => t.End != null));

    private IReadOnlyList<string> Resolve(string? joycode, string? half)
    {
        if (joycode == null) return [];

        IEnumerable<string> genericNames = joycode.Split(' ')
            .Select(part => part.Trim())
            .Where(part => part.StartsWith("JOYCODE_"))
            .SelectMany(part => _data.TryGetValue(part, out List<JoycodeTarget>? targets) ? targets : [])
            .Where(target => half == null || target.Half == half)
            .Select(target => target.Input)
            .Distinct();

        return [.. genericNames];
    }
}
