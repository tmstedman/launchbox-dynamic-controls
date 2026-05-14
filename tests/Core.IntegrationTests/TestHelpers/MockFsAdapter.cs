namespace DynamicControls.Core.IntegrationTests.TestHelpers;

/// <summary>
/// Adapts an in-memory <c>System.IO.Abstractions</c> filesystem (e.g. <c>MockFileSystem</c>) to the
/// production <see cref="IFileSystem"/>, so subsystem tests can keep driving factories with a real
/// in-memory filesystem even though Core no longer depends on System.IO.Abstractions. Test-only —
/// the System.IO.Abstractions packages live in the test assembly and are never distributed.
/// </summary>
internal sealed class MockFsAdapter(System.IO.Abstractions.IFileSystem inner) : IFileSystem
{
    public bool FileExists(string path) => inner.File.Exists(path);
    public Stream OpenRead(string path) => inner.File.OpenRead(path);
    public string ReadAllText(string path) => inner.File.ReadAllText(path);
    public void AppendAllText(string path, string contents) => inner.File.AppendAllText(path, contents);
    public void DeleteFile(string path) => inner.File.Delete(path);

    public bool DirectoryExists(string path) => inner.Directory.Exists(path);
    public void CreateDirectory(string path) => inner.Directory.CreateDirectory(path);

    public string Combine(params string[] paths) => inner.Path.Combine(paths);
    public string GetFileName(string path) => inner.Path.GetFileName(path);
    public string? GetDirectoryName(string path) => inner.Path.GetDirectoryName(path);
}
