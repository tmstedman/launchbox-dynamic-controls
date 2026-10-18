using DynamicControls.Templates;
using static DynamicControls.Core.TestHelpers.Templates.LayoutElements;

namespace DynamicControls.Core.Tests.Templates;

/// <summary>
/// Unit tests for <see cref="CollapseGroupBuilder"/>. The builder walks a collapsing container's
/// children to identify slot-level nodes (one slot per InputDefinition, one slot per nested
/// Container/OneOf) and then writes a <see cref="CollapseInfo"/> entry to the output dictionary
/// for every InputDefinition leaf reachable from those slots — including leaves nested inside a
/// Container or OneOf slot, which SetMetadata recurses through regardless. The shared-list
/// identity inside CollapseInfo.Group is load-bearing — render-time collapse logic compares
/// against that list to vacate or shift slots.
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
        // given two top-level Inputs as Group children
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
        // and VAlign defaults to "top" when the caller doesn't pass one (every pre-existing caller)
        output[a].VAlign.ShouldBe("top");
    }

    [Fact]
    public void Build_VAlignPassedByCaller_FlowsThroughToCollapseInfo()
    {
        // given a Group whose LayoutResolver already validated vAlign="bottom"
        InputDefinition a = Input("A");
        Dictionary<InputDefinition, CollapseInfo> output = NewOutput();

        // when the builder runs with that value
        CollapseGroupBuilder.Build(children: [a], gap: 50, output, vAlign: "bottom");

        // then it lands on the CollapseInfo entry, for LayoutFilter's render-time correction
        output[a].VAlign.ShouldBe("bottom");
    }

    [Fact]
    public void Build_NestedGroup_OccupiesOneSlotAsBlock_InputsShareThatSlot()
    {
        // given a Group containing another Group of two Inputs
        InputDefinition a = Input("A");
        InputDefinition b = Input("B");
        Container inner = Container(a, b);
        Dictionary<InputDefinition, CollapseInfo> output = NewOutput();

        // when the builder runs
        CollapseGroupBuilder.Build(children: [inner], gap: 50, output);

        // then the inner group is the slot; A and B both reference [inner] as their group
        output[a].Group.ShouldBe([inner]);
        output[b].Group.ShouldBeSameAs(output[a].Group);
    }

    [Fact]
    public void Build_OneOf_IsOneSlot_AllAlternativeLeavesAreStamped()
    {
        // given a Group containing a OneOf with two alternative Inputs
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
        // given a Group containing: a bare Input, a nested Group of two Inputs, and a OneOf
        InputDefinition a = Input("A");
        InputDefinition b = Input("B");
        InputDefinition c = Input("C");
        InputDefinition d = Input("D");
        InputDefinition e = Input("E");
        Container group = Container(b, c);
        OneOf oneOf = OneOf(d, e);
        Dictionary<InputDefinition, CollapseInfo> output = NewOutput();

        // when the builder runs
        CollapseGroupBuilder.Build(children: [a, group, oneOf], gap: 40, output);

        // then the slot list is [A, Group, OneOf] in document order — both Group and OneOf stay whole
        output[a].Group.ShouldBe([a, group, oneOf]);

        // and every reachable leaf, including B and C nested inside the Group slot, is stamped
        // with the same shared list and gap
        new[] { a, b, c, d, e }.ShouldAllBe(i => ReferenceEquals(output[i].Group, output[a].Group));
        new[] { a, b, c, d, e }.ShouldAllBe(i => output[i].Gap == 40);
    }

    [Fact]
    public void Build_OneOfWithGroupAlternative_StampsDeepLeaves()
    {
        // given a OneOf whose second alternative is a Group of two Inputs
        InputDefinition primary = Input("Primary");
        InputDefinition fa = Input("FA");
        InputDefinition fb = Input("FB");
        Container fallbackGroup = Container(fa, fb);
        OneOf oneOf = OneOf(primary, fallbackGroup);
        Dictionary<InputDefinition, CollapseInfo> output = NewOutput();

        // when the builder runs
        CollapseGroupBuilder.Build(children: [oneOf], gap: 50, output);

        // then leaves inside the Group alternative are stamped too (fan-out recurses through groups)
        output[fa].Group.ShouldBe([oneOf]);
        output[fb].Group.ShouldBeSameAs(output[fa].Group);
        output[primary].Group.ShouldBeSameAs(output[fa].Group);
    }

    [Fact]
    public void Build_UnknownChildType_Throws()
    {
        // given a top-level child whose ILayoutElement subtype is not handled by CollectSlots
        Should.Throw<InvalidOperationException>(() =>
            CollapseGroupBuilder.Build(children: [new UnknownElement()], gap: 50, NewOutput()))
            .Message.ShouldContain("UnknownElement");
    }

    [Fact]
    public void Build_UnknownTypeInsideGroup_Throws()
    {
        // CollectSlots adds the Group itself as a slot without inspecting its children;
        // SetMetadata then recurses into the Group's children and hits the unknown type.
        Container group = Container(new UnknownElement());

        Should.Throw<InvalidOperationException>(() =>
            CollapseGroupBuilder.Build(children: [group], gap: 50, NewOutput()))
            .Message.ShouldContain("UnknownElement");
    }

    private class UnknownElement : ILayoutElement { }
}
