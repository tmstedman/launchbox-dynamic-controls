using DynamicControls.InputMapping;

namespace DynamicControls.Templates;

public interface IInputDescendantsBuilder
{
    IReadOnlyDictionary<InputDefinition, IReadOnlyList<string>> Build(IReadOnlyList<ILayoutElement> elements);
}

/// <summary>
/// Builds the input-descendants map for a template element tree: a map from each
/// <see cref="InputDefinition"/> to the generic input names <see cref="VisibilityEvaluator.GetVisibilityFlags"/>
/// should fold into its own hasLabel/isMapped check, alongside its own name. Sourced from
/// <see cref="WholeInputs.PartsOf"/> — a whole control's four direction names — rather than from
/// the tree's own structural nesting: the direction Inputs live as siblings of their whole in
/// Layout.xml, not as its children, so the fan-out has to be a fact about names, not tree shape.
/// An Input whose name isn't a whole gets an empty list, same outcome as before this existed.
/// </summary>
public class InputDescendantsBuilder : IInputDescendantsBuilder
{
    /// <summary>
    /// Walks <paramref name="elements"/> and returns a map from every <see cref="InputDefinition"/>
    /// in the tree to its whole-control part names, if any. Containers, OneOfs, and Conditions are
    /// transparent — they contribute their children to the traversal but do not appear as keys.
    /// Uses reference equality so structurally identical but distinct instances are tracked
    /// separately. Result is exposed as read-only.
    /// </summary>
    public IReadOnlyDictionary<InputDefinition, IReadOnlyList<string>> Build(IReadOnlyList<ILayoutElement> elements)
    {
        var descendants = new Dictionary<InputDefinition, IReadOnlyList<string>>(ReferenceEqualityComparer.Instance);
        foreach (ILayoutElement element in elements)
        {
            Collect(element, descendants);
        }
        return descendants;
    }

    /// <summary>
    /// Registers every <see cref="InputDefinition"/> reachable from <paramref name="element"/> as a
    /// key, keyed to its whole-control part names (empty if it isn't one). Still recurses into an
    /// Input's own <see cref="InputDefinition.Children"/> so a nested Input (unusual — the shipped
    /// template no longer does it at all — but still valid for the style-cascade fallthrough it
    /// enables) still gets its own entry.
    /// </summary>
    private static void Collect(ILayoutElement element, Dictionary<InputDefinition, IReadOnlyList<string>> descendants)
    {
        switch (element)
        {
            case InputDefinition def:
                descendants[def] = WholeInputs.PartsOf.GetValueOrDefault(def.Name, []);
                foreach (ILayoutElement child in def.Children)
                {
                    Collect(child, descendants);
                }
                break;
            case Container container:
                foreach (ILayoutElement child in container.Children)
                {
                    Collect(child, descendants);
                }
                break;
            case OneOf oneOf:
                foreach (ILayoutElement alt in oneOf.Alternatives)
                {
                    Collect(alt, descendants);
                }
                break;
            case ConditionElement condition:
                foreach (ILayoutElement child in condition.Children)
                {
                    Collect(child, descendants);
                }
                break;
            case LabelElement:
                // Leaf, not an InputDefinition -- nothing to register or descend into.
                break;
            default:
                throw new InvalidOperationException($"Unhandled ILayoutElement subtype: {element.GetType().Name}");
        }
    }
}
