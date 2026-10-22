using DynamicControls.Templates;

namespace DynamicControls.Rendering;

/// <summary>
/// Applies OneOf/Condition selection and collapse-stack adjustments to a template, producing the
/// ordered list of inputs that should appear in the render output (paired with their y-offsets
/// and aggregate visibility flags) and the container-level overlays that survived filtering.
/// </summary>
public interface ILayoutFilter
{
    /// <summary>
    /// Walks <paramref name="template"/>'s layout elements, applies OneOf/Condition selection and
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
        var looseLabels = new Dictionary<InputDefinition, List<(LabelDefinition Label, Container? Container)>>(ReferenceEqualityComparer.Instance);
        var looseInputOverlays = new Dictionary<InputDefinition, List<OverlayDefinition>>(ReferenceEqualityComparer.Instance);
        var looseContainerOverlays = new Dictionary<Container, List<OverlayDefinition>>(ReferenceEqualityComparer.Instance);
        var namedLabels = new List<(LabelDefinition Label, Container? Container, IReadOnlyList<string> Names)>();

        foreach (ILayoutElement element in template.Layout.Elements)
        {
            CollectVisibleElement(element, inputsToRender, includedGroupOverlays, looseLabels, looseInputOverlays, looseContainerOverlays, namedLabels, currentInput: null, currentContainer: null, ctx);
        }

        // Resolved after the walk, not during it: a label naming its inputs is routinely written
        // before them (the merged direction clusters all put their shared <Label> first), so at
        // the moment one is reached the inputs it names may not have been collected yet. The
        // first of them that renders owns it -- naming several is how a label says "whichever of
        // these this game actually draws", not a request to pick the best one.
        if (namedLabels.Count > 0)
        {
            var renderedByName = inputsToRender
                .GroupBy(i => i.Name)
                .ToDictionary(g => g.Key, g => g.First());
            foreach ((LabelDefinition label, Container? container, IReadOnlyList<string> names) in namedLabels)
            {
                InputDefinition? owner = names
                    .Select(n => renderedByName.GetValueOrDefault(n))
                    .FirstOrDefault(i => i != null);
                if (owner is null)
                {
                    // Every input it named is absent this game. Not a template error -- the
                    // label simply has nothing to say, the same as a Condition that dropped it.
                    continue;
                }
                looseLabels.TryAdd(owner, []);
                looseLabels[owner].Add((label, container));
            }
        }

        var renderSet = new HashSet<InputDefinition>(inputsToRender, ReferenceEqualityComparer.Instance);
        Dictionary<InputDefinition, double> adjustments = ComputeCollapseAdjustments(inputsToRender, template, ctx, renderSet);

        // Merged once, here, so InputLabelRenderer/InputImageRenderer never need to know a static
        // (InputDefinition.Labels/Overlays) and a loose (Condition-gated) source ever existed
        // separately -- LayoutInput.Labels/Overlays are simply the complete sets for this render
        // pass. Images have no loose source (there is no longer a separate Render concept), so
        // LayoutInput.Images is always exactly InputDefinition.InputImages.
        var layout = inputsToRender.Select(input =>
        {
            VisibilityFlags flags = _evaluator.GetVisibilityFlags(input, ctx);
            return new LayoutInput(
                input,
                adjustments.GetValueOrDefault(input, 0.0),
                flags,
                input.InputImages,
                [.. input.Labels, .. looseLabels.GetValueOrDefault(input, [])
                    .Select(entry => ResolveLooseLabel(entry, template, ctx, renderSet))],
                [.. input.Overlays, .. looseInputOverlays.GetValueOrDefault(input, [])]);
        }).ToList();

        return new FilteredLayout(Inputs: layout, GroupOverlays: includedGroupOverlays);
    }

    /// <summary>
    /// Computes per-input Y offsets for members of collapsing containers. Members whose images
    /// are all zero-opacity vacate their slot; subsequent members shift up by the collapse gap.
    /// Each collapse group is processed once — the shared <see cref="CollapseInfo.Group"/>
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

        // Driven by the groups themselves rather than by what each rendering input is keyed to.
        // CollapseGroupBuilder claims an input for the innermost collapsing container holding it,
        // so a container whose every member sits inside a nested block of its own is claimed by
        // nothing that renders, and keying off inputsToRender skipped it — leaving the rows it
        // owns unshifted while the blocks inside them collapsed. A group is this game's business
        // when anything beneath it renders, whoever that input is keyed to.
        foreach (CollapseInfo info in collapseInfo.Values)
        {
            if (!processedGroups.Add(info.Group)) continue;
            if (!info.Group.Any(slot => CollectInputLeaves(slot).Any(renderSet.Contains))) continue;

            double gap = info.Gap;
            int visibleCount = info.Group.Sum(slot => VisibleSpan(slot, template, ctx, renderSet));
            double vAlignCorrection = StackVAlign.Shift(info.VAlign, info.Group.Sum(CollapseGroupBuilder.SpanOf), gap)
                - StackVAlign.Shift(info.VAlign, visibleCount, gap);

            double cumulativeOffset = vAlignCorrection;
            foreach (ILayoutElement slot in info.Group)
            {
                switch (slot)
                {
                    case InputDefinition slotInput:
                        Shift(adjustments, slotInput, cumulativeOffset);
                        if (IsHidden(slotInput, template, ctx, renderSet))
                            cumulativeOffset -= gap;
                        break;
                    case OneOf oneOf:
                        foreach (InputDefinition leaf in SelectedLeaves(oneOf, renderSet))
                        {
                            Shift(adjustments, leaf, cumulativeOffset);
                        }
                        // Advance past the rows the selected alternative drew, not the rows the
                        // OneOf reserved for its largest. Selecting nothing draws none of them,
                        // which is the same arithmetic and how the whole block vacates.
                        cumulativeOffset -= (CollapseGroupBuilder.SpanOf(oneOf)
                            - VisibleSpan(oneOf, template, ctx, renderSet)) * gap;
                        break;
                    case Container nested:
                        // A nested block's members belong to its own collapse group, which knows
                        // nothing about what vacated above it out here, so this stack's running
                        // offset is added on top of whatever that group computed. Both writes are
                        // additive for exactly that reason -- the two compose, one per level.
                        foreach (InputDefinition leaf in CollectInputLeaves(nested).Where(renderSet.Contains))
                        {
                            Shift(adjustments, leaf, cumulativeOffset);
                        }
                        // Advance past the rows it actually draws rather than the rows it
                        // reserved, so a vacancy inside the block is reclaimed by what follows.
                        cumulativeOffset -= (CollapseGroupBuilder.SpanOf(nested)
                            - VisibleSpan(nested, template, ctx, renderSet)) * gap;
                        break;
                    case ConditionElement:
                        // Never reaches here: CollectSlots walks a Condition's children inline.
                        break;
                    default:
                        throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {slot.GetType().Name}");
                }
            }
        }

        return adjustments;
    }

    /// <summary>Adds <paramref name="offset"/> to an input's running adjustment. Additive rather
    /// than assigning, because an input inside a nested block is shifted once by its own group and
    /// again by every collapsing stack above it.</summary>
    private static void Shift(Dictionary<InputDefinition, double> adjustments, InputDefinition input, double offset) =>
        adjustments[input] = adjustments.GetValueOrDefault(input, 0.0) + offset;

    /// <summary>
    /// How many rows a slot actually draws this game: zero when it vacates, its full span when it
    /// doesn't, and for a collapsing nested Container the sum over its own slots — the counterpart
    /// to <see cref="CollapseGroupBuilder.SpanOf"/>, which answers what was reserved. A nested
    /// container that doesn't collapse keeps every row it reserved, vacated or not.
    /// </summary>
    private int VisibleSpan(ILayoutElement slot, Template template, VisibilityContext ctx, HashSet<InputDefinition> renderSet) =>
        slot switch
        {
            Container nested when nested.Collapse => CollapseGroupBuilder.Flatten(nested.Children)
                .Sum(inner => VisibleSpan(inner, template, ctx, renderSet)),
            // Whichever alternative drew is what the OneOf actually occupies, which can be fewer
            // rows than it reserved — a merged axis takes one where its unmerged form takes two.
            OneOf oneOf => SelectedAlternative(oneOf, renderSet) is { } alternative
                ? VisibleSpan(alternative, template, ctx, renderSet)
                : 0,
            _ => IsHidden(slot, template, ctx, renderSet) ? 0 : CollapseGroupBuilder.SpanOf(slot),
        };

    /// <summary>The alternative this game selected — the first one holding an input that
    /// rendered. Null when none did, which is also how a OneOf vacates its slots entirely.</summary>
    private static ILayoutElement? SelectedAlternative(OneOf oneOf, HashSet<InputDefinition> renderSet) =>
        oneOf.Alternatives.FirstOrDefault(a => CollectInputLeaves(a).Any(renderSet.Contains));

    /// <summary>
    /// Resolves a loose Label's final Y. Unchanged if it has no enclosing Container (nothing to
    /// center against). Otherwise, the visual center of however many of that container's slots
    /// actually survive for this game — reusing the exact same slot-flattening
    /// (<see cref="CollapseGroupBuilder.Flatten"/>) and hidden-check this class already uses for
    /// member Inputs' own Y offsets, applied instead to the container's own shape (declared Y,
    /// gap, vAlign — see <see cref="Container"/>'s doc comment for why these live directly on the
    /// container rather than being threaded through the resolver). A non-collapsing container
    /// always uses its full nominal slot count: nothing ever vacates without
    /// <c>collapse="true"</c>, so nothing about the center varies by game in that case, and the
    /// formula below reduces to a fixed value.
    ///
    /// <para>The label's own <c>y</c> attribute (e.g. a hand-tuned <c>y="+15"</c> nudge) is
    /// preserved as an additive offset on top of the computed center, never discarded: Phase 1
    /// already baked it into <paramref name="entry"/>'s <c>Label.Y</c> relative to the
    /// container's *nominal* (uncollapsed) frame origin, so subtracting that same nominal origin
    /// back out recovers exactly the delta the author wrote, whatever it was resolved against.</para>
    /// </summary>
    private LabelDefinition ResolveLooseLabel(
        (LabelDefinition Label, Container? Container) entry,
        Template template,
        VisibilityContext ctx,
        HashSet<InputDefinition> renderSet)
    {
        if (entry.Container is null) return entry.Label;

        List<ILayoutElement> slots = CollapseGroupBuilder.Flatten(entry.Container.Children);
        int nominalCount = slots.Sum(CollapseGroupBuilder.SpanOf);
        int visibleCount = entry.Container.Collapse
            ? slots.Sum(slot => VisibleSpan(slot, template, ctx, renderSet))
            : nominalCount;

        double nominalFrameOriginY = entry.Container.DeclaredOriginY
            - StackVAlign.Shift(entry.Container.VAlign, nominalCount, entry.Container.Gap);
        double ownOffset = entry.Label.Y - nominalFrameOriginY;

        double trueCenterY = entry.Container.DeclaredOriginY
            - StackVAlign.Shift(entry.Container.VAlign, visibleCount, entry.Container.Gap)
            + ((visibleCount - 1) * entry.Container.Gap / 2);

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
            case Container:
            case ConditionElement:
                // A block never vacates as a single slot; how much of it actually drew is
                // VisibleSpan's question, and that is what the parent advances past.
                return false;
            default:
                throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {slot.GetType().Name}");
        }
    }

    /// <summary>
    /// Recursively collects inputs and container overlays for a single template node.
    /// InputDefinitions always render and recurse into their Children. Containers are always
    /// included once reached — unlike the &lt;Group&gt; they replaced, they never decide their
    /// own visibility; whatever wraps one (typically an explicit Condition) already decided that
    /// before reaching it here. OneOfs render the first alternative with a visible render and
    /// drop the rest.
    /// <paramref name="currentInput"/> tracks whichever InputDefinition was most recently entered
    /// (reset each time a nested one is), so a bare <see cref="LabelElement"/>/<see
    /// cref="OverlayElement"/> reached through Container/OneOf/Condition attaches to the right
    /// owner — it can only be reached at all by having already recursed through every wrapping
    /// Condition/Container/OneOf above it, which is what makes nested Conditions AND together for
    /// free, with no separate "accumulate and re-check" step needed here. A <see
    /// cref="LabelElement"/> that names its own inputs ignores the ambient one and goes to
    /// <paramref name="namedLabels"/> instead, to be matched against the inputs that rendered
    /// once the walk is over — see <see cref="Filter"/>.
    /// <paramref name="currentContainer"/> tracks whichever Container was most recently entered,
    /// the same way — reset on entering a nested InputDefinition or overwritten on entering a
    /// nested Container (never transparent), unaffected by OneOf/Condition — so a loose Label
    /// knows which container's shape to center against (see <see cref="ResolveLooseLabel"/>) and
    /// a loose Overlay with no ambient Input knows which container's aggregate flags to join; an
    /// element with no enclosing container at all is unaffected either way.
    /// <para>A loose <see cref="OverlayElement"/> is looked up by owner in strict priority order:
    /// an ambient Input (<paramref name="looseInputOverlays"/>, merged into that Input's own
    /// Overlays the same way a loose Label merges into its Labels — see <see cref="Filter"/>),
    /// else an ambient Container (<paramref name="looseContainerOverlays"/>, merged into that
    /// Container's own <see cref="Container.Overlays"/> right where its case below already emits
    /// them, since by then every loose overlay in its subtree has necessarily already been
    /// collected — children are always visited before a Container emits its overlays), else
    /// neither at all, in which case it renders unconditionally right here, with no fold-in
    /// target to gate against.</para>
    /// </summary>
    private void CollectVisibleElement(
        ILayoutElement element,
        List<InputDefinition> inputsToRender,
        List<LayoutGroupOverlay> includedGroupOverlays,
        Dictionary<InputDefinition, List<(LabelDefinition Label, Container? Container)>> looseLabels,
        Dictionary<InputDefinition, List<OverlayDefinition>> looseInputOverlays,
        Dictionary<Container, List<OverlayDefinition>> looseContainerOverlays,
        List<(LabelDefinition Label, Container? Container, IReadOnlyList<string> Names)> namedLabels,
        InputDefinition? currentInput,
        Container? currentContainer,
        VisibilityContext ctx)
    {
        switch (element)
        {
            case InputDefinition input:
                inputsToRender.Add(input);
                foreach (ILayoutElement child in input.Children)
                {
                    CollectVisibleElement(child, inputsToRender, includedGroupOverlays, looseLabels, looseInputOverlays, looseContainerOverlays, namedLabels, input, currentContainer: null, ctx);
                }
                break;
            case Container container:
                foreach (ILayoutElement child in container.Children)
                {
                    CollectVisibleElement(child, inputsToRender, includedGroupOverlays, looseLabels, looseInputOverlays, looseContainerOverlays, namedLabels, currentInput, currentContainer: container, ctx);
                }
                List<OverlayDefinition> containerOverlays = [.. container.Overlays, .. looseContainerOverlays.GetValueOrDefault(container, [])];
                if (containerOverlays.Count > 0)
                {
                    VisibilityFlags containerFlags = _evaluator.AggregateFlags(container, ctx);
                    foreach (OverlayDefinition overlay in containerOverlays)
                    {
                        includedGroupOverlays.Add(new LayoutGroupOverlay(overlay, containerFlags));
                    }
                }
                break;
            case OneOf oneOf:
                foreach (ILayoutElement alt in oneOf.Alternatives)
                {
                    if (_evaluator.AnyVisible(alt, ctx))
                    {
                        CollectVisibleElement(alt, inputsToRender, includedGroupOverlays, looseLabels, looseInputOverlays, looseContainerOverlays, namedLabels, currentInput, currentContainer, ctx);
                        break;
                    }
                }
                break;
            case ConditionElement condition when _evaluator.AnyVisible(condition, ctx):
                foreach (ILayoutElement child in condition.Children)
                {
                    CollectVisibleElement(child, inputsToRender, includedGroupOverlays, looseLabels, looseInputOverlays, looseContainerOverlays, namedLabels, currentInput, currentContainer, ctx);
                }
                break;
            case ConditionElement:
                // Condition evaluated false: drop it and everything inside -- the only way
                // anything (including a Container) is ever excluded now.
                break;
            case LabelElement le when le.InputNames is { Count: > 0 }:
                // Named outright: which of them owns it can only be settled once the whole walk
                // has run, so it waits (see Filter). A label that names inputs never falls back
                // to the ambient one -- naming is the more specific statement of the two.
                namedLabels.Add((le.Label, currentContainer, le.InputNames));
                break;
            case LabelElement le when currentInput != null:
                looseLabels.TryAdd(currentInput, []);
                looseLabels[currentInput].Add((le.Label, currentContainer));
                break;
            case LabelElement:
                // Names nothing and no ambient Input reached this point at all -- LayoutResolver
                // already logged this as a template error when it was parsed; nothing more to do.
                break;
            case OverlayElement oe when currentInput != null:
                looseInputOverlays.TryAdd(currentInput, []);
                looseInputOverlays[currentInput].Add(oe.Overlay);
                break;
            case OverlayElement oe when currentContainer != null:
                looseContainerOverlays.TryAdd(currentContainer, []);
                looseContainerOverlays[currentContainer].Add(oe.Overlay);
                break;
            case OverlayElement oe:
                // No ambient Input or Container at all -- unlike a loose Label, this isn't a
                // template error: there's simply no fold-in target to gate against, so it
                // renders unconditionally as its own template-level overlay. Its own ShowIf still
                // governs its own opacity, the same as a direct child always has.
                includedGroupOverlays.Add(new LayoutGroupOverlay(oe.Overlay, VisibilityFlags.None));
                break;
            default:
                throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {element.GetType().Name}");
        }
    }

    private static IEnumerable<InputDefinition> CollectInputLeaves(ILayoutElement node) => node switch
    {
        InputDefinition input => [input],
        Container container => container.Children.SelectMany(CollectInputLeaves),
        OneOf oneOf => oneOf.Alternatives.SelectMany(CollectInputLeaves),
        ConditionElement condition => condition.Children.SelectMany(CollectInputLeaves),
        LabelElement => [],
        OverlayElement => [],
        _ => throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {node.GetType().Name}")
    };
}
