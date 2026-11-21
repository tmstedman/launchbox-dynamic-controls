using SkiaSharp;

namespace DynamicControls.Preview;

/// <summary>
/// Writes a control set's name and page number ("Rocket Belt   2 / 5") in the bottom-right
/// corner of its render, so a file opened on its own says that the game has other sets and which
/// of them this is. A game's only set has no number, as the pause screen shows it.
/// </summary>
public static class PageIndicator
{
    private const float Size = 34;

    /// <summary>Room kept clear between the indicator and the image's right and bottom edges.</summary>
    private const float Padding = 64;

    private const string Gap = "   ";

    public static void Stamp(SKBitmap bitmap, string position, string name, SKTypeface typeface)
    {
        using var canvas = new SKCanvas(bitmap);
        using SKPaint number = Paint(typeface, bold: false);
        using SKPaint title = Paint(typeface, bold: true);

        float gap = position.Length > 0 ? number.MeasureText(Gap) : 0;
        float positionWidth = number.MeasureText(position);
        float y = bitmap.Height - Padding;

        canvas.DrawText(position, bitmap.Width - Padding - positionWidth, y, number);
        canvas.DrawText(name, bitmap.Width - Padding - positionWidth - gap - title.MeasureText(name), y, title);
    }

    private static SKPaint Paint(SKTypeface typeface, bool bold) => new()
    {
        Typeface = typeface,
        TextSize = Size,
        Color = SKColors.White,
        IsAntialias = true,
        SubpixelText = true,
        FakeBoldText = bold,
    };
}
