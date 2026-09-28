using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.Templates;

/// <summary>
/// Per-input collapse metadata: the shared list of slot-level nodes for the collapsing Group
/// the input belongs to, the gap distance between slots, and the Group's own <c>vAlign</c>
/// (already validated/normalized by <see cref="LayoutResolver"/> — always "top", "bottom", or
/// "center"). Multiple InputDefinitions in the same Group point at the same <see cref="Group"/>
/// list — the reference identity is used by LayoutFilter as the dedup key when computing
/// offsets, and <see cref="VAlign"/> lets it correct the collapsed (actually-visible) slot count
/// against the fixed count <see cref="StackVAlign.Shift"/> used when the group was resolved.
/// </summary>
[ExcludeFromCodeCoverage]
public record CollapseInfo(IReadOnlyList<ILayoutElement> Group, double Gap, string VAlign = "top");

/// <summary>
/// Computes collapse group metadata for a collapsing Group's children. Identifies the
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
    /// Flattens <paramref name="children"/> into its slot-level nodes — the same walk <see
    /// cref="Build"/> uses to populate <see cref="CollapseInfo.Group"/>, exposed so <see
    /// cref="Rendering.LayoutFilter"/> can compute a loose Label's true center from a group's
    /// <em>current</em> slot list without duplicating this logic (needed regardless of whether
    /// the group actually collapses — see <see cref="InputGroup"/>'s doc comment).
    /// </summary>
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
    /// Collects slot-level nodes into <paramref name="output"/>. A nested InputGroup and a OneOf
    /// each occupy a single slot as a block.
    /// </summary>
    private static void CollectSlots(ILayoutElement node, List<ILayoutElement> output)
    {
        switch (node)
        {
            case InputDefinition input:
                output.Add(input);
                break;
            case InputGroup group:
                output.Add(group);
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
                // Takes no slot, same as an Overlay.
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
                output[input] = info;
                break;
            case InputGroup group:
                foreach (ILayoutElement child in group.Children)
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
                // Leaf, not an InputDefinition -- no collapse metadata to record.
                break;
            default:
                throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {slot.GetType().Name}");
        }
    }
}
