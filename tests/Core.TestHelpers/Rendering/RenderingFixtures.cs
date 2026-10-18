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
    public static VisibilityContext Ctx(
        ResolvedMapping? mapping = null,
        IReadOnlyDictionary<string, string>? labelText = null,
        bool isGameSpecific = false)
    {
        return new(
            Mapping: mapping ?? EmptyMapping(),
            LabelText: labelText ?? new Dictionary<string, string>(),
            IsGameSpecific: isGameSpecific);
    }
}
