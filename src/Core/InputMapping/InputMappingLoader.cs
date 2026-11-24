using System.Xml;

namespace DynamicControls.InputMapping;

/// <summary>
/// Locates and parses a platform's Controllers.xml — its controller vocabulary and its per-game
/// remaps — into thin DTOs. A game's remap is left unresolved against its controller: the provider
/// that answers for it does that.
/// </summary>
public interface IInputMappingLoader
{
    /// <summary>
    /// Loads the platform-level controllers file (<c>Controllers.xml</c>) — one or more
    /// <c>&lt;Controller&gt;</c> blocks, each with its own button vocabulary. A controller may
    /// declare <c>inheritFrom="OtherControllerName"</c> to prepend another controller's mappings
    /// before its own; inheritance is transitive (the base may itself inheritFrom a third, and so
    /// on) with cycles detected and broken. The root <c>&lt;Controllers&gt;</c> element may also
    /// carry <c>inheritFrom="OtherFile"</c> to pull in a shared base file's controllers and merge
    /// this file's own on top (override by name, append new) — letting platforms that share a
    /// layout reduce to a one-line pointer. <c>&lt;Game launchBoxId="..." romName="..."&gt;</c>
    /// entries alongside the controllers remap one of them for a single game; a root inheritFrom
    /// brings in the base file's controllers but not its games. A <c>User\</c> file is merged over
    /// the shipped one the same way, controller by controller and game by game, rather than
    /// replacing it. Returns null when no controllers file exists for the given platform in either
    /// layer.
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
    public PlatformControllersConfig? LoadPlatformMapping(string platform)
    {
        var result = new PlatformControllersConfig();

        // Pass 1: read this platform's raw <Controller> list — the user's file laid over the
        // shipped one — resolving any root-level file inheritFrom first: a
        // `<Controllers inheritFrom="_Base">` file pulls in the base file's controllers and merges
        // its own on top (override by name, append new names). This lets many platforms that share
        // a controller layout reduce to a one-line pointer at a shared base file. No
        // controller-level inheritance is resolved yet.
        ParsedPlatform? raw = LoadControllersRaw(platform, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { platform });
        if (raw == null) return null;
        List<ParsedController> parsed = raw.Controllers;
        string path = ControllersPath(platform);
        result.Games.AddRange(raw.Games);
        string? defaultName = ChooseDefault(raw, path);

        // Pass 2: build the final ControllerConfig list. For each controller, walk its inheritFrom
        // chain to the root and prepend every ancestor's own mappings in order (root ancestor first,
        // this controller's own mappings last). Inheritance is transitive — a controller that
        // inheritsFrom another which itself inheritsFrom a third accumulates all three levels.
        var byName = parsed.ToDictionary(pc => pc.Name);
        foreach (ParsedController pc in parsed)
        {
            (List<MappingEntry> inheritedMappings, List<ControlEntry> inheritedControls, AnalogToDigitalMode? a2d) =
                ResolveInheritedMappings(pc, byName, path);
            var controller = new ControllerConfig
            {
                Name = pc.Name,
                IsDefault = pc.Name == defaultName,
                AnalogToDigital = a2d,
            };
            controller.Mappings.AddRange(inheritedMappings);
            controller.Controls.AddRange(inheritedControls);
            result.Controllers.Add(controller);

            _logger.Debug($"Controller '{controller.Name}': {controller.Mappings.Count} mappings (inheritFrom={pc.InheritFrom ?? "none"}), default={controller.IsDefault}, analogToDigital={controller.AnalogToDigital}");
        }

        _logger.Debug($"Platform controllers: {result.Controllers.Count}, games: {result.Games.Count}");
        return result;
    }

    /// <summary>
    /// Picks the platform default from the root <c>default</c> attributes, nearest first: the
    /// user's file, the shipped one, then the platform a root inheritFrom names, worked out the same
    /// way. A name that matches no controller is logged and passed over, so a typo costs nothing but
    /// the line it is on. Null when none names a controller, leaving
    /// <see cref="PlatformControllersConfig.Resolve"/> to take the first.
    /// </summary>
    private string? ChooseDefault(ParsedPlatform raw, string path)
    {
        foreach ((string name, string from) in raw.Defaults)
        {
            if (raw.Controllers.Any(c => c.Name == name))
            {
                _logger.Debug($"Controllers {path}: default '{name}' (from {from})");
                return name;
            }
            _logger.Error($"Controllers file {from}: default='{name}' names no controller; ignoring it");
        }
        return null;
    }

    /// <summary>
    /// Reads a platform's Controllers files into a raw <see cref="ParsedController"/> list: the
    /// <c>User\</c> file's controllers laid over the <c>Defaults\</c> file's by name (see
    /// <see cref="MergeControllers"/>), so a user's file holds only what it changes. Then resolves a
    /// root-level <c>&lt;Controllers inheritFrom="OtherFile"&gt;</c> reference — the user's when its
    /// file states one, an empty one included, else the shipped one — by loading that platform the
    /// same way and merging this one's controllers on top. Root-level inheritance is transitive (a
    /// base may itself inheritFrom another) with cycles detected via <paramref name="visited"/> and
    /// a missing base logged; either case falls back to this platform's own controllers. The games are this
    /// platform's own, the user's laid over the shipped ones (see <see cref="MergeGames"/>): a
    /// game's ID and ROM name belong to its platform, so a base file's are not brought in. The root
    /// <c>default</c>s are collected nearest first — user, shipped, then the base's — for
    /// <see cref="ChooseDefault"/>. Returns null when neither layer has a file.
    /// </summary>
    private ParsedPlatform? LoadControllersRaw(string platform, HashSet<string> visited)
    {
        string relative = ControllersPath(platform);
        bool defaultsExists = _lfs.Defaults.FileExists(relative);
        bool userExists = _lfs.User.FileExists(relative);
        _logger.Debug($"Controllers '{relative}' — defaults: {(defaultsExists ? "found" : "missing")}, user: {(userExists ? "found" : "missing")}");
        if (!defaultsExists && !userExists) return null;

        ParsedFile? shipped = defaultsExists ? ParseFile(_lfs.Defaults.OpenRead(relative), Path.Combine("Defaults", relative)) : null;
        ParsedFile? user = userExists ? ParseFile(_lfs.User.OpenRead(relative), Path.Combine("User", relative)) : null;

        List<ParsedController> own = MergeControllers(shipped?.Controllers ?? [], user?.Controllers ?? []);
        List<GameMappingConfig> games = MergeGames(shipped?.Games ?? [], user?.Games ?? []);
        List<(string Name, string From)> defaults = [.. new[] { user, shipped }
            .Where(f => f?.Default != null)
            .Select(f => (f!.Default!, f.Path))];
        string? baseName = user?.InheritFrom ?? shipped?.InheritFrom;
        if (string.IsNullOrEmpty(baseName)) return new ParsedPlatform(own, games, defaults);

        if (!visited.Add(baseName))
        {
            _logger.Error($"Controllers file {relative}: root inheritFrom chain forms a cycle at '{baseName}'; stopping resolution");
            return new ParsedPlatform(own, games, defaults);
        }
        ParsedPlatform? basePlatform = LoadControllersRaw(baseName, visited);
        if (basePlatform == null)
        {
            _logger.Error($"Controllers file {relative}: inheritFrom='{baseName}' names '{ControllersPath(baseName)}', found in neither User\\Platforms nor Defaults\\Platforms; using own controllers only");
            return new ParsedPlatform(own, games, defaults);
        }

        return new ParsedPlatform(MergeControllers(basePlatform.Controllers, own), games, [.. defaults, .. basePlatform.Defaults]);
    }

    /// <summary>One layer's Controllers file as written: its controllers, its games and its root
    /// inheritFrom and default.</summary>
    private ParsedFile ParseFile(Stream openedStream, string path)
    {
        using Stream stream = openedStream;
        var doc = new XmlDocument();
        doc.Load(stream);
        XmlElement root = doc.DocumentElement!;

        var controllers = new List<ParsedController>();
        var games = new List<GameMappingConfig>();
        foreach (XmlElement node in root.ChildNodes.OfType<XmlElement>())
        {
            switch (node.Name)
            {
                case "Controller":
                    if (ParseControllerRaw(node, path) is ParsedController pc) AddController(controllers, pc, path);
                    break;
                case "Game":
                    if (ParseGame(node, path) is GameMappingConfig game) AddGame(games, game, path);
                    break;
                default:
                    _logger.Error($"Skipping <{node.Name}> in {path}: expected <Controller> or <Game>");
                    break;
            }
        }

        // An empty inheritFrom still states something: that this file inherits from no platform,
        // which is how a user's file drops the shipped file's pointer. So only an absent one is null.
        string? inheritFrom = root.Attributes["inheritFrom"]?.Value.Trim();
        string? defaultName = root.Attributes["default"]?.Value;
        return new ParsedFile(path, controllers, games,
            inheritFrom,
            string.IsNullOrEmpty(defaultName) ? null : defaultName);
    }

    /// <summary>
    /// Adds <paramref name="controller"/> to one file's controllers unless an earlier one has its
    /// name: which of the two applied would come down to document order, so the first is kept and
    /// the second logged, as a game stated twice is.
    /// </summary>
    private void AddController(List<ParsedController> controllers, ParsedController controller, string path)
    {
        if (controllers.Any(c => c.Name == controller.Name))
        {
            _logger.Error($"Skipping the second <Controller name=\"{controller.Name}\"> in {path}: the name is already taken");
            return;
        }
        controllers.Add(controller);
    }

    /// <summary>
    /// Adds <paramref name="game"/> to one file's games unless an earlier entry already has its ID
    /// or ROM name: the two would be one game stated twice, and which one applied would come down
    /// to document order, so the first is kept and the second logged.
    /// </summary>
    private void AddGame(List<GameMappingConfig> games, GameMappingConfig game, string path)
    {
        if (games.FirstOrDefault(g => SameGame(g, game)) is GameMappingConfig earlier)
        {
            _logger.Error($"Skipping <Game {Describe(game)}> in {path}: <Game {Describe(earlier)}> already names that game");
            return;
        }
        games.Add(game);
    }

    /// <summary>
    /// Lays the user's games over the shipped ones: a user entry replaces, whole, every shipped
    /// entry it shares an ID or a ROM name with, taking the first one's place, and is appended when
    /// it shares neither. Whole entries rather than keys, so a user entry naming a game by its ROM
    /// alone still replaces a shipped one that also has an ID — which an ID lookup would otherwise
    /// find first. Each user entry is marked <see cref="GameMappingConfig.FromUser"/>, for the
    /// shipped entries the merge can't see are the same game: those sharing no key with it.
    /// </summary>
    private static List<GameMappingConfig> MergeGames(List<GameMappingConfig> shipped, List<GameMappingConfig> user)
    {
        var merged = new List<GameMappingConfig>(shipped);
        foreach (GameMappingConfig game in user)
        {
            game.FromUser = true;
            int at = merged.FindIndex(g => SameGame(g, game));
            if (at < 0)
            {
                merged.Add(game);
                continue;
            }
            merged[at] = game;
            merged.RemoveAll(g => !ReferenceEquals(g, game) && SameGame(g, game));
        }
        return merged;
    }

    private static bool SameGame(GameMappingConfig a, GameMappingConfig b) =>
        (a.LaunchBoxId != null && a.LaunchBoxId == b.LaunchBoxId)
        || (a.RomName != null && string.Equals(a.RomName, b.RomName, StringComparison.OrdinalIgnoreCase));

    private static string Describe(GameMappingConfig game) =>
        string.Join(" ", new[]
        {
            game.LaunchBoxId is int id ? $"launchBoxId=\"{id}\"" : null,
            game.RomName is string rom ? $"romName=\"{rom}\"" : null,
        }.Where(k => k != null));

    /// <summary>
    /// Parses a &lt;Game&gt;: the keys it is found by, the controller it remaps, and the remap
    /// itself. An entry with neither key could never be found, so it is logged and skipped.
    /// </summary>
    private GameMappingConfig? ParseGame(XmlElement node, string path)
    {
        string? idText = node.Attributes["launchBoxId"]?.Value;
        int? id = null;
        if (idText != null)
        {
            if (int.TryParse(idText, out int parsed)) id = parsed;
            else _logger.Error($"Ignoring launchBoxId=\"{idText}\" on a <Game> in {path}: not a number");
        }
        string? romName = node.Attributes["romName"]?.Value;
        if (string.IsNullOrEmpty(romName)) romName = null;
        if (id == null && romName == null)
        {
            _logger.Error($"Skipping <Game> in {path}: it needs a launchBoxId or a romName to be found by");
            return null;
        }

        string? inheritFrom = node.Attributes["inheritFrom"]?.Value;
        var game = new GameMappingConfig { LaunchBoxId = id, RomName = romName, InheritFrom = string.IsNullOrEmpty(inheritFrom) ? null : inheritFrom };
        if (TryParseAnalogToDigital(node, path, out AnalogToDigitalMode? a2d))
        {
            game.AnalogToDigital = a2d;
            game.StatesAnalogToDigital = true;
        }

        foreach (XmlElement child in node.ChildNodes.OfType<XmlElement>())
        {
            switch (child.Name)
            {
                case "Mapping":
                    if (ParseMappingEntry(child, path) is MappingEntry entry) game.Mappings.Add(entry);
                    break;
                case "Unmap":
                    if (ParseUnmapName(child, path) is string unmapped) game.Unmaps.Add(unmapped);
                    break;
                case "Control":
                    if (ParseControlEntry(child, path) is ControlEntry control) game.Controls.Add(control);
                    break;
                default:
                    _logger.Error($"Invalid element <{child.Name}> in <Game {Describe(game)}> in {path}");
                    break;
            }
        }

        _logger.Debug($"Game {Describe(game)}: inheritFrom={game.InheritFrom ?? "none"}, {game.Mappings.Count} mappings, {game.Unmaps.Count} unmaps");
        return game;
    }

    private static string ControllersPath(string platform) =>
        Path.Combine("Platforms", platform.SafeFileName(), "Controllers.xml");

    /// <summary>
    /// Overlays <paramref name="own"/> controllers onto <paramref name="baseList"/>: a controller
    /// whose name matches a base entry replaces it in place; a new name is appended after the base
    /// entries. Base document order is preserved for the shared controllers. Which one is the
    /// default is the root's to say, not a controller's, so replacing one never moves it.
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
    /// whose Name matches a parent entry replaces it, a child's &lt;Unmap&gt; drops it, and names
    /// the child doesn't mention are inherited unchanged. analogToDigital is the nearest level's
    /// that states one, <c>none</c> included, so a controller with a stick of its own can switch
    /// off the mirror its base has. A missing base controller or an inheritFrom cycle is logged
    /// and stops the walk, so resolution never loops or throws.
    /// </summary>
    private (List<MappingEntry> Mappings, List<ControlEntry> Controls, AnalogToDigitalMode? AnalogToDigital) ResolveInheritedMappings(
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
        List<MappingEntry> mappings = [];
        for (int i = chain.Count - 1; i >= 0; i--)
            mappings = MappingOverlay.Apply(mappings, chain[i].OwnMappings, chain[i].OwnUnmaps);

        // Controls inherit by the same rule, keyed on their own name.
        List<ControlEntry> controls = [];
        for (int i = chain.Count - 1; i >= 0; i--)
            controls = MappingOverlay.ApplyControls(controls, chain[i].OwnControls, chain[i].OwnUnmaps);

        AnalogToDigitalMode? a2d = chain.FirstOrDefault(c => c.StatesAnalogToDigital)?.AnalogToDigital;
        return (mappings, controls, a2d);
    }

    private ParsedController? ParseControllerRaw(XmlElement node, string path)
    {
        string? name = node.Attributes["name"]?.Value;
        if (string.IsNullOrEmpty(name))
        {
            _logger.Error($"Skipping <Controller> in {path}: missing 'name' attribute");
            return null;
        }

        if (node.Attributes["default"] != null)
            _logger.Error($"Ignoring default= on <Controller name=\"{name}\"> in {path}: the default is named on the root, <Controllers default=\"{name}\">");

        bool statesA2d = TryParseAnalogToDigital(node, path, out AnalogToDigitalMode? a2d);

        string? inheritFrom = node.Attributes["inheritFrom"]?.Value;

        var ownMappings = new List<MappingEntry>();
        var ownUnmaps = new List<string>();
        var ownControls = new List<ControlEntry>();
        foreach (XmlElement child in node.ChildNodes.OfType<XmlElement>())
        {
            switch (child.Name)
            {
                case "Mapping":
                    MappingEntry? entry = ParseMappingEntry(child, path);
                    if (entry != null) ownMappings.Add(entry);
                    break;
                case "Unmap":
                    if (ParseUnmapName(child, path) is string unmapped) ownUnmaps.Add(unmapped);
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

        return new ParsedController(name, statesA2d, a2d, inheritFrom, ownMappings, ownUnmaps, ownControls);
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

    private sealed record ParsedFile(string Path, List<ParsedController> Controllers, List<GameMappingConfig> Games, string? InheritFrom, string? Default);

    /// <param name="Defaults">Every root <c>default</c> in reach, nearest first, with the file
    /// stating it.</param>
    private sealed record ParsedPlatform(List<ParsedController> Controllers, List<GameMappingConfig> Games, List<(string Name, string From)> Defaults);

    /// <param name="StatesAnalogToDigital">The controller sets analogToDigital itself, <c>none</c>
    /// included, rather than inheriting its base's.</param>
    private sealed record ParsedController(
        string Name,
        bool StatesAnalogToDigital,
        AnalogToDigitalMode? AnalogToDigital,
        string? InheritFrom,
        List<MappingEntry> OwnMappings,
        List<string> OwnUnmaps,
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

    private string? ParseUnmapName(XmlElement node, string path)
    {
        string? name = node.Attributes["name"]?.Value;
        if (!string.IsNullOrEmpty(name)) return name;
        _logger.Error($"Skipping <Unmap> in {path}: missing 'name' attribute");
        return null;
    }

    /// <summary>
    /// Reads an <c>analogToDigital</c> attribute. Returns true when the element states one —
    /// <c>none</c> included, with a null <paramref name="result"/> — and false when it is absent
    /// or invalid, so the value is inherited.
    /// </summary>
    private bool TryParseAnalogToDigital(XmlElement node, string path, out AnalogToDigitalMode? result)
    {
        result = null;
        string? attr = node.Attributes["analogToDigital"]?.Value;
        if (attr == null) return false;
        switch (attr.ToLowerInvariant())
        {
            case "left": result = AnalogToDigitalMode.Left; return true;
            case "right": result = AnalogToDigitalMode.Right; return true;
            case "none": return true;
            default:
                break;
        }
        _logger.Error($"Invalid analogToDigital value '{attr}' in {path} (expected 'left', 'right' or 'none')");
        return false;
    }
}
