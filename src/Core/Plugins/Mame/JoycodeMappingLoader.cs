using System.Xml;

namespace DynamicControls.Plugins.Mame;

/// <summary>
/// Loads the JOYCODE-to-generic-input mapping from JoycodeMapping.xml. The parsed result is
/// cached on first call; subsequent calls return the same instance without re-reading the file.
/// Multiple &lt;Mapping&gt; entries for the same joycode accumulate rather than overwrite — a
/// bare analogue axis has no sign, so it is expected to carry both halves.
/// </summary>
public class JoycodeMappingLoader(
    ILogger logger,
    LayeredFileSystem lfs) : IJoycodeMappingLoader
{
    private readonly ILogger _logger = logger;
    private readonly LayeredFileSystem _lfs = lfs;
    private JoycodeMapping? _cached;

    /// <summary>
    /// Returns the joycode mapping, parsing JoycodeMapping.xml on first call and caching the
    /// result. Returns an empty mapping if the file does not exist.
    /// </summary>
    public JoycodeMapping Load() => _cached ??= LoadFromFile();

    private JoycodeMapping LoadFromFile()
    {
        var map = new Dictionary<string, List<JoycodeTarget>>();

        string? path = _lfs.Resolve("Emulators", "MAME", "JoycodeMapping.xml");
        _logger.Debug($"Joycode mapping path: {path}");

        if (path == null)
        {
            _logger.Error("JoycodeMapping.xml not found, MAME joycode translation disabled");
            return new JoycodeMapping(map);
        }

        using Stream stream = _lfs.OpenRead(path);
        var doc = new XmlDocument();
        doc.Load(stream);
        XmlElement root = doc.DocumentElement!;

        foreach (XmlElement node in root.ChildNodes.OfType<XmlElement>())
        {
            if (node.Name != "Mapping") continue;

            string? joycode = node.Attributes["joycode"]?.Value;
            string? input = node.Attributes["input"]?.Value;

            if (joycode == null || input == null)
            {
                _logger.Error("Skipping <Mapping> in JoycodeMapping.xml: missing 'joycode' or 'input' attribute");
                continue;
            }

            // Optional, and only meaningful on the two halves of a bare analogue axis: which end
            // of the axis this input lies on, in MAME's own increment/decrement terms. Absent on
            // every ordinary button, which has no direction to state.
            string? half = node.Attributes["half"]?.Value;
            if (half is not null and not "dec" and not "inc")
            {
                _logger.Error($"Ignoring half=\"{half}\" on {joycode} in JoycodeMapping.xml: expected \"dec\" or \"inc\"");
                half = null;
            }

            if (!map.TryGetValue(joycode, out List<JoycodeTarget>? targets))
            {
                targets = [];
                map[joycode] = targets;
            }
            targets.Add(new JoycodeTarget(input, half));
        }

        _logger.Debug($"Joycode mapping entries loaded: {map.Count}");
        return new JoycodeMapping(map);
    }
}
