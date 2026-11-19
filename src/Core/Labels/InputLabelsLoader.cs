using System.Xml;
using DynamicControls.Config;

namespace DynamicControls.Labels;

/// <summary>
/// Production implementation: loads labels from a per-platform
/// <c>Platforms/{platform}/Labels.xml</c> file that combines all game entries and platform defaults
/// in one place. Both the <c>Defaults\</c> and <c>User\</c> tiers are read and merged at the entry
/// level — User entries win over Defaults entries, matched first by <c>id</c> (database ID)
/// then by <c>name</c> (ROM name). The two <c>&lt;Defaults&gt;</c> blocks are returned as layers,
/// User first, for <see cref="InputLabelsService"/> to merge: whether two entries name the same
/// buttons depends on the mapping (a MAME port can be named two ways), which only it has.
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
/// <para>A space-separated <c>name</c> denotes a button combination — an action performed by
/// pressing those buttons together. It is carried through unchanged; <see cref="InputLabelsService"/>
/// resolves it against the mapping.</para>
///
/// <para>A game whose controls change with the vehicle, view or mode can also declare
/// <c>&lt;ControlSet name="..."&gt;</c>s, each labelling every control as it stands in that
/// context. The <c>&lt;Input&gt;</c>s directly under <c>&lt;Game&gt;</c> stay its default — one
/// picture of all of it — and a set takes nothing from them. A User entry still replaces the
/// shipped entry whole, sets and all, so leaving a set out of it is how a set is removed.</para>
/// </summary>
public class InputLabelsLoader(ILogger logger, LayeredFileSystem lfs) : IInputLabelsLoader
{
    private readonly ILogger _logger = logger;
    private readonly LayeredFileSystem _lfs = lfs;
    private readonly Dictionary<string, PlatformLabels?> _cache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] LineBreaks = ["\r\n", "\n"];

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

    public IReadOnlyList<InputLabelsConfig> LoadDefaultLabels(string platform) =>
        LoadPlatform(platform)?.Defaults ?? [];

    private PlatformLabels? LoadPlatform(string platform)
    {
        if (_cache.TryGetValue(platform, out PlatformLabels? cached))
            return cached;

        string safePlatform = platform.SafeFileName();
        string relativePath = Path.Combine("Platforms", safePlatform, "Labels.xml");

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
                InputLabelsConfig labels = ParseLabels(node, path, controlSetsAllowed: true);
                file.Games.Add(new GameEntry(id, name, labels));
            }
            else if (node.Name == "ControlSet")
            {
                _logger.Error($"Skipping <ControlSet> in {path}: a control set belongs directly inside <Game>");
            }
            else
            {
                _logger.Error($"Skipping <{node.Name}> in {path}: expected <Defaults> or <Game>");
            }
        }

        return file;
    }

    /// <summary>
    /// Reads the <c>&lt;Input&gt;</c> children of <paramref name="parent"/>, and when
    /// <paramref name="controlSetsAllowed"/> (directly inside a <c>&lt;Game&gt;</c>) its
    /// <c>&lt;ControlSet&gt;</c>s too. A set anywhere else is refused: in <c>&lt;Defaults&gt;</c>
    /// it would belong to every game, and inside another set it would be a set of a set.
    /// </summary>
    private InputLabelsConfig ParseLabels(XmlElement parent, string path, bool controlSetsAllowed = false)
    {
        var result = new InputLabelsConfig();
        foreach (XmlElement node in parent.ChildNodes.OfType<XmlElement>())
        {
            if (node.Name == "Input")
            {
                if (ParseInput(node, path) is LabelEntry entry) result.Labels.Add(entry);
            }
            else if (node.Name == "ControlSet" && controlSetsAllowed)
            {
                if (ParseControlSet(node, path, result.ControlSets) is ControlSetConfig set) result.ControlSets.Add(set);
            }
            else if (node.Name == "ControlSet")
            {
                _logger.Error($"Skipping <ControlSet> in {path}: a control set belongs directly inside <Game>");
            }
            else
            {
                _logger.Error($"Skipping <{node.Name}> in {path}: expected <Input name=\"...\">label</Input>");
            }
        }
        return result;
    }

    private LabelEntry? ParseInput(XmlElement node, string path)
    {
        string? name = node.Attributes["name"]?.Value;
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.Error($"Skipping <Input> in {path}: missing 'name' attribute");
            return null;
        }

        string label = LabelText(node);
        if (string.IsNullOrEmpty(label))
        {
            _logger.Error($"Skipping <Input name=\"{name}\"> in {path}: element has no text value");
            return null;
        }

        return new LabelEntry { Name = name, Label = label };
    }

    /// <summary>
    /// Reads one <c>&lt;ControlSet&gt;</c>, or null when it can't be one: a set is shown under
    /// its name, so it needs one, distinct from every <paramref name="earlier"/> set's (compared
    /// case-insensitively, since two that differ only in case read as one), and a set with no
    /// labels says nothing.
    /// </summary>
    private ControlSetConfig? ParseControlSet(XmlElement node, string path, List<ControlSetConfig> earlier)
    {
        string? name = node.Attributes["name"]?.Value.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.Error($"Skipping <ControlSet> in {path}: missing 'name' attribute");
            return null;
        }

        if (earlier.Any(set => SameSetName(set.Name, name)))
        {
            _logger.Error($"Skipping <ControlSet name=\"{name}\"> in {path}: a set of that name is already declared for this game");
            return null;
        }

        InputLabelsConfig labels = ParseLabels(node, path);
        if (labels.Labels.Count == 0)
        {
            _logger.Error($"Skipping <ControlSet name=\"{name}\"> in {path}: it has no <Input> labels");
            return null;
        }

        return new ControlSetConfig { Name = name, Labels = labels.Labels };
    }

    private static bool SameSetName(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// An entry's text in one form however it was written: each line trimmed and the lines joined
    /// by <c>\n</c>. A break written <c>&amp;#10;</c> and one typed into the file (which
    /// <see cref="XmlDocument"/> keeps as CRLF, indent and all) then read the same, so two entries
    /// saying the same thing compare equal wherever labels are compared.
    /// </summary>
    private static string LabelText(XmlElement node) =>
        string.Join("\n", node.InnerText.Split(LineBreaks, StringSplitOptions.None).Select(l => l.Trim())).Trim();

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

        InputLabelsConfig[] defaultLayers = [.. new[] { user?.Defaults, defaults?.Defaults }
            .OfType<InputLabelsConfig>()
            .Where(layer => layer.Labels.Count > 0)];
        return new PlatformLabels(defaultLayers, byId, byName, byNormalizedName);
    }

    private sealed class PlatformFile
    {
        public InputLabelsConfig? Defaults { get; set; }
        public List<GameEntry> Games { get; } = [];
    }

    private sealed record GameEntry(int? Id, string? Name, InputLabelsConfig Labels);

    private sealed record PlatformLabels(
        IReadOnlyList<InputLabelsConfig> Defaults,
        IReadOnlyDictionary<int, InputLabelsConfig> ById,
        IReadOnlyDictionary<string, InputLabelsConfig> ByName,
        IReadOnlyDictionary<string, InputLabelsConfig> ByNormalizedName);
}
