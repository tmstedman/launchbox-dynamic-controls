using DynamicControls.Rendering;

namespace DynamicControls;

/// <summary>
/// Output model produced by ControllerOverlayService containing everything needed to render the controller overlay.
/// Aggregates the base image path, canvas dimensions, all resolved text labels, and all overlay images.
/// Consumed by the LaunchBox host, whose DynamicControlsViewModel shows it and steps through its
/// control sets for the BigBox pause theme XAML to bind to.
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

    /// <summary>The control set this overlay draws, or null for the game's default — the one
    /// picture of all its controls, which the pause screen shows only for a game without sets.</summary>
    public string? SetName { get; set; }

    /// <summary>On the default, one overlay per control set the game's labels declare, in their
    /// order, each drawn on the same base image and canvas. Empty for a game without sets, and on
    /// a set itself.</summary>
    public List<ControllerOverlayModel> ControlSets { get; set; } = [];

    /// <summary>Whether this overlay draws nothing: no label and no image. A control set that failed
    /// to draw is left blank under its name.</summary>
    public bool IsBlank => InputLabels.Count == 0 && RenderedImages.Count == 0;

    /// <summary>The control sets that drew anything, in their order: the pages the pause screen
    /// steps through and the ones Preview writes, so both number them alike.</summary>
    public IReadOnlyList<ControllerOverlayModel> DrawnControlSets => [.. ControlSets.Where(set => !set.IsBlank)];
}
