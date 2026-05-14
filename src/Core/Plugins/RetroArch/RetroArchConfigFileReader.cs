namespace DynamicControls.Plugins.RetroArch;

public interface IRetroArchConfigFileReader
{
    /// <summary>
    /// Reads and parses a RetroArch key=value config file. Returns null if the file does not
    /// exist or cannot be read.
    /// </summary>
    Dictionary<string, string>? LoadConfigFile(string path);
}

/// <summary>
/// Shared file-level reader for RetroArch's key=value config format, used by both .cfg and .rmp files.
/// </summary>
public class RetroArchConfigFileReader(ILogger logger, IFileSystem fs) : IRetroArchConfigFileReader
{
    private readonly ILogger _logger = logger;
    private readonly IFileSystem _fs = fs;

    /// <inheritdoc />
    public Dictionary<string, string>? LoadConfigFile(string path)
    {
        if (!_fs.FileExists(path)) return null;

        string content;
        try { content = _fs.ReadAllText(path); }
        catch (Exception ex)
        {
            _logger.Error($"RetroArch config read failed for '{path}': {ex.Message}");
            return null;
        }

        _logger.Debug($"RetroArch config file loaded: {path}");
        var result = new Dictionary<string, string>();
        foreach (string line in content.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("#") || !trimmed.Contains('=')) continue;

            int eqIdx = trimmed.IndexOf('=');
            string key = trimmed[..eqIdx].Trim();
            string value = trimmed[(eqIdx + 1)..].Trim().Trim('"');
            if (!string.IsNullOrEmpty(key))
                result[key] = value;
        }
        return result;
    }
}
