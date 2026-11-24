namespace DynamicControls.InputMapping;

/// <summary>
/// Shared overlay logic used by both controller-level inheritFrom resolution and per-game
/// mapping layering: baseline entries whose Name appears in the overlay (or remove list) are
/// dropped and the overlay entries are appended, so the child's declaration of a Name always
/// wins over the parent's.
/// </summary>
internal static class MappingOverlay
{
    internal static List<MappingEntry> Apply(
        List<MappingEntry> baseline,
        List<MappingEntry> overlay,
        List<string>? removes = null)
    {
        var overriddenNames = new HashSet<string>(overlay.Select(m => m.Name));
        if (removes != null) overriddenNames.UnionWith(removes);
        var result = baseline.Where(m => !overriddenNames.Contains(m.Name)).ToList();
        result.AddRange(overlay);
        return result;
    }

    /// <summary>
    /// Controls overlay by their own name too, but a later declaration replaces an earlier one
    /// outright rather than adding members to it, so what a name stands for is always stated in
    /// one place. A control is a platform button like any other, so an &lt;Unmap&gt; naming one
    /// drops it as it would a mapping.
    /// </summary>
    internal static List<ControlEntry> ApplyControls(
        List<ControlEntry> baseline,
        List<ControlEntry> overlay,
        List<string> removes)
    {
        var removed = new HashSet<string>(removes);
        var result = baseline.Where(c => !removed.Contains(c.Name)).ToList();
        foreach (ControlEntry control in overlay)
        {
            int at = result.FindIndex(c => string.Equals(c.Name, control.Name, StringComparison.Ordinal));
            if (at >= 0) result[at] = control; else result.Add(control);
        }
        return result;
    }
}
