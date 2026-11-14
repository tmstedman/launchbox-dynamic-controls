using DynamicControls.Rendering;

namespace DynamicControls.Core.Tests.Rendering;

/// <summary>
/// Unit tests for <see cref="LabelLineSplitter"/>: a one-line label passes through untouched, a
/// label on several lines becomes one label per line centred as a block on the original's Top,
/// with everything but the text and Top carried over and the whole label kept in
/// <see cref="RenderedLabel.FullText"/>, and a line break at either end is dropped first.
/// </summary>
public class LabelLineSplitterTests
{
    private const double FontSize = 20;
    private const double Line = FontSize * RenderingDefaults.LabelLineHeight;

    private static RenderedLabel Label(string text, double top = 100) =>
        new(Left: 10, Top: top, Text: text, Alignment: "Center", FontSize: FontSize, InputName: "AxisLeftStick");

    [Fact]
    public void Split_OneLineLabel_PassesThroughAsTheSameInstance()
    {
        // given a label with no line break
        RenderedLabel label = Label("Jump");

        // when split
        IReadOnlyList<RenderedLabel> result = LabelLineSplitter.Split([label]);

        // then it is the only output, unchanged
        result.Single().ShouldBeSameAs(label);
    }

    [Theory]
    [InlineData("Punch\nBlock")]
    [InlineData("Punch\r\nBlock")] // a Windows line break is one break, and leaves no stray \r
    public void Split_LabelOnTwoLines_BecomesTwoLabelsHalfALineEitherSideOfItsTop(string text)
    {
        // given a two-line label at Top=100
        RenderedLabel label = Label(text);

        // when split
        IReadOnlyList<RenderedLabel> result = LabelLineSplitter.Split([label]);

        // then each line is its own label, one line apart, their middle on the original Top
        result.Select(l => l.Text).ShouldBe(["Punch", "Block"]);
        result[0].Top.ShouldBe(100 - (Line / 2), tolerance: 1e-9);
        result[1].Top.ShouldBe(100 + (Line / 2), tolerance: 1e-9);
    }

    [Fact]
    public void Split_LabelOnFourLines_KeepsEverythingButTextAndTop()
    {
        // given a four-line label
        RenderedLabel label = Label("Punch\nBlock\nCombo\nMoves");

        // when split
        IReadOnlyList<RenderedLabel> result = LabelLineSplitter.Split([label]);

        // then the lines step down one line at a time from a line and a half above the Top...
        result.Select(l => l.Top).ShouldBe(
            [100 - (1.5 * Line), 100 - (0.5 * Line), 100 + (0.5 * Line), 100 + (1.5 * Line)],
            tolerance: 1e-9);

        // ...and each keeps the label's position, style, input and whole text
        result.ShouldAllBe(l =>
            l.Left == 10 && l.Alignment == "Center" && l.FontSize == FontSize &&
            l.InputName == "AxisLeftStick" && l.FullText == "Punch\nBlock\nCombo\nMoves");
    }

    [Fact]
    public void Split_EmptyLine_KeepsItsPlaceButDrawsNothing()
    {
        // given a label whose middle line is empty
        RenderedLabel label = Label("Punch\n\nBlock");

        // when split
        IReadOnlyList<RenderedLabel> result = LabelLineSplitter.Split([label]);

        // then only the two lines with text are drawn, still a full line either side of the Top
        result.Select(l => l.Text).ShouldBe(["Punch", "Block"]);
        result.Select(l => l.Top).ShouldBe([100 - Line, 100 + Line], tolerance: 1e-9);
    }

    [Theory]
    [InlineData("Jump\n")]
    [InlineData("\nJump")]
    [InlineData("\r\nJump\r\n")]
    public void Split_LineBreakAtAnEnd_IsDropped_LeavingAOneLineLabelWhereItWas(string text)
    {
        // given a one-line label with a stray line break before or after it
        RenderedLabel label = Label(text);

        // when split
        IReadOnlyList<RenderedLabel> result = LabelLineSplitter.Split([label]);

        // then it draws as the one line it is, not lifted or lowered, with nothing drawn empty
        RenderedLabel only = result.Single();
        only.Text.ShouldBe("Jump");
        only.FullText.ShouldBe("Jump");
        only.Top.ShouldBe(100);
    }

    [Fact]
    public void Split_LineBreaksAtBothEndsOfATwoLineLabel_AreDroppedBeforeCentring()
    {
        // given a two-line label with a line break at each end
        RenderedLabel label = Label("\nPunch\nBlock\n");

        // when split
        IReadOnlyList<RenderedLabel> result = LabelLineSplitter.Split([label]);

        // then it centres as the two lines it holds, and reports itself without the strays
        result.Select(l => l.Text).ShouldBe(["Punch", "Block"]);
        result.Select(l => l.Top).ShouldBe([100 - (Line / 2), 100 + (Line / 2)], tolerance: 1e-9);
        result.ShouldAllBe(l => l.FullText == "Punch\nBlock");
    }

    [Fact]
    public void Split_LabelOfNothingButLineBreaks_DrawsNothing()
    {
        // given a label that is only a line break
        RenderedLabel label = Label("\n");

        // when split
        IReadOnlyList<RenderedLabel> result = LabelLineSplitter.Split([label]);

        // then nothing is drawn for it
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Split_MixedLabels_KeepTheirOrder()
    {
        // given a one-line label either side of a two-line one
        RenderedLabel before = Label("Start"), after = Label("Select");

        // when split
        IReadOnlyList<RenderedLabel> result = LabelLineSplitter.Split([before, Label("Punch\nBlock"), after]);

        // then the lines take the multi-line label's place
        result.Select(l => l.Text).ShouldBe(["Start", "Punch", "Block", "Select"]);
    }
}
