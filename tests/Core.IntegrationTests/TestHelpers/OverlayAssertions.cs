using DynamicControls.Rendering;

namespace DynamicControls.Core.IntegrationTests.TestHelpers;

/// <summary>Expected label on a specific input, for batch slot-based assertions.</summary>
public record ExpectedLabel(string? Input, string Text);

/// <summary>
/// Expected image on a specific input, for batch slot-based assertions. <see cref="Input"/> is
/// null for group-level overlays that don't belong to any specific input. Style fields
/// (opacity, blur radius) must be specified; null is treated as a literal null in comparisons.
/// Width and Height are optional — null means "don't assert on this field".
/// </summary>
public record ExpectedImage(
    string? Input,
    string Src,
    double? W = null,
    double? H = null,
    double Opacity = 1.0,
    double BlurRadius = 0.0);

/// <summary>
/// Slot-based assertions on <see cref="ControllerOverlayModel"/>. Each assertion looks up the
/// rendered output by the input identity it was generated for, so failures say "label X belongs
/// on input Y but wasn't found there" rather than reporting a coordinate diff. Relies on the
/// <c>InputName</c> metadata attached to <c>RenderedLabel</c> and <c>RenderedImage</c>.
/// </summary>
public static class OverlayAssertions
{
    /// <summary>
    /// Asserts that the overlay carries a label with the given text rendered for the given input.
    /// An input may have multiple labels (e.g. when the same input appears in multiple parts of
    /// the template); the assertion passes if any of them match.
    /// </summary>
    public static void ShouldHaveLabel(this ControllerOverlayModel overlay, string inputName, string expectedText) =>
        overlay.InputLabels
            .Where(l => l.InputName == inputName)
            .Select(l => l.Text)
            .ShouldContain(expectedText, $"expected '{expectedText}' label on {inputName}");

    /// <summary>
    /// Asserts that the overlay carries an image with the given source path rendered for the
    /// given input. An input may have multiple images (e.g. when the same input appears in
    /// multiple parts of the template); the assertion passes if any of them match all the
    /// specified criteria. <paramref name="expectedOpacity"/> and <paramref name="expectedBlurRadius"/>
    /// are optional — only checked when supplied — letting tests pin the active/inactive visual
    /// treatment only where it matters.
    /// </summary>
    public static void ShouldHaveImage(
        this ControllerOverlayModel overlay,
        string inputName,
        string expectedSource,
        double? expectedOpacity = null,
        double? expectedBlurRadius = null)
    {
        IEnumerable<RenderedImage> candidates = overlay.RenderedImages
            .Where(i => i.InputName == inputName && i.Source == expectedSource);
        if (expectedOpacity.HasValue)
            candidates = candidates.Where(i => i.Opacity == expectedOpacity.Value);
        if (expectedBlurRadius.HasValue)
            candidates = candidates.Where(i => i.BlurRadius == expectedBlurRadius.Value);

        string criteria = $"source='{expectedSource}'"
            + (expectedOpacity.HasValue ? $", opacity={expectedOpacity.Value}" : "")
            + (expectedBlurRadius.HasValue ? $", blurRadius={expectedBlurRadius.Value}" : "");
        candidates.ShouldNotBeEmpty($"expected image on {inputName} with {criteria}");
    }

    /// <summary>
    /// Asserts that the overlay's labels exactly match <paramref name="expected"/> (order-insensitive).
    /// Fails if any expected label is missing or if the overlay carries any label not in the list.
    /// </summary>
    public static void ShouldHaveLabels(
        this ControllerOverlayModel overlay,
        params ExpectedLabel[] expected)
    {
        overlay.InputLabels
            .Select(l => new ExpectedLabel(l.InputName, l.Text))
            .ShouldBe(expected, ignoreOrder: true);
    }

    /// <summary>
    /// Asserts that the overlay's rendered images exactly match <paramref name="expected"/>
    /// (order-insensitive). Fails if any expected image is missing or if the overlay carries any
    /// image not in the list. All style fields (<c>Opacity</c>, <c>BlurRadius</c>) must be
    /// specified; null is treated as a literal null in the comparison. <c>Width</c> and
    /// <c>Height</c> are optional — null means "don't assert on this field".
    /// </summary>
    public static void ShouldHaveImages(
        this ControllerOverlayModel overlay,
        params ExpectedImage[] expected)
    {
        var actual = overlay.RenderedImages
            .Select(i => new ExpectedImage(i.InputName, i.Source, i.Width, i.Height, i.Opacity, i.BlurRadius))
            .ToList();
        AssertImages(actual, expected);
    }

    internal static bool Matches(ExpectedImage expected, ExpectedImage actual) =>
        expected.Input == actual.Input &&
        expected.Src == actual.Src &&
        expected.Opacity == actual.Opacity &&
        expected.BlurRadius == actual.BlurRadius &&
        (expected.W == null || expected.W == actual.W) &&
        (expected.H == null || expected.H == actual.H);

    internal static void AssertImages(List<ExpectedImage> actual, ExpectedImage[] expected)
    {
        var remaining = new List<ExpectedImage>(actual);
        var missing = new List<ExpectedImage>();
        foreach (ExpectedImage e in expected)
        {
            int idx = remaining.FindIndex(a => Matches(e, a));
            if (idx >= 0) remaining.RemoveAt(idx);
            else missing.Add(e);
        }

        if (missing.Count == 0 && remaining.Count == 0) return;

        static string FormatImage(ExpectedImage e)
        {
            string dims = e.W != null || e.H != null ? $" ({e.W}×{e.H})" : "";
            string style = (e.Opacity, e.BlurRadius) == (1.0, 0.0) ? "" : $" (opacity={e.Opacity}, blur={e.BlurRadius})";
            return $"[{e.Input ?? "null"}] {e.Src}{dims}{style}";
        }

        List<string> parts = [];
        if (missing.Count > 0)
            parts.Add("Missing: " + string.Join(", ", missing.Select(FormatImage)));
        if (remaining.Count > 0)
            parts.Add("Extra: " + string.Join(", ", remaining.Select(FormatImage)));

        throw new Exception(string.Join(" | ", parts));
    }

    /// <summary>
    /// Scopes subsequent image assertions to a template directory under the fixtures root: the
    /// given <paramref name="templatePath"/> is resolved against <see cref="TestPaths.FixturesRoot"/>,
    /// and each <c>ExpectedImage.Source</c> passed to the resulting wrapper's <c>ShouldHaveImages</c>
    /// is treated as relative to that template path. Lets tests declare the template once and use
    /// short relative paths per expected image, instead of repeating the full template prefix in
    /// every entry.
    /// </summary>
    public static OverlayInTemplate InTemplate(this ControllerOverlayModel overlay, string templatePath) =>
        new(overlay, templatePath.AsFixturePath());
}

/// <summary>
/// Scoped wrapper produced by <see cref="OverlayAssertions.InTemplate"/>. Carries a template
/// base path that is prepended to each <see cref="ExpectedImage.Src"/> before delegation to
/// the underlying <see cref="OverlayAssertions.ShouldHaveImage"/>.
/// </summary>
public readonly record struct OverlayInTemplate(ControllerOverlayModel Overlay, string TemplatePath)
{
    /// <summary>
    /// Asserts that the overlay's base image is the given file under the template path and that
    /// the canvas was sized to match.
    /// </summary>
    public void ShouldHaveBaseImage(string source, double width, double height)
    {
        Overlay.ImagePath.ShouldBe(Path.Combine(TemplatePath, source.AsPath()));
        Overlay.CanvasWidth.ShouldBe(width);
        Overlay.CanvasHeight.ShouldBe(height);
    }

    /// <summary>
    /// Asserts that the overlay's rendered images exactly match <paramref name="expected"/>
    /// (order-insensitive), each <c>Source</c> resolved against the template path. Fails if any
    /// expected image is missing or if the overlay carries any image not in the list.
    /// </summary>
    public void ShouldHaveImages(params ExpectedImage[] expected)
    {
        string prefix = TemplatePath + Path.DirectorySeparatorChar;
        var actual = Overlay.RenderedImages
            .Select(i => new ExpectedImage(
                i.InputName,
                i.Source.StartsWith(prefix)
                    ? i.Source[prefix.Length..].Replace(Path.DirectorySeparatorChar, '\\')
                    : i.Source,
                i.Width,
                i.Height,
                i.Opacity,
                i.BlurRadius))
            .ToList();

        OverlayAssertions.AssertImages(actual, expected);
    }
}
