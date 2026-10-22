using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.Templates;

/// <summary>
/// Per-input collapse metadata: the shared list of slot-level nodes for the collapsing
/// Container the input belongs to, the gap distance between slots, and the Container's own
/// <c>vAlign</c> (already validated/normalized by <see cref="LayoutResolver"/> — always "top",
/// "bottom", or "center"). Multiple InputDefinitions in the same Container point at the same
/// <see cref="Group"/> list — the reference identity is used by LayoutFilter as the dedup key
/// when computing offsets, and <see cref="VAlign"/> lets it correct the collapsed
/// (actually-visible) slot count against the fixed count <see cref="StackVAlign.Shift"/> used
/// when the container was resolved.
/// </summary>
[ExcludeFromCodeCoverage]
public record CollapseInfo(IReadOnlyList<ILayoutElement> Group, double Gap, string VAlign = "top");

/// <summary>
/// Computes collapse group metadata for a collapsing Container's children. Identifies the
/// slot-level nodes, then records a <see cref="CollapseInfo"/> entry for every InputDefinition
/// leaf reachable from those slots, so LayoutFilter can shift or vacate slots at render time.
/// Produces data into an external dictionary — does not mutate InputDefinitions.
/// </summary>
internal static class CollapseGroupBuilder
{
    /// <summary>
    /// Collects the slot-level nodes from <paramref name="children"/> and writes a
    /// <see cref="CollapseInfo"/> entry to <paramref name="output"/> for every InputDefinition
    /// leaf reachable from those slots.
    /// </summary>
    internal static void Build(
        IReadOnlyList<ILayoutElement> children,
        double gap,
        Dictionary<InputDefinition, CollapseInfo> output,
        string vAlign = "top")
    {
        List<ILayoutElement> collapseGroup = Flatten(children);
        var info = new CollapseInfo(collapseGroup, gap, vAlign);
        foreach (ILayoutElement slot in collapseGroup)
        {
            SetMetadata(slot, info, output);
        }
    }

    /// <summary>
    /// Records <paramref name="info"/> for <paramref name="input"/> unless a nearer container
    /// already claimed it.
    ///
    /// <para>A container resolves its children before registering its own group, so an inner
    /// collapsing container's entries land first and the outer one's would overwrite them.
    /// That matters because <see cref="SetMetadata"/> walks a nested container's children while
    /// <see cref="CollectSlots"/> treats that container as a single opaque slot: the outer group
    /// would claim inputs it has no slot for, leaving them unadjusted at render time while
    /// appearing to be handled. The innermost claim is the correct one — it is the container
    /// whose gap and slot list those inputs actually sit in.</para>
    /// </summary>
    private static void Claim(
        InputDefinition input,
        CollapseInfo info,
        Dictionary<InputDefinition, CollapseInfo> output)
    {
        if (!output.ContainsKey(input)) output[input] = info;
    }

    /// <summary>
    /// Flattens <paramref name="children"/> into its slot-level nodes — the same walk <see
    /// cref="Build"/> uses to populate <see cref="CollapseInfo.Group"/>, exposed so <see
    /// cref="Rendering.LayoutFilter"/> can compute a loose Label's true center from a container's
    /// <em>current</em> slot list without duplicating this logic (needed regardless of whether
    /// the container actually collapses — see <see cref="Container"/>'s doc comment).
    /// </summary>
    /// <summary>
    /// How many rows a slot occupies: one for an Input or a OneOf, and for a nested Container as
    /// many as it draws — matching what <c>LayoutResolver.CountSlots</c> reserved for it, so the
    /// render-time arithmetic (collapse shifts, vAlign, a loose label's center) measures the same
    /// stack the coordinates were laid out against. Floored at one for an empty container.
    /// </summary>
    internal static int SpanOf(ILayoutElement slot) => slot switch
    {
        Container container => Math.Max(1, Flatten(container.Children).Sum(SpanOf)),
        OneOf oneOf => oneOf.Alternatives.Count == 0 ? 1 : Math.Max(1, oneOf.Alternatives.Max(SpanOf)),
        _ => 1
    };

    internal static List<ILayoutElement> Flatten(IReadOnlyList<ILayoutElement> children)
    {
        var output = new List<ILayoutElement>();
        foreach (ILayoutElement child in children)
        {
            CollectSlots(child, output);
        }
        return output;
    }

    /// <summary>
    /// Collects slot-level nodes into <paramref name="output"/>. A nested Container and a OneOf
    /// each occupy a single slot as a block.
    /// </summary>
    private static void CollectSlots(ILayoutElement node, List<ILayoutElement> output)
    {
        switch (node)
        {
            case InputDefinition input:
                output.Add(input);
                break;
            case Container container:
                output.Add(container);
                break;
            case OneOf oneOf:
                output.Add(oneOf);
                break;
            case ConditionElement condition:
                foreach (ILayoutElement child in condition.Children)
                {
                    CollectSlots(child, output);
                }
                break;
            case LabelElement:
            case OverlayElement:
                // Neither takes a slot.
                break;
            default:
                throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {node.GetType().Name}");
        }
    }

    /// <summary>
    /// Recursively records a <see cref="CollapseInfo"/> entry for every InputDefinition leaf
    /// reachable from <paramref name="slot"/>, so that whichever alternative renders can trigger
    /// collapse group processing at render time.
    /// </summary>
    private static void SetMetadata(
        ILayoutElement slot,
        CollapseInfo info,
        Dictionary<InputDefinition, CollapseInfo> output)
    {
        switch (slot)
        {
            case InputDefinition input:
                Claim(input, info, output);
                break;
            case Container container:
                foreach (ILayoutElement child in container.Children)
                {
                    SetMetadata(child, info, output);
                }
                break;
            case OneOf oneOf:
                foreach (ILayoutElement alt in oneOf.Alternatives)
                {
                    SetMetadata(alt, info, output);
                }
                break;
            case ConditionElement condition:
                foreach (ILayoutElement child in condition.Children)
                {
                    SetMetadata(child, info, output);
                }
                break;
            case LabelElement:
            case OverlayElement:
                // Leaves, not InputDefinitions -- no collapse metadata to record.
                break;
            default:
                throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {slot.GetType().Name}");
        }
    }
}
