using System.Xml;
using DynamicControls.Labels;

namespace DynamicControls.Plugins.ControlsXml;

/// <summary>
/// Returns the labels for a given ROM from the cached controls.xml. Returns null when the file
/// is missing or the ROM has no entry. Introduced primarily to let consumers (and tests)
/// substitute the loader cleanly.
/// </summary>
public interface IControlsXmlLoader
{
    /// <summary>
    /// Returns the labels for <paramref name="romName"/>, or null if the controls.xml file is
    /// missing or the ROM has no entry.
    /// </summary>
    InputLabelsConfig? Lookup(string romName);
}

/// <summary>
/// Loads and caches game labels from the BYOAC-format controls.xml file shipped under Data/.
/// The whole file is parsed once on first lookup; subsequent lookups hit the cache. Returns null
/// when the file does not exist or the ROM has no entry. Knows nothing about MAME — emulator
/// gating is the caller's responsibility.
/// </summary>
public class ControlsXmlLoader(ILogger logger, LayeredFileSystem lfs) : IControlsXmlLoader
{
    private readonly ILogger _logger = logger;
    private readonly LayeredFileSystem _lfs = lfs;
    private Dictionary<string, InputLabelsConfig>? _allLabels;
    private bool _loaded;

    /// <summary>
    /// Returns the labels for <paramref name="romName"/>, or null if the file is missing or the
    /// ROM has no entry. Triggers a one-time parse of the full controls.xml on first call.
    /// </summary>
    public InputLabelsConfig? Lookup(string romName)
    {
        Dictionary<string, InputLabelsConfig>? all = LoadAll();
        if (all == null) return null;

        if (all.TryGetValue(romName, out InputLabelsConfig? result))
        {
            _logger.Debug($"controls.xml labels found for: {romName}, entries: {result.Labels.Count}");
            return result;
        }

        _logger.Debug($"controls.xml labels not found for: {romName}");
        return null;
    }

    private Dictionary<string, InputLabelsConfig>? LoadAll()
    {
        if (_loaded) return _allLabels;
        _loaded = true;

        string path = Path.Combine(_lfs.RootDir, "controls.xml");
        _logger.Debug($"controls.xml path: {path}, Exists: {_lfs.Fs.FileExists(path)}");

        if (!_lfs.Fs.FileExists(path))
        {
            _logger.Debug("controls.xml not found, skipping");
            return null;
        }

        var allLabels = new Dictionary<string, InputLabelsConfig>();
        using Stream stream = _lfs.Fs.OpenRead(path);
        var doc = new XmlDocument();
        doc.Load(stream);
        XmlElement root = doc.DocumentElement!;

        foreach (XmlElement gameNode in root.ChildNodes.OfType<XmlElement>())
        {
            if (gameNode.Name != "game") continue;

            string? romName = gameNode.Attributes["romname"]?.Value;
            if (romName == null) continue;

            InputLabelsConfig labels = ParseGame(gameNode);
            if (labels.Labels.Any())
                allLabels[romName] = labels;
        }

        _logger.Debug($"controls.xml loaded: {allLabels.Count} games");
        return _allLabels = allLabels;
    }

    /// <summary>
    /// Parses a single game node from controls.xml, extracting player 1 button labels.
    /// </summary>
    private static InputLabelsConfig ParseGame(XmlElement gameNode)
    {
        var result = new InputLabelsConfig();

        foreach (XmlElement playerNode in gameNode.ChildNodes.OfType<XmlElement>())
        {
            if (playerNode.Name != "player") continue;

            string? playerNum = playerNode.Attributes["number"]?.Value;
            if (playerNum != "1") continue;

            foreach (XmlElement child in playerNode.ChildNodes.OfType<XmlElement>())
            {
                if (child.Name != "labels") continue;

                foreach (XmlElement labelNode in child.ChildNodes.OfType<XmlElement>())
                {
                    if (labelNode.Name != "label") continue;

                    string? name = labelNode.Attributes["name"]?.Value;
                    string? value = labelNode.Attributes["value"]?.Value;

                    if (name != null && value != null)
                    {
                        if (name.StartsWith("P1_"))
                            name = name[3..];

                        if (string.IsNullOrEmpty(name)) continue;

                        result.Labels.Add(new LabelEntry
                        {
                            Name = name,
                            Label = value,
                        });
                    }
                }
            }

            break;
        }

        return result;
    }
}
