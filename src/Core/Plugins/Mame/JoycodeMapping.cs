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
/// One generic input a joycode reaches, and which way along the axis it lies.
///
/// <para><see cref="Half"/> is <c>"dec"</c> or <c>"inc"</c> on the two halves of a bare analogue
/// axis, and on a switch at one end of an axis, which also names that axis in
/// <see cref="Axis"/>; null everywhere else — an ordinary button or a trigger has no direction to
/// state. It names MAME's own sequence types rather than a direction, because which way a nudge
/// goes is the game's business, not the hardware's.</para>
/// </summary>
public record JoycodeTarget(string Input, string? Half, string? Axis = null);

/// <summary>
/// Which end of which axis a joycode is: a hat direction, or a stick axis read as a switch.
/// <see cref="Axis"/> is only a name to match on — two joycodes are one axis's two ends when they
/// name the same axis and opposite halves.
/// </summary>
public record AxisEnd(string Axis, string Half)
{
    public AxisEnd Opposite => this with { Half = Half == "dec" ? "inc" : "dec" };
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
    /// axis — a button, a trigger, a bare axis (both ends at once), a chord, or a joycode the
    /// mapping doesn't know. Declared in JoycodeMapping.xml rather than read off the joycode's
    /// name, so a device whose codes this file has never seen needs only a line of data.
    /// </summary>
    public AxisEnd? EndOf(string joycode) =>
        _data.TryGetValue(joycode, out List<JoycodeTarget>? targets)
            && targets.FirstOrDefault(t => t.Axis != null) is { Axis: string axis, Half: string half }
            ? new AxisEnd(axis, half)
            : null;

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
