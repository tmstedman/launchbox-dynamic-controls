using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.Templates;

/// <summary>
/// Marker interface for nodes in the built template tree: InputDefinition (a single generic
/// input), InputGroup (a conditional or always-included cluster), and OneOf (a mutually-exclusive
/// alternatives container). Used purely for polymorphic dispatch; structural equality semantics
/// would be on the concrete records, not the interface (the layout pipeline already tracks these
/// nodes by reference identity via ReferenceEqualityComparer).
/// </summary>
public interface ILayoutElement;

/// <summary>
/// A wrapper around a cluster of related inputs (i.e. a &lt;Group&gt; or &lt;Stack&gt;). 
/// When <paramref name="AlwaysInclude"/> is false (a plain &lt;Group&gt;), the entire 
/// group is included only when any contained input has at least one render visible under
/// its own ShowIf — otherwise every member is excluded from inputsToRender (semantic exclusion,
/// not just visual fading). When true (a &lt;Stack&gt;), the group is always included; children 
/// handle their own visibility.
/// </summary>
/// <param name="AlwaysInclude">Set for Stack elements; false for plain Group elements. When
/// true the group renders unconditionally; children's own showIf still controls their
/// individual opacity.</param>
/// <param name="Children">Nested layout children — InputDefinition, InputGroup, or OneOf in
/// document order.</param>
/// <param name="Overlays">Overlays declared at the group level. Rendered once when the group is
/// included; no per-overlay visibility — the group's inclusion does the gating. Lets a cluster
/// declare shared overlay artwork (e.g. dpad lines) once rather than repeating it on every
/// member.</param>
[ExcludeFromCodeCoverage]
public record InputGroup(
    bool AlwaysInclude,
    IReadOnlyList<ILayoutElement> Children,
    IReadOnlyList<OverlayDefinition> Overlays) : ILayoutElement;

/// <summary>
/// A mutually-exclusive container: at render time, alternatives are evaluated in document order
/// and only the first one with a visible render (any-render-visible for an InputDefinition,
/// any-member-visible for an InputGroup) is included in the output. The rest are dropped
/// entirely. Used to express "render X OR render Y, never both" — e.g., a labelled cluster of
/// directional inputs vs. a single labelled stick render at the same screen position.
/// </summary>
/// <param name="Alternatives">The alternative branches in document order. Each is an
/// InputDefinition or InputGroup; the first whose visibility check passes is rendered.</param>
[ExcludeFromCodeCoverage]
public record OneOf(IReadOnlyList<ILayoutElement> Alternatives) : ILayoutElement;

/// <summary>
/// Fully resolved layout data for a single generic input within a Template.
/// Built by TemplateService from InputNode; all positions are resolved at build time.
/// Image paths are deferred to render time via InputImageResolver.
/// Stored in Template.Elements and consumed by InputRenderingService,
/// which passes individual definitions to InputImageRenderer, OverlayRenderer, and InputLabelRenderer.
/// Collapse metadata lives on <see cref="ResolvedLayout.CollapseInfo"/>, keyed by reference
/// identity, rather than as fields on this type.
/// </summary>
/// <param name="Name">Generic input name (e.g. "ButtonA").</param>
/// <param name="InputImages">Positions where the button image is rendered.</param>
/// <param name="Overlays">Overlay images associated with this input (e.g. dotted lines).</param>
/// <param name="Labels">Positions where the label text is rendered.</param>
/// <param name="Children">Nested layout elements — either InputDefinitions or InputGroups in
/// document order. Structural nesting is how parent/child relationships are expressed: a parent
/// Input's renders fan out to every InputDefinition in its structural descendant set
/// (recursive, through nested Inputs and Groups), and each descendant's image fallback resolves
/// through its structural parent's Name. A duplicate top-level Input with an empty Children
/// list expresses a strict-self render position (no fan-out).</param>
[ExcludeFromCodeCoverage]
public record InputDefinition(
    string Name,
    IReadOnlyList<InputImageDefinition> InputImages,
    IReadOnlyList<OverlayDefinition> Overlays,
    IReadOnlyList<LabelDefinition> Labels,
    IReadOnlyList<ILayoutElement> Children) : ILayoutElement;

/// <summary>
/// Resolved canvas position and dimensions for a single button image render within an
/// InputDefinition. Built by TemplateService from a RenderNode; consumed by InputImageRenderer.
/// </summary>
/// <param name="X">Left position in template canvas coordinates.</param>
/// <param name="Y">Top position in template canvas coordinates.</param>
/// <param name="ImageFile">Base image filename for this render (e.g. "ButtonA.png"), derived
/// from the input's Name. Resolved to a path at render time against the active platform and
/// controller via InputImageResolver.</param>
/// <param name="Width">Render width. NaN means use the image's natural width.</param>
/// <param name="Height">Render height. NaN means use the image's natural height.</param>
/// <param name="UseImageFile">Explicit image filename override from `useImage` (e.g.
/// "Stick.png"), or null if the render uses the input's own image. When non-null, the image
/// resolver tries this file first (preferring any platform-specific variant), then falls back
/// to ImageFile. Also drives asset-borrowing semantics: a borrowed asset gets its
/// platform-specific variant even when the owning input isn't mapped, an own-identity render
/// does not.</param>
/// <param name="ShowIf">Conditions under which this element is shown. Always shown if no flags
/// are set.</param>
/// <param name="MinOpacity">Opacity when ShowIf conditions are not met. Null means fall back to
/// the template default. 0 means hidden.</param>
/// <param name="InactiveBlurRadius">Blur radius when ShowIf conditions are not met. Null means
/// fall back to the template default.</param>
[ExcludeFromCodeCoverage]
public record InputImageDefinition(
    double X,
    double Y,
    string ImageFile,
    double Width = double.NaN,
    double Height = double.NaN,
    string? UseImageFile = null,
    ShowIfCondition ShowIf = ShowIfCondition.Always,
    double? MinOpacity = null,
    double? InactiveBlurRadius = null);

/// <summary>
/// Resolved canvas position, dimensions, and image path for a single overlay within an
/// InputDefinition. Built by TemplateService from an OverlayNode with the image path fully
/// resolved; consumed by OverlayRenderer.
/// </summary>
/// <param name="X">Left position in template canvas coordinates.</param>
/// <param name="Y">Top position in template canvas coordinates.</param>
/// <param name="Source">Full resolved path to the overlay image file.</param>
/// <param name="Width">Render width. NaN means use the image's natural width.</param>
/// <param name="Height">Render height. NaN means use the image's natural height.</param>
/// <param name="ShowIf">Conditions under which this element is shown. Always shown if no flags
/// are set.</param>
/// <param name="MinOpacity">Opacity when ShowIf conditions are not met. Null means fall back to
/// the template default. 0 means hidden.</param>
/// <param name="InactiveBlurRadius">Blur radius when ShowIf conditions are not met. Null means
/// fall back to the template default.</param>
[ExcludeFromCodeCoverage]
public record OverlayDefinition(
    double X,
    double Y,
    string Source,
    double Width = double.NaN,
    double Height = double.NaN,
    ShowIfCondition ShowIf = ShowIfCondition.Always,
    double? MinOpacity = null,
    double? InactiveBlurRadius = null);

/// <summary>
/// Resolved canvas position, alignment, and font size for label text within an InputDefinition.
/// Built by TemplateService from a LabelNode; consumed by InputLabelRenderer.
/// </summary>
/// <param name="X">Left position in template canvas coordinates.</param>
/// <param name="Y">Top position in template canvas coordinates.</param>
/// <param name="Alignment">Text alignment ("left", "center", "right").</param>
/// <param name="FontSize">Font size for this label. NaN means use the template default.</param>
[ExcludeFromCodeCoverage]
public record LabelDefinition(
    double X,
    double Y,
    string? Alignment = null,
    double FontSize = double.NaN);

/// <summary>
/// Controls when an image or overlay is shown. Parsed from the showIf attribute in Layout.xml.
/// </summary>
public enum ShowIfCondition
{
    /// <summary>Always shown.</summary>
    Always,

    /// <summary>Show only when the input has a label.</summary>
    Label,

    /// <summary>Show only when the input has a platform mapping.</summary>
    Mapped,

    /// <summary>Show by label when the game has its own labels XML; show by mapping otherwise.
    /// Purely-inherited platform defaults don't count — an empty or missing game-specific labels
    /// file falls through to mapping-mode.</summary>
    Auto
}