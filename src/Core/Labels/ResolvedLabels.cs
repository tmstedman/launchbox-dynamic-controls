using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.Labels;

/// <summary>
/// Processed output from InputLabelsService containing display text keyed by generic input name.
/// Platform button names have been mapped to generic names (e.g. "BUTTON1" → "ButtonA")
/// and per-game labels have been merged over defaults.
/// Consumed by ControllerOverlayService to feed label text into the rendering pipeline.
/// </summary>
/// <param name="LabelText">Label text keyed by generic input name (e.g. "ButtonA" → "Jump").</param>
/// <param name="IsGameSpecific">True when the labels were sourced from a game-specific labels XML
/// (i.e. some authority other than the platform's default labels). An empty game-specific labels
/// XML is treated as if it didn't exist, so this flag also means "the game contributed at least
/// one of its own label entries." Used by the renderer's showIf="auto" rule to switch into
/// label-mode only when the game has its own labels — purely-inherited defaults don't count.</param>
/// <param name="GameLabelled">The generic input names whose text came from the game's own entry,
/// as opposed to a <c>&lt;Defaults&gt;</c> block merged in beneath it. A game that labels only
/// its buttons still inherits every default, so <paramref name="IsGameSpecific"/> cannot say
/// whether a particular input was spoken for; this can. Empty when the labels are all defaults.</param>
[ExcludeFromCodeCoverage]
public record ResolvedLabels(
    IReadOnlyDictionary<string, string> LabelText,
    bool IsGameSpecific = false,
    IReadOnlySet<string>? GameLabelled = null)
{
    public IReadOnlySet<string> GameLabelled { get; init; } = GameLabelled ?? new HashSet<string>();
}
