namespace DynamicControls.Templates;

/// <summary>
/// Raw DTO deserialized directly from a controller template's Layout.xml file.
/// Mirrors the XML structure exactly; no path resolution or business logic is applied.
/// Produced by TemplateLoader and consumed by TemplateService,
/// which transforms it into a fully resolved Template.
/// </summary>
public record LayoutConfig
{
    /// <summary>The template's &lt;Head&gt; section — non-display metadata (template-wide visual
    /// defaults via &lt;Style&gt;, plus any future meta blocks). Defaults to an empty head when
    /// the file uses the flat schema (no &lt;Head&gt;/&lt;Body&gt; wrapper) or omits the element.</summary>
    public HeadConfig Head { get; set; } = new();

    /// <summary>The body — display layout elements in document order. Polymorphic — entries are
    /// InputNode, GroupNode, or OneOfNode. When the file uses the flat schema (no
    /// &lt;Body&gt; wrapper), root-level layout children are collected here for compatibility.</summary>
    public List<IConfigNode> Elements { get; set; } = [];
}

/// <summary>
/// Raw DTO for &lt;Head&gt; — template-scoped metadata that isn't display layout. Currently
/// holds an optional &lt;Style&gt; with template-wide visual defaults; future home for other
/// meta blocks. Kept separate from the body so the two concerns stay syntactically distinct
/// within a single Layout.xml file.
/// </summary>
public record HeadConfig
{
    /// <summary>Optional unnamed &lt;Style&gt; — template-wide visual defaults. Null means
    /// use the built-in defaults from RenderingDefaults.</summary>
    public StyleConfig? Style { get; set; }

    /// <summary>Named &lt;Style&gt; elements keyed by name. An Input with `style="X"` inherits
    /// each missing attribute from NamedStyles["X"]. Explicit attributes on the Input always
    /// win over the named-style value.</summary>
    public Dictionary<string, StyleConfig> NamedStyles { get; set; } = [];
}

/// <summary>
/// Raw DTO for &lt;Style&gt;. Each attribute is nullable; absent means "fall through to the next
/// layer." Used in two modes selected by Name: unnamed (template-wide defaults) or named
/// (referenceable bundle applied to Inputs via the `style` attribute).
/// </summary>
public record StyleConfig
{
    /// <summary>Visibility condition applied to the bundle. Layered with Input/Render's own
    /// `showIf` — explicit wins. Not used by the unnamed template-wide defaults form.</summary>
    public string? ShowIf { get; set; }

    /// <summary>Default font size for all labels in this template. Null means use the global
    /// default from RenderingDefaults.</summary>
    public double? FontSize { get; set; }

    /// <summary>Fallback minOpacity for render and overlay elements that don't specify their
    /// own and whose containing Input doesn't either. Null means 0 (hidden).</summary>
    public double? MinOpacity { get; set; }

    /// <summary>Fallback blur radius for inactive render and overlay elements. Null means use
    /// the global default from RenderingDefaults.</summary>
    public double? InactiveBlurRadius { get; set; }
}

/// <summary>
/// A canvas coordinate parsed from a Layout.xml x/y attribute. A plain number is absolute;
/// a number prefixed with + or - is relative to the enclosing container's current slot origin.
/// Resolved to an absolute double by TemplateService before being stored in domain objects.
/// </summary>
public readonly record struct Coordinate(bool IsRelative, double Value)
{
    /// <summary>Constructs an absolute coordinate.</summary>
    public static Coordinate Absolute(double v) => new(false, v);

    /// <summary>Constructs a coordinate offset from an origin.</summary>
    public static Coordinate Relative(double v) => new(true, v);

    /// <summary>Resolves to an absolute canvas value against the given origin.
    /// Relative coordinates add to the origin; absolute coordinates ignore it.</summary>
    public double Resolve(double origin) => IsRelative ? origin + Value : Value;
}

/// <summary>
/// Marker interface for anything that can appear in the parsed Layout.xml element tree —
/// InputNode, GroupNode, StackNode, or OneOfNode. Used purely for polymorphic dispatch.
/// </summary>
public interface IConfigNode;

/// <summary>
/// Raw DTO for a &lt;Group&gt; wrapper around a cluster of inputs. The whole cluster is included
/// in the rendered output whenever any contained input has at least one visible render; inputs
/// in a failing group are semantically excluded (gone from inputsToRender), not just visually
/// faded. The group has no explicit condition attribute — "any member visible" is the rule.
/// </summary>
public record GroupNode : IConfigNode
{
    /// <summary>Nested layout children — Input, Group, Stack, or OneOf in document order. The
    /// group is included whenever any descendant has a visible render, recursing through nested
    /// Groups and the active branch of nested OneOfs.</summary>
    public List<IConfigNode> Children { get; set; } = [];

    /// <summary>Overlays declared at the group level. Each renders once when the group is
    /// included; their visibility is binary (gated by group inclusion, not by per-overlay
    /// conditions). Used to declutter templates where the same overlay would otherwise be
    /// repeated on every input in a cluster.</summary>
    public List<OverlayNode> Overlays { get; set; } = [];
}

/// <summary>
/// Raw DTO for a &lt;Stack&gt; positioned layout container. Unlike &lt;Group&gt;, a Stack is always
/// included in the render output regardless of whether its children have visible renders —
/// children handle their own visibility via showIf. Children are stacked vertically: each Input
/// (at any depth through transparent plain Groups) occupies one slot, with positions computed
/// from the stack origin plus the running slot index times Gap.
/// </summary>
public record StackNode : IConfigNode
{
    /// <summary>Horizontal canvas origin for the stack. Absolute or relative (+ / - prefix). Defaults to +0.</summary>
    public Coordinate X { get; set; } = Coordinate.Relative(0);

    /// <summary>Vertical canvas origin for the stack. Absolute or relative (+ / - prefix). Defaults to +0.</summary>
    public Coordinate Y { get; set; } = Coordinate.Relative(0);

    /// <summary>Vertical spacing between slots in pixels.</summary>
    public double? Gap { get; set; }

    /// <summary>When true, inputs that are fully hidden (all renders have opacity=0) vacate
    /// their slot and subsequent inputs shift up to fill the gap.</summary>
    public bool Collapse { get; set; }

    /// <summary>Nested layout children — Input, Group, Stack, or OneOf in document order.</summary>
    public List<IConfigNode> Children { get; set; } = [];

    /// <summary>Overlays declared at the stack level. Rendered unconditionally whenever the
    /// stack itself renders.</summary>
    public List<OverlayNode> Overlays { get; set; } = [];
}

/// <summary>
/// Raw DTO for a &lt;OneOf&gt; mutually-exclusive alternatives container. The children are
/// evaluated in document order; the first child whose own visibility check passes (any-render-
/// visible for an Input, any-member-visible for a Group) is rendered, and the rest are dropped
/// entirely. Used to express "render X, OR render Y, but not both" without per-element opt-out
/// flags. Can appear anywhere a IConfigNode can: top-level, inside &lt;Input&gt;.Children,
/// or inside &lt;Group&gt; alongside its Inputs.
/// </summary>
public record OneOfNode : IConfigNode
{
    /// <summary>The alternative branches in document order — each is an InputNode or
    /// GroupNode. Only the first whose visibility check passes is rendered.</summary>
    public List<IConfigNode> Alternatives { get; set; } = [];
}

/// <summary>
/// Raw DTO for a single named input within Layout.xml (e.g. ButtonA, AxisLeftStick).
/// Contains unparsed render, overlay, and label child elements as read from XML.
/// Nested within LayoutConfig or GroupNode; consumed by TemplateService
/// when building InputDefinition entries for a Template.
/// </summary>
public record InputNode : IConfigNode
{
    /// <summary>Generic input name (e.g. "ButtonA"). Required.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Optional reference to a named &lt;Style&gt; in &lt;Head&gt;. Each missing visibility
    /// attribute on this Input falls back to the named style's value. Explicit attributes on
    /// the Input override the style.</summary>
    public string? Style { get; set; }

    /// <summary>Default visibility condition applied to all Renders and Overlays nested in this
    /// input that don't set their own `showIf` attribute. Inherited at build time; the domain
    /// model carries the resolved value per render.</summary>
    public string? ShowIf { get; set; }

    /// <summary>Default minOpacity applied to all Renders and Overlays in this input that don't
    /// set their own. Inherited at build time.</summary>
    public double? MinOpacity { get; set; }

    /// <summary>Default inactiveBlurRadius applied to all Renders and Overlays in this input
    /// that don't set their own. Inherited at build time.</summary>
    public double? InactiveBlurRadius { get; set; }

    /// <summary>Default font size for labels in this input that don't set their own fontSize.
    /// Sits between the Label's explicit fontSize and the named style's fontSize in the resolution
    /// chain. Null means fall through to the named style or template default.</summary>
    public double? FontSize { get; set; }

    /// <summary>Optional x origin for this Input as a coordinate container. The Input's own
    /// Renders, Labels, and Overlays resolve their + / - coords against this origin, and nested
    /// child Inputs inherit it as their base origin. Defaults to +0.</summary>
    public Coordinate X { get; set; } = Coordinate.Relative(0);

    /// <summary>Optional y origin for this Input as a coordinate container. See X. Defaults to +0.</summary>
    public Coordinate Y { get; set; } = Coordinate.Relative(0);

    /// <summary>Render child elements defining where the input image is positioned.</summary>
    public List<RenderNode> Renders { get; set; } = [];

    /// <summary>Overlay child elements for associated images (e.g. dotted lines).</summary>
    public List<OverlayNode> Overlays { get; set; } = [];

    /// <summary>Label child elements defining where label text is positioned.</summary>
    public List<LabelNode> Labels { get; set; } = [];

    /// <summary>Nested layout children — either &lt;Input&gt; or &lt;Group&gt; in document order.
    /// Structural nesting is how parent/child relationships are expressed (replaces the old
    /// `childOf` attribute): a parent Input's renders fan out to every InputNode in its
    /// structural descendant set, and a nested input's per-render image fallback resolves
    /// through its structural parent's Name. Strict-self renders are written as a duplicate
    /// top-level &lt;Input&gt; with no nested children — its fan-out scope is empty.</summary>
    public List<IConfigNode> Children { get; set; } = [];
}

/// <summary>
/// Raw DTO for a Render child element in Layout.xml, specifying where a button image is drawn.
/// Nested within InputNode; consumed by TemplateService,
/// which maps it to an InputImageDefinition.
/// </summary>
public record RenderNode
{
    /// <summary>Left position. Absolute or relative (+ / - prefix) to the enclosing container's slot origin. Defaults to +0.</summary>
    public Coordinate X { get; set; } = Coordinate.Relative(0);

    /// <summary>Top position. Absolute or relative (+ / - prefix) to the enclosing container's slot origin. Defaults to +0.</summary>
    public Coordinate Y { get; set; } = Coordinate.Relative(0);

    /// <summary>Render width. NaN means use the image's natural width.</summary>
    public double Width { get; set; } = double.NaN;

    /// <summary>Render height. NaN means use the image's natural height.</summary>
    public double Height { get; set; } = double.NaN;

    /// <summary>Optional name of another input/asset whose image file this render prefers.
    /// Affects image resolution for this render only — does not establish a mapping relationship.
    /// If the referenced file doesn't exist, the resolver falls back through Name then the
    /// structural parent's Name.</summary>
    public string? UseImage { get; set; }

    /// <summary>Show condition: "label", "mapping", "auto", or null to always show.</summary>
    public string? ShowIf { get; set; }

    /// <summary>Opacity when ShowIf conditions are not met. Null means use the template default.</summary>
    public double? MinOpacity { get; set; }

    /// <summary>Blur radius when ShowIf conditions are not met. Null means use the template default.</summary>
    public double? InactiveBlurRadius { get; set; }
}

/// <summary>
/// Raw DTO for an Overlay child element in Layout.xml, specifying an arbitrary image and its canvas position.
/// Nested within InputNode; consumed by TemplateService,
/// which resolves the image path and maps it to an OverlayDefinition.
/// </summary>
public record OverlayNode
{
    /// <summary>
    /// Image filename as written in Layout.xml, passed to TemplateImageResolver to produce a full path.
    /// Resolved in order: Templates/{template}/{platform}/{src}, Templates/{template}/{src}, Templates/{src}.
    /// </summary>
    public string? Src { get; set; }

    /// <summary>Left position. Absolute or relative (+ / - prefix) to the enclosing container's slot origin. Defaults to +0.</summary>
    public Coordinate X { get; set; } = Coordinate.Relative(0);

    /// <summary>Top position. Absolute or relative (+ / - prefix) to the enclosing container's slot origin. Defaults to +0.</summary>
    public Coordinate Y { get; set; } = Coordinate.Relative(0);

    /// <summary>Render width. NaN means use the image's natural width.</summary>
    public double Width { get; set; } = double.NaN;

    /// <summary>Render height. NaN means use the image's natural height.</summary>
    public double Height { get; set; } = double.NaN;

    /// <summary>Show condition: "label", "mapping", "auto", or null to always show.</summary>
    public string? ShowIf { get; set; }

    /// <summary>Opacity when ShowIf conditions are not met. Null means use the template default.</summary>
    public double? MinOpacity { get; set; }

    /// <summary>Blur radius when ShowIf conditions are not met. Null means use the template default.</summary>
    public double? InactiveBlurRadius { get; set; }
}

/// <summary>
/// Raw DTO for a Label child element in Layout.xml, specifying label text position, alignment, and font size.
/// Nested within InputNode; consumed by TemplateService,
/// which maps it to a LabelDefinition.
/// </summary>
public record LabelNode
{
    /// <summary>Left position. Absolute or relative (+ / - prefix) to the enclosing container's slot origin. Defaults to +0.</summary>
    public Coordinate X { get; set; } = Coordinate.Relative(0);

    /// <summary>Top position. Absolute or relative (+ / - prefix) to the enclosing container's slot origin. Defaults to +0.</summary>
    public Coordinate Y { get; set; } = Coordinate.Relative(0);

    /// <summary>Text alignment ("left", "center", "right"). Defaults to "left".</summary>
    public string Align { get; set; } = "left";

    /// <summary>Font size for this label. Null means use the template default.</summary>
    public double? FontSize { get; set; }
}
