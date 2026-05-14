using DynamicControls.Rendering;
using DynamicControls.Templates;
using NSubstitute;
using LayoutElements = DynamicControls.Core.TestHelpers.Templates.LayoutElements;

namespace DynamicControls.Core.Tests.Rendering;

/// <summary>
/// Unit tests for <see cref="InputLabelRenderer"/>. Covers the empty-input short-circuit, the
/// alignment → (left-offset, alignment-string) translation, the baseline-adjusted Y, and the
/// fact that <see cref="RenderedLabel"/>'s text/input metadata is carried through unchanged.
/// </summary>
public class InputLabelRendererTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly InputLabelRenderer _underTest;

    public InputLabelRendererTests()
    {
        _underTest = new InputLabelRenderer(_logger);
    }

    private static InputDefinition Input(string name, params LabelDefinition[] labels) =>
        LayoutElements.Input(name) with { Labels = labels };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Render_NullOrEmptyLabelValue_ReturnsEmptyList(string? labelValue)
    {
        // given an input with a label slot but null or empty text
        var input = Input("ButtonA", new LabelDefinition(X: 0, Y: 0));

        // when the renderer is asked
        var result = _underTest.Render(input, labelValue: labelValue);

        // then nothing is emitted — both are treated as "no label"
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_InputWithNoLabelSlots_ReturnsEmptyList()
    {
        // given an input with no label definitions
        var input = Input("ButtonA");

        // when the renderer is asked even with text
        var result = _underTest.Render(input, labelValue: "Jump");

        // then nothing is emitted (no slot to render into)
        result.ShouldBeEmpty();
    }

    [Theory]
    #pragma warning disable format
    [InlineData("left",   100, "Left",   100)]                                      // X unchanged
    [InlineData("right",  100, "Right",  100 - RenderingDefaults.LabelWidth)]       // shifted by full width
    [InlineData("center", 100, "Center", 100 - (RenderingDefaults.LabelWidth / 2))] // shifted by half width
    [InlineData("anythingelse", 100, "Left", 100)]                                  // unknown -> Left
    #pragma warning restore format
    public void Render_Alignment_TranslatesToAlignmentStringAndLeftOffset(
        string alignment,
        double labelX,
        string expectedAlignment,
        double expectedLeft)
    {
        // given an input with a single label at the configured alignment
        var input = Input("ButtonA", new LabelDefinition(X: labelX, Y: 0, Alignment: alignment));

        // when the renderer runs
        var rendered = _underTest.Render(input, "Jump").Single();

        // then the alignment string is normalized and Left is shifted accordingly
        rendered.Alignment.ShouldBe(expectedAlignment);
        rendered.Left.ShouldBe(expectedLeft);
    }

    [Fact]
    public void Render_TopIsBaselineAdjusted_ByFontSize()
    {
        // given a label slot at Y=100 with font size 20
        // (Top should be Y - 0.75 * FontSize to convert from baseline to top-left positioning)
        var input = Input("ButtonA", new LabelDefinition(X: 0, Y: 100, FontSize: 20));

        // when the renderer runs
        var rendered = _underTest.Render(input, "Jump").Single();

        // then the Top is shifted up by 0.75 * fontSize
        rendered.Top.ShouldBe(100 - (20 * 0.75));
    }

    [Fact]
    public void Render_CarriesInputNameTextAndFontSize_Through()
    {
        // given a label slot with a specific font size
        var input = Input("ButtonA", new LabelDefinition(X: 0, Y: 0, FontSize: 14));

        // when the renderer runs
        var rendered = _underTest.Render(input, "Jump").Single();

        // then the rendered label echoes the input's identity, text, and font size
        rendered.Text.ShouldBe("Jump");
        rendered.InputName.ShouldBe("ButtonA");
        rendered.FontSize.ShouldBe(14);
    }

    [Fact]
    public void Render_MultipleLabelSlots_AllRenderedInDocumentOrder()
    {
        // given an input with three label slots at distinct positions
        var input = Input("ButtonA",
            new LabelDefinition(X: 10, Y: 0),
            new LabelDefinition(X: 20, Y: 0),
            new LabelDefinition(X: 30, Y: 0));

        // when the renderer runs
        var result = _underTest.Render(input, "Jump");

        // then each slot becomes a RenderedLabel in source order
        result.Select(l => l.Left).ShouldBe([10.0, 20.0, 30.0]);
    }
}
