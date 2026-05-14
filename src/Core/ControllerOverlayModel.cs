using DynamicControls.Rendering;

namespace DynamicControls;

/// <summary>
/// Output model produced by ControllerOverlayService containing everything needed to render the controller overlay.
/// Aggregates the base image path, canvas dimensions, all resolved text labels, and all overlay images.
/// Consumed by DynamicControlsPlugin, which copies each field directly to DynamicControlsViewModel
/// for binding to the BigBox pause theme XAML.
/// </summary>
public record ControllerOverlayModel
{
    /// <summary>Full path to the base controller image, or null if none.</summary>
    public string? ImagePath { get; set; }

    /// <summary>Width of the rendering canvas in pixels.</summary>
    public double CanvasWidth { get; set; } = RenderingDefaults.CanvasWidth;

    /// <summary>Height of the rendering canvas in pixels.</summary>
    public double CanvasHeight { get; set; } = RenderingDefaults.CanvasHeight;

    /// <summary>All positioned text labels to display on the overlay.</summary>
    public List<RenderedLabel> InputLabels { get; set; } = [];

    /// <summary>All positioned images (button and overlay) to display.</summary>
    public List<RenderedImage> RenderedImages { get; set; } = [];
}
