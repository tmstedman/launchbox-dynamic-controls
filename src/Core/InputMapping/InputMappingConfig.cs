namespace DynamicControls.InputMapping;

/// <summary>
/// Raw DTO for per-game / per-provider input mapping data — a single mapping list plus optional
/// selection of which platform-defined Controller to inherit from. Produced by the providers — the
/// per-game one from a <see cref="GameMappingConfig"/>, and the emulator ones (MAME, RetroArch) from
/// the emulator's own files; a provider hands back two of them as a
/// <see cref="ProvidedMapping"/>, and InputMappingResolver turns each into a ResolvedMapping.
/// </summary>
public record InputMappingConfig
{
    /// <summary>Mapping entries declared in this config — overlay onto the selected controller's
    /// baseline mappings.</summary>
    public List<MappingEntry> Mappings { get; set; } = [];

    /// <summary>Platform-button Names to drop from the selected controller's baseline without
    /// substituting anything in their place. From a game's &lt;Unmap name="..."/&gt;
    /// elements. Lets a game disable a baseline button the player isn't supposed to use, in
    /// parity with RetroArch's <c>-1</c> sentinel for the same purpose. Empty when no
    /// &lt;Unmap&gt; elements are present.</summary>
    public List<string> Unmaps { get; set; } = [];

    /// <summary>
    /// Which stick the Dpad mirrors onto. Null when the game's entry states none, allowing it to
    /// inherit the controller value.
    /// </summary>
    public AnalogToDigitalMode? AnalogToDigital { get; set; }

    /// <summary>
    /// Which Controller inside the platform's Controllers.xml provides the baseline — a game
    /// entry's <c>inheritFrom</c>, or the controller an emulator provider picked. Null means use
    /// the platform's default controller.
    /// </summary>
    public string? Controller { get; set; }

    /// <summary>
    /// Other names a label may call a platform button by, each mapped to the button it means. Set
    /// by an emulator provider that knows them — MAME names a port by its type, tag and mask
    /// (<c>OTHER:STROBE1:16</c>) where the button goes by its bare type — never read from a
    /// file. Carried to <see cref="ResolvedMapping.ButtonAliases"/>.
    /// </summary>
    public Dictionary<string, string> ButtonAliases { get; set; } = [];

    /// <summary>
    /// The mappings whose Dpad the analogToDigital mirror copies onto the stick; null means this
    /// config's own. Set by an emulator provider whose emulator has the stick press the Dpad the
    /// controller started with whatever its own swaps did — RetroArch works out a stick's Dpad
    /// presses from the stick itself, after remapping — and never read from a file.
    /// </summary>
    public List<MappingEntry>? DpadMirrorSource { get; set; }
}

/// <summary>
/// Raw DTO for a platform's Controllers.xml — one or more &lt;Controller&gt; entries,
/// each declaring its own button vocabulary, and the &lt;Game&gt; entries remapping one of them for a
/// single game. A controller may carry an
/// <c>inheritFrom="OtherControllerName"</c> attribute; the loader applies the inheriting
/// controller's own entries as an overlay onto the base: entries whose Name matches a base
/// entry replace it, new Names are appended. Inheritance is transitive — the named base may
/// itself inheritFrom another controller — with cycles detected and broken. Games either select
/// one controller explicitly via their entry's <c>inheritFrom</c> or fall back to whichever
/// controller is marked default (or, if none is marked, the first one in document order).
/// </summary>
public record PlatformControllersConfig
{
    /// <summary>The platform's controllers in document order. Always at least one element when
    /// the file parses successfully.</summary>
    public List<ControllerConfig> Controllers { get; set; } = [];

    /// <summary>The platform's per-game entries, each keyed by LaunchBox ID, ROM name or both.</summary>
    public List<GameMappingConfig> Games { get; set; } = [];

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

    /// <summary>
    /// Returns the entry for <paramref name="game"/>: the one with its LaunchBox ID, else the one
    /// with its ROM name, compared ignoring case; null when neither matches. A ROM name is not
    /// matched with its region and revision tags stripped, as a label is, and a clone is not
    /// matched by its parent's: a mapping is one ROM's setup, as an emulator keeps it, so a remap
    /// written for one dump shouldn't reach another.
    /// </summary>
    public GameMappingConfig? FindGame(GameInfo game) =>
        (game.LaunchBoxId is int id ? Games.FirstOrDefault(g => g.LaunchBoxId == id) : null)
        ?? Games.FirstOrDefault(g => string.Equals(g.RomName, game.RomName, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// One &lt;Game&gt; in a platform's Controllers.xml: a remap of one of the platform's controllers for
/// a single game, standing in for an emulator configuration the plugin can't read. It is written
/// like a &lt;Controller&gt;, but its mappings are a remap of the controller it inherits from rather
/// than buttons of its own, so a button it moves is drawn as the physical button the player presses.
/// </summary>
public record GameMappingConfig
{
    /// <summary>The game's LaunchBox ID, matched first.</summary>
    public int? LaunchBoxId { get; set; }

    /// <summary>The game's ROM file name without extension, matched when no ID does.</summary>
    public string? RomName { get; set; }

    /// <summary>The controller this game remaps; null means the platform default.</summary>
    public string? InheritFrom { get; set; }

    /// <summary>Which stick the Dpad mirrors onto, when <see cref="StatesAnalogToDigital"/>;
    /// null there is <c>analogToDigital="none"</c>.</summary>
    public AnalogToDigitalMode? AnalogToDigital { get; set; }

    /// <summary>The entry sets analogToDigital itself, <c>none</c> included; false inherits the
    /// controller's.</summary>
    public bool StatesAnalogToDigital { get; set; }

    /// <summary>Buttons this game binds differently, overlaid on the controller's by name.</summary>
    public List<MappingEntry> Mappings { get; set; } = [];

    /// <summary>Buttons of the controller this game leaves unbound.</summary>
    public List<string> Unmaps { get; set; } = [];
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
    /// `default="true"`. Games whose entry names no controller inherit from the first
    /// controller with this flag set; if none has it, the first controller in document order
    /// is the implicit default.</summary>
    public bool IsDefault { get; set; }

    /// <summary>`analogToDigital` as resolved along inheritFrom: the nearest controller's that
    /// states one. Null means no mirroring.</summary>
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
    /// binding of its own — set by an emulator provider that derives it (a MAME nudge reaching
    /// its half of the port's axis), never read from a file. Carried to
    /// <see cref="ResolvedMapping.DerivedButtonToInput"/>.
    /// </summary>
    public bool Derived { get; set; }
}
