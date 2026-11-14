namespace DynamicControls.Rendering;

/// <summary>
/// The last step of rendering: turns a label written on several lines (a line break in its text,
/// <c>&amp;#10;</c> in <c>Labels.xml</c>) into one <see cref="RenderedLabel"/> per line, centred as
/// a block on where the label would sit on one line. Whatever draws the result — the pause theme,
/// the preview tool — only ever draws one line of text at a time, so where each line goes is
/// decided here, whatever font the drawing uses.
///
/// <para>Each line keeps the whole label in <see cref="RenderedLabel.LineOf"/>, so a reader that
/// wants the label rather than the drawing (the label dump tool, a test) still sees it whole.</para>
///
/// <para>A line break at either end of a label is dropped first. The <c>Labels.xml</c> loader trims
/// those already, but not every source does, and one left in would draw an empty line and move the
/// rest half a line off where the label belongs.</para>
/// </summary>
public static class LabelLineSplitter
{
    private const double LineHeight = RenderingDefaults.LabelLineHeight;

    private static readonly string[] LineBreaks = ["\r\n", "\n"];

    /// <summary>
    /// Returns <paramref name="labels"/> in order with every multi-line label replaced by its
    /// lines. A one-line label passes through as the same instance.
    /// </summary>
    public static IReadOnlyList<RenderedLabel> Split(IReadOnlyList<RenderedLabel> labels) =>
        [.. labels.SelectMany(SplitOne)];

    private static IEnumerable<RenderedLabel> SplitOne(RenderedLabel label)
    {
        string text = label.Text.Trim('\r', '\n');
        if (text.Length == 0) return [];
        if (!text.Contains('\n')) return text == label.Text ? [label] : [label with { Text = text }];

        string[] lines = text.Split(LineBreaks, StringSplitOptions.None);
        double step = label.FontSize * LineHeight;

        // Each line past the first raises the block by half a line, so its middle sits where a
        // one-line label's would. An empty line keeps its place but draws nothing.
        double top = label.Top - ((lines.Length - 1) * step / 2);
        return lines
            .Select((line, i) => label with { Text = line, Top = top + (i * step), LineOf = text })
            .Where(l => l.Text.Length > 0);
    }
}
