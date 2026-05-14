namespace DynamicControls.Infrastructure;

/// <summary>
/// Wraps an <see cref="IFileSystem"/> with two-tier path resolution: a <c>User\</c> layer that
/// shadows a <c>Defaults\</c> layer under the same root. Loaders call <see cref="Resolve"/> to
/// get the right path without knowing which layer wins; all other I/O delegates to the underlying
/// <see cref="IFileSystem"/> unchanged.
/// </summary>
public sealed class LayeredFileSystem(string rootDir, IFileSystem fs)
{
    /// <summary>The underlying <see cref="IFileSystem"/> — for components that need a plain
    /// filesystem reference rather than layered path resolution (e.g. template loading, loggers,
    /// RetroArch cfg reading from the emulator directory).</summary>
    public IFileSystem Fs { get; } = fs;

    public string RootDir { get; } = rootDir;
    public string DefaultsDir { get; } = Path.Combine(rootDir, "Defaults");
    public string UserDir { get; } = Path.Combine(rootDir, "User");

    /// <summary>
    /// Returns the <c>User\{segments}</c> path when that file exists; otherwise returns
    /// <c>Defaults\{segments}</c>. The returned path may not exist either — callers handle that
    /// as they do today (check <see cref="FileExists"/> before opening).
    /// </summary>
    public string Resolve(params string[] segments)
    {
        string user = Path.Combine([UserDir, .. segments]);
        return Fs.FileExists(user) ? user : Path.Combine([DefaultsDir, .. segments]);
    }

    public bool FileExists(string path) => Fs.FileExists(path);
    public Stream OpenRead(string path) => Fs.OpenRead(path);
}
