namespace DynamicControls.Templates;

/// <summary>
/// Converts a <see cref="LayoutDocument"/> into a <see cref="ResolvedLayout"/>: resolves style
/// defaults, builds the element tree with absolute coordinates and resolved styles, and
/// precomputes the <c>InputDescendants</c> and <c>CollapseInfo</c> lookup tables. Pure
/// transformation — no file I/O, no caching.
/// </summary>
public interface ILayoutResolver
{
    /// <summary>
    /// Resolves style defaults from <paramref name="config"/>, builds the element tree, and
    /// precomputes both the descendants index and the collapse-info map from the resolved tree.
    /// </summary>
    ResolvedLayout Resolve(LayoutDocument config, ITemplateImageSource imageSource);
}

/// <summary>
/// Production implementation: delegates the descendants pre-pass to
/// <see cref="IInputDescendantsBuilder"/>; collapse-info accumulation runs inline through a
/// shared dictionary threaded by <c>BuildContext</c>.
/// </summary>
public class LayoutResolver(ILogger logger, IInputDescendantsBuilder descendantsBuilder) : ILayoutResolver
{
    private readonly ILogger _logger = logger;
    private readonly IInputDescendantsBuilder _descendantsBuilder = descendantsBuilder;

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
            DefaultFontSize: defaultFontSize,
            NamedStyles: config.Head.NamedStyles,
            CollapseInfo: collapseInfo);

        var elements = config.Elements.Select(e => BuildNode(e, ctx)).ToList();
        return new ResolvedLayout(
            Elements: elements,
            InputDescendants: _descendantsBuilder.Build(elements),
            CollapseInfo: collapseInfo,
            DefaultFontSize: defaultFontSize,
            DefaultMinOpacity: defaultMinOpacity,
            DefaultInactiveBlurRadius: defaultInactiveBlurRadius);
    }

    private ILayoutElement BuildNode(ILayoutNode node, BuildContext ctx) => node switch
    {
        InputNode inputXml => BuildInputDefinition(inputXml, ctx),
        GroupNode groupXml => BuildInputGroup(groupXml, ctx),
        OneOfNode oneOfXml => BuildOneOf(oneOfXml, ctx),
        ConditionNode conditionXml => BuildCondition(conditionXml, ctx),
        LabelNode labelXml => BuildLooseLabel(labelXml, ctx),
        _ => throw new InvalidOperationException($"Unknown node type: {node.GetType()}")
    };

    /// <summary>
    /// Resolves an InputNode DTO into a fully-built InputDefinition: its own image, labels,
    /// overlays, and nested children are all resolved against the template's coordinate origin.
    /// </summary>
    private InputDefinition BuildInputDefinition(InputNode inputXml, BuildContext ctx)
    {
        string name = inputXml.Name;

        // Resolve the Input's referenced style (if any). Explicit Input attributes win over
        // the named style's values; absent attributes inherit from the style.
        StyleNode? namedStyle = null;
        if (inputXml.Style != null && !ctx.NamedStyles.TryGetValue(inputXml.Style, out namedStyle))
            _logger.Error($"Input '{name}' references unknown style '{inputXml.Style}'");

        // Resolve this Input's own optional x/y — if present they establish a new coordinate
        // origin for its own image, Labels, Overlays, and nested Children. Absent = inherit
        // ctx.OriginX/Y (which may be a stack slot position or the canvas origin).
        double inputOriginX = inputXml.X.Resolve(ctx.OriginX);
        double inputOriginY = inputXml.Y.Resolve(ctx.OriginY);

        // Build a context carrying this input's effective inherited values and identity. Labels
        // and overlays on this input read from inputCtx; nested children also receive inputCtx
        // (rather than a fresh ctx) so a *loose* Label reached through Group/OneOf/Condition can
        // inherit it exactly like a true direct child would — a nested <Input> is unaffected,
        // since it always overrides Inherited* from its own ShowIf/Style/FontSize rather than
        // falling through to whatever's ambient.
        ShowIfCondition showIf = ParseShowIf(inputXml.ShowIf ?? namedStyle?.ShowIf);
        double? minOpacity = inputXml.MinOpacity ?? namedStyle?.MinOpacity;
        double? inactiveBlurRadius = inputXml.InactiveBlurRadius ?? namedStyle?.InactiveBlurRadius;
        BuildContext inputCtx = ctx with
        {
            InheritedShowIf = inputXml.ShowIf ?? namedStyle?.ShowIf,
            InheritedMinOpacity = minOpacity,
            InheritedInactiveBlurRadius = inactiveBlurRadius,
            InheritedFontSize = inputXml.FontSize ?? namedStyle?.FontSize,
            OriginX = inputOriginX,
            OriginY = inputOriginY,
            CurrentInputName = name
        };

        var labels = inputXml.Labels.Select(labelXml => BuildLabelDefinition(labelXml, inputCtx)).ToList();

        var image = new InputImageDefinition(
            X: inputOriginX,
            Y: inputOriginY,
            ImageFile: $"{name}.png",
            Width: inputXml.Width,
            Height: inputXml.Height,
            UseImageFile: inputXml.UseImage != null ? $"{inputXml.UseImage}.png" : null,
            ShowIf: showIf,
            MinOpacity: minOpacity,
            InactiveBlurRadius: inactiveBlurRadius);
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

    /// <summary>Resolves a GroupNode DTO into an InputGroup, included at render time only when
    /// any descendant has a visible render — the group drops out entirely (itself and any
    /// Overlay it carries) when nothing inside it is visible. Establishes a
    /// canvas origin and stacks children vertically: each Input (at any depth through transparent
    /// Conditions) consumes one slot, advancing the y position by Gap. <c>VAlign</c> shifts
    /// that origin up front so the declared Y lands on the first, last, or middle slot rather
    /// than always being the first — using the template's fixed slot count, since this runs once
    /// at template-load time. When the group also collapses, that fixed-count shift is corrected
    /// at render time (see <see cref="Rendering.LayoutFilter"/>) against whatever slot count is
    /// actually left, using the same <see cref="StackVAlign.Shift"/> math with the vAlign this
    /// method already validated — carried through <see cref="CollapseInfo"/> so a bad value is
    /// only ever logged once, here.
    ///
    /// <para>The group's own shape — declared Y (before the shift above), Gap, VAlign, and
    /// whether it collapses — is attached directly to the returned <see cref="InputGroup"/>
    /// (rather than threaded through <see cref="BuildContext"/>) so <see
    /// cref="Rendering.LayoutFilter"/> can compute where a loose <c>&lt;Label&gt;</c> placed
    /// directly inside this group should sit: the visual center of however many of this group's
    /// slots actually survive for the current game, which is a per-game fact this build-time
    /// pass has no way to know — see <see cref="InputGroup"/>'s own doc comment.</para>
    /// </summary>
    private InputGroup BuildInputGroup(GroupNode groupXml, BuildContext ctx)
    {
        double gap = groupXml.Gap ?? 0;
        int slotCount = CountSlots(groupXml.Children);
        string vAlign = NormalizeVAlign(groupXml.VAlign);
        double vAlignShift = StackVAlign.Shift(vAlign, slotCount, gap);
        double declaredOriginY = groupXml.Y.Resolve(ctx.OriginY);

        var frame = new StackFrame
        {
            OriginX = groupXml.X.Resolve(ctx.OriginX),
            OriginY = declaredOriginY - vAlignShift,
            Gap = gap,
            SlotIndex = 0,
        };
        BuildContext groupCtx = ctx with { OriginX = frame.OriginX, OriginY = frame.OriginY };

        var children = new List<ILayoutElement>();
        foreach (ILayoutNode child in groupXml.Children)
        {
            children.Add(BuildNodeInStack(child, frame, groupCtx));
        }

        var group = new InputGroup(
            Children: children,
            Overlays: [.. groupXml.Overlays
                .Where(o => o.Src != null)
                .Select(o => BuildOverlayDefinition(o, groupCtx))],
            DeclaredOriginY: declaredOriginY,
            Gap: gap,
            VAlign: vAlign,
            Collapse: groupXml.Collapse);

        if (groupXml.Collapse)
            CollapseGroupBuilder.Build(children, frame.Gap, ctx.CollapseInfo, vAlign);

        _logger.Debug($"Group (at {frame.OriginX},{frame.OriginY} gap={frame.Gap} vAlign={vAlign} collapse={groupXml.Collapse}): children={group.Children.Count}, overlays={group.Overlays.Count}");
        return group;
    }

    /// <summary>
    /// Counts the slots <paramref name="nodes"/> will consume once built, mirroring
    /// <see cref="BuildNodeInStack"/>'s own slot rule exactly: Input/Group/OneOf each consume
    /// one slot, and a Condition is transparent, contributing its children's slots instead of
    /// one of its own. Computed ahead of the build so <see cref="StackVAlign.Shift"/> can shift
    /// the origin before the first slot is actually consumed.
    /// </summary>
    private static int CountSlots(IReadOnlyList<ILayoutNode> nodes) => nodes.Sum(CountSlots);

    private static int CountSlots(ILayoutNode node) => node switch
    {
        InputNode => 1,
        GroupNode => 1,
        OneOfNode => 1,
        ConditionNode conditionXml => CountSlots(conditionXml.Children),
        LabelNode => 0,
        _ => throw new InvalidOperationException($"Unknown node type: {node.GetType()}")
    };

    /// <summary>
    /// Validates a Group's <c>vAlign</c> against the values <see cref="StackVAlign.Shift"/>
    /// recognizes. An unrecognized value is logged and replaced with "top" — the only point in
    /// the pipeline this is ever checked, since render-time re-use of the value (for a
    /// collapsing group's correction) trusts whatever this method already normalized.
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
    /// Builds one layout node within a positioned group's slot loop. Inputs consume one slot
    /// each and advance the frame's SlotIndex. Nested Groups and OneOfs consume one slot as a
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
            case GroupNode groupXml:
            {
                (double sx, double sy) = ConsumeSlot(frame);
                return BuildInputGroup(groupXml, ctx with { OriginX = sx, OriginY = sy });
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
            default:
                throw new InvalidOperationException($"Unknown node type: {node.GetType()}");
        }
    }

    /// <summary>Computes the canvas origin for the frame's current slot and advances its
    /// SlotIndex.</summary>
    private static (double x, double y) ConsumeSlot(StackFrame frame) =>
        (frame.OriginX, frame.OriginY + (frame.SlotIndex++ * frame.Gap));

    /// <summary>Resolves a OneOfNode DTO into a OneOf, recursively building each
    /// alternative branch (Input, Group, or nested OneOf).</summary>
    private OneOf BuildOneOf(OneOfNode oneOfXml, BuildContext ctx)
    {
        var oneOf = new OneOf(
            Alternatives: [.. oneOfXml.Alternatives.Select(a => BuildNode(a, ctx))]);
        _logger.Debug($"OneOf: alternatives={oneOf.Alternatives.Count}");
        return oneOf;
    }

    /// <summary>Resolves a ConditionNode DTO into a ConditionElement, recursively building each
    /// child (Input, Group, OneOf, or nested Condition).</summary>
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
    /// enclosing Group's declared position is a per-game fact (which of the group's slots
    /// actually survive) this build-time pass has no way to know, so it's computed entirely by
    /// <see cref="Rendering.LayoutFilter"/> at render time instead, from the group's own shape
    /// (see <see cref="InputGroup"/>'s doc comment); this method never needs to special-case it.
    /// </summary>
    private LabelDefinition BuildLabelDefinition(LabelNode labelXml, BuildContext ctx)
    {
        var label = new LabelDefinition(
            X: labelXml.X.Resolve(ctx.OriginX),
            Y: labelXml.Y.Resolve(ctx.OriginY),
            Alignment: labelXml.Align,
            FontSize: labelXml.FontSize ?? ctx.InheritedFontSize ?? ctx.DefaultFontSize);
        _logger.Debug($"Label position: {ctx.CurrentInputName} at ({labelXml.X},{labelXml.Y}) align={labelXml.Align} fontSize={label.FontSize}");
        return label;
    }

    /// <summary>
    /// Resolves a &lt;Label&gt; found somewhere other than as a direct child of its own
    /// &lt;Input&gt; (e.g. nested inside a &lt;Condition&gt; wrapping a &lt;Group&gt;/
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
    /// Resolves an OverlayNode DTO into an OverlayDefinition. Inherited showIf/opacity/blur
    /// values flow in via ctx — input-level overlays pass inputCtx (with inherited values set);
    /// group-level overlays pass ctx (inherited values null).
    /// </summary>
    private OverlayDefinition BuildOverlayDefinition(OverlayNode overlayXml, BuildContext ctx)
    {
        (string resolvedPath, _) = ctx.ImageSource.Resolve(overlayXml.Src!, platform: null);
        ShowIfCondition showIf = ParseShowIf(overlayXml.ShowIf ?? ctx.InheritedShowIf);
        _logger.Debug($"Overlay: {overlayXml.Src} at ({overlayXml.X},{overlayXml.Y}) showIf={showIf}");
        return new OverlayDefinition(
            X: overlayXml.X.Resolve(ctx.OriginX),
            Y: overlayXml.Y.Resolve(ctx.OriginY),
            Source: resolvedPath,
            Width: overlayXml.Width,
            Height: overlayXml.Height,
            ShowIf: showIf,
            MinOpacity: overlayXml.MinOpacity ?? ctx.InheritedMinOpacity,
            InactiveBlurRadius: overlayXml.InactiveBlurRadius ?? ctx.InheritedInactiveBlurRadius);
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
    /// Context for the template tree walk. Carries the per-build inputs that are constant
    /// across all Build* calls (ImageSource, DefaultFontSize, NamedStyles, CollapseInfo
    /// accumulator) alongside the inherited visibility values that flow from an Input down to
    /// its own overlays. The CollapseInfo dictionary is a single shared reference
    /// across all <c>with</c> clones — mutations are visible to every BuildInputGroup call.
    /// Use <c>with</c> to produce an inputCtx with the inherited values set; nested Inputs
    /// receive that same inputCtx too — but since a nested Input always overrides Inherited* from
    /// its own ShowIf/Style/FontSize (never falling through to whatever was ambient), it's a
    /// no-op for that case, and only ever actually matters for a bare Label found while
    /// descending through Group/OneOf/Condition on the way to one — letting a loose Label
    /// inherit exactly what a true direct child of the same Input would.
    /// <c>CurrentInputName</c> similarly tracks whichever Input is ambient at this point in the
    /// tree, reset whenever a new one is entered, for a bare Label's default coordinate origin —
    /// <see cref="Rendering.LayoutFilter"/> separately rediscovers the same Input during its own
    /// per-game walk, since the resolved <see cref="InputDefinition"/> a loose label belongs to
    /// doesn't exist as an object yet at the point its own children are being built. A loose
    /// Label's centering against its enclosing Group is handled the same way — entirely by
    /// <see cref="Rendering.LayoutFilter"/>, which rediscovers the enclosing
    /// <see cref="InputGroup"/> during its own walk — so this context carries no equivalent field
    /// for it; see <see cref="InputGroup"/>'s doc comment.
    /// </summary>
    private record BuildContext(
        ITemplateImageSource ImageSource,
        double DefaultFontSize,
        Dictionary<string, StyleNode> NamedStyles,
        Dictionary<InputDefinition, CollapseInfo> CollapseInfo,
        string? InheritedShowIf = null,
        double? InheritedMinOpacity = null,
        double? InheritedInactiveBlurRadius = null,
        double? InheritedFontSize = null,
        double OriginX = 0,
        double OriginY = 0,
        string? CurrentInputName = null);

    /// <summary>
    /// Mutable iteration state for one group's slot loop. SlotIndex advances as children consume
    /// slots; a nested Group creates its own frame — slot counting does not leak across group
    /// boundaries.
    /// </summary>
    private class StackFrame
    {
        public double OriginX { get; init; }
        public double OriginY { get; init; }
        public double Gap { get; init; }
        public int SlotIndex { get; set; }
    }
}
