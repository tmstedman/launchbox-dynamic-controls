using System.Xml;
using DynamicControls.Config;

namespace DynamicControls.Labels;

/// <summary>
/// Production implementation: loads labels from a per-platform
/// <c>Labels/{platform}.xml</c> file that combines all game entries and platform defaults in one
/// place. Both the <c>Defaults\</c> and <c>User\</c> tiers are read and merged at the entry
/// level — User entries win over Defaults entries, matched first by <c>id</c> (database ID)
/// then by <c>name</c> (ROM name). The <c>&lt;Defaults&gt;</c> block is merged the same way:
/// User button entries override Defaults button entries by name.
///
/// <para>File format. Each entry names a platform button in the <c>name</c> attribute and carries
/// its display text as the element's content:</para>
/// <code>
/// &lt;Labels&gt;
///   &lt;Defaults&gt;
///     &lt;Input name="Start"&gt;Pause&lt;/Input&gt;
///   &lt;/Defaults&gt;
///   &lt;Game launchBoxId="12345" romName="Sonic the Hedgehog (USA, Europe)"&gt;
///     &lt;Input name="A"&gt;Jump&lt;/Input&gt;
///   &lt;/Game&gt;
/// &lt;/Labels&gt;
/// </code>
/// <para>A space-separated <c>name</c> denotes a button combination. Those entries parse without
/// error but are ignored, since resolving them needs mapping support that does not exist yet.</para>
/// </summary>
public class InputLabelsLoader(ILogger logger, LayeredFileSystem lfs) : IInputLabelsLoader
{
    private readonly ILogger _logger = logger;
    private readonly LayeredFileSystem _lfs = lfs;
    private readonly Dictionary<string, PlatformLabels?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public bool IsEnabled(GlobalConfig config) => true;

    public InputLabelsConfig? Load(GameInfo game)
    {
        PlatformLabels? platform = LoadPlatform(game.Platform);
        if (platform == null) return null;

        InputLabelsConfig? result = null;

        if (game.LaunchBoxId != null)
            platform.ById.TryGetValue(game.LaunchBoxId.Value, out result);

        result ??= platform.ByName.GetValueOrDefault(game.RomName);

        if (result == null)
        {
            string normalized = game.RomName.NormalizeRomName();
            platform.ByNormalizedName.TryGetValue(normalized, out result);
            if (result != null)
                _logger.Debug($"Label entry found by fuzzy romName match: '{game.RomName}' -> '{normalized}'");
        }

        if (result == null)
            _logger.Debug($"No label entry found for launchBoxId='{game.LaunchBoxId}' romName='{game.RomName}'");

        return result;
    }

    public InputLabelsConfig? LoadDefaultLabels(string platform)
    {
        PlatformLabels? data = LoadPlatform(platform);
        return data?.Defaults.Labels.Count > 0 ? data.Defaults : null;
    }

    private PlatformLabels? LoadPlatform(string platform)
    {
        if (_cache.TryGetValue(platform, out PlatformLabels? cached))
            return cached;

        string safePlatform = platform.SafeFileName();
        string relativePath = Path.Combine("Labels", safePlatform + ".xml");

        bool defaultsExists = _lfs.Defaults.FileExists(relativePath);
        bool userExists = _lfs.User.FileExists(relativePath);
        _logger.Debug($"Platform labels '{relativePath}' — defaults: {(defaultsExists ? "found" : "missing")}, user: {(userExists ? "found" : "missing")}");

        PlatformLabels? result = null;
        if (defaultsExists || userExists)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            PlatformFile? defaultsFile = defaultsExists
                ? ParseFile(_lfs.Defaults.OpenRead(relativePath), Path.Combine("Defaults", relativePath))
                : null;
            PlatformFile? userFile = userExists
                ? ParseFile(_lfs.User.OpenRead(relativePath), Path.Combine("User", relativePath))
                : null;
            result = Merge(defaultsFile, userFile);
            _logger.Debug($"Platform labels '{platform}' loaded in {sw.ElapsedMilliseconds}ms");
        }

        _cache[platform] = result;
        return result;
    }

    private PlatformFile ParseFile(Stream openedStream, string path)
    {
        using Stream stream = openedStream;
        var file = new PlatformFile();
        var doc = new XmlDocument();
        doc.Load(stream);
        XmlElement root = doc.DocumentElement!;

        foreach (XmlElement node in root.ChildNodes.OfType<XmlElement>())
        {
            if (node.Name == "Defaults")
            {
                file.Defaults = ParseLabels(node, path);
            }
            else if (node.Name == "Game")
            {
                string? idStr = node.Attributes["launchBoxId"]?.Value;
                int? id = idStr != null && int.TryParse(idStr, out int parsed) ? parsed : null;
                string? name = node.Attributes["romName"]?.Value;
                InputLabelsConfig labels = ParseLabels(node, path);
                file.Games.Add(new GameEntry(id, name, labels));
            }
        }

        return file;
    }

    private InputLabelsConfig ParseLabels(XmlElement parent, string path)
    {
        var result = new InputLabelsConfig();
        foreach (XmlElement node in parent.ChildNodes.OfType<XmlElement>())
        {
            if (node.Name != "Input")
            {
                _logger.Error($"Skipping <{node.Name}> in {path}: expected <Input name=\"...\">label</Input>");
                continue;
            }

            string? name = node.Attributes["name"]?.Value;
            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.Error($"Skipping <Input> in {path}: missing 'name' attribute");
                continue;
            }

            // A space-separated name means "these buttons pressed together". Not yet resolvable,
            // so the entry is skipped rather than mis-attributed to one of the buttons named.
            if (name.Contains(' '))
            {
                _logger.Info($"Ignoring <Input name=\"{name}\"> in {path}: labels for button combinations are not supported yet");
                continue;
            }

            string label = node.InnerText.Trim();
            if (string.IsNullOrEmpty(label))
            {
                _logger.Error($"Skipping <Input name=\"{name}\"> in {path}: element has no text value");
                continue;
            }

            result.Labels.Add(new LabelEntry { Name = name, Label = label });
        }
        return result;
    }

    private static PlatformLabels Merge(PlatformFile? defaults, PlatformFile? user)
    {
        var byId = new Dictionary<int, InputLabelsConfig>();
        var byName = new Dictionary<string, InputLabelsConfig>(StringComparer.OrdinalIgnoreCase);
        var byNormalizedName = new Dictionary<string, InputLabelsConfig>(StringComparer.OrdinalIgnoreCase);

        foreach (GameEntry e in defaults?.Games ?? [])
        {
            if (e.Id != null) byId[e.Id.Value] = e.Labels;
            if (e.Name != null) { byName[e.Name] = e.Labels; byNormalizedName[e.Name.NormalizeRomName()] = e.Labels; }
        }
        foreach (GameEntry e in user?.Games ?? [])
        {
            if (e.Id != null) byId[e.Id.Value] = e.Labels;
            if (e.Name != null) { byName[e.Name] = e.Labels; byNormalizedName[e.Name.NormalizeRomName()] = e.Labels; }
        }

        InputLabelsConfig mergedDefaults = MergeDefaults(defaults?.Defaults, user?.Defaults);
        return new PlatformLabels(mergedDefaults, byId, byName, byNormalizedName);
    }

    private static InputLabelsConfig MergeDefaults(InputLabelsConfig? defaults, InputLabelsConfig? user)
    {
        var result = new InputLabelsConfig();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (LabelEntry e in user?.Labels ?? [])
        {
            result.Labels.Add(e);
            seen.Add(e.Name);
        }
        foreach (LabelEntry e in defaults?.Labels ?? [])
        {
            if (!seen.Contains(e.Name))
                result.Labels.Add(e);
        }
        return result;
    }

    private sealed class PlatformFile
    {
        public InputLabelsConfig? Defaults { get; set; }
        public List<GameEntry> Games { get; } = [];
    }

    private sealed record GameEntry(int? Id, string? Name, InputLabelsConfig Labels);

    private sealed record PlatformLabels(
        InputLabelsConfig Defaults,
        IReadOnlyDictionary<int, InputLabelsConfig> ById,
        IReadOnlyDictionary<string, InputLabelsConfig> ByName,
        IReadOnlyDictionary<string, InputLabelsConfig> ByNormalizedName);
}
