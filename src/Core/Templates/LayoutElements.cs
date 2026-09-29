using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.Templates;

/// <summary>
/// Marker interface for nodes in the built template tree: InputDefinition (a single generic
/// input), Container (a positioned cluster), and OneOf (a mutually-exclusive alternatives
/// container). Used purely for polymorphic dispatch; structural equality semantics
/// would be on the concrete records, not the interface (the layout pipeline already tracks these
/// nodes by reference identity via ReferenceEqualityComparer).
/// </summary>
public interface ILayoutElement;

/// <summary>
/// A wrapper around a cluster of related inputs (i.e. a &lt;Container&gt;), positioned but never
/// self-gating: unlike the &lt;Group&gt; it replaced, a Container has no implicit "any member
/// visible" check of its own — it's always rendered once <see cref="Rendering.LayoutFilter"/>
/// reaches it. Whoever wraps it (typically an explicit &lt;Condition&gt;) decides whether it
/// exists at all this game; the Container itself only decides where its members sit. Members
/// still handle their own individual visibility via showIf once the Container is reached; when
/// nested inside another Container's stack, one always occupies one slot as an opaque block (see
/// <see cref="Templates.CollapseGroupBuilder"/>) — its own inner traversal is independent.
/// </summary>
/// <param name="Children">Nested layout children — InputDefinition, Container, or OneOf in
/// document order.</param>
/// <param name="Overlays">Overlays declared as direct children of the container. Rendered once
/// whenever the container itself is reached — no per-overlay visibility, and no gating of their
/// own; an enclosing Condition decides whether the container (and so these overlays) exists at
/// all this game. Lets a cluster declare shared overlay artwork (e.g. dpad lines) once rather
/// than repeating it on every member. <see cref="Rendering.LayoutFilter"/> additionally folds in
/// any loose overlays discovered deeper in this container's subtree (see
/// <see cref="OverlayElement"/>) alongside these when it computes the aggregate flags — this
/// field alone is only the static, direct-child set.</param>
/// <param name="DeclaredOriginY">The container's own declared Y, before any <paramref
/// name="VAlign"/> shift — i.e. the value a bare `y` attribute resolved to. Together with
/// <paramref name="Gap"/>/<paramref name="VAlign"/>/<paramref name="Collapse"/>, this is
/// everything <see cref="Rendering.LayoutFilter"/> needs to compute where a loose
/// <c>&lt;Label&gt;</c> placed directly inside this container should sit — the visual center of
/// however many of this container's slots actually survive for the current game, which can only
/// be known at render time (see <see cref="Rendering.LayoutFilter"/>'s label-centering logic).
/// Kept as plain fields directly on this record, rather than sharing the per-Input <see
/// cref="CollapseInfo"/> instance member Inputs are keyed by, so each type stays a single,
/// self-contained concept: this record fully describes itself; <see cref="CollapseInfo"/> stays
/// purely "how does an arbitrary Input find its way back to its enclosing container's shape."</param>
/// <param name="Gap">Vertical spacing between this container's slots. Meaningless unless it
/// actually has slotted children; defaults to 0.</param>
/// <param name="VAlign">Which slot <paramref name="DeclaredOriginY"/> refers to — "top" (default),
/// "bottom", or "center". Already validated/normalized by <see cref="Templates.LayoutResolver"/>.</param>
/// <param name="Collapse">Whether this container's slots vacate when hidden. When false, a loose
/// label's center always uses the full nominal slot count — nothing varies by game, since without
/// collapse slots never vacate, hidden or not.</param>
/// <param name="ForInputName">Name of the Input this Container builds on behalf of, when it has
/// no enclosing Input of its own (see <see cref="Templates.ContainerNode.For"/>) — lets
/// <see cref="Rendering.LayoutFilter"/> resolve which InputDefinition a loose Label placed
/// directly inside should attach to, since the render-time walk otherwise only learns "current
/// input" by actually entering one.</param>
[ExcludeFromCodeCoverage]
public record Container(
    IReadOnlyList<ILayoutElement> Children,
    IReadOnlyList<OverlayDefinition> Overlays,
    double DeclaredOriginY = 0,
    double Gap = 0,
    string VAlign = "top",
    bool Collapse = false,
    string? ForInputName = null) : ILayoutElement;

/// <summary>
/// A mutually-exclusive container: at render time, alternatives are evaluated in document order
/// and only the first one with a visible render (any-render-visible for an InputDefinition, its
/// own explicit check for a Condition) is included in the output. The rest are dropped
/// entirely. Used to express "render X OR render Y, never both" — e.g., a labelled cluster of
/// directional inputs vs. a single labelled stick render at the same screen position.
/// </summary>
/// <param name="Alternatives">The alternative branches in document order. Each is an
/// InputDefinition or ConditionElement; the first whose visibility check passes is rendered.</param>
[ExcludeFromCodeCoverage]
public record OneOf(IReadOnlyList<ILayoutElement> Alternatives) : ILayoutElement;

/// <summary>
/// Gates its <see cref="Children"/> on an explicit boolean check over named generic inputs'
/// label/mapping state, evaluated directly against <see cref="Rendering.VisibilityContext"/> —
/// unlike a structural fold, it never depends on what's actually inside it, so it can gate a
/// subtree on a name that isn't (or isn't only) one of that subtree's own inputs, e.g.
/// distinguishing "all four directions individually labelled" from "some subset labelled" when
/// both leave a label on the same whole-control name. This is also the schema's only mechanism
/// for deciding whether a <see cref="Container"/> exists at all this game, since a Container has
/// no implicit visibility check of its own.
/// </summary>
/// <param name="Mode">Whether <see cref="Names"/> must all match, any one, or none.</param>
/// <param name="Names">The generic input names the condition checks — not necessarily
/// descendants of <see cref="Children"/>.</param>
/// <param name="Match">Whether a name "matches" by having a label or by being mapped.</param>
/// <param name="Children">Rendered only when the condition evaluates true; dropped entirely
/// otherwise, the same as anything else that isn't reached.</param>
[ExcludeFromCodeCoverage]
public record ConditionElement(
    ConditionMode Mode,
    IReadOnlyList<string> Names,
    ConditionMatch Match,
    IReadOnlyList<ILayoutElement> Children) : ILayoutElement;

/// <summary>How a <see cref="ConditionElement"/> combines its <see cref="ConditionElement.Names"/>.</summary>
public enum ConditionMode
{
    /// <summary>True only when every named input matches.</summary>
    All,

    /// <summary>True when at least one named input matches.</summary>
    Any,

    /// <summary>True when no named input matches.</summary>
    None
}

/// <summary>What "matches" means for a single name in a <see cref="ConditionElement"/>.</summary>
public enum ConditionMatch
{
    /// <summary>The name has non-empty label text.</summary>
    Label,

    /// <summary>The name is mapped (a platform button drives it, or its natural button still is).</summary>
    Mapped,

    /// <summary>Label mode when the game contributed its own labels, Mapped mode otherwise —
    /// mirrors <see cref="Rendering.VisibilityFlags.IsVisible"/>'s handling of
    /// <see cref="ShowIfCondition.Auto"/>.</summary>
    Auto
}

/// <summary>
/// A single label render that lives outside its owning Input's own direct children — e.g. nested
/// inside a &lt;Condition&gt; wrapping a &lt;Container&gt;/&lt;OneOf&gt;/&lt;Condition&gt;
/// rather than directly inside an &lt;Input&gt;. Carries no owner reference of its own: the owning
/// InputDefinition can't be baked in at resolve time (it's still being constructed while its own
/// children, including this one, are being built), so <see cref="Rendering.LayoutFilter"/>
/// discovers the owner itself during its own tree walk, the same way <see cref="Label"/>'s
/// coordinates were resolved against an ambient origin at resolve time.
/// </summary>
/// <param name="Label">The resolved label, already positioned against whichever Input's origin
/// was ambient when this label was parsed.</param>
[ExcludeFromCodeCoverage]
public record LabelElement(LabelDefinition Label) : ILayoutElement;

/// <summary>
/// A single overlay image that lives outside its owning Input's or Container's own direct
/// children — e.g. nested inside a &lt;Condition&gt; wrapping a &lt;Container&gt;/&lt;OneOf&gt;/
/// &lt;Condition&gt; rather than directly inside an &lt;Input&gt; or &lt;Container&gt;. Carries no
/// owner reference of its own, for the same reason <see cref="LabelElement"/> doesn't: the owner
/// can't be baked in at resolve time, so <see cref="Rendering.LayoutFilter"/> discovers it during
/// its own tree walk instead — an ambient Input if one is reached, else an ambient Container, else
/// (no owner at all) rendered unconditionally as its own template-level overlay, since there's no
/// fold-in target to gate against; its own ShowIf still governs its own opacity either way, the
/// same as a direct child always has.
/// </summary>
/// <param name="Overlay">The resolved overlay, already positioned against whichever origin was
/// ambient when this overlay was parsed — unlike a loose Label, position never depends on which
/// owner (if any) is later discovered.</param>
[ExcludeFromCodeCoverage]
public record OverlayElement(OverlayDefinition Overlay) : ILayoutElement;

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
/// <param name="InputImages">Where this Input's own image is rendered — always exactly one
/// element, built directly from the Input's own attributes (there is no longer a separate
/// &lt;Render&gt; concept). Identical for every game that uses this template. Once a render pass
/// is underway you have a <see cref="Rendering.LayoutInput"/>, not a bare InputDefinition —
/// prefer its own <see cref="Rendering.LayoutInput.Images"/> there for symmetry with
/// <see cref="Rendering.LayoutInput.Labels"/>, though for images the two are always identical.</param>
/// <param name="Overlays">Overlay images associated with this input (e.g. dotted lines) — this
/// Input's own static set only. Once a render pass is underway, prefer
/// <see cref="Rendering.LayoutInput.Overlays"/> instead, which additionally includes any loose
/// Condition-gated overlays that survived for the current game (see <see cref="OverlayElement"/>);
/// reading this field directly at that point would silently miss those.</param>
/// <param name="Labels">Positions where the label text is rendered — this Input's own static set
/// only. Once a render pass is underway, prefer <see cref="Rendering.LayoutInput.Labels"/>
/// instead, which additionally includes any loose Condition-gated labels that survived for the
/// current game (see <see cref="LabelElement"/>); reading this field directly at that point
/// would silently miss those.</param>
/// <param name="Children">Nested layout elements — Container, OneOf, or Condition in document
/// order (nesting another InputDefinition directly is unusual, and the shipped template no
/// longer does it at all). A whole control's own <c>showIf="auto"</c> fan-out does <b>not</b>
/// come from here — see <see cref="DynamicControls.WholeInputs.PartsOf"/>.</param>
[ExcludeFromCodeCoverage]
public record InputDefinition(
    string Name,
    IReadOnlyList<InputImageDefinition> InputImages,
    IReadOnlyList<OverlayDefinition> Overlays,
    IReadOnlyList<LabelDefinition> Labels,
    IReadOnlyList<ILayoutElement> Children) : ILayoutElement;

/// <summary>
/// Resolved canvas position and dimensions for a single button image render within an
/// InputDefinition. Built by TemplateService from an InputNode's own attributes; consumed by InputImageRenderer.
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