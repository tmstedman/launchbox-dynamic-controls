namespace DynamicControls.Labels;

/// <summary>
/// Raw DTO for input label data deserialized from a per-game or default labels XML file.
/// Holds label text keyed by platform button name with no input mapping applied.
/// Produced by InputLabelsLoader and MameControlsXmlLoader;
/// consumed by InputLabelsService to build ResolvedLabels.
/// </summary>
public record InputLabelsConfig
{
    /// <summary>All label entries in this config — for a game with control sets, its default:
    /// the one picture of every control that the pause screen shows.</summary>
    public List<LabelEntry> Labels { get; set; } = [];

    /// <summary>The game's control sets, in the order the file declares them: one per context
    /// (vehicle, view, mode) whose controls differ. Each is complete in itself and takes nothing
    /// from <see cref="Labels"/>. Empty for a game whose controls stay the same.</summary>
    public List<ControlSetConfig> ControlSets { get; set; } = [];
}

/// <summary>
/// One named <c>&lt;ControlSet&gt;</c> inside a <c>&lt;Game&gt;</c>: the controls as they stand
/// in one context of the game, labelled in full.
/// </summary>
public record ControlSetConfig
{
    /// <summary>The set's name as the file gives it (e.g. "Rocket Belt").</summary>
    public string Name { get; set; } = null!;

    /// <summary>The set's label entries.</summary>
    public List<LabelEntry> Labels { get; set; } = [];
}

/// <summary>
/// A single button-to-label-text entry within an InputLabelsConfig.
/// Consumed by InputLabelsService when merging per-game and default label sets.
/// </summary>
public record LabelEntry
{
    /// <summary>Platform-specific button name (e.g. "BUTTON1").</summary>
    public string Name { get; set; } = null!;

    /// <summary>Display label text for this button (e.g. "Jump").</summary>
    public string Label { get; set; } = null!;
}
