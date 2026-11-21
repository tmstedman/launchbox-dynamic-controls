using SkiaSharp;

namespace DynamicControls.Preview;

/// <summary>
/// Lays a game's renders out in a grid, each captioned, so a game with control sets can be
/// checked in one image: every set side by side, where a label that should hold in all of them
/// and doesn't stands out without paging through the files.
///
/// <para>The sheet is one picture rather than a wall of finished ones: the renders arrive bare, on
/// a transparent canvas, and the backdrop is laid once across the whole sheet with the game's
/// title above the grid. The sheet takes the renders' own aspect ratio, so it reads like one of
/// them at a larger size: the grid's shape is the one that makes the smallest sheet at that
/// aspect, and what is left over is room around it, not a stretched tile. A last row that can't
/// be filled is centred rather than left against the edge.</para>
/// </summary>
public static class ContactSheet
{
    /// <summary>Each render at half size: six full-size canvases side by side is past what a
    /// screen shows at once, and a caption names each, so no label needs reading off the sheet
    /// that its own file doesn't show at full size.</summary>
    private const float Scale = 0.5f;

    private const float CaptionSize = 40f;
    private const int Gap = 24;

    /// <summary>The sheet's title at this multiple of its size on a single image, which the
    /// larger canvas needs to read as the heading, and the room left clear above and below it
    /// within its header.</summary>
    private const float TitleScale = 1.5f;
    private const int TitleMargin = 60;

    /// <param name="title">Written above the grid with <paramref name="subtitle"/>; both empty
    /// leaves the sheet without a header.</param>
    public static SKBitmap Compose(
        IReadOnlyList<(string Caption, SKBitmap Render)> renders, string title, string subtitle,
        SKTypeface typeface, SKColor background, OverlayRenderer.Backdrop backdrop)
    {
        int renderWidth = renders.Max(r => r.Render.Width);
        int renderHeight = renders.Max(r => r.Render.Height);
        int tileWidth = (int)Math.Round(renderWidth * Scale);
        int tileHeight = (int)Math.Round(renderHeight * Scale);
        int captionHeight = (int)Math.Round(CaptionSize * 1.5f);
        bool titled = title.Length + subtitle.Length > 0;
        double aspect = (double)renderWidth / renderHeight;

        // The title gets a header of the room it needs, margin either side, and the grid is
        // centred in what is left below it. Where the aspect leaves more room than that, the grid
        // sits lower and the title has it all; where it leaves none (a grid that already fills the
        // aspect, two by two say), the sheet grows to fit the header, and wider to keep the
        // aspect. The column count is the one that makes the smallest sheet.
        float titleBlock = titled ? TitleFrame.BlockHeight(typeface, TitleScale) : 0;
        int header = titled ? (int)Math.Ceiling(titleBlock + (2 * TitleMargin)) : 0;
        int columns = Enumerable.Range(1, renders.Count).MinBy(c => (double)SheetWidth(c) * SheetHeight(c));
        int width = SheetWidth(columns), height = SheetHeight(columns);

        var sheet = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(background);
        OverlayRenderer.DrawBackdrop(canvas, backdrop, width, height);

        using var caption = new SKPaint
        {
            Typeface = typeface,
            TextSize = CaptionSize,
            Color = SKColors.White,
            IsAntialias = true,
        };
        using var scaled = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };

        float left = (width - GridWidth(columns)) / 2f;
        float top = header + ((height - header - GridHeight(columns)) / 2f);
        if (titled) TitleFrame.DrawTitle(canvas, width, top, title, subtitle, typeface, TitleScale);
        for (int i = 0; i < renders.Count; i++)
        {
            (string text, SKBitmap render) = renders[i];
            int inRow = Math.Min(columns, renders.Count - (i / columns * columns));
            float x = left + Gap + ((columns - inRow) * (tileWidth + Gap) / 2f) + (i % columns * (tileWidth + Gap));
            float y = top + Gap + (i / columns * (captionHeight + tileHeight + Gap));

            canvas.DrawText(text, x + ((tileWidth - caption.MeasureText(text)) / 2f), y - caption.FontMetrics.Ascent, caption);
            canvas.DrawBitmap(render, SKRect.Create(x, y + captionHeight, render.Width * Scale, render.Height * Scale), scaled);
        }

        return sheet;

        int RowsFor(int c) => (renders.Count + c - 1) / c;
        int GridWidth(int c) => (c * tileWidth) + ((c + 1) * Gap);
        int GridHeight(int c) => (RowsFor(c) * (captionHeight + tileHeight)) + ((RowsFor(c) + 1) * Gap);
        int SheetHeight(int c) => (int)Math.Round(Math.Max(header + GridHeight(c), GridWidth(c) / aspect));
        int SheetWidth(int c) => (int)Math.Round(SheetHeight(c) * aspect);
    }
}
