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
/// for several with <c>&lt;Control&gt;</c> — neither needs a name this layer knows in advance. A
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
    /// <c>InputMappingResolver</c> relies on that ordering to tell them apart, recording whatever
    /// the mirror appended as derived — which is what lets a real binding landing on the same
    /// input own its label and image.</para>
    /// </summary>
    public static void Mirror(Dictionary<string, List<string>> buttonToInput, AnalogToDigitalMode mode)
    {
        IReadOnlyDictionary<string, string> map = MapFor(mode);
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

    /// <summary>
    /// What <see cref="Mirror"/> would give <paramref name="mappings"/>' own Dpad directions, as
    /// derived entries a resolver can take alongside another mapping's. For a mapping an
    /// emulator's config has swapped: RetroArch works out the stick's Dpad presses from the stick
    /// itself, after any remap, so the stick presses the Dpad the controller started with, not
    /// wherever a swap moved its buttons.
    /// </summary>
    public static IEnumerable<MappingEntry> EntriesFor(IEnumerable<MappingEntry> mappings, AnalogToDigitalMode mode)
    {
        IReadOnlyDictionary<string, string> map = MapFor(mode);
        return mappings
            .Where(e => map.ContainsKey(e.Input))
            .Select(e => new MappingEntry { Name = e.Name, Input = map[e.Input], Derived = true });
    }

    /// <summary>Whether <paramref name="generic"/> is a direction of the stick
    /// <paramref name="mode"/> mirrors the Dpad onto.</summary>
    public static bool IsMirroredStickDirection(string generic, AnalogToDigitalMode mode) =>
        MapFor(mode).Values.Contains(generic);

    private static IReadOnlyDictionary<string, string> MapFor(AnalogToDigitalMode mode) => mode switch
    {
        AnalogToDigitalMode.Left => DpadToLeftStick,
        AnalogToDigitalMode.Right => DpadToRightStick,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown AnalogToDigitalMode")
    };
}

/// <summary>
/// Which analog stick the Dpad mirrors onto when analogToDigital is set.
/// </summary>
public enum AnalogToDigitalMode
{
    Left,
    Right
}
