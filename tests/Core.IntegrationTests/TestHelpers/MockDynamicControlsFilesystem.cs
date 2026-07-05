using System.IO.Abstractions.TestingHelpers;
using System.Text;
using System.Xml;

namespace DynamicControls.Core.IntegrationTests.TestHelpers;

/// <summary>
/// Wraps <see cref="MockFileSystem"/> with intent-named helpers that map to DC's fixture layout,
/// eliminating path-construction boilerplate and <see cref="MockFileData"/> noise from subsystem
/// test classes. <see cref="Fs"/> exposes the in-memory filesystem as the production
/// <see cref="IFileSystem"/> (via <see cref="MockFsAdapter"/>) for passing to factory methods.
///
/// Label helpers accumulate entries in memory and re-write the combined per-platform
/// <c>Labels/{platform}.xml</c> file each time an entry is added, so tests can call
/// <see cref="WriteGameLabels"/> and <see cref="WriteDefaultLabels"/> independently in any order.
/// </summary>
internal sealed class MockDynamicControlsFilesystem(string root)
{
    private readonly MockFileSystem _mock = new();

    // Accumulated label state keyed by (layer, platform).
    private readonly Dictionary<(string Layer, string Platform), PlatformLabelAccumulator> _labels = [];

    /// <summary>The in-memory filesystem as the production <see cref="IFileSystem"/>.</summary>
    public IFileSystem Fs => new MockFsAdapter(_mock);

    /// <summary>A <see cref="LayeredFileSystem"/> wrapping <see cref="Fs"/> at the given root —
    /// pass to factories and loaders that require layered path resolution.</summary>
    public LayeredFileSystem Lfs => new(root, Fs);

    public void WriteFile(string path, string content) =>
        _mock.AddFile(Path.Combine(root, path), new MockFileData(content));

    // ---- Defaults layer (shipped config) ----

    public void WritePlatform(string platform, string xml) =>
        WriteFile(Path.Combine("Defaults", "Controllers", platform + ".xml"), xml);

    public void WriteGameMapping(string platform, string romName, string xml) =>
        WriteFile(Path.Combine("Defaults", "InputMappings", platform, romName + ".xml"), xml);

    /// <summary>Sets the <c>&lt;Defaults&gt;</c> block for the platform. <paramref name="xml"/>
    /// is the inner XML of a root element (e.g. <c>&lt;InputLabels&gt;...&lt;/InputLabels&gt;</c>);
    /// the root wrapper is stripped and only child elements are kept.</summary>
    public void WriteDefaultLabels(string platform, string xml) =>
        AddLabels("Defaults", platform, defaultsInnerXml: InnerXml(xml));

    /// <summary>Adds a game entry to the platform labels file. <paramref name="xml"/> is the
    /// inner XML of a root element; the root wrapper is stripped. Pass <paramref name="launchBoxId"/>
    /// to also emit an <c>id="..."</c> attribute so the entry can be found by database ID.</summary>
    public void WriteGameLabels(string platform, string romName, string xml, string? launchBoxId = null) =>
        AddLabels("Defaults", platform, gameName: romName, gameId: launchBoxId, gameInnerXml: InnerXml(xml));

    public void WriteControlsXml(string xml) =>
        WriteFile("controls.xml", xml);

    public void WriteMameMapping(string xml) =>
        WriteFile(Path.Combine("Defaults", "Emulators", "MAME", "JoycodeMapping.xml"), xml);

    public void WriteRetroArchCore(string coreDisplayName, string xml) =>
        WriteFile(Path.Combine("Defaults", "Emulators", "RetroArch", coreDisplayName + ".xml"), xml);

    // ---- User layer (user overrides) ----

    public void WriteUserGlobalConfig(string xml) =>
        WriteFile(Path.Combine("User", "GlobalConfig.xml"), xml);

    public void WriteUserPlatform(string platform, string xml) =>
        WriteFile(Path.Combine("User", "Controllers", platform + ".xml"), xml);

    public void WriteUserGameMapping(string platform, string romName, string xml) =>
        WriteFile(Path.Combine("User", "InputMappings", platform, romName + ".xml"), xml);

    /// <summary>Sets the <c>&lt;Defaults&gt;</c> block in the User-layer platform labels file.</summary>
    public void WriteUserDefaultLabels(string platform, string xml) =>
        AddLabels("User", platform, defaultsInnerXml: InnerXml(xml));

    /// <summary>Adds a game entry to the User-layer platform labels file.</summary>
    public void WriteUserGameLabels(string platform, string romName, string xml) =>
        AddLabels("User", platform, gameName: romName, gameInnerXml: InnerXml(xml));

    public void WriteUserRetroArchCore(string coreDisplayName, string xml) =>
        WriteFile(Path.Combine("User", "Emulators", "RetroArch", coreDisplayName + ".xml"), xml);

    public void WriteUserMameMapping(string xml) =>
        WriteFile(Path.Combine("User", "Emulators", "MAME", "JoycodeMapping.xml"), xml);

    // ---- Templates (shipped, not layered) ----

    public void WriteLayout(string templateName, string xml) =>
        WriteFile(Path.Combine("Templates", templateName, "Layout.xml"), xml);

    // ---- Emulators (not layered) ----

    /// <summary>Writes a MAME cfg file under the <c>Emulators/mame/cfg/</c> directory that the
    /// MAME transform derives from a <c>mame*.exe</c> emulator path in that folder. Pass
    /// <c>"{rom}.cfg"</c> or <c>"default.cfg"</c>.</summary>
    public void WriteMameCfg(string fileName, string xml) =>
        WriteFile(Path.Combine("Emulators", "mame", "cfg", fileName), xml);

    // ---- Label accumulation ----

    private void AddLabels(string layer, string platform,
        string? defaultsInnerXml = null,
        string? gameName = null,
        string? gameId = null,
        string? gameInnerXml = null)
    {
        var key = (layer, platform);
        if (!_labels.TryGetValue(key, out PlatformLabelAccumulator? acc))
            _labels[key] = acc = new PlatformLabelAccumulator();

        if (defaultsInnerXml != null)
            acc.DefaultsInnerXml = defaultsInnerXml;
        if (gameName != null && gameInnerXml != null)
            acc.Games[gameName] = (gameId, gameInnerXml);

        string path = Path.Combine(layer, "Labels", platform + ".xml");
        WriteFile(path, acc.ToXml());
    }

    private static string InnerXml(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        return doc.DocumentElement!.InnerXml;
    }

    private sealed class PlatformLabelAccumulator
    {
        public string? DefaultsInnerXml { get; set; }
        public Dictionary<string, (string? Id, string InnerXml)> Games { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string ToXml()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<Labels>");
            if (DefaultsInnerXml != null)
            {
                sb.AppendLine("  <Defaults>");
                sb.AppendLine($"    {DefaultsInnerXml}");
                sb.AppendLine("  </Defaults>");
            }
            foreach (KeyValuePair<string, (string? Id, string InnerXml)> game in Games)
            {
                string safeName = game.Key.Replace("\"", "&quot;");
                string idAttr = game.Value.Id != null ? $" launchBoxId=\"{game.Value.Id}\"" : "";
                sb.AppendLine($"  <Game{idAttr} romName=\"{safeName}\">");
                sb.AppendLine($"    {game.Value.InnerXml}");
                sb.AppendLine("  </Game>");
            }
            sb.AppendLine("</Labels>");
            return sb.ToString();
        }
    }
}
