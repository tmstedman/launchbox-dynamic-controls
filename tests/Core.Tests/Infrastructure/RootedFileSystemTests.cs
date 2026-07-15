using NSubstitute;

namespace DynamicControls.Core.Tests.Infrastructure;

public class RootedFileSystemTests
{
    private const string RootDir = @"C:\plugin";
    private readonly IFileSystem _inner = TestFs.Create();
    private readonly RootedFileSystem _underTest;

    public RootedFileSystemTests()
    {
        _underTest = new RootedFileSystem(RootDir, _inner);
    }

    [Fact]
    public void FullPath_JoinsPathOntoRootDir()
    {
        _underTest.FullPath(Path.Combine("Static", "OutRun.png"))
            .ShouldBe(Path.Combine(RootDir, "Static", "OutRun.png"));
    }

    [Fact]
    public void FileExists_JoinsPathOntoRootDir()
    {
        string rooted = Path.Combine(RootDir, "Defaults", "GlobalConfig.xml");
        _inner.FileExists(rooted).Returns(true);

        _underTest.FileExists(Path.Combine("Defaults", "GlobalConfig.xml")).ShouldBeTrue();
    }

    [Fact]
    public void OpenRead_JoinsPathOntoRootDir()
    {
        string rooted = Path.Combine(RootDir, "controls.xml");
        var stream = new MemoryStream();
        _inner.OpenRead(rooted).Returns(stream);

        _underTest.OpenRead("controls.xml").ShouldBeSameAs(stream);
    }

    [Fact]
    public void ReadAllText_JoinsPathOntoRootDir()
    {
        string rooted = Path.Combine(RootDir, "Logs", "debug.log");
        _inner.ReadAllText(rooted).Returns("log contents");

        _underTest.ReadAllText(Path.Combine("Logs", "debug.log")).ShouldBe("log contents");
    }

    [Fact]
    public void AppendAllText_JoinsPathOntoRootDir()
    {
        string rooted = Path.Combine(RootDir, "Logs", "debug.log");

        _underTest.AppendAllText(Path.Combine("Logs", "debug.log"), "line");

        _inner.Received(1).AppendAllText(rooted, "line");
    }

    [Fact]
    public void DeleteFile_JoinsPathOntoRootDir()
    {
        string rooted = Path.Combine(RootDir, "Logs", "debug.log");

        _underTest.DeleteFile(Path.Combine("Logs", "debug.log"));

        _inner.Received(1).DeleteFile(rooted);
    }

    [Fact]
    public void DirectoryExists_JoinsPathOntoRootDir()
    {
        string rooted = Path.Combine(RootDir, "Logs");
        _inner.DirectoryExists(rooted).Returns(true);

        _underTest.DirectoryExists("Logs").ShouldBeTrue();
    }

    [Fact]
    public void CreateDirectory_JoinsPathOntoRootDir()
    {
        string rooted = Path.Combine(RootDir, "Logs");

        _underTest.CreateDirectory("Logs");

        _inner.Received(1).CreateDirectory(rooted);
    }
}
