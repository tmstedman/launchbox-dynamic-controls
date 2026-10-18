using DynamicControls.Templates;
using static DynamicControls.Core.TestHelpers.Templates.LayoutElements;

namespace DynamicControls.Core.Tests.Templates;

/// <summary>
/// Unit tests for <see cref="InputDescendantsBuilder"/>. The builder keys every
/// <see cref="InputDefinition"/> reachable in the tree to its whole-control part names (from
/// <see cref="DynamicControls.InputMapping.WholeInputs.PartsOf"/>, by the Input's own name) —
/// tree shape decides *which* Inputs get an entry (treating <see cref="Container"/> and
/// <see cref="OneOf"/> as transparent containers, traversed but not keyed), but never *what* that
/// entry contains. The map uses reference equality so structurally identical instances stay
/// distinct.
/// </summary>
public class InputDescendantsBuilderTests
{
    private readonly InputDescendantsBuilder _underTest = new();

    [Fact]
    public void Build_EmptyList_ReturnsEmptyIndex()
    {
        // given no top-level elements
        // when building the index
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<string>> index = _underTest.Build([]);

        // then the result is an empty map
        index.ShouldBeEmpty();
    }

    [Fact]
    public void Build_InputWhoseNameIsNotAWhole_MapsToEmptyList()
    {
        // given a single Input whose name isn't one of WholeInputs.PartsOf's keys
        InputDefinition input = Input("ButtonA");

        // when building the index
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<string>> index = _underTest.Build([input]);

        // then it's keyed with an empty descendants list
        index.Keys.ShouldBe([input]);
        index[input].ShouldBeEmpty();
    }

    [Fact]
    public void Build_InputNamedForAWhole_MapsToItsPartNames()
    {
        // given a bare, childless Input named after one of WholeInputs.PartsOf's wholes — the
        // direction Inputs are its siblings in Layout.xml, not its structural children
        InputDefinition dpad = Input("ButtonDpad");

        // when building the index
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<string>> index = _underTest.Build([dpad]);

        // then its descendants are the whole's part names, sourced purely from the name — no
        // sibling Input named "ButtonDpadUp" etc. needs to exist in this tree at all
        index[dpad].ShouldBe(["ButtonDpadUp", "ButtonDpadDown", "ButtonDpadLeft", "ButtonDpadRight"]);
    }

    [Fact]
    public void Build_NestedInputs_DoNotFoldChildNamesIntoParent()
    {
        // given a three-level Input nesting: outer > middle > inner, none of them a whole
        InputDefinition inner = Input("Inner");
        InputDefinition middle = Input("Middle", inner);
        InputDefinition outer = Input("Outer", middle);

        // when building the index
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<string>> index = _underTest.Build([outer]);

        // then every input is keyed, but nesting itself contributes nothing to any entry --
        // fan-out is a fact about whole names now, not tree shape
        index[outer].ShouldBeEmpty();
        index[middle].ShouldBeEmpty();
        index[inner].ShouldBeEmpty();
    }

    [Fact]
    public void Build_Container_IsTransparent_NotKeyed_ButChildrenAre()
    {
        // given a top-level Container wrapping two Inputs
        InputDefinition a = Input("A");
        InputDefinition b = Input("B");
        Container group = Container(a, b);

        // when building the index
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<string>> index = _underTest.Build([group]);

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
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<string>> index = _underTest.Build([oneOf]);

        // then only the alternatives are keyed
        index.Keys.ShouldBe([primary, fallback], ignoreOrder: true);
    }

    [Fact]
    public void Build_ContainerNestedUnderInput_ContainerMembersAreStillKeyedIndependently()
    {
        // given an Input whose Children list contains a Container of two Inputs (transparent container)
        InputDefinition a = Input("A");
        InputDefinition b = Input("B");
        InputDefinition parent = Input("Parent", Container(a, b));

        // when building the index
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<string>> index = _underTest.Build([parent]);

        // then the group's members get their own entries, but the parent's own entry is
        // unaffected by their presence -- "Parent" isn't a whole name
        index.Keys.ShouldBe([parent, a, b], ignoreOrder: true);
        index[parent].ShouldBeEmpty();
    }

    [Fact]
    public void Build_OneOfNestedUnderInput_AllAlternativesAreStillKeyedIndependently()
    {
        // given an Input with a OneOf child containing two alternatives
        InputDefinition primary = Input("Primary");
        InputDefinition fallback = Input("Fallback");
        InputDefinition parent = Input("Parent", OneOf(primary, fallback));

        // when building the index
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<string>> index = _underTest.Build([parent]);

        // then both alternatives are keyed, independent of which one would fire at render time --
        // the index is structural discovery only, never runtime-evaluated
        index.Keys.ShouldBe([parent, primary, fallback], ignoreOrder: true);
    }

    [Fact]
    public void Build_MultipleTopLevelInputs_AllAreKeyedIndependently()
    {
        // given several top-level inputs with disjoint subtrees, none of them wholes
        InputDefinition aChild = Input("AChild");
        InputDefinition a = Input("A", aChild);
        InputDefinition b = Input("B");

        // when building the index
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<string>> index = _underTest.Build([a, b]);

        // then each is its own key with its own (empty) descendants list
        index[a].ShouldBeEmpty();
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
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<string>> index = _underTest.Build([a1, a2]);

        // then they occupy two separate entries — the index keys on reference, not record value
        index.Keys.ShouldBe([a1, a2], ignoreOrder: true);
    }

    [Fact]
    public void Build_TwoInputsSharingAWholeName_BothGetTheSamePartNames()
    {
        // given the "strict-self render position" pattern -- the same whole name appearing twice
        // in the tree as distinct instances (e.g. a duplicate top-level glyph alongside the
        // direction cluster's own OneOf)
        InputDefinition glyph = Input("AxisLeftStick");
        InputDefinition duplicate = Input("AxisLeftStick");

        // when building the index
        IReadOnlyDictionary<InputDefinition, IReadOnlyList<string>> index = _underTest.Build([glyph, duplicate]);

        // then both instances independently resolve the same part names from the shared name
        index[glyph].ShouldBe(["AxisLeftStickUp", "AxisLeftStickDown", "AxisLeftStickLeft", "AxisLeftStickRight"]);
        index[duplicate].ShouldBe(index[glyph]);
    }

    // --- Default branches (unknown ILayoutElement subtypes) ---

    [Fact]
    public void Build_UnknownTopLevelElement_Throws()
    {
        // given an ILayoutElement subtype that Collect does not handle
        // when building the index
        // then an InvalidOperationException is thrown naming the unhandled type
        Should.Throw<InvalidOperationException>(() => _underTest.Build([new UnknownElement()]));
    }

    [Fact]
    public void Build_UnknownChildElement_Throws()
    {
        // given an InputDefinition whose child is an unknown ILayoutElement subtype
        // (hits the default branch of Collect)
        InputDefinition parent = Input("Parent", new UnknownElement());

        // when building the index
        // then an InvalidOperationException is thrown
        Should.Throw<InvalidOperationException>(() => _underTest.Build([parent]));
    }

    private record UnknownElement : ILayoutElement;
}
