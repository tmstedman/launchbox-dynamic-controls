namespace DynamicControls.Labels;

/// <summary>
/// Raw DTO for input label data deserialized from a per-game or default labels XML file.
/// Holds label text keyed by platform button name with no input mapping applied.
/// Produced by InputLabelsLoader and MameControlsXmlLoader;
/// consumed by InputLabelsService to build ResolvedLabels.
/// </summary>
public record InputLabelsConfig
{
    /// <summary>All label entries in this config.</summary>
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
