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
}
