using System.Text.RegularExpressions;

namespace DynamicControls.Plugins.Mame;

/// <summary>
/// One <c>&lt;newseq&gt;</c> from a MAME cfg, read as its <c>OR</c>-separated alternatives —
/// each one way to trigger the sequence. An alternative naming several codes together is a
/// chord and stays whole. <c>NONE</c> or nothing at all is unbound: no alternatives.
/// </summary>
public sealed class MameSequence
{
    private static readonly Regex AxisSwitch = new(@"^(JOYCODE_\d+_\w+)_(POS|NEG)_SWITCH$");

    public static readonly MameSequence Unbound = new([]);

    private MameSequence(IReadOnlyList<string> alternatives) => Alternatives = alternatives;

    public IReadOnlyList<string> Alternatives { get; }

    public bool IsBound => Alternatives.Count > 0;

    public static MameSequence Parse(string? text)
    {
        string trimmed = text?.Trim() ?? "";
        if (trimmed.Length == 0 || trimmed == "NONE") return Unbound;
        return Of(trimmed.Split(" OR ").Select(a => a.Trim()));
    }

    /// <summary>This sequence with <paramref name="removed"/>'s alternatives taken out.</summary>
    public MameSequence Without(MameSequence removed) =>
        Of(Alternatives.Where(a => !removed.Alternatives.Contains(a)));

    /// <summary>Both sequences' alternatives, this one's first.</summary>
    public MameSequence Concat(MameSequence other) => Of(Alternatives.Concat(other.Alternatives));

    /// <summary>
    /// The switches forming one axis between this sequence and <paramref name="other"/>: an end of
    /// the axis here and its opposite end there. Only a lone switch counts — a chord holding one
    /// is not that end of the stick on its own — and a sequence holding both ends of an axis
    /// pushes both ways at once, so it pairs with nothing.
    /// </summary>
    public (MameSequence Here, MameSequence There) AxisEndsSharedWith(MameSequence other)
    {
        var here = new List<string>();
        var there = new List<string>();
        foreach (string alternative in Alternatives)
        {
            Match match = AxisSwitch.Match(alternative);
            if (!match.Success) continue;
            string opposite = $"{match.Groups[1].Value}_{(match.Groups[2].Value == "POS" ? "NEG" : "POS")}_SWITCH";
            if (!other.Alternatives.Contains(opposite)
                || Alternatives.Contains(opposite)
                || other.Alternatives.Contains(alternative)) continue;
            here.Add(alternative);
            there.Add(opposite);
        }
        return (Of(here), Of(there));
    }

    /// <summary>The sequence as a cfg would write it, which is what joycode translation reads.</summary>
    public override string ToString() => string.Join(" OR ", Alternatives);

    private static MameSequence Of(IEnumerable<string> alternatives)
    {
        List<string> list = [.. alternatives.Where(a => a.Length > 0)];
        return list.Count == 0 ? Unbound : new MameSequence(list);
    }
}
