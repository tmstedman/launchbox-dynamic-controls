namespace DynamicControls.InputMapping;

/// <summary>
/// Fixed correspondence between generic Dpad input names and their analog-stick equivalents.
/// Used when a mapping has analogToDigital set to make any platform button that drives a Dpad
/// generic also drive its left- or right-stick equivalent — propagating labels and isMapped
/// state without affecting image resolution.
/// </summary>
public static class AnalogToDigitalMirror
{
    private static readonly IReadOnlyDictionary<string, string> DpadToLeftStick = new Dictionary<string, string>
    {
        ["ButtonDpad"] = "AxisLeftStick",
        ["ButtonDpadUp"] = "AxisLeftStickUp",
        ["ButtonDpadDown"] = "AxisLeftStickDown",
        ["ButtonDpadLeft"] = "AxisLeftStickLeft",
        ["ButtonDpadRight"] = "AxisLeftStickRight",
    };

    private static readonly IReadOnlyDictionary<string, string> DpadToRightStick = new Dictionary<string, string>
    {
        ["ButtonDpad"] = "AxisRightStick",
        ["ButtonDpadUp"] = "AxisRightStickUp",
        ["ButtonDpadDown"] = "AxisRightStickDown",
        ["ButtonDpadLeft"] = "AxisRightStickLeft",
        ["ButtonDpadRight"] = "AxisRightStickRight",
    };

    /// <summary>
    /// Appends the selected stick's generic to every list that contains the matching Dpad generic.
    /// Existing stick entries within the same list are not duplicated.
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
