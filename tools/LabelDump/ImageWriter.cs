using System.Globalization;
using System.Text;

namespace DynamicControls.LabelDump;

/// <summary>
/// One rendered image, reduced to the fields that vary.
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
/// Writes what a run drew, as a diff baseline for changes that never touch a label. The label
/// dump answers "what text does the player read"; these answer "what does the player look at" —
/// the half a labels-only comparison cannot see, since an input losing its platform-specific
/// artwork and falling back to the generic leaves no trace in the labels at all.
///
/// <para>Two outputs, because image resolution varies along two axes and only one of them is the
/// ROM. <see cref="WritePlatforms"/> covers platform × control, which is where bespoke artwork
/// actually lives and where nothing else has bulk coverage. <see cref="WriteRomShapes"/> covers
/// the per-ROM axis, which on a platform shipping no artwork of its own reduces to a few dozen
/// distinct render shapes — recorded once each rather than repeated per ROM.</para>
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

    private static string Num(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Order images within a block: by control, then source, then size. Not render
    /// order, which is template document order and would reshuffle every block the moment an
    /// element moves in the layout.</summary>
    private static IEnumerable<GameImage> Ordered(IEnumerable<GameImage> images) => images
        .OrderBy(i => i.Control, StringComparer.Ordinal)
        .ThenBy(i => i.Source, StringComparer.Ordinal)
        .ThenBy(i => i.Width)
        .ThenBy(i => i.Height);

    private static void AppendImages(StringBuilder sb, IEnumerable<GameImage> images, string indent)
    {
        foreach (GameImage i in Ordered(images))
        {
            // NaN means "use the file's natural size", which is a fact about the asset rather
            // than the render — emitting nothing says that more plainly than a sentinel would.
            string size = (double.IsNaN(i.Width) ? "" : $" w=\"{Num(i.Width)}\"")
                        + (double.IsNaN(i.Height) ? "" : $" h=\"{Num(i.Height)}\"");
            string blur = i.BlurRadius > 0 ? $" blur=\"{Num(i.BlurRadius)}\"" : "";
            sb.Append($"{indent}<Image control=\"{Escape(i.Control)}\" src=\"{Escape(i.Source)}\""
                    + $"{size} opacity=\"{Num(i.Opacity)}\"{blur} />\n");
        }
    }

    /// <summary>
    /// Writes one block per platform: what the template draws for a game with no configuration
    /// of its own, which is the platform's default mapping and nothing else. The axis along which
    /// bespoke artwork actually varies, and the only bulk coverage the console platforms have —
    /// none of them has a ROM pack to run against.
    /// </summary>
    public static void WritePlatforms(
        string path,
        IReadOnlyList<(string Platform, IReadOnlyList<GameImage> Images)> platforms,
        string header,
        string command)
    {
        var sb = new StringBuilder();
        AppendPrologue(sb, header, command, "Platforms");

        foreach ((string platform, IReadOnlyList<GameImage> images) in
                 platforms.OrderBy(p => p.Platform, StringComparer.Ordinal))
        {
            sb.Append($"    <Platform name=\"{Escape(platform)}\">\n");
            AppendImages(sb, images, "        ");
            sb.Append("    </Platform>\n");
        }

        sb.Append("</Platforms>\n");
        Save(path, sb.ToString());
    }

    /// <summary>
    /// Writes one block per distinct render shape, listing the ROMs that produced it. Thousands
    /// of ROMs collapse to a few hundred shapes, because what varies per ROM is which
    /// <c>OneOf</c> alternative fired, not the artwork — recording every ROM's full image list
    /// separately would be the same few hundred facts repeated, at forty times the size.
    ///
    /// <para>Shapes are ordered by their first ROM rather than numbered, so a new shape appearing
    /// does not renumber every one after it. A ROM changing shape moves between two lists; a
    /// shape changing shows in its own block. Both stay local in a diff.</para>
    /// </summary>
    public static void WriteRomShapes(
        string path,
        IReadOnlyList<(string RomName, IReadOnlyList<GameImage> Images)> roms,
        string header,
        string command)
    {
        var sb = new StringBuilder();
        AppendPrologue(sb, header, command, "Renders");

        var shapes = roms
            .GroupBy(r => string.Join('\n', Ordered(r.Images).Select(i =>
                $"{i.Control}|{i.Source}|{i.Width}|{i.Height}|{i.Opacity}|{i.BlurRadius}")))
            .Select(g => (
                Roms: g.Select(r => r.RomName).OrderBy(n => n, StringComparer.Ordinal).ToList(),
                Images: g.First().Images))
            .OrderBy(s => s.Roms[0], StringComparer.Ordinal);

        foreach ((List<string> shapeRoms, IReadOnlyList<GameImage> images) in shapes)
        {
            sb.Append($"    <Shape roms=\"{shapeRoms.Count}\">\n");
            AppendImages(sb, images, "        ");
            foreach (string rom in shapeRoms)
                sb.Append($"        <Rom name=\"{Escape(rom)}\" />\n");
            sb.Append("    </Shape>\n");
        }

        sb.Append("</Renders>\n");
        Save(path, sb.ToString());
    }

    private static void AppendPrologue(StringBuilder sb, string header, string command, string root)
    {
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<!--\n");
        foreach (string line in header.Replace("\r\n", "\n").Split('\n'))
            sb.Append(line.Length == 0 ? "\n" : $"  {line}\n");
        sb.Append("-->\n");
        sb.Append($"<?regenerate {command} ?>\n");
        sb.Append($"<{root}>\n");
    }

    private static void Save(string path, string content)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
    }

    private static string Escape(string v) => v
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
