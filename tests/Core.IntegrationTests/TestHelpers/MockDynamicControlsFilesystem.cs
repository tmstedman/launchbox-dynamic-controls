using System.IO.Abstractions.TestingHelpers;

namespace DynamicControls.Core.IntegrationTests.TestHelpers;

/// <summary>
/// Wraps <see cref="MockFileSystem"/> with intent-named helpers that map to DC's fixture layout,
/// eliminating path-construction boilerplate and <see cref="MockFileData"/> noise from subsystem
/// test classes. <see cref="Fs"/> exposes the in-memory filesystem as the production
/// <see cref="IFileSystem"/> (via <see cref="MockFsAdapter"/>) for passing to factory methods.
/// </summary>
internal sealed class MockDynamicControlsFilesystem(string root)
{
    private readonly MockFileSystem _mock = new();

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

    public void WriteDefaultLabels(string platform, string xml) =>
        WriteFile(Path.Combine("Defaults", "Labels", platform, "_DefaultLabels.xml"), xml);

    public void WriteGameLabels(string platform, string romName, string xml) =>
        WriteFile(Path.Combine("Defaults", "Labels", platform, romName + ".xml"), xml);

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

    public void WriteUserGameLabels(string platform, string romName, string xml) =>
        WriteFile(Path.Combine("User", "Labels", platform, romName + ".xml"), xml);

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
}
