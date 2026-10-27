using DynamicControls;
using DynamicControls.Rendering;
using SkiaSharp;

namespace DynamicControls.Preview;

/// <summary>
/// Draws a <see cref="ControllerOverlayModel"/> to a bitmap, reproducing what the WPF pause
/// screen puts on the canvas — the overlay region of
/// <c>pause-themes/Dynamic Controls/Default.xaml</c> and nothing around it.
///
/// <para>Deliberately a dumb compositor. Every coordinate arrives resolved:
/// <see cref="RenderedLabel.Left"/> already has its alignment folded in (InputLabelRenderer
/// subtracts the label width, or half of it, before handing it over) and <see cref="RenderedLabel.Top"/>
/// is already shifted by three quarters of the font size. Re-deriving any of that here would
/// mean a second implementation of the layout rules, which is the one thing a preview must not
/// have — it would then be able to disagree with the plugin and still look right.</para>
///
/// <para>Two places where matching WPF exactly is not possible off Windows, both approximations
/// a reader should know about rather than trust:</para>
/// <list type="bullet">
/// <item>the theme asks for Segoe UI SemiBold, which exists on Windows alone. A substitute has
/// different metrics, so text wraps inside the fixed-width label box at slightly different
/// points.</item>
/// <item>WPF's <c>BlurEffect.Radius</c> is a kernel radius, where Skia takes a standard
/// deviation. <c>sigma = radius / 3</c> is the usual correspondence and is what is used here.</item>
/// </list>
/// </summary>
public static class OverlayRenderer
{
    /// <summary>Label box width the theme fixes at 300, matching
    /// <see cref="RenderingDefaults.LabelWidth"/>. Alignment is resolved against this box, so
    /// the two must agree or every centred and right-aligned label sits off by a margin.</summary>
    private const float LabelBoxWidth = (float)RenderingDefaults.LabelWidth;

    /// <summary>WPF kernel radius to Skia standard deviation.</summary>
    private const float BlurRadiusToSigma = 1f / 3f;

    public static SKBitmap Render(ControllerOverlayModel model, SKTypeface typeface, SKColor background)
    {
        int width = (int)Math.Round(model.CanvasWidth);
        int height = (int)Math.Round(model.CanvasHeight);

        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(background);

        DrawBaseImage(canvas, model.ImagePath, width, height);

        // Labels first, then overlay images — the theme stacks the two ItemsControls in that
        // order, and a glyph drawn over text looks different from text drawn over a glyph.
        foreach (RenderedLabel label in model.InputLabels) DrawLabel(canvas, label, typeface);
        foreach (RenderedImage image in model.RenderedImages) DrawOverlay(canvas, image);

        return bitmap;
    }

    /// <summary>
    /// Draws the controller photo under everything, scaled <c>Uniform</c> and centred — the
    /// theme sets it as the labels panel's ImageBrush with <c>Stretch="Uniform"</c>.
    /// </summary>
    private static void DrawBaseImage(SKCanvas canvas, string? path, int width, int height)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        using var source = SKBitmap.Decode(path);
        if (source == null) return;

        float scale = Math.Min((float)width / source.Width, (float)height / source.Height);
        float w = source.Width * scale, h = source.Height * scale;
        var dest = SKRect.Create((width - w) / 2, (height - h) / 2, w, h);

        using var paint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
        canvas.DrawBitmap(source, dest, paint);
    }

    /// <summary>
    /// Draws one overlay image. The blur is applied through a paint filter rather than to the
    /// bitmap, so it spreads past the image's own rectangle exactly as the WPF effect does —
    /// blurring the source in place would clip the halo at the edges.
    /// </summary>
    private static void DrawOverlay(SKCanvas canvas, RenderedImage image)
    {
        if (!File.Exists(image.Source)) return;

        using var source = SKBitmap.Decode(image.Source);
        if (source == null) return;

        SKRect dest = Fit(source, image);

        using var paint = new SKPaint
        {
            FilterQuality = SKFilterQuality.High,
            IsAntialias = true,
            Color = SKColors.White.WithAlpha((byte)Math.Round(Math.Clamp(image.Opacity, 0, 1) * 255)),
        };
        if (image.BlurRadius > 0)
        {
            float sigma = (float)image.BlurRadius * BlurRadiusToSigma;
            paint.ImageFilter = SKImageFilter.CreateBlur(sigma, sigma);
        }

        canvas.DrawBitmap(source, dest, paint);
    }

    /// <summary>
    /// Places an image inside the slot the layout gave it, scaled proportionally and centred.
    ///
    /// <para>The theme's <c>&lt;Image&gt;</c> sets no <c>Stretch</c>, so WPF applies its default
    /// of <c>Uniform</c>: the declared width and height are a bounding box, not the drawn size.
    /// That distinction only shows when an image's own proportions differ from its slot's, which
    /// is exactly what happens once a remap moves one — a square face button landing in the
    /// shoulder's 64×42 slot draws 42×42 and centred on Windows, where filling the box would
    /// squash it.</para>
    ///
    /// <para>A dimension the layout leaves unset is taken from the other and the image's own
    /// aspect; with neither set the image draws at its natural size, as WPF does.</para>
    /// </summary>
    private static SKRect Fit(SKBitmap source, RenderedImage image)
    {
        bool hasW = !double.IsNaN(image.Width), hasH = !double.IsNaN(image.Height);
        float aspect = source.Height == 0 ? 1f : (float)source.Width / source.Height;

        float w, h;
        switch (hasW, hasH)
        {
            case (false, false): (w, h) = (source.Width, source.Height); break;
            case (true, false): (w, h) = ((float)image.Width, (float)image.Width / aspect); break;
            case (false, true): (w, h) = ((float)image.Height * aspect, (float)image.Height); break;
            default:
                float boxW = (float)image.Width, boxH = (float)image.Height;
                float scale = Math.Min(boxW / source.Width, boxH / source.Height);
                (w, h) = (source.Width * scale, source.Height * scale);
                break;
        }

        // Centred in the box, which is where Uniform leaves it: the element occupies the full
        // declared size and the scaled content sits in the middle of it.
        float boxWidth = hasW ? (float)image.Width : w;
        float boxHeight = hasH ? (float)image.Height : h;
        return SKRect.Create(
            (float)image.Left + ((boxWidth - w) / 2),
            (float)image.Top + ((boxHeight - h) / 2),
            w, h);
    }

    /// <summary>
    /// Draws one label inside the fixed-width box its alignment was resolved against. Skia
    /// draws from a baseline and aligns about a point, so the box is reconstructed here and the
    /// anchor placed at its left, middle or right edge to match what WPF's TextBlock does with
    /// <c>TextAlignment</c> across a fixed <c>Width</c>.
    /// </summary>
    private static void DrawLabel(SKCanvas canvas, RenderedLabel label, SKTypeface typeface)
    {
        if (string.IsNullOrEmpty(label.Text)) return;

        using var paint = new SKPaint
        {
            Typeface = typeface,
            TextSize = (float)label.FontSize,
            Color = SKColors.White,
            IsAntialias = true,
            SubpixelText = true,
        };

        (float anchor, SKTextAlign align) = label.Alignment switch
        {
            "Right" => ((float)label.Left + LabelBoxWidth, SKTextAlign.Right),
            "Center" => ((float)label.Left + (LabelBoxWidth / 2), SKTextAlign.Center),
            _ => ((float)label.Left, SKTextAlign.Left),
        };
        paint.TextAlign = align;

        // Top is the box's top edge; Skia wants a baseline. Descending by the font's ascent
        // puts the cap height where WPF puts it.
        float baseline = (float)label.Top - paint.FontMetrics.Ascent;
        canvas.DrawText(label.Text, anchor, baseline, paint);
    }
}
