using DynamicControls.Rendering;
using NSubstitute;
using static DynamicControls.Core.TestHelpers.Rendering.RenderingFixtures;

namespace DynamicControls.Core.Tests.Rendering;

/// <summary>
/// Unit tests for <see cref="WholeLabelCollapser"/>, which reconciles a composite control's own
/// label against its parts' in both directions, using the composites the active template
/// declares. Pins both directions and — more importantly — the cases where it must *not* fire:
/// a lone labelled part has nothing to merge with, and parts that genuinely disagree each keep
/// their own text rather than being smoothed into a shared label they never had.
///
/// The composites are passed in the way a template declares them in its <c>&lt;Head&gt;</c>.
/// Tests that mean "an ordinary controller" use <see cref="RenderingFixtures.StandardWholes"/>.
/// </summary>
public class WholeLabelCollapserTests
{
    private readonly WholeLabelCollapser _underTest = new(Substitute.For<ILogger>());

    private IReadOnlyDictionary<string, string> Collapse(
        params (string Name, string Text)[] labels) =>
        _underTest.Collapse(labels.ToDictionary(l => l.Name, l => l.Text), StandardWholes);

    // --- composite to parts ---

    [Fact]
    public void CompositeLabelledWithNoPartsOfItsOwn_CopiesOntoEveryPart()
    {
        // The whole-only shape: a platform button bound straight at the composite, with no
        // separate ports for its directions at all. Gating each direction on its own
        // (nonexistent) label would wrongly dim all four.
        IReadOnlyDictionary<string, string> result = Collapse(("ButtonDpad", "Move"));

        result.ShouldBeDictionaryOf(
            ("ButtonDpad", "Move"),
            ("ButtonDpadUp", "Move"), ("ButtonDpadDown", "Move"),
            ("ButtonDpadLeft", "Move"), ("ButtonDpadRight", "Move"));
    }

    [Fact]
    public void TwoCompositesLabelledIndependently_EachCopiesOntoItsOwnParts()
    {
        // Each composite is reconciled on its own -- one carrying text says nothing about another.
        IReadOnlyDictionary<string, string> result = Collapse(
            ("AxisLeftStick", "Look"),
            ("ButtonDpad", "Move"));

        result.ShouldBeDictionaryOf(
            ("AxisLeftStick", "Look"),
            ("AxisLeftStickUp", "Look"), ("AxisLeftStickDown", "Look"),
            ("AxisLeftStickLeft", "Look"), ("AxisLeftStickRight", "Look"),
            ("ButtonDpad", "Move"),
            ("ButtonDpadUp", "Move"), ("ButtonDpadDown", "Move"),
            ("ButtonDpadLeft", "Move"), ("ButtonDpadRight", "Move"));
    }

    // --- parts to composite ---

    [Fact]
    public void EveryPartAgrees_WritesSharedTextOntoTheComposite()
    {
        IReadOnlyDictionary<string, string> result = Collapse(
            ("ButtonDpadUp", "Move"), ("ButtonDpadDown", "Move"),
            ("ButtonDpadLeft", "Move"), ("ButtonDpadRight", "Move"));

        result["ButtonDpad"].ShouldBe("Move");
    }

    [Fact]
    public void ThreeOfFourPartsAgree_FourthGenuinelyBlank_WritesSharedTextOntoTheComposite()
    {
        // Down has nothing driving it at all -- genuinely blank, not merely labelled differently.
        // The individual entries survive, so the layout's Condition can still tell this apart
        // from "all four agree" by checking whether Down carries anything of its own.
        IReadOnlyDictionary<string, string> result = Collapse(
            ("ButtonDpadUp", "Jump"), ("ButtonDpadLeft", "Jump"), ("ButtonDpadRight", "Jump"));

        result.ShouldBeDictionaryOf(
            ("ButtonDpad", "Jump"),
            ("ButtonDpadUp", "Jump"), ("ButtonDpadLeft", "Jump"), ("ButtonDpadRight", "Jump"));
    }

    // --- where it must not fire ---

    [Fact]
    public void PartsDisagree_LeavesThemAloneAndWritesNothingOntoTheComposite()
    {
        // A real per-direction remap: one direction is driven by an independently labelled
        // button. Merging would paper "Move" over "Jump".
        IReadOnlyDictionary<string, string> result = Collapse(
            ("ButtonDpadUp", "Jump"), ("ButtonDpadDown", "Move"),
            ("ButtonDpadLeft", "Move"), ("ButtonDpadRight", "Move"));

        result.ContainsKey("ButtonDpad").ShouldBeFalse();
        result["ButtonDpadUp"].ShouldBe("Jump");
    }

    [Fact]
    public void OnlyOnePartLabelled_NoMerge_RendersOnItsOwn()
    {
        IReadOnlyDictionary<string, string> result = Collapse(("ButtonDpadUp", "Jump"));

        result.ShouldBeDictionaryOf(("ButtonDpadUp", "Jump"));
    }

    [Fact]
    public void CompositeAndItsPartsBothLabelled_PartsAreNeverOverwritten()
    {
        // Parts present means the composite-to-parts direction does not run at all, so a
        // direction that says something of its own keeps saying it.
        IReadOnlyDictionary<string, string> result = Collapse(
            ("ButtonDpad", "Move"),
            ("ButtonDpadUp", "Jump"), ("ButtonDpadDown", "Jump"));

        result["ButtonDpadUp"].ShouldBe("Jump");
        result["ButtonDpadDown"].ShouldBe("Jump");
        result.ContainsKey("ButtonDpadLeft").ShouldBeFalse();
    }

    // --- the template's own declaration is the source ---

    [Fact]
    public void TemplateDeclaringNoComposites_ReturnsTheLabelsUntouched()
    {
        var labels = new Dictionary<string, string> { ["ButtonDpad"] = "Move" };

        IReadOnlyDictionary<string, string> result = _underTest.Collapse(labels, new Dictionary<string, IReadOnlyList<string>>());

        result.ShouldBeSameAs(labels);
    }

    [Fact]
    public void CompositeTheTemplateDeclaresItsOwnWay_IsReconciledAgainstThoseParts()
    {
        // Nothing here is a name Core knows: the relation comes entirely from the declaration,
        // which is what lets a template describe whatever its chassis actually has.
        var wholes = new Dictionary<string, IReadOnlyList<string>>
        {
            ["C-Cluster"] = ["C-North", "C-South"],
        };

        IReadOnlyDictionary<string, string> result = _underTest.Collapse(
            new Dictionary<string, string> { ["C-North"] = "Camera", ["C-South"] = "Camera" },
            wholes);

        result["C-Cluster"].ShouldBe("Camera");
    }
}
