using System.Xml;
using DynamicControls.Config;

namespace DynamicControls.Labels;

/// <summary>
/// Production implementation: loads labels from
/// <c>Data/Labels/{platform}/{romName | _DefaultLabels}.xml</c>. Filesystem and XML parsing
/// run lazily on each call (no caching).
/// </summary>
public class InputLabelsLoader(ILogger logger, LayeredFileSystem lfs) : IInputLabelsLoader
{
    private readonly ILogger _logger = logger;
    private readonly LayeredFileSystem _lfs = lfs;

    /// <inheritdoc />
    public bool IsEnabled(GlobalConfig config) => true;

    /// <inheritdoc />
    public InputLabelsConfig? Load(GameInfo game) =>
        LoadLabelsFile(game.Platform, game.RomName, "Game labels");

    /// <inheritdoc />
    public InputLabelsConfig? LoadDefaultLabels(string platform) =>
        LoadLabelsFile(platform, "_DefaultLabels", "Default labels");

    private InputLabelsConfig? LoadLabelsFile(string platform, string fileName, string description)
    {
        string path = _lfs.Resolve("Labels", platform.SafeFileName(), $"{fileName}.xml");
        _logger.Debug($"{description} path: {path}, Exists: {_lfs.FileExists(path)}");
        return LoadLabels(path);
    }

    /// <summary>
    /// Parses a label XML file into an InputLabelsConfig.
    /// Returns null if the file does not exist.
    /// </summary>
    private InputLabelsConfig? LoadLabels(string path)
    {
        if (!_lfs.FileExists(path)) return null;

        var result = new InputLabelsConfig();
        using Stream stream = _lfs.OpenRead(path);
        var doc = new XmlDocument();
        doc.Load(stream);
        XmlElement root = doc.DocumentElement!;

        foreach (XmlElement node in root.ChildNodes.OfType<XmlElement>())
        {
            string label = node.InnerText;
            if (string.IsNullOrEmpty(label))
            {
                _logger.Error($"Skipping <{node.Name}> in {path}: element has no text value");
                continue;
            }

            result.Labels.Add(new LabelEntry
            {
                Name = node.Name,
                Label = label,
            });
        }

        _logger.Debug($"Label entries: {result.Labels.Count}");
        return result;
    }
}
