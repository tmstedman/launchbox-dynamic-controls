namespace DynamicControls.Templates;

public interface IInputDescendantsBuilder
{
    IReadOnlyDictionary<InputDefinition, IReadOnlyList<InputDefinition>> Build(IReadOnlyList<ILayoutElement> elements);
}

/// <summary>
/// Builds the input-descendants map for a template element tree: a map from each
/// <see cref="InputDefinition"/> to all <see cref="InputDefinition"/> nodes reachable through
/// its <see cref="InputDefinition.Children"/>. Used by <see cref="VisibilityEvaluator"/> to fan
/// out hasLabel and isMapped checks across structural children without re-traversing the tree on
/// every render pass.
/// </summary>
public class InputDescendantsBuilder : IInputDescendantsBuilder
{
    /// <summary>
    /// Walks <paramref name="elements"/> and returns a map from every <see cref="InputDefinition"/>
    /// in the tree to its flattened list of descendant <see cref="InputDefinition"/> nodes.
    /// InputGroups and OneOfs are transparent — they contribute their children to the traversal
    /// but do not appear as keys. Uses reference equality so structurally identical but distinct
    /// instances are tracked separately. Result is exposed as read-only.
    /// </summary>
    public IReadOnlyDictionary<InputDefinition, IReadOnlyList<InputDefinition>> Build(IReadOnlyList<ILayoutElement> elements)
    {
        var descendants = new Dictionary<InputDefinition, IReadOnlyList<InputDefinition>>(ReferenceEqualityComparer.Instance);
        foreach (ILayoutElement element in elements)
        {
            PopulateDescendants(element, descendants);
        }
        return descendants;
    }

    /// <summary>
    /// Recursively registers every <see cref="InputDefinition"/> reachable from
    /// <paramref name="element"/> as a key in <paramref name="descendants"/>, with its own
    /// flattened descendant list as the value. InputGroups and OneOfs are traversed but not
    /// registered — only InputDefinitions appear as keys.
    /// </summary>
    private static void PopulateDescendants(
        ILayoutElement element,
        Dictionary<InputDefinition, IReadOnlyList<InputDefinition>> descendants)
    {
        switch (element)
        {
            case InputGroup group:
                foreach (ILayoutElement child in group.Children)
                {
                    PopulateDescendants(child, descendants);
                }
                break;
            case OneOf oneOf:
                foreach (ILayoutElement alt in oneOf.Alternatives)
                {
                    PopulateDescendants(alt, descendants);
                }
                break;
            case InputDefinition def:
                var defDescendants = new List<InputDefinition>();
                foreach (ILayoutElement child in def.Children)
                {
                    CollectDescendants(child, defDescendants);
                }
                descendants[def] = defDescendants;
                foreach (ILayoutElement child in def.Children)
                {
                    PopulateDescendants(child, descendants);
                }
                break;
            default:
                throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {element.GetType().Name}");
        }
    }

    /// <summary>
    /// Appends all <see cref="InputDefinition"/> nodes reachable from <paramref name="element"/>
    /// to <paramref name="output"/>. InputGroups and OneOfs are traversed but not collected
    /// themselves — only leaf InputDefinitions (and their own InputDefinition descendants) are
    /// added.
    /// </summary>
    private static void CollectDescendants(ILayoutElement element, List<InputDefinition> output)
    {
        switch (element)
        {
            case InputDefinition input:
                output.Add(input);
                foreach (ILayoutElement child in input.Children)
                {
                    CollectDescendants(child, output);
                }
                break;
            case InputGroup group:
                foreach (ILayoutElement child in group.Children)
                {
                    CollectDescendants(child, output);
                }
                break;
            case OneOf oneOf:
                foreach (ILayoutElement alt in oneOf.Alternatives)
                {
                    CollectDescendants(alt, output);
                }
                break;
            default:
                throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {element.GetType().Name}");
        }
    }
}
