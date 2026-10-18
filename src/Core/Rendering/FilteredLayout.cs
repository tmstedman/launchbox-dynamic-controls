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
/// <para><see cref="Images"/>/<see cref="Labels"/> are already the full set for this render
/// pass — <see cref="Input"/>'s own static <c>InputImages</c>/<c>Labels</c> concatenated with
/// whatever bare Render/Label elements survived a wrapping Condition/Group/OneOf this game (see
/// <see cref="Templates.RenderElement"/>/<see cref="Templates.LabelElement"/>), merged once here
/// by <see cref="LayoutFilter"/> so renderers never need to know two sources existed. A
/// surrounding Condition only ever decided whether one of the latter exists at all this game —
/// each still carries its own ShowIf governing its own opacity, exactly as if it had been a
/// direct child.</para>
/// </summary>
[ExcludeFromCodeCoverage]
public record LayoutInput(
    InputDefinition Input,
    double YOffset,
    VisibilityFlags Flags,
    IReadOnlyList<InputImageDefinition>? Images = null,
    IReadOnlyList<LabelDefinition>? Labels = null)
{
    public IReadOnlyList<InputImageDefinition> Images { get; init; } = Images ?? [];
    public IReadOnlyList<LabelDefinition> Labels { get; init; } = Labels ?? [];
}

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
