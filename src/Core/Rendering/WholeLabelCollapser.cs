namespace DynamicControls.Rendering;

/// <summary>
/// Reconciles a composite control's own label against its parts', in both directions, using the
/// composites the active template declares (<c>&lt;Whole name= parts=&gt;</c> in its
/// <c>&lt;Head&gt;</c>).
///
/// <para>Runs at render time rather than during label resolution because this is the first point
/// where a template has been chosen, and the template is what knows which of its inputs are
/// composites — whether <c>AxisLeftStick</c> is four directions or something else is a fact about
/// the chassis being drawn, not about the engine. It replaces a pass in
/// <c>InputLabelsService</c> that read the same relation from a fixed three-entry table, which
/// was the last such table in Core.</para>
///
/// <para>Individual part entries are never removed, only ever added to. That is what lets the
/// layout tell "the composite collapsed to one shared label" from "the parts each say something
/// different" with a <c>&lt;Condition match="agree"&gt;</c>, rather than having that decided
/// here.</para>
/// </summary>
public class WholeLabelCollapser(ILogger logger)
{
    private readonly ILogger _logger = logger;

    /// <summary>
    /// Returns <paramref name="labelText"/> with each declared composite reconciled against its
    /// parts. Returns the input unchanged when the template declares no composites, so a template
    /// without them costs nothing.
    /// </summary>
    public IReadOnlyDictionary<string, string> Collapse(
        IReadOnlyDictionary<string, string> labelText,
        IReadOnlyDictionary<string, IReadOnlyList<string>> wholes)
    {
        if (wholes.Count == 0) return labelText;

        var result = new Dictionary<string, string>(labelText);
        foreach ((string whole, IReadOnlyList<string> parts) in wholes)
        {
            List<string> present = [.. parts.Where(result.ContainsKey)];

            // No per-direction breakdown at all -- a platform button is bound straight at the
            // composite and none of its parts carry anything of their own. Gating each part's
            // render on its own (nonexistent) label would wrongly dim all of them; instead the
            // composite's text is copied onto every part, so each renders its own icon at full
            // brightness. Nothing ever distinguished them, so there is no basis to single one out.
            if (present.Count == 0)
            {
                if (result.TryGetValue(whole, out string? wholeText))
                {
                    foreach (string part in parts) result[part] = wholeText;
                    _logger.Debug($"Label: {whole} has no per-part breakdown -- '{wholeText}' copied onto {string.Join(", ", parts)}");
                }
                continue;
            }

            // A lone labelled part has nothing to merge with and already renders correctly.
            if (present.Count < 2) continue;

            // Parts that genuinely disagree keep their own text: a real per-part remap must not
            // be smoothed into a shared label it never had.
            List<string> distinct = [.. present.Select(p => result[p]).Distinct()];
            if (distinct.Count != 1) continue;

            result[whole] = distinct[0];
            _logger.Debug($"Label: {string.Join(", ", present)} agree on '{distinct[0]}' -- also written onto {whole}");
        }
        return result;
    }
}
