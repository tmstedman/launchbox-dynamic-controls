namespace DynamicControls.Plugins.RetroArch;

/// <summary>
/// Reads the RetroArch cfg cascade for a given emulator installation:
///   retroarch.cfg (input_joypad_driver only) → config/&lt;core&gt;/&lt;core&gt;.cfg →
///   config/&lt;core&gt;/&lt;contentDirectory&gt;.cfg → config/&lt;core&gt;/&lt;rom&gt;.cfg
/// where &lt;core&gt; is the display name from the core's .info file (e.g. "Genesis Plus GX"),
/// not the DLL name, and &lt;contentDirectory&gt; is the name of the folder containing the ROM.
/// Only <c>input_joypad_driver</c> is taken from <c>retroarch.cfg</c> — its other entries are
/// autoconfig-derived physical-button assignments that are uninterpretable without the autoconfig
/// itself. Returns null if no file was found at any level.
/// </summary>
public class RetroArchCfgLoader(
    IRetroArchConfigFileReader configReader,
    IFileSystem fs,
    IApplicationData applicationData) : IRetroArchGameLoader
{
    private readonly IRetroArchConfigFileReader _configReader = configReader;
    private readonly IFileSystem _fs = fs;
    private readonly IApplicationData _applicationData = applicationData;

    public RetroArchGameData? Load(string retroArchDir, string? core, GameInfo game)
    {
        if (core == null) return null;

        string configRoot = GetConfigRoot(retroArchDir);

        Dictionary<string, string>? global = LoadGlobal(configRoot);

        string coreDir = Path.Combine(configRoot, "config", core);
        Dictionary<string, string>? coreLevel = _configReader.LoadConfigFile(Path.Combine(coreDir, $"{core}.cfg"));
        Dictionary<string, string>? contentDir = game.RomDirectory != null
            ? _configReader.LoadConfigFile(Path.Combine(coreDir, $"{Path.GetFileName(game.RomDirectory)}.cfg"))
            : null;
        Dictionary<string, string>? gameLevel = _configReader.LoadConfigFile(Path.Combine(coreDir, $"{game.RomName}.cfg"));

        return global == null && coreLevel == null && contentDir == null && gameLevel == null
            ? null
            : new RetroArchGameData(global, coreLevel, contentDir, gameLevel);
    }

    private Dictionary<string, string>? LoadGlobal(string configRoot)
    {
        Dictionary<string, string>? retroarchCfg = _configReader.LoadConfigFile(Path.Combine(configRoot, "retroarch.cfg"));
        if (retroarchCfg == null) return null;
        return retroarchCfg.TryGetValue("input_joypad_driver", out string? driver)
            ? new Dictionary<string, string> { ["input_joypad_driver"] = driver }
            : null;
    }

    private string GetConfigRoot(string retroArchDir)
    {
        if (_fs.FileExists(Path.Combine(retroArchDir, "retroarch.cfg")))
            return retroArchDir;

        return Path.Combine(_applicationData.Path, "RetroArch");
    }
}
