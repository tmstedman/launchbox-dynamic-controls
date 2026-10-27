namespace DynamicControls.InputMapping;

/// <summary>
/// Raw DTO for per-game / per-source input mapping data — a single mapping list plus optional
/// selection of which platform-defined Controller to inherit from. Produced by InputMappingLoader
/// (game XMLs) and per-game sources (MAME, RetroArch), consumed by InputMappingService.
/// </summary>
public record InputMappingConfig
{
    /// <summary>Mapping entries declared in this config — overlay onto the selected controller's
    /// baseline mappings.</summary>
    public List<MappingEntry> Mappings { get; set; } = [];

    /// <summary>Platform-button Names to drop from the selected controller's baseline without
    /// substituting anything in their place. Parsed from per-game &lt;Unmap name="..."/&gt;
    /// elements. Lets a game disable a baseline button the player isn't supposed to use, in
    /// parity with RetroArch's <c>-1</c> sentinel for the same purpose. Empty when no
    /// &lt;Unmap&gt; elements are present.</summary>
    public List<string> Unmaps { get; set; } = [];

    /// <summary>
    /// Root-level `analogToDigital` attribute selecting which stick the Dpad mirrors onto.
    /// Null when the attribute is absent, allowing game-specific configs to inherit the
    /// controller value.
    /// </summary>
    public AnalogToDigitalMode? AnalogToDigital { get; set; }

    /// <summary>
    /// Optional `controller="..."` attribute on the game's XML root — selects which Controller
    /// inside the platform's Controllers.xml provides the baseline. Null means use the
    /// platform's default controller.
    /// </summary>
    public string? Controller { get; set; }
}

/// <summary>
/// Raw DTO for a platform's Controllers.xml — one or more &lt;Controller&gt; entries,
/// each declaring its own button vocabulary. A controller may carry an
/// <c>inheritFrom="OtherControllerName"</c> attribute; the loader applies the inheriting
/// controller's own entries as an overlay onto the base: entries whose Name matches a base
/// entry replace it, new Names are appended. Inheritance is transitive — the named base may
/// itself inheritFrom another controller — with cycles detected and broken. Games either select
/// one controller explicitly via <c>controller="..."</c> or fall back to whichever controller
/// is marked default (or, if none is marked, the first one in document order).
/// </summary>
public record PlatformControllersConfig
{
    /// <summary>The platform's controllers in document order. Always at least one element when
    /// the file parses successfully.</summary>
    public List<ControllerConfig> Controllers { get; set; } = [];

    /// <summary>
    /// Returns the controller named <paramref name="preferredName"/>, or null when no such
    /// controller exists. When <paramref name="preferredName"/> is null, returns the first
    /// controller flagged default, or — if none is flagged — the first controller in document
    /// order. Returns null only when the platform declares no controllers.
    /// </summary>
    public ControllerConfig? Resolve(string? preferredName) => preferredName switch
    {
        _ when Controllers.Count == 0 => null,
        not null => Controllers.FirstOrDefault(c => c.Name == preferredName),
        null => Controllers.FirstOrDefault(c => c.IsDefault) ?? Controllers[0],
    };
}

/// <summary>
/// A single controller variant within a platform — has its own name, button vocabulary, and
/// AnalogToDigital mode. Games either select it explicitly or inherit the platform's default.
/// When an XML <c>inheritFrom</c> attribute is present, the loader prepends the named
/// controller's mappings (transitively, following the base's own inheritFrom) before this
/// controller's own entries; after loading, <c>Mappings</c> already reflects the fully-merged list.
/// </summary>
public record ControllerConfig
{
    /// <summary>Unique name of the controller within the platform (e.g. "Pad", "Zapper").</summary>
    public string Name { get; set; } = null!;

    /// <summary>True when this controller is marked as the platform default via
    /// `default="true"`. Games without a `controller="..."` selection inherit from the first
    /// controller with this flag set; if none has it, the first controller in document order
    /// is the implicit default.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Controller-level `analogToDigital` value. Null means no mirroring.</summary>
    public AnalogToDigitalMode? AnalogToDigital { get; set; }

    /// <summary>Platform-button-to-generic-input mappings for this controller, fully resolved
    /// after inheritFrom overlay: each Name appears at most once, with the deepest descendant's
    /// entry winning.</summary>
    public List<MappingEntry> Mappings { get; set; } = [];

    /// <summary>Controls declared by this controller, resolved along inheritFrom the same way
    /// <see cref="Mappings"/> is.</summary>
    public List<ControlEntry> Controls { get; set; } = [];
}

/// <summary>
/// One platform button standing for several others:
/// <c>&lt;Control name="Dpad-Any" mappings="Dpad-Up Dpad-Down Dpad-Left Dpad-Right" /&gt;</c>.
///
/// <para>A control reaches whatever its members reach, resolved after the emulator's own
/// configuration has been applied rather than fixed when the file was written. That is the whole
/// point: a cfg binds directions one at a time, so a button naming a control as a unit has to be
/// re-read against wherever its directions actually ended up. A list of generic input names
/// written here instead would be decided at authoring time and could not follow a rebind.</para>
/// </summary>
public record ControlEntry
{
    /// <summary>The standing-in button's own name, as a label file or a cfg would spell it.</summary>
    public string Name { get; set; } = null!;

    /// <summary>The platform buttons it stands for.</summary>
    public List<string> Mappings { get; set; } = [];
}

/// <summary>
/// A single platform-button-to-generic-input mapping entry. Consumed by InputMappingService
/// when building the forward and reverse ResolvedMapping lookups.
/// </summary>
public record MappingEntry
{
    /// <summary>Platform-specific button name (e.g. "Triangle").</summary>
    public string Name { get; set; } = null!;

    /// <summary>Generic input name this button maps to (e.g. "ButtonY").</summary>
    public string Input { get; set; } = null!;

    /// <summary>
    /// The button reaches <see cref="Input"/> only because something else does, rather than by a
    /// binding of its own — set by a transform that derives it as part of a larger control (a
    /// MAME nudge reaching its half of the port's axis, a joystick direction its hat and stick),
    /// never read from a file. Carried to <see cref="ResolvedMapping.DerivedButtonToInput"/> and
    /// <see cref="ResolvedMapping.ControlledButtonToInput"/>.
    /// </summary>
    public bool Derived { get; set; }
}
