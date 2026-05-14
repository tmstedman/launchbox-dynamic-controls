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
