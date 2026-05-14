using DynamicControls.InputMapping;
using DynamicControls.Templates;

namespace DynamicControls.Rendering;

/// <summary>
/// Renders the images and overlays for a single input against the current mapping. Applies
/// ShowIf evaluation, MinOpacity fallback, and the collapse-stack YOffset directly to the
/// image positions so downstream stages get fully-resolved coordinates.
/// </summary>
public interface IInputImageRenderer
{
    /// <summary>
    /// Returns the rendered images and overlays for <paramref name="li"/>. Zero-opacity images
    /// are filtered out at this layer rather than emitted with <c>Opacity=0</c>. The mapping is
    /// passed to the image resolver; <paramref name="isGameSpecific"/> drives ShowIf=Auto.
    /// </summary>
    IEnumerable<RenderedImage> Render(LayoutInput li, Template template, ResolvedMapping mapping, bool isGameSpecific);
}

/// <summary>
/// Production implementation: image paths are resolved via <see cref="IInputImageResolver"/>;
/// overlay paths are already resolved on the <see cref="OverlayDefinition"/> at template build
/// time, so no further lookup is needed.
/// </summary>
public class InputImageRenderer(IInputImageResolver imageResolver) : IInputImageRenderer
{
    private readonly IInputImageResolver _imageResolver = imageResolver;

    public IEnumerable<RenderedImage> Render(
        LayoutInput li,
        Template template,
        ResolvedMapping mapping,
        bool isGameSpecific)
    {
        IEnumerable<RenderedImage> images = li.Input.InputImages
            .Select(image => (image, visible: li.Flags.IsVisible(image.ShowIf, isGameSpecific)))
            .Select(x => (x.image, x.visible, opacity: x.visible ? 1.0 : x.image.MinOpacity ?? template.Layout.DefaultMinOpacity))
            .Where(x => x.opacity > 0)
            .Select(x => new RenderedImage(
                Source: _imageResolver.Resolve(x.image, li.Input, mapping, template),
                Left: x.image.X,
                Top: x.image.Y + li.YOffset,
                Width: x.image.Width,
                Height: x.image.Height,
                Opacity: x.opacity,
                BlurRadius: x.visible ? 0.0 : x.image.InactiveBlurRadius ?? template.Layout.DefaultInactiveBlurRadius,
                InputName: li.Input.Name));

        IEnumerable<RenderedImage> overlays = li.Input.Overlays
            .Select(overlay => (overlay, visible: li.Flags.IsVisible(overlay.ShowIf, isGameSpecific)))
            .Select(x => (x.overlay, x.visible, opacity: x.visible ? 1.0 : x.overlay.MinOpacity ?? template.Layout.DefaultMinOpacity))
            .Where(x => x.opacity > 0)
            .Select(x => new RenderedImage(
                Source: x.overlay.Source,
                Left: x.overlay.X,
                Top: x.overlay.Y + li.YOffset,
                Width: x.overlay.Width,
                Height: x.overlay.Height,
                Opacity: x.opacity,
                BlurRadius: x.visible ? 0.0 : x.overlay.InactiveBlurRadius ?? template.Layout.DefaultInactiveBlurRadius,
                InputName: li.Input.Name));

        return images.Concat(overlays);
    }
}
