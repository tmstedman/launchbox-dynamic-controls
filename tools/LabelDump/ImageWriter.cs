using System.Globalization;
using System.Text;

namespace DynamicControls.LabelDump;

/// <summary>
/// One rendered image, reduced to the fields that vary by game.
/// </summary>
/// <param name="Control">Generic input the image rendered on, or <c>(none)</c> for a
/// template-level overlay that belongs to no input.</param>
/// <param name="Source">Path below the template root, e.g. <c>Nintendo 64\C-Any.png</c> or
/// <c>ButtonA.png</c>. The platform folder is what makes a bespoke image distinguishable from
/// the generic it would otherwise fall back to.</param>
/// <param name="Opacity">What the visibility rules resolved to. On a platform with no artwork of
/// its own this is the only per-game signal in the source column — every path is generic, and
/// whether an input is lit or dimmed is the whole of what the render says.</param>
/// <param name="Width">Render width, and with it the signal for <i>which</i> alternative of a
/// <c>OneOf</c> fired: a whole stick draws at 124 as the background glyph, 64 as the all-agree
/// single icon and 34 as a merged mini-icon, so the size separates three outcomes that share a
/// source file. Left/Top are omitted — they discriminate the same alternatives less reliably and
/// would double the width of every line.</param>
public record GameImage(
    string Control, string Source, double Opacity, double BlurRadius, double Width, double Height);

/// <summary>
/// Writes the images a run rendered, per ROM, as a diff baseline for changes that never touch a
/// label. <c>LabelDump</c>'s other output answers "what text does the player read"; this one
/// answers "what does the player look at", which is the half a labels-only comparison cannot see
/// — an input silently losing its platform-specific artwork and falling back to the generic
/// leaves no trace in the label dump at all.
/// </summary>
public static class ImageWriter
{
    /// <summary>
    /// Strips the template root from an absolute image path, leaving the platform folder and
    /// filename. Keyed on the <c>Templates</c> segment and the template name after it rather
    /// than on a prefix passed in, so the output is identical whatever directory the tool ran
    /// from — these files are committed and diffed across machines.
    /// </summary>
    internal static string Relative(string source)
    {
        string normalized = source.Replace('/', '\\');
        const string marker = "Templates\\";
        int at = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (at < 0) return normalized;

        int afterTemplateName = normalized.IndexOf('\\', at + marker.Length);
        return afterTemplateName < 0 ? normalized : normalized[(afterTemplateName + 1)..];
    }

    private static string Num(double v) =>
        v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// Writes <paramref name="games"/> to <paramref name="path"/>, ROMs ordinal by name and each
    /// ROM's images ordinal by control then source — not in render order, which is template
    /// document order and would reshuffle the whole file the moment an element moves.
    /// </summary>
    public static void Write(
        string path,
        IReadOnlyList<(string RomName, IReadOnlyList<GameImage> Images)> games,
        string header,
        string command)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<!--\n");
        foreach (string line in header.Replace("\r\n", "\n").Split('\n'))
            sb.Append(line.Length == 0 ? "\n" : $"  {line}\n");
        sb.Append("-->\n");
        sb.Append($"<?regenerate {command} ?>\n");
        sb.Append("<Renders>\n");

        foreach ((string rom, IReadOnlyList<GameImage> images) in games.OrderBy(g => g.RomName, StringComparer.Ordinal))
        {
            sb.Append($"    <Game romName=\"{Escape(rom)}\">\n");
            foreach (GameImage i in images
                .OrderBy(i => i.Control, StringComparer.Ordinal)
                .ThenBy(i => i.Source, StringComparer.Ordinal)
                .ThenBy(i => i.Width)
                .ThenBy(i => i.Height))
            {
                // NaN means "use the file's natural size", which is a fact about the asset
                // rather than the render — emitting nothing says that more plainly than a
                // sentinel would.
                string size = (double.IsNaN(i.Width) ? "" : $" w=\"{Num(i.Width)}\"")
                            + (double.IsNaN(i.Height) ? "" : $" h=\"{Num(i.Height)}\"");
                string blur = i.BlurRadius > 0 ? $" blur=\"{Num(i.BlurRadius)}\"" : "";
                sb.Append($"        <Image control=\"{Escape(i.Control)}\" src=\"{Escape(i.Source)}\""
                        + $"{size} opacity=\"{Num(i.Opacity)}\"{blur} />\n");
            }
            sb.Append("    </Game>\n");
        }

        sb.Append("</Renders>\n");

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, sb.ToString());
    }

    private static string Escape(string v) => v
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
