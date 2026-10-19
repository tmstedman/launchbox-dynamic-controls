using DynamicControls.InputMapping;
using DynamicControls.Rendering;
using DynamicControls.Templates;
using static DynamicControls.Core.TestHelpers.InputMapping.MappingFixtures;

namespace DynamicControls.Core.TestHelpers.Rendering;

/// <summary>
/// Factory helpers for rendering-pass inputs: <see cref="VisibilityContext"/>. Use via
/// <c>using static DynamicControls.Core.TestHelpers.Rendering.RenderingFixtures;</c>.
/// Cross-subsystem inputs (mapping, labels, template) live in their own subsystem fixture files.
/// </summary>
public static class RenderingFixtures
{
    /// <param name="wholes">Composite controls the template declares. Defaults to the shipped
    /// template's own — a stick or d-pad and its directions — because that is what nearly every
    /// rendering test means by "a controller", and a context without them fans out across
    /// nothing. Pass an empty dictionary to test a template that declares no composites.</param>
    public static VisibilityContext Ctx(
        ResolvedMapping? mapping = null,
        IReadOnlyDictionary<string, string>? labelText = null,
        bool isGameSpecific = false,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? wholes = null)
    {
        return new(
            Mapping: mapping ?? EmptyMapping(),
            LabelText: labelText ?? new Dictionary<string, string>(),
            IsGameSpecific: isGameSpecific,
            Wholes: wholes ?? StandardWholes);
    }

    /// <summary>The composites the shipped template declares, as a test default.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> StandardWholes =
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["ButtonDpad"] = ["ButtonDpadUp", "ButtonDpadDown", "ButtonDpadLeft", "ButtonDpadRight"],
            ["AxisLeftStick"] = ["AxisLeftStickUp", "AxisLeftStickDown", "AxisLeftStickLeft", "AxisLeftStickRight"],
            ["AxisRightStick"] = ["AxisRightStickUp", "AxisRightStickDown", "AxisRightStickLeft", "AxisRightStickRight"],
        };
}
