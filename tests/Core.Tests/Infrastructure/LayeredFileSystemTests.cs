using NSubstitute;

namespace DynamicControls.Core.Tests.Infrastructure;

public class LayeredFileSystemTests
{
    private const string RootDir = @"C:\plugin";
    private readonly IFileSystem _fs = TestFs.Create();

    private LayeredFileSystem Lfs => new(RootDir, _fs);

    [Fact]
    public void Resolve_UserFileExists_ReturnsUserRelativePath()
    {
        string userPath = Path.Combine(RootDir, "User", "Platforms", "Sega Genesis", "Controllers.xml");
        _fs.FileExists(userPath).Returns(true);

        Lfs.Resolve("Platforms", "Sega Genesis", "Controllers.xml")
            .ShouldBe(Path.Combine("User", "Platforms", "Sega Genesis", "Controllers.xml"));
    }

    [Fact]
    public void Resolve_UserFileAbsentDefaultsFileExists_ReturnsDefaultsRelativePath()
    {
        string userPath = Path.Combine(RootDir, "User", "Platforms", "Sega Genesis", "Controllers.xml");
        string defaultsPath = Path.Combine(RootDir, "Defaults", "Platforms", "Sega Genesis", "Controllers.xml");
        _fs.FileExists(userPath).Returns(false);
        _fs.FileExists(defaultsPath).Returns(true);

        Lfs.Resolve("Platforms", "Sega Genesis", "Controllers.xml")
            .ShouldBe(Path.Combine("Defaults", "Platforms", "Sega Genesis", "Controllers.xml"));
    }

    [Fact]
    public void Resolve_NeitherFileExists_ReturnsNull()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(false);

        Lfs.Resolve("Platforms", "Sega Genesis", "Controllers.xml").ShouldBeNull();
    }

    [Fact]
    public void FileExists_RootsRelativePathAgainstRootDir()
    {
        string absolutePath = Path.Combine(RootDir, "Defaults", "GlobalConfig.xml");
        _fs.FileExists(absolutePath).Returns(true);

        Lfs.FileExists(Path.Combine("Defaults", "GlobalConfig.xml")).ShouldBeTrue();
    }

    [Fact]
    public void Defaults_RootsPathOneLevelIntoDefaultsLayer()
    {
        string absolutePath = Path.Combine(RootDir, "Defaults", "GlobalConfig.xml");
        _fs.FileExists(absolutePath).Returns(true);

        Lfs.Defaults.FileExists("GlobalConfig.xml").ShouldBeTrue();
    }

    [Fact]
    public void User_RootsPathOneLevelIntoUserLayer()
    {
        string absolutePath = Path.Combine(RootDir, "User", "GlobalConfig.xml");
        _fs.FileExists(absolutePath).Returns(true);

        Lfs.User.FileExists("GlobalConfig.xml").ShouldBeTrue();
    }
}
