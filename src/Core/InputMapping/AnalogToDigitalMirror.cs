namespace DynamicControls.InputMapping;

/// <summary>
/// Fixed correspondence between generic Dpad direction names and their analog-stick equivalents.
/// Used when a mapping has analogToDigital set to make any platform button that drives a Dpad
/// direction also drive its left- or right-stick equivalent — propagating labels and isMapped
/// state without affecting image resolution.
///
/// <para>Directions only: the composite names (<c>ButtonDpad</c>, <c>AxisLeftStick</c>,
/// <c>AxisRightStick</c>) are deliberately absent. Which inputs form a composite is the
/// template's statement, declared with <c>&lt;Whole&gt;</c>, and a platform says a button stands
/// for several with <c>&lt;Alias&gt;</c> — neither needs a name this layer knows in advance. A
/// button declared that way reaches the four directions, so mirroring them produces the four
/// stick directions and the composite follows from those.</para>
/// </summary>
public static class AnalogToDigitalMirror
{
    private static readonly IReadOnlyDictionary<string, string> DpadToLeftStick = new Dictionary<string, string>
    {
        ["ButtonDpadUp"] = "AxisLeftStickUp",
        ["ButtonDpadDown"] = "AxisLeftStickDown",
        ["ButtonDpadLeft"] = "AxisLeftStickLeft",
        ["ButtonDpadRight"] = "AxisLeftStickRight",
    };

    private static readonly IReadOnlyDictionary<string, string> DpadToRightStick = new Dictionary<string, string>
    {
        ["ButtonDpadUp"] = "AxisRightStickUp",
        ["ButtonDpadDown"] = "AxisRightStickDown",
        ["ButtonDpadLeft"] = "AxisRightStickLeft",
        ["ButtonDpadRight"] = "AxisRightStickRight",
    };

    /// <summary>
    /// Appends the selected stick's generic to every list that contains the matching Dpad generic.
    /// Existing stick entries within the same list are not duplicated.
    ///
    /// <para>Mirrored entries are always appended, so they sit after the button's own bindings.
    /// <c>InputLabelsService</c> relies on that ordering: where a mirrored binding and a real one
    /// land on the same input, the earlier — real — binding owns the label.</para>
    /// </summary>
    public static void Mirror(Dictionary<string, List<string>> buttonToInput, AnalogToDigitalMode mode)
    {
        IReadOnlyDictionary<string, string> map = mode switch
        {
            AnalogToDigitalMode.Left => DpadToLeftStick,
            AnalogToDigitalMode.Right => DpadToRightStick,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown AnalogToDigitalMode")
        };
        foreach (List<string> generics in buttonToInput.Values)
        {
            var toAdd = generics
                .Where(map.ContainsKey)
                .Select(g => map[g])
                .Where(stick => !generics.Contains(stick))
                .ToList();
            generics.AddRange(toAdd);
        }
    }
}

/// <summary>
/// Which analog stick the Dpad mirrors onto when analogToDigital is set.
/// </summary>
public enum AnalogToDigitalMode
{
    Left,
    Right
}
