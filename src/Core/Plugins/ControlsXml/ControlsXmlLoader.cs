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

        const string path = "controls.xml";
        _logger.Debug($"controls.xml path: {path}, Exists: {_lfs.FileExists(path)}");

        if (!_lfs.FileExists(path))
        {
            _logger.Debug("controls.xml not found, skipping");
            return null;
        }

        var allLabels = new Dictionary<string, InputLabelsConfig>();
        using Stream stream = _lfs.OpenRead(path);
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

                    if (name != null && value != null && !string.IsNullOrEmpty(name))
                    {
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

        return PairHalves(result);
    }

    /// <summary>
    /// Rewrites controls.xml's own name for the two halves of an analogue port onto the platform
    /// button vocabulary: <c>FOO</c> and <c>FOO_EXT</c> become <c>FOO.dec</c> and <c>FOO.inc</c>.
    ///
    /// <para>The <c>_EXT</c> spelling is this database's invention — MAME has no such port, and a
    /// cfg never names one — so unlike the cfg's own port types it has to be translated on the way
    /// in rather than read verbatim. A game naming it is the database stating that this port has
    /// two ends, which is the only place that fact appears, so the pairing is read from the data
    /// and no list of which ports are analogue is kept here.</para>
    ///
    /// <para>A bare name with no <c>_EXT</c> beside it is left exactly as it is: a pedal travels
    /// one way only and is one button, so renaming it would strand it.</para>
    /// </summary>
    private static InputLabelsConfig PairHalves(InputLabelsConfig labels)
    {
        var paired = new HashSet<string>(
            labels.Labels
                .Select(entry => entry.Name)
                .Where(name => name.EndsWith(ExtSuffix))
                .Select(name => name[..^ExtSuffix.Length]));

        if (paired.Count == 0) return labels;

        foreach (LabelEntry entry in labels.Labels)
        {
            if (entry.Name.EndsWith(ExtSuffix) && paired.Contains(entry.Name[..^ExtSuffix.Length]))
                entry.Name = entry.Name[..^ExtSuffix.Length] + ".inc";
            else if (paired.Contains(entry.Name))
                entry.Name += ".dec";
        }

        return labels;
    }

    private const string ExtSuffix = "_EXT";
}
