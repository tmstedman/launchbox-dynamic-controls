namespace DynamicControls.Plugins.RetroArch;

public interface IRetroArchCoreInfo
{
    string? ReadDisplayName(string retroArchDir, string coreDllName);
}

/// <summary>
/// Reads metadata from a RetroArch core's .info file. Info files live at
/// &lt;retroArchDir&gt;/info/&lt;coreDllName&gt;.info or &lt;retroArchDir&gt;/cores/&lt;coreDllName&gt;.info.
/// </summary>
public class RetroArchCoreInfo(IFileSystem fs, ILogger logger) : IRetroArchCoreInfo
{
    private readonly IFileSystem _fs = fs;
    private readonly ILogger _logger = logger;

    /// <summary>
    /// Returns the core display name (e.g. "Genesis Plus GX") for a given dll name
    /// (e.g. "genesis_plus_gx_libretro"). RetroArch uses the display name for both the
    /// config/ subfolder and the config/remaps/ subfolder — not the dll name.
    /// Returns null if the info file is absent or contains no corename entry.
    /// </summary>
    public string? ReadDisplayName(string retroArchDir, string coreDllName)
    {
        foreach (string subDir in new[] { "info", "cores" })
        {
            string infoPath = Path.Combine(retroArchDir, subDir, coreDllName + ".info");
            if (!_fs.FileExists(infoPath)) continue;
            _logger.Debug($"RetroArch core info found: {infoPath}");
            foreach (string line in _fs.ReadAllText(infoPath).Split('\n'))
            {
                string trimmed = line.Trim();
                if (!trimmed.StartsWith("corename")) continue;
                int eq = trimmed.IndexOf('=');
                if (eq >= 0) return trimmed[(eq + 1)..].Trim().Trim('"');
            }
        }
        return null;
    }
}
