using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.Templates;

/// <summary>
/// Output of <see cref="LayoutResolver.Configure"/>: the layout tree after relative
/// coordinates, inherited styles, image filenames, overlay paths, and ShowIf strings have all
/// been resolved into concrete render-ready values, plus precomputed lookup tables derived
/// from the resolved tree (<see cref="InputDescendants"/> for visibility fan-out — a whole
/// control's direction names, per <see cref="DynamicControls.InputMapping.WholeInputs.PartsOf"/>,
/// not the tree's own nesting — and <see cref="CollapseInfo"/> for collapsing-Stack offsets).
/// Consumed by <see cref="TemplateService"/>, which assembles it alongside the base image and
/// template name into an immutable <see cref="Template"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public record ResolvedLayout(
    IReadOnlyList<ILayoutElement> Elements,
    IReadOnlyDictionary<InputDefinition, IReadOnlyList<string>> InputDescendants,
    IReadOnlyDictionary<InputDefinition, CollapseInfo> CollapseInfo,
    double DefaultFontSize,
    double DefaultMinOpacity,
    double DefaultInactiveBlurRadius);
