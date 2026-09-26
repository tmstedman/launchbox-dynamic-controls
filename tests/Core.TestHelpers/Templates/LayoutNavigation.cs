using DynamicControls.Templates;

namespace DynamicControls.Core.TestHelpers.Templates;

/// <summary>
/// Test helpers for navigating a <see cref="ResolvedLayout"/>'s element tree. Extensions live
/// on both <see cref="ResolvedLayout"/> and <see cref="IEnumerable{T}"/> of <see cref="ILayoutElement"/>
/// so lookups chain uniformly: <c>result.FirstInput().Children.FirstInputGroup()...</c>.
/// </summary>
public static class LayoutNavigation
{
    /// <summary>Returns the first top-level <see cref="InputDefinition"/> in document order.</summary>
    public static InputDefinition FirstInput(this ResolvedLayout result) =>
        result.Elements.FirstInput();

    /// <summary>Returns the first top-level <see cref="InputGroup"/> in document order. Stacks
    /// and plain Groups both materialize as <see cref="InputGroup"/>; distinguish via
    /// <see cref="InputGroup.AlwaysInclude"/> if needed.</summary>
    public static InputGroup FirstInputGroup(this ResolvedLayout result) =>
        result.Elements.FirstInputGroup();

    /// <summary>Returns the first top-level <see cref="OneOf"/> in document order.</summary>
    public static OneOf FirstOneOf(this ResolvedLayout result) =>
        result.Elements.FirstOneOf();

    /// <summary>Returns the first top-level <see cref="ConditionElement"/> in document order.</summary>
    public static ConditionElement FirstCondition(this ResolvedLayout result) =>
        result.Elements.FirstCondition();

    /// <summary>Returns the first <see cref="InputDefinition"/> in the sequence — usable on any
    /// <c>Children</c> or <c>Alternatives</c> list to keep the chain reading uniformly.</summary>
    public static InputDefinition FirstInput(this IEnumerable<ILayoutElement> elements) =>
        elements.OfType<InputDefinition>().First();

    /// <summary>Returns the first <see cref="InputGroup"/> in the sequence.</summary>
    public static InputGroup FirstInputGroup(this IEnumerable<ILayoutElement> elements) =>
        elements.OfType<InputGroup>().First();

    /// <summary>Returns the first <see cref="OneOf"/> in the sequence.</summary>
    public static OneOf FirstOneOf(this IEnumerable<ILayoutElement> elements) =>
        elements.OfType<OneOf>().First();

    /// <summary>Returns the first <see cref="ConditionElement"/> in the sequence.</summary>
    public static ConditionElement FirstCondition(this IEnumerable<ILayoutElement> elements) =>
        elements.OfType<ConditionElement>().First();

    /// <summary>Depth-first walk over a layout element tree, yielding every element and recursing
    /// through <see cref="InputGroup.Children"/> and <see cref="ConditionElement.Children"/>.
    /// Useful for assertions that need to reach inputs nested inside transparent Groups (e.g. a
    /// Stack containing a Group of Inputs) or gated behind a Condition, without caring about the
    /// intermediate container shape.</summary>
    public static IEnumerable<ILayoutElement> Flatten(this IEnumerable<ILayoutElement> elements)
    {
        foreach (ILayoutElement e in elements)
        {
            yield return e;
            if (e is InputGroup g)
                foreach (ILayoutElement c in g.Children.Flatten()) yield return c;
            if (e is ConditionElement cond)
                foreach (ILayoutElement c in cond.Children.Flatten()) yield return c;
        }
    }
}
