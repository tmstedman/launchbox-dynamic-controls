using DynamicControls.Templates;
using static DynamicControls.Core.TestHelpers.Templates.LayoutElements;

namespace DynamicControls.Core.Tests.Templates;

/// <summary>
/// Unit tests for <see cref="InputDescendantsBuilder"/>. The builder flattens an
/// <see cref="InputDefinition"/>'s reachable input subtree into a list, treating
/// <see cref="InputGroup"/> and <see cref="OneOf"/> as transparent containers (traversed but not
/// keyed). The map uses reference equality so structurally identical instances stay distinct.
/// </summary>
public class InputDescendantsBuilderTests
{
    private readonly InputDescendantsBuilder _underTest = new();

    [Fact]
    public void Build_EmptyList_ReturnsEmptyIndex()
    {
        // given no top-level elements
        // when building the index
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<InputDefinition>> index = _underTest.Build([]);

        // then the result is an empty map
        index.ShouldBeEmpty();
    }

    [Fact]
    public void Build_LeafInput_MapsToEmptyList()
    {
        // given a single Input with no children
        InputDefinition input = Input("ButtonA");

        // when building the index
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<InputDefinition>> index = _underTest.Build([input]);

        // then the input is keyed with an empty descendants list
        index.Keys.ShouldBe([input]);
        index[input].ShouldBeEmpty();
    }

    [Fact]
    public void Build_NestedInputs_ParentDescendantsIncludeAllReachableInputs()
    {
        // given a three-level Input nesting: outer > middle > inner
        InputDefinition inner = Input("Inner");
        InputDefinition middle = Input("Middle", inner);
        InputDefinition outer = Input("Outer", middle);

        // when building the index
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<InputDefinition>> index = _underTest.Build([outer]);

        // then every input is keyed, and outer's list is the full flattened subtree
        index[outer].ShouldBe([middle, inner]);
        index[middle].ShouldBe([inner]);
        index[inner].ShouldBeEmpty();
    }

    [Fact]
    public void Build_InputGroup_IsTransparent_NotKeyed_ButChildrenAre()
    {
        // given a top-level InputGroup wrapping two Inputs
        InputDefinition a = Input("A");
        InputDefinition b = Input("B");
        InputGroup group = Group(a, b);

        // when building the index
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<InputDefinition>> index = _underTest.Build([group]);

        // then only the wrapped inputs appear as keys
        index.Keys.ShouldBe([a, b], ignoreOrder: true);
    }

    [Fact]
    public void Build_OneOf_IsTransparent_NotKeyed_ButAlternativesAre()
    {
        // given a top-level OneOf with two alternative Inputs
        InputDefinition primary = Input("Primary");
        InputDefinition fallback = Input("Fallback");
        OneOf oneOf = OneOf(primary, fallback);

        // when building the index
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<InputDefinition>> index = _underTest.Build([oneOf]);

        // then only the alternatives are keyed
        index.Keys.ShouldBe([primary, fallback], ignoreOrder: true);
    }

    [Fact]
    public void Build_GroupNestedUnderInput_GroupChildrenFlattenIntoParentDescendants()
    {
        // given an Input whose Children list contains a Group of two Inputs (transparent container)
        InputDefinition a = Input("A");
        InputDefinition b = Input("B");
        InputDefinition parent = Input("Parent", Group(a, b));

        // when building the index
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<InputDefinition>> index = _underTest.Build([parent]);

        // then the parent's descendants include both group members (group itself is not a key)
        index[parent].ShouldBe([a, b]);
        index.Keys.ShouldBe([parent, a, b], ignoreOrder: true);
    }

    [Fact]
    public void Build_OneOfNestedUnderInput_AllAlternativesContributeToParentDescendants()
    {
        // given an Input with a OneOf child containing two alternatives
        InputDefinition primary = Input("Primary");
        InputDefinition fallback = Input("Fallback");
        InputDefinition parent = Input("Parent", OneOf(primary, fallback));

        // when building the index
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<InputDefinition>> index = _underTest.Build([parent]);

        // then BOTH alternatives are folded into the parent's descendants list, even though only
        // one fires at render time — the index is structural, not runtime-evaluated
        index[parent].ShouldBe([primary, fallback]);
    }

    [Fact]
    public void Build_MultipleTopLevelInputs_AllAreKeyedIndependently()
    {
        // given several top-level inputs with disjoint subtrees
        InputDefinition aChild = Input("AChild");
        InputDefinition a = Input("A", aChild);
        InputDefinition b = Input("B");

        // when building the index
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<InputDefinition>> index = _underTest.Build([a, b]);

        // then each is its own key with its own descendants list
        index[a].ShouldBe([aChild]);
        index[b].ShouldBeEmpty();
        index[aChild].ShouldBeEmpty();
    }

    [Fact]
    public void Build_TwoStructurallyEqualInputs_AreKeyedSeparatelyByReference()
    {
        // given two distinct InputDefinition instances with the same Name and shape
        InputDefinition a1 = Input("ButtonA");
        InputDefinition a2 = Input("ButtonA");

        // when building the index
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<InputDefinition>> index = _underTest.Build([a1, a2]);

        // then they occupy two separate entries — the index keys on reference, not record value
        index.Keys.ShouldBe([a1, a2], ignoreOrder: true);
    }

    // --- Default branches (unknown ILayoutElement subtypes) ---

    [Fact]
    public void Build_UnknownTopLevelElement_Throws()
    {
        // given an ILayoutElement subtype that PopulateDescendants does not handle
        // when building the index
        // then an InvalidOperationException is thrown naming the unhandled type
        Should.Throw<InvalidOperationException>(() => _underTest.Build([new UnknownElement()]));
    }

    [Fact]
    public void Build_UnknownChildElement_Throws()
    {
        // given an InputDefinition whose child is an unknown ILayoutElement subtype
        // (hits the default branch of CollectDescendants)
        InputDefinition parent = Input("Parent", new UnknownElement());

        // when building the index
        // then an InvalidOperationException is thrown
        Should.Throw<InvalidOperationException>(() => _underTest.Build([parent]));
    }

    private record UnknownElement : ILayoutElement;
}
