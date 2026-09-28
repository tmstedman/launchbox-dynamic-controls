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
        var looseImages = new Dictionary<InputDefinition, List<InputImageDefinition>>(ReferenceEqualityComparer.Instance);
        var looseLabels = new Dictionary<InputDefinition, List<(LabelDefinition Label, InputGroup? Group)>>(ReferenceEqualityComparer.Instance);

        foreach (ILayoutElement element in template.Layout.Elements)
        {
            CollectVisibleElement(element, inputsToRender, includedGroupOverlays, looseImages, looseLabels, currentInput: null, currentGroup: null, ctx);
        }

        var renderSet = new HashSet<InputDefinition>(inputsToRender, ReferenceEqualityComparer.Instance);
        Dictionary<InputDefinition, double> adjustments = ComputeCollapseAdjustments(inputsToRender, template, ctx, renderSet);

        // Merged once, here, so InputImageRenderer/InputLabelRenderer never need to know a static
        // (InputDefinition.InputImages/Labels) and a loose (Condition-gated) source ever existed
        // separately -- LayoutInput.Images/Labels is simply the complete set for this render pass.
        var layout = inputsToRender.Select(input =>
        {
            VisibilityFlags flags = _evaluator.GetVisibilityFlags(input, ctx);
            return new LayoutInput(
                input,
                adjustments.GetValueOrDefault(input, 0.0),
                flags,
                [.. input.InputImages, .. looseImages.GetValueOrDefault(input, [])],
                [.. input.Labels, .. looseLabels.GetValueOrDefault(input, [])
                    .Select(entry => ResolveLooseLabel(entry, template, ctx, renderSet))]);
        }).ToList();

        return new FilteredLayout(Inputs: layout, GroupOverlays: includedGroupOverlays);
    }

    /// <summary>
    /// Computes per-input Y offsets for members of collapsing groups. Members whose images are
    /// all zero-opacity vacate their slot; subsequent members shift up by the group's collapse
    /// gap. Each collapse group is processed once — the shared <see cref="CollapseInfo.Group"/>
    /// reference serves as the dedup key. OneOf slots vacate when no alternative rendered or all
    /// rendered inputs are zero-opacity.
    ///
    /// <para>When the group's <see cref="CollapseInfo.VAlign"/> isn't "top", every member also
    /// gets a uniform correction on top of that: <see cref="LayoutResolver"/> anchored "bottom"
    /// or "center" against the group's full, fixed slot count when the template was resolved, but
    /// vacated slots mean fewer are actually left at render time, so that anchor would otherwise
    /// drift toward the top by one gap per vacated slot. The correction re-derives the same shift
    /// against the count that's actually left and folds the difference into the running offset
    /// before the vacate loop below ever touches it, so the two adjustments compose exactly
    /// instead of fighting each other.</para>
    /// </summary>
    private Dictionary<InputDefinition, double> ComputeCollapseAdjustments(
        List<InputDefinition> inputsToRender,
        Template template,
        VisibilityContext ctx,
        HashSet<InputDefinition> renderSet)
    {
        var adjustments = new Dictionary<InputDefinition, double>(ReferenceEqualityComparer.Instance);
        var processedGroups = new HashSet<IReadOnlyList<ILayoutElement>>(ReferenceEqualityComparer.Instance);
        IReadOnlyDictionary<InputDefinition, CollapseInfo> collapseInfo = template.Layout.CollapseInfo;

        foreach (InputDefinition input in inputsToRender)
        {
            if (!collapseInfo.TryGetValue(input, out CollapseInfo? info) || !processedGroups.Add(info.Group)) continue;

            double gap = info.Gap;
            int visibleCount = info.Group.Count(slot => !IsHidden(slot, template, ctx, renderSet));
            double vAlignCorrection = StackVAlign.Shift(info.VAlign, info.Group.Count, gap)
                - StackVAlign.Shift(info.VAlign, visibleCount, gap);

            double cumulativeOffset = vAlignCorrection;
            foreach (ILayoutElement slot in info.Group)
            {
                switch (slot)
                {
                    case InputDefinition slotInput:
                        adjustments[slotInput] = cumulativeOffset;
                        if (IsHidden(slotInput, template, ctx, renderSet))
                            cumulativeOffset -= gap;
                        break;
                    case OneOf oneOf:
                        foreach (InputDefinition leaf in SelectedLeaves(oneOf, renderSet))
                        {
                            adjustments[leaf] = cumulativeOffset;
                        }
                        if (IsHidden(oneOf, template, ctx, renderSet))
                            cumulativeOffset -= gap;
                        break;
                    case InputGroup:
                    case ConditionElement:
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
    /// Resolves a loose Label's final Y. Unchanged if it has no enclosing Group (nothing to
    /// center against). Otherwise, the visual center of however many of that group's slots
    /// actually survive for this game — reusing the exact same slot-flattening
    /// (<see cref="CollapseGroupBuilder.Flatten"/>) and hidden-check this class already uses for
    /// member Inputs' own Y offsets, applied instead to the group's own shape (declared Y, gap,
    /// vAlign — see <see cref="InputGroup"/>'s doc comment for why these live directly on the
    /// group rather than being threaded through the resolver). A non-collapsing group always
    /// uses its full nominal slot count: nothing ever vacates without <c>collapse="true"</c>, so
    /// nothing about the center varies by game in that case, and the formula below reduces to a
    /// fixed value.
    ///
    /// <para>The label's own <c>y</c> attribute (e.g. a hand-tuned <c>y="+15"</c> nudge) is
    /// preserved as an additive offset on top of the computed center, never discarded: Phase 1
    /// already baked it into <paramref name="entry"/>'s <c>Label.Y</c> relative to the group's
    /// *nominal* (uncollapsed) frame origin, so subtracting that same nominal origin back out
    /// recovers exactly the delta the author wrote, whatever it was resolved against.</para>
    /// </summary>
    private LabelDefinition ResolveLooseLabel(
        (LabelDefinition Label, InputGroup? Group) entry,
        Template template,
        VisibilityContext ctx,
        HashSet<InputDefinition> renderSet)
    {
        if (entry.Group is null) return entry.Label;

        List<ILayoutElement> slots = CollapseGroupBuilder.Flatten(entry.Group.Children);
        int visibleCount = entry.Group.Collapse
            ? slots.Count(slot => !IsHidden(slot, template, ctx, renderSet))
            : slots.Count;

        double nominalFrameOriginY = entry.Group.DeclaredOriginY
            - StackVAlign.Shift(entry.Group.VAlign, slots.Count, entry.Group.Gap);
        double ownOffset = entry.Label.Y - nominalFrameOriginY;

        double trueCenterY = entry.Group.DeclaredOriginY
            - StackVAlign.Shift(entry.Group.VAlign, visibleCount, entry.Group.Gap)
            + ((visibleCount - 1) * entry.Group.Gap / 2);

        return entry.Label with { Y = trueCenterY + ownOffset };
    }

    private List<InputDefinition> SelectedLeaves(OneOf oneOf, HashSet<InputDefinition> renderSet) =>
        [.. oneOf.Alternatives.SelectMany(CollectInputLeaves).Where(renderSet.Contains)];

    private bool IsHidden(ILayoutElement slot, Template template, VisibilityContext ctx, HashSet<InputDefinition> renderSet)
    {
        switch (slot)
        {
            case InputDefinition slotInput:
                return _evaluator.AllImagesZeroOpacity(slotInput, template.Layout.DefaultMinOpacity, ctx);
            case OneOf oneOf:
                List<InputDefinition> selected = SelectedLeaves(oneOf, renderSet);
                return selected.Count == 0
                    || selected.All(leaf => _evaluator.AllImagesZeroOpacity(leaf, template.Layout.DefaultMinOpacity, ctx));
            case InputGroup:
            case ConditionElement:
                return false; // Stack-as-slot: never considered hidden, so it never vacates.
            default:
                throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {slot.GetType().Name}");
        }
    }

    /// <summary>
    /// Recursively collects inputs and group overlays for a single template node.
    /// InputDefinitions always render and recurse into their Children. InputGroups are included
    /// only when any child has a visible render; excluded groups drop all members. OneOfs render
    /// the first alternative with a visible render and drop the rest.
    /// <paramref name="currentInput"/> tracks whichever InputDefinition was most recently entered
    /// (reset each time a nested one is), so a bare <see cref="RenderElement"/>/
    /// <see cref="LabelElement"/> reached through Group/OneOf/Condition attaches to the
    /// right owner — it can only be reached at all by having already recursed through every
    /// wrapping Condition/Group/OneOf above it, which is what makes nested Conditions AND
    /// together for free, with no separate "accumulate and re-check" step needed here.
    /// <paramref name="currentGroup"/> tracks whichever InputGroup was most recently entered, the
    /// same way — reset on entering a nested InputDefinition or overwritten on entering a nested
    /// InputGroup (never transparent), unaffected by OneOf/Condition — so a loose Label knows
    /// which group's shape to center against (see <see cref="ResolveLooseLabel"/>); a label with
    /// no enclosing group at all just keeps its build-time value unchanged.
    /// </summary>
    private void CollectVisibleElement(
        ILayoutElement element,
        List<InputDefinition> inputsToRender,
        List<LayoutGroupOverlay> includedGroupOverlays,
        Dictionary<InputDefinition, List<InputImageDefinition>> looseImages,
        Dictionary<InputDefinition, List<(LabelDefinition Label, InputGroup? Group)>> looseLabels,
        InputDefinition? currentInput,
        InputGroup? currentGroup,
        VisibilityContext ctx)
    {
        switch (element)
        {
            case InputDefinition input:
                inputsToRender.Add(input);
                foreach (ILayoutElement child in input.Children)
                {
                    CollectVisibleElement(child, inputsToRender, includedGroupOverlays, looseImages, looseLabels, input, currentGroup: null, ctx);
                }
                break;
            case InputGroup group when IsGroupVisible(group, ctx):
                foreach (ILayoutElement child in group.Children)
                {
                    CollectVisibleElement(child, inputsToRender, includedGroupOverlays, looseImages, looseLabels, currentInput, currentGroup: group, ctx);
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
                        CollectVisibleElement(alt, inputsToRender, includedGroupOverlays, looseImages, looseLabels, currentInput, currentGroup, ctx);
                        break;
                    }
                }
                break;
            case ConditionElement condition when _evaluator.AnyVisible(condition, ctx):
                foreach (ILayoutElement child in condition.Children)
                {
                    CollectVisibleElement(child, inputsToRender, includedGroupOverlays, looseImages, looseLabels, currentInput, currentGroup, ctx);
                }
                break;
            case ConditionElement:
                // Condition evaluated false: drop it and everything inside, same as an excluded Group.
                break;
            case RenderElement re when currentInput != null:
                looseImages.TryAdd(currentInput, []);
                looseImages[currentInput].Add(re.Image);
                break;
            case LabelElement le when currentInput != null:
                looseLabels.TryAdd(currentInput, []);
                looseLabels[currentInput].Add((le.Label, currentGroup));
                break;
            case RenderElement or LabelElement:
                // No ambient Input reached this point at all -- LayoutResolver already logged
                // this as a template error when it was parsed; nothing more to do at render time.
                break;
            default:
                throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {element.GetType().Name}");
        }
    }

    private bool IsGroupVisible(InputGroup group, VisibilityContext ctx) =>
        group.Children.Any(c => _evaluator.AnyVisible(c, ctx));

    private static IEnumerable<InputDefinition> CollectInputLeaves(ILayoutElement node) => node switch
    {
        InputDefinition input => [input],
        InputGroup group => group.Children.SelectMany(CollectInputLeaves),
        OneOf oneOf => oneOf.Alternatives.SelectMany(CollectInputLeaves),
        ConditionElement condition => condition.Children.SelectMany(CollectInputLeaves),
        RenderElement or LabelElement => [],
        _ => throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {node.GetType().Name}")
    };
}
