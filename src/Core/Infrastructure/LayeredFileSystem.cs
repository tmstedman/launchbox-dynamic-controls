namespace DynamicControls.Infrastructure;

/// <summary>
/// Wraps an <see cref="IFileSystem"/> with two-tier path resolution: a <c>User\</c> layer that
/// shadows a <c>Defaults\</c> layer under the same root. <see cref="Defaults"/>/<see cref="User"/>
/// are rooted one level into each layer, for loaders that need to check/read a specific layer
/// directly. <see cref="Resolve"/> picks the winning layer for loaders that just want "the" file
/// without caring which layer it came from; its root-relative result is read back via the plain
/// <see cref="FileExists"/>/<see cref="OpenRead"/> pair (rooted at the plugin root itself).
/// </summary>
public sealed class LayeredFileSystem(string rootDir, IFileSystem fs)
{
    private readonly IFileSystem _rooted = new RootedFileSystem(rootDir, fs);

    /// <summary>Rooted at <c>{rootDir}\Defaults</c> — paths passed here are relative to that layer.</summary>
    public RootedFileSystem Defaults { get; } = new(Path.Combine(rootDir, "Defaults"), fs);

    /// <summary>Rooted at <c>{rootDir}\User</c> — paths passed here are relative to that layer.</summary>
    public RootedFileSystem User { get; } = new(Path.Combine(rootDir, "User"), fs);

    /// <summary>
    /// Returns the <c>User\{segments}</c> path when that file exists; otherwise the
    /// <c>Defaults\{segments}</c> path when that exists; otherwise null. The returned path is
    /// root-relative — pass it to <see cref="FileExists"/>/<see cref="OpenRead"/>.
    /// </summary>
    public string? Resolve(params string[] segments)
    {
        string relative = Path.Combine(segments);
        if (User.FileExists(relative)) return Path.Combine("User", relative);
        if (Defaults.FileExists(relative)) return Path.Combine("Defaults", relative);
        return null;
    }

    public bool FileExists(string path) => _rooted.FileExists(path);
    public Stream OpenRead(string path) => _rooted.OpenRead(path);
}
