using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.Infrastructure;

/// <summary>
/// Minimal filesystem abstraction — just the operations Core actually uses, so the production
/// build has no third-party dependency to distribute. Injected everywhere instead of touching
/// <see cref="System.IO"/> directly, so tests can substitute an in-memory implementation.
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);
    Stream OpenRead(string path);
    string ReadAllText(string path);
    void AppendAllText(string path, string contents);
    void DeleteFile(string path);

    bool DirectoryExists(string path);
    void CreateDirectory(string path);

}

/// <summary>
/// Production <see cref="IFileSystem"/> backed by <see cref="System.IO"/>. Thin pass-through, so
/// it carries no logic worth covering.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class SystemFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public Stream OpenRead(string path) => File.OpenRead(path);
    public string ReadAllText(string path) => File.ReadAllText(path);
    public void AppendAllText(string path, string contents) => File.AppendAllText(path, contents);
    public void DeleteFile(string path) => File.Delete(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

}

/// <summary>
/// Decorates an <see cref="IFileSystem"/> so callers work in paths relative to
/// <paramref name="rootDir"/> — every path is joined onto the root before reaching
/// <paramref name="inner"/>. Lets composition wire up the plugin root once instead of threading
/// <c>rootDir</c> through every loader that would otherwise <c>Path.Combine</c> it in manually.
/// </summary>
public sealed class RootedFileSystem(string rootDir, IFileSystem inner) : IFileSystem
{
    /// <summary>Joins <paramref name="path"/> onto the root — for callers that need to hand an
    /// absolute path to something outside the <see cref="IFileSystem"/> seam (e.g. an image path
    /// rendered by the UI layer).</summary>
    public string FullPath(string path) => Path.Combine(rootDir, path);

    public bool FileExists(string path) => inner.FileExists(FullPath(path));
    public Stream OpenRead(string path) => inner.OpenRead(FullPath(path));
    public string ReadAllText(string path) => inner.ReadAllText(FullPath(path));
    public void AppendAllText(string path, string contents) => inner.AppendAllText(FullPath(path), contents);
    public void DeleteFile(string path) => inner.DeleteFile(FullPath(path));

    public bool DirectoryExists(string path) => inner.DirectoryExists(FullPath(path));
    public void CreateDirectory(string path) => inner.CreateDirectory(FullPath(path));
}
