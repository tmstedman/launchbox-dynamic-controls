using DynamicControls.InputMapping;

namespace DynamicControls.Core.TestHelpers.InputMapping;

/// <summary>
/// Factory helpers for <see cref="ResolvedMapping"/>. Use via
/// <c>using static DynamicControls.Core.TestHelpers.InputMapping.MappingFixtures;</c>. Defaults
/// are intentionally empty/neutral; pass only what the test cares about.
/// </summary>
public static class MappingFixtures
{
    public static ResolvedMapping EmptyMapping(string platform = "Sega Genesis", string? controller = null) =>
        MappingOf(platform: platform, controller: controller);

    public static ResolvedMapping MappingOf(
        string platform = "Sega Genesis",
        string? controller = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? buttonToInput = null,
        IReadOnlyDictionary<string, string>? inputToButton = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? naturalButtonToInput = null,
        IReadOnlyDictionary<string, string>? naturalInputToButton = null,
        AnalogToDigitalMode? analogToDigital = null)
    {
        return new(
            Platform: platform,
            Controller: controller,
            ButtonToInput: buttonToInput ?? new Dictionary<string, IReadOnlyList<string>>(),
            InputToButton: inputToButton ?? new Dictionary<string, string>(),
            NaturalButtonToInput: naturalButtonToInput ?? new Dictionary<string, IReadOnlyList<string>>(),
            NaturalInputToButton: naturalInputToButton ?? new Dictionary<string, string>(),
            AnalogToDigital: analogToDigital);
    }
}
