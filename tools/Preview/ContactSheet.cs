using SkiaSharp;

namespace DynamicControls.Preview;

/// <summary>
/// Lays a game's renders out in a grid, each captioned, so a game with control sets can be
/// checked in one image: every set side by side, where a label that should hold in all of them
/// and doesn't stands out without paging through the files. A last row that can't be filled is
/// centred rather than left against the edge.
/// </summary>
public static class ContactSheet
{
    private const int MaxColumns = 3;

    /// <summary>Each render at half size: six full-size canvases side by side is past what a
    /// screen shows at once, and a caption names each, so no label needs reading off the sheet
    /// that its own file doesn't show at full size.</summary>
    private const float Scale = 0.5f;

    private const float CaptionSize = 40f;
    private const int Gap = 24;

    public static SKBitmap Compose(
        IReadOnlyList<(string Caption, SKBitmap Render)> renders, SKTypeface typeface, SKColor background)
    {
        int tileWidth = (int)Math.Round(renders.Max(r => r.Render.Width) * Scale);
        int tileHeight = (int)Math.Round(renders.Max(r => r.Render.Height) * Scale);
        int captionHeight = (int)Math.Round(CaptionSize * 1.5f);
        int columns = Math.Min(MaxColumns, renders.Count);
        int rows = (renders.Count + columns - 1) / columns;

        var sheet = new SKBitmap(
            (columns * tileWidth) + ((columns + 1) * Gap),
            (rows * (captionHeight + tileHeight)) + ((rows + 1) * Gap),
            SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(background);

        using var caption = new SKPaint
        {
            Typeface = typeface,
            TextSize = CaptionSize,
            Color = SKColors.White,
            IsAntialias = true,
        };
        using var scaled = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };

        for (int i = 0; i < renders.Count; i++)
        {
            (string text, SKBitmap render) = renders[i];
            int inRow = Math.Min(columns, renders.Count - (i / columns * columns));
            float x = Gap + ((columns - inRow) * (tileWidth + Gap) / 2f) + (i % columns * (tileWidth + Gap));
            float y = Gap + (i / columns * (captionHeight + tileHeight + Gap));

            canvas.DrawText(text, x, y - caption.FontMetrics.Ascent, caption);
            canvas.DrawBitmap(render, SKRect.Create(x, y + captionHeight, render.Width * Scale, render.Height * Scale), scaled);
        }

        return sheet;
    }
}
