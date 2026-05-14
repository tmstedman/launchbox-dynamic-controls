using DynamicControls.Rendering;
using DynamicControls.Templates;
using NSubstitute;
using static DynamicControls.Core.TestHelpers.Rendering.RenderingFixtures;
using static DynamicControls.Core.TestHelpers.Templates.TemplateFixtures;

namespace DynamicControls.Core.Tests.Rendering;

/// <summary>
/// Unit tests for <see cref="LayoutFilter"/>. <see cref="IVisibilityEvaluator"/> is a substitute
/// — each scenario states its visibility decisions directly on the evaluator stub rather than
/// encoding them as image ShowIf + MinOpacity values that the reader would have to translate
/// back into "this slot is visible / vacates." The filter's own work (group inclusion, OneOf
/// selection, collapse-offset accumulation) is what's being verified; the evaluator's behavior
/// is covered by <see cref="VisibilityEvaluatorTests"/>, and their integration by
/// <c>InputRenderingSubsystemTests</c>. Pins three things: (1) structural selection — top-level
/// InputDefinitions and their structural Children always render, InputGroups gate on
/// any-child-visible (unless AlwaysInclude), OneOfs pick the first visible alternative;
/// (2) group overlays — included only when the group is included, tagged with the group's
/// aggregated visibility flags; (3) collapse adjustments — zero-opacity slots vacate and shift
/// later slots up by the stack's gap, including the OneOf-no-visible case.
/// </summary>
public class LayoutFilterTests
{
    private readonly IVisibilityEvaluator _evaluator = Substitute.For<IVisibilityEvaluator>();
    private readonly LayoutFilter _underTest;

    public LayoutFilterTests()
    {
        _underTest = new LayoutFilter(_evaluator);
    }

    // ---- fixtures ----

    private static InputDefinition Input(
        string name,
        IReadOnlyList<OverlayDefinition>? overlays = null,
        IReadOnlyList<ILayoutElement>? children = null) =>
        new(
            Name: name,
            InputImages: [],
            Overlays: overlays ?? [],
            Labels: [],
            Children: children ?? []);

    private static InputGroup Group(
        bool alwaysInclude,
        IReadOnlyList<ILayoutElement> children,
        IReadOnlyList<OverlayDefinition>? overlays = null) =>
        new(AlwaysInclude: alwaysInclude, Children: children, Overlays: overlays ?? []);

    private static OverlayDefinition Overlay(string source = "overlay.png") =>
        new(X: 0, Y: 0, Source: source);

    // Stands in for any future ILayoutElement subtype that the switch-on-type guards haven't been updated to handle.
    private record UnknownElement : ILayoutElement;

    // ---- selection ----

    [Fact]
    public void Filter_EmptyLayout_ReturnsEmpty()
    {
        // given a template with no elements
        Template template = TemplateOf();

        // when filtering
        FilteredLayout result = _underTest.Filter(template, Ctx());

        // then both lists are empty
        result.Inputs.ShouldBeEmpty();
        result.GroupOverlays.ShouldBeEmpty();
    }

    [Fact]
    public void Filter_TopLevelInput_AlwaysRenders()
    {
        // given a top-level InputDefinition
        InputDefinition input = Input("ButtonA");
        Template template = TemplateOf(elements: [input]);

        // when filtering
        FilteredLayout result = _underTest.Filter(template, Ctx());

        // then the input renders — top-level inputs aren't gated by visibility
        result.Inputs.Select(i => i.Input).ShouldBe([input]);
        result.Inputs[0].YOffset.ShouldBe(0);
    }

    [Fact]
    public void Filter_InputWithStructuralChildren_RendersParentThenChildren()
    {
        // given a parent InputDefinition with a structural child (e.g. a stick with an axis sub-input)
        InputDefinition child = Input("AxisLeftStickUp");
        InputDefinition parent = Input("LeftStick", children: [child]);
        Template template = TemplateOf(elements: [parent]);

        // when filtering
        FilteredLayout result = _underTest.Filter(template, Ctx());

        // then both render in document order — parent first, then descendant
        result.Inputs.Select(i => i.Input.Name).ShouldBe(["LeftStick", "AxisLeftStickUp"]);
    }

    // ---- groups ----

    [Fact]
    public void Filter_GroupWithNoVisibleChildren_DropsGroupAndMembers()
    {
        // given a plain group whose only child is invisible (default substitute return)
        InputDefinition gated = Input("ButtonA");
        InputGroup group = Group(alwaysInclude: false, children: [gated]);
        Template template = TemplateOf(elements: [group]);

        // when filtering
        FilteredLayout result = _underTest.Filter(template, Ctx());

        // then the group drops and its members never enter the render list
        result.Inputs.ShouldBeEmpty();
    }

    [Fact]
    public void Filter_GroupWithOneVisibleChild_IncludesGroupAndAllMembers()
    {
        // given a plain group with one visible and one invisible member
        InputDefinition visible = Input("ButtonA");
        InputDefinition gated = Input("ButtonB");
        InputGroup group = Group(alwaysInclude: false, children: [visible, gated]);
        Template template = TemplateOf(elements: [group]);
        _evaluator.AnyVisible(visible, Arg.Any<VisibilityContext>()).Returns(true);

        // when filtering
        FilteredLayout result = _underTest.Filter(template, Ctx());

        // then the group's any-visible test passes and every member renders (the gated one
        // included — gating happens later via opacity, not here)
        result.Inputs.Select(i => i.Input.Name).ShouldBe(["ButtonA", "ButtonB"]);
    }

    [Fact]
    public void Filter_GroupAlwaysInclude_RendersMembersEvenWithNoneVisible()
    {
        // given an AlwaysInclude group (a <Stack>) whose only child is invisible
        InputDefinition gated = Input("ButtonA");
        InputGroup stack = Group(alwaysInclude: true, children: [gated]);
        Template template = TemplateOf(elements: [stack]);

        // when filtering (evaluator.AnyVisible never consulted for AlwaysInclude)
        FilteredLayout result = _underTest.Filter(template, Ctx());

        // then the member still renders — AlwaysInclude bypasses the any-visible check
        result.Inputs.Select(i => i.Input.Name).ShouldBe(["ButtonA"]);
    }

    [Fact]
    public void Filter_GroupOverlays_IncludedWhenGroupVisibleWithAggregatedFlags()
    {
        // given a group with one mapped child and a group-level overlay
        InputDefinition mapped = Input("ButtonA");
        OverlayDefinition overlay = Overlay("dpad-lines.png");
        InputGroup group = Group(alwaysInclude: false, children: [mapped], overlays: [overlay]);
        Template template = TemplateOf(elements: [group]);
        _evaluator.AnyVisible(mapped, Arg.Any<VisibilityContext>()).Returns(true);
        _evaluator.AggregateFlags(group, Arg.Any<VisibilityContext>())
            .Returns(new VisibilityFlags(HasLabel: false, IsMapped: true));

        // when filtering
        FilteredLayout result = _underTest.Filter(template, Ctx());

        // then the overlay is included and tagged with the flags AggregateFlags returned
        result.GroupOverlays.Select(o => o.Overlay).ShouldBe([overlay]);
        result.GroupOverlays[0].Flags.IsMapped.ShouldBeTrue();
    }

    [Fact]
    public void Filter_GroupOverlays_DroppedWhenGroupExcluded()
    {
        // given an invisible group with a group-level overlay
        InputDefinition gated = Input("ButtonA");
        InputGroup group = Group(alwaysInclude: false, children: [gated], overlays: [Overlay()]);
        Template template = TemplateOf(elements: [group]);

        // when filtering
        FilteredLayout result = _underTest.Filter(template, Ctx());

        // then the overlay drops with the group
        result.GroupOverlays.ShouldBeEmpty();
    }

    // ---- OneOf ----

    [Fact]
    public void Filter_OneOf_PicksFirstVisibleAlternativeAndDropsTheRest()
    {
        // given a OneOf where the first alternative is hidden, the second is visible, and the
        // third is also visible — only the second should render
        InputDefinition hidden = Input("ButtonX");
        InputDefinition shown = Input("ButtonY");
        InputDefinition alsoShown = Input("ButtonZ");
        var oneOf = new OneOf(Alternatives: [hidden, shown, alsoShown]);
        Template template = TemplateOf(elements: [oneOf]);
        _evaluator.AnyVisible(shown, Arg.Any<VisibilityContext>()).Returns(true);
        _evaluator.AnyVisible(alsoShown, Arg.Any<VisibilityContext>()).Returns(true);

        // when filtering
        FilteredLayout result = _underTest.Filter(template, Ctx());

        // then only the first visible alternative renders
        result.Inputs.Select(i => i.Input.Name).ShouldBe(["ButtonY"]);
    }

    [Fact]
    public void Filter_OneOf_NoVisibleAlternative_RendersNothing()
    {
        // given a OneOf whose every alternative is invisible (default substitute return)
        InputDefinition a = Input("ButtonA");
        InputDefinition b = Input("ButtonB");
        var oneOf = new OneOf(Alternatives: [a, b]);
        Template template = TemplateOf(elements: [oneOf]);

        // when filtering
        FilteredLayout result = _underTest.Filter(template, Ctx());

        // then nothing from the OneOf renders
        result.Inputs.ShouldBeEmpty();
    }

    // ---- collapse adjustments ----

    [Fact]
    public void Filter_CollapseStack_VisibleSlots_StayAtZeroOffset()
    {
        // given a collapsing stack of two slots, neither vacating
        InputDefinition slot0 = Input("ButtonA");
        InputDefinition slot1 = Input("ButtonB");
        ILayoutElement[] group = [slot0, slot1];
        var collapseInfo = new Dictionary<InputDefinition, CollapseInfo>(ReferenceEqualityComparer.Instance)
        {
            [slot0] = new(group, Gap: 100),
            [slot1] = new(group, Gap: 100),
        };
        Template template = TemplateOf(elements: [slot0, slot1], collapseInfo: collapseInfo);

        // when filtering (AllImagesZeroOpacity default false for both)
        FilteredLayout result = _underTest.Filter(template, Ctx());

        // then no slot vacates, so every offset stays at zero
        result.Inputs.Select(i => i.YOffset).ShouldBe([0.0, 0.0]);
    }

    [Fact]
    public void Filter_CollapseStack_ZeroOpacitySlot_ShiftsLaterSlotsUp()
    {
        // given a collapsing stack where slot0 vacates (zero-opacity), slot1 and slot2 stay
        InputDefinition slot0 = Input("ButtonA");
        InputDefinition slot1 = Input("ButtonB");
        InputDefinition slot2 = Input("ButtonC");
        ILayoutElement[] group = [slot0, slot1, slot2];
        var collapseInfo = new Dictionary<InputDefinition, CollapseInfo>(ReferenceEqualityComparer.Instance)
        {
            [slot0] = new(group, Gap: 100),
            [slot1] = new(group, Gap: 100),
            [slot2] = new(group, Gap: 100),
        };
        Template template = TemplateOf(elements: [slot0, slot1, slot2], collapseInfo: collapseInfo);
        _evaluator.AllImagesZeroOpacity(slot0, Arg.Any<double>(), Arg.Any<VisibilityContext>()).Returns(true);

        // when filtering
        FilteredLayout result = _underTest.Filter(template, Ctx());

        // then slot0 sits at 0 (vacates), and every later slot shifts up by one gap — offsets
        // are accumulators, applied at the slot that follows the vacancy
        result.Inputs.Select(i => (i.Input.Name, i.YOffset))
            .ShouldBe([("ButtonA", 0.0), ("ButtonB", -100.0), ("ButtonC", -100.0)]);
    }

    [Fact]
    public void Filter_CollapseStack_OneOfWithVisibleAlternative_AssignsOffset()
    {
        // given a collapsing stack where the first slot is a OneOf with a visible, non-zero-opacity
        // alternative, followed by a tail slot
        InputDefinition altA = Input("ButtonA");
        InputDefinition tail = Input("ButtonB");
        var oneOf = new OneOf(Alternatives: [altA]);
        ILayoutElement[] group = [oneOf, tail];
        var collapseInfo = new Dictionary<InputDefinition, CollapseInfo>(ReferenceEqualityComparer.Instance)
        {
            [altA] = new(group, Gap: 50),
            [tail] = new(group, Gap: 50),
        };
        Template template = TemplateOf(elements: [oneOf, tail], collapseInfo: collapseInfo);
        _evaluator.AnyVisible(altA, Arg.Any<VisibilityContext>()).Returns(true);
        // AllImagesZeroOpacity defaults false — altA stays visible and the slot does not vacate

        // when filtering
        FilteredLayout result = _underTest.Filter(template, Ctx());

        // then altA gets offset 0 (the foreach assigns it), and the slot does not vacate so tail
        // also stays at 0
        result.Inputs.Select(i => (i.Input.Name, i.YOffset))
            .ShouldBe([("ButtonA", 0.0), ("ButtonB", 0.0)]);
    }

    [Fact]
    public void Filter_CollapseStack_OneOfWithVisibleButZeroOpacityAlternative_VacatesSlot()
    {
        // given a collapsing stack where the first slot is a OneOf whose visible alternative is
        // zero-opacity — it enters the render list but then AllImagesZeroOpacity fires
        InputDefinition altA = Input("ButtonA");
        InputDefinition tail = Input("ButtonB");
        var oneOf = new OneOf(Alternatives: [altA]);
        ILayoutElement[] group = [oneOf, tail];
        var collapseInfo = new Dictionary<InputDefinition, CollapseInfo>(ReferenceEqualityComparer.Instance)
        {
            [altA] = new(group, Gap: 50),
            [tail] = new(group, Gap: 50),
        };
        Template template = TemplateOf(elements: [oneOf, tail], collapseInfo: collapseInfo);
        _evaluator.AnyVisible(altA, Arg.Any<VisibilityContext>()).Returns(true);
        _evaluator.AllImagesZeroOpacity(altA, Arg.Any<double>(), Arg.Any<VisibilityContext>()).Returns(true);

        // when filtering
        FilteredLayout result = _underTest.Filter(template, Ctx());

        // then altA still gets an offset entry (from the foreach), but the slot vacates because
        // all selected leaves are zero-opacity — tail shifts up by the gap
        result.Inputs.Select(i => (i.Input.Name, i.YOffset))
            .ShouldBe([("ButtonA", 0.0), ("ButtonB", -50.0)]);
    }

    [Fact]
    public void Filter_CollapseStack_InputGroupSlot_IsSkippedAndAdjacentSlotsUnaffected()
    {
        // given a collapsing stack whose first slot is an InputGroup (Stack-as-slot — a known
        // gap: adjustments for these are not yet computed), followed by a plain slot
        InputDefinition child = Input("ButtonA");
        InputGroup inputGroupSlot = Group(alwaysInclude: true, children: [child]);
        InputDefinition plain = Input("ButtonB");
        ILayoutElement[] group = [inputGroupSlot, plain];
        var collapseInfo = new Dictionary<InputDefinition, CollapseInfo>(ReferenceEqualityComparer.Instance)
        {
            [child] = new(group, Gap: 50),
            [plain] = new(group, Gap: 50),
        };
        Template template = TemplateOf(elements: [inputGroupSlot, plain], collapseInfo: collapseInfo);

        FilteredLayout result = _underTest.Filter(template, Ctx());

        // the InputGroup slot is skipped without throwing; the cumulative offset is never
        // modified, so the plain slot following it stays at zero
        result.Inputs.Select(i => (i.Input.Name, i.YOffset))
            .ShouldBe([("ButtonA", 0.0), ("ButtonB", 0.0)]);
    }

    [Fact]
    public void Filter_CollapseStack_OneOfWithNoVisibleAlternative_VacatesSlot()
    {
        // given a collapsing stack where the first slot is a OneOf whose every alternative is
        // invisible, followed by a visible slot
        InputDefinition altA = Input("ButtonA");
        InputDefinition altB = Input("ButtonB");
        var oneOf = new OneOf(Alternatives: [altA, altB]);
        InputDefinition tail = Input("ButtonC");
        ILayoutElement[] group = [oneOf, tail];
        var collapseInfo = new Dictionary<InputDefinition, CollapseInfo>(ReferenceEqualityComparer.Instance)
        {
            // collapse metadata is keyed by leaf inputs, including the OneOf's alternatives
            [altA] = new(group, Gap: 50),
            [altB] = new(group, Gap: 50),
            [tail] = new(group, Gap: 50),
        };
        Template template = TemplateOf(elements: [oneOf, tail], collapseInfo: collapseInfo);

        // when filtering (every AnyVisible defaults false → OneOf picks nothing, slot vacates)
        FilteredLayout result = _underTest.Filter(template, Ctx());

        // then the OneOf slot vacates and the tail shifts up by one gap
        result.Inputs.Select(i => (i.Input.Name, i.YOffset))
            .ShouldBe([("ButtonC", -50.0)]);
    }

    [Fact]
    public void Filter_CollapseStack_OneOfSlotWithInputGroupAlternative_CollectsLeavesFromGroup()
    {
        // given a collapse group whose first slot is a OneOf whose only alternative is an
        // InputGroup — CollectInputLeaves must recurse into the InputGroup to find the leaf
        // InputDefinitions (the InputGroup arm of CollectInputLeaves)
        InputDefinition inner = Input("Inner");
        InputGroup groupAlt = Group(alwaysInclude: true, children: [inner]);
        var oneOf = new OneOf(Alternatives: [groupAlt]);
        InputDefinition tail = Input("Tail");
        ILayoutElement[] collapseGroup = [oneOf, tail];
        var collapseInfo = new Dictionary<InputDefinition, CollapseInfo>(ReferenceEqualityComparer.Instance)
        {
            [inner] = new(collapseGroup, Gap: 50),
            [tail] = new(collapseGroup, Gap: 50),
        };
        // groupAlt in the main elements so its leaf (inner) enters inputsToRender — the
        // collapse group's oneOf slot is not in the main elements, only in CollapseInfo.Group
        Template template = TemplateOf(elements: [groupAlt, tail], collapseInfo: collapseInfo);

        // when filtering
        FilteredLayout result = _underTest.Filter(template, Ctx());

        // then inner (leaf inside the InputGroup alternative) is found and assigned offset 0;
        // slot does not vacate so tail is also at 0
        result.Inputs.Select(i => (i.Input.Name, i.YOffset))
            .ShouldBe([("Inner", 0.0), ("Tail", 0.0)]);
    }

    [Fact]
    public void Filter_CollapseStack_OneOfSlotWithNestedOneOfAlternative_CollectsLeavesRecursively()
    {
        // given a collapse group whose first slot is a OneOf whose alternative is itself a
        // nested OneOf — CollectInputLeaves must recurse through the nested OneOf to find the
        // leaf InputDefinition (the OneOf arm of CollectInputLeaves)
        InputDefinition leaf = Input("Leaf");
        var nestedOneOf = new OneOf(Alternatives: [leaf]);
        var outerOneOf = new OneOf(Alternatives: [nestedOneOf]);
        InputDefinition tail = Input("Tail");
        ILayoutElement[] collapseGroup = [outerOneOf, tail];
        var collapseInfo = new Dictionary<InputDefinition, CollapseInfo>(ReferenceEqualityComparer.Instance)
        {
            [leaf] = new(collapseGroup, Gap: 50),
            [tail] = new(collapseGroup, Gap: 50),
        };
        // leaf and tail are top-level so they enter inputsToRender; the nested OneOf
        // structure lives only in CollapseInfo.Group, not in the main elements
        Template template = TemplateOf(elements: [leaf, tail], collapseInfo: collapseInfo);

        // when filtering
        FilteredLayout result = _underTest.Filter(template, Ctx());

        // then leaf (reached by recursing through two OneOf layers) is assigned offset 0;
        // slot does not vacate so tail is also at 0
        result.Inputs.Select(i => (i.Input.Name, i.YOffset))
            .ShouldBe([("Leaf", 0.0), ("Tail", 0.0)]);
    }

    // ---- defensive throws ----

    [Fact]
    public void Filter_UnknownTopLevelElement_Throws()
    {
        // given a template whose element list contains an ILayoutElement subtype that
        // CollectVisibleElement has no case for
        Template template = TemplateOf(elements: [new UnknownElement()],
            inputDescendants: new Dictionary<InputDefinition, IReadOnlyList<InputDefinition>>());

        // when filtering
        // then the defensive default in CollectVisibleElement throws
        Should.Throw<InvalidOperationException>(() => _underTest.Filter(template, Ctx()));
    }

    [Fact]
    public void Filter_CollapseStack_UnknownSlotType_Throws()
    {
        // given a collapse group whose slot list contains an ILayoutElement subtype that
        // ComputeCollapseAdjustments has no case for — the unknown element sits only in
        // CollapseInfo.Group so CollectVisibleElement never encounters it
        InputDefinition input = Input("ButtonA");
        ILayoutElement[] collapseGroup = [input, new UnknownElement()];
        var collapseInfo = new Dictionary<InputDefinition, CollapseInfo>(ReferenceEqualityComparer.Instance)
        {
            [input] = new(collapseGroup, Gap: 50),
        };
        Template template = TemplateOf(elements: [input], collapseInfo: collapseInfo);

        // when filtering
        // then the defensive default in ComputeCollapseAdjustments throws
        Should.Throw<InvalidOperationException>(() => _underTest.Filter(template, Ctx()));
    }

    [Fact]
    public void Filter_CollapseStack_OneOfSlotWithUnknownAlternativeType_Throws()
    {
        // given a collapse group whose OneOf slot has an alternative of an unrecognised
        // ILayoutElement subtype — CollectInputLeaves hits its defensive default throw
        InputDefinition input = Input("ButtonA");
        var oneOf = new OneOf(Alternatives: [new UnknownElement()]);
        ILayoutElement[] collapseGroup = [oneOf, input];
        var collapseInfo = new Dictionary<InputDefinition, CollapseInfo>(ReferenceEqualityComparer.Instance)
        {
            [input] = new(collapseGroup, Gap: 50),
        };
        Template template = TemplateOf(elements: [input], collapseInfo: collapseInfo);

        // when filtering
        // then the defensive default in CollectInputLeaves throws
        Should.Throw<InvalidOperationException>(() => _underTest.Filter(template, Ctx()));
    }
}
