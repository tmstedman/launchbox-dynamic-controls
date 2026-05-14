using System.Diagnostics.CodeAnalysis;
using DynamicControls.Templates;

namespace DynamicControls.Rendering;

/// <summary>
/// Intermediate pipeline DTO between LayoutFilter and InputRenderingService. Holds the inputs
/// selected for this render pass (group visibility and collapse-stack adjustments already
/// applied) and the overlays from included groups.
/// </summary>
[ExcludeFromCodeCoverage]
public record FilteredLayout(
    IReadOnlyList<LayoutInput> Inputs,
    IReadOnlyList<LayoutGroupOverlay> GroupOverlays);

/// <summary>
/// An input selected for rendering, paired with render-specific state that cannot live on
/// InputDefinition because it varies per render pass. YOffset is non-zero only for slots in a
/// collapsing stack where one or more earlier slots vacated — either an InputDefinition with
/// all zero-opacity images, or a OneOf with no visible alternative. Flags are pre-computed
/// here so downstream stages don't recompute them per image.
/// </summary>
[ExcludeFromCodeCoverage]
public record LayoutInput(
    InputDefinition Input,
    double YOffset,
    VisibilityFlags Flags);

/// <summary>
/// A group-level overlay selected for rendering, paired with the group's aggregate visibility
/// flags — OR-reduced across the inputs that actually render in this pass (OneOf nodes
/// contribute only their first visible alternative, matching LayoutFilter's selection rules).
/// The flags drive ShowIf evaluation on the overlay so MinOpacity / InactiveBlurRadius behave
/// the same as on input overlays.
/// </summary>
[ExcludeFromCodeCoverage]
public record LayoutGroupOverlay(
    OverlayDefinition Overlay,
    VisibilityFlags Flags);
