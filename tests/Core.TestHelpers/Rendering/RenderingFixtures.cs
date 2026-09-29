using DynamicControls.InputMapping;
using DynamicControls.Rendering;
using DynamicControls.Templates;
using static DynamicControls.Core.TestHelpers.InputMapping.MappingFixtures;

namespace DynamicControls.Core.TestHelpers.Rendering;

/// <summary>
/// Factory helpers for rendering-pass inputs: <see cref="VisibilityContext"/> and the structural
/// descendants index it consumes. Use via
/// <c>using static DynamicControls.Core.TestHelpers.Rendering.RenderingFixtures;</c>.
/// Cross-subsystem inputs (mapping, labels, template) live in their own subsystem fixture files.
/// </summary>
public static class RenderingFixtures
{
    public static VisibilityContext Ctx(
        ResolvedMapping? mapping = null,
        IReadOnlyDictionary<string, string>? labelText = null,
        bool isGameSpecific = false,
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<string>>? descendants = null)
    {
        return new(
            Mapping: mapping ?? EmptyMapping(),
            LabelText: labelText ?? new Dictionary<string, string>(),
            IsGameSpecific: isGameSpecific,
            InputDescendants: descendants ?? new Dictionary<InputDefinition, IReadOnlyList<string>>());
    }

    /// <summary>Maps each given Input to the descendant *names* GetVisibilityFlags should fold in
    /// for it (a whole control's direction names in production, per WholeInputs.PartsOf — an
    /// arbitrary override here, since these are unit tests of VisibilityEvaluator itself).</summary>
    public static IReadOnlyDictionary<InputDefinition, IReadOnlyList<string>> Descendants(
        params (InputDefinition Input, string[] Names)[] entries)
    {
        var dict = new Dictionary<InputDefinition, IReadOnlyList<string>>(
            ReferenceEqualityComparer.Instance);
        foreach ((InputDefinition input, string[] names) in entries)
            dict[input] = names;
        return dict;
    }
}
