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
    /// <c>&lt;Controller&gt;</c> blocks, each with its own button vocabulary. Root-level
    /// <c>&lt;Mapping&gt;</c> entries (direct children of the root, outside any
    /// <c>&lt;Controller&gt;</c>) form a shared baseline merged into every controller. Returns
    /// null when no controllers file exists for the given platform.
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
        using Stream stream = _lfs.OpenRead(path);
        var doc = new XmlDocument();
        doc.Load(stream);
        XmlElement root = doc.DocumentElement!;

        // Root-level <Mapping> entries form a shared baseline inherited by every <Controller>:
        // buttons common to all variants (e.g. A/B/C/Start/Dpad) are declared once here, and each
        // controller adds only the buttons unique to it.
        var shared = new List<MappingEntry>();
        foreach (XmlElement node in root.ChildNodes.OfType<XmlElement>())
        {
            if (node.Name != "Mapping") continue;
            MappingEntry? entry = ParseMappingEntry(node, path);
            if (entry != null) shared.Add(entry);
        }

        foreach (XmlElement node in root.ChildNodes.OfType<XmlElement>())
        {
            if (node.Name != "Controller") continue;
            ControllerConfig? controller = ParseController(node, path, shared);
            if (controller != null) result.Controllers.Add(controller);
        }

        _logger.Debug($"Platform controllers: {result.Controllers.Count}, shared mappings: {shared.Count}");
        return result;
    }

    private ControllerConfig? ParseController(XmlElement node, string path, List<MappingEntry> shared)
    {
        string? name = node.Attributes["name"]?.Value;
        if (string.IsNullOrEmpty(name))
        {
            _logger.Error($"Skipping <Controller> in {path}: missing 'name' attribute");
            return null;
        }

        var controller = new ControllerConfig { Name = name };
        string? defaultAttr = node.Attributes["default"]?.Value;
        if (defaultAttr != null && bool.TryParse(defaultAttr, out bool isDefault))
            controller.IsDefault = isDefault;

        if (TryParseAnalogToDigital(node, path, out AnalogToDigitalMode? a2d))
            controller.AnalogToDigital = a2d;

        // Shared mappings first, then controller-specific ones — so the controller's own entries
        // sit after the baseline it inherits.
        controller.Mappings.AddRange(shared);
        foreach (XmlElement child in node.ChildNodes.OfType<XmlElement>())
        {
            if (child.Name != "Mapping") continue;
            MappingEntry? entry = ParseMappingEntry(child, path);
            if (entry != null) controller.Mappings.Add(entry);
        }

        _logger.Debug($"Controller '{controller.Name}': {controller.Mappings.Count} mappings ({shared.Count} shared), default={controller.IsDefault}, analogToDigital={controller.AnalogToDigital}");
        return controller;
    }

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
