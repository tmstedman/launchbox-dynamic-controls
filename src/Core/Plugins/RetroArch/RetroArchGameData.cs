namespace DynamicControls.Plugins.RetroArch;

/// <summary>
/// Per-level key/value data from the RetroArch cfg cascade or remap cascade, kept separate so
/// callers can access individual levels (e.g. game-level only for swap detection) or the merged
/// view (for variant selection). Levels are named after their cfg equivalents; for the remap
/// cascade, Global = common.rmp, Core = {core}.rmp, ContentDir = {contentDir}.rmp,
/// Game = {rom}.rmp. Null means no file was found at that level.
/// </summary>
public record RetroArchGameData(
    Dictionary<string, string>? Global,
    Dictionary<string, string>? Core,
    Dictionary<string, string>? ContentDir,
    Dictionary<string, string>? Game)
{
    /// <summary>
    /// Last-wins merge of all levels in cascade order (Global → Core → ContentDir → Game).
    /// More specific levels override less specific ones on key conflicts.
    /// </summary>
    public Dictionary<string, string> Merged => new[] { Game, ContentDir, Core, Global }
            .OfType<Dictionary<string, string>>()
            .SelectMany(d => d)
            .DistinctBy(kv => kv.Key)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
}
