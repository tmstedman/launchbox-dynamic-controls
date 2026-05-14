namespace DynamicControls.Templates;

/// <summary>
/// Converts a <see cref="LayoutConfig"/> into a <see cref="ResolvedLayout"/>: resolves style
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
    ResolvedLayout Resolve(LayoutConfig config, ITemplateImageSource imageSource);
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
        LayoutConfig config,
        ITemplateImageSource imageSource)
    {
        StyleConfig? style = config.Head.Style;
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

    private ILayoutElement BuildNode(IConfigNode node, BuildContext ctx) => node switch
    {
        InputNode inputXml => BuildInputDefinition(inputXml, ctx),
        GroupNode groupXml => BuildInputGroup(groupXml, ctx),
        StackNode stackXml => BuildInputStack(stackXml, ctx),
        OneOfNode oneOfXml => BuildOneOf(oneOfXml, ctx),
        _ => throw new InvalidOperationException($"Unknown node type: {node.GetType()}")
    };

    /// <summary>
    /// Resolves an InputNode DTO into a fully-built InputDefinition: labels, renders,
    /// overlays, and nested children are all resolved against the template's coordinate origin.
    /// </summary>
    private InputDefinition BuildInputDefinition(InputNode inputXml, BuildContext ctx)
    {
        string name = inputXml.Name;

        // Resolve the Input's referenced style (if any). Explicit Input attributes win over
        // the named style's values; absent attributes inherit from the style.
        StyleConfig? namedStyle = null;
        if (inputXml.Style != null && !ctx.NamedStyles.TryGetValue(inputXml.Style, out namedStyle))
            _logger.Error($"Input '{name}' references unknown style '{inputXml.Style}'");

        // Resolve this Input's own optional x/y — if present they establish a new coordinate
        // origin for all of its Renders, Labels, Overlays, and nested Children. Absent = inherit
        // ctx.OriginX/Y (which may be a stack slot position or the canvas origin).
        double inputOriginX = inputXml.X.Resolve(ctx.OriginX);
        double inputOriginY = inputXml.Y.Resolve(ctx.OriginY);

        // Build a context carrying this input's effective inherited values. Renders and overlays
        // on this input read from inputCtx; nested child inputs start fresh from ctx (no cascade).
        BuildContext inputCtx = ctx with
        {
            InheritedShowIf = inputXml.ShowIf ?? namedStyle?.ShowIf,
            InheritedMinOpacity = inputXml.MinOpacity ?? namedStyle?.MinOpacity,
            InheritedInactiveBlurRadius = inputXml.InactiveBlurRadius ?? namedStyle?.InactiveBlurRadius,
            OriginX = inputOriginX,
            OriginY = inputOriginY
        };

        var labels = new List<LabelDefinition>();
        foreach (LabelNode labelXml in inputXml.Labels)
        {
            var label = new LabelDefinition(
                X: labelXml.X.Resolve(inputOriginX),
                Y: labelXml.Y.Resolve(inputOriginY),
                Alignment: labelXml.Align,
                FontSize: labelXml.FontSize ?? inputXml.FontSize ?? namedStyle?.FontSize ?? ctx.DefaultFontSize);
            labels.Add(label);
            _logger.Debug($"Label position: {name} at ({labelXml.X},{labelXml.Y}) align={labelXml.Align} fontSize={label.FontSize}");
        }

        var images = new List<InputImageDefinition>();
        foreach (RenderNode renderXml in inputXml.Renders)
        {
            ShowIfCondition showIf = ParseShowIf(renderXml.ShowIf ?? inputCtx.InheritedShowIf);
            images.Add(new InputImageDefinition(
                X: renderXml.X.Resolve(inputOriginX),
                Y: renderXml.Y.Resolve(inputOriginY),
                ImageFile: $"{name}.png",
                Width: renderXml.Width,
                Height: renderXml.Height,
                UseImageFile: renderXml.UseImage != null ? $"{renderXml.UseImage}.png" : null,
                ShowIf: showIf,
                MinOpacity: renderXml.MinOpacity ?? inputCtx.InheritedMinOpacity,
                InactiveBlurRadius: renderXml.InactiveBlurRadius ?? inputCtx.InheritedInactiveBlurRadius));
            _logger.Debug($"Render position: ({renderXml.X},{renderXml.Y}) showIf={showIf}");
        }

        var overlays = new List<OverlayDefinition>();
        foreach (OverlayNode overlayXml in inputXml.Overlays)
        {
            if (overlayXml.Src == null) continue;
            overlays.Add(BuildOverlayDefinition(overlayXml, inputCtx));
        }

        var children = inputXml.Children
            .Select(c => BuildNode(c, ctx with { OriginX = inputOriginX, OriginY = inputOriginY }))
            .ToList();

        return new InputDefinition(
            Name: name,
            InputImages: images,
            Overlays: overlays,
            Labels: labels,
            Children: children);
    }

    /// <summary>Resolves a GroupNode DTO into a conditional InputGroup. The group is included
    /// at render time only when any descendant has a visible render.</summary>
    private InputGroup BuildInputGroup(GroupNode groupXml, BuildContext ctx)
    {
        var group = new InputGroup(
            AlwaysInclude: false,
            Children: [.. groupXml.Children.Select(c => BuildNode(c, ctx))],
            Overlays: [.. groupXml.Overlays
                .Where(o => o.Src != null)
                .Select(o => BuildOverlayDefinition(o, ctx))]);
        _logger.Debug($"Group: children={group.Children.Count}, overlays={group.Overlays.Count}");
        return group;
    }

    /// <summary>Resolves a StackNode DTO into an always-included InputGroup. Establishes a
    /// canvas origin and stacks children vertically: each Input (at any depth through transparent
    /// plain Groups) consumes one slot, advancing the y position by Gap.</summary>
    private InputGroup BuildInputStack(StackNode stackXml, BuildContext ctx)
    {
        var frame = new StackFrame
        {
            OriginX = stackXml.X.Resolve(ctx.OriginX),
            OriginY = stackXml.Y.Resolve(ctx.OriginY),
            Gap = stackXml.Gap ?? 0,
            SlotIndex = 0,
        };
        BuildContext stackCtx = ctx with { OriginX = frame.OriginX, OriginY = frame.OriginY };

        var children = new List<ILayoutElement>();
        foreach (IConfigNode child in stackXml.Children)
        {
            children.Add(BuildNodeInStack(child, frame, stackCtx));
        }

        var stack = new InputGroup(
            AlwaysInclude: true,
            Children: children,
            Overlays: [.. stackXml.Overlays
                .Where(o => o.Src != null)
                .Select(o => BuildOverlayDefinition(o, stackCtx))]);

        if (stackXml.Collapse)
            CollapseGroupBuilder.Build(children, frame.Gap, ctx.CollapseInfo);

        _logger.Debug($"Stack (at {frame.OriginX},{frame.OriginY} gap={frame.Gap} collapse={stackXml.Collapse}): children={stack.Children.Count}, overlays={stack.Overlays.Count}");
        return stack;
    }

    /// <summary>
    /// Builds one layout node within a positioned group's slot loop. Inputs consume one slot
    /// each and advance the frame's SlotIndex. Plain Groups are transparent — their Input
    /// children each advance the same counter. Positioned Groups and OneOfs consume one slot as
    /// a block and own their own inner traversal.
    /// </summary>
    private ILayoutElement BuildNodeInStack(
        IConfigNode node,
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
            case StackNode stackXml:
            {
                (double sx, double sy) = ConsumeSlot(frame);
                return BuildInputStack(stackXml, ctx with { OriginX = sx, OriginY = sy });
            }
            case GroupNode plainGroupXml:
            {
                // Plain nested group: transparent to slot counting; its Inputs each advance the counter.
                var children = new List<ILayoutElement>();
                foreach (IConfigNode child in plainGroupXml.Children)
                {
                    children.Add(BuildNodeInStack(child, frame, ctx));
                }
                var group = new InputGroup(
                    AlwaysInclude: false,
                    Children: children,
                    Overlays: [.. plainGroupXml.Overlays
                        .Where(o => o.Src != null)
                        .Select(o => BuildOverlayDefinition(o, ctx))]);
                _logger.Debug($"Group (transparent in stack): children={group.Children.Count}, overlays={group.Overlays.Count}");
                return group;
            }
            case OneOfNode oneOfXml:
            {
                // OneOf: consumes one slot; all alternatives share that slot's origin.
                (double sx, double sy) = ConsumeSlot(frame);
                return BuildOneOf(oneOfXml, ctx with { OriginX = sx, OriginY = sy });
            }
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
    /// its own renders and overlays. The CollapseInfo dictionary is a single shared reference
    /// across all <c>with</c> clones — mutations are visible to every BuildInputStack call.
    /// Use <c>with</c> to produce an inputCtx with the inherited values set; child inputs receive
    /// the original ctx (no cascade across structural levels).
    /// </summary>
    private record BuildContext(
        ITemplateImageSource ImageSource,
        double DefaultFontSize,
        Dictionary<string, StyleConfig> NamedStyles,
        Dictionary<InputDefinition, CollapseInfo> CollapseInfo,
        string? InheritedShowIf = null,
        double? InheritedMinOpacity = null,
        double? InheritedInactiveBlurRadius = null,
        double OriginX = 0,
        double OriginY = 0);

    /// <summary>
    /// Mutable iteration state for one stack's slot loop. SlotIndex advances as children consume
    /// slots; plain Groups share the frame with their enclosing stack so their inputs each
    /// advance the same counter. A nested Stack creates its own frame — slot counting does not
    /// leak across stack boundaries.
    /// </summary>
    private class StackFrame
    {
        public double OriginX { get; init; }
        public double OriginY { get; init; }
        public double Gap { get; init; }
        public int SlotIndex { get; set; }
    }
}
