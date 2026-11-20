using System.Xml;
using System.Xml.Linq;
using DynamicControls.Tools.Common;

namespace DynamicControls.Preview;

/// <summary>
/// Game titles from a LaunchBox Games Database export, for the band above a preview. A game is
/// found by the <c>launchBoxId</c> its <c>Labels.xml</c> entry carries, which names its
/// <c>&lt;DatabaseID&gt;</c> in <c>Metadata.xml</c>; an Arcade ROM set with no id is found by its
/// file name in <c>Mame.xml</c>. A game neither names has no title here, and the caller falls
/// back to its ROM name.
/// </summary>
/// <remarks>
/// <c>Metadata.xml</c> runs to hundreds of megabytes, so it is streamed once for the ids the run
/// needs and nothing else is kept.
/// </remarks>
public sealed class LaunchBoxTitles
{
    private readonly Dictionary<(string Platform, string RomName), int> _ids;
    private readonly Dictionary<int, string> _names;
    private readonly Lazy<IReadOnlyDictionary<string, string>> _mame;

    private LaunchBoxTitles(
        Dictionary<(string, string), int> ids,
        Dictionary<int, string> names,
        Lazy<IReadOnlyDictionary<string, string>> mame)
    {
        _ids = ids;
        _names = names;
        _mame = mame;
    }

    /// <summary>
    /// Reads the ids of every game the <paramref name="platforms"/>' <c>Labels.xml</c> files name,
    /// then their titles out of <paramref name="exportDir"/>'s <c>Metadata.xml</c>. A User entry
    /// replaces the shipped one for the same ROM name, as it does when the pipeline resolves it.
    /// </summary>
    public static LaunchBoxTitles Load(
        string exportDir, string root, IEnumerable<string> platforms, Action<string> onError)
    {
        Dictionary<(string, string), int> ids = [];
        foreach (string platform in platforms)
        {
            // Shipped entries first so a User entry for the same ROM name overwrites them.
            foreach (string layer in new[] { "Defaults", "User" })
            {
                ReadIds(Path.Combine(root, layer, "Platforms", platform, "Labels.xml"), platform, ids, onError);
            }
        }

        string metadata = Path.Combine(exportDir, "Metadata.xml");
        Dictionary<int, string> names = ReadNames(metadata, [.. ids.Values], onError);

        string mamePath = Path.Combine(exportDir, "Mame.xml");
        return new LaunchBoxTitles(ids, names, new Lazy<IReadOnlyDictionary<string, string>>(
            () => ReadMameTitles(mamePath, onError)));
    }

    /// <summary>The title of <paramref name="target"/>'s game, or null when the export has none.</summary>
    public string? TitleOf(SweepTarget target)
    {
        if (target.IsPlatformDefault) return null;

        if (_ids.TryGetValue((target.Platform, target.RomName), out int id) && _names.TryGetValue(id, out string? name))
            return name;

        return target.Platform == "Arcade" && _mame.Value.TryGetValue(target.RomName, out string? title) ? title : null;
    }

    private static void ReadIds(string path, string platform, Dictionary<(string, string), int> ids, Action<string> onError)
    {
        if (!File.Exists(path)) return;

        try
        {
            foreach (XElement game in XDocument.Load(path).Root?.Elements("Game") ?? [])
            {
                if ((string?)game.Attribute("romName") is { Length: > 0 } romName
                    && int.TryParse((string?)game.Attribute("launchBoxId"), out int id))
                {
                    ids[(platform, romName)] = id;
                }
            }
        }
        catch (Exception ex)
        {
            onError($"Could not read {path}: {ex.Message}");
        }
    }

    /// <summary>
    /// Streams <c>Metadata.xml</c> for the <c>&lt;Name&gt;</c> of each wanted <c>&lt;DatabaseID&gt;</c>,
    /// stopping once every one is found. Each game's long <c>&lt;Overview&gt;</c> and the rest are
    /// skipped unread.
    /// </summary>
    private static Dictionary<int, string> ReadNames(string path, HashSet<int> wanted, Action<string> onError)
    {
        Dictionary<int, string> names = [];
        if (wanted.Count == 0) return names;

        try
        {
            using var reader = XmlReader.Create(path, ReaderSettings);
            while (names.Count < wanted.Count && reader.ReadToFollowing("Game"))
            {
                string? name = null;
                int id = 0;
                using (XmlReader game = reader.ReadSubtree())
                {
                    game.Read();
                    game.Read();
                    while (!game.EOF)
                    {
                        if (game.NodeType != XmlNodeType.Element) game.Read();
                        else if (game.Name == "Name") name = game.ReadElementContentAsString();
                        else if (game.Name == "DatabaseID") int.TryParse(game.ReadElementContentAsString(), out id);
                        else game.Skip();
                    }
                }

                if (name != null && wanted.Contains(id)) names[id] = name;
            }
        }
        catch (Exception ex)
        {
            onError($"Could not read {path}: {ex.Message}");
        }
        return names;
    }

    /// <summary>
    /// <c>Mame.xml</c>'s ROM set file name to title, for Arcade games whose entry carries no id.
    /// Read only when such a game is asked for.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ReadMameTitles(string path, Action<string> onError)
    {
        Dictionary<string, string> titles = new(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return titles;

        try
        {
            using var reader = XmlReader.Create(path, ReaderSettings);
            while (reader.ReadToFollowing("MameFile"))
            {
                string? fileName = null, name = null;
                using (XmlReader file = reader.ReadSubtree())
                {
                    file.Read();
                    file.Read();
                    while (!file.EOF)
                    {
                        if (file.NodeType != XmlNodeType.Element) file.Read();
                        else if (file.Name == "FileName") fileName = file.ReadElementContentAsString();
                        else if (file.Name == "Name") name = file.ReadElementContentAsString();
                        else file.Skip();
                    }
                }

                if (!string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(name)) titles.TryAdd(fileName, name);
            }
        }
        catch (Exception ex)
        {
            onError($"Could not read {path}: {ex.Message}");
        }
        return titles;
    }

    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Ignore,
        IgnoreWhitespace = true,
    };
}
