using System.Xml;

namespace DynamicControls.InputMapping;

/// <summary>
/// Locates and parses platform-level Controllers.xml (multi-controller vocabulary) and per-game
/// ControllerOverrides/{Rom}.xml (single mapping list with optional controller selection) into
/// thin DTOs. No merging is performed at this layer — callers receive the raw XML structure.
/// </summary>
public interface IInputMappingLoader
{
    /// <summary>
    /// Loads a per-game mapping override. Returns null if no game-specific mapping exists.
    /// The root element may carry a <c>controller="..."</c> attribute selecting which controller
    /// in the platform's Controllers.xml to inherit from; nested <c>&lt;Mapping&gt;</c> entries
    /// overlay onto that controller's baseline.
    /// </summary>
    InputMappingConfig? LoadGameMapping(GameInfo game);

    /// <summary>
    /// Loads the platform-level controllers file (<c>Controllers.xml</c>) — one or more
    /// <c>&lt;Controller&gt;</c> blocks, each with its own button vocabulary. A controller may
    /// declare <c>inheritFrom="OtherControllerName"</c> to prepend another controller's mappings
    /// before its own; inheritance is transitive (the base may itself inheritFrom a third, and so
    /// on) with cycles detected and broken. The root <c>&lt;Controllers&gt;</c> element may also
    /// carry <c>inheritFrom="OtherFile"</c> to pull in a shared base file's controllers and merge
    /// this file's own on top (override by name, append new) — letting platforms that share a
    /// layout reduce to a one-line pointer. A <c>User\</c> file is merged over the shipped one the
    /// same way, controller by controller, rather than replacing it. Returns null when no
    /// controllers file exists for the given platform in either layer.
    /// </summary>
    PlatformControllersConfig? LoadPlatformMapping(string platform);
}

/// <summary>
/// Production implementation: filesystem and XML parsing run lazily on each call (no caching);
/// platform names are sanitized via <c>SafeFileName</c> before being joined into the lookup path.
/// </summary>
public class InputMappingLoader(ILogger logger, LayeredFileSystem lfs) : IInputMappingLoader
{
    private readonly ILogger _logger = logger;
    private readonly LayeredFileSystem _lfs = lfs;

    /// <inheritdoc />
    public InputMappingConfig? LoadGameMapping(GameInfo game)
    {
        string safePlatform = game.Platform.SafeFileName();
        string? gamePath = _lfs.Resolve("Platforms", safePlatform, "ControllerOverrides", game.RomName + ".xml");
        _logger.Debug($"Game input mapping path: {gamePath}");

        return gamePath != null ? ParseGameMapping(gamePath) : null;
    }

    private InputMappingConfig ParseGameMapping(string path)
    {
        var result = new InputMappingConfig();
        using Stream stream = _lfs.OpenRead(path);
        var doc = new XmlDocument();
        doc.Load(stream);
        XmlElement root = doc.DocumentElement!;

        result.Controller = root.Attributes["controller"]?.Value;
        if (result.Controller != null) _logger.Debug($"Game controller selection: {result.Controller}");

        if (TryParseAnalogToDigital(root, path, out AnalogToDigitalMode? a2d))
            result.AnalogToDigital = a2d;

        foreach (XmlElement node in root.ChildNodes.OfType<XmlElement>())
        {
            if (node.Name == "Mapping")
            {
                MappingEntry? entry = ParseMappingEntry(node, path);
                if (entry != null) result.Mappings.Add(entry);
            }
            else if (node.Name == "Unmap")
            {
                string? name = node.Attributes["name"]?.Value;
                if (string.IsNullOrEmpty(name))
                    _logger.Error($"Skipping <Unmap> in {path}: missing 'name' attribute");
                else
                    result.Unmaps.Add(name);
            }
        }

        _logger.Debug($"Game mapping entries: {result.Mappings.Count}, unmaps: {result.Unmaps.Count}");
        return result;
    }

    /// <inheritdoc />
    public PlatformControllersConfig? LoadPlatformMapping(string platform)
    {
        var result = new PlatformControllersConfig();

        // Pass 1: read this platform's raw <Controller> list — the user's file laid over the
        // shipped one — resolving any root-level file inheritFrom first: a
        // `<Controllers inheritFrom="_Base">` file pulls in the base file's controllers and merges
        // its own on top (override by name, append new names). This lets many platforms that share
        // a controller layout reduce to a one-line pointer at a shared base file. No
        // controller-level inheritance is resolved yet.
        List<ParsedController>? parsed = LoadControllersRaw(platform, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { platform });
        if (parsed == null) return null;
        string path = ControllersPath(platform);

        // Pass 2: build the final ControllerConfig list. For each controller, walk its inheritFrom
        // chain to the root and prepend every ancestor's own mappings in order (root ancestor first,
        // this controller's own mappings last). Inheritance is transitive — a controller that
        // inheritsFrom another which itself inheritsFrom a third accumulates all three levels.
        var byName = parsed.ToDictionary(pc => pc.Name);
        foreach (ParsedController pc in parsed)
        {
            var controller = new ControllerConfig
            {
                Name = pc.Name,
                IsDefault = pc.IsDefault,
                AnalogToDigital = pc.AnalogToDigital,
            };

            (List<MappingEntry> inheritedMappings, List<ControlEntry> inheritedControls) =
                ResolveInheritedMappings(pc, byName, path);
            controller.Mappings.AddRange(inheritedMappings);
            controller.Controls.AddRange(inheritedControls);
            result.Controllers.Add(controller);

            _logger.Debug($"Controller '{controller.Name}': {controller.Mappings.Count} mappings (inheritFrom={pc.InheritFrom ?? "none"}), default={controller.IsDefault}, analogToDigital={controller.AnalogToDigital}");
        }

        _logger.Debug($"Platform controllers: {result.Controllers.Count}");
        return result;
    }

    /// <summary>
    /// Reads a platform's Controllers files into a raw <see cref="ParsedController"/> list: the
    /// <c>User\</c> file's controllers laid over the <c>Defaults\</c> file's by name (see
    /// <see cref="MergeControllers"/>), so a user's file holds only what it changes. Then resolves a
    /// root-level <c>&lt;Controllers inheritFrom="OtherFile"&gt;</c> reference — the user's when its
    /// file states one, else the shipped one — by loading that platform the same way and merging
    /// this one's controllers on top. Root-level inheritance is transitive (a base may itself
    /// inheritFrom another) with cycles detected via <paramref name="visited"/> and a missing base
    /// logged; either case falls back to this platform's own controllers. Returns null when neither
    /// layer has a file.
    /// </summary>
    private List<ParsedController>? LoadControllersRaw(string platform, HashSet<string> visited)
    {
        string relative = ControllersPath(platform);
        bool defaultsExists = _lfs.Defaults.FileExists(relative);
        bool userExists = _lfs.User.FileExists(relative);
        _logger.Debug($"Controllers '{relative}' — defaults: {(defaultsExists ? "found" : "missing")}, user: {(userExists ? "found" : "missing")}");
        if (!defaultsExists && !userExists) return null;

        ParsedFile? shipped = defaultsExists ? ParseFile(_lfs.Defaults.OpenRead(relative), Path.Combine("Defaults", relative)) : null;
        ParsedFile? user = userExists ? ParseFile(_lfs.User.OpenRead(relative), Path.Combine("User", relative)) : null;

        List<ParsedController> own = MergeControllers(shipped?.Controllers ?? [], user?.Controllers ?? [], user?.Path ?? relative);
        string? baseName = user?.InheritFrom ?? shipped?.InheritFrom;
        if (string.IsNullOrEmpty(baseName)) return own;

        if (!visited.Add(baseName))
        {
            _logger.Error($"Controllers file {relative}: root inheritFrom chain forms a cycle at '{baseName}'; stopping resolution");
            return own;
        }
        List<ParsedController>? baseControllers = LoadControllersRaw(baseName, visited);
        if (baseControllers == null)
        {
            _logger.Error($"Controllers file {relative}: inheritFrom='{baseName}' names '{ControllersPath(baseName)}', found in neither User\\Platforms nor Defaults\\Platforms; using own controllers only");
            return own;
        }

        return MergeControllers(baseControllers, own, relative);
    }

    /// <summary>One layer's Controllers file as written: its controllers and its root inheritFrom.</summary>
    private ParsedFile ParseFile(Stream openedStream, string path)
    {
        using Stream stream = openedStream;
        var doc = new XmlDocument();
        doc.Load(stream);
        XmlElement root = doc.DocumentElement!;

        var controllers = new List<ParsedController>();
        foreach (XmlElement node in root.ChildNodes.OfType<XmlElement>())
        {
            if (node.Name != "Controller") continue;
            ParsedController? pc = ParseControllerRaw(node, path);
            if (pc != null) controllers.Add(pc);
        }

        var defaults = controllers.Where(c => c.IsDefault).Select(c => c.Name).ToList();
        if (defaults.Count > 1)
            _logger.Error($"Controllers file {path}: multiple controllers marked default ({string.Join(", ", defaults)}); one will be picked arbitrarily by document order");

        string? inheritFrom = root.Attributes["inheritFrom"]?.Value;
        return new ParsedFile(path, controllers, string.IsNullOrEmpty(inheritFrom) ? null : inheritFrom);
    }

    private static string ControllersPath(string platform) =>
        Path.Combine("Platforms", platform.SafeFileName(), "Controllers.xml");

    /// <summary>
    /// Overlays <paramref name="own"/> controllers onto <paramref name="baseList"/>: a controller
    /// whose name matches a base entry replaces it in place; a new name is appended after the base
    /// entries. Base document order is preserved for the shared controllers. If <paramref name="own"/>
    /// declares its own default, it overrides the base's default (even for an appended, newly-named
    /// controller) — otherwise <see cref="PlatformControllersConfig.Resolve"/> would always pick the
    /// base's default first, since appended entries land after it regardless of intent.
    /// </summary>
    private List<ParsedController> MergeControllers(List<ParsedController> baseList, List<ParsedController> own, string path)
    {
        var merged = new List<ParsedController>(baseList);

        if (own.Any(c => c.IsDefault))
        {
            var demoted = merged.Where(c => c.IsDefault).Select(c => c.Name).ToList();
            if (demoted.Count > 0)
            {
                string newDefault = own.First(c => c.IsDefault).Name;
                _logger.Debug($"Controllers file {path}: '{newDefault}' overrides '{string.Join(", ", demoted)}' as the platform default");
            }
            for (int i = 0; i < merged.Count; i++)
                if (merged[i].IsDefault) merged[i] = merged[i] with { IsDefault = false };
        }

        foreach (ParsedController pc in own)
        {
            int idx = merged.FindIndex(c => string.Equals(c.Name, pc.Name, StringComparison.Ordinal));
            if (idx >= 0) merged[idx] = pc;
            else merged.Add(pc);
        }
        return merged;
    }

    /// <summary>
    /// Resolves a controller's transitive inheritFrom chain into a single mapping list. Each level
    /// is applied as an overlay onto the accumulated result from the root down: a child entry
    /// whose Name matches a parent entry replaces it; names not mentioned in the child are
    /// inherited unchanged. A missing base controller or an inheritFrom cycle is logged and stops
    /// the walk, so resolution never loops or throws.
    /// </summary>
    private (List<MappingEntry> Mappings, List<ControlEntry> Controls) ResolveInheritedMappings(
        ParsedController pc, Dictionary<string, ParsedController> byName, string path)
    {
        // Walk from pc up towards the root, collecting the chain leaf-first.
        var chain = new List<ParsedController>();
        var visited = new HashSet<string>();
        ParsedController? current = pc;
        while (current != null)
        {
            if (!visited.Add(current.Name))
            {
                _logger.Error($"Controller '{pc.Name}' in {path}: inheritFrom chain forms a cycle at '{current.Name}'; stopping resolution");
                break;
            }
            chain.Add(current);

            if (current.InheritFrom == null) break;
            if (!byName.TryGetValue(current.InheritFrom, out ParsedController? baseController))
            {
                _logger.Error($"Controller '{current.Name}' in {path}: inheritFrom='{current.InheritFrom}' names a controller that does not exist; using accumulated mappings only");
                break;
            }
            current = baseController;
        }

        // chain is leaf-first; fold root-first so each level's entries override the previous.
        List<MappingEntry> mappings = chain[^1].OwnMappings;
        for (int i = chain.Count - 2; i >= 0; i--)
            mappings = MappingOverlay.Apply(mappings, chain[i].OwnMappings);

        // Controls inherit by the same rule, keyed on their own name: a descendant redeclaring
        // one replaces it outright rather than adding members to it, so what a name stands for
        // is always stated in one place.
        var controls = new List<ControlEntry>();
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            foreach (ControlEntry a in chain[i].OwnControls)
            {
                int at = controls.FindIndex(x => string.Equals(x.Name, a.Name, StringComparison.Ordinal));
                if (at >= 0) controls[at] = a; else controls.Add(a);
            }
        }

        return (mappings, controls);
    }

    private ParsedController? ParseControllerRaw(XmlElement node, string path)
    {
        string? name = node.Attributes["name"]?.Value;
        if (string.IsNullOrEmpty(name))
        {
            _logger.Error($"Skipping <Controller> in {path}: missing 'name' attribute");
            return null;
        }

        bool isDefault = false;
        string? defaultAttr = node.Attributes["default"]?.Value;
        if (defaultAttr != null && bool.TryParse(defaultAttr, out bool parsed))
            isDefault = parsed;

        TryParseAnalogToDigital(node, path, out AnalogToDigitalMode? a2d);

        string? inheritFrom = node.Attributes["inheritFrom"]?.Value;

        var ownMappings = new List<MappingEntry>();
        var ownControls = new List<ControlEntry>();
        foreach (XmlElement child in node.ChildNodes.OfType<XmlElement>())
        {
            switch (child.Name)
            {
                case "Mapping":
                    MappingEntry? entry = ParseMappingEntry(child, path);
                    if (entry != null) ownMappings.Add(entry);
                    break;
                case "Control":
                    ControlEntry? control = ParseControlEntry(child, path);
                    if (control != null) ownControls.Add(control);
                    break;
                default:
                    // Silently skipping a typo'd element is how a whole button goes missing with
                    // nothing to read afterwards -- the same shape as an unrecognised <Head> child.
                    _logger.Error($"Invalid element <{child.Name}> in <Controller name=\"{name}\"> in {path}");
                    break;
            }
        }

        return new ParsedController(name, isDefault, a2d, inheritFrom, ownMappings, ownControls);
    }

    /// <summary>
    /// Parses an &lt;Control&gt;: one platform button standing for several others. Both attributes
    /// are required, and the button it stands for must be more than one — a single member is that
    /// button under another name, which the mapping entries already express.
    /// </summary>
    private ControlEntry? ParseControlEntry(XmlElement node, string path)
    {
        string? name = node.Attributes["name"]?.Value;
        string? mappings = node.Attributes["mappings"]?.Value;

        if (string.IsNullOrEmpty(name))
        {
            _logger.Error($"Skipping <Control> in {path}: missing 'name' attribute");
            return null;
        }

        string[] members = mappings?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (members.Length < 2)
        {
            _logger.Error($"Skipping <Control name=\"{name}\"> in {path}: 'mappings' must name more than one button");
            return null;
        }

        return new ControlEntry { Name = name, Mappings = [.. members] };
    }

    private sealed record ParsedFile(string Path, List<ParsedController> Controllers, string? InheritFrom);

    private sealed record ParsedController(
        string Name,
        bool IsDefault,
        AnalogToDigitalMode? AnalogToDigital,
        string? InheritFrom,
        List<MappingEntry> OwnMappings,
        List<ControlEntry> OwnControls);

    private MappingEntry? ParseMappingEntry(XmlElement node, string path)
    {
        string? name = node.Attributes["name"]?.Value;
        string? input = node.Attributes["input"]?.Value;
        if (name == null || input == null)
        {
            _logger.Error($"Skipping <Mapping> in {path}: missing 'name' or 'input' attribute");
            return null;
        }
        return new MappingEntry { Name = name, Input = input };
    }

    private bool TryParseAnalogToDigital(XmlElement node, string path, out AnalogToDigitalMode? result)
    {
        result = null;
        string? attr = node.Attributes["analogToDigital"]?.Value;
        if (attr == null) return false;
        switch (attr.ToLowerInvariant())
        {
            case "left": result = AnalogToDigitalMode.Left; return true;
            case "right": result = AnalogToDigitalMode.Right; return true;
            default:
                break;
        }
        _logger.Error($"Invalid analogToDigital value '{attr}' in {path} (expected 'left' or 'right')");
        return false;
    }
}
