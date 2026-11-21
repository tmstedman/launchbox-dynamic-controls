using SkiaSharp;

namespace DynamicControls.Preview;

/// <summary>
/// Puts a rendered frame on a taller canvas with a band above it, and writes the game's title
/// in that band — so a folder of previews can be paged through without reading filenames.
///
/// <para>Deliberately outside <see cref="OverlayRenderer"/>, which reproduces the pause screen
/// and nothing else. The frame arrives rendered on a transparent canvas without its backdrop;
/// the backdrop is laid here across the whole image instead, so the band reads as part of one
/// picture rather than a strip glued on top.</para>
/// </summary>
public static class TitleFrame
{
    /// <summary>Height of the title band above the frame.</summary>
    public const int Band = 100;

    private const float TitleSize = 46, SubtitleSize = 24, Gap = 10;

    /// <summary>Horizontal room kept clear either side of the text; a title wider than what is
    /// left shrinks to fit rather than running off the canvas.</summary>
    private const float SidePadding = 40;

    public static SKBitmap Wrap(SKBitmap frame, string title, string subtitle, SKTypeface typeface,
                                SKColor background, OverlayRenderer.Backdrop backdrop)
    {
        int width = frame.Width, height = frame.Height + Band;
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(background);
        OverlayRenderer.DrawBackdrop(canvas, backdrop, width, height);
        canvas.DrawBitmap(frame, 0, Band);

        // Centre the two lines as one block in the free space above the drawing — the band plus
        // however many of the frame's own top rows nothing was drawn in. Varies by game: one
        // whose topmost control is unlabelled and faint leaves more room than one with a label
        // up there.
        DrawTitle(canvas, width, Band + FirstInkedRow(frame), title, subtitle, typeface);
        return bitmap;
    }

    /// <summary>Writes the title and subtitle, centred as one block in the top <paramref name="free"/>
    /// pixels of a canvas <paramref name="width"/> wide, measured cap-top to baseline, at
    /// <paramref name="scale"/> times their usual size.</summary>
    public static void DrawTitle(SKCanvas canvas, float width, float free, string title, string subtitle,
                                 SKTypeface typeface, float scale = 1)
    {
        using SKPaint titlePaint = TextPaint(typeface, TitleSize * scale, SKColors.White);
        using SKPaint subtitlePaint = TextPaint(typeface, SubtitleSize * scale, new SKColor(0xA0, 0xA0, 0xA8));
        float room = width - (2 * SidePadding * scale);
        FitWidth(titlePaint, title, room);
        FitWidth(subtitlePaint, subtitle, room);

        float titleHeight = -titlePaint.FontMetrics.Ascent;
        float subtitleHeight = -subtitlePaint.FontMetrics.Ascent;
        float block = titleHeight + (Gap * scale) + subtitleHeight;
        float titleBaseline = ((free - block) / 2) + titleHeight;
        float centre = width / 2f;

        canvas.DrawText(title, centre, titleBaseline, titlePaint);
        canvas.DrawText(subtitle, centre, titleBaseline + (Gap * scale) + subtitleHeight, subtitlePaint);
    }

    /// <summary>Height of the title and subtitle as <see cref="DrawTitle"/> draws them, before any
    /// shrinking to fit a width, at <paramref name="scale"/> times their usual size.</summary>
    public static float BlockHeight(SKTypeface typeface, float scale)
    {
        using SKPaint titlePaint = TextPaint(typeface, TitleSize * scale, SKColors.White);
        using SKPaint subtitlePaint = TextPaint(typeface, SubtitleSize * scale, SKColors.White);
        return -titlePaint.FontMetrics.Ascent + (Gap * scale) - subtitlePaint.FontMetrics.Ascent;
    }

    /// <summary>
    /// The first row of <paramref name="frame"/> holding any pixel at or above
    /// <see cref="InkAlpha"/>, or its height when nothing was drawn. Relies on the frame having
    /// been rendered onto a transparent canvas, which is what keeps its alpha meaningful.
    /// </summary>
    private static int FirstInkedRow(SKBitmap frame)
    {
        ReadOnlySpan<byte> pixels = frame.GetPixelSpan();
        int stride = frame.RowBytes;
        for (int y = 0; y < frame.Height; y++)
        {
            // Rgba8888 and Bgra8888 both keep alpha as the fourth byte of each pixel.
            for (int i = (y * stride) + 3; i < (y * stride) + (frame.Width * 4); i += 4)
            {
                if (pixels[i] >= InkAlpha) return y;
            }
        }
        return frame.Height;
    }

    /// <summary>Alpha below which a pixel counts as empty — high enough that the far edge of a
    /// blur halo does not claim the row, low enough that a dimmed, unlabelled glyph does.</summary>
    private const byte InkAlpha = 24;

    private static SKPaint TextPaint(SKTypeface typeface, float size, SKColor colour) => new()
    {
        Typeface = typeface,
        TextSize = size,
        Color = colour,
        IsAntialias = true,
        SubpixelText = true,
        TextAlign = SKTextAlign.Center,
    };

    private static void FitWidth(SKPaint paint, string text, float room)
    {
        float width = paint.MeasureText(text);
        if (width > room) paint.TextSize *= room / width;
    }
}
