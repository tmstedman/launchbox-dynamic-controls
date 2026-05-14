using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.Templates;

/// <summary>
/// Per-input collapse metadata: the shared list of slot-level nodes for the collapsing Stack
/// the input belongs to, and the gap distance between slots. Multiple InputDefinitions in the
/// same Stack point at the same <see cref="Group"/> list — the reference identity is used by
/// LayoutFilter as the dedup key when computing offsets.
/// </summary>
[ExcludeFromCodeCoverage]
public record CollapseInfo(IReadOnlyList<ILayoutElement> Group, double Gap);

/// <summary>
/// Computes collapse group metadata for a collapsing Stack's children. Identifies the
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
        Dictionary<InputDefinition, CollapseInfo> output)
    {
        var collapseGroup = new List<ILayoutElement>();
        foreach (ILayoutElement child in children)
        {
            CollectSlots(child, collapseGroup);
        }
        var info = new CollapseInfo(collapseGroup, gap);
        foreach (ILayoutElement slot in collapseGroup)
        {
            SetMetadata(slot, info, output);
        }
    }

    /// <summary>
    /// Collects slot-level nodes into <paramref name="output"/>. Plain Groups are transparent
    /// (their Input children each get their own slot); always-include Groups (Stacks) and
    /// OneOfs occupy a single slot as a block.
    /// </summary>
    private static void CollectSlots(ILayoutElement node, List<ILayoutElement> output)
    {
        switch (node)
        {
            case InputDefinition input:
                output.Add(input);
                break;
            case InputGroup group when !group.AlwaysInclude:
                foreach (ILayoutElement child in group.Children)
                {
                    CollectSlots(child, output);
                }
                break;
            case InputGroup group:
                output.Add(group);
                break;
            case OneOf oneOf:
                output.Add(oneOf);
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
            default:
                throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {slot.GetType().Name}");
        }
    }
}
