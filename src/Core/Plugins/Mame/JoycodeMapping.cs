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
public class JoycodeMapping(Dictionary<string, string> data)
{
    private readonly Dictionary<string, string> _data = data;

    /// <summary>
    /// Translates a MAME JOYCODE sequence into one or more generic input names.
    /// MAME sequences can chain multiple codes with "OR" (e.g. "JOYCODE_1_BUTTON3 OR JOYCODE_1_BUTTON4"),
    /// meaning either physical input triggers the same in-game function. Each recognized JOYCODE is
    /// translated and the results are returned in source order with duplicates removed. Non-JOYCODE
    /// tokens (KEYCODE_*, MOUSECODE_*, etc.) and unrecognized JOYCODEs are skipped.
    /// </summary>
    public IReadOnlyList<string> Translate(string? joycode)
    {
        if (joycode == null) return [];

        IEnumerable<string> genericNames = joycode.Split(' ')
            .Select(part => part.Trim())
            .Where(part => part.StartsWith("JOYCODE_"))
            .Select(part => _data.GetValueOrDefault(part))
            .OfType<string>()
            .Distinct();

        return [.. genericNames];
    }
}
