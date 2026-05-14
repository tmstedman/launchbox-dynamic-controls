using DynamicControls.Plugins.RetroArch;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Unit tests for <see cref="RetroArchCoreInfo"/>. The reader probes two locations
/// (<c>info/{dll}.info</c> then <c>cores/{dll}.info</c>), parses the <c>corename = ...</c>
/// line out of the first file it finds, and returns the value with surrounding quotes
/// stripped. Filesystem is substituted so each test supplies exact file presence and content.
/// </summary>
public class RetroArchCoreInfoTests
{
    private const string RetroArchDir = @"C:\RetroArch";
    private const string CoreDll = "genesis_plus_gx_libretro";

    private static readonly string InfoPath = Path.Combine(RetroArchDir, "info", $"{CoreDll}.info");
    private static readonly string CoresPath = Path.Combine(RetroArchDir, "cores", $"{CoreDll}.info");

    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IFileSystem _fs = TestFs.Create();
    private readonly RetroArchCoreInfo _underTest;

    public RetroArchCoreInfoTests()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(false);
        _underTest = new RetroArchCoreInfo(_fs, _logger);
    }

    private void StubInfo(string path, string content)
    {
        _fs.FileExists(path).Returns(true);
        _fs.ReadAllText(path).Returns(content);
    }

    // ---- file location ----

    [Fact]
    public void ReadDisplayName_FileInInfoDir_IsRead()
    {
        // given the .info file lives under info/ (the canonical RetroArch layout)
        StubInfo(InfoPath, """corename = "Genesis Plus GX" """);

        // when the reader runs
        var result = _underTest.ReadDisplayName(RetroArchDir, CoreDll);

        // then the corename value is returned
        result.ShouldBe("Genesis Plus GX");
    }

    [Fact]
    public void ReadDisplayName_FileInCoresDir_IsReadAsFallback()
    {
        // given no info/ entry but a cores/ entry (older RetroArch layout)
        StubInfo(CoresPath, """corename = "Genesis Plus GX" """);

        // when the reader runs
        var result = _underTest.ReadDisplayName(RetroArchDir, CoreDll);

        // then cores/ supplies the value when info/ is absent
        result.ShouldBe("Genesis Plus GX");
    }

    [Fact]
    public void ReadDisplayName_BothLocationsPresent_InfoWins()
    {
        // given both info/ and cores/ contain entries for the same dll
        StubInfo(InfoPath, """corename = "From Info" """);
        StubInfo(CoresPath, """corename = "From Cores" """);

        // when the reader runs
        var result = _underTest.ReadDisplayName(RetroArchDir, CoreDll);

        // then info/ takes priority (probed first in iteration order)
        result.ShouldBe("From Info");
        _fs.DidNotReceive().ReadAllText(CoresPath);
    }

    [Fact]
    public void ReadDisplayName_NeitherLocationPresent_ReturnsNull()
    {
        // given no .info file exists in either directory (default state)

        // when the reader runs
        var result = _underTest.ReadDisplayName(RetroArchDir, CoreDll);

        // then null is returned and no read is attempted
        result.ShouldBeNull();
        _fs.DidNotReceive().ReadAllText(Arg.Any<string>());
    }

    // ---- corename parsing ----

    [Fact]
    public void ReadDisplayName_NoCorenameLine_ReturnsNull()
    {
        // given an info file present but missing the corename key
        StubInfo(InfoPath, """
            display_name = "Genesis Plus GX"
            categories = "Emulator"
            """);

        // when the reader runs
        var result = _underTest.ReadDisplayName(RetroArchDir, CoreDll);

        // then null is returned — corename is the only key we care about
        result.ShouldBeNull();
    }

    [Fact]
    public void ReadDisplayName_StripsSurroundingQuotes()
    {
        // given a corename value wrapped in double quotes (RetroArch's normal convention)
        StubInfo(InfoPath, """corename = "Genesis Plus GX" """);

        // when the reader runs
        var result = _underTest.ReadDisplayName(RetroArchDir, CoreDll);

        // then the surrounding quotes are stripped
        result.ShouldBe("Genesis Plus GX");
    }

    [Fact]
    public void ReadDisplayName_UnquotedValue_IsReturnedAsIs()
    {
        // given a corename value without quotes (some .info files omit them)
        StubInfo(InfoPath, "corename = Genesis Plus GX");

        // when the reader runs
        var result = _underTest.ReadDisplayName(RetroArchDir, CoreDll);

        // then the value is returned with whitespace trimmed
        result.ShouldBe("Genesis Plus GX");
    }

    [Fact]
    public void ReadDisplayName_FindsCorenameAmongOtherKeys()
    {
        // given a realistic info file with multiple keys
        StubInfo(InfoPath, """
            display_name = "Sega - MS/GG/MD/CD (Genesis Plus GX)"
            categories = "Emulator"
            corename = "Genesis Plus GX"
            manufacturer = "Sega"
            systemname = "Mega Drive"
            """);

        // when the reader runs
        var result = _underTest.ReadDisplayName(RetroArchDir, CoreDll);

        // then corename is located among the surrounding metadata
        result.ShouldBe("Genesis Plus GX");
    }

    [Fact]
    public void ReadDisplayName_CorenameLineWithNoEquals_IsSkippedAndNextLineUsed()
    {
        // given a file where the first corename line has no '=' separator (malformed) followed
        // by a valid one — the if (eq >= 0) guard must skip the malformed line rather than
        // indexing at -1+1=0 and returning the whole trimmed string
        StubInfo(InfoPath, "corename\ncorename = \"Genesis Plus GX\"");

        var result = _underTest.ReadDisplayName(RetroArchDir, CoreDll);

        result.ShouldBe("Genesis Plus GX");
    }

    [Fact]
    public void ReadDisplayName_StopsAtFirstCorenameMatch()
    {
        // given a malformed file with two corename lines — the first match wins
        StubInfo(InfoPath, """
            corename = "First"
            corename = "Second"
            """);

        // when the reader runs
        var result = _underTest.ReadDisplayName(RetroArchDir, CoreDll);

        // then the first occurrence is returned (the reader returns on first hit)
        result.ShouldBe("First");
    }
}
