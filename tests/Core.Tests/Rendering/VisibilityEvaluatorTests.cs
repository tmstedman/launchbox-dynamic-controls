using DynamicControls.InputMapping;
using DynamicControls.Rendering;
using DynamicControls.Templates;
using static DynamicControls.Core.TestHelpers.InputMapping.MappingFixtures;
using static DynamicControls.Core.TestHelpers.Rendering.RenderingFixtures;

namespace DynamicControls.Core.Tests.Rendering;

/// <summary>
/// Unit tests for <see cref="VisibilityEvaluator"/>. Pins three things:
/// (1) flag fan-out — a parent input's HasLabel/IsMapped flags OR-reduce across its structural
/// descendants, including the IsMapped fallback through NaturalInputToButton that lets remapped
/// inputs still count as mapped; (2) AnyVisible dispatch — InputDefinitions resolve via their
/// own renders or their descendants, InputGroups OR across children, OneOfs OR across
/// alternatives; (3) AllImagesZeroOpacity — the gate the collapse-stack logic uses to decide
/// whether a slot vacates, honouring per-image MinOpacity over the template default.
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
        VisibilityContext ctx = Ctx(
            descendants: Descendants((input, [])));

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
            labelText: new Dictionary<string, string> { ["ButtonA"] = "Punch" },
            descendants: Descendants((input, [])));

        // when flags are evaluated
        VisibilityFlags flags = _underTest.GetVisibilityFlags(input, ctx);

        // then HasLabel is true, IsMapped is false
        flags.HasLabel.ShouldBeTrue();
        flags.IsMapped.ShouldBeFalse();
    }

    [Fact]
    public void GetVisibilityFlags_DescendantHasLabel_PropagatesHasLabelToParent()
    {
        // given a parent input with a labelled descendant — Stick has no label of its own, but
        // StickUp does
        var stickUp = Input(name: "AxisLeftStickUp");
        var stick = Input(
            name: "LeftStick",
            children: [stickUp]);
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string> { ["AxisLeftStickUp"] = "Steer" },
            descendants: Descendants(
                (stick, [stickUp]),
                (stickUp, [])));

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
            mapping: MappingOf(inputToButton: new Dictionary<string, string> { ["ButtonA"] = "A" }),
            descendants: Descendants((input, [])));

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
                buttonToInput: new Dictionary<string, IReadOnlyList<string>> { ["A"] = ["ButtonB"] }),
            descendants: Descendants((input, [])));

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
                naturalInputToButton: new Dictionary<string, string> { ["ButtonA"] = "A" }),
            descendants: Descendants((input, [])));

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
        VisibilityContext ctx = Ctx(descendants: Descendants((input, [])));

        // when AnyVisible runs
        bool visible = _underTest.AnyVisible(input, ctx);

        // then it passes through the input's own render
        visible.ShouldBeTrue();
    }

    [Fact]
    public void AnyVisible_InputWithoutRenders_ButMappedDescendant_ReturnsTrue()
    {
        // given an input with no renders of its own, but a child that's mapped
        var child = Input(
            name: "AxisLeftStickUp",
            images: [Image(ShowIfCondition.Mapped)]);
        var parent = Input(
            name: "LeftStick",
            children: [child]);
        VisibilityContext ctx = Ctx(
            mapping: MappingOf(
                inputToButton: new Dictionary<string, string> { ["AxisLeftStickUp"] = "Up" }),
            descendants: Descendants(
                (parent, [child]),
                (child, [])));

        // when AnyVisible runs on the parent
        bool visible = _underTest.AnyVisible(parent, ctx);

        // then the dispatch descends into the child
        visible.ShouldBeTrue();
    }

    [Fact]
    public void AnyVisible_GroupWithOneVisibleChild_ReturnsTrue()
    {
        // given a group with one hidden and one visible child
        var hidden = Input(
            name: "ButtonX",
            images: [Image(ShowIfCondition.Mapped)]);
        var shown = Input(
            name: "ButtonY",
            images: [Image(ShowIfCondition.Always)]);
        var group = new InputGroup(
            AlwaysInclude: false,
            Children: [hidden, shown],
            Overlays: []);
        VisibilityContext ctx = Ctx(descendants: Descendants(
            (hidden, []),
            (shown, [])));

        // when AnyVisible runs on the group
        bool visible = _underTest.AnyVisible(group, ctx);

        // then the OR across children carries it
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
        VisibilityContext ctx = Ctx(descendants: Descendants(
            (hidden, []),
            (shown, [])));

        // when AnyVisible runs on the OneOf
        bool visible = _underTest.AnyVisible(oneOf, ctx);

        // then the OR across alternatives carries it (note: AnyVisible only asks "any" — the
        // first-match-wins selection happens in LayoutFilter, not here)
        visible.ShouldBeTrue();
    }

    [Fact]
    public void AnyVisible_GroupWithAllHiddenChildren_ReturnsFalse()
    {
        // given a group whose every child needs a mapping, and no mapping exists
        var a = Input(
            name: "ButtonA",
            images: [Image(ShowIfCondition.Mapped)]);
        var b = Input(
            name: "ButtonB",
            images: [Image(ShowIfCondition.Mapped)]);
        var group = new InputGroup(
            AlwaysInclude: false,
            Children: [a, b],
            Overlays: []);
        VisibilityContext ctx = Ctx(descendants: Descendants(
            (a, []),
            (b, [])));

        // when AnyVisible runs
        bool visible = _underTest.AnyVisible(group, ctx);

        // then nothing passes
        visible.ShouldBeFalse();
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
            isGameSpecific: true,
            descendants: Descendants((input, [])));

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
            isGameSpecific: false,
            descendants: Descendants((input, [])));

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
        VisibilityContext ctx = Ctx(descendants: Descendants((a, []), (b, [])));

        // when AnyVisible runs
        bool visible = _underTest.AnyVisible(oneOf, ctx);

        // then nothing passes
        visible.ShouldBeFalse();
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
    public void AggregateFlags_InputGroupWithLabelledChild_ReturnsHasLabel()
    {
        // given a group whose only child has a label
        var child = Input(name: "ButtonA");
        var group = new InputGroup(AlwaysInclude: false, Children: [child], Overlays: []);
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string> { ["ButtonA"] = "Punch" },
            descendants: Descendants((child, [])));

        VisibilityFlags result = _underTest.AggregateFlags(group, ctx);

        result.HasLabel.ShouldBeTrue();
        result.IsMapped.ShouldBeFalse();
    }

    [Fact]
    public void AggregateFlags_InputGroupMultipleChildren_OrReducesFlags()
    {
        // given a group with one labelled child and one mapped child
        var labelled = Input(name: "ButtonA");
        var mapped = Input(name: "ButtonB");
        var group = new InputGroup(AlwaysInclude: false, Children: [labelled, mapped], Overlays: []);
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string> { ["ButtonA"] = "Punch" },
            mapping: MappingOf(inputToButton: new Dictionary<string, string> { ["ButtonB"] = "X" }),
            descendants: Descendants((labelled, []), (mapped, [])));

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
        var group = new InputGroup(AlwaysInclude: false, Children: [parent], Overlays: []);
        VisibilityContext ctx = Ctx(
            mapping: MappingOf(inputToButton: new Dictionary<string, string> { ["AxisLeft"] = "Left" }),
            descendants: Descendants((parent, []), (grandchild, [])));

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
        var group = new InputGroup(AlwaysInclude: false, Children: [oneOf], Overlays: []);
        VisibilityContext ctx = Ctx(
            labelText: new Dictionary<string, string> { ["ButtonY"] = "Jump" },
            descendants: Descendants((invisible, []), (visible, [])));

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
        var group = new InputGroup(AlwaysInclude: false, Children: [oneOf], Overlays: []);
        VisibilityContext ctx = Ctx(descendants: Descendants((a, []), (b, [])));

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
        VisibilityContext ctx = Ctx(descendants: Descendants((input, [])));

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
        VisibilityContext ctx = Ctx(descendants: Descendants((input, [])));

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
        VisibilityContext ctx = Ctx(descendants: Descendants((input, [])));

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
        VisibilityContext ctx = Ctx(descendants: Descendants((input, [])));

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
        VisibilityContext ctx = Ctx(descendants: Descendants((input, [])));

        // when the collapse gate runs
        bool result = _underTest.AllImagesZeroOpacity(input, defaultMinOpacity: 0, ctx);

        // then "all" means all — one visible image keeps the slot occupied
        result.ShouldBeFalse();
    }
}
