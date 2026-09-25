namespace DynamicControls.Templates;

/// <summary>
/// The pure shift math behind a Stack's <c>vAlign</c>, shared between <see cref="LayoutResolver"/>
/// (which applies it once, at template-load time, against the stack's fixed slot count) and
/// <see cref="Rendering.LayoutFilter"/> (which re-applies it at render time against however many
/// slots a collapsing stack actually has left, correcting for the difference). Takes an
/// already-validated value — "top", "bottom", or "center" — since validation (logging on an
/// unrecognized value) only ever needs to happen once, when the template loads.
/// </summary>
internal static class StackVAlign
{
    /// <summary>
    /// The downward shift to subtract from a stack's declared Y so the origin lands on the slot
    /// <paramref name="vAlign"/> names instead of always the first: "top" is 0 (no shift);
    /// "bottom" shifts up by every slot but the last, so the last slot lands on Y; "center"
    /// shifts up by half that, so the midpoint between the first and last slot lands on Y. A
    /// stack with zero or one slot is unaffected either way, since bottom and center already
    /// coincide with top when there's nothing to distribute around.
    /// </summary>
    public static double Shift(string vAlign, int slotCount, double gap)
    {
        int slots = Math.Max(slotCount, 1);
        return vAlign switch
        {
            "bottom" => (slots - 1) * gap,
            "center" => (slots - 1) * gap / 2.0,
            _ => 0,
        };
    }
}
