using System.Xml.Linq;

namespace DynamicControls.LabelDump;

/// <summary>
/// Reads <c>arcade-artwork.xml</c> into <see cref="GameLabels"/>. Used for two things: to
/// re-emit the transcription in canonical order, and to lend the pipeline dump its
/// <c>title</c> attributes so the two files line up when diffed.
/// </summary>
public static class ArtworkReader
{
    public static IReadOnlyList<GameLabels> Read(string path)
    {
        XElement root = XDocument.Load(path).Root
            ?? throw new InvalidDataException($"No root element in {path}");

        return
        [
            .. root.Elements("Game").Select(g => new GameLabels(
                RomName: (string?)g.Attribute("romName") ?? "",
                Title: (string?)g.Attribute("title"),
                Labels:
                [
                    .. g.Elements("Label")
                        .Select(l => (Control: (string?)l.Attribute("control") ?? "",
                                      Text: (l.Value ?? "").Trim()))
                        .Where(l => l.Control.Length > 0 && l.Text.Length > 0)
                ],
                Notes: [.. g.Elements("Note").Select(n => (n.Value ?? "").Trim())
                            .Where(n => n.Length > 0)]))
        ];
    }
}
