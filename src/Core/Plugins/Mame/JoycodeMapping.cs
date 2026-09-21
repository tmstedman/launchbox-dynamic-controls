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
/// Translates MAME JOYCODE values to generic input names.
/// Loaded from JoycodeMapping.xml by JoycodeMappingLoader.
/// </summary>
public class JoycodeMapping(Dictionary<string, List<string>> data)
{
    private readonly Dictionary<string, List<string>> _data = data;

    /// <summary>
    /// Translates a MAME JOYCODE sequence into one or more generic input names.
    /// MAME sequences can chain multiple codes with "OR" (e.g. "JOYCODE_1_BUTTON3 OR JOYCODE_1_BUTTON4"),
    /// meaning either physical input triggers the same in-game function. A single JOYCODE can also map
    /// to more than one generic input on its own — a bare analogue axis (e.g. JOYCODE_1_XAXIS) has no
    /// sign, so it stands for both halves of the axis, and both are recorded rather than guessed at.
    /// Each recognized JOYCODE contributes every generic input it maps to; the results are returned
    /// in source order with duplicates removed. Non-JOYCODE tokens (KEYCODE_*, MOUSECODE_*, etc.) and
    /// unrecognized JOYCODEs are skipped.
    /// </summary>
    public IReadOnlyList<string> Translate(string? joycode)
    {
        if (joycode == null) return [];

        IEnumerable<string> genericNames = joycode.Split(' ')
            .Select(part => part.Trim())
            .Where(part => part.StartsWith("JOYCODE_"))
            .SelectMany(part => _data.TryGetValue(part, out List<string>? inputs) ? inputs : [])
            .Distinct();

        return [.. genericNames];
    }
}
