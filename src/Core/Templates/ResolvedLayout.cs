using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.Templates;

/// <summary>
/// Output of <see cref="LayoutResolver.Configure"/>: the layout tree after relative
/// coordinates, inherited styles, image filenames, overlay paths, and ShowIf strings have all
/// been resolved into concrete render-ready values, plus a precomputed lookup table derived
/// from the resolved tree (<see cref="CollapseInfo"/> for collapsing-Stack offsets). Consumed by
/// <see cref="TemplateService"/>, which assembles it alongside the base image and template name
/// into an immutable <see cref="Template"/>.
/// </summary>
[ExcludeFromCodeCoverage]
/// <param name="Wholes">Composite controls the template declares, each mapped to the inputs it
/// is made of — see <see cref="WholeNode"/>. Read wherever a name has to be expanded into the
/// controls behind it: a composite's visibility is the union of its parts', and its image is
/// classified by the platform button driving all of them.</param>
public record ResolvedLayout(
    IReadOnlyList<ILayoutElement> Elements,
    IReadOnlyDictionary<InputDefinition, CollapseInfo> CollapseInfo,
    double DefaultFontSize,
    double DefaultMinOpacity,
    double DefaultInactiveBlurRadius,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Wholes = null)
{
    /// <summary>Never null, so a caller never has to ask whether the template declared any.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Wholes { get; init; } =
        Wholes ?? new Dictionary<string, IReadOnlyList<string>>();
}
