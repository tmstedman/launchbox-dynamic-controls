using DynamicControls.Templates;

namespace DynamicControls.Core.TestHelpers.Templates;

/// <summary>
/// Factory helpers for building <see cref="ILayoutElement"/> trees in tests. Use via
/// <c>using static DynamicControls.Core.TestHelpers.Templates.LayoutElements;</c> so the call sites
/// read <c>Input("A", Group(B, C))</c> rather than verbose constructor invocations. All
/// "container" fields default to empty arrays — supply only the children or alternatives that
/// matter to the assertion. For tests that need to set Overlays, InputImages, etc., construct
/// the records directly.
/// </summary>
public static class LayoutElements
{
    /// <summary>An <see cref="InputDefinition"/> with the given name and structural children; all
    /// other collections (InputImages, Overlays, Labels) default to empty.</summary>
    public static InputDefinition Input(string name, params ILayoutElement[] children)
    {
        return new(
            Name: name,
            InputImages: [],
            Overlays: [],
            Labels: [],
            Children: [.. children]);
    }

    /// <summary>An <see cref="InputGroup"/> with the given children; Overlays defaults to empty.</summary>
    public static InputGroup Group(params ILayoutElement[] children)
    {
        return new(
            Children: [.. children],
            Overlays: []);
    }

    /// <summary>A <see cref="OneOf"/> with the given alternatives in document order.</summary>
    public static OneOf OneOf(params ILayoutElement[] alternatives) =>
        new(Alternatives: [.. alternatives]);
}
