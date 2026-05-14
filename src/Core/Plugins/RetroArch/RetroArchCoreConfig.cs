namespace DynamicControls.Plugins.RetroArch;

/// <summary>
/// Per-core RetroArch controller catalog loaded from
/// <c>{rootDir}/Data/RetroArch/{coreDisplayName}.xml</c>. Declares which controller variants a core
/// can expose and the RetroArch device-type IDs that select each one. Variant names index into
/// the platform's Controllers.xml, which owns the actual button-vocabulary mappings — this file
/// only carries the libretro-side wiring (device IDs, the autoconfig default).
/// </summary>
public record RetroArchCoreConfig
{
    /// <summary>The core's controllers in document order. Always at least one element when
    /// the file parses successfully.</summary>
    public List<RetroArchControllerConfig> Controllers { get; set; } = [];

    /// <summary>
    /// Looks up the variant matching the given RetroArch device-type ID, or null when no variant
    /// declares it. Pure ID lookup — there is no "default variant" in this file; when nothing
    /// matches, the caller falls back to Controllers.xml's own default.
    /// </summary>
    public RetroArchControllerConfig? SelectController(int deviceType) =>
        Controllers.FirstOrDefault(c => c.RetropadIds.Contains(deviceType));
}

/// <summary>
/// A single controller variant within a RetroArch core — a name that indexes into
/// Controllers.xml plus the set of libretro device-type IDs that select it. A variant may list
/// multiple IDs (e.g. several Genesis 6-button device types that share a mapping).
/// </summary>
public record RetroArchControllerConfig
{
    /// <summary>Variant name; must match a &lt;Controller name="..."/&gt; entry in the
    /// platform's Controllers.xml, which supplies the actual button vocabulary.</summary>
    public string Name { get; set; } = null!;

    /// <summary>RetroArch device-type IDs (e.g. 513 for Genesis 6-Button) that select this
    /// controller. Declared as <c>&lt;Retropad id="..."/&gt;</c> child elements.</summary>
    public List<int> RetropadIds { get; set; } = [];
}
