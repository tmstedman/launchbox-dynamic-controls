using DynamicControls.Plugins.RetroArch;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Unit tests for <see cref="RetroArchConfigFileReader"/>. Covers: (1) missing file returns null;
/// (2) read failure logs error and returns null; (3) comment, blank, and malformed lines are
/// ignored; (4) whitespace trimmed from keys and values; (5) surrounding quotes stripped from
/// values; (6) first '=' is the key/value separator; (7) later duplicate key overwrites earlier.
/// </summary>
public class RetroArchConfigFileReaderTests
{
    private const string FilePath = @"C:\RetroArch\config\Core\game.cfg";

    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IFileSystem _fs = TestFs.Create();
    private readonly RetroArchConfigFileReader _underTest;

    public RetroArchConfigFileReaderTests()
    {
        _underTest = new RetroArchConfigFileReader(_logger, _fs);
    }

    private void StubFile(string content)
    {
        _fs.FileExists(FilePath).Returns(true);
        _fs.ReadAllText(FilePath).Returns(content);
    }

    [Fact]
    public void LoadConfigFile_MissingFile_ReturnsNull()
    {
        _underTest.LoadConfigFile(FilePath).ShouldBeNull();
    }

    [Fact]
    public void LoadConfigFile_ReadFailure_LogsErrorAndReturnsNull()
    {
        _fs.FileExists(FilePath).Returns(true);
        _fs.ReadAllText(FilePath).Returns(_ => throw new IOException("locked"));

        _underTest.LoadConfigFile(FilePath).ShouldBeNull();

        _logger.Received().Error(Arg.Is<string>(s => s.Contains("read failed") && s.Contains(FilePath)));
    }

    [Fact]
    public void LoadConfigFile_IgnoresCommentsAndBlanksAndMalformedLines()
    {
        StubFile("""
            # this is a comment
            key_a = "0"

            this line has no equals sign at all
            # key_b = "1"
            key_b = "1"
            """);

        _underTest.LoadConfigFile(FilePath).ShouldBeDictionaryOf(
            ("key_a", "0"),
            ("key_b", "1"));
    }

    [Fact]
    public void LoadConfigFile_StripsWhitespaceAndSurroundingQuotes()
    {
        StubFile("""
              key_a   =   "0"
            key_b=1
            key_c = "padded value"
            """);

        _underTest.LoadConfigFile(FilePath).ShouldBeDictionaryOf(
            ("key_a", "0"),
            ("key_b", "1"),
            ("key_c", "padded value"));
    }

    [Fact]
    public void LoadConfigFile_FirstEqualsIsSeparator_RestIsValue()
    {
        StubFile("""video_shader = "foo=bar=baz" """);

        _underTest.LoadConfigFile(FilePath).ShouldBeDictionaryOf(("video_shader", "foo=bar=baz"));
    }

    [Fact]
    public void LoadConfigFile_LaterKeyInSameFile_Overwrites()
    {
        StubFile("""
            key_a = "0"
            key_a = "7"
            """);

        _underTest.LoadConfigFile(FilePath).ShouldBeDictionaryOf(("key_a", "7"));
    }
}
