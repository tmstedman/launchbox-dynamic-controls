namespace DynamicControls;

/// <summary>
/// Fallback constants used when no explicit values are provided in template or config files.
/// Shared across the template, rendering, and plugin layers.
/// </summary>
public static class RenderingDefaults
{
    /// <summary>Default canvas width in pixels when no base image is present.</summary>
    public const double CanvasWidth = 1600;

    /// <summary>Default canvas height in pixels when no base image is present.</summary>
    public const double CanvasHeight = 1000;

    /// <summary>Default font size for label text.</summary>
    public const double FontSize = 28;

    /// <summary>Maximum width allocated for label text elements.</summary>
    public const double LabelWidth = 300;

    /// <summary>Blur radius applied to images rendered at less than full opacity (inactive state).</summary>
    public const double InactiveBlurRadius = 0.0;
}
