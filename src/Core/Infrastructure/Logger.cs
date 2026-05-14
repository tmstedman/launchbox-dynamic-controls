namespace DynamicControls.Infrastructure;

public interface ILogger
{
    bool IsDebugEnabled { get; set; }
    void Debug(string message);
    void Info(string message);
    void Error(string message);
    void ClearLog();
}

/// <summary>
/// Debug logger that writes to a log file in the plugin directory.
/// Logging is controlled by the IsDebugEnabled flag.
/// </summary>
public class Logger(IFileSystem fs, string rootDir, Func<DateTime> now) : ILogger
{
    private readonly string _logPath = Path.Combine(rootDir, "Logs", "debug.log");
    private readonly IFileSystem _fs = fs;
    private readonly Func<DateTime> _now = now;

    public bool IsDebugEnabled { get; set; }

    public Logger(IFileSystem fs, string rootDir)
        : this(fs, rootDir, () => DateTime.Now) { }

    /// <summary>
    /// Writes a message to the debug log. Only writes if Debug is true.
    /// </summary>
    public void Debug(string message)
    {
        if (!IsDebugEnabled) return;
        Write("DEBUG", message);
    }

    /// <summary>
    /// Always writes an informational message, regardless of the Debug flag.
    /// Use for messages that are useful even outside debug mode (e.g. startup state).
    /// </summary>
    public void Info(string message) => Write("INFO", message);

    /// <summary>
    /// Always writes an error message, regardless of the Debug flag.
    /// Use for unexpected conditions: missing required data, parse failures, exceptions.
    /// </summary>
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        string timestamp = _now().ToString("HH:mm:ss");
        string dir = Path.GetDirectoryName(_logPath)!;
        if (!_fs.DirectoryExists(dir))
            _fs.CreateDirectory(dir);
        _fs.AppendAllText(_logPath, $"[{timestamp}] [{level}] {message}\r\n");
    }

    /// <summary>
    /// Deletes the debug log file if it exists. No-op on a fresh install where the log
    /// directory hasn't been created yet.
    /// </summary>
    public void ClearLog()
    {
        if (_fs.FileExists(_logPath))
            _fs.DeleteFile(_logPath);
    }
}
