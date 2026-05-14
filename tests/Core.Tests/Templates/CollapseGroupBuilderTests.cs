using DynamicControls.Templates;
using static DynamicControls.Core.TestHelpers.Templates.LayoutElements;

namespace DynamicControls.Core.Tests.Templates;

/// <summary>
/// Unit tests for <see cref="CollapseGroupBuilder"/>. The builder walks a collapsing Stack's
/// children to identify slot-level nodes (one slot per InputDefinition, one slot per
/// always-include Group/OneOf, transparent for plain Groups) and then writes a
/// <see cref="CollapseInfo"/> entry to the output dictionary for every InputDefinition leaf
/// reachable from those slots. The shared-list identity inside CollapseInfo.Group is
/// load-bearing — render-time collapse logic compares against that list to vacate or shift slots.
/// </summary>
public class CollapseGroupBuilderTests
{
    private static Dictionary<InputDefinition, CollapseInfo> NewOutput() =>
        new(ReferenceEqualityComparer.Instance);

    [Fact]
    public void Build_NoChildren_DoesNothing()
    {
        // given an empty children list and an empty output dictionary
        Dictionary<InputDefinition, CollapseInfo> output = NewOutput();

        // when the builder runs
        Should.NotThrow(() => CollapseGroupBuilder.Build(children: [], gap: 50, output));

        // then no entries are written
        output.ShouldBeEmpty();
    }

    [Fact]
    public void Build_FlatInputs_EachInputIsOwnSlot_AllShareTheSameGroup()
    {
        // given two top-level Inputs as Stack children
        InputDefinition a = Input("A");
        InputDefinition b = Input("B");
        Dictionary<InputDefinition, CollapseInfo> output = NewOutput();

        // when the builder runs
        CollapseGroupBuilder.Build(children: [a, b], gap: 50, output);

        // then both inputs have an entry pointing at the same shared slot list ([A, B] in order) with gap=50
        output[a].Group.ShouldBe([a, b]);
        output[b].Group.ShouldBeSameAs(output[a].Group);
        output[a].Gap.ShouldBe(50);
        output[b].Gap.ShouldBe(50);
    }

    [Fact]
    public void Build_PlainGroup_IsTransparent_ItsInputsBecomeIndividualSlots()
    {
        // given a Stack containing a plain Group of two Inputs (Group.AlwaysInclude = false)
        InputDefinition a = Input("A");
        InputDefinition b = Input("B");
        InputGroup group = Group(a, b);
        Dictionary<InputDefinition, CollapseInfo> output = NewOutput();

        // when the builder runs
        CollapseGroupBuilder.Build(children: [group], gap: 50, output);

        // then the group does not appear in the slot list; A and B do, as separate slots
        output[a].Group.ShouldBe([a, b]);
        output[b].Group.ShouldBeSameAs(output[a].Group);
    }

    [Fact]
    public void Build_NestedStack_OccupiesOneSlotAsBlock_InputsShareThatSlot()
    {
        // given a Stack containing another Stack (AlwaysInclude = true) of two Inputs
        InputDefinition a = Input("A");
        InputDefinition b = Input("B");
        InputGroup innerStack = Stack(a, b);
        Dictionary<InputDefinition, CollapseInfo> output = NewOutput();

        // when the builder runs
        CollapseGroupBuilder.Build(children: [innerStack], gap: 50, output);

        // then the inner stack is the slot; A and B both reference [innerStack] as their group
        output[a].Group.ShouldBe([innerStack]);
        output[b].Group.ShouldBeSameAs(output[a].Group);
    }

    [Fact]
    public void Build_OneOf_IsOneSlot_AllAlternativeLeavesAreStamped()
    {
        // given a Stack containing a OneOf with two alternative Inputs
        InputDefinition primary = Input("Primary");
        InputDefinition fallback = Input("Fallback");
        OneOf oneOf = OneOf(primary, fallback);
        Dictionary<InputDefinition, CollapseInfo> output = NewOutput();

        // when the builder runs
        CollapseGroupBuilder.Build(children: [oneOf], gap: 50, output);

        // then both alternatives are stamped with the same shared group containing the OneOf
        output[primary].Group.ShouldBe([oneOf]);
        output[fallback].Group.ShouldBeSameAs(output[primary].Group);
        output[primary].Gap.ShouldBe(50);
        output[fallback].Gap.ShouldBe(50);
    }

    [Fact]
    public void Build_MixedSlotTypes_CombineInOrder()
    {
        // given a Stack containing: a bare Input, a plain Group (transparent), and a OneOf
        InputDefinition a = Input("A");
        InputDefinition b = Input("B");
        InputDefinition c = Input("C");
        InputDefinition d = Input("D");
        InputDefinition e = Input("E");
        InputGroup group = Group(b, c);
        OneOf oneOf = OneOf(d, e);
        Dictionary<InputDefinition, CollapseInfo> output = NewOutput();

        // when the builder runs
        CollapseGroupBuilder.Build(children: [a, group, oneOf], gap: 40, output);

        // then the slot list is [A, B, C, OneOf] in document order — Group flattens, OneOf stays whole
        output[a].Group.ShouldBe([a, b, c, oneOf]);

        // and every reachable leaf is stamped with the same shared list and gap
        new[] { a, b, c, d, e }.ShouldAllBe(i => ReferenceEquals(output[i].Group, output[a].Group));
        new[] { a, b, c, d, e }.ShouldAllBe(i => output[i].Gap == 40);
    }

    [Fact]
    public void Build_OneOfWithStackAlternative_StampsDeepLeaves()
    {
        // given a OneOf whose second alternative is a Stack of two Inputs
        InputDefinition primary = Input("Primary");
        InputDefinition fa = Input("FA");
        InputDefinition fb = Input("FB");
        InputGroup fallbackStack = Stack(fa, fb);
        OneOf oneOf = OneOf(primary, fallbackStack);
        Dictionary<InputDefinition, CollapseInfo> output = NewOutput();

        // when the builder runs
        CollapseGroupBuilder.Build(children: [oneOf], gap: 50, output);

        // then leaves inside the Stack-alternative are stamped too (fan-out recurses through groups)
        output[fa].Group.ShouldBe([oneOf]);
        output[fb].Group.ShouldBeSameAs(output[fa].Group);
        output[primary].Group.ShouldBeSameAs(output[fa].Group);
    }

    [Fact]
    public void Build_UnknownChildType_Throws()
    {
        // given a Stack child whose ILayoutElement subtype is not handled by CollectSlots
        Should.Throw<InvalidOperationException>(() =>
            CollapseGroupBuilder.Build(children: [new UnknownElement()], gap: 50, NewOutput()))
            .Message.ShouldContain("UnknownElement");
    }

    [Fact]
    public void Build_UnknownTypeInsideStack_Throws()
    {
        // CollectSlots adds the Stack itself as a slot without inspecting its children;
        // SetMetadata then recurses into the Stack's children and hits the unknown type.
        InputGroup stack = Stack(new UnknownElement());

        Should.Throw<InvalidOperationException>(() =>
            CollapseGroupBuilder.Build(children: [stack], gap: 50, NewOutput()))
            .Message.ShouldContain("UnknownElement");
    }

    private class UnknownElement : ILayoutElement { }
}
