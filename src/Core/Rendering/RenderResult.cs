using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.Rendering;

/// <summary>
/// Pipeline output of InputRenderingService. All resolution is complete — images looked up,
/// opacity and blur applied, labels rendered, Y offsets baked into positions. Handed directly
/// to ControllerOverlayService for binding to the ControllerOverlayModel.
/// </summary>
[ExcludeFromCodeCoverage]
public record RenderResult(
    IReadOnlyList<RenderedLabel> Labels,
    IReadOnlyList<RenderedImage> Images);

/// <summary>
/// Pipeline output of InputLabelRenderer. All positions and styles are fully resolved.
/// Collected into RenderedInput.Labels and ultimately bound to the BigBox pause theme
/// XAML via ControllerOverlayModel.InputLabels and DynamicControlsViewModel.
/// </summary>
/// <param name="Left">Left position in canvas coordinates.</param>
/// <param name="Top">Top position in canvas coordinates.</param>
/// <param name="Text">The label text to display.</param>
/// <param name="Alignment">Text alignment ("left", "center", "right").</param>
/// <param name="FontSize">Font size in points.</param>
/// <param name="InputName">Generic input name this label was rendered for (e.g. "ButtonX").
/// Diagnostic metadata — lets E2E tests verify slot associations directly and supports debug
/// logging. Null only if produced outside the per-input rendering path.</param>
[ExcludeFromCodeCoverage]
public record RenderedLabel(
    double Left,
    double Top,
    string Text,
    string Alignment,
    double FontSize,
    string? InputName = null);

/// <summary>
/// Pipeline output of InputImageRenderer. All image paths, positions, opacity, and blur are
/// fully resolved. Collected into RenderedInput.Images and ultimately bound to the BigBox
/// pause theme XAML via ControllerOverlayModel.RenderedImages and DynamicControlsViewModel.
/// </summary>
/// <param name="Source">Full resolved path to the image file.</param>
/// <param name="Left">Left position in canvas coordinates.</param>
/// <param name="Top">Top position in canvas coordinates.</param>
/// <param name="Width">Display width. NaN means use the image's natural width.</param>
/// <param name="Height">Display height. NaN means use the image's natural height.</param>
/// <param name="Opacity">Opacity to render at, between 0.0 and 1.0.</param>
/// <param name="BlurRadius">Blur radius to apply. 0.0 means no blur.</param>
/// <param name="InputName">Generic input name this image was rendered for (e.g. "ButtonX").
/// Diagnostic metadata — lets E2E tests verify slot associations directly and supports debug
/// logging. Null for group-level overlays that don't belong to any specific input.</param>
[ExcludeFromCodeCoverage]
public record RenderedImage(
    string Source,
    double Left = 0,
    double Top = 0,
    double Width = double.NaN,
    double Height = double.NaN,
    double Opacity = 1.0,
    double BlurRadius = 0.0,
    string? InputName = null);
