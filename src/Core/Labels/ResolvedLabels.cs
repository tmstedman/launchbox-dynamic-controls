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
[ExcludeFromCodeCoverage]
public record ResolvedLabels(
    IReadOnlyDictionary<string, string> LabelText,
    bool IsGameSpecific = false);
