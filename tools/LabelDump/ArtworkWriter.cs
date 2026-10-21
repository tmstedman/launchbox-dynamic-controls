using System.Text;

namespace DynamicControls.LabelDump;

/// <summary>
/// One ROM's labels, keyed by the generic input each landed on. Shared by both outputs: the
/// pipeline's own render and the re-ordered copy of the artwork transcription, so a single
/// writer and a single ordering govern the pair and they stay line-for-line comparable.
/// </summary>
/// <param name="Title">Cabinet title, carried from the artwork. Null when unknown, which is
/// every ROM the artwork has never transcribed.</param>
/// <param name="Notes">Free text the artwork records where a Label could not hold it. Only
/// ever populated for the artwork copy — the pipeline has no equivalent.</param>
public record GameLabels(
    string RomName,
    string? Title,
    IReadOnlyList<(string Control, string Text)> Labels,
    IReadOnlyList<string> Notes);

/// <summary>
/// Writes <see cref="GameLabels"/> in <c>arcade-artwork.xml</c>'s shape, under one fixed
/// ordering. Both the artwork copy and the pipeline dump go through here precisely so their
/// ordering can never diverge: the two files exist to be diffed, and a difference in sort
/// order would swamp the differences that matter.
/// </summary>
public static class ArtworkWriter
{
    /// <summary>
    /// Canonical control order — the controller read physically rather than alphabetically:
    /// sticks, D-pad (whole before its directions), triggers, shoulders, face buttons, stick
    /// clicks, then the cabinet controls last. Alphabetical would split
    /// <c>AxisLeftStick</c> from <c>AxisRightStick</c> around the trigger pair and bury
    /// <c>ButtonBack</c>/<c>ButtonStart</c> among the face buttons, making a hand-read diff
    /// harder than it needs to be. A control missing from this list sorts after every listed
    /// one, alphabetically among its peers, so a new generic input degrades to a stable
    /// position rather than throwing.
    /// </summary>
    private static readonly string[] ControlOrder =
    [
        "AxisLeftStick",
        "AxisRightStick",
        "ButtonDpad",
        "ButtonDpadUp",
        "ButtonDpadDown",
        "ButtonDpadLeft",
        "ButtonDpadRight",
        "AxisTriggerLeft",
        "AxisTriggerRight",
        "ButtonLeftShoulder",
        "ButtonRightShoulder",
        "ButtonA",
        "ButtonB",
        "ButtonX",
        "ButtonY",
        "ButtonLeftStick",
        "ButtonRightStick",
        "ButtonStart",
        "ButtonBack",
    ];

    private static (int, string) SortKey(string control)
    {
        int i = Array.IndexOf(ControlOrder, control);
        return i >= 0 ? (i, "") : (ControlOrder.Length, control);
    }

    /// <summary>
    /// Writes <paramref name="games"/> to <paramref name="path"/>, ROMs ordinal by name and
    /// each ROM's labels in <see cref="ControlOrder"/>.
    /// </summary>
    /// <param name="header">Comment body explaining what this particular file is. Must not
    /// contain a double hyphen, which XML forbids inside a comment.</param>
    /// <param name="command">Exact command line that regenerates the file. Emitted as a
    /// processing instruction, not a comment, because it carries "--" flags that a comment
    /// may not hold — so the line stays runnable rather than being paraphrased.</param>
    public static void Write(string path, IReadOnlyList<GameLabels> games, string header, string command)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<!--\n");
        foreach (string line in header.Replace("\r\n", "\n").Split('\n'))
            sb.Append(line.Length == 0 ? "\n" : $"  {line}\n");
        sb.Append("-->\n");
        sb.Append($"<?regenerate {command} ?>\n");
        sb.Append("<Artwork>\n");

        foreach (GameLabels game in games.OrderBy(g => g.RomName, StringComparer.Ordinal))
        {
            string title = game.Title is { Length: > 0 } t ? $" title=\"{Escape(t)}\"" : "";
            sb.Append($"    <Game romName=\"{Escape(game.RomName)}\"{title}>\n");
            foreach ((string control, string text) in game.Labels.OrderBy(l => SortKey(l.Control)))
                sb.Append($"        <Label control=\"{Escape(control)}\">{Escape(text)}</Label>\n");
            foreach (string note in game.Notes)
                sb.Append($"        <Note>{Escape(note)}</Note>\n");
            sb.Append("    </Game>\n");
        }

        sb.Append("</Artwork>\n");

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, sb.ToString());
    }

    private static string Escape(string v) => v
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
