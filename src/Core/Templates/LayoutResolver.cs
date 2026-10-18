namespace DynamicControls.Templates;

/// <summary>
/// Converts a <see cref="LayoutDocument"/> into a <see cref="ResolvedLayout"/>: resolves style
/// defaults, builds the element tree with absolute coordinates and resolved styles, and
/// precomputes the <c>CollapseInfo</c> lookup table. Pure transformation — no file I/O, no
/// caching.
/// </summary>
public interface ILayoutResolver
{
    /// <summary>
    /// Resolves style defaults from <paramref name="config"/>, builds the element tree, and
    /// precomputes the collapse-info map from the resolved tree.
    /// </summary>
    ResolvedLayout Resolve(LayoutDocument config, ITemplateImageSource imageSource);
}

/// <summary>
/// Production implementation: collapse-info accumulation runs inline through a shared
/// dictionary threaded by <c>BuildContext</c>.
/// </summary>
public class LayoutResolver(ILogger logger) : ILayoutResolver
{
    private readonly ILogger _logger = logger;

    /// <inheritdoc />
    public ResolvedLayout Resolve(
        LayoutDocument config,
        ITemplateImageSource imageSource)
    {
        StyleNode? style = config.Head.Style;
        double defaultFontSize = style?.FontSize ?? RenderingDefaults.FontSize;
        double defaultMinOpacity = style?.MinOpacity ?? 0;
        double defaultInactiveBlurRadius = style?.InactiveBlurRadius ?? RenderingDefaults.InactiveBlurRadius;

        var collapseInfo = new Dictionary<InputDefinition, CollapseInfo>(ReferenceEqualityComparer.Instance);
        var ctx = new BuildContext(
            ImageSource: imageSource,
            NamedStyles: config.Head.NamedStyles,
            CollapseInfo: collapseInfo,
            Style: new ComputedStyle(ShowIf: null, MinOpacity: null, InactiveBlurRadius: null, FontSize: defaultFontSize));

        var elements = config.Elements.Select(e => BuildNode(e, ctx)).ToList();
        return new ResolvedLayout(
            Elements: elements,
            CollapseInfo: collapseInfo,
            DefaultFontSize: defaultFontSize,
            DefaultMinOpacity: defaultMinOpacity,
            DefaultInactiveBlurRadius: defaultInactiveBlurRadius);
    }

    private ILayoutElement BuildNode(ILayoutNode node, BuildContext ctx) => node switch
    {
        InputNode inputXml => BuildInputDefinition(inputXml, ctx),
        ContainerNode containerXml => BuildContainer(containerXml, ctx),
        OneOfNode oneOfXml => BuildOneOf(oneOfXml, ctx),
        ConditionNode conditionXml => BuildCondition(conditionXml, ctx),
        LabelNode labelXml => BuildLooseLabel(labelXml, ctx),
        OverlayNode overlayXml => BuildLooseOverlay(overlayXml, ctx),
        _ => throw new InvalidOperationException($"Unknown node type: {node.GetType()}")
    };

    /// <summary>
    /// Resolves an InputNode DTO into a fully-built InputDefinition: its own image, labels,
    /// overlays, and nested children are all resolved against the template's coordinate origin.
    /// </summary>
    private InputDefinition BuildInputDefinition(InputNode inputXml, BuildContext ctx)
    {
        string name = inputXml.Name;

        // Merges the Input's own style attributes over its named style (if any) over whatever
        // was already ambient — see ResolveStyle. An Input's own attributes always win; absent
        // ones fall through the same chain a Label/Overlay reachable from it would.
        ComputedStyle computed = Resolve(inputXml, ctx, $"Input '{name}'");

        // Resolve this Input's own optional x/y — if present they establish a new coordinate
        // origin for its own image, Labels, Overlays, and nested Children. Absent = inherit
        // ctx.OriginX/Y (which may be a stack slot position or the canvas origin).
        double inputOriginX = inputXml.X.Resolve(ctx.OriginX);
        double inputOriginY = inputXml.Y.Resolve(ctx.OriginY);

        // Build a context carrying this input's computed style and identity. Labels and overlays
        // on this input read from inputCtx; nested children also receive inputCtx (rather than a
        // fresh ctx) so a *loose* Label reached through Container/OneOf/Condition can inherit it
        // exactly like a true direct child would — a nested <Input> is unaffected, since it
        // always overrides Style from its own ShowIf/Style/FontSize rather than falling through
        // to whatever's ambient.
        BuildContext inputCtx = ctx with
        {
            Style = computed,
            OriginX = inputOriginX,
            OriginY = inputOriginY,
            CurrentInputName = name
        };

        var labels = inputXml.Labels.Select(labelXml => BuildLabelDefinition(labelXml, inputCtx)).ToList();

        ShowIfCondition showIf = ParseShowIf(computed.ShowIf);
        var image = new InputImageDefinition(
            X: inputOriginX,
            Y: inputOriginY,
            ImageFile: $"{name}.png",
            Width: inputXml.Width,
            Height: inputXml.Height,
            UseImageFile: inputXml.UseImage != null ? $"{inputXml.UseImage}.png" : null,
            ShowIf: showIf,
            MinOpacity: computed.MinOpacity,
            InactiveBlurRadius: computed.InactiveBlurRadius);
        _logger.Debug($"Input image: {name} at ({inputOriginX},{inputOriginY}) showIf={showIf}");

        var overlays = new List<OverlayDefinition>();
        foreach (OverlayNode overlayXml in inputXml.Overlays)
        {
            if (overlayXml.Src == null) continue;
            overlays.Add(BuildOverlayDefinition(overlayXml, inputCtx));
        }

        var children = inputXml.Children
            .Select(c => BuildNode(c, inputCtx))
            .ToList();

        return new InputDefinition(
            Name: name,
            InputImages: [image],
            Overlays: overlays,
            Labels: labels,
            Children: children);
    }

    /// <summary>Resolves a ContainerNode DTO into a Container. Unlike the old &lt;Group&gt; it
    /// replaced, a Container never decides its own inclusion at render time — it's always
    /// rendered once <see cref="Rendering.LayoutFilter"/> reaches it; an explicit wrapping
    /// &lt;Condition&gt; is how a template author gates it now. This method only establishes a
    /// canvas origin and stacks children vertically: each Input (at any depth through transparent
    /// Conditions) consumes one slot, advancing the y position by Gap. <c>VAlign</c> shifts
    /// that origin up front so the declared Y lands on the first, last, or middle slot rather
    /// than always being the first — using the template's fixed slot count, since this runs once
    /// at template-load time. When the container also collapses, that fixed-count shift is
    /// corrected at render time (see <see cref="Rendering.LayoutFilter"/>) against whatever slot
    /// count is actually left, using the same <see cref="StackVAlign.Shift"/> math with the
    /// vAlign this method already validated — carried through <see cref="CollapseInfo"/> so a
    /// bad value is only ever logged once, here.
    ///
    /// <para>The container's own shape — declared Y (before the shift above), Gap, VAlign, and
    /// whether it collapses — is attached directly to the returned <see cref="Container"/>
    /// (rather than threaded through <see cref="BuildContext"/>) so <see
    /// cref="Rendering.LayoutFilter"/> can compute where a loose <c>&lt;Label&gt;</c> placed
    /// directly inside this container should sit: the visual center of however many of this
    /// container's slots actually survive for the current game, which is a per-game fact this
    /// build-time pass has no way to know — see <see cref="Container"/>'s own doc comment.</para>
    ///
    /// <para><c>containerXml.For</c> is unrelated to any of the above — it overrides
    /// <c>ctx.CurrentInputName</c> for this Container's own children, for a Container reached
    /// with no enclosing Input at all to have supplied one the ordinary way (see
    /// <see cref="ContainerNode.For"/>).</para>
    /// </summary>
    private Container BuildContainer(ContainerNode containerXml, BuildContext ctx)
    {
        double gap = containerXml.Gap ?? 0;
        int slotCount = CountSlots(containerXml.Children);
        string vAlign = NormalizeVAlign(containerXml.VAlign);
        double vAlignShift = StackVAlign.Shift(vAlign, slotCount, gap);
        double declaredOriginY = containerXml.Y.Resolve(ctx.OriginY);

        // Merges the Container's own style attributes the same way an Input's do (see
        // ResolveStyle), so a cluster can declare style="…" once instead of repeating it on
        // every member Input — but cascadeAmbient: false, so an empty Container doesn't pass a
        // wrapping Input's own showIf/minOpacity/inactiveBlurRadius through to members several
        // levels inside it (see ResolveStyle's own doc comment for why this is the
        // Container-specific exception, not Input's).
        ComputedStyle computed = Resolve(containerXml, ctx, "Container", cascadeAmbient: false);

        var frame = new StackFrame
        {
            OriginX = containerXml.X.Resolve(ctx.OriginX),
            OriginY = declaredOriginY - vAlignShift,
            Gap = gap,
            SlotIndex = 0,
        };
        // containerXml.For overrides CurrentInputName unconditionally when set -- it's only ever
        // set when this Container has no enclosing Input of its own to have supplied one (a
        // top-level Container inside a OneOf sibling of the whole it describes). Lets a loose
        // Label placed directly inside still find its whole with no enclosing Input to attach to.
        BuildContext containerCtx = ctx with
        {
            Style = computed,
            OriginX = frame.OriginX,
            OriginY = frame.OriginY,
            CurrentInputName = containerXml.For ?? ctx.CurrentInputName,
        };

        var children = new List<ILayoutElement>();
        foreach (ILayoutNode child in containerXml.Children)
        {
            children.Add(BuildNodeInStack(child, frame, containerCtx));
        }

        var container = new Container(
            Children: children,
            Overlays: [.. containerXml.Overlays
                .Where(o => o.Src != null)
                .Select(o => BuildOverlayDefinition(o, containerCtx))],
            DeclaredOriginY: declaredOriginY,
            Gap: gap,
            VAlign: vAlign,
            Collapse: containerXml.Collapse,
            ForInputName: containerXml.For);

        if (containerXml.Collapse)
            CollapseGroupBuilder.Build(children, frame.Gap, ctx.CollapseInfo, vAlign);

        _logger.Debug($"Container (at {frame.OriginX},{frame.OriginY} gap={frame.Gap} vAlign={vAlign} collapse={containerXml.Collapse}): children={container.Children.Count}, overlays={container.Overlays.Count}");
        return container;
    }

    /// <summary>
    /// Counts the slots <paramref name="nodes"/> will consume once built, mirroring
    /// <see cref="BuildNodeInStack"/>'s own slot rule exactly: Input/Container/OneOf each consume
    /// one slot, and a Condition is transparent, contributing its children's slots instead of
    /// one of its own. Computed ahead of the build so <see cref="StackVAlign.Shift"/> can shift
    /// the origin before the first slot is actually consumed.
    /// </summary>
    private static int CountSlots(IReadOnlyList<ILayoutNode> nodes) => nodes.Sum(CountSlots);

    private static int CountSlots(ILayoutNode node) => node switch
    {
        InputNode => 1,
        ContainerNode => 1,
        OneOfNode => 1,
        ConditionNode conditionXml => CountSlots(conditionXml.Children),
        LabelNode => 0,
        OverlayNode => 0,
        _ => throw new InvalidOperationException($"Unknown node type: {node.GetType()}")
    };

    /// <summary>
    /// Validates a Container's <c>vAlign</c> against the values <see cref="StackVAlign.Shift"/>
    /// recognizes. An unrecognized value is logged and replaced with "top" — the only point in
    /// the pipeline this is ever checked, since render-time re-use of the value (for a
    /// collapsing container's correction) trusts whatever this method already normalized.
    /// </summary>
    private string NormalizeVAlign(string vAlign) => vAlign switch
    {
        "top" or "bottom" or "center" => vAlign,
        _ => LogUnknownVAlign(vAlign)
    };

    private string LogUnknownVAlign(string value)
    {
        _logger.Error($"Unknown vAlign value: \"{value}\". Expected: top, bottom, center. Defaulting to top.");
        return "top";
    }

    /// <summary>
    /// Builds one layout node within a positioned container's slot loop. Inputs consume one slot
    /// each and advance the frame's SlotIndex. Nested Containers and OneOfs consume one slot as a
    /// block and own their own inner traversal; Conditions are transparent — their children each
    /// advance the same counter.
    /// </summary>
    private ILayoutElement BuildNodeInStack(
        ILayoutNode node,
        StackFrame frame,
        BuildContext ctx)
    {
        switch (node)
        {
            case InputNode inputXml:
            {
                (double sx, double sy) = ConsumeSlot(frame);
                return BuildInputDefinition(inputXml, ctx with { OriginX = sx, OriginY = sy });
            }
            case ContainerNode containerXml:
            {
                (double sx, double sy) = ConsumeSlot(frame);
                return BuildContainer(containerXml, ctx with { OriginX = sx, OriginY = sy });
            }
            case OneOfNode oneOfXml:
            {
                // OneOf: consumes one slot; all alternatives share that slot's origin.
                (double sx, double sy) = ConsumeSlot(frame);
                return BuildOneOf(oneOfXml, ctx with { OriginX = sx, OriginY = sy });
            }
            case ConditionNode conditionXml:
            {
                // Transparent: its children each advance the same counter.
                var children = new List<ILayoutElement>();
                foreach (ILayoutNode child in conditionXml.Children)
                {
                    children.Add(BuildNodeInStack(child, frame, ctx));
                }
                return BuildConditionElement(conditionXml, children);
            }
            case LabelNode labelXml:
                // Takes no slot, same as an Overlay.
                return BuildLooseLabel(labelXml, ctx);
            case OverlayNode overlayXml:
                // Takes no slot, same as a Label.
                return BuildLooseOverlay(overlayXml, ctx);
            default:
                throw new InvalidOperationException($"Unknown node type: {node.GetType()}");
        }
    }

    /// <summary>Computes the canvas origin for the frame's current slot and advances its
    /// SlotIndex.</summary>
    private static (double x, double y) ConsumeSlot(StackFrame frame) =>
        (frame.OriginX, frame.OriginY + (frame.SlotIndex++ * frame.Gap));

    /// <summary>Resolves a OneOfNode DTO into a OneOf, recursively building each
    /// alternative branch (Input, Condition, or nested OneOf).</summary>
    private OneOf BuildOneOf(OneOfNode oneOfXml, BuildContext ctx)
    {
        var oneOf = new OneOf(
            Alternatives: [.. oneOfXml.Alternatives.Select(a => BuildNode(a, ctx))]);
        _logger.Debug($"OneOf: alternatives={oneOf.Alternatives.Count}");
        return oneOf;
    }

    /// <summary>Resolves a ConditionNode DTO into a ConditionElement, recursively building each
    /// child (Input, Container, OneOf, or nested Condition).</summary>
    private ConditionElement BuildCondition(ConditionNode conditionXml, BuildContext ctx) =>
        BuildConditionElement(conditionXml, [.. conditionXml.Children.Select(c => BuildNode(c, ctx))]);

    /// <summary>Shared by both build paths (top-level/nested and in-stack) — parses the
    /// mode/names/match attributes once the children are already built.</summary>
    private ConditionElement BuildConditionElement(ConditionNode conditionXml, List<ILayoutElement> children)
    {
        (ConditionMode mode, string? names) = conditionXml switch
        {
            { All: { } all } => (ConditionMode.All, all),
            { None: { } none } => (ConditionMode.None, none),
            _ => (ConditionMode.Any, conditionXml.Any)
        };

        ConditionMatch match = ParseConditionMatch(conditionXml.Match);
        var condition = new ConditionElement(
            Mode: mode,
            Names: names?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [],
            Match: match,
            Children: children);
        _logger.Debug($"Condition: mode={mode}, names=[{string.Join(",", condition.Names)}], match={match}, children={children.Count}");
        return condition;
    }

    private ConditionMatch ParseConditionMatch(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "label" => ConditionMatch.Label,
            "mapping" => ConditionMatch.Mapped,
            _ => LogUnknownConditionMatch(value)
        };
    }

    private ConditionMatch LogUnknownConditionMatch(string value)
    {
        _logger.Error($"Unknown Condition match value: \"{value}\". Expected: label, mapping. Defaulting to label.");
        return ConditionMatch.Label;
    }

    /// <summary>
    /// Resolves a LabelNode DTO into a LabelDefinition. Shared by
    /// <see cref="BuildInputDefinition"/>'s own direct-child loop and <see cref="BuildLooseLabel"/>.
    /// Y always resolves against the plain ambient origin — a loose Label centering against its
    /// enclosing Container's declared position is a per-game fact (which of the container's
    /// slots actually survive) this build-time pass has no way to know, so it's computed
    /// entirely by <see cref="Rendering.LayoutFilter"/> at render time instead, from the
    /// container's own shape (see <see cref="Container"/>'s doc comment); this method never
    /// needs to special-case it.
    /// </summary>
    private LabelDefinition BuildLabelDefinition(LabelNode labelXml, BuildContext ctx)
    {
        // A leaf: reached only through its own concretely-typed caller, never generic dispatch,
        // so it calls the shared merge directly with its own fields rather than implementing
        // IStyledNode — it has no showIf/minOpacity/inactiveBlurRadius of its own (see
        // IStyledNode's doc comment for why), only style (for fontSize) and fontSize itself.
        ComputedStyle computed = ResolveStyle(
            labelXml.Style, showIf: null, minOpacity: null, inactiveBlurRadius: null, labelXml.FontSize,
            ctx, "Label");
        var label = new LabelDefinition(
            X: labelXml.X.Resolve(ctx.OriginX),
            Y: labelXml.Y.Resolve(ctx.OriginY),
            Alignment: labelXml.Align,
            FontSize: computed.FontSize);
        _logger.Debug($"Label position: {ctx.CurrentInputName} at ({labelXml.X},{labelXml.Y}) align={labelXml.Align} fontSize={label.FontSize}");
        return label;
    }

    /// <summary>
    /// Resolves a &lt;Label&gt; found somewhere other than as a direct child of its own
    /// &lt;Input&gt; (e.g. nested inside a &lt;Condition&gt; wrapping a &lt;Container&gt;/
    /// &lt;OneOf&gt;/&lt;Condition&gt;) against whichever Input is ambient in <paramref name="ctx"/>.
    /// A missing ambient Input is a template-authoring error — logged once, here, at load time,
    /// same as a missing required attribute elsewhere in this file — rather than silently
    /// rendering nothing or throwing.
    /// </summary>
    private LabelElement BuildLooseLabel(LabelNode labelXml, BuildContext ctx)
    {
        if (ctx.CurrentInputName is null)
        {
            _logger.Error("Skipping <Label>: not nested inside any <Input>, directly or ambiently");
            return new LabelElement(new LabelDefinition(X: 0, Y: 0));
        }
        return new LabelElement(BuildLabelDefinition(labelXml, ctx));
    }

    /// <summary>
    /// Resolves an &lt;Overlay&gt; found somewhere other than as a direct child of its own
    /// &lt;Input&gt;/&lt;Container&gt; (e.g. nested inside a &lt;Condition&gt; wrapping a
    /// &lt;Container&gt;/&lt;OneOf&gt;/&lt;Condition&gt;, or a bare top-level child of
    /// &lt;Body&gt;). Unlike a loose Label, position never depends on an ambient owner — X/Y still
    /// resolve against whatever origin is ambient in <paramref name="ctx"/> regardless, so there's
    /// no missing-owner error case to guard here. Which owner (an Input, a Container, or none at
    /// all) governs its visibility is a per-game fact <see cref="Rendering.LayoutFilter"/>
    /// discovers during its own walk instead — this method only builds the definition itself.
    /// </summary>
    private OverlayElement BuildLooseOverlay(OverlayNode overlayXml, BuildContext ctx)
    {
        if (overlayXml.Src == null)
        {
            _logger.Error("Skipping <Overlay>: missing 'src' attribute");
            return new OverlayElement(new OverlayDefinition(X: 0, Y: 0, Source: ""));
        }
        return new OverlayElement(BuildOverlayDefinition(overlayXml, ctx));
    }

    /// <summary>
    /// Resolves an OverlayNode DTO into an OverlayDefinition. Style values flow in via
    /// <paramref name="ctx"/> — an input-level overlay's caller passes inputCtx (whose Style is
    /// that Input's own computed style); a container-level overlay's caller passes containerCtx
    /// (whose Style is the Container's own) — both now genuinely populated, not always null the
    /// way a container-level overlay's used to be before Container (né Group) could originate
    /// its own style.
    /// </summary>
    private OverlayDefinition BuildOverlayDefinition(OverlayNode overlayXml, BuildContext ctx)
    {
        (string resolvedPath, _) = ctx.ImageSource.Resolve(overlayXml.Src!, platform: null);

        // A leaf, like Label — calls the shared merge directly rather than implementing
        // IStyledNode, since it has no fontSize of its own (it never renders text).
        ComputedStyle computed = ResolveStyle(
            overlayXml.Style, overlayXml.ShowIf, overlayXml.MinOpacity, overlayXml.InactiveBlurRadius,
            fontSize: null, ctx, $"Overlay src=\"{overlayXml.Src}\"");
        ShowIfCondition showIf = ParseShowIf(computed.ShowIf);
        _logger.Debug($"Overlay: {overlayXml.Src} at ({overlayXml.X},{overlayXml.Y}) showIf={showIf}");
        return new OverlayDefinition(
            X: overlayXml.X.Resolve(ctx.OriginX),
            Y: overlayXml.Y.Resolve(ctx.OriginY),
            Source: resolvedPath,
            Width: overlayXml.Width,
            Height: overlayXml.Height,
            ShowIf: showIf,
            MinOpacity: computed.MinOpacity,
            InactiveBlurRadius: computed.InactiveBlurRadius);
    }

    /// <summary>
    /// Merges an <see cref="IStyledNode"/>'s own style attributes over its named style (if any)
    /// over whatever <see cref="ComputedStyle"/> is already ambient in <paramref name="ctx"/> —
    /// the shared implementation <see cref="ResolveStyle"/> uses for every element. Only
    /// <see cref="InputNode"/> and <see cref="ContainerNode"/> ever need this overload, since only
    /// they're ever treated polymorphically as a cascade origin (see <see cref="IStyledNode"/>'s
    /// doc comment); a leaf calls <see cref="ResolveStyle"/> directly with its own fields.
    /// </summary>
    private ComputedStyle Resolve(IStyledNode node, BuildContext ctx, string errorContext, bool cascadeAmbient = true) =>
        ResolveStyle(node.Style, node.ShowIf, node.MinOpacity, node.InactiveBlurRadius, node.FontSize, ctx, errorContext, cascadeAmbient);

    /// <summary>
    /// Resolves the named style reference (if any), logging once if it names a style that isn't
    /// declared in &lt;Head&gt;, then merges each attribute in precedence order: this element's
    /// own explicit value, then the named style's, then — for <c>showIf</c>/<c>minOpacity</c>/
    /// <c>inactiveBlurRadius</c>, only when <paramref name="cascadeAmbient"/> is true — whatever
    /// was already ambient in <paramref name="ctx"/>.
    ///
    /// <para><paramref name="cascadeAmbient"/> is false only for <see cref="ContainerNode"/>
    /// (see <see cref="BuildContainer"/>). A Container exists specifically so a cluster can
    /// declare style once for its own members — <see cref="BuildInputDefinition"/> passes true
    /// (the default) for an Input's own resolution, so a member Input that sets nothing of its
    /// own still sees the Container's own explicit value via <c>ctx.Style</c>. But an Input like
    /// <c>AxisLeftStick</c> that wraps such a Container *itself* has real visual attributes (its
    /// own <c>auto-blur</c>, say) that are only meant to govern its own glyph, not this
    /// Container's members several levels inside it. If the Container's own resolution let that
    /// ambient value flow through unchanged whenever the Container itself set nothing, a style
    /// like <c>small-label-vacate</c> — meant to vacate to the built-in default (opacity 0) when
    /// inactive — would instead inherit the wrapping Input's own <c>minOpacity</c>, fading
    /// instead of vanishing. Stopping the cascade at an empty Container (rather than letting it
    /// reach through to whatever's further out) fixes that without touching Input's own
    /// resolution at all: a Container's *explicit* value still reaches its members exactly as
    /// intended; only a Container with nothing of its own stops being a pass-through for someone
    /// else's ambient value.</para>
    ///
    /// <para><c>ShowIf</c> stays a raw string through this chain (like <see cref="StyleNode"/>'s
    /// own field) rather than a parsed <see cref="ShowIfCondition"/>, so an absent value can keep
    /// falling through without needing a sentinel distinct from a real parsed value — callers
    /// parse it via <see cref="ParseShowIf"/> once they have the final merged string.
    /// <c>MinOpacity</c>/<c>InactiveBlurRadius</c> stay nullable all the way through for the same
    /// reason they always have: null reaching <see cref="InputImageDefinition"/>/
    /// <see cref="OverlayDefinition"/> means "nothing in the tree set this," resolved against the
    /// template default later, at evaluation time, not baked in here. <c>FontSize</c> is the one
    /// exception to both of the above — it always falls through regardless of
    /// <paramref name="cascadeAmbient"/> (a Container's own members still need to reach a
    /// template-wide font size default through an empty Container), and its template-default tier is already
    /// baked into the root <see cref="BuildContext.Style"/>, set once at the top of the outer
    /// <c>Resolve</c> method, so it's always non-null by the time anything reads it.</para>
    /// </summary>
    private ComputedStyle ResolveStyle(
        string? style, string? showIf, double? minOpacity, double? inactiveBlurRadius, double? fontSize,
        BuildContext ctx, string errorContext, bool cascadeAmbient = true)
    {
        StyleNode? namedStyle = null;
        if (style != null && !ctx.NamedStyles.TryGetValue(style, out namedStyle))
            _logger.Error($"{errorContext} references unknown style '{style}'");

        return new ComputedStyle(
            ShowIf: showIf ?? namedStyle?.ShowIf ?? (cascadeAmbient ? ctx.Style.ShowIf : null),
            MinOpacity: minOpacity ?? namedStyle?.MinOpacity ?? (cascadeAmbient ? ctx.Style.MinOpacity : null),
            InactiveBlurRadius: inactiveBlurRadius ?? namedStyle?.InactiveBlurRadius ?? (cascadeAmbient ? ctx.Style.InactiveBlurRadius : null),
            FontSize: fontSize ?? namedStyle?.FontSize ?? ctx.Style.FontSize);
    }

    private ShowIfCondition ParseShowIf(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            null or "" => ShowIfCondition.Always,
            "label" => ShowIfCondition.Label,
            "mapping" => ShowIfCondition.Mapped,
            "auto" => ShowIfCondition.Auto,
            _ => LogUnknownShowIf(value)
        };
    }

    private ShowIfCondition LogUnknownShowIf(string value)
    {
        _logger.Error($"Unknown showIf value: \"{value}\". Expected: label, mapping, auto. Defaulting to always.");
        return ShowIfCondition.Always;
    }

    /// <summary>
    /// Context for the template tree walk. Carries the per-build inputs that are constant across
    /// all Build* calls (ImageSource, NamedStyles, CollapseInfo accumulator) alongside the
    /// <see cref="ComputedStyle"/> currently ambient — the style cascade's whole state at this
    /// point in the tree. The CollapseInfo dictionary is a single shared reference across all
    /// <c>with</c> clones — mutations are visible to every BuildContainer call.
    /// Use <c>with</c> to produce an inputCtx/containerCtx with <see cref="Style"/> set to that
    /// element's own <see cref="ComputedStyle"/>; nested Inputs receive that same context too —
    /// but since a nested Input always overrides <see cref="Style"/> from its own
    /// ShowIf/Style/FontSize (never falling through to whatever was ambient) via its own call to
    /// <c>Resolve</c>, this only ever actually matters for a bare Label found while descending
    /// through Container/OneOf/Condition on the way to one — letting a loose Label inherit
    /// exactly what a true direct child of the same Input (or Container) would.
    /// <c>CurrentInputName</c> similarly tracks whichever Input is ambient at this point in the
    /// tree, reset whenever a new one is entered, for a bare Label's default coordinate origin —
    /// <see cref="Rendering.LayoutFilter"/> separately rediscovers the same Input during its own
    /// per-game walk, since the resolved <see cref="InputDefinition"/> a loose label belongs to
    /// doesn't exist as an object yet at the point its own children are being built. A loose
    /// Label's centering against its enclosing Container is handled the same way — entirely by
    /// <see cref="Rendering.LayoutFilter"/>, which rediscovers the enclosing
    /// <see cref="Container"/> during its own walk — so this context carries no equivalent field
    /// for it; see <see cref="Container"/>'s doc comment.
    /// </summary>
    private record BuildContext(
        ITemplateImageSource ImageSource,
        Dictionary<string, StyleNode> NamedStyles,
        Dictionary<InputDefinition, CollapseInfo> CollapseInfo,
        ComputedStyle Style,
        double OriginX = 0,
        double OriginY = 0,
        string? CurrentInputName = null);

    /// <summary>
    /// The style cascade's state at one point in the tree — the fully-merged result of every
    /// <see cref="IStyledNode"/> (or leaf) ancestor's own attributes, each explicit value winning
    /// over whatever was already ambient. See <see cref="ResolveStyle"/> for how one more level
    /// gets folded in. <see cref="ShowIf"/> stays a raw, unparsed string (like
    /// <see cref="StyleNode.ShowIf"/>) so an absent value can keep falling through the chain;
    /// <see cref="MinOpacity"/>/<see cref="InactiveBlurRadius"/> stay nullable all the way through
    /// for the same reason <see cref="InputImageDefinition"/>/<see cref="OverlayDefinition"/>
    /// themselves do — null means "nothing in the tree set this," resolved against the template
    /// default later, at evaluation time. <see cref="FontSize"/> is the one field guaranteed
    /// non-null: its template-default tier is baked into the root context's <c>Style</c> up
    /// front, the same guarantee <c>BuildContext.DefaultFontSize</c> used to provide directly.
    /// </summary>
    private record ComputedStyle(
        string? ShowIf,
        double? MinOpacity,
        double? InactiveBlurRadius,
        double FontSize);

    /// <summary>
    /// Mutable iteration state for one container's slot loop. SlotIndex advances as children
    /// consume slots; a nested Container creates its own frame — slot counting does not leak
    /// across container boundaries.
    /// </summary>
    private class StackFrame
    {
        public double OriginX { get; init; }
        public double OriginY { get; init; }
        public double Gap { get; init; }
        public int SlotIndex { get; set; }
    }
}
