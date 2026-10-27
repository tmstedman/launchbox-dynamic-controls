namespace DynamicControls.Plugins.Mame;

/// <summary>
/// One <c>&lt;newseq&gt;</c> from a MAME cfg, read as its <c>OR</c>-separated alternatives —
/// each one way to trigger the sequence. An alternative naming several codes together is a
/// chord and stays whole. <c>NONE</c> or nothing at all is unbound: no alternatives.
/// </summary>
public sealed class MameSequence
{
    public static readonly MameSequence Unbound = new([]);

    private MameSequence(IReadOnlyList<string> alternatives)
    {
        Alternatives = alternatives;
    }

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
    /// an axis here and its opposite end there, as <paramref name="joycodes"/> declares them. Only
    /// a lone switch counts — a chord holding one is not that end of the stick on its own — and a
    /// sequence holding both ends of an axis pushes both ways at once, so it pairs with nothing.
    /// </summary>
    public (MameSequence Here, MameSequence There) AxisEndsSharedWith(MameSequence other, JoycodeMapping joycodes)
    {
        var here = new List<string>();
        var there = new List<string>();
        foreach (string alternative in Alternatives)
        {
            if (joycodes.EndOf(alternative) is not AxisEnd end)
            {
                continue;
            }

            List<string> opposite = [.. other.Alternatives.Where(a => joycodes.EndOf(a) == end.Opposite)];
            if (opposite.Count == 0
                || Alternatives.Any(a => joycodes.EndOf(a) == end.Opposite)
                || other.Alternatives.Any(a => joycodes.EndOf(a) == end))
            {
                continue;
            }

            here.Add(alternative);
            there.AddRange(opposite.Where(a => !there.Contains(a)));
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
