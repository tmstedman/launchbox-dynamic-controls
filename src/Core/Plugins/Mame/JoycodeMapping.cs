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
/// axis, and null everywhere else — an ordinary button has no direction to state. It names MAME's
/// own sequence types rather than a direction, because which way a nudge goes is the game's
/// business, not the hardware's.</para>
/// </summary>
public record JoycodeTarget(string Input, string? Half);

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
