using DynamicControls.Rendering;
using DynamicControls.Templates;
using static DynamicControls.Core.TestHelpers.InputMapping.MappingFixtures;
using static DynamicControls.Core.TestHelpers.Rendering.RenderingFixtures;

namespace DynamicControls.Core.Tests.Rendering;

/// <summary>
/// Unit tests for <see cref="VisibilityEvaluator"/>. Pins three things:
/// (1) flag fan-out — a parent input's HasLabel/IsMapped flags OR-reduce across its composite's
/// part names, looked up in <see cref="VisibilityContext.Wholes"/> by the input's own name,
/// including the IsMapped fallback through NaturalInputToButton that lets remapped inputs still
/// count as mapped; (2) AnyVisible dispatch — InputDefinitions resolve via their own renders or
/// their structural Children, OneOfs OR across alternatives, Containers have no case of their own
/// (they never decide their own visibility); (3) AllImagesZeroOpacity — the gate the
/// collapse-stack logic uses to decide whether a slot vacates, honouring per-image MinOpacity
/// over the template default.
/// </summary>
public class VisibilityEvaluatorTests
{
    private readonly VisibilityEvaluator _underTest = new();

    // ---- fixtures ----

    private static InputDefinition Input(
        string name,
        IReadOnlyList<InputImageDefinition>? images = null,
        IReadOnlyList<ILayoutElement>? children = null)
    {
        return new(
            Name: name,
            InputImages: images ?? [],
            Overlays: [],
            Labels: [],
            Children: children ?? []);
    }

    private static InputImageDefinition Image(
        ShowIfCondition showIf = ShowIfCondition.Always,
        double? minOpacity = null)
    {
        return new(X: 0, Y: 0, ImageFile: "x.png", ShowIf: showIf, MinOpacity: minOpacity);
    }

    // ---- GetVisibilityFlags ----

    [Fact]
    public void GetVisibilityFlags_NoLabelOrMapping_ReturnsNone()
    {
        // given an input with neither a label nor a mapping
        var input = Input(name: "ButtonA");
        VisibilityContext ctx = Ctx();

        // when flags are evaluated
        VisibilityFlags flags = _underTest.GetVisibilityFlags(input, ctx);

        // then both are false
        flags.ShouldBe(VisibilityFlags.None);
    }

    [Fact]
    public void GetVisibilityFlags_OwnLabel_SetsHasLabel()
    {
        // given an input that has a label of its own
        var input = Input("ButtonA");
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string> { ["ButtonA"] = "Punch" });

        // when flags are evaluated
        VisibilityFlags flags = _underTest.GetVisibilityFlags(input, ctx);

        // then HasLabel is true, IsMapped is false
        flags.HasLabel.ShouldBeTrue();
        flags.IsMapped.ShouldBeFalse();
    }

    [Fact]
    public void GetVisibilityFlags_DescendantHasLabel_PropagatesHasLabelToParent()
    {
        // given a parent input with a labelled descendant — the stick has no label of its own,
        // but its Up direction does. Named for a composite Ctx declares by default, since fan-out
        // comes from the context's Wholes rather than the Children nesting below.
        var stickUp = Input(name: "AxisLeftStickUp");
        var stick = Input(
            name: "AxisLeftStick",
            children: [stickUp]);
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string> { ["AxisLeftStickUp"] = "Steer" });

        // when flags are evaluated for the parent
        VisibilityFlags flags = _underTest.GetVisibilityFlags(stick, ctx);

        // then HasLabel fans out from the descendant — this is what lights a stick up as a unit
        flags.HasLabel.ShouldBeTrue();
    }

    [Fact]
    public void GetVisibilityFlags_InputDirectlyMapped_SetsIsMapped()
    {
        // given an input that a platform button currently drives
        var input = Input(name: "ButtonA");
        VisibilityContext ctx = Ctx(
            mapping: MappingOf(inputToButton: new Dictionary<string, string> { ["ButtonA"] = "A" }));

        // when flags are evaluated
        VisibilityFlags flags = _underTest.GetVisibilityFlags(input, ctx);

        // then IsMapped is true
        flags.IsMapped.ShouldBeTrue();
    }

    [Fact]
    public void GetVisibilityFlags_RemappedButNaturalButtonStillPresent_SetsIsMapped()
    {
        // given an input that isn't currently mapped, but its natural physical button is still
        // present in the mapping (driving some other input) — the on-screen slot still belongs
        // to a real button, so it should count as mapped
        var input = Input(name: "ButtonA");
        VisibilityContext ctx = Ctx(
            mapping: MappingOf(
                inputToButton: new Dictionary<string, string> { ["ButtonB"] = "A" },
                naturalInputToButton: new Dictionary<string, string> { ["ButtonA"] = "A" },
                buttonToInput: new Dictionary<string, IReadOnlyList<string>> { ["A"] = ["ButtonB"] }));

        // when flags are evaluated
        VisibilityFlags flags = _underTest.GetVisibilityFlags(input, ctx);

        // then IsMapped is true via the natural-button fallback — this is the remap-survival path
        flags.IsMapped.ShouldBeTrue();
    }

    [Fact]
    public void GetVisibilityFlags_RemappedAndNaturalButtonAbsent_ClearsIsMapped()
    {
        // given an input whose natural button has been removed from the current mapping
        // entirely (e.g. a controller without that button)
        var input = Input(name: "ButtonA");
        VisibilityContext ctx = Ctx(
            mapping: MappingOf(
                naturalInputToButton: new Dictionary<string, string> { ["ButtonA"] = "A" }));

        // when flags are evaluated
        VisibilityFlags flags = _underTest.GetVisibilityFlags(input, ctx);

        // then IsMapped is false — the slot has no physical button behind it
        flags.IsMapped.ShouldBeFalse();
    }

    // ---- AnyVisible dispatch ----

    [Fact]
    public void AnyVisible_InputWithVisibleRender_ReturnsTrue()
    {
        // given an input with an Always-visible image
        var input = Input(name: "ButtonA", images: [Image(ShowIfCondition.Always)]);
        VisibilityContext ctx = Ctx();

        // when AnyVisible runs
        bool visible = _underTest.AnyVisible(input, ctx);

        // then it passes through the input's own render
        visible.ShouldBeTrue();
    }

    [Fact]
    public void AnyVisible_InputWithoutRenders_ButMappedDescendant_ReturnsTrue()
    {
        // given an input with no renders of its own, but a child that's mapped. Named for a
        // composite Ctx declares by default, since that is where fan-out reads its parts.
        var child = Input(
            name: "AxisLeftStickUp",
            images: [Image(ShowIfCondition.Mapped)]);
        var parent = Input(
            name: "AxisLeftStick",
            children: [child]);
        VisibilityContext ctx = Ctx(
            mapping: MappingOf(
                inputToButton: new Dictionary<string, string> { ["AxisLeftStickUp"] = "Up" }));

        // when AnyVisible runs on the parent
        bool visible = _underTest.AnyVisible(parent, ctx);

        // then the dispatch descends into the child
        visible.ShouldBeTrue();
    }

    [Fact]
    public void AnyVisible_OneOfWithOneVisibleAlternative_ReturnsTrue()
    {
        // given a OneOf with one hidden and one visible alternative
        var hidden = Input(
            name: "ButtonX",
            images: [Image(ShowIfCondition.Mapped)]);
        var shown = Input(
            name: "ButtonY",
            images: [Image(ShowIfCondition.Always)]);
        var oneOf = new OneOf(Alternatives: [hidden, shown]);
        VisibilityContext ctx = Ctx();

        // when AnyVisible runs on the OneOf
        bool visible = _underTest.AnyVisible(oneOf, ctx);

        // then the OR across alternatives carries it (note: AnyVisible only asks "any" — the
        // first-match-wins selection happens in LayoutFilter, not here)
        visible.ShouldBeTrue();
    }

    [Fact]
    public void AnyVisible_Container_IsTrueWhateverItHolds_SoAOneOfCanUseOneAsItsDefault()
    {
        // A Container states no condition and always renders once reached, so there is nothing
        // for it to answer no to. Answering yes is what lets a OneOf use a bare one as a
        // trailing default -- try the conditional alternatives, otherwise draw this -- where
        // answering no made it silently unselectable in the one place anything asks.
        //
        // Unconditional, not derived from its children: alternatives holding the same inputs and
        // differing only in when they apply cannot be told apart by content, so a content-based
        // answer would pick the first of them every time. That is <Condition>'s job, and
        // inferring it from descendants is what <Group> did and Container exists to drop. Both
        // cases below answer the same.
        var withVisibleChild = new Container(
            Children: [Input(name: "ButtonY", images: [Image(ShowIfCondition.Always)])], Overlays: []);
        var empty = new Container(Children: [], Overlays: []);
        VisibilityContext ctx = Ctx();

        _underTest.AnyVisible(withVisibleChild, ctx).ShouldBeTrue();
        _underTest.AnyVisible(empty, ctx).ShouldBeTrue();
    }

    [Fact]
    public void AnyVisible_AutoShowIfWithLabelAndGameSpecific_ReturnsTrue()
    {
        // given an input whose only render is ShowIf=Auto, with a label and no mapping,
        // and the game contributed its own labels
        var input = Input(
            name: "ButtonA",
            images: [Image(ShowIfCondition.Auto)]);
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string> { ["ButtonA"] = "Punch" },
            isGameSpecific: true);

        // when AnyVisible runs
        bool visible = _underTest.AnyVisible(input, ctx);

        // then Auto behaves like Label — the labelled render passes
        visible.ShouldBeTrue();
    }

    [Fact]
    public void AnyVisible_AutoShowIfWithLabelButNotGameSpecific_ReturnsFalse()
    {
        // given an input whose only render is ShowIf=Auto, with a label (purely inherited
        // platform default, not game-supplied) and no mapping
        var input = Input(
            name: "ButtonA",
            images: [Image(ShowIfCondition.Auto)]);
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string> { ["ButtonA"] = "Punch" },
            isGameSpecific: false);

        // when AnyVisible runs
        bool visible = _underTest.AnyVisible(input, ctx);

        // then Auto behaves like Mapped — without a mapping the render fails
        visible.ShouldBeFalse();
    }

    [Fact]
    public void AnyVisible_OneOfWithAllHiddenAlternatives_ReturnsFalse()
    {
        // given a OneOf where every alternative needs a mapping and none is present
        var a = Input(name: "ButtonA", images: [Image(ShowIfCondition.Mapped)]);
        var b = Input(name: "ButtonB", images: [Image(ShowIfCondition.Mapped)]);
        var oneOf = new OneOf(Alternatives: [a, b]);
        VisibilityContext ctx = Ctx();

        // when AnyVisible runs
        bool visible = _underTest.AnyVisible(oneOf, ctx);

        // then nothing passes
        visible.ShouldBeFalse();
    }

    // ---- AnyVisible: Condition ----

    [Fact]
    public void AnyVisible_ConditionAll_TrueOnlyWhenEveryNameHasLabel()
    {
        VisibilityContext all = Ctx(labelText: new Dictionary<string, string> { ["A"] = "Jump", ["B"] = "Jump" });
        VisibilityContext partial = Ctx(labelText: new Dictionary<string, string> { ["A"] = "Jump" });
        var condition = new ConditionElement(ConditionMode.AtLeast, ["A", "B"], ConditionMatch.Label, []);

        _underTest.AnyVisible(condition, all).ShouldBeTrue();
        _underTest.AnyVisible(condition, partial).ShouldBeFalse();
    }

    [Fact]
    public void AnyVisible_ConditionAny_TrueWhenAtLeastOneNameHasLabel()
    {
        VisibilityContext ctx = Ctx(labelText: new Dictionary<string, string> { ["A"] = "Jump" });
        var condition = new ConditionElement(ConditionMode.AtLeast, ["A", "B"], ConditionMatch.Label, [], AtLeast: 1);

        _underTest.AnyVisible(condition, ctx).ShouldBeTrue();
    }

    [Fact]
    public void AnyVisible_ConditionAny_FalseWhenNoNameHasLabel()
    {
        VisibilityContext ctx = Ctx();
        var condition = new ConditionElement(ConditionMode.AtLeast, ["A", "B"], ConditionMatch.Label, [], AtLeast: 1);

        _underTest.AnyVisible(condition, ctx).ShouldBeFalse();
    }

    [Fact]
    public void AnyVisible_ConditionNone_TrueOnlyWhenNoNameHasLabel()
    {
        VisibilityContext empty = Ctx();
        VisibilityContext oneLabelled = Ctx(labelText: new Dictionary<string, string> { ["A"] = "Jump" });
        var condition = new ConditionElement(ConditionMode.None, ["A", "B"], ConditionMatch.Label, []);

        _underTest.AnyVisible(condition, empty).ShouldBeTrue();
        _underTest.AnyVisible(condition, oneLabelled).ShouldBeFalse();
    }

    [Fact]
    public void AnyVisible_ConditionMatchMapped_ChecksMappingNotLabel()
    {
        // labelled but not mapped — a Label-match Condition would pass, a Mapped-match one shouldn't
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string> { ["A"] = "Jump" },
            mapping: EmptyMapping());
        var condition = new ConditionElement(ConditionMode.AtLeast, ["A"], ConditionMatch.Mapped, [], AtLeast: 1);

        _underTest.AnyVisible(condition, ctx).ShouldBeFalse();
    }

    [Fact]
    public void AnyVisible_ConditionMatchMapped_TrueWhenInputToButtonHasIt()
    {
        VisibilityContext ctx = Ctx(mapping: MappingOf(inputToButton: new Dictionary<string, string> { ["A"] = "BUTTON1" }));
        var condition = new ConditionElement(ConditionMode.AtLeast, ["A"], ConditionMatch.Mapped, [], AtLeast: 1);

        _underTest.AnyVisible(condition, ctx).ShouldBeTrue();
    }

    // Regression guard: match="auto" used to be unrecognized (only label/mapping were valid),
    // silently defaulting to label -- which stranded a Condition gating a ROM with no labels at
    // all (not game-specific, no defaults either), even when the named input was mapped fine.
    [Fact]
    public void AnyVisible_ConditionMatchAuto_NotGameSpecific_ChecksMappingNotLabel()
    {
        // given a name that's labelled but not mapped, and default (non-game-specific) labels --
        // Auto behaves like Mapped here, so a Label-only match would wrongly pass
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string> { ["A"] = "Jump" },
            mapping: EmptyMapping(),
            isGameSpecific: false);
        var condition = new ConditionElement(ConditionMode.AtLeast, ["A"], ConditionMatch.Auto, [], AtLeast: 1);

        _underTest.AnyVisible(condition, ctx).ShouldBeFalse();
    }

    [Fact]
    public void AnyVisible_ConditionMatchAuto_NotGameSpecific_TrueWhenMapped()
    {
        // the exact real-world case this fixes: no labels anywhere, but the name is mapped
        VisibilityContext ctx = Ctx(
            mapping: MappingOf(inputToButton: new Dictionary<string, string> { ["A"] = "BUTTON1" }),
            isGameSpecific: false);
        var condition = new ConditionElement(ConditionMode.AtLeast, ["A"], ConditionMatch.Auto, [], AtLeast: 1);

        _underTest.AnyVisible(condition, ctx).ShouldBeTrue();
    }

    [Fact]
    public void AnyVisible_ConditionMatchAuto_GameSpecific_ChecksLabelNotMapping()
    {
        // given a name that's mapped but not labelled, with game-specific labels present --
        // Auto behaves like Label here, so a Mapped-only match would wrongly pass
        VisibilityContext ctx = Ctx(
            mapping: MappingOf(inputToButton: new Dictionary<string, string> { ["A"] = "BUTTON1" }),
            isGameSpecific: true);
        var condition = new ConditionElement(ConditionMode.AtLeast, ["A"], ConditionMatch.Auto, [], AtLeast: 1);

        _underTest.AnyVisible(condition, ctx).ShouldBeFalse();
    }

    [Fact]
    public void AnyVisible_ConditionMatchAuto_GameSpecific_TrueWhenLabelled()
    {
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string> { ["A"] = "Jump" },
            mapping: EmptyMapping(),
            isGameSpecific: true);
        var condition = new ConditionElement(ConditionMode.AtLeast, ["A"], ConditionMatch.Auto, [], AtLeast: 1);

        _underTest.AnyVisible(condition, ctx).ShouldBeTrue();
    }

    [Fact]
    public void AnyVisible_ConditionAll_EmptyNamesList_ReturnsFalse()
    {
        // an "all" Condition with no names would otherwise vacuously pass (LINQ All on an empty
        // sequence is true) — guarded explicitly since a template author almost certainly meant
        // something else if they left the list empty
        var condition = new ConditionElement(ConditionMode.AtLeast, [], ConditionMatch.Label, []);

        _underTest.AnyVisible(condition, Ctx()).ShouldBeFalse();
    }

    [Fact]
    public void AnyVisible_ConditionDoesNotConsultItsOwnChildrenVisibility_OnlyTheNamedCheck()
    {
        // given a Condition whose named check passes but whose only child would itself be
        // invisible (needs a mapping that isn't present) -- Condition's own AnyVisible must not
        // fold that in, since LayoutFilter renders whatever's inside once the gate passes,
        // dimmed or not, same as any other included subtree
        var child = Input(name: "Unrelated", images: [Image(ShowIfCondition.Mapped)]);
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string> { ["Whole"] = "Move" });
        var condition = new ConditionElement(ConditionMode.AtLeast, ["Whole"], ConditionMatch.Label, [child], AtLeast: 1);

        _underTest.AnyVisible(condition, ctx).ShouldBeTrue();
    }

    [Fact]
    public void AnyVisible_NestedCondition_OuterPassesButInnerFails_ReturnsFalse()
    {
        // given the compound-AND idiom -- an outer Condition ("the whole has a label") wrapping
        // an inner one ("all four directions individually agree") -- where only the outer passes.
        // A OneOf choosing between alternatives relies on AnyVisible to know whether this
        // alternative has anything to show; if it only consulted the outer check it would pick
        // this alternative and then render nothing once the inner check drops everything inside,
        // stranding the slot instead of falling through to a later alternative.
        var glyph = Input(name: "Whole", images: [Image(ShowIfCondition.Label)]);
        var inner = new ConditionElement(ConditionMode.AtLeast, ["Up", "Down", "Left", "Right"], ConditionMatch.Label, [glyph]);
        var outer = new ConditionElement(ConditionMode.AtLeast, ["Whole"], ConditionMatch.Label, [inner], AtLeast: 1);
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string> { ["Whole"] = "Steering", ["Left"] = "Steering", ["Right"] = "Steering" });

        _underTest.AnyVisible(outer, ctx).ShouldBeFalse();
    }

    [Fact]
    public void AnyVisible_NestedCondition_OuterAndInnerBothPass_ReturnsTrue()
    {
        // the same compound-AND shape, but all four directions genuinely agree -- both checks
        // pass, so the nested Condition alternative correctly reports itself visible
        var glyph = Input(name: "Whole", images: [Image(ShowIfCondition.Label)]);
        var inner = new ConditionElement(ConditionMode.AtLeast, ["Up", "Down", "Left", "Right"], ConditionMatch.Label, [glyph]);
        var outer = new ConditionElement(ConditionMode.AtLeast, ["Whole"], ConditionMatch.Label, [inner], AtLeast: 1);
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string>
            {
                ["Whole"] = "Move",
                ["Up"] = "Move",
                ["Down"] = "Move",
                ["Left"] = "Move",
                ["Right"] = "Move",
            });

        _underTest.AnyVisible(outer, ctx).ShouldBeTrue();
    }

    [Fact]
    public void AnyVisible_UnknownElement_ReturnsFalse()
    {
        // given an ILayoutElement subtype that none of the switch arms handle
        // when AnyVisible runs
        bool visible = _underTest.AnyVisible(new UnknownElement(), Ctx());

        // then the default arm returns false
        visible.ShouldBeFalse();
    }

    private record UnknownElement : ILayoutElement;

    // ---- AggregateFlags ----

    [Fact]
    public void AggregateFlags_ContainerWithLabelledChild_ReturnsHasLabel()
    {
        // given a group whose only child has a label
        var child = Input(name: "ButtonA");
        var group = new Container(Children: [child], Overlays: []);
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string> { ["ButtonA"] = "Punch" });

        VisibilityFlags result = _underTest.AggregateFlags(group, ctx);

        result.HasLabel.ShouldBeTrue();
        result.IsMapped.ShouldBeFalse();
    }

    [Fact]
    public void AggregateFlags_ContainerMultipleChildren_OrReducesFlags()
    {
        // given a group with one labelled child and one mapped child
        var labelled = Input(name: "ButtonA");
        var mapped = Input(name: "ButtonB");
        var group = new Container(Children: [labelled, mapped], Overlays: []);
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string> { ["ButtonA"] = "Punch" },
            mapping: MappingOf(inputToButton: new Dictionary<string, string> { ["ButtonB"] = "X" }));

        VisibilityFlags result = _underTest.AggregateFlags(group, ctx);

        result.HasLabel.ShouldBeTrue();
        result.IsMapped.ShouldBeTrue();
    }

    [Fact]
    public void AggregateFlags_InputDefinitionWithStructuralChildren_RecursesIntoThem()
    {
        // given a group whose InputDefinition child itself has a structural child that's mapped —
        // Walk must recurse into the parent's Children list to collect the grandchild's flags
        var grandchild = Input(name: "AxisLeft");
        var parent = Input(name: "LeftStick", children: [grandchild]);
        var group = new Container(Children: [parent], Overlays: []);
        VisibilityContext ctx = Ctx(
            mapping: MappingOf(inputToButton: new Dictionary<string, string> { ["AxisLeft"] = "Left" }));

        VisibilityFlags result = _underTest.AggregateFlags(group, ctx);

        // grandchild's IsMapped flag reaches the result only via Walk's recursive Children loop
        result.IsMapped.ShouldBeTrue();
    }

    [Fact]
    public void AggregateFlags_OneOfPicksFirstVisibleAlternative()
    {
        // given a group containing a OneOf — only the first visible alternative contributes flags
        var invisible = Input(name: "ButtonX");  // no images → AnyRenderVisible false
        var visible = Input(name: "ButtonY", images: [Image(ShowIfCondition.Always)]);
        var oneOf = new OneOf(Alternatives: [invisible, visible]);
        var group = new Container(Children: [oneOf], Overlays: []);
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string> { ["ButtonY"] = "Jump" });

        VisibilityFlags result = _underTest.AggregateFlags(group, ctx);

        // ButtonY (visible) contributes HasLabel; ButtonX (invisible) is skipped entirely
        result.HasLabel.ShouldBeTrue();
        result.IsMapped.ShouldBeFalse();
    }

    [Fact]
    public void AggregateFlags_OneOfWithNoVisibleAlternative_ContributesNothing()
    {
        // given a group containing a OneOf where no alternative is visible (no images on either)
        var a = Input(name: "ButtonA");
        var b = Input(name: "ButtonB");
        var oneOf = new OneOf(Alternatives: [a, b]);
        var group = new Container(Children: [oneOf], Overlays: []);
        VisibilityContext ctx = Ctx();

        VisibilityFlags result = _underTest.AggregateFlags(group, ctx);

        result.ShouldBe(VisibilityFlags.None);
    }

    [Fact]
    public void AggregateFlags_UnknownElement_Throws()
    {
        // given an ILayoutElement subtype that Walk has no case for
        Should.Throw<InvalidOperationException>(() =>
            _underTest.AggregateFlags(new UnknownElement(), Ctx()));
    }

    // ---- AllImagesZeroOpacity ----

    [Fact]
    public void AllImagesZeroOpacity_InputHasNoImages_ReturnsTrue()
    {
        // given an input with no images at all (e.g. a label-only slot)
        var input = Input(name: "ButtonA");
        VisibilityContext ctx = Ctx();

        // when the collapse gate runs
        bool result = _underTest.AllImagesZeroOpacity(input, defaultMinOpacity: 1.0, ctx);

        // then it counts as zero-opacity — a labels-only slot can vacate in a collapsing stack
        result.ShouldBeTrue();
    }

    [Fact]
    public void AllImagesZeroOpacity_VisibleImage_ReturnsFalse()
    {
        // given an input with an Always-visible image
        var input = Input(
            name: "ButtonA",
            images: [Image(ShowIfCondition.Always)]);
        VisibilityContext ctx = Ctx();

        // when the collapse gate runs
        bool result = _underTest.AllImagesZeroOpacity(input, defaultMinOpacity: 0, ctx);

        // then the slot stays occupied
        result.ShouldBeFalse();
    }

    [Fact]
    public void AllImagesZeroOpacity_HiddenImageWithImageMinOpacityZero_ReturnsTrue()
    {
        // given an invisible image authored with MinOpacity=0 (so it disappears entirely)
        var input = Input("ButtonA",
            images: [Image(ShowIfCondition.Mapped, minOpacity: 0)]);
        VisibilityContext ctx = Ctx();

        // when the collapse gate runs with a positive template default (proving it isn't used)
        bool result = _underTest.AllImagesZeroOpacity(input, defaultMinOpacity: 0.5, ctx);

        // then the slot vacates — the image's own MinOpacity wins over the template default
        result.ShouldBeTrue();
    }

    [Fact]
    public void AllImagesZeroOpacity_HiddenImageWithoutImageMinOpacity_UsesTemplateDefault()
    {
        // given an invisible image that doesn't set its own MinOpacity
        var input = Input(
            name: "ButtonA",
            images: [Image(ShowIfCondition.Mapped)]);
        VisibilityContext ctx = Ctx();

        // when the template default is positive, the slot stays
        _underTest.AllImagesZeroOpacity(input, defaultMinOpacity: 0.3, ctx)
            .ShouldBeFalse();

        // but when the template default is zero, the slot vacates
        _underTest.AllImagesZeroOpacity(input, defaultMinOpacity: 0, ctx)
            .ShouldBeTrue();
    }

    [Fact]
    public void AllImagesZeroOpacity_OneVisibleAndOneHiddenAtZero_ReturnsFalse()
    {
        // given two images: one hidden with MinOpacity=0, one fully visible
        var hidden = Image(
            showIf: ShowIfCondition.Mapped,
            minOpacity: 0);
        var visible = Image(showIf: ShowIfCondition.Always);
        var input = Input(
            name: "ButtonA",
            images: [hidden, visible]);
        VisibilityContext ctx = Ctx();

        // when the collapse gate runs
        bool result = _underTest.AllImagesZeroOpacity(input, defaultMinOpacity: 0, ctx);

        // then "all" means all — one visible image keeps the slot occupied
        result.ShouldBeFalse();
    }

    // ---- Condition: match="agree" ----
    //
    // Counting cannot separate four directions each saying something different from four all
    // saying the same thing: both leave every name labelled, and they are drawn completely
    // differently -- four labels of their own against one merged one. "agree" says the second
    // directly, and the "rest carry none" clause is what stops a direction with genuinely
    // different text being smoothed into a shared label it never had.

    private static ConditionElement Agree(int atLeast, params string[] names) =>
        new(ConditionMode.AtLeast, names, ConditionMatch.Agree, [], AtLeast: atLeast);

    private static VisibilityContext Labelled(params (string Name, string Text)[] labels) =>
        Ctx(labelText: labels.ToDictionary(l => l.Name, l => l.Text));

    [Fact]
    public void Agree_EveryNameCarriesTheSameText_Passes()
    {
        VisibilityContext ctx = Labelled(
            ("AxisLeftStickUp", "Move"), ("AxisLeftStickDown", "Move"),
            ("AxisLeftStickLeft", "Move"), ("AxisLeftStickRight", "Move"));

        _underTest.AnyVisible(Agree(0, "AxisLeftStick"), ctx).ShouldBeTrue();
    }

    [Fact]
    public void Agree_NamesCarryDifferentText_Fails()
    {
        // Four labels, same as the agreeing case -- only the text tells them apart.
        VisibilityContext ctx = Labelled(
            ("AxisLeftStickUp", "Up"), ("AxisLeftStickDown", "Down"),
            ("AxisLeftStickLeft", "Left"), ("AxisLeftStickRight", "Right"));

        _underTest.AnyVisible(Agree(0, "AxisLeftStick"), ctx).ShouldBeFalse();
    }

    [Fact]
    public void Agree_NoAtLeast_RequiresEveryPart()
    {
        // The composite expands to its parts and the threshold defaults to all of them, so three
        // agreeing directions are not enough for the single-glyph alternative.
        VisibilityContext ctx = Labelled(
            ("AxisLeftStickUp", "Move"), ("AxisLeftStickDown", "Move"),
            ("AxisLeftStickLeft", "Move"));

        _underTest.AnyVisible(Agree(0, "AxisLeftStick"), ctx).ShouldBeFalse();
    }

    [Fact]
    public void Agree_AtLeastTwo_EnoughAgreeAndTheRestAreBlank_Passes()
    {
        VisibilityContext ctx = Labelled(
            ("AxisLeftStickUp", "Move"), ("AxisLeftStickDown", "Move"));

        _underTest.AnyVisible(Agree(2, "AxisLeftStick"), ctx).ShouldBeTrue();
    }

    [Fact]
    public void Agree_AtLeastTwo_EnoughAgreeButAnotherDisagrees_Fails()
    {
        // The "rest carry none" clause: Left has its own text, so there is no shared label here
        // and merging would paper over a real per-direction remap.
        VisibilityContext ctx = Labelled(
            ("AxisLeftStickUp", "Move"), ("AxisLeftStickDown", "Move"),
            ("AxisLeftStickLeft", "Fire"));

        _underTest.AnyVisible(Agree(2, "AxisLeftStick"), ctx).ShouldBeFalse();
    }

    [Fact]
    public void Agree_FewerCarryTextThanRequired_Fails()
    {
        VisibilityContext ctx = Labelled(("AxisLeftStickUp", "Move"));

        _underTest.AnyVisible(Agree(2, "AxisLeftStick"), ctx).ShouldBeFalse();
    }

    [Fact]
    public void Agree_NoneCarryAnyText_Fails()
    {
        _underTest.AnyVisible(Agree(2, "AxisLeftStick"), Ctx()).ShouldBeFalse();
    }

    [Fact]
    public void Agree_OnNamesTheTemplateDoesNotCallAComposite_ChecksThoseNamesThemselves()
    {
        // A name with no <Whole> declaration stands for itself, so this asks about the two
        // buttons named and nothing else.
        VisibilityContext ctx = Labelled(("ButtonA", "Kick"), ("ButtonB", "Kick"));

        _underTest.AnyVisible(Agree(0, "ButtonA", "ButtonB"), ctx).ShouldBeTrue();
    }

    // ---- a composite is whatever the context says it is ----
    //
    // Every other test here takes Ctx's default set of composites, which mirrors the shipped
    // template's, so the familiar names can read as though the evaluator recognises them. It does
    // not: it looks the Input's own name up in VisibilityContext.Wholes and expands whatever it
    // finds. These two say that in names the engine has never seen.

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Paddle =
        new Dictionary<string, IReadOnlyList<string>> { ["Paddle"] = ["Paddle-Twist", "Paddle-Push"] };

    [Fact]
    public void GetVisibilityFlags_FansOutAcrossACompositeTheEngineHasNeverSeen()
    {
        var paddle = Input(name: "Paddle");
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string> { ["Paddle-Twist"] = "Steer" },
            wholes: Paddle);

        _underTest.GetVisibilityFlags(paddle, ctx).HasLabel.ShouldBeTrue();
    }

    [Fact]
    public void GetVisibilityFlags_NameTheContextDoesNotDeclare_IsOnlyItself()
    {
        // The same name, with nothing declaring it a composite: its parts' labels say nothing
        // about it, so a template that omits the declaration gets no fan-out at all.
        var paddle = Input(name: "Paddle");
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string> { ["Paddle-Twist"] = "Steer" },
            wholes: new Dictionary<string, IReadOnlyList<string>>());

        _underTest.GetVisibilityFlags(paddle, ctx).HasLabel.ShouldBeFalse();
    }

    [Fact]
    public void GetVisibilityFlags_CompositeBoundDirectly_IsNotMapped_ItsPartsDecide()
    {
        // A declared composite is exactly its parts, and its own name is not also checked. A
        // platform that binds a button straight at the composite is naming something only the
        // template declares, so it would light up on templates declaring that <Whole> and sit
        // dead on every other one — Controllers.xml would depend on a vocabulary it does not own.
        // Binding the directions, which every layer speaks, needs no such agreement.
        var paddle = Input(name: "Paddle");
        VisibilityContext ctx = Ctx(
            mapping: MappingOf(inputToButton: new Dictionary<string, string> { ["Paddle"] = "Twist" }),
            wholes: Paddle);

        _underTest.GetVisibilityFlags(paddle, ctx).IsMapped.ShouldBeFalse();
    }

    [Fact]
    public void GetVisibilityFlags_CompositeWhosePartIsBound_IsMapped()
    {
        // The form that does work, and the one every shipped platform uses: an <Alias> reaches
        // the directions, so the composite follows from them whatever the template calls it.
        var paddle = Input(name: "Paddle");
        VisibilityContext ctx = Ctx(
            mapping: MappingOf(inputToButton: new Dictionary<string, string> { ["Paddle-Twist"] = "Twist" }),
            wholes: Paddle);

        _underTest.GetVisibilityFlags(paddle, ctx).IsMapped.ShouldBeTrue();
    }

    [Fact]
    public void Condition_ExpandsACompositeTheEngineHasNeverSeen()
    {
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string>
            {
                ["Paddle-Twist"] = "Steer",
                ["Paddle-Push"] = "Steer",
            },
            wholes: Paddle);

        _underTest.AnyVisible(Agree(0, "Paddle"), ctx).ShouldBeTrue();
    }
}
