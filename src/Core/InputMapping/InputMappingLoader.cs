using System.Xml;

namespace DynamicControls.InputMapping;

/// <summary>
/// Locates and parses platform-level Controllers.xml (multi-controller vocabulary) and per-game
/// InputMappings/{Rom}.xml (single mapping list with optional controller selection) into thin
/// DTOs. No merging is performed at this layer — callers receive the raw XML structure.
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
    /// layout reduce to a one-line pointer. Returns null when no controllers file exists for the
    /// given platform.
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
        string gamePath = _lfs.Resolve("InputMappings", safePlatform, game.RomName + ".xml");
        _logger.Debug($"Game input mapping path: {gamePath}, Exists: {_lfs.FileExists(gamePath)}");

        return _lfs.FileExists(gamePath) ? ParseGameMapping(gamePath) : null;
    }

    /// <inheritdoc />
    public PlatformControllersConfig? LoadPlatformMapping(string platform)
    {
        string safePlatform = platform.SafeFileName();
        string platformPath = _lfs.Resolve("Controllers", safePlatform + ".xml");
        _logger.Debug($"Controllers path: {platformPath}, Exists: {_lfs.FileExists(platformPath)}");

        return _lfs.FileExists(platformPath) ? ParsePlatformMapping(platformPath) : null;
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

    private PlatformControllersConfig ParsePlatformMapping(string path)
    {
        var result = new PlatformControllersConfig();

        // Pass 1: read this file's raw <Controller> list, resolving any root-level file
        // inheritFrom first — a `<Controllers inheritFrom="_Base">` file pulls in the base file's
        // controllers and merges its own on top (override by name, append new names). This lets
        // many platforms that share a controller layout reduce to a one-line pointer at a shared
        // base file. No controller-level inheritance is resolved yet.
        List<ParsedController> parsed = LoadControllersRaw(path, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

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

            controller.Mappings.AddRange(ResolveInheritedMappings(pc, byName, path));
            result.Controllers.Add(controller);

            _logger.Debug($"Controller '{controller.Name}': {controller.Mappings.Count} mappings (inheritFrom={pc.InheritFrom ?? "none"}), default={controller.IsDefault}, analogToDigital={controller.AnalogToDigital}");
        }

        _logger.Debug($"Platform controllers: {result.Controllers.Count}");
        return result;
    }

    /// <summary>
    /// Reads a Controllers file into a raw <see cref="ParsedController"/> list, resolving a
    /// root-level <c>&lt;Controllers inheritFrom="OtherFile"&gt;</c> reference by loading that file
    /// first and merging this file's own controllers on top. Root-level inheritance is transitive
    /// (a base may itself inheritFrom another) with cycles detected via <paramref name="visited"/>
    /// and a missing base logged; either case falls back to this file's own controllers.
    /// </summary>
    private List<ParsedController> LoadControllersRaw(string path, HashSet<string> visited)
    {
        using Stream stream = _lfs.OpenRead(path);
        var doc = new XmlDocument();
        doc.Load(stream);
        XmlElement root = doc.DocumentElement!;

        var own = new List<ParsedController>();
        foreach (XmlElement node in root.ChildNodes.OfType<XmlElement>())
        {
            if (node.Name != "Controller") continue;
            ParsedController? pc = ParseControllerRaw(node, path);
            if (pc != null) own.Add(pc);
        }

        string? baseName = root.Attributes["inheritFrom"]?.Value;
        if (string.IsNullOrEmpty(baseName)) return own;

        string basePath = _lfs.Resolve("Controllers", baseName.SafeFileName() + ".xml");
        if (!_lfs.FileExists(basePath))
        {
            _logger.Error($"Controllers file {path}: inheritFrom='{baseName}' resolves to '{basePath}' which does not exist; using own controllers only");
            return own;
        }
        if (!visited.Add(baseName))
        {
            _logger.Error($"Controllers file {path}: root inheritFrom chain forms a cycle at '{baseName}'; stopping resolution");
            return own;
        }

        List<ParsedController> baseControllers = LoadControllersRaw(basePath, visited);
        return MergeControllers(baseControllers, own);
    }

    /// <summary>
    /// Overlays <paramref name="own"/> controllers onto <paramref name="baseList"/>: a controller
    /// whose name matches a base entry replaces it in place; a new name is appended after the base
    /// entries. Base document order is preserved for the shared controllers.
    /// </summary>
    private static List<ParsedController> MergeControllers(List<ParsedController> baseList, List<ParsedController> own)
    {
        var merged = new List<ParsedController>(baseList);
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
    private List<MappingEntry> ResolveInheritedMappings(
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
        return mappings;
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
        foreach (XmlElement child in node.ChildNodes.OfType<XmlElement>())
        {
            if (child.Name != "Mapping") continue;
            MappingEntry? entry = ParseMappingEntry(child, path);
            if (entry != null) ownMappings.Add(entry);
        }

        return new ParsedController(name, isDefault, a2d, inheritFrom, ownMappings);
    }

    private sealed record ParsedController(
        string Name,
        bool IsDefault,
        AnalogToDigitalMode? AnalogToDigital,
        string? InheritFrom,
        List<MappingEntry> OwnMappings);

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
