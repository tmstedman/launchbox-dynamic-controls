using NSubstitute;

namespace DynamicControls.Core.Tests.Infrastructure;

public class LayeredFileSystemTests
{
    private const string RootDir = @"C:\plugin";
    private readonly IFileSystem _fs = TestFs.Create();

    private LayeredFileSystem Lfs => new(RootDir, _fs);

    [Fact]
    public void Resolve_UserFileExists_ReturnsUserPath()
    {
        string userPath = Path.Combine(RootDir, "User", "Controllers", "Sega Genesis.xml");
        _fs.FileExists(userPath).Returns(true);

        Lfs.Resolve("Controllers", "Sega Genesis.xml").ShouldBe(userPath);
    }

    [Fact]
    public void Resolve_UserFileAbsent_ReturnsDefaultsPath()
    {
        string userPath = Path.Combine(RootDir, "User", "Controllers", "Sega Genesis.xml");
        string defaultsPath = Path.Combine(RootDir, "Defaults", "Controllers", "Sega Genesis.xml");
        _fs.FileExists(userPath).Returns(false);

        Lfs.Resolve("Controllers", "Sega Genesis.xml").ShouldBe(defaultsPath);
    }
}
