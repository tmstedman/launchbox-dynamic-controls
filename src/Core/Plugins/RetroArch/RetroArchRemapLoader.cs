namespace DynamicControls.Plugins.RetroArch;

/// <summary>
/// Loads RetroArch remap files (.rmp) for each level of the remap cascade independently:
///
///   Global:     config/remaps/common.rmp
///   Core:       config/remaps/&lt;core&gt;/&lt;core&gt;.rmp
///   ContentDir: config/remaps/&lt;core&gt;/&lt;romDir&gt;.rmp
///   Game:       config/remaps/&lt;core&gt;/&lt;rom&gt;.rmp
///
/// Each level is populated only if its file exists; levels don't influence each other. Callers
/// use <see cref="RetroArchGameData.Game"/> for swap detection (game-level only, per the trust
/// boundary) and <see cref="RetroArchGameData.Merged"/> for variant selection (cascade walk).
/// Returns null when no file was found at any level.
/// </summary>
public class RetroArchRemapLoader(IRetroArchConfigFileReader configReader, IFileSystem fs) : IRetroArchGameLoader
{
    private readonly IRetroArchConfigFileReader _configReader = configReader;
    private readonly IFileSystem _fs = fs;

    public RetroArchGameData? Load(string retroArchDir, string? core, GameInfo game)
    {
        if (core == null) return null;

        string remapsDir = Path.Combine(retroArchDir, "config", "remaps");
        if (!_fs.DirectoryExists(remapsDir)) return null;

        string coreDir = Path.Combine(remapsDir, core);
        bool coreDirExists = _fs.DirectoryExists(coreDir);

        Dictionary<string, string>? gameLevel = coreDirExists
            ? _configReader.LoadConfigFile(Path.Combine(coreDir, game.RomName + ".rmp"))
            : null;

        Dictionary<string, string>? contentDir = coreDirExists && game.RomDirectory != null
            ? _configReader.LoadConfigFile(Path.Combine(coreDir, Path.GetFileName(game.RomDirectory) + ".rmp"))
            : null;

        Dictionary<string, string>? coreLevel = coreDirExists
            ? _configReader.LoadConfigFile(Path.Combine(coreDir, core + ".rmp"))
            : null;

        Dictionary<string, string>? global = _configReader.LoadConfigFile(Path.Combine(remapsDir, "common.rmp"));

        return gameLevel == null && contentDir == null && coreLevel == null && global == null
            ? null
            : new RetroArchGameData(global, coreLevel, contentDir, gameLevel);
    }
}
