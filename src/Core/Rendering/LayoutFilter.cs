using DynamicControls.Templates;

namespace DynamicControls.Rendering;

/// <summary>
/// Applies group visibility rules and collapse-stack adjustments to a template, producing the
/// ordered list of inputs that should appear in the render output (paired with their y-offsets
/// and aggregate visibility flags) and the group-level overlays that survived filtering.
/// </summary>
public interface ILayoutFilter
{
    /// <summary>
    /// Walks <paramref name="template"/>'s layout elements, applies group visibility and
    /// collapse-stack rules under <paramref name="ctx"/>, and returns the inputs/overlays for
    /// this render pass.
    /// </summary>
    FilteredLayout Filter(Template template, VisibilityContext ctx);
}

/// <summary>
/// Production implementation: delegates visibility checks to <see cref="IVisibilityEvaluator"/>;
/// collapse-stack y-offsets accumulate as the filter iterates the cached collapse groups
/// produced by <see cref="CollapseGroupBuilder"/> at template build time.
/// </summary>
public class LayoutFilter(IVisibilityEvaluator evaluator) : ILayoutFilter
{
    private readonly IVisibilityEvaluator _evaluator = evaluator;

    /// <inheritdoc />
    public FilteredLayout Filter(Template template, VisibilityContext ctx)
    {
        var inputsToRender = new List<InputDefinition>();
        var includedGroupOverlays = new List<LayoutGroupOverlay>();

        foreach (ILayoutElement element in template.Layout.Elements)
        {
            CollectVisibleElement(element, inputsToRender, includedGroupOverlays, ctx);
        }

        Dictionary<InputDefinition, double> adjustments = ComputeCollapseAdjustments(inputsToRender, template, ctx);

        var layout = inputsToRender.Select(input =>
        {
            VisibilityFlags flags = _evaluator.GetVisibilityFlags(input, ctx);
            return new LayoutInput(input, adjustments.GetValueOrDefault(input, 0.0), flags);
        }).ToList();

        return new FilteredLayout(Inputs: layout, GroupOverlays: includedGroupOverlays);
    }

    /// <summary>
    /// Computes per-input Y offsets for members of collapsing stacks. Members whose images are
    /// all zero-opacity vacate their slot; subsequent members shift up by the stack's collapse
    /// gap. Each collapse group is processed once — the shared <see cref="CollapseInfo.Group"/>
    /// reference serves as the dedup key. OneOf slots vacate when no alternative rendered or all
    /// rendered inputs are zero-opacity.
    ///
    /// <para>When the stack's <see cref="CollapseInfo.VAlign"/> isn't "top", every member also
    /// gets a uniform correction on top of that: <see cref="LayoutResolver"/> anchored "bottom"
    /// or "center" against the stack's full, fixed slot count when the template was resolved, but
    /// vacated slots mean fewer are actually left at render time, so that anchor would otherwise
    /// drift toward the top by one gap per vacated slot. The correction re-derives the same shift
    /// against the count that's actually left and folds the difference into the running offset
    /// before the vacate loop below ever touches it, so the two adjustments compose exactly
    /// instead of fighting each other.</para>
    /// </summary>
    private Dictionary<InputDefinition, double> ComputeCollapseAdjustments(
        List<InputDefinition> inputsToRender,
        Template template,
        VisibilityContext ctx)
    {
        var adjustments = new Dictionary<InputDefinition, double>(ReferenceEqualityComparer.Instance);
        var processedGroups = new HashSet<IReadOnlyList<ILayoutElement>>(ReferenceEqualityComparer.Instance);
        var renderSet = new HashSet<InputDefinition>(inputsToRender, ReferenceEqualityComparer.Instance);
        IReadOnlyDictionary<InputDefinition, CollapseInfo> collapseInfo = template.Layout.CollapseInfo;

        List<InputDefinition> SelectedLeaves(OneOf oneOf) =>
            [.. oneOf.Alternatives.SelectMany(CollectInputLeaves).Where(renderSet.Contains)];

        bool IsHidden(ILayoutElement slot)
        {
            switch (slot)
            {
                case InputDefinition slotInput:
                    return _evaluator.AllImagesZeroOpacity(slotInput, template.Layout.DefaultMinOpacity, ctx);
                case OneOf oneOf:
                    List<InputDefinition> selected = SelectedLeaves(oneOf);
                    return selected.Count == 0
                        || selected.All(leaf => _evaluator.AllImagesZeroOpacity(leaf, template.Layout.DefaultMinOpacity, ctx));
                case InputGroup:
                    return false; // Stack-as-slot: never considered hidden, so it never vacates.
                default:
                    throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {slot.GetType().Name}");
            }
        }

        foreach (InputDefinition input in inputsToRender)
        {
            if (!collapseInfo.TryGetValue(input, out CollapseInfo? info) || !processedGroups.Add(info.Group)) continue;

            double gap = info.Gap;
            int visibleCount = info.Group.Count(slot => !IsHidden(slot));
            double vAlignCorrection = StackVAlign.Shift(info.VAlign, info.Group.Count, gap)
                - StackVAlign.Shift(info.VAlign, visibleCount, gap);

            double cumulativeOffset = vAlignCorrection;
            foreach (ILayoutElement slot in info.Group)
            {
                switch (slot)
                {
                    case InputDefinition slotInput:
                        adjustments[slotInput] = cumulativeOffset;
                        if (IsHidden(slotInput))
                            cumulativeOffset -= gap;
                        break;
                    case OneOf oneOf:
                        foreach (InputDefinition leaf in SelectedLeaves(oneOf))
                        {
                            adjustments[leaf] = cumulativeOffset;
                        }
                        if (IsHidden(oneOf))
                            cumulativeOffset -= gap;
                        break;
                    case InputGroup:
                        // Stack-as-slot: collapse adjustments not currently computed for these.
                        break;
                    default:
                        throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {slot.GetType().Name}");
                }
            }
        }

        return adjustments;
    }

    /// <summary>
    /// Recursively collects inputs and group overlays for a single template node.
    /// InputDefinitions always render and recurse into their Children. InputGroups are included
    /// when AlwaysInclude is set or any child has a visible render; excluded groups drop all
    /// members. OneOfs render the first alternative with a visible render and drop the rest.
    /// </summary>
    private void CollectVisibleElement(
        ILayoutElement element,
        List<InputDefinition> inputsToRender,
        List<LayoutGroupOverlay> includedGroupOverlays,
        VisibilityContext ctx)
    {
        switch (element)
        {
            case InputDefinition input:
                inputsToRender.Add(input);
                foreach (ILayoutElement child in input.Children)
                {
                    CollectVisibleElement(child, inputsToRender, includedGroupOverlays, ctx);
                }
                break;
            case InputGroup group when IsGroupVisible(group, ctx):
                foreach (ILayoutElement child in group.Children)
                {
                    CollectVisibleElement(child, inputsToRender, includedGroupOverlays, ctx);
                }
                if (group.Overlays.Count > 0)
                {
                    VisibilityFlags groupFlags = _evaluator.AggregateFlags(group, ctx);
                    foreach (OverlayDefinition overlay in group.Overlays)
                    {
                        includedGroupOverlays.Add(new LayoutGroupOverlay(overlay, groupFlags));
                    }
                }
                break;
            case InputGroup:
                // Invisible group: drop the group and all its members.
                break;
            case OneOf oneOf:
                foreach (ILayoutElement alt in oneOf.Alternatives)
                {
                    if (_evaluator.AnyVisible(alt, ctx))
                    {
                        CollectVisibleElement(alt, inputsToRender, includedGroupOverlays, ctx);
                        break;
                    }
                }
                break;
            default:
                throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {element.GetType().Name}");
        }
    }

    private bool IsGroupVisible(InputGroup group, VisibilityContext ctx) =>
        group.AlwaysInclude || group.Children.Any(c => _evaluator.AnyVisible(c, ctx));

    private static IEnumerable<InputDefinition> CollectInputLeaves(ILayoutElement node) => node switch
    {
        InputDefinition input => [input],
        InputGroup group => group.Children.SelectMany(CollectInputLeaves),
        OneOf oneOf => oneOf.Alternatives.SelectMany(CollectInputLeaves),
        _ => throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {node.GetType().Name}")
    };
}
